using System;
using Knockbox.Simulation;
using UnityEngine;

namespace Knockbox.Game
{
    /// <summary>
    /// 틱 실행기(01-architecture.md 5절). Update에서 실제 경과 시간을 누적해 1/60초마다 SimWorld.Step을 부른다.
    /// 한 프레임에 최대 4틱까지만 따라잡는다. 연출은 PreviousState, World.State, InterpolationAlpha로 보간한다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class MatchRunner : MonoBehaviour
    {
        [SerializeField] MovementTuning movementTuning;
        [SerializeField] CombatTuning combatTuning;
        [Tooltip("F11로 순환하는 전투 튜닝 프리셋(기본, 더 빠르게)")]
        [SerializeField] CombatTuning[] combatPresets;
        [Tooltip("시작 폼과 기능 스위치(CR-003): F1 폼, F4 가드·잡기, Insert 에너지. 다음 StartNewMatch부터 적용")]
        [SerializeField] RuleSet rules = RuleSet.Default.WithOwned(Forms.AllPlayable);
        [SerializeField] TextAsset[] arenaFiles;
        [Tooltip("맵 짝 파일(<이름>.json): 테마, 광원, 순환 포함 여부")]
        [SerializeField] TextAsset[] arenaMetaFiles;
        [Tooltip("Rotation이면 라운드마다 맵 교체(순환 맵 전부), 맵 이름이면 그 맵에 고정(F2)")]
        [SerializeField] string arenaName = RotationName;
        [SerializeField] uint seed = 1;
        [Tooltip("켜면 StartNewMatch마다 시드를 새로 뽑는다(맵 순서가 경기마다 달라짐)")]
        [SerializeField] bool randomizeSeed;
        [SerializeField, Range(0, InputDelayQueue.MaxDelay)] int inputDelayTicks;
        [SerializeField] bool startOnAwake = true;
        [SerializeField] bool skipCountdown;
        [Tooltip("켜면 경기 시작 때 P1 키보드 왼쪽, P2 첫 게임패드(없으면 키보드 오른쪽)를 붙인다")]
        [SerializeField] bool useLocalDevices;

        readonly FixedTickClock clock = new FixedTickClock();
        readonly InputDelayQueue delay1 = new InputDelayQueue();
        readonly InputDelayQueue delay2 = new InputDelayQueue();
        SyncTest<SimWorld> syncTest;
        double lastRealtime;
        /// <summary>StartNewMatch로 튜닝 에셋에서 만든 경기만 라운드 사이에 튜닝을 다시 읽는다(시나리오 덮어쓰기 보존).</summary>
        bool configFromTuning;

        public SimWorld World { get; private set; }
        public SimState PreviousState { get; private set; }
        public float InterpolationAlpha => clock.Alpha;
        public FixedTickClock Clock => clock;

        /// <summary>Begin 이후 실행한 틱 수.</summary>
        public long TicksRun { get; private set; }

        /// <summary>false면 시간이 흘러도 틱을 실행하지 않는다(일시정지).</summary>
        public bool Running { get; set; }

        /// <summary>0보다 크면 그 틱 수에 도달했을 때 자동으로 멈춘다. 시나리오 재생용.</summary>
        public int StopAtTick { get; set; }

        public IInputSource Player1Input { get; set; } = new IdleInputSource();
        public IInputSource Player2Input { get; set; } = new IdleInputSource();

        /// <summary>null이 아니면 매 틱 실제로 쓰인(지연이 적용된) 입력을 덧붙인다.</summary>
        public InputRecording Recording { get; private set; }

        public SyncTest<SimWorld> SyncTest => syncTest;

        /// <summary>매 틱 Step 직후. 연출이 World.Events를 소비하는 지점이다.</summary>
        public event Action<MatchRunner> Ticked;

        /// <summary>Begin 직후. 연출이 맵을 다시 그리는 지점이다.</summary>
        public event Action<MatchRunner> MatchBegan;

        /// <summary>
        /// 멈춰 있으면 1(현재 상태를 그대로 그림), 진행 중이면 다음 틱까지의 비율.
        /// 히트스탑 중 보간 금지는 연출 쪽에서 처리한다(CP4).
        /// </summary>
        public float RenderAlpha => Running ? clock.Alpha : 1f;

        /// <summary>arenaName이 이 값이면 순환 맵 전부를 라운드마다 바꿔 가며 쓴다(04-arena.md 6절).</summary>
        public const string RotationName = "Rotation";

        public string ArenaName
        {
            get => arenaName;
            set => arenaName = value;
        }

        public uint Seed
        {
            get => seed;
            set => seed = value;
        }

        public MovementTuning Tuning
        {
            get => movementTuning;
            set => movementTuning = value;
        }

        public CombatTuning CombatTuning
        {
            get => combatTuning;
            set => combatTuning = value;
        }

        public string PresetName => combatTuning != null && !string.IsNullOrEmpty(combatTuning.PresetName) ? combatTuning.PresetName : "default";

        /// <summary>F11: 다음 전투 튜닝 프리셋으로 바꾼다(다음 StartNewMatch부터).</summary>
        public void CyclePreset()
        {
            if (combatPresets == null || combatPresets.Length == 0)
                return;
            int index = System.Array.IndexOf(combatPresets, combatTuning);
            combatTuning = combatPresets[(index + 1) % combatPresets.Length];
        }

        /// <summary>재생 중인 입력 기록(F10). 끝나면 최종 체크섬을 기록 시점과 비교해 로그로 남긴다.</summary>
        public InputRecording Playback { get; private set; }

        /// <summary>
        /// 입력 기록을 처음부터 재생한다. 지금 튜닝과 기록의 모드로 설정을 만들고, 설정·맵 체크섬이 다르면 경고한다.
        /// </summary>
        public bool PlayRecording(InputRecording recording)
        {
            if (recording == null)
                return false;
            var config = FindRecordedConfig(recording);
            if (config == null)
            {
                Debug.LogWarning("[Knockbox] 기록과 같은 튜닝 설정을 찾지 못해 재생 결과가 다를 수 있음");
                config = SimConfigFactory.Create(movementTuning, combatTuning, RecordedRuleCandidates(recording.Mode)[0]);
            }
            string[] names = recording.ArenaNames;
            var arenas = new ArenaDef[names.Length];
            for (int i = 0; i < names.Length; i++)
                arenas[i] = ResolveArena(names[i]);
            if (InputRecording.ComputeArenaChecksum(arenas) != recording.ArenaChecksum)
                Debug.LogWarning("[Knockbox] 기록과 맵이 달라 재생 결과가 다를 수 있음");
            var p1 = new InputFrame[recording.Count];
            var p2 = new InputFrame[recording.Count];
            for (int i = 0; i < recording.Count; i++)
            {
                p1[i] = recording.GetP1(i);
                p2[i] = recording.GetP2(i);
            }
            Begin(config, arenas, recording.BuildState(config, arenas));
            // 기록된 입력은 이미 지연을 거친 값이다.
            delay1.Reset(0);
            delay2.Reset(0);
            Player1Input = new FrameArrayInputSource(p1);
            Player2Input = new FrameArrayInputSource(p2);
            Playback = recording;
            return true;
        }

        /// <summary>지금 프리셋들과 기록의 규칙 후보 중에서 기록의 설정 체크섬과 같은 설정을 찾는다. 없으면 null.</summary>
        SimConfig FindRecordedConfig(InputRecording recording)
        {
            var candidates = RecordedRuleCandidates(recording.Mode);
            for (int p = -1; p < (combatPresets != null ? combatPresets.Length : 0); p++)
            {
                var tuning = p < 0 ? combatTuning : combatPresets[p];
                if (p >= 0 && (tuning == null || tuning == combatTuning))
                    continue;
                foreach (var candidate in candidates)
                {
                    var config = SimConfigFactory.Create(movementTuning, tuning, candidate);
                    if (config.ComputeChecksum() == recording.ConfigChecksum)
                        return config;
                }
            }
            return null;
        }

        /// <summary>
        /// 기록의 규칙 문자열(RuleSet.Format) → 후보 하나. CP16까지의 모드 이름("Charge" 등)이면 레거시 규칙의 잡기 켬·끔 두 후보.
        /// </summary>
        RuleSet[] RecordedRuleCandidates(string recorded)
        {
            if (RuleSet.TryParse(recorded, out var parsed))
                return new[] { parsed };
            var legacy = System.Enum.TryParse(recorded, out CombatMode recordedMode) ? recordedMode : Mode;
            return new[] { RuleSet.Legacy(legacy, rules.Switches.GrabEnabled), RuleSet.Legacy(legacy, !rules.Switches.GrabEnabled) };
        }

        /// <summary>시작 폼과 기능 스위치. 다음 StartNewMatch부터 적용된다.</summary>
        public RuleSet Rules
        {
            get => rules;
            set => rules = value;
        }

        /// <summary>CP16까지의 전역 모드(레거시 테스트용). 설정하면 레거시 스위치(가드 켬, 에너지 끔)로 바뀐다.</summary>
        public CombatMode Mode
        {
            get => rules.StartForm == FighterForm.Scissors ? CombatMode.Charge : rules.StartForm == FighterForm.Paper ? CombatMode.Swing : CombatMode.Test;
            set => rules = RuleSet.Legacy(value, rules.Switches.GrabEnabled);
        }

        /// <summary>다음 StartNewMatch부터 적용된다.</summary>
        public bool GrabEnabled
        {
            get => rules.Switches.GrabEnabled;
            set => rules.Switches.GrabEnabled = value;
        }

        public int InputDelayTicks
        {
            get => inputDelayTicks;
            set
            {
                inputDelayTicks = value;
                delay1.Reset(value);
                delay2.Reset(value);
            }
        }

        void Awake()
        {
            if (startOnAwake && World == null)
                StartNewMatch();
        }

        /// <summary>튜닝을 다시 읽어 새 경기를 시작한다.</summary>
        public void StartNewMatch()
        {
            var config = CreateConfig();
            var arenas = ResolveArenas(arenaName);
            var phase = skipCountdown ? MatchPhase.Fighting : MatchPhase.Countdown;
            if (randomizeSeed)
                seed = (uint)Environment.TickCount | 1u;
            if (useLocalDevices)
            {
                Player1Input = new KeyboardInputSource(KeyboardLayout.Left);
                Player2Input = GamepadInputSource.Available(0) ? new GamepadInputSource(0) : new KeyboardInputSource(KeyboardLayout.Right);
            }
            Begin(config, arenas, SimState.CreateMatch(config, arenas, seed, phase));
            configFromTuning = true;
        }

        /// <summary>RotationName이면 순환 맵 전부(이름순), 아니면 그 맵 하나.</summary>
        public ArenaDef[] ResolveArenas(string name)
        {
            if (name != RotationName)
                return new[] { ResolveArena(name) };
            string[] names = RotationNames();
            if (names.Length == 0)
                throw new InvalidOperationException("순환에 들어간 맵이 없음(짝 파일의 \"rotation\": true)");
            var arenas = new ArenaDef[names.Length];
            for (int i = 0; i < names.Length; i++)
                arenas[i] = ResolveArena(names[i]);
            return arenas;
        }

        /// <summary>짝 파일에서 "rotation": true인 맵 이름(이름순).</summary>
        public string[] RotationNames()
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (string name in MetaNames())
            {
                if (GetMeta(name).Rotation)
                    names.Add(name);
            }
            names.Sort(StringComparer.Ordinal);
            return names.ToArray();
        }

        /// <summary>F2 순환: Rotation → 순환 맵 → 나머지 맵(v1 테스트 고정 자료).</summary>
        public string[] ArenaChoices()
        {
            var choices = new System.Collections.Generic.List<string> { RotationName };
            choices.AddRange(RotationNames());
            var rest = new System.Collections.Generic.List<string>();
            if (arenaFiles != null)
            {
                foreach (var file in arenaFiles)
                {
                    if (file != null && !choices.Contains(file.name))
                        rest.Add(file.name);
                }
            }
            rest.Sort(StringComparer.Ordinal);
            choices.AddRange(rest);
            return choices.ToArray();
        }

        readonly System.Collections.Generic.Dictionary<string, ArenaMeta> metaCache = new System.Collections.Generic.Dictionary<string, ArenaMeta>();

        /// <summary>맵 짝 파일. 없으면 ArenaMeta.Default.</summary>
        public ArenaMeta GetMeta(string name)
        {
            if (metaCache.TryGetValue(name, out var cached))
                return cached;
            ArenaMeta meta = null;
            if (arenaMetaFiles != null)
            {
                foreach (var file in arenaMetaFiles)
                {
                    if (file != null && file.name == name)
                        meta = ArenaMeta.Parse(file.text, name + ".json");
                }
            }
#if UNITY_EDITOR
            if (meta == null)
            {
                string path = System.IO.Path.Combine(ArenaFiles.Folder, name + ".json");
                if (System.IO.File.Exists(path))
                    meta = ArenaMeta.Parse(System.IO.File.ReadAllText(path), name + ".json");
            }
#endif
            meta ??= ArenaMeta.Default;
            metaCache[name] = meta;
            return meta;
        }

        System.Collections.Generic.IEnumerable<string> MetaNames()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            if (arenaMetaFiles != null)
            {
                foreach (var file in arenaMetaFiles)
                {
                    if (file != null && seen.Add(file.name))
                        yield return file.name;
                }
            }
#if UNITY_EDITOR
            foreach (string name in ArenaFiles.ListMetaNames())
            {
                if (seen.Add(name))
                    yield return name;
            }
#endif
        }

        public SimConfig CreateConfig() => SimConfigFactory.Create(movementTuning, combatTuning, rules);

        /// <summary>등록된 맵 텍스트에서 찾고, 에디터에서는 없으면 디스크에서 읽는다.</summary>
        public ArenaDef ResolveArena(string name)
        {
            if (arenaFiles != null)
            {
                foreach (var file in arenaFiles)
                {
                    if (file != null && file.name == name)
                        return ArenaFiles.WithMeta(ArenaFiles.Parse(file.text, name), GetMeta(name));
                }
            }
#if UNITY_EDITOR
            return ArenaFiles.LoadByName(name);
#else
            throw new InvalidOperationException($"MatchRunner에 등록되지 않은 맵: {name}");
#endif
        }

        public void Begin(SimConfig config, ArenaDef arena, in SimState initial) => Begin(config, new[] { arena }, initial);

        public void Begin(SimConfig config, ArenaDef[] arenas, in SimState initial)
        {
            World = new SimWorld(config, arenas, initial);
            PreviousState = initial;
            configFromTuning = false;
            Playback = null;
            delay1.Reset(inputDelayTicks);
            delay2.Reset(inputDelayTicks);
            clock.Reset();
            lastRealtime = Time.realtimeSinceStartupAsDouble;
            TicksRun = 0;
            StopAtTick = 0;
            Recording = null;
            if (syncTest != null)
                syncTest = new SyncTest<SimWorld>(World);
            Running = true;
            MatchBegan?.Invoke(this);
        }

        /// <summary>F8. 켜면 매 틱 7틱 되감기 검사를 하고 불일치를 오류 로그로 남긴다.</summary>
        public void SetSyncTest(bool enabled, int rollbackTicks = SyncTest<SimWorld>.DefaultRollbackTicks)
        {
            syncTest = enabled && World != null ? new SyncTest<SimWorld>(World, rollbackTicks) : null;
        }

        public void StartRecording(string mode)
        {
            if (World == null)
                return;
            var s = World.State;
            // 기록은 현재 상태를 시작점으로 삼을 수 없으므로(라운드 생성 값만 저장) 라운드 시작 직후에만 의미가 있다.
            var arenas = new ArenaDef[World.ArenaCount];
            for (int i = 0; i < arenas.Length; i++)
                arenas[i] = World.GetArena(i);
            Recording = InputRecording.Begin(World.Config, arenas, s.Match.Rotation, s.Rng.State, s.Fighter0.Position, s.Fighter1.Position, s.Match.Phase, mode);
            if (Recording.InitialChecksum != World.ComputeChecksum())
            {
                Debug.LogWarning("[Knockbox] 입력 기록은 라운드 시작 상태에서만 시작할 수 있음");
                Recording = null;
            }
        }

        public InputRecording StopRecording()
        {
            var rec = Recording;
            Recording = null;
            rec?.Finish(World.ComputeChecksum());
            return rec;
        }

        void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            double elapsed = now - lastRealtime;
            lastRealtime = now;
            if (!Running || World == null)
                return;

            int ticks = clock.Advance(elapsed);
            for (int i = 0; i < ticks && Running; i++)
                StepOnce();
        }

        /// <summary>한 틱 진행한다. 일시정지 중 한 틱 진행(O)에도 쓴다.</summary>
        public void StepOnce()
        {
            if (World == null)
                return;
            int tick = World.State.Tick;
            var p1 = delay1.Push(Player1Input != null ? Player1Input.Poll(tick) : default);
            var p2 = delay2.Push(Player2Input != null ? Player2Input.Poll(tick) : default);
            Recording?.Append(p1, p2);

            PreviousState = World.State;
            if (syncTest != null)
            {
                if (!syncTest.Advance(p1, p2))
                    Debug.LogError("[Knockbox] " + syncTest.LastMismatch);
            }
            else
            {
                World.Step(p1, p2);
            }
            TicksRun++;
            bool roundTransition = false;
            for (int i = 0; i < World.Events.Count; i++)
            {
                ref readonly var e = ref World.Events[i];
                if (e.Type == SimEventType.OutOfBounds)
                    Debug.LogWarning($"[Knockbox] 경기장 밖으로 나가 되돌림(버그 신호): {e}");
                else if (e.Type == SimEventType.RoundTransition)
                    roundTransition = true;
            }
            if (World.Events.Dropped > 0)
                Debug.LogWarning($"[Knockbox] 이벤트 버퍼가 넘쳐 {World.Events.Dropped}개를 버림(틱 {World.State.Tick})");
            Ticked?.Invoke(this);
            if (roundTransition)
                ApplyTuningAtRoundTransition();

            if (Playback != null && TicksRun == Playback.Count)
            {
                bool same = !Playback.HasFinalChecksum || World.ComputeChecksum() == Playback.FinalChecksum;
                if (same)
                    Debug.Log($"[Knockbox] 기록 재생 끝({Playback.Count}틱): 최종 체크섬이 기록과 같음");
                else
                    Debug.LogWarning($"[Knockbox] 기록 재생 끝({Playback.Count}틱): 최종 체크섬이 기록과 다름");
            }

            if (StopAtTick > 0 && World.State.Tick >= StopAtTick)
                Running = false;
        }

        /// <summary>
        /// RoundTransition 훅: 튜닝을 다시 읽어 다음 라운드부터 반영한다(06-match-and-tools.md 1절).
        /// 바뀐 것이 없으면 그대로 둔다. 바뀌면 입력 기록과 SyncTest는 이 지점 이전과 설정이 달라진다.
        /// </summary>
        void ApplyTuningAtRoundTransition()
        {
            if (!configFromTuning)
                return;
            var config = CreateConfig();
            if (config.ComputeChecksum() == World.Config.ComputeChecksum())
                return;
            World.ReplaceConfig(config);
            Debug.Log("[Knockbox] 다음 라운드부터 바뀐 튜닝을 적용함");
            if (Recording != null)
                Debug.LogWarning("[Knockbox] 기록 도중 설정이 바뀌어 이 입력 기록은 재생 결과가 달라질 수 있음");
            if (syncTest != null)
                syncTest = new SyncTest<SimWorld>(World, syncTest.RollbackTicks);
        }
    }
}

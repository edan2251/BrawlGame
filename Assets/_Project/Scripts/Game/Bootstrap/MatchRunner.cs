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
        [SerializeField] uint seed = 1;
        [SerializeField, Range(0, InputDelayQueue.MaxDelay)] int inputDelayTicks;
        [SerializeField] bool startOnAwake = true;
        [SerializeField] bool skipCountdown;

        readonly FixedTickClock clock = new FixedTickClock();
        readonly InputDelayQueue delay1 = new InputDelayQueue();
        readonly InputDelayQueue delay2 = new InputDelayQueue();
        SyncTest<SimWorld> syncTest;
        double lastRealtime;

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

        public MovementTuning Tuning
        {
            get => movementTuning;
            set => movementTuning = value;
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
            var config = SimConfigFactory.Create(movementTuning);
            var phase = skipCountdown ? MatchPhase.Fighting : MatchPhase.Countdown;
            var initial = SimState.CreateRound(config, seed, TempArena.DefaultSpawn(config, 0), TempArena.DefaultSpawn(config, 1), phase);
            Begin(config, initial);
        }

        public void Begin(SimConfig config, in SimState initial)
        {
            World = new SimWorld(config, initial);
            PreviousState = initial;
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
        }

        /// <summary>F8. 켜면 매 틱 7틱 되감기 검사를 하고 불일치를 오류 로그로 남긴다.</summary>
        public void SetSyncTest(bool enabled, int rollbackTicks = SyncTest<SimWorld>.DefaultRollbackTicks)
        {
            syncTest = enabled && World != null ? new SyncTest<SimWorld>(World, rollbackTicks) : null;
        }

        public void StartRecording(string arena, string mode)
        {
            if (World == null)
                return;
            var s = World.State;
            // 기록은 현재 상태를 시작점으로 삼을 수 없으므로(라운드 생성 값만 저장) 라운드 시작 직후에만 의미가 있다.
            Recording = InputRecording.Begin(World.Config, s.Rng.State, s.Fighter0.Position, s.Fighter1.Position, s.Match.Phase, arena, mode);
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
            Ticked?.Invoke(this);

            if (StopAtTick > 0 && World.State.Tick >= StopAtTick)
                Running = false;
        }
    }
}

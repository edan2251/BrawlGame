using Knockbox.Presentation;
using Knockbox.Simulation;
using UnityEngine;

namespace Knockbox.Game
{
    /// <summary>
    /// MatchRunner의 상태와 이벤트를 연출 계층에 넘긴다. 연출은 상태를 읽기만 하고 시뮬레이션에 쓰지 않는다.
    /// 연출 시간은 시뮬레이션 시간((틱 + 보간 비율) / 60)으로 진행한다. 멈추면 연출도 멈추고,
    /// 캡처처럼 한 틱씩 진행해도 실제 플레이와 같은 모습이 나온다.
    /// 타격 연출(05-presentation.md 4절)은 여러 연출을 같은 순간에 겹친다: 번쩍임, 떨림, 파편, 고리, 흔들림, 확대, 효과음.
    /// </summary>
    [RequireComponent(typeof(MatchRunner))]
    public sealed class MatchPresenter : MonoBehaviour
    {
        const int EventQueueCapacity = 256;
        const float MaxFrameDt = 0.1f;
        const float Frame = 1f / 60f;

        [SerializeField] PaletteAsset palette;
        [SerializeField] ShapeLibrary shapes;
        [SerializeField] SfxLibrary sfx;
        [SerializeField] bool showDebugLabels = true;

        MatchRunner runner;
        BackgroundView background;
        PostFx postFx;
        ArenaView arenaView;
        PropView propView;
        ProjectileView projectileView;
        PropShadowView propShadows;
        BoundaryView boundaryView;
        FighterView fighter0;
        FighterView fighter1;
        DustPool dust;
        ImpactFx fx;
        HudView hud;
        SfxPlayer sfxPlayer;
        CameraRig cameraRig;
        DebugStateLabels labels;
        /// <summary>디버그 글자(상태, 틱, 설정 줄). 빌드에서는 기본으로 숨기고 F12로 켠다(08 6절). 에디터는 켠 채 시작.</summary>
        bool debugText;
        bool fighterLabelsAllowed = true;
        HitboxOverlay overlay;
        HelpOverlay help;
        KoShatter koShatter;
        readonly bool[] shattered = new bool[2];
        int countdownKey = -1;

        readonly SimEvent[] queue = new SimEvent[EventQueueCapacity];
        int queued;
        double lastSimTime;
        int renderedRound;
        /// <summary>지금 그려진 맵. 시뮬레이션의 현재 맵(라운드마다 바뀔 수 있다)과 다르면 다시 그린다.</summary>
        ArenaDef builtArena;

        // 디버그 글자 아래줄: 바뀔 때만 새로 만든다.
        string footer;
        string footerArena;
        SimConfig footerConfig;
        IInputSource footerP2;
        DummyBehavior footerDummy;
        string footerPreset;
        bool footerRecording;
        bool footerPlayback;

        public PaletteAsset Palette
        {
            get => palette;
            set => palette = value;
        }

        public FighterView Fighter(int index) => index == 0 ? fighter0 : fighter1;
        public DustPool Dust => dust;
        public ImpactFx Fx => fx;
        public CameraRig CameraRig => cameraRig;
        public SfxPlayer SfxPlayer => sfxPlayer;
        public bool OverlayVisible => overlay != null && overlay.gameObject.activeSelf;
        public SfxLibrary Library => sfx;
        public HudView Hud => hud;
        public HelpOverlay Help => help;
        public KoShatter KoShatter => koShatter;

        /// <summary>연출 물리(KO 파편)를 켜고 끈다. 시뮬레이션 결과와 무관해야 한다(CP8-5).</summary>
        public bool PresentationPhysics
        {
            get => koShatter == null || koShatter.PhysicsEnabled;
            set
            {
                if (koShatter != null)
                    koShatter.PhysicsEnabled = value;
            }
        }

        /// <summary>마지막 LateUpdate에서 쓴 연출 dt(초).</summary>
        public float LastDt { get; private set; }

        void Awake()
        {
            runner = GetComponent<MatchRunner>();
            if (palette == null)
                palette = ScriptableObject.CreateInstance<PaletteAsset>();
            if (shapes == null)
                shapes = ScriptableObject.CreateInstance<ShapeLibrary>();
            shapes.Activate();

            background = Create<BackgroundView>("Background");
            arenaView = Create<ArenaView>("Arena");
            propView = Create<PropView>("Props");
            propShadows = Create<PropShadowView>("PropShadows");
            propShadows.Setup(palette, ShapeLibrary.ActiveMaterial);
            fighter0 = CreateFighter(0);
            fighter1 = CreateFighter(1);
            dust = Create<DustPool>("Dust");
            dust.Setup(palette, shapes);
            projectileView = Create<ProjectileView>("Projectiles");
            projectileView.Setup(palette, shapes);
            boundaryView = Create<BoundaryView>("Boundary");
            boundaryView.Setup(palette, shapes);
            fx = Create<ImpactFx>("Impact Fx");
            fx.Setup(palette, shapes);
            koShatter = Create<KoShatter>("KO Shatter");
            koShatter.Setup(palette, shapes);
            hud = Create<HudView>("Hud");
            hud.Setup(palette, shapes);
            sfxPlayer = Create<SfxPlayer>("Sfx");
            sfxPlayer.Setup(sfx);
            // 최종 출력 리미터는 AudioListener와 같은 오브젝트에 있어야 전체 소리에 걸린다(F-2).
            var listener = Object.FindFirstObjectByType<AudioListener>();
            if (listener != null && listener.GetComponent<OutputLimiter>() == null)
                listener.gameObject.AddComponent<OutputLimiter>();
            if (showDebugLabels)
            {
                labels = Create<DebugStateLabels>("Debug Labels");
                // 파이터 위 상태 글자와 위 틱 줄은 어두운 배경 위(uiSub), 아래 설정 줄은 바닥 테두리 지형 위(ink).
                labels.Setup(palette.uiSub, palette.ink);
                debugText = Application.isEditor;
            }
            overlay = Create<HitboxOverlay>("Hitbox Overlay");
            overlay.Setup(palette, shapes);
            overlay.gameObject.SetActive(false);
            help = Create<HelpOverlay>("Help");
            help.Setup(palette, shapes);
            help.Visible = false;

            var cam = Camera.main;
            if (cam != null)
            {
                cam.backgroundColor = palette.bgBase;
                cameraRig = cam.GetComponent<CameraRig>() ?? cam.gameObject.AddComponent<CameraRig>();
            }
            postFx = Create<PostFx>("Post Fx");
            postFx.Setup(cam);

            runner.MatchBegan += OnMatchBegan;
            runner.Ticked += OnTicked;
            if (runner.World != null)
                OnMatchBegan(runner);
        }

        void OnDestroy()
        {
            if (runner == null)
                return;
            runner.MatchBegan -= OnMatchBegan;
            runner.Ticked -= OnTicked;
        }

        T Create<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        FighterView CreateFighter(int index)
        {
            var view = Create<FighterView>($"Fighter {index + 1}");
            view.Setup(index, palette, shapes);
            return view;
        }

        /// <summary>시나리오가 정한 테마(0 A 민트, 1 B 사이언, 2 C 슬레이트). -1이면 맵 짝 파일이 정한다.</summary>
        public int ThemeOverride { get; set; } = -1;

        public int ThemeIndex(ArenaDef arena)
        {
            int theme = ThemeOverride >= 0 ? ThemeOverride : runner.GetMeta(arena.Name).Theme;
            return Mathf.Clamp(theme, 0, palette.ThemeCount - 1);
        }

        /// <summary>주 광원 위치: 맵 짝 파일, 없으면 기본값.</summary>
        public Vector2 LightPosition(ArenaDef arena)
        {
            var meta = runner.GetMeta(arena.Name);
            return meta.HasLight ? meta.Light : BackgroundView.DefaultLight(arena);
        }

        public ArenaTheme CurrentTheme { get; private set; }
        public BackgroundView Background => background;
        public PostFx PostFx => postFx;

        /// <summary>맵을 그린다: 테마, 배경(빛과 그림자), 지형, KO 파편 충돌, 카메라 맞춤, HUD 위치.</summary>
        void BuildArena(ArenaDef arena)
        {
            builtArena = arena;
            CurrentTheme = palette.Theme(ThemeIndex(arena));
            background.Build(arena, CurrentTheme, palette, shapes, LightPosition(arena));
            background.ResetMotes();
            arenaView.Build(arena, CurrentTheme, palette, shapes);
            propView.Build(arena, CurrentTheme, shapes);
            propShadows.SetArena(arena, LightPosition(arena));
            boundaryView.SetArena(arena);
            koShatter.BuildArena(arena);
            if (cameraRig != null)
                cameraRig.FitArena(arena);
            hud.SetArena(arena);
        }

        void OnMatchBegan(MatchRunner r)
        {
            BuildArena(r.World.Arena);
            postFx.ResetEffects();
            shattered[0] = shattered[1] = false;
            countdownKey = -1;
            fighter0.ResetPose();
            fighter1.ResetPose();
            dust.Clear();
            fx.Clear();
            overlay.ClearPower();
            if (cameraRig != null)
                cameraRig.ResetEffects();
            renderedRound = r.World.State.Match.Round;
            queued = 0;
            lastSimTime = SimTime();
        }

        /// <summary>틱마다 이벤트를 모아 두었다가 LateUpdate에서 연출로 소비한다.</summary>
        void OnTicked(MatchRunner r)
        {
            var events = r.World.Events;
            for (int i = 0; i < events.Count && queued < EventQueueCapacity; i++)
                queue[queued++] = events[i];
        }

        double SimTime() => (runner.World.State.Tick + (double)runner.RenderAlpha) / SimConfig.TickRate;

        /// <summary>디버그 글자를 끄면 측정(CP3-5)에서 글자 갱신 할당이 빠진다.</summary>
        public void SetDebugLabels(bool visible)
        {
            if (labels != null)
                labels.gameObject.SetActive(visible);
        }

        /// <summary>파이터 위 상태 글자만 켜고 끈다(위·아래 설정 줄은 그대로). 봇 영상은 끈다.</summary>
        public void SetFighterLabels(bool visible) => fighterLabelsAllowed = visible;

        public bool DebugTextVisible => debugText;

        /// <summary>F12: 디버그 글자 전체. 꺼져 있어도 입력 기록·재생 중에는 설정 줄(REC·REPLAY)이 보인다.</summary>
        public void SetDebugText(bool visible) => debugText = visible;

        /// <summary>F3 판정 오버레이.</summary>
        public void SetOverlay(bool visible)
        {
            if (overlay != null)
                overlay.gameObject.SetActive(visible);
        }

        void LateUpdate() => RenderFrame();

        /// <summary>한 프레임 연출. LateUpdate가 부르며, 측정 테스트가 직접 부를 수도 있다.</summary>
        public void RenderFrame()
        {
            if (runner.World == null)
                return;
            double now = SimTime();
            float dt = Mathf.Clamp((float)(now - lastSimTime), 0f, MaxFrameDt);
            lastSimTime = now;
            LastDt = dt;

            for (int i = 0; i < queued; i++)
                Consume(queue[i]);
            queued = 0;

            var world = runner.World;
            var previous = runner.PreviousState;
            var current = world.State;
            float alpha = runner.RenderAlpha;
            if (current.Match.Round != renderedRound)
            {
                // 새 라운드(또는 새 경기): 시작 위치로 순간 이동했으므로 보간하지 않고 발을 새로 디딘다.
                // 맵 순환(CP12)이면 맵도 바뀌었다.
                renderedRound = current.Match.Round;
                if (world.Arena != builtArena)
                {
                    BuildArena(world.Arena);
                    fx.Clear();
                    dust.Clear();
                }
                fighter0.ResetPose();
                fighter1.ResetPose();
                koShatter.Clear();
                shattered[0] = shattered[1] = false;
                alpha = 1f;
            }
            propView.Render(previous.Props, current.Props, alpha);
            projectileView.Render(previous.Projectiles, current.Projectiles, alpha, world.Config.Combat.Rock.Radius);
            propShadows.Render(propView, current.Props);
            fighter0.Render(previous.Fighter0, current.Fighter0, alpha, dt, world.Arena, world.Config);
            fighter1.Render(previous.Fighter1, current.Fighter1, alpha, dt, world.Arena, world.Config);
            UpdateKnockout(current, world.Config);
            UpdateCountdownSound(current, world.Config);
            dust.Tick(dt);
            koShatter.Tick(dt);
            background.Tick(dt);
            if (cameraRig != null)
            {
                cameraRig.Tick(dt);
                postFx.ReduceMotion = cameraRig.ReduceMotion;
            }
            postFx.Tick(dt);
            fx.Tick(dt, cameraRig != null ? (Vector2)cameraRig.transform.position : Vector2.zero);

            Vector2 body0 = fighter0.transform.localPosition;
            Vector2 body1 = fighter1.transform.localPosition;
            boundaryView.Render(V(current.Fighter0.Position), V(current.Fighter1.Position), world.Config.HalfBody);
            boundaryView.Tick(dt);
            hud.Render(current, body0 + new Vector2(0f, FighterView.VisualLift), body1 + new Vector2(0f, FighterView.VisualLift),
                world.Config.Match, world.Config.Combat.MaxHealth, world.Config.Combat.Energy, world.Config.EnergyEnabled, dt);
            if (labels != null && labels.gameObject.activeSelf)
            {
                bool recordingOrReplay = runner.Recording != null || runner.Playback != null;
                labels.SetVisibility(debugText, debugText || recordingOrReplay, debugText && fighterLabelsAllowed);
                labels.Render(current, body0, body1, Footer(world));
            }
            if (overlay.gameObject.activeSelf)
                overlay.Render(current, world.Config, dt);
        }

        /// <summary>
        /// KO 히트스탑(20틱)이 끝나면 진 쪽 몸을 조각내고, 이긴 쪽은 통통 튄다. 상태만 보고 정하므로 틱을 건너뛰어도 한 번만 일어난다.
        /// </summary>
        void UpdateKnockout(in SimState s, SimConfig config)
        {
            if (s.Match.Phase != MatchPhase.KO || s.Match.PhaseTick < config.Combat.KoHitstop)
                return;
            for (int k = 0; k < 2; k++)
            {
                var f = s.GetFighter(k);
                if (f.Action != ActionState.KO || shattered[k])
                    continue;
                shattered[k] = true;
                var view = Fighter(k);
                var center = view.BodyCenter;
                koShatter.Shatter(center, palette.FighterBody(k), new Vector2(f.Velocity.X, f.Velocity.Y));
                if (koShatter.PhysicsEnabled)
                    view.SetShattered(true);
                fx.RingBurst(center, palette.FighterBody(k), 0.6f, 4f, 0.3f);
                fx.Shards(center, Vector2.up, 360f, 10, palette.FighterBody(k), 11f, 0.22f, 0.7f);
                dust.Burst(center, 4, 3f, 0f);
                AddTrauma(0.5f);
                sfxPlayer.Play(Sfx.WallSplat, 0.8f);
            }
            if (s.Match.RoundWinner >= 0)
                Fighter(s.Match.RoundWinner).Celebrating = true;
        }

        /// <summary>카운트다운: READY가 뜰 때 짧은 신호음, GO가 뜰 때 높은 신호음.</summary>
        void UpdateCountdownSound(in SimState s, SimConfig config)
        {
            int key = s.Match.Phase != MatchPhase.Countdown ? -1
                : s.Match.PhaseTick >= config.Match.CountdownTicks - HudView.GoTicks ? 1 : 0;
            if (key == countdownKey)
                return;
            countdownKey = key;
            if (key == 0)
                sfxPlayer.Play(Sfx.Countdown);
            else if (key == 1)
                sfxPlayer.Play(Sfx.Countdown, 1f, 1.5f);
        }

        string Footer(SimWorld world)
        {
            var p2 = runner.Player2Input;
            var dummy = p2 is DummyInputSource d ? d.Behavior : DummyBehavior.Idle;
            bool recording = runner.Recording != null;
            bool playback = runner.Playback != null;
            if (footer == null || !ReferenceEquals(world.Arena.Name, footerArena) || !ReferenceEquals(world.Config, footerConfig)
                || !ReferenceEquals(p2, footerP2) || dummy != footerDummy
                || !ReferenceEquals(runner.PresetName, footerPreset) || recording != footerRecording || playback != footerPlayback)
            {
                footerPreset = runner.PresetName;
                footerRecording = recording;
                footerPlayback = playback;
                footerArena = world.Arena.Name;
                footerConfig = world.Config;
                footerP2 = p2;
                footerDummy = dummy;
                var sw = world.Config.Rules.Switches;
                footer = $"{footerArena} / {world.Config.StartForm}{(sw.GuardEnabled ? " +guard" : "")}{(sw.GrabEnabled ? " +grab" : "")}{(sw.EnergyEnabled ? "" : " (no energy)")} / {footerPreset} / P2 {InputSourceName(p2)}"
                         + (recording ? " / REC" : "") + (playback ? " / REPLAY" : "");
            }
            return footer;
        }

        public static string InputSourceName(IInputSource source)
        {
            switch (source)
            {
                case KeyboardInputSource _: return "keyboard";
                case GamepadInputSource _: return "gamepad";
                case DummyInputSource d: return "dummy " + d.Behavior;
                case BotInputSource _: return "bot";
                case FrameArrayInputSource _: return "scenario";
                default: return source == null ? "none" : "idle";
            }
        }

        static Vector2 V(Vec2 v) => new Vector2(v.X, v.Y);

        void Consume(in SimEvent e)
        {
            if (e.Fighter == 0)
                fighter0.OnEvent(e);
            else if (e.Fighter == 1)
                fighter1.OnEvent(e);

            float half = runner.World.Config.HalfBody;
            var feet = new Vector2(e.Position.X, e.Position.Y - half);
            // 몸 그림은 충돌 상자보다 VisualLift만큼 위에 있으므로 몸에 생기는 연출은 그만큼 올린다.
            var onBody = V(e.Position) + new Vector2(0f, FighterView.VisualLift);
            bool reduceMotion = cameraRig != null && cameraRig.ReduceMotion;
            switch (e.Type)
            {
                case SimEventType.Land:
                    // 먼지 원 2~4개(세게 떨어질수록 많이).
                    dust.Burst(feet, e.Strength > 20f ? 4 : e.Strength > 10f ? 3 : 2, 1.5f + e.Strength * 0.05f, 0f);
                    if (e.Strength > 24f && cameraRig != null)
                        cameraRig.AddTrauma(0.18f);
                    sfxPlayer.Play(Sfx.Land, Mathf.Clamp01(0.4f + e.Strength / 40f));
                    break;
                case SimEventType.Jump:
                    dust.Burst(feet, 2, 1.2f, 0f);
                    sfxPlayer.Play(Sfx.Jump);
                    break;
                case SimEventType.AirJump:
                    dust.Burst(feet, 2, 0.8f, 0f);
                    sfxPlayer.Play(Sfx.Jump, 0.8f);
                    break;
                case SimEventType.WallJump:
                    dust.Burst(new Vector2(e.Position.X - e.Direction.X * half, e.Position.Y), 3, 1.2f, e.Direction.X);
                    sfxPlayer.Play(Sfx.Jump);
                    break;
                case SimEventType.PadBounce:
                    dust.Burst(feet, 3, 1.5f, 0f);
                    fx.RingBurst(feet, palette.energySoft, 0.3f, 1.4f, 0.18f);
                    sfxPlayer.Play(Sfx.Pad);
                    break;
                case SimEventType.Spike:
                    // 가시: 위험색 파편이 위로, 고리, 흔들림.
                    fx.Shards(V(e.Position), Vector2.up, 70f, 7, palette.hit, 9f, 0.18f, 0.4f);
                    fx.RingBurst(V(e.Position), palette.hit, 0.3f, 1.6f, 0.18f);
                    AddTrauma(0.3f);
                    sfxPlayer.Play(Sfx.Spike);
                    break;
                case SimEventType.Dash:
                    dust.Burst(new Vector2(e.Position.X, e.Position.Y - half), 2, 1.6f, -e.Direction.X);
                    sfxPlayer.Play(Sfx.Dash);
                    break;
                case SimEventType.LungeStart:
                    // 돌진 발사: 뒤로 먼지, 바람 소리, 약한 흔들림(빠를수록 조금 더).
                    dust.Burst(new Vector2(e.Position.X, e.Position.Y - half), 3, 2f, -e.Direction.X);
                    AddTrauma(0.05f + e.Strength * 0.003f);
                    sfxPlayer.Play(Sfx.Dash);
                    break;
                case SimEventType.ProjectileFire:
                    // 바위 발사(CP19): 작은 고리, 높은 바람 소리(돌진 소리 재사용, S-151).
                    fx.RingBurst(V(e.Position), palette.FighterBody(e.Fighter), 0.15f, 0.6f, 0.12f);
                    sfxPlayer.Play(Sfx.Dash, 0.55f, 1.45f);
                    break;
                case SimEventType.ProjectileEnd:
                    if (e.Strength == (int)ProjectileEndReason.Terrain || e.Strength == (int)ProjectileEndReason.Prop
                        || e.Strength == (int)ProjectileEndReason.Boundary)
                    {
                        fx.Shards(V(e.Position), -new Vector2(e.Direction.X, e.Direction.Y), 80f, 4, palette.FighterBody(e.Fighter), 6f, 0.12f, 0.3f);
                        sfxPlayer.Play(Sfx.Land, 0.35f, 1.3f);
                    }
                    break;
                case SimEventType.PowerCheck:
                    overlay.ShowPower(e.Fighter, e.Strength);
                    break;
                case SimEventType.Grab:
                {
                    // 손이 붙는 순간: 양쪽 짧은 번쩍임, 작은 고리, 약한 흔들림.
                    int grabber = 1 - e.Fighter;
                    Fighter(grabber).Flash(2f * Frame);
                    fx.RingBurst(onBody, palette.flash, 0.2f, 1.0f, 0.12f);
                    AddTrauma(0.12f);
                    sfxPlayer.Play(Sfx.Grab);
                    break;
                }
                case SimEventType.Throw:
                    // 던지기: 크게 휘두르는 바람 소리와 흔들림. 던져진 쪽은 잔상(FighterView).
                    dust.Burst(new Vector2(e.Position.X, e.Position.Y - half), 3, 2f, -e.Direction.X);
                    fx.Shards(onBody, V(e.Direction), 50f, 4, palette.FighterBody(1 - e.Fighter), 9f, 0.18f, 0.35f);
                    AddTrauma(0.22f);
                    sfxPlayer.Play(Sfx.Throw);
                    break;
                case SimEventType.GrabBreak:
                    // 잡기 풀기: 불꽃과 고리, 양쪽 외곽선 번쩍임.
                    fx.Sparks(onBody, 8, 11f);
                    fx.RingBurst(onBody, palette.energy, 0.3f, 1.4f, 0.18f);
                    fighter0.FlashOutline(3f * Frame);
                    fighter1.FlashOutline(3f * Frame);
                    AddTrauma(0.2f);
                    sfxPlayer.Play(Sfx.Clash, 0.7f);
                    break;

                case SimEventType.Hit:
                {
                    // 맞은 쪽 4프레임·때린 쪽 3프레임 번쩍임, 파편 6~10개(hit 빨강 위주 + 공격자 몸 색 일부, 08 5절), 흰 고리,
                    // 데미지 비례 흔들림, 15 이상이면 확대와 약한 색수차.
                    int attacker = 1 - e.Fighter;
                    float damage = e.Strength;
                    Fighter(attacker).Flash(3f * Frame);
                    int shards = Mathf.Clamp(6 + Mathf.RoundToInt(damage / 4f), 6, 10);
                    fx.Shards(onBody, V(e.Direction), 75f, shards - shards / 3, palette.hit, 8f + damage * 0.35f, 0.24f);
                    fx.Shards(onBody, V(e.Direction), 60f, shards / 3, palette.FighterBody(attacker), 8f + damage * 0.35f, 0.24f);
                    fx.RingBurst(onBody, palette.flash, 0.3f, 1.5f + damage * 0.04f);
                    fx.Glow(onBody, palette.flash, 2.2f + damage * 0.06f, 0.45f, 0.2f);
                    AddTrauma(0.2f + damage * 0.016f);
                    if (damage >= 15f)
                    {
                        Punch(0.015f, 6f * Frame);
                        postFx.Pulse(0.25f, 0.15f);
                    }
                    sfxPlayer.Play(damage >= 15f ? Sfx.HitStrong : Sfx.HitWeak);
                    break;
                }
                case SimEventType.PropHit:
                case SimEventType.BoxHit:
                {
                    // 구조물에 맞음(CP14): 명중과 같은 크기의 연출, 파편은 hit 빨강과 구조물 색.
                    float damage = e.Strength;
                    int shards = Mathf.Clamp(6 + Mathf.RoundToInt(damage / 4f), 6, 10);
                    fx.Shards(onBody, V(e.Direction), 75f, shards - shards / 3, palette.hit, 8f + damage * 0.35f, 0.24f);
                    fx.Shards(onBody, V(e.Direction), 60f, shards / 3, CurrentTheme.prop, 8f + damage * 0.35f, 0.24f);
                    fx.RingBurst(onBody, palette.flash, 0.3f, 1.5f + damage * 0.04f);
                    fx.Glow(onBody, palette.flash, 2.2f + damage * 0.06f, 0.45f, 0.2f);
                    AddTrauma(0.2f + damage * 0.016f);
                    sfxPlayer.Play(damage >= 15f ? Sfx.HitStrong : Sfx.HitWeak);
                    break;
                }
                case SimEventType.JointBreak:
                    // 줄·경첩이 끊어짐(CP15): 연결점에서 작은 섬광과 먼지, 짧은 끊어지는 소리.
                    fx.Glow(V(e.Position), palette.flash, 1.6f, 0.4f, 0.15f);
                    fx.Shards(V(e.Position), Vector2.down, 140f, 4, CurrentTheme.prop, 6f, 0.12f, 0.35f);
                    AddTrauma(0.12f);
                    sfxPlayer.Play(Sfx.Snap);
                    break;
                case SimEventType.PropStrike:
                    // 돌진이 구조물을 침: 먼지, 작은 고리, 둔탁한 소리, 위력 비례 흔들림.
                    dust.Burst(V(e.Position), 3, 1.6f, 0f);
                    fx.RingBurst(V(e.Position), palette.energySoft, 0.22f, 0.9f + e.Strength * 0.02f, 0.16f);
                    AddTrauma(0.06f + e.Strength * 0.006f);
                    sfxPlayer.Play(Sfx.WallSplat, 0.5f);
                    break;
                case SimEventType.BoxKnockout:
                    // 상자가 기둥에서 빠짐(CP16): 상자 중심에서 먼지와 구조물 색 파편, 둔탁한 소리, 위력 비례 흔들림.
                    dust.Burst(V(e.Position), 5, 2.2f, 0f);
                    fx.Shards(V(e.Position), V(e.Direction), 70f, 4, CurrentTheme.prop, 7f, 0.16f, 0.4f);
                    AddTrauma(0.1f + e.Strength * 0.006f);
                    sfxPlayer.Play(Sfx.WallSplat, 0.8f);
                    break;
                case SimEventType.PropImpact:
                    // 구조물이 세게 부딪힘(09 6절): 작은 먼지와 둔탁한 소리. 속도 비례(5 → 작게, 20 이상 → 크게).
                {
                    float strength = Mathf.InverseLerp(5f, 20f, e.Strength);
                    dust.Burst(V(e.Position), 2 + Mathf.RoundToInt(strength * 3f), 1.2f + strength, 0f);
                    AddTrauma(0.02f + strength * 0.06f);
                    sfxPlayer.Play(Sfx.Land, 0.35f + strength * 0.4f);
                    break;
                }
                case SimEventType.GuardHit:
                    fx.RingBurst(onBody, palette.uiSub, 0.25f, 0.9f, 0.15f);
                    fx.Shards(onBody, V(e.Direction), 60f, 3, palette.uiSub, 6f, 0.14f, 0.3f);
                    AddTrauma(0.12f);
                    sfxPlayer.Play(Sfx.Guard);
                    break;
                case SimEventType.Parry:
                {
                    // 패링한 쪽 색의 고리 폭발, 짧은 화면 색 반전(흔들림 줄이기에서는 끔), 높은 "팅".
                    var center = Fighter(e.Fighter).BodyCenter;
                    var color = palette.FighterBody(e.Fighter);
                    fx.RingBurst(center, color, 0.6f, 3.2f, 0.28f);
                    fx.RingBurst(center, palette.flash, 0.3f, 2.0f, 0.18f);
                    fx.Glow(center, palette.energy, 3.2f, 0.5f, 0.3f);
                    Fighter(e.Fighter).FlashOutline(4f * Frame);
                    if (!reduceMotion)
                        fx.ScreenFlash(true);
                    AddTrauma(0.25f);
                    sfxPlayer.Play(Sfx.Parry);
                    break;
                }
                case SimEventType.Clash:
                    // 접점의 별 모양 폭발과 불꽃, 양쪽 외곽선 번쩍임, 중간 흔들림, 금속성 소리.
                    fx.Star(onBody, 8, 1.1f);
                    fx.Sparks(onBody, 12, 15f);
                    fx.RingBurst(onBody, palette.energy, 0.2f, 1.2f, 0.15f);
                    fx.Glow(onBody, palette.flash, 2.6f, 0.5f, 0.22f);
                    fx.Glow(onBody, palette.energy, 3.6f, 0.3f, 0.3f);
                    fighter0.FlashOutline(4f * Frame);
                    fighter1.FlashOutline(4f * Frame);
                    AddTrauma(0.3f);
                    sfxPlayer.Play(Sfx.Clash);
                    break;
                case SimEventType.WallSplat:
                {
                    // 금 간 자국, 먼지 폭발, 강한 흔들림, 확대, 묵직한 충돌음, 작은 지형 파편.
                    var point = V(e.Position);
                    var normal = V(e.Direction);
                    fx.Crack(point, normal);
                    dust.Burst(point, 4, 2.6f, Mathf.Abs(normal.x) > 0.5f ? normal.x : 0f);
                    fx.Shards(point, normal, 110f, 6, CurrentTheme.terrain, 7f, 0.15f, 0.55f);
                    fx.RingBurst(point, palette.flash, 0.3f, 1.8f, 0.2f);
                    fx.Glow(point, palette.flash, 2.8f, 0.35f, 0.22f);
                    AddTrauma(0.5f);
                    Punch(0.02f, 8f * Frame);
                    postFx.Pulse(0.25f, 0.15f);
                    sfxPlayer.Play(Sfx.WallSplat);
                    break;
                }
                case SimEventType.BoundaryHit:
                {
                    // 경계 튕김(CR-002): 가장자리를 따라 퍼지는 빨강 물결, 흰 섬광, 흔들림, 짧은 전기 소리. 면역 중(피해 0)은 작게.
                    var point = V(e.Position);
                    bool hurt = e.Strength > 0f;
                    boundaryView.Hit(point, V(e.Direction));
                    fx.Glow(point, palette.flash, hurt ? 3f : 1.6f, hurt ? 0.45f : 0.2f, 0.2f);
                    AddTrauma(hurt ? 0.25f + e.Strength * 0.015f : 0.06f);
                    sfxPlayer.Play(Sfx.Boundary, hurt ? 1f : 0.5f);
                    break;
                }
                case SimEventType.BounceRebound:
                    // 경직·돌진 중 지형에 튕김: 먼지, 작은 고리, 세기 비례 흔들림.
                    dust.Burst(V(e.Position), 3, 1.4f, Mathf.Abs(e.Direction.X) > 0.5f ? e.Direction.X : 0f);
                    fx.RingBurst(V(e.Position), palette.uiSub, 0.2f, 0.8f + e.Strength * 0.02f, 0.16f);
                    AddTrauma(0.04f + e.Strength * 0.006f);
                    sfxPlayer.Play(Sfx.Land, 0.6f);
                    break;
                case SimEventType.KO:
                {
                    // 큰 폭발. 히트스탑 20틱 뒤 진 쪽 몸이 조각난다(UpdateKnockout).
                    var center = Fighter(e.Fighter).BodyCenter;
                    fx.RingBurst(center, palette.flash, 0.5f, 4.5f, 0.35f);
                    fx.Glow(center, palette.flash, 6f, 0.5f, 0.4f);
                    fx.Shards(center, Vector2.up, 360f, 10, palette.FighterBody(e.Fighter), 12f, 0.26f, 0.6f);
                    if (!reduceMotion)
                        fx.ScreenFlash(false);
                    AddTrauma(0.6f);
                    Punch(0.03f, 12f * Frame);
                    postFx.Pulse(0.6f, 0.25f);
                    sfxPlayer.Play(Sfx.KO);
                    break;
                }
            }
        }

        void AddTrauma(float amount)
        {
            if (cameraRig != null)
                cameraRig.AddTrauma(amount);
        }

        void Punch(float amount, float duration)
        {
            if (cameraRig != null)
                cameraRig.Punch(amount, duration);
        }
    }
}

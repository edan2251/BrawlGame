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
        ArenaView arenaView;
        FighterView fighter0;
        FighterView fighter1;
        DustPool dust;
        ImpactFx fx;
        HudView hud;
        SfxPlayer sfxPlayer;
        CameraRig cameraRig;
        DebugStateLabels labels;
        HitboxOverlay overlay;

        readonly SimEvent[] queue = new SimEvent[EventQueueCapacity];
        int queued;
        double lastSimTime;
        int renderedRound;

        // 디버그 글자 아래줄: 바뀔 때만 새로 만든다.
        string footer;
        string footerArena;
        CombatMode footerMode;
        IInputSource footerP2;
        DummyBehavior footerDummy;
        bool footerGrab;

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
            background.Build(palette, shapes);
            arenaView = Create<ArenaView>("Arena");
            fighter0 = CreateFighter(0);
            fighter1 = CreateFighter(1);
            dust = Create<DustPool>("Dust");
            dust.Setup(palette, shapes);
            fx = Create<ImpactFx>("Impact Fx");
            fx.Setup(palette, shapes);
            hud = Create<HudView>("Hud");
            hud.Setup(palette, shapes);
            sfxPlayer = Create<SfxPlayer>("Sfx");
            sfxPlayer.Setup(sfx);
            if (showDebugLabels)
            {
                labels = Create<DebugStateLabels>("Debug Labels");
                labels.Setup(palette.ink);
            }
            overlay = Create<HitboxOverlay>("Hitbox Overlay");
            overlay.Setup(palette, shapes);
            overlay.gameObject.SetActive(false);

            var cam = Camera.main;
            if (cam != null)
            {
                cam.backgroundColor = palette.bgBase;
                cameraRig = cam.GetComponent<CameraRig>() ?? cam.gameObject.AddComponent<CameraRig>();
            }

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

        void OnMatchBegan(MatchRunner r)
        {
            arenaView.Build(r.World.Arena, palette, shapes);
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
                renderedRound = current.Match.Round;
                fighter0.ResetPose();
                fighter1.ResetPose();
                alpha = 1f;
            }
            fighter0.Render(previous.Fighter0, current.Fighter0, alpha, dt, world.Arena, world.Config);
            fighter1.Render(previous.Fighter1, current.Fighter1, alpha, dt, world.Arena, world.Config);
            dust.Tick(dt);
            if (cameraRig != null)
                cameraRig.Tick(dt);
            fx.Tick(dt, cameraRig != null ? (Vector2)cameraRig.transform.position : Vector2.zero);

            Vector2 body0 = fighter0.transform.localPosition;
            Vector2 body1 = fighter1.transform.localPosition;
            hud.Render(current, body0 + new Vector2(0f, FighterView.VisualLift), body1 + new Vector2(0f, FighterView.VisualLift),
                world.Config.Match.RoundsToWin, world.Config.Combat.MaxHealth, dt);
            if (labels != null && labels.gameObject.activeSelf)
                labels.Render(current, body0, body1, Footer(world));
            if (overlay.gameObject.activeSelf)
                overlay.Render(current, world.Config, dt);
        }

        string Footer(SimWorld world)
        {
            var p2 = runner.Player2Input;
            var dummy = p2 is DummyInputSource d ? d.Behavior : DummyBehavior.Idle;
            if (footer == null || !ReferenceEquals(world.Arena.Name, footerArena) || world.Config.Mode != footerMode
                || !ReferenceEquals(p2, footerP2) || dummy != footerDummy || world.Config.GrabEnabled != footerGrab)
            {
                footerArena = world.Arena.Name;
                footerMode = world.Config.Mode;
                footerP2 = p2;
                footerDummy = dummy;
                footerGrab = world.Config.GrabEnabled;
                footer = $"{footerArena} / mode {footerMode}{(world.Config.GrabEnabled ? "" : " (no grab)")} / P2 {InputSourceName(p2)}";
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
                    sfxPlayer.Play(Sfx.Jump);
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
                case SimEventType.PowerCheck:
                    overlay.ShowPower(e.Fighter, e.Strength);
                    break;

                case SimEventType.Hit:
                {
                    // 맞은 쪽 4프레임·때린 쪽 3프레임 번쩍임, 공격자 색 파편 6~10개, 흰 고리, 데미지 비례 흔들림, 15 이상이면 확대.
                    int attacker = 1 - e.Fighter;
                    float damage = e.Strength;
                    Fighter(attacker).Flash(3f * Frame);
                    fx.Shards(onBody, V(e.Direction), 75f, Mathf.Clamp(6 + Mathf.RoundToInt(damage / 4f), 6, 10), palette.FighterBody(attacker), 8f + damage * 0.35f, 0.24f);
                    fx.RingBurst(onBody, palette.flash, 0.3f, 1.5f + damage * 0.04f);
                    AddTrauma(0.2f + damage * 0.016f);
                    if (damage >= 15f)
                        Punch(0.015f, 6f * Frame);
                    sfxPlayer.Play(damage >= 15f ? Sfx.HitStrong : Sfx.HitWeak);
                    break;
                }
                case SimEventType.GuardHit:
                    fx.RingBurst(onBody, palette.ink, 0.25f, 0.9f, 0.15f);
                    fx.Shards(onBody, V(e.Direction), 60f, 3, palette.shadow, 6f, 0.14f, 0.3f);
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
                    fx.RingBurst(onBody, palette.spark, 0.2f, 1.2f, 0.15f);
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
                    fx.Shards(point, normal, 110f, 6, palette.ink, 7f, 0.15f, 0.55f);
                    fx.RingBurst(point, palette.flash, 0.3f, 1.8f, 0.2f);
                    AddTrauma(0.5f);
                    Punch(0.02f, 8f * Frame);
                    sfxPlayer.Play(Sfx.WallSplat);
                    break;
                }
                case SimEventType.BounceRebound:
                    // 경직·돌진 중 지형에 튕김: 먼지, 작은 고리, 세기 비례 흔들림.
                    dust.Burst(V(e.Position), 3, 1.4f, Mathf.Abs(e.Direction.X) > 0.5f ? e.Direction.X : 0f);
                    fx.RingBurst(V(e.Position), palette.shadow, 0.2f, 0.8f + e.Strength * 0.02f, 0.16f);
                    AddTrauma(0.04f + e.Strength * 0.006f);
                    sfxPlayer.Play(Sfx.Land, 0.6f);
                    break;
                case SimEventType.KO:
                {
                    // 큰 폭발. 진 쪽이 조각나는 연출 물리는 CP8.
                    var center = Fighter(e.Fighter).BodyCenter;
                    fx.RingBurst(center, palette.flash, 0.5f, 4.5f, 0.35f);
                    fx.Shards(center, Vector2.up, 360f, 10, palette.FighterBody(e.Fighter), 12f, 0.26f, 0.6f);
                    if (!reduceMotion)
                        fx.ScreenFlash(false);
                    AddTrauma(0.6f);
                    Punch(0.03f, 12f * Frame);
                    sfxPlayer.Play(Sfx.HitStrong);
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

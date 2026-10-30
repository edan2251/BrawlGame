using Knockbox.Presentation;
using Knockbox.Simulation;
using UnityEngine;

namespace Knockbox.Game
{
    /// <summary>
    /// MatchRunner의 상태와 이벤트를 연출 계층에 넘긴다. 연출은 상태를 읽기만 하고 시뮬레이션에 쓰지 않는다.
    /// 연출 시간은 시뮬레이션 시간((틱 + 보간 비율) / 60)으로 진행한다. 멈추면 연출도 멈추고,
    /// 캡처처럼 한 틱씩 진행해도 실제 플레이와 같은 모습이 나온다.
    /// </summary>
    [RequireComponent(typeof(MatchRunner))]
    public sealed class MatchPresenter : MonoBehaviour
    {
        const int EventQueueCapacity = 256;
        const float MaxFrameDt = 0.1f;
        const float MaxHealth = 100f;

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
        HudView hud;
        SfxPlayer sfxPlayer;
        CameraRig cameraRig;
        DebugStateLabels labels;

        readonly SimEvent[] queue = new SimEvent[EventQueueCapacity];
        int queued;
        double lastSimTime;

        public PaletteAsset Palette
        {
            get => palette;
            set => palette = value;
        }

        public FighterView Fighter(int index) => index == 0 ? fighter0 : fighter1;
        public DustPool Dust => dust;
        public CameraRig CameraRig => cameraRig;
        public SfxPlayer SfxPlayer => sfxPlayer;

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
            hud = Create<HudView>("Hud");
            hud.Setup(palette, shapes);
            sfxPlayer = Create<SfxPlayer>("Sfx");
            sfxPlayer.Setup(sfx);
            if (showDebugLabels)
            {
                labels = Create<DebugStateLabels>("Debug Labels");
                labels.Setup(palette.ink);
            }

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
            float half = world.Config.HalfBody;
            float maxRun = world.Config.Movement.MaxRunSpeed;
            fighter0.Render(previous.Fighter0, current.Fighter0, alpha, dt, world.Arena, half, maxRun);
            fighter1.Render(previous.Fighter1, current.Fighter1, alpha, dt, world.Arena, half, maxRun);
            dust.Tick(dt);
            if (cameraRig != null)
                cameraRig.Tick(dt);

            Vector2 body0 = fighter0.transform.localPosition;
            Vector2 body1 = fighter1.transform.localPosition;
            hud.Render(current, body0 + new Vector2(0f, FighterView.VisualLift), body1 + new Vector2(0f, FighterView.VisualLift), world.Config.Match.RoundsToWin, MaxHealth, dt);
            if (labels != null && labels.gameObject.activeSelf)
                labels.Render(current, body0, body1, world.Arena.Name);
        }

        void Consume(in SimEvent e)
        {
            if (e.Fighter == 0)
                fighter0.OnEvent(e);
            else if (e.Fighter == 1)
                fighter1.OnEvent(e);

            float half = runner.World.Config.HalfBody;
            var feet = new Vector2(e.Position.X, e.Position.Y - half);
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
            }
        }
    }
}

using Knockbox.Simulation;
using UnityEngine;

namespace Knockbox.Game
{
    /// <summary>
    /// 플레이 모드에서 시나리오를 MatchRunner로 재생하고, 진행하면서 expect를 평가한다.
    /// 실패는 오류 로그로 남긴다(플레이 모드 테스트가 콘솔 오류로 잡는다). 캡처 저장은 CP2에서 붙인다.
    /// </summary>
    public sealed class ScenarioPlayer : MonoBehaviour
    {
        [SerializeField] MatchRunner runner;
        [SerializeField] string scenarioName;
        [SerializeField] bool playOnStart;

        public ScenarioDef Current { get; private set; }
        public int FailureCount { get; private set; }

        public MatchRunner Runner
        {
            get => runner;
            set => runner = value;
        }

        public bool Finished => Current != null && runner != null && runner.World != null && runner.World.State.Tick >= Current.Ticks;

        void Start()
        {
            if (playOnStart && !string.IsNullOrEmpty(scenarioName))
                Play(ScenarioJson.LoadByName(scenarioName));
        }

        void OnDestroy()
        {
            if (runner != null)
                runner.Ticked -= OnTicked;
        }

        public void Play(ScenarioDef def)
        {
            Current = def;
            FailureCount = 0;
            var config = SimConfigFactory.Create(runner.Tuning);
            runner.InputDelayTicks = 0;
            runner.Begin(config, ScenarioInputs.CreateInitialState(def, config));
            runner.Player1Input = new FrameArrayInputSource(ScenarioInputs.Build(def.P1, def.Ticks));
            runner.Player2Input = new FrameArrayInputSource(ScenarioInputs.Build(def.P2, def.Ticks));
            runner.StopAtTick = def.Ticks;
            runner.Ticked -= OnTicked;
            runner.Ticked += OnTicked;
            Evaluate(runner.World.State);
        }

        void OnTicked(MatchRunner r) => Evaluate(r.World.State);

        void Evaluate(in SimState state)
        {
            if (Current == null)
                return;
            foreach (var e in Current.Expects)
            {
                if (e.Tick != state.Tick)
                    continue;
                string failure = ExpectEvaluator.Evaluate(state, e);
                if (failure == null)
                    continue;
                FailureCount++;
                Debug.LogError($"[Knockbox] 시나리오 {Current.Name}: {failure}");
            }
        }
    }
}

using Knockbox.Presentation;
using UnityEngine;

namespace Knockbox.Game
{
    /// <summary>
    /// MatchRunner의 상태를 연출 계층에 넘긴다. 연출은 상태를 읽기만 하고 시뮬레이션에 쓰지 않는다.
    /// </summary>
    [RequireComponent(typeof(MatchRunner))]
    public sealed class MatchPresenter : MonoBehaviour
    {
        [SerializeField] PaletteAsset palette;
        [SerializeField] bool showDebugLabels = true;

        MatchRunner runner;
        ArenaView arenaView;
        FighterView fighter0;
        FighterView fighter1;
        DebugStateLabels labels;

        public PaletteAsset Palette
        {
            get => palette;
            set => palette = value;
        }

        void Awake()
        {
            runner = GetComponent<MatchRunner>();
            if (palette == null)
                palette = ScriptableObject.CreateInstance<PaletteAsset>();

            arenaView = new GameObject("Arena").AddComponent<ArenaView>();
            arenaView.transform.SetParent(transform, false);
            fighter0 = CreateFighter(0);
            fighter1 = CreateFighter(1);
            if (showDebugLabels)
            {
                labels = new GameObject("Debug Labels").AddComponent<DebugStateLabels>();
                labels.transform.SetParent(transform, false);
                labels.Setup(palette.ink);
            }
            runner.MatchBegan += OnMatchBegan;
            if (runner.World != null)
                OnMatchBegan(runner);
        }

        void OnDestroy()
        {
            if (runner != null)
                runner.MatchBegan -= OnMatchBegan;
        }

        FighterView CreateFighter(int index)
        {
            var view = new GameObject($"Fighter {index + 1}").AddComponent<FighterView>();
            view.transform.SetParent(transform, false);
            view.Setup(index, palette);
            return view;
        }

        void OnMatchBegan(MatchRunner r) => arenaView.Build(r.World.Arena, palette);

        void LateUpdate()
        {
            if (runner.World == null)
                return;
            var previous = runner.PreviousState;
            var current = runner.World.State;
            float alpha = runner.RenderAlpha;
            fighter0.Render(previous.Fighter0, current.Fighter0, alpha);
            fighter1.Render(previous.Fighter1, current.Fighter1, alpha);
            if (labels != null)
                labels.Render(current, fighter0.transform.localPosition, fighter1.transform.localPosition);
        }
    }
}

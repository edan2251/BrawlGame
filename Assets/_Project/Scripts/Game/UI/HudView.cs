using Knockbox.Presentation;
using Knockbox.Simulation;
using UnityEngine;

namespace Knockbox.Game
{
    /// <summary>
    /// HUD 뼈대(05-presentation.md 6절): 머리 위 체력 바(폭 1.2, 높이 0.12, 줄어든 부분은 흰색으로 0.4초 남음),
    /// 화면 위 가운데 라운드 점수 칸(양쪽 RoundsToWin칸). 월드 공간 스프라이트라 카메라 캡처에도 찍힌다.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        const float BarWidth = 1.2f;
        const float BarHeight = 0.12f;
        const float BarOffsetY = 1.05f;
        const float TrailHold = 0.4f;
        const float TrailSpeed = 2.5f;
        const float SlotSize = 0.42f;
        const float SlotGap = 0.14f;
        const float SlotY = 8.5f;
        const int MaxSlots = 9;

        struct Bar
        {
            public SpriteRenderer Back;
            public SpriteRenderer Trail;
            public SpriteRenderer Fill;
            public float Shown;
            public float Trailing;
            public float Hold;
        }

        readonly Bar[] bars = new Bar[2];
        readonly SpriteRenderer[] slots0 = new SpriteRenderer[MaxSlots];
        readonly SpriteRenderer[] slots1 = new SpriteRenderer[MaxSlots];
        PaletteAsset palette;

        public void Setup(PaletteAsset paletteAsset, ShapeLibrary shapes)
        {
            palette = paletteAsset;
            for (int i = 0; i < 2; i++)
            {
                bars[i].Back = ShapeLibrary.Create(transform, $"Bar{i}Back", shapes.Square, SortingLayers.UI, 0, palette.ink);
                bars[i].Trail = ShapeLibrary.Create(transform, $"Bar{i}Trail", shapes.Square, SortingLayers.UI, 1, palette.flash);
                bars[i].Fill = ShapeLibrary.Create(transform, $"Bar{i}Fill", shapes.Square, SortingLayers.UI, 2, palette.FighterBody(i));
                bars[i].Shown = 1f;
                bars[i].Trailing = 1f;
            }
            for (int k = 0; k < MaxSlots; k++)
            {
                slots0[k] = ShapeLibrary.CreateSliced(transform, $"Score0_{k}", shapes.RoundedBody, SortingLayers.UI, 0, palette.terrainLip);
                slots1[k] = ShapeLibrary.CreateSliced(transform, $"Score1_{k}", shapes.RoundedBody, SortingLayers.UI, 0, palette.terrainLip);
                ShapeLibrary.SetSize(slots0[k], SlotSize, SlotSize);
                ShapeLibrary.SetSize(slots1[k], SlotSize, SlotSize);
            }
        }

        public void Render(in SimState state, Vector2 body0, Vector2 body1, int roundsToWin, float maxHealth, float dt)
        {
            RenderBar(ref bars[0], body0, Health(state.Fighter0, maxHealth), dt);
            RenderBar(ref bars[1], body1, Health(state.Fighter1, maxHealth), dt);

            int count = Mathf.Clamp(roundsToWin, 0, MaxSlots);
            for (int k = 0; k < MaxSlots; k++)
            {
                bool visible = k < count;
                slots0[k].enabled = visible;
                slots1[k].enabled = visible;
                if (!visible)
                    continue;
                // P1은 가운데에서 왼쪽으로, P2는 오른쪽으로 채운다.
                float offset = SlotGap * 2f + k * (SlotSize + SlotGap) + SlotSize * 0.5f;
                slots0[k].transform.localPosition = new Vector3(-offset, SlotY, 0f);
                slots1[k].transform.localPosition = new Vector3(offset, SlotY, 0f);
                slots0[k].color = k < state.Match.Score0 ? palette.p1Body : palette.terrainLip;
                slots1[k].color = k < state.Match.Score1 ? palette.p2Body : palette.terrainLip;
            }
        }

        static float Health(in FighterState f, float maxHealth) => maxHealth > 0f ? f.Health / maxHealth : 0f;

        void RenderBar(ref Bar bar, Vector2 body, float fraction, float dt)
        {
            fraction = Mathf.Clamp01(fraction);
            if (fraction < bar.Shown)
                bar.Hold = TrailHold;
            bar.Shown = fraction;
            if (bar.Trailing < bar.Shown)
                bar.Trailing = bar.Shown;
            if (bar.Hold > 0f)
                bar.Hold -= dt;
            else
                bar.Trailing = Mathf.Max(bar.Shown, bar.Trailing - TrailSpeed * dt);

            var center = new Vector2(body.x, body.y + BarOffsetY);
            Place(bar.Back, center.x - BarWidth * 0.5f, center.y, BarWidth + 0.06f, BarHeight + 0.06f, -0.03f);
            Place(bar.Trail, center.x - BarWidth * 0.5f, center.y, BarWidth * bar.Trailing, BarHeight, 0f);
            Place(bar.Fill, center.x - BarWidth * 0.5f, center.y, BarWidth * bar.Shown, BarHeight, 0f);
        }

        static void Place(SpriteRenderer sr, float left, float y, float width, float height, float pad)
        {
            sr.transform.localPosition = new Vector3(left + pad + width * 0.5f, y, 0f);
            ShapeLibrary.SetSize(sr, Mathf.Max(width, 0.0001f), height);
        }
    }
}

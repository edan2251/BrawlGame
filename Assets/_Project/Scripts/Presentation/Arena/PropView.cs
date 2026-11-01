using Knockbox.Simulation;
using UnityEngine;

namespace Knockbox.Presentation
{
    /// <summary>
    /// 구조물 그리기(09-props-physics.md 6절, 08-visual-direction.md 2절). 테마의 `prop` 색 단색, 줄은 같은 색의 가는 선,
    /// 축과 경첩은 작은 점. 상자(기둥, 흩어진 상자)는 상자마다 밝기 ±6% 이내로 다르고 상자 사이에 얇고 어두운 이음매를 둔다.
    /// CP13: 구조물은 아직 움직이지 않는 임시 물체라 맵을 그릴 때 한 번 만든다(CP14부터 상태를 따라 움직인다).
    /// </summary>
    public sealed class PropView : MonoBehaviour
    {
        public const float RopeWidth = 0.06f;
        const float PivotSize = 0.24f;
        const float SeamHeight = 0.07f;
        /// <summary>상자 밝기 차의 최대 비율(08 2절: ±6% 이내).</summary>
        public const float BoxShadeRange = 0.06f;
        const float SeamDarkness = 0.55f;
        const float PivotDarkness = 0.6f;

        ArenaDef current;
        string currentTheme;

        public void Build(ArenaDef arena, ArenaTheme theme, ShapeLibrary shapes)
        {
            if (arena == current && theme.name == currentTheme)
                return;
            current = arena;
            currentTheme = theme.name;
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            for (int i = 0; i < arena.PropCount; i++)
            {
                ref readonly var p = ref arena.GetProp(i);
                bool box = p.Kind == PropKind.BoxPillar || p.Kind == PropKind.LooseBox;
                var color = box ? BoxShade(theme.prop, i) : theme.prop;
                Rect(shapes, color, 3, p.MinX, p.MinY, p.MaxX, p.MaxY);
                if (p.HangsByRope)
                    Line(shapes, theme.prop, 1, ToVector(p.Anchor), new Vector2(p.Center.X, p.MaxY), RopeWidth);
                else if (p.HasAnchor)
                    Dot(shapes, Darken(theme.prop, PivotDarkness), 5, ToVector(p.Anchor), PivotSize);

                // 기둥의 위아래 상자 사이 이음매.
                if (p.Kind == PropKind.BoxPillar && i + 1 < arena.PropCount)
                {
                    ref readonly var above = ref arena.GetProp(i + 1);
                    if (above.Group == p.Group && Mathf.Abs(above.MinY - p.MaxY) < 1e-3f)
                    {
                        float half = Mathf.Min(p.HalfSize.X, above.HalfSize.X);
                        Rect(shapes, Darken(theme.prop, SeamDarkness), 4, p.Center.X - half, p.MaxY - SeamHeight * 0.5f, p.Center.X + half, p.MaxY + SeamHeight * 0.5f);
                    }
                }
            }
        }

        /// <summary>상자마다 정해진 밝기 배율(1 ± 0.06 이내). 번호로 정하므로 매번 같다.</summary>
        public static Color BoxShade(Color prop, int index)
        {
            float k = ((index * 37 + 11) % 13) / 12f * 2f - 1f;
            return PaletteAsset.Scaled(prop, 1f + BoxShadeRange * k);
        }

        static Color Darken(Color c, float scale) => PaletteAsset.Scaled(c, scale);

        static Vector2 ToVector(Vec2 v) => new Vector2(v.X, v.Y);

        void Rect(ShapeLibrary shapes, Color color, int order, float minX, float minY, float maxX, float maxY)
        {
            var sr = ShapeLibrary.Create(transform, "Prop", shapes.Square, SortingLayers.Terrain, order, color);
            sr.transform.localPosition = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
            ShapeLibrary.SetSize(sr, maxX - minX, maxY - minY);
        }

        void Line(ShapeLibrary shapes, Color color, int order, Vector2 from, Vector2 to, float width)
        {
            var delta = to - from;
            var sr = ShapeLibrary.Create(transform, "Rope", shapes.Square, SortingLayers.Terrain, order, color);
            sr.transform.localPosition = (Vector3)((from + to) * 0.5f);
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            ShapeLibrary.SetSize(sr, delta.magnitude, width);
        }

        void Dot(ShapeLibrary shapes, Color color, int order, Vector2 at, float size)
        {
            var sr = ShapeLibrary.Create(transform, "Pivot", shapes.Circle, SortingLayers.Terrain, order, color);
            sr.transform.localPosition = at;
            ShapeLibrary.SetSize(sr, size, size);
        }
    }
}

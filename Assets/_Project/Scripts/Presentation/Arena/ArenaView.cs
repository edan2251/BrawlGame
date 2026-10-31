using Knockbox.Simulation;
using UnityEngine;

namespace Knockbox.Presentation
{
    /// <summary>
    /// 지형 그리기(08-visual-direction.md 1·4절). 시뮬레이션과 같은 상자 목록으로 그리므로 둘이 어긋날 수 없다.
    /// 지형은 테마의 고정 지형 색 단색(외곽선·윗면 띠·평면 그림자 없음). 그림자는 배경의 광원이 만든다(BackgroundView).
    /// 단방향 발판: 같은 색의 얇은 판. 튕김 패드: 지형 색 받침 + energySoft 반원 돌기 줄. 가시: hit 삼각형 반복.
    /// </summary>
    public sealed class ArenaView : MonoBehaviour
    {
        /// <summary>단방향 발판을 그리는 두께(시뮬레이션의 칸 위쪽 절반 0.5 중 윗부분). 그림자도 이 판으로 만든다.</summary>
        public const float OneWayThickness = 0.24f;
        const float BumpSpacing = 0.5f;
        const float BumpSize = 0.34f;
        const float SpikeWidth = 0.5f;

        ArenaDef current;
        string currentTheme;

        public void Build(ArenaDef arena, ArenaTheme theme, PaletteAsset palette, ShapeLibrary shapes)
        {
            if (arena == current && theme.name == currentTheme)
                return;
            current = arena;
            currentTheme = theme.name;
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            for (int i = 0; i < arena.BoxCount; i++)
            {
                var b = arena.GetBox(i);
                // 경기장 맨 위 테두리 줄은 그리지 않는다: 화면 위에 띠나 액자가 없어야 하고(08 4·7절), 광원이 그 바깥에 있다.
                // 천장은 시뮬레이션에 그대로 있다(화면 위 끝에 부딪힌다).
                if (b.MinY >= arena.HalfHeight - 1f - 1e-3f)
                    continue;
                switch (b.Kind)
                {
                    case TerrainKind.Solid:
                        Rect(shapes, theme.terrain, SortingLayers.Terrain, 0, b.MinX, b.MinY, b.MaxX, b.MaxY);
                        break;
                    case TerrainKind.OneWay:
                        Rect(shapes, theme.terrain, SortingLayers.Terrain, 2, b.MinX, b.MaxY - OneWayThickness, b.MaxX, b.MaxY);
                        break;
                    case TerrainKind.Bounce:
                        Rect(shapes, theme.terrain, SortingLayers.Terrain, 2, b.MinX, b.MinY, b.MaxX, b.MaxY - BumpSize * 0.5f);
                        for (float x = b.MinX + BumpSpacing * 0.5f; x < b.MaxX; x += BumpSpacing)
                        {
                            var bump = ShapeLibrary.Create(transform, "Bump", shapes.Circle, SortingLayers.Terrain, 1, palette.energySoft);
                            bump.transform.localPosition = new Vector3(x, b.MaxY - BumpSize * 0.5f, 0f);
                            ShapeLibrary.SetSize(bump, BumpSize, BumpSize);
                        }
                        break;
                    case TerrainKind.Spike:
                        for (float x = b.MinX; x < b.MaxX - 1e-3f; x += SpikeWidth)
                        {
                            var spike = ShapeLibrary.Create(transform, "Spike", shapes.Triangle, SortingLayers.Terrain, 2, palette.hit);
                            spike.transform.localPosition = new Vector3(x + SpikeWidth * 0.5f, (b.MinY + b.MaxY) * 0.5f, 0f);
                            ShapeLibrary.SetSize(spike, SpikeWidth, b.MaxY - b.MinY);
                        }
                        break;
                }
            }
        }

        void Rect(ShapeLibrary shapes, Color color, string layer, int order, float minX, float minY, float maxX, float maxY)
        {
            var sr = ShapeLibrary.Create(transform, "Rect", shapes.Square, layer, order, color);
            sr.transform.localPosition = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
            ShapeLibrary.SetSize(sr, maxX - minX, maxY - minY);
        }
    }
}

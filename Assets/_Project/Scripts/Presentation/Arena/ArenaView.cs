using System.Collections.Generic;
using Knockbox.Simulation;
using UnityEngine;

namespace Knockbox.Presentation
{
    /// <summary>
    /// 지형 그리기(05-presentation.md 1절). 시뮬레이션과 같은 상자 목록으로 그리므로 둘이 어긋날 수 없다.
    /// 단단한 지형: ink 몸체 + 드러난 윗면에 terrainLip 띠(0.15) + 평면 그림자(외곽선 없음).
    /// 단방향 발판: 얇고 밝은 판. 튕김 패드: 반원 돌기 줄. 가시: 삼각형 반복.
    /// </summary>
    public sealed class ArenaView : MonoBehaviour
    {
        const float LipThickness = 0.15f;
        const float ShadowOffset = 0.08f;
        const float OneWayThickness = 0.24f;
        const float BumpSpacing = 0.5f;
        const float BumpSize = 0.34f;
        const float SpikeWidth = 0.5f;

        ArenaDef current;
        readonly List<(float min, float max)> scratch = new List<(float, float)>();

        public void Build(ArenaDef arena, PaletteAsset palette, ShapeLibrary shapes)
        {
            if (arena == current)
                return;
            current = arena;
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            for (int i = 0; i < arena.BoxCount; i++)
            {
                var b = arena.GetBox(i);
                switch (b.Kind)
                {
                    case TerrainKind.Solid:
                        Rect(shapes, palette.shadow, SortingLayers.Shadow, 0, b.MinX + ShadowOffset, b.MinY - ShadowOffset, b.MaxX + ShadowOffset, b.MaxY - ShadowOffset);
                        Rect(shapes, palette.ink, SortingLayers.Terrain, 0, b.MinX, b.MinY, b.MaxX, b.MaxY);
                        // 경기장 바깥 테두리 윗면(화면 맨 위)에는 띠를 그리지 않는다.
                        if (b.MaxY < ArenaDef.HalfHeight - 1e-3f)
                            foreach (var (min, max) in ExposedTop(arena, i))
                            Rect(shapes, palette.terrainLip, SortingLayers.Terrain, 1, min, b.MaxY - LipThickness, max, b.MaxY);
                        break;
                    case TerrainKind.OneWay:
                        Rect(shapes, palette.shadow, SortingLayers.Shadow, 0, b.MinX + ShadowOffset, b.MaxY - OneWayThickness - ShadowOffset, b.MaxX + ShadowOffset, b.MaxY - ShadowOffset);
                        Rect(shapes, palette.oneWay, SortingLayers.Terrain, 2, b.MinX, b.MaxY - OneWayThickness, b.MaxX, b.MaxY);
                        break;
                    case TerrainKind.Bounce:
                        Rect(shapes, palette.bouncy, SortingLayers.Terrain, 2, b.MinX, b.MinY, b.MaxX, b.MaxY - BumpSize * 0.5f);
                        for (float x = b.MinX + BumpSpacing * 0.5f; x < b.MaxX; x += BumpSpacing)
                        {
                            var bump = ShapeLibrary.Create(transform, "Bump", shapes.Circle, SortingLayers.Terrain, 2, palette.bouncy);
                            bump.transform.localPosition = new Vector3(x, b.MaxY - BumpSize * 0.5f, 0f);
                            ShapeLibrary.SetSize(bump, BumpSize, BumpSize);
                        }
                        break;
                    case TerrainKind.Spike:
                        for (float x = b.MinX; x < b.MaxX - 1e-3f; x += SpikeWidth)
                        {
                            var spike = ShapeLibrary.Create(transform, "Spike", shapes.Triangle, SortingLayers.Terrain, 2, palette.hazard);
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

        /// <summary>단단한 상자 윗면 중 바로 위에 다른 단단한 상자가 붙어 있지 않은 구간.</summary>
        List<(float min, float max)> ExposedTop(ArenaDef arena, int index)
        {
            var b = arena.GetBox(index);
            scratch.Clear();
            scratch.Add((b.MinX, b.MaxX));
            for (int i = 0; i < arena.BoxCount; i++)
            {
                var o = arena.GetBox(i);
                if (i == index || o.Kind != TerrainKind.Solid || Mathf.Abs(o.MinY - b.MaxY) > 1e-3f)
                    continue;
                for (int k = scratch.Count - 1; k >= 0; k--)
                {
                    var (min, max) = scratch[k];
                    if (o.MaxX <= min || o.MinX >= max)
                        continue;
                    scratch.RemoveAt(k);
                    if (o.MinX > min)
                        scratch.Add((min, o.MinX));
                    if (o.MaxX < max)
                        scratch.Add((o.MaxX, max));
                }
            }
            return scratch;
        }
    }
}

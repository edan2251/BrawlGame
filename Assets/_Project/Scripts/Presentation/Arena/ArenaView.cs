using Knockbox.Simulation;
using UnityEngine;

namespace Knockbox.Presentation
{
    /// <summary>
    /// 지형 그리기(임시). 시뮬레이션과 같은 상자 목록으로 그리므로 둘이 어긋날 수 없다.
    /// CP3에서 윗면 띠, 평면 그림자, 종류별 무늬를 붙인다.
    /// </summary>
    public sealed class ArenaView : MonoBehaviour
    {
        public const int SortingOrder = 0;

        ArenaDef current;

        public void Build(ArenaDef arena, PaletteAsset palette)
        {
            if (arena == current)
                return;
            current = arena;
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            for (int i = 0; i < arena.BoxCount; i++)
            {
                ref readonly var b = ref arena.GetBox(i);
                var go = new GameObject($"{b.Kind} {i}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3((b.MinX + b.MaxX) * 0.5f, (b.MinY + b.MaxY) * 0.5f, 0f);
                go.transform.localScale = new Vector3(b.MaxX - b.MinX, b.MaxY - b.MinY, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = ShapeSprites.Square;
                sr.color = ColorFor(b.Kind, palette);
                sr.sortingOrder = SortingOrder;
            }
        }

        static Color ColorFor(TerrainKind kind, PaletteAsset palette)
        {
            switch (kind)
            {
                case TerrainKind.OneWay: return palette.oneWay;
                case TerrainKind.Bounce: return palette.bouncy;
                case TerrainKind.Spike: return palette.hazard;
                default: return palette.ink;
            }
        }
    }
}

using UnityEngine;

namespace Knockbox.Presentation
{
    /// <summary>배경: 단색 바탕(카메라 배경색) 위에 큰 사각형과 원 몇 개의 실루엣. 움직이지 않는다.</summary>
    public sealed class BackgroundView : MonoBehaviour
    {
        public void Build(PaletteAsset palette, ShapeLibrary shapes)
        {
            if (transform.childCount > 0)
                return;
            // 경기장 32 × 18 안쪽에 고정 배치. 지형과 겹쳐도 지형이 위에 그려진다.
            Shape(shapes.Square, palette.bgShape, -10.5f, 1.5f, 7f, 9f, 0f);
            Shape(shapes.Square, palette.bgShape, 11f, -1f, 5f, 12f, 0f);
            Shape(shapes.Circle, palette.bgShape, -3f, 5f, 6f, 6f, 0f);
            Shape(shapes.Circle, palette.bgShape, 6.5f, 6.2f, 3f, 3f, 0f);
            Shape(shapes.Square, palette.bgShape, 2f, -5.5f, 9f, 3f, 0f);
        }

        void Shape(Sprite sprite, Color color, float x, float y, float width, float height, float angle)
        {
            var sr = ShapeLibrary.Create(transform, "Silhouette", sprite, SortingLayers.Background, 0, color);
            sr.transform.localPosition = new Vector3(x, y, 0f);
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            ShapeLibrary.SetSize(sr, width, height);
        }
    }
}

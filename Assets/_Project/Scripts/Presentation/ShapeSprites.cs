using UnityEngine;

namespace Knockbox.Presentation
{
    /// <summary>
    /// 실행 중에 만드는 단순 도형 스프라이트. CP3에서 에디터 SpriteFactory가 만든 에셋으로 바꾼다.
    /// </summary>
    public static class ShapeSprites
    {
        static Sprite square;

        /// <summary>한 변 1유닛, 가운데 기준의 흰 사각형.</summary>
        public static Sprite Square
        {
            get
            {
                if (square != null)
                    return square;
                var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                var pixels = new Color32[16];
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32(255, 255, 255, 255);
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                square = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                square.hideFlags = HideFlags.HideAndDontSave;
                square.name = "KnockboxSquare";
                return square;
            }
        }
    }
}

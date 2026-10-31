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

        /// <summary>
        /// 배경 질감용 저주파 노이즈 마스크(08 4절): 흰색에 노이즈 값을 알파로 담는다(0 ~ maxAlpha).
        /// 색은 이 마스크를 그리는 렌더러가 팔레트에서 정한다. Mathf.PerlinNoise라 실행마다 같다.
        /// </summary>
        public static Texture2D NoiseMask(int width, int height, float scale, float maxAlpha)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                name = "KnockboxNoise",
            };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 두 옥타브: 큰 얼룩 + 붓질 같은 가로로 긴 결.
                    float n = Mathf.PerlinNoise(x * scale + 17.3f, y * scale + 41.9f) * 0.7f
                              + Mathf.PerlinNoise(x * scale * 3.1f + 5.1f, y * scale * 9.7f + 2.7f) * 0.3f;
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(n) * maxAlpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}

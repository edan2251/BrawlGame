using System.Collections.Generic;
using Knockbox.Simulation;
using UnityEngine;

namespace Knockbox.Presentation
{
    /// <summary>
    /// 배경(08-visual-direction.md 3·4절, D-028): 화면 위 가운데 높은 곳의 광원 하나가 화면 전체를 테마의 빛 색으로 비춘다.
    /// 위쪽 가운데가 가장 밝고 아래·가장자리로 서서히 어두워지며(원뿔 테두리 없음), 지형이 광원 반대쪽으로 드리우는 그림자는
    /// 같은 빛 색의 75% 밝기이고 경계가 부드럽다(넓은 광원 표본 + 흐림). 지형·캐릭터는 이 위에 플랫하게 그려진다.
    /// 배경 전체를 텍스처 한 장으로 직접 계산해 그린다(08 3절이 허용한 대안). 텍스처 값이 곧 화면의 sRGB 값이라
    /// 밝기 비율(감마 휘도)이 설계값 그대로 나오고, 캡처가 재현된다. 맵·테마·광원이 같으면 만든 텍스처를 다시 쓴다.
    /// </summary>
    public sealed class BackgroundView : MonoBehaviour
    {
        /// <summary>광원 위치: 경기장 위쪽 끝에서 경기장 높이의 3/4 위(D-028 "지금보다 높이", 이전 1/4).</summary>
        public static Vector2 DefaultLight(ArenaDef arena) => new Vector2(0f, arena.HalfHeight + arena.Rows * 0.75f);

        /// <summary>그림자 영역 밝기 = 같은 곳의 빛 × (1 − ShadowStrength). D-028 (1): 65~85%.</summary>
        public const float ShadowStrength = 0.25f;
        /// <summary>
        /// 밝기 = (광원~화면 위 가운데 거리 / 광원~점 거리)^FalloffExponent. 광원이 경기장 높이의 3/4 위에 있을 때
        /// 화면 아래 가운데 약 0.55, 아래 구석 약 0.5(D-028 (3): 40~60%). 비율이라 경기장 크기와 상관없다.
        /// </summary>
        public const float FalloffExponent = 0.7f;
        /// <summary>넓은 광원의 반폭과 표본 수(반그림자).</summary>
        const float LightHalfWidth = 2f;
        const int LightSamples = 8;
        /// <summary>텍스처 해상도(유닛당 텍셀)와 경기장보다 넓게 덮는 여유. 흔들림·확대에도 가장자리가 보이지 않게.</summary>
        const int TexelsPerUnit = 6;
        const float CoverMarginX = 4f;
        const float CoverMarginY = 2.25f;
        /// <summary>그림자 마스크 흐림(상자 흐림 반경 텍셀 × 횟수). 경계 전환 폭이 1080p에서 30픽셀 이상(D-028 (2)).</summary>
        const int BlurRadius = 2;
        const int BlurPasses = 2;
        /// <summary>붓질 같은 저주파 질감(08 4절, 밝기 ±3% 안팎).</summary>
        const float NoiseDepth = 0.06f;
        const int MoteCount = 26;

        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        Vector2 lightPoint;
        SpriteRenderer baseRenderer;
        Sprite baseSprite;
        readonly SpriteRenderer[] motes = new SpriteRenderer[MoteCount];
        readonly Vector2[] motePosition = new Vector2[MoteCount];
        readonly Vector2[] moteVelocity = new Vector2[MoteCount];
        readonly float[] moteSize = new float[MoteCount];
        FxRandom random;
        PaletteAsset palette;
        string currentKey;
        float[] shadowMask;
        int width;
        int height;
        float coverWidth;
        float coverHeight;
        float halfWidth = ArenaDef.ClassicColumns * 0.5f;
        float halfHeight = ArenaDef.ClassicRows * 0.5f;

        public Vector2 LightPosition => lightPoint;

        public void Build(ArenaDef arena, ArenaTheme theme, PaletteAsset paletteAsset, ShapeLibrary shapes, Vector2 lightPosition)
        {
            palette = paletteAsset;
            if (motes[0] == null)
            {
                for (int i = 0; i < MoteCount; i++)
                    motes[i] = ShapeLibrary.Create(transform, "Mote", shapes.Circle, SortingLayers.Background, 3, palette.mote);
            }
            string key = $"{arena.Name}#{arena.ComputeChecksum():X8}|{theme.name}|{lightPosition.x:F2},{lightPosition.y:F2}";
            if (key == currentKey)
                return;
            currentKey = key;
            lightPoint = lightPosition;
            halfWidth = arena.HalfWidth;
            halfHeight = arena.HalfHeight;
            coverWidth = arena.Columns + CoverMarginX;
            coverHeight = arena.Rows + CoverMarginY;
            width = Mathf.RoundToInt(coverWidth * TexelsPerUnit);
            height = Mathf.RoundToInt(coverHeight * TexelsPerUnit);
            shadowMask = ComputeShadowMask(arena);
            if (!Cache.TryGetValue(key, out var texture) || texture == null)
            {
                texture = CreateTexture(theme);
                Cache[key] = texture;
            }
            if (baseSprite != null)
                Destroy(baseSprite);
            baseSprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), TexelsPerUnit);
            baseSprite.hideFlags = HideFlags.HideAndDontSave;
            if (baseRenderer == null)
                baseRenderer = ShapeLibrary.Create(transform, "Background", baseSprite, SortingLayers.Background, 0, palette.flash);
            baseRenderer.sprite = baseSprite;
            baseRenderer.drawMode = SpriteDrawMode.Simple;
            baseRenderer.color = palette.flash;
            for (int i = 0; i < MoteCount; i++)
                motes[i].color = PaletteAsset.WithAlpha(palette.mote, 0.4f);
            ResetMotes();
        }

        Vector2 TexelCenter(int x, int y) =>
            new Vector2(-coverWidth * 0.5f + (x + 0.5f) / TexelsPerUnit, -coverHeight * 0.5f + (y + 0.5f) / TexelsPerUnit);

        /// <summary>광원 감쇠(위쪽 가운데 1, 아래·가장자리로 서서히 어두워짐).</summary>
        public float Falloff(Vector2 point)
        {
            float top = Mathf.Abs(lightPoint.y - halfHeight);
            float distance = Mathf.Max(top, Vector2.Distance(point, lightPoint));
            return Mathf.Pow(top / distance, FalloffExponent);
        }

        /// <summary>그림자 정도(0 빛, 1 완전한 그림자). 흐린 마스크를 이중 선형으로 읽는다.</summary>
        public float ShadowAmount(Vector2 point)
        {
            if (shadowMask == null)
                return 0f;
            float fx = (point.x + coverWidth * 0.5f) * TexelsPerUnit - 0.5f;
            float fy = (point.y + coverHeight * 0.5f) * TexelsPerUnit - 0.5f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, width - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(fy), 0, height - 1);
            int x1 = Mathf.Min(x0 + 1, width - 1), y1 = Mathf.Min(y0 + 1, height - 1);
            float tx = Mathf.Clamp01(fx - x0), ty = Mathf.Clamp01(fy - y0);
            float a = Mathf.Lerp(shadowMask[y0 * width + x0], shadowMask[y0 * width + x1], tx);
            float b = Mathf.Lerp(shadowMask[y1 * width + x0], shadowMask[y1 * width + x1], tx);
            return Mathf.Lerp(a, b, ty);
        }

        /// <summary>
        /// 텍셀마다 넓은 광원(가로 선분)의 표본 점들을 향한 선분이 지형에 막히는 비율 → 두 번 상자 흐림.
        /// 경기장 위쪽 테두리(광원이 그 바깥에 있다)와 가시(낮다)는 그림자를 만들지 않고, 단방향 발판은 그려지는 얇은 판으로 막는다.
        /// 지형 상자 안쪽 텍셀은 빛으로 둔다(지형에 가려 보이지 않고, 흐림이 지형 윗면 위로 어둠을 번지게 하지 않도록).
        /// </summary>
        float[] ComputeShadowMask(ArenaDef arena)
        {
            var boxes = new List<Rect>();
            var all = new List<Rect>();
            for (int i = 0; i < arena.BoxCount; i++)
            {
                var b = arena.GetBox(i);
                float minY = b.Kind == TerrainKind.OneWay ? b.MaxY - ArenaView.OneWayThickness : b.MinY;
                var rect = Rect.MinMaxRect(b.MinX, minY, b.MaxX, b.MaxY);
                all.Add(rect);
                if (b.Kind != TerrainKind.Spike && b.MaxY < halfHeight - 1e-3f)
                    boxes.Add(rect);
            }
            var mask = new float[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var p = TexelCenter(x, y);
                    bool inside = false;
                    for (int k = 0; k < all.Count && !inside; k++)
                        inside = all[k].Contains(p);
                    if (inside)
                        continue;
                    int blocked = 0;
                    for (int s = 0; s < LightSamples; s++)
                    {
                        var light = lightPoint + new Vector2(Mathf.Lerp(-LightHalfWidth, LightHalfWidth, (s + 0.5f) / LightSamples), 0f);
                        for (int k = 0; k < boxes.Count; k++)
                        {
                            if (SegmentHitsRect(p, light, boxes[k]))
                            {
                                blocked++;
                                break;
                            }
                        }
                    }
                    mask[y * width + x] = blocked / (float)LightSamples;
                }
            }
            var scratch = new float[mask.Length];
            for (int pass = 0; pass < BlurPasses; pass++)
            {
                BoxBlur(mask, scratch, width, height, BlurRadius, true);
                BoxBlur(scratch, mask, width, height, BlurRadius, false);
            }
            return mask;
        }

        static void BoxBlur(float[] source, float[] target, int w, int h, int radius, bool horizontal)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float sum = 0f;
                    int n = 0;
                    for (int d = -radius; d <= radius; d++)
                    {
                        int sx = horizontal ? x + d : x, sy = horizontal ? y : y + d;
                        if (sx < 0 || sy < 0 || sx >= w || sy >= h)
                            continue;
                        sum += source[sy * w + sx];
                        n++;
                    }
                    target[y * w + x] = sum / n;
                }
            }
        }

        /// <summary>선분 a→b가 사각형의 안쪽을 지나는가(슬랩 판정).</summary>
        static bool SegmentHitsRect(Vector2 a, Vector2 b, Rect r)
        {
            float t0 = 0f, t1 = 1f;
            var d = b - a;
            return Clip(-d.x, a.x - r.xMin, ref t0, ref t1) && Clip(d.x, r.xMax - a.x, ref t0, ref t1)
                && Clip(-d.y, a.y - r.yMin, ref t0, ref t1) && Clip(d.y, r.yMax - a.y, ref t0, ref t1) && t1 > t0 + 1e-5f;
        }

        static bool Clip(float p, float q, ref float t0, ref float t1)
        {
            if (Mathf.Abs(p) < 1e-9f)
                return q >= 0f;
            float t = q / p;
            if (p < 0f)
            {
                if (t > t1)
                    return false;
                if (t > t0)
                    t0 = t;
            }
            else
            {
                if (t < t0)
                    return false;
                if (t < t1)
                    t1 = t;
            }
            return true;
        }

        /// <summary>텍셀 색(sRGB) = 빛 색 × 감쇠 × (1 − 그림자 × 세기) × (1 − 질감). 그림자는 빛과 같은 색 계열의 어둠이다.</summary>
        Texture2D CreateTexture(ArenaTheme theme)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                name = "KnockboxBackground",
            };
            var noise = ShapeSprites.NoiseValues(width, height, 0.035f * 6f / TexelsPerUnit);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    float k = Falloff(TexelCenter(x, y)) * (1f - shadowMask[i] * ShadowStrength) * (1f - noise[i] * NoiseDepth);
                    var c = theme.light * k;
                    c.a = 1f;
                    pixels[i] = c;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>먼지 입자를 처음 배치로(라운드·시나리오마다 같은 화면이 나오게).</summary>
        public void ResetMotes()
        {
            random = new FxRandom(0xD057u);
            for (int i = 0; i < MoteCount; i++)
                Respawn(i, true);
            Tick(0f);
        }

        void Respawn(int i, bool anywhere)
        {
            float y = anywhere ? random.Range(-halfHeight + 1f, halfHeight - 1f) : random.Range(-halfHeight + 1f, -2f);
            motePosition[i] = new Vector2(random.Range(-halfWidth + 1f, halfWidth - 1f), y);
            moteVelocity[i] = new Vector2(random.Range(-0.18f, 0.18f), random.Range(0.06f, 0.28f));
            moteSize[i] = random.Range(0.06f, 0.12f);
        }

        /// <summary>먼지가 천천히 떠오른다. 화면을 벗어나면 아래쪽에서 다시 생긴다(연출 시간 dt).</summary>
        public void Tick(float dt)
        {
            if (palette == null)
                return;
            for (int i = 0; i < MoteCount; i++)
            {
                motePosition[i] += moteVelocity[i] * dt;
                if (motePosition[i].y > halfHeight - 0.5f || Mathf.Abs(motePosition[i].x) > halfWidth - 0.5f)
                    Respawn(i, false);
                motes[i].transform.localPosition = motePosition[i];
                ShapeLibrary.SetSize(motes[i], moteSize[i], moteSize[i]);
            }
        }

        void OnDestroy()
        {
            if (baseSprite != null)
                Destroy(baseSprite);
        }
    }
}

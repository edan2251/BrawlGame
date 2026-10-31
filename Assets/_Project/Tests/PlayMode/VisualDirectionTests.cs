using System.Collections;
using System.Collections.Generic;
using Knockbox.Game;
using Knockbox.Presentation;
using Knockbox.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Knockbox.Tests.PlayMode
{
    /// <summary>
    /// CP11-2(대비와 흑백 구분), CP11-3(그림자·광원, D-028): 테마 3종을 1080p 렌더 텍스처(후처리 포함)로 그려 픽셀로 잰다.
    /// 두 파이터는 Box 가운데 발판 위(x ±1.5, y 6)에 세우고 틱을 진행하지 않는다.
    /// "밝기"는 화면 sRGB 값의 감마 휘도(Rec.601: 0.299R + 0.587G + 0.114B)다. 사용자가 Rounds 캡처에서 잰 비율과 같은 척도.
    /// 그림자는 가운데 발판(x −4~4, 윗면 y 4) 바로 아래(0, 2.5)와 같은 높이의 빛 받는 곳(−6.5, 2.5)을 비교한다.
    /// </summary>
    public class VisualDirectionTests
    {
        const int Width = 1920;
        const int Height = 1080;
        MatchRunner runner;
        MatchPresenter presenter;
        Camera camera;

        IEnumerator LoadSandbox()
        {
            yield return SceneManager.LoadSceneAsync("Sandbox", LoadSceneMode.Single);
            yield return null;
            runner = Object.FindFirstObjectByType<MatchRunner>();
            presenter = Object.FindFirstObjectByType<MatchPresenter>();
            camera = Camera.main;
            Assert.IsNotNull(runner);
            Assert.IsNotNull(presenter);
        }

        static float Linear(float srgb) => srgb <= 0.04045f ? srgb / 12.92f : Mathf.Pow((srgb + 0.055f) / 1.055f, 2.4f);
        static float Luminance(Color c) => 0.2126f * Linear(c.r) + 0.7152f * Linear(c.g) + 0.0722f * Linear(c.b);
        static float Contrast(float a, float b) => (Mathf.Max(a, b) + 0.05f) / (Mathf.Min(a, b) + 0.05f);
        static float Luma(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        Vector2Int ToPixel(Vector2 world)
        {
            float halfH = camera.orthographicSize;
            float halfW = halfH * Width / Height;
            Vector3 c = camera.transform.position;
            return new Vector2Int(Mathf.RoundToInt((world.x - c.x + halfW) / (2f * halfW) * Width),
                Mathf.RoundToInt((world.y - c.y + halfH) / (2f * halfH) * Height));
        }

        /// <summary>반경 안 픽셀 휘도의 중앙값(먼지 입자 같은 작은 점을 피한다).</summary>
        static float MedianLuminance(Texture2D tex, Vector2Int p, int radius) => Median(tex, p, radius, Luminance);
        static float MedianLuma(Texture2D tex, Vector2Int p, int radius) => Median(tex, p, radius, Luma);

        static float Median(Texture2D tex, Vector2Int p, int radius, System.Func<Color, float> measure)
        {
            var values = new List<float>();
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
                values.Add(measure(tex.GetPixel(p.x + dx, p.y + dy)));
            values.Sort();
            return values[values.Count / 2];
        }

        /// <summary>테두리 벽만 있는 빈 경기장(광원 측정용).</summary>
        static ArenaDef EmptyArena()
        {
            var rows = new System.Text.StringBuilder("name: Empty\n---\n");
            for (int r = 0; r < ArenaDef.ClassicRows; r++)
            {
                for (int c = 0; c < ArenaDef.ClassicColumns; c++)
                {
                    bool border = r == 0 || r == ArenaDef.ClassicRows - 1 || c == 0 || c == ArenaDef.ClassicColumns - 1;
                    rows.Append(border ? '#' : r == ArenaDef.ClassicRows - 2 && c == 3 ? '1' : r == ArenaDef.ClassicRows - 2 && c == ArenaDef.ClassicColumns - 4 ? '2' : '.');
                }
                rows.Append('\n');
            }
            return ArenaFiles.Parse(rows.ToString(), "Empty");
        }

        IEnumerator RenderTheme(int theme, System.Action<Texture2D> measure, ArenaDef arenaOverride = null)
        {
            presenter.ThemeOverride = theme;
            presenter.SetDebugText(false);
            var config = runner.CreateConfig();
            var arena = arenaOverride ?? runner.ResolveArena("Box");
            runner.Begin(config, arena, SimState.CreateRound(config, arena, 1, new Vec2(-1.5f, 6f), new Vec2(1.5f, 6f), MatchPhase.Fighting));
            runner.Running = false;
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();
            var tex = ScenarioPlayer.RenderCamera(camera, Width, Height);
            try
            {
                measure(tex);
            }
            finally
            {
                Object.Destroy(tex);
            }
        }

        [UnityTest]
        public IEnumerator EveryTheme_FightersStandOutFromLitBackground()
        {
            yield return LoadSandbox();
            var failures = new List<string>();
            for (int theme = 0; theme < 3; theme++)
            {
                int t = theme;
                yield return RenderTheme(theme, tex =>
                {
                    Assert.Less(presenter.Background.ShadowAmount(new Vector2(-3.5f, 6f)), 0.02f, "배경 비교 지점이 빛 받는 곳이어야 함");
                    float background = MedianLuminance(tex, ToPixel(new Vector2(-3.5f, 6f)), 4);
                    var body = new float[2];
                    for (int i = 0; i < 2; i++)
                    {
                        // 몸 중심에서 눈 아래쪽(눈과 외곽선을 피한 곳).
                        Vector2 center = presenter.Fighter(i).Body.transform.position;
                        body[i] = MedianLuminance(tex, ToPixel(center + new Vector2(0f, -0.2f)), 2);
                    }
                    float c1 = Contrast(body[0], background), c2 = Contrast(body[1], background), diff = Mathf.Abs(body[0] - body[1]);
                    string report = $"[Knockbox][CP11-2] 테마 {presenter.CurrentTheme.name}: 빛 받는 배경 휘도 {background:F4}, P1 {body[0]:F3}(대비 {c1:F1}:1), P2 {body[1]:F3}(대비 {c2:F1}:1), 흑백 차 {diff:F3}";
                    Debug.Log(report);
                    TestContext.WriteLine(report);
                    if (c1 < 4.5f || c2 < 3.5f || diff < 0.25f)
                        failures.Add(report);
                });
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        /// <summary>CP11-3 (1)·(2): 그림자 밝기는 같은 높이 빛의 65~85%, 경계 전환 폭은 1080p에서 30픽셀 이상.</summary>
        [UnityTest]
        public IEnumerator EveryTheme_ShadowIsSoftAndPartial()
        {
            yield return LoadSandbox();
            var failures = new List<string>();
            for (int theme = 0; theme < 3; theme++)
            {
                yield return RenderTheme(theme, tex =>
                {
                    var shadowPoint = new Vector2(0f, 2.5f);
                    var litPoint = new Vector2(-6.5f, 2.5f);
                    Assert.Greater(presenter.Background.ShadowAmount(shadowPoint), 0.98f, "그림자 측정점은 완전한 그림자여야 함");
                    Assert.Less(presenter.Background.ShadowAmount(litPoint), 0.02f, "빛 측정점은 그림자가 없어야 함");
                    float shadow = MedianLuma(tex, ToPixel(shadowPoint), 4);
                    float lit = MedianLuma(tex, ToPixel(litPoint), 4);
                    float ratio = shadow / Mathf.Max(lit, 1e-5f);

                    // 경계 전환 폭: y 2.5에서 x −7.5 → −1.5로 가로지르며(가운데 발판 그림자의 왼쪽 경계) 열마다 세로 9픽셀의 중앙값.
                    int from = ToPixel(new Vector2(-7.5f, 2.5f)).x, to = ToPixel(new Vector2(-1.5f, 2.5f)).x, row = ToPixel(shadowPoint).y;
                    var profile = new List<float>();
                    for (int x = from; x <= to; x++)
                        profile.Add(MedianLuma(tex, new Vector2Int(x, row), 4));
                    float high = 0f, low = 0f;
                    for (int k = 0; k < 20; k++)
                    {
                        high += profile[k] / 20f;
                        low += profile[profile.Count - 1 - k] / 20f;
                    }
                    int start = -1, end = -1;
                    for (int k = 0; k < profile.Count; k++)
                    {
                        float t = (high - profile[k]) / Mathf.Max(high - low, 1e-5f);
                        if (start < 0 && t >= 0.1f)
                            start = k;
                        if (end < 0 && t >= 0.9f)
                            end = k;
                    }
                    int edgeWidth = end - start;
                    string report = $"[Knockbox][CP11-3] 테마 {presenter.CurrentTheme.name}: 그림자 밝기 {shadow:F3}, 같은 높이 빛 {lit:F3} → {ratio * 100f:F0}%(기준 65~85%), " +
                                    $"경계 전환 폭 {edgeWidth}px(10%→90%, 기준 30 이상)";
                    Debug.Log(report);
                    TestContext.WriteLine(report);
                    if (ratio < 0.65f || ratio > 0.85f || start < 0 || end < 0 || edgeWidth < 30)
                        failures.Add(report);
                });
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        /// <summary>
        /// CP11-3 (3): 빈 경기장에서 광원이 화면 전체를 비춘다. 위쪽 가운데가 가장 밝고, 아래쪽 가장자리(가운데·양 구석)는 그 40~60%,
        /// 가로 줄을 따라 이웃한 측정점(0.25 유닛 간격) 사이에 계단(위쪽 가운데 밝기의 8% 넘는 차이)이 없고, 화면 어디도 그 35% 아래로 꺼지지 않는다.
        /// 8%는 배경 질감(밝기 ±3%, 이웃끼리 최대 약 6%)과 감쇠 기울기를 넘는 값이다. 원뿔 테두리 같은 경계는 수십 % 계단이라 걸린다.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryTheme_LightCoversScreenWithSmoothFalloff()
        {
            yield return LoadSandbox();
            var failures = new List<string>();
            var arena = EmptyArena();
            for (int theme = 0; theme < 3; theme++)
            {
                yield return RenderTheme(theme, tex =>
                {
                    float top = MedianLuma(tex, ToPixel(new Vector2(0f, 8.3f)), 4);
                    float bottom = MedianLuma(tex, ToPixel(new Vector2(0f, -7.7f)), 4);
                    float bottomLeft = MedianLuma(tex, ToPixel(new Vector2(-14.5f, -7.7f)), 4);
                    float bottomRight = MedianLuma(tex, ToPixel(new Vector2(14.5f, -7.7f)), 4);
                    float worstStep = 0f, darkest = float.MaxValue, brightest = 0f;
                    foreach (float y in new[] { 8.3f, 4f, 0f, -4f, -7.7f })
                    {
                        float previous = -1f;
                        for (float x = -14.5f; x <= 14.5f + 1e-3f; x += 0.25f)
                        {
                            float v = MedianLuma(tex, ToPixel(new Vector2(x, y)), 3);
                            darkest = Mathf.Min(darkest, v);
                            brightest = Mathf.Max(brightest, v);
                            if (previous >= 0f)
                                worstStep = Mathf.Max(worstStep, Mathf.Abs(v - previous));
                            previous = v;
                        }
                    }
                    float rb = bottom / top, rl = bottomLeft / top, rr = bottomRight / top;
                    string report = $"[Knockbox][CP11-3] 테마 {presenter.CurrentTheme.name}: 위 가운데 {top:F3}, 아래 가운데 {rb * 100f:F0}%, 아래 왼쪽 {rl * 100f:F0}%, 아래 오른쪽 {rr * 100f:F0}%(기준 40~60%), " +
                                    $"가장 어두운 곳 {darkest / top * 100f:F0}%, 가장 밝은 곳 {brightest / top * 100f:F0}%, 이웃 측정점 최대 차이 {worstStep / top * 100f:F1}%";
                    Debug.Log(report);
                    TestContext.WriteLine(report);
                    bool ok = rb >= 0.4f && rb <= 0.6f && rl >= 0.4f && rl <= 0.6f && rr >= 0.4f && rr <= 0.6f
                              && darkest >= 0.35f * top && brightest <= 1.05f * top && worstStep <= 0.08f * top;
                    if (!ok)
                        failures.Add(report);
                }, arena);
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [UnityTest]
        public IEnumerator PostFx_AberrationPulsesOnKoAndRestsAtZero()
        {
            yield return LoadSandbox();
            yield return RenderTheme(0, tex => { });
            var postFx = presenter.PostFx;
            Assert.AreEqual(0f, postFx.Aberration, 1e-4f, "평소 색수차 0");
            postFx.Pulse(0.6f, 0.25f);
            postFx.Tick(0f);
            Assert.AreEqual(0.6f, postFx.Aberration, 1e-4f);
            postFx.Tick(0.3f);
            Assert.AreEqual(0f, postFx.Aberration, 1e-4f, "0.25초 뒤 0");
            postFx.ReduceMotion = true;
            postFx.Pulse(0.6f, 0.25f);
            postFx.Tick(0f);
            Assert.AreEqual(0f, postFx.Aberration, 1e-4f, "흔들림 줄이기면 끔");
        }
    }
}

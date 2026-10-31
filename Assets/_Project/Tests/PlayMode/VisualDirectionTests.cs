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
    /// CP11-2(대비와 흑백 구분), CP11-3(그림자): 테마 3종을 1080p 렌더 텍스처(후처리 포함)로 그려 픽셀로 잰다.
    /// 두 파이터는 Box 가운데 발판 위 빛 원뿔 안(x ±1.5, y 6)에 세우고 틱을 진행하지 않는다.
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

        Vector2Int ToPixel(Vector2 world)
        {
            float halfH = camera.orthographicSize;
            float halfW = halfH * Width / Height;
            Vector3 c = camera.transform.position;
            return new Vector2Int(Mathf.RoundToInt((world.x - c.x + halfW) / (2f * halfW) * Width),
                Mathf.RoundToInt((world.y - c.y + halfH) / (2f * halfH) * Height));
        }

        /// <summary>반경 안 픽셀 휘도의 중앙값(먼지 입자 같은 작은 점을 피한다).</summary>
        static float MedianLuminance(Texture2D tex, Vector2Int p, int radius)
        {
            var values = new List<float>();
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
                values.Add(Luminance(tex.GetPixel(p.x + dx, p.y + dy)));
            values.Sort();
            return values[values.Count / 2];
        }

        IEnumerator RenderTheme(int theme, System.Action<Texture2D> measure)
        {
            presenter.ThemeOverride = theme;
            presenter.SetDebugText(false);
            var config = runner.CreateConfig();
            var arena = runner.ResolveArena("Box");
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
                    Assert.IsTrue(presenter.Background.InLightCone(new Vector2(-3.5f, 6f)), "배경 비교 지점이 빛 원뿔 안이어야 함");
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

        [UnityTest]
        public IEnumerator EveryTheme_TerrainCastsDarkShadowAwayFromLight()
        {
            yield return LoadSandbox();
            var failures = new List<string>();
            for (int theme = 0; theme < 3; theme++)
            {
                yield return RenderTheme(theme, tex =>
                {
                    var shadowPoint = new Vector2(0f, 2.5f);
                    var litPoint = new Vector2(-6.5f, 2.5f);
                    Assert.IsTrue(presenter.Background.InLightCone(litPoint));
                    float shadow = MedianLuminance(tex, ToPixel(shadowPoint), 4);
                    float lit = MedianLuminance(tex, ToPixel(litPoint), 4);
                    float ratio = lit / Mathf.Max(shadow, 1e-5f);
                    string report = $"[Knockbox][CP11-3] 테마 {presenter.CurrentTheme.name}: 가운데 발판 그림자 휘도 {shadow:F4}, 같은 높이 빛 받는 배경 {lit:F4}, {ratio:F1}배";
                    Debug.Log(report);
                    TestContext.WriteLine(report);
                    if (ratio < 1.8f)
                        failures.Add(report);
                });
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

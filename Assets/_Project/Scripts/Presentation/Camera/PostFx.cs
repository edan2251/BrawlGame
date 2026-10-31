using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Knockbox.Presentation
{
    /// <summary>
    /// 후처리(08-visual-direction.md 5절). 실행 중에 만드는 전역 Volume 하나:
    /// 블룸(문턱값을 높게 두어 흰 섬광, 에너지 색, 캐릭터 몸의 밝은 부분만), 색수차 펄스(평소 0, KO에 강하게, 강한 명중·벽꽝에 약하게),
    /// 약한 비네트. 흔들림 줄이기(F7)를 켜면 색수차를 끈다. 연출 전용이고 시뮬레이션과 무관하다.
    /// </summary>
    public sealed class PostFx : MonoBehaviour
    {
        /// <summary>감마 공간 문턱값. 고정 지형 중 가장 밝은 사이언(#0AB2E6, 최대 채널 0.90)은 넘지 않고, 에너지 민트(0.94)와 흰색, 몸 색(1.0)은 넘는다.</summary>
        public const float BloomThreshold = 0.92f;
        public const float BloomIntensity = 0.6f;
        public const float BloomScatter = 0.6f;
        public const float VignetteIntensity = 0.2f;

        Volume volume;
        VolumeProfile profile;
        ChromaticAberration aberration;
        float pulseStrength;
        float pulseDuration;
        float pulseAge;

        public bool ReduceMotion { get; set; }

        /// <summary>후처리 전체를 켜고 끈다(성능 비교·진단용).</summary>
        public bool VolumeEnabled
        {
            get => volume != null && volume.enabled;
            set
            {
                if (volume != null)
                    volume.enabled = value;
            }
        }

        /// <summary>지금 색수차 세기(테스트·캡처 확인용).</summary>
        public float Aberration => aberration != null ? aberration.intensity.value : 0f;

        public void Setup(Camera camera)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(BloomThreshold);
            bloom.intensity.Override(BloomIntensity);
            bloom.scatter.Override(BloomScatter);
            aberration = profile.Add<ChromaticAberration>(true);
            aberration.intensity.Override(0f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(VignetteIntensity);
            vignette.smoothness.Override(0.45f);
            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
            if (camera != null)
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        /// <summary>색수차를 strength에서 duration초 동안 0으로. 더 약한 펄스는 진행 중인 강한 펄스를 덮지 않는다.</summary>
        public void Pulse(float strength, float duration)
        {
            if (strength < Current())
                return;
            pulseStrength = strength;
            pulseDuration = duration;
            pulseAge = 0f;
        }

        float Current() => pulseDuration > 0f ? pulseStrength * Mathf.Clamp01(1f - pulseAge / pulseDuration) : 0f;

        public void Tick(float dt)
        {
            if (aberration == null)
                return;
            pulseAge += dt;
            aberration.intensity.Override(ReduceMotion ? 0f : Current());
        }

        public void ResetEffects()
        {
            pulseStrength = 0f;
            pulseDuration = 0f;
            pulseAge = 0f;
            Tick(0f);
        }

        void OnDestroy()
        {
            if (profile != null)
                Destroy(profile);
        }
    }
}

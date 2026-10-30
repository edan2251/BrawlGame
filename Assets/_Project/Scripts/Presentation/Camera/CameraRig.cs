using UnityEngine;

namespace Knockbox.Presentation
{
    /// <summary>
    /// 카메라 프레이밍과 화면 흔들림(05-presentation.md 1·4절).
    /// 프레이밍: 직교 크기 9(세로 18)로 32 × 18 경기장을 정확히 담고, 16:9보다 좁은 화면은 가로가 다 보이게 크기를 키운다.
    /// 남는 부분은 카메라 배경색(bgBase)으로 레터박스가 된다.
    /// 흔들림: 트라우마 방식. 오프셋 = 최대 진폭 0.35 × 트라우마² × 노이즈, 트라우마는 시간에 따라 줄어든다.
    /// 흔들림 줄이기(F7)를 켜면 30%로 줄인다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        public const float ArenaHalfWidth = 16f;
        public const float ArenaHalfHeight = 9f;
        public const float MaxShake = 0.35f;
        public const float ReducedShakeScale = 0.3f;
        const float TraumaDecay = 1.6f;
        const float NoiseFrequency = 22f;

        Camera cam;
        Vector3 basePosition;
        float trauma;
        float noiseTime;

        /// <summary>흔들림 줄이기(F7). 켜면 흔들림 30%, 화면 전체 번쩍임과 색 반전 끔(CP4).</summary>
        public bool ReduceMotion { get; set; }
        public float Trauma => trauma;
        public Vector2 ShakeOffset { get; private set; }

        void Awake()
        {
            cam = GetComponent<Camera>();
            basePosition = transform.localPosition;
        }

        public void AddTrauma(float amount) => trauma = Mathf.Clamp01(trauma + amount);

        /// <summary>연출 시간으로 진행한다(멈추면 흔들림도 멈춘다).</summary>
        public void Tick(float dt)
        {
            if (cam == null)
                Awake();
            Frame();
            noiseTime += dt;
            trauma = Mathf.Max(0f, trauma - TraumaDecay * dt);
            float amount = MaxShake * trauma * trauma * (ReduceMotion ? ReducedShakeScale : 1f);
            float nx = Mathf.PerlinNoise(noiseTime * NoiseFrequency, 0.37f) * 2f - 1f;
            float ny = Mathf.PerlinNoise(0.71f, noiseTime * NoiseFrequency) * 2f - 1f;
            ShakeOffset = new Vector2(nx, ny) * amount;
            transform.localPosition = basePosition + (Vector3)ShakeOffset;
        }

        void Frame()
        {
            float aspect = cam.aspect;
            float sizeForWidth = ArenaHalfWidth / Mathf.Max(aspect, 0.01f);
            cam.orthographicSize = Mathf.Max(ArenaHalfHeight, sizeForWidth);
        }
    }
}

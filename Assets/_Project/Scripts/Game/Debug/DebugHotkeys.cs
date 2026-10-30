using Knockbox.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Knockbox.Game
{
    /// <summary>
    /// 디버그 단축키(06-match-and-tools.md 4절) 중 지금 있는 기능만: F5 라운드 리셋(튜닝 재적용),
    /// F7 흔들림 줄이기, F8 SyncTest, P 일시정지, O 정지 중 한 틱 진행. 나머지는 해당 CP에서 붙인다.
    /// 에디터와 개발 빌드에서만 동작한다.
    /// </summary>
    [RequireComponent(typeof(MatchRunner))]
    public sealed class DebugHotkeys : MonoBehaviour
    {
        MatchRunner runner;
        CameraRig cameraRig;

        void Awake()
        {
            runner = GetComponent<MatchRunner>();
        }

        void Update()
        {
            if (!Debug.isDebugBuild && !Application.isEditor)
                return;
            var k = Keyboard.current;
            if (k == null)
                return;
            if (cameraRig == null && Camera.main != null)
                cameraRig = Camera.main.GetComponent<CameraRig>();

            if (k.f5Key.wasPressedThisFrame)
                runner.StartNewMatch();
            if (k.f7Key.wasPressedThisFrame && cameraRig != null)
            {
                cameraRig.ReduceMotion = !cameraRig.ReduceMotion;
                Debug.Log($"[Knockbox] 흔들림 줄이기 {(cameraRig.ReduceMotion ? "켬" : "끔")}");
            }
            if (k.f8Key.wasPressedThisFrame)
            {
                bool enable = runner.SyncTest == null;
                runner.SetSyncTest(enable);
                Debug.Log($"[Knockbox] SyncTest {(enable ? "켬" : "끔")}");
            }
            if (k.pKey.wasPressedThisFrame)
                runner.Running = !runner.Running;
            if (k.oKey.wasPressedThisFrame && !runner.Running)
                runner.StepOnce();
        }
    }
}

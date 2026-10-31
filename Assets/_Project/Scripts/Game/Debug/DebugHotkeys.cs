using Knockbox.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Knockbox.Game
{
    /// <summary>
    /// 디버그 단축키(06-match-and-tools.md 4절) 중 지금 있는 기능만: F3 판정 오버레이, F5 라운드 리셋(튜닝 재적용),
    /// F6 P2 입력원 순환(키보드 오른쪽 → 게임패드 → 허수아비), `[` `]` 허수아비 행동 바꾸기,
    /// F7 흔들림 줄이기, F8 SyncTest, P 일시정지, O 정지 중 한 틱 진행. 나머지는 해당 CP에서 붙인다.
    /// 에디터와 개발 빌드에서만 동작한다.
    /// </summary>
    [RequireComponent(typeof(MatchRunner))]
    public sealed class DebugHotkeys : MonoBehaviour
    {
        MatchRunner runner;
        MatchPresenter presenter;
        CameraRig cameraRig;
        readonly DummyInputSource dummy = new DummyInputSource();

        void Awake()
        {
            runner = GetComponent<MatchRunner>();
            presenter = GetComponent<MatchPresenter>();
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

            if (k.f3Key.wasPressedThisFrame && presenter != null)
                presenter.SetOverlay(!presenter.OverlayVisible);
            if (k.f5Key.wasPressedThisFrame)
            {
                // 새 경기는 P2를 기본 장치로 되돌리므로 고른 입력원을 다시 붙인다.
                var p2 = runner.Player2Input;
                runner.StartNewMatch();
                if (p2 is DummyInputSource)
                    runner.Player2Input = p2;
            }
            if (k.f6Key.wasPressedThisFrame)
                CycleP2Input();
            if (runner.Player2Input is DummyInputSource current)
            {
                int step = (k.rightBracketKey.wasPressedThisFrame ? 1 : 0) - (k.leftBracketKey.wasPressedThisFrame ? 1 : 0);
                if (step != 0)
                {
                    current.Behavior = DummyInputSource.Cycle(current.Behavior, step);
                    Debug.Log($"[Knockbox] 허수아비 행동: {current.Behavior}");
                }
            }
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

        void CycleP2Input()
        {
            var current = runner.Player2Input;
            IInputSource next;
            if (current is KeyboardInputSource)
                next = GamepadInputSource.Available(0) ? new GamepadInputSource(0) : (IInputSource)dummy;
            else if (current is GamepadInputSource)
                next = dummy;
            else
                next = new KeyboardInputSource(KeyboardLayout.Right);
            runner.Player2Input = next;
            Debug.Log($"[Knockbox] P2 입력원: {MatchPresenter.InputSourceName(next)}");
        }
    }
}

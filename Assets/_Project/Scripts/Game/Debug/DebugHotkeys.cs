using Knockbox.Presentation;
using Knockbox.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Knockbox.Game
{
    /// <summary>
    /// 디버그 단축키(06-match-and-tools.md 4절) 중 지금 있는 기능만: F1 전투 모드 전환(라운드 리셋), F3 판정 오버레이,
    /// F4 잡기 켜기/끄기(라운드 리셋), F5 라운드 리셋(튜닝 재적용),
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

            if (k.f1Key.wasPressedThisFrame)
            {
                var modes = (CombatMode[])System.Enum.GetValues(typeof(CombatMode));
                int next = (System.Array.IndexOf(modes, runner.Mode) + 1) % modes.Length;
                runner.Mode = modes[next];
                RestartKeepingDummy();
                Debug.Log($"[Knockbox] 전투 모드: {runner.Mode}");
            }
            if (k.f4Key.wasPressedThisFrame)
            {
                runner.GrabEnabled = !runner.GrabEnabled;
                RestartKeepingDummy();
                Debug.Log($"[Knockbox] 잡기 {(runner.GrabEnabled ? "켬" : "끔")}");
            }
            if (k.f3Key.wasPressedThisFrame && presenter != null)
                presenter.SetOverlay(!presenter.OverlayVisible);
            if (k.f5Key.wasPressedThisFrame)
                RestartKeepingDummy();
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

        /// <summary>새 경기는 P2를 기본 장치로 되돌리므로, 허수아비를 쓰고 있었다면 다시 붙인다.</summary>
        void RestartKeepingDummy()
        {
            var p2 = runner.Player2Input;
            runner.StartNewMatch();
            if (p2 is DummyInputSource)
                runner.Player2Input = p2;
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

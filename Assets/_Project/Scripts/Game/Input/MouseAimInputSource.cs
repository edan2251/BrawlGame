using System;
using Knockbox.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Knockbox.Game
{
    /// <summary>
    /// CR-004 실험 조작: 키보드 왼쪽 배치(WASD, Space 점프, Left Shift 대시) + 마우스. 좌클릭 = 공격(탭 주먹, 홀드 박치기),
    /// 우클릭 = 잡기(단계 C). 조준은 파이터 몸 중심에서 커서까지의 방향을 64방향으로 양자화해 입력에 담는다.
    /// </summary>
    public sealed class MouseAimInputSource : IInputSource
    {
        readonly KeyboardInputSource keyboard = new KeyboardInputSource(KeyboardLayout.Left);
        readonly Func<SimWorld> world;
        readonly int fighter;

        public MouseAimInputSource(Func<SimWorld> world, int fighter)
        {
            this.world = world;
            this.fighter = fighter;
        }

        public InputFrame Poll(int tick)
        {
            var frame = keyboard.Poll(tick);
            var mouse = Mouse.current;
            if (mouse == null)
                return frame;
            var buttons = frame.Buttons;
            if (mouse.leftButton.isPressed)
                buttons |= InputButtons.Attack;
            if (mouse.rightButton.isPressed)
                buttons |= InputButtons.Grab;
            byte aim = 0;
            var w = world();
            var cam = Camera.main;
            if (w != null && cam != null)
            {
                var p = w.State.GetFighter(fighter).Position;
                aim = AimInput.FromScreen(cam, mouse.position.ReadValue(), new Vector2(p.X, p.Y));
            }
            return new InputFrame(frame.MoveX, frame.MoveY, buttons, aim);
        }
    }

    /// <summary>Game 계층의 조준 양자화(시뮬레이션은 번호만 받는다).</summary>
    public static class AimInput
    {
        /// <summary>방향 → 조준 값(1~64). 길이가 거의 0이면 0(없음).</summary>
        public static byte Quantize(Vector2 delta)
        {
            if (delta.sqrMagnitude < 1e-6f)
                return 0;
            float step = 2f * Mathf.PI / AimDirections.Count;
            int index = Mathf.RoundToInt(Mathf.Atan2(delta.y, delta.x) / step);
            return AimDirections.Encode(index);
        }

        /// <summary>화면 좌표의 커서 → 월드 원점(origin)에서 본 조준 값.</summary>
        public static byte FromScreen(Camera cam, Vector2 screen, Vector2 origin)
        {
            var world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            return Quantize((Vector2)world - origin);
        }
    }
}

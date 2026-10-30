using System;

namespace Knockbox.Simulation
{
    [Flags]
    public enum InputButtons : byte
    {
        None = 0,
        Jump = 1 << 0,
        Attack = 1 << 1,
        Dash = 1 << 2,
        Guard = 1 << 3,
        Grab = 1 << 4,
        All = Jump | Attack | Dash | Guard | Grab,
    }

    /// <summary>
    /// 한 틱의 한 플레이어 입력. 방향은 Game 계층이 8방향으로 양자화해서 넘긴다.
    /// "이번 틱에 눌렀다/뗐다"는 시뮬레이션이 직전 버튼 값과 비교해서 계산한다.
    /// </summary>
    [Serializable]
    public struct InputFrame : IEquatable<InputFrame>
    {
        public sbyte MoveX;
        public sbyte MoveY;
        public InputButtons Buttons;

        public InputFrame(int moveX, int moveY, InputButtons buttons)
        {
            MoveX = (sbyte)SimMath.Clamp(moveX, -1, 1);
            MoveY = (sbyte)SimMath.Clamp(moveY, -1, 1);
            Buttons = buttons & InputButtons.All;
        }

        public bool Held(InputButtons button) => (Buttons & button) != 0;

        public static InputButtons Pressed(InputButtons current, InputButtons previous) => current & ~previous;
        public static InputButtons Released(InputButtons current, InputButtons previous) => previous & ~current;

        /// <summary>기록 파일용 3바이트 표현.</summary>
        public void Write(byte[] buffer, int offset)
        {
            buffer[offset] = unchecked((byte)MoveX);
            buffer[offset + 1] = unchecked((byte)MoveY);
            buffer[offset + 2] = (byte)Buttons;
        }

        public static InputFrame Read(byte[] buffer, int offset) =>
            new InputFrame(unchecked((sbyte)buffer[offset]), unchecked((sbyte)buffer[offset + 1]), (InputButtons)buffer[offset + 2]);

        public bool Equals(InputFrame other) => MoveX == other.MoveX && MoveY == other.MoveY && Buttons == other.Buttons;
        public override bool Equals(object obj) => obj is InputFrame other && Equals(other);
        public override int GetHashCode() => (MoveX + 1) | ((MoveY + 1) << 2) | ((int)Buttons << 4);
        public static bool operator ==(InputFrame a, InputFrame b) => a.Equals(b);
        public static bool operator !=(InputFrame a, InputFrame b) => !a.Equals(b);
        public override string ToString() => $"({MoveX},{MoveY}) {Buttons}";
    }
}

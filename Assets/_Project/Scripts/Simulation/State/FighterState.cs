using System;

namespace Knockbox.Simulation
{
    /// <summary>파이터의 행동 상태. 전투 상태는 CP4에서 늘어난다.</summary>
    public enum ActionState : byte
    {
        Idle,
        Run,
        Airborne,
        WallSlide,
        Dash,
    }

    /// <summary>
    /// 파이터 한 명의 상태. 배열이나 참조 필드 없이 값만 둔다.
    /// 필드를 추가하면 반드시 Visit에도 추가한다(체크섬 범위 테스트가 잡아낸다).
    /// </summary>
    [Serializable]
    public struct FighterState
    {
        public const int NoBox = -1;
        public const int OnFighter = -2;
        public const int TimerCap = 1000;

        public Vec2 Position;
        /// <summary>유닛/초.</summary>
        public Vec2 Velocity;
        /// <summary>-1 왼쪽, 1 오른쪽.</summary>
        public sbyte Facing;
        public ActionState Action;
        /// <summary>현재 행동에 들어온 뒤 지난 틱 수.</summary>
        public int ActionTick;
        /// <summary>직전 틱의 버튼. 눌림/뗌 계산용.</summary>
        public InputButtons PrevButtons;

        public bool Grounded;
        /// <summary>딛고 선 지형 상자 번호. NoBox, 또는 상대 머리 위면 OnFighter.</summary>
        public int GroundBox;
        /// <summary>지금 닿은 벽 쪽. -1 왼쪽 벽, 1 오른쪽 벽, 0 없음.</summary>
        public sbyte WallSide;
        /// <summary>마지막으로 닿았던 벽 쪽(벽 코요테용).</summary>
        public sbyte LastWallSide;
        public int TicksSinceGrounded;
        public int TicksSinceWall;

        /// <summary>남은 점프 버퍼 틱. 0이면 없음.</summary>
        public int JumpBuffer;
        /// <summary>점프로 올라가는 중이라 버튼을 떼면 조기 해제 중력을 받는다.</summary>
        public bool JumpCutEligible;
        public int AirJumpsLeft;

        public int DashCharges;
        public int DashCooldown;
        public int DashTicksLeft;
        public Vec2 DashDirection;
        public bool DashFromGround;

        public int WallJumpLockTicks;
        /// <summary>벽 점프 직후 무시하는 입력 방향(떠난 벽 쪽).</summary>
        public sbyte WallJumpLockDirection;

        public int DropThroughTicks;
        public int DropThroughBox;

        public void SetAction(ActionState action)
        {
            if (Action == action)
                return;
            Action = action;
            ActionTick = 0;
        }

        public readonly void Visit<T>(ref T v) where T : struct, IStateVisitor
        {
            v.Value("Position.X", Position.X);
            v.Value("Position.Y", Position.Y);
            v.Value("Velocity.X", Velocity.X);
            v.Value("Velocity.Y", Velocity.Y);
            v.Value(nameof(Facing), (int)Facing);
            v.Value(nameof(Action), (int)Action);
            v.Value(nameof(ActionTick), ActionTick);
            v.Value(nameof(PrevButtons), (int)PrevButtons);
            v.Value(nameof(Grounded), Grounded);
            v.Value(nameof(GroundBox), GroundBox);
            v.Value(nameof(WallSide), (int)WallSide);
            v.Value(nameof(LastWallSide), (int)LastWallSide);
            v.Value(nameof(TicksSinceGrounded), TicksSinceGrounded);
            v.Value(nameof(TicksSinceWall), TicksSinceWall);
            v.Value(nameof(JumpBuffer), JumpBuffer);
            v.Value(nameof(JumpCutEligible), JumpCutEligible);
            v.Value(nameof(AirJumpsLeft), AirJumpsLeft);
            v.Value(nameof(DashCharges), DashCharges);
            v.Value(nameof(DashCooldown), DashCooldown);
            v.Value(nameof(DashTicksLeft), DashTicksLeft);
            v.Value("DashDirection.X", DashDirection.X);
            v.Value("DashDirection.Y", DashDirection.Y);
            v.Value(nameof(DashFromGround), DashFromGround);
            v.Value(nameof(WallJumpLockTicks), WallJumpLockTicks);
            v.Value(nameof(WallJumpLockDirection), (int)WallJumpLockDirection);
            v.Value(nameof(DropThroughTicks), DropThroughTicks);
            v.Value(nameof(DropThroughBox), DropThroughBox);
        }
    }
}

using System;

namespace Knockbox.Simulation
{
    /// <summary>파이터의 행동 상태. 전투 상태는 CP4에서 늘어난다.</summary>
    public enum ActionState : byte
    {
        Idle,
        Run,
        Airborne,
    }

    /// <summary>
    /// 파이터 한 명의 상태. 배열이나 참조 필드 없이 값만 둔다.
    /// 필드를 추가하면 반드시 Visit에도 추가한다(체크섬 범위 테스트가 잡아낸다).
    /// </summary>
    [Serializable]
    public struct FighterState
    {
        public Vec2 Position;
        public Vec2 Velocity;
        /// <summary>-1 왼쪽, 1 오른쪽.</summary>
        public sbyte Facing;
        public ActionState Action;
        /// <summary>현재 행동에 들어온 뒤 지난 틱 수.</summary>
        public int ActionTick;
        public bool Grounded;
        /// <summary>직전 틱의 버튼. 눌림/뗌 계산용.</summary>
        public InputButtons PrevButtons;

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
            v.Value(nameof(Grounded), Grounded);
            v.Value(nameof(PrevButtons), (int)PrevButtons);
        }
    }
}

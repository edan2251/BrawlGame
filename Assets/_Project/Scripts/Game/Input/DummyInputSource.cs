using Knockbox.Simulation;

namespace Knockbox.Game
{
    /// <summary>허수아비 행동(06-match-and-tools.md 3절).</summary>
    public enum DummyBehavior
    {
        Idle,
        JumpRepeat,
        GuardHold,
        AttackEvery40,
        WalkBackAndForth,
        Random,
    }

    /// <summary>
    /// AI 대신 정해진 행동을 반복하는 테스트 상대. 틱 번호만 보고 입력을 정하므로 같은 틱에는 같은 입력이 나온다
    /// (무작위 행동은 고정 시드). 디버그 키 `[` `]`로 행동을 바꾼다.
    /// </summary>
    public sealed class DummyInputSource : IInputSource
    {
        public const int JumpPeriod = 45;
        public const int JumpHoldTicks = 12;
        public const int AttackPeriod = 40;
        public const int WalkPeriod = 60;
        public const uint RandomSeed = 0xD077u;

        RandomInputGenerator random = new RandomInputGenerator(RandomSeed);

        public DummyInputSource(DummyBehavior behavior = DummyBehavior.Idle)
        {
            Behavior = behavior;
        }

        public DummyBehavior Behavior { get; set; }

        public InputFrame Poll(int tick)
        {
            switch (Behavior)
            {
                case DummyBehavior.JumpRepeat:
                    return new InputFrame(0, 0, tick % JumpPeriod < JumpHoldTicks ? InputButtons.Jump : InputButtons.None);
                case DummyBehavior.GuardHold:
                    return new InputFrame(0, 0, InputButtons.Guard);
                case DummyBehavior.AttackEvery40:
                    return new InputFrame(0, 0, tick % AttackPeriod == 0 ? InputButtons.Attack : InputButtons.None);
                case DummyBehavior.WalkBackAndForth:
                    return new InputFrame(tick / WalkPeriod % 2 == 0 ? 1 : -1, 0, InputButtons.None);
                case DummyBehavior.Random:
                    return random.Next();
                default:
                    return default;
            }
        }

        public static DummyBehavior Cycle(DummyBehavior behavior, int step)
        {
            int count = (int)DummyBehavior.Random + 1;
            return (DummyBehavior)((((int)behavior + step) % count + count) % count);
        }
    }
}

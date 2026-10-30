using System;

namespace Knockbox.Simulation
{
    /// <summary>
    /// 시뮬레이션 전체 상태. 값 타입이라 `var snapshot = world.State;` 한 줄이 완전한 저장이다.
    /// 배열이나 참조 필드를 두지 않는다.
    /// </summary>
    [Serializable]
    public struct SimState
    {
        /// <summary>라운드 상태를 만든 뒤 진행한 Step 수.</summary>
        public int Tick;
        public SimRng Rng;
        public FighterState Fighter0;
        public FighterState Fighter1;
        public MatchState Match;

        public readonly uint ComputeChecksum()
        {
            var h = Hasher.Create();
            Visit(ref h);
            return h.Hash;
        }

        public readonly void Visit<T>(ref T v) where T : struct, IStateVisitor
        {
            v.Value(nameof(Tick), Tick);
            v.Value("Rng.State", Rng.State);
            v.Begin(nameof(Fighter0));
            Fighter0.Visit(ref v);
            v.End();
            v.Begin(nameof(Fighter1));
            Fighter1.Visit(ref v);
            v.End();
            v.Begin(nameof(Match));
            Match.Visit(ref v);
            v.End();
        }

        public readonly FighterState GetFighter(int index) => index == 0 ? Fighter0 : Fighter1;

        /// <summary>
        /// 새 라운드 상태. 두 파이터는 서로를 바라본다.
        /// startPhase가 Fighting이면 카운트다운을 건너뛴다(시나리오와 테스트용).
        /// </summary>
        public static SimState CreateRound(SimConfig config, uint seed, Vec2 spawn0, Vec2 spawn1, MatchPhase startPhase)
        {
            var state = new SimState
            {
                Tick = 0,
                Rng = new SimRng(seed),
                Fighter0 = CreateFighter(config, spawn0, spawn0.X <= spawn1.X ? 1 : -1),
                Fighter1 = CreateFighter(config, spawn1, spawn1.X < spawn0.X ? 1 : -1),
            };
            state.Match.Round = 1;
            state.Match.SetPhase(startPhase);
            return state;
        }

        static FighterState CreateFighter(SimConfig config, Vec2 spawn, int facing)
        {
            var f = new FighterState
            {
                Position = spawn,
                Facing = (sbyte)facing,
            };
            f.Grounded = TempArena.IsOnFloor(config, spawn);
            f.Action = f.Grounded ? ActionState.Idle : ActionState.Airborne;
            return f;
        }
    }
}

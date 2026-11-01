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
        /// <summary>구조물 강체(CP14). 라운드가 시작될 때 맵의 처음 모양으로 놓인다.</summary>
        public PropState Props;
        /// <summary>투사체 풀(CP19). 라운드가 시작될 때 비운다.</summary>
        public ProjectileState Projectiles;

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
            v.Begin(nameof(Props));
            Props.Visit(ref v);
            v.End();
            v.Begin(nameof(Projectiles));
            Projectiles.Visit(ref v);
            v.End();
        }

        public readonly FighterState GetFighter(int index) => index == 0 ? Fighter0 : Fighter1;

        /// <summary>맵의 시작 칸 위에 선 파이터의 중심 위치.</summary>
        public static Vec2 SpawnCenter(SimConfig config, ArenaDef arena, int fighter) =>
            arena.SpawnFoot(fighter) + new Vec2(0f, config.HalfBody);

        /// <summary>
        /// 맵의 시작 위치로 새 라운드 상태를 만든다. 두 파이터는 서로를 바라본다.
        /// startPhase가 Fighting이면 카운트다운을 건너뛴다(시나리오와 테스트용).
        /// </summary>
        public static SimState CreateRound(SimConfig config, ArenaDef arena, uint seed, MatchPhase startPhase) =>
            CreateRound(config, arena, seed, SpawnCenter(config, arena, 0), SpawnCenter(config, arena, 1), startPhase);

        /// <summary>
        /// 맵 순환 경기(04-arena.md 6절): 시드로 맵 순서를 섞고 첫 맵의 시작 위치로 1라운드를 만든다.
        /// 맵 배열은 SimWorld에 같은 순서로 넘긴다.
        /// </summary>
        public static SimState CreateMatch(SimConfig config, ArenaDef[] arenas, uint seed, MatchPhase startPhase)
        {
            var rotation = ArenaRotation.Create(seed, arenas.Length);
            var state = CreateRound(config, arenas[rotation.Current], seed, startPhase);
            // 맵 하나면 순환 상태를 비워 둔다(CreateRound와 같은 상태).
            if (arenas.Length > 1)
                state.Match.Rotation = rotation;
            return state;
        }

        /// <summary>시작 위치(파이터 중심)를 직접 정한다.</summary>
        public static SimState CreateRound(SimConfig config, ArenaDef arena, uint seed, Vec2 center0, Vec2 center1, MatchPhase startPhase)
        {
            var state = new SimState
            {
                Tick = 0,
                Rng = new SimRng(seed),
                Fighter0 = CreateFighter(config, arena, center0, center0.X <= center1.X ? 1 : -1),
                Fighter1 = CreateFighter(config, arena, center1, center1.X < center0.X ? 1 : -1),
            };
            state.Match.Round = 1;
            state.Match.RoundWinner = -1;
            state.Match.SetPhase(startPhase);
            PropPhysics.Init(ref state.Props, arena, config);
            return state;
        }

        /// <summary>
        /// 다음 라운드: 파이터를 맵의 시작 위치로 되돌리고 체력과 상태를 초기화한다.
        /// 점수, 틱, 난수 상태는 그대로 둔다. Countdown에서 시작한다.
        /// </summary>
        public static void StartNextRound(ref SimState state, SimConfig config, ArenaDef arena)
        {
            var center0 = SpawnCenter(config, arena, 0);
            var center1 = SpawnCenter(config, arena, 1);
            state.Fighter0 = CreateFighter(config, arena, center0, center0.X <= center1.X ? 1 : -1);
            state.Fighter1 = CreateFighter(config, arena, center1, center1.X < center0.X ? 1 : -1);
            state.Match.Round++;
            state.Match.RoundWinner = -1;
            state.Match.SetPhase(MatchPhase.Countdown);
            state.Projectiles = default;
            PropPhysics.Init(ref state.Props, arena, config);
        }

        /// <summary>MatchEnd 뒤 새 경기: 점수를 지우고 1라운드 Countdown부터.</summary>
        public static void StartNewMatch(ref SimState state, SimConfig config, ArenaDef arena)
        {
            state.Match.Score0 = 0;
            state.Match.Score1 = 0;
            state.Match.Round = 0;
            StartNextRound(ref state, config, arena);
        }

        static FighterState CreateFighter(SimConfig config, ArenaDef arena, Vec2 center, int facing)
        {
            ref readonly var m = ref config.Movement;
            var f = new FighterState
            {
                Position = center,
                Facing = (sbyte)facing,
                AirJumpsLeft = m.AirJumps,
                DashCharges = m.DashCharges,
                TicksSinceWall = FighterState.TimerCap,
                DropThroughBox = FighterState.NoBox,
                Health = config.Combat.MaxHealth,
                AttackId = FighterState.NoAttack,
                TicksSinceGuardPress = FighterState.TimerCap,
                Form = config.StartForm,
                OwnedForms = Forms.Bit(config.StartForm),
                Energy = config.Combat.Energy.Max,
            };
            f.GroundBox = TerrainCollision.ProbeGround(center, config.HalfBody, arena, m.GroundProbeDistance, FighterState.NoBox);
            f.Grounded = f.GroundBox != FighterState.NoBox;
            f.TicksSinceGrounded = f.Grounded ? 0 : FighterState.TimerCap;
            f.Action = f.Grounded ? ActionState.Idle : ActionState.Airborne;
            return f;
        }
    }
}

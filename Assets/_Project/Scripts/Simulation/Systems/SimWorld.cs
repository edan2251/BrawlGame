using System;

namespace Knockbox.Simulation
{
    /// <summary>롤백·SyncTest 도구가 다루는 최소 단위. SimWorld와 테스트용 가짜 구현이 따른다.</summary>
    public interface ISimStepper
    {
        SimState State { get; set; }
        void Step(in InputFrame p1, in InputFrame p2);
        uint ComputeChecksum();
    }

    /// <summary>
    /// 고정 60틱 시뮬레이션. `입력 두 개 → 다음 상태`인 순수 함수처럼 동작한다.
    /// 상태는 State 한 필드에 모두 있고, Events는 상태가 아니다(매 Step 시작 시 비워짐).
    /// </summary>
    public sealed class SimWorld : ISimStepper
    {
        public SimState State;
        readonly ArenaDef[] arenas;

        public SimWorld(SimConfig config, ArenaDef arena, in SimState initial, int eventCapacity = SimEventBuffer.DefaultCapacity)
            : this(config, new[] { arena }, initial, eventCapacity)
        {
        }

        /// <param name="arenas">맵 순환 목록(CP12). 지금 맵은 State.Match.Rotation이 고른다.</param>
        public SimWorld(SimConfig config, ArenaDef[] arenas, in SimState initial, int eventCapacity = SimEventBuffer.DefaultCapacity)
        {
            if (arenas == null || arenas.Length == 0 || Array.IndexOf(arenas, null) >= 0)
                throw new ArgumentException("맵이 하나 이상 있어야 함", nameof(arenas));
            int count = initial.Match.Rotation.Count;
            if (count > 1 && count != arenas.Length)
                throw new ArgumentException($"맵 순환 상태({count}개)와 맵 목록({arenas.Length}개)이 다름", nameof(arenas));
            Config = config;
            this.arenas = arenas;
            State = initial;
            Events = new SimEventBuffer(eventCapacity);
            PropScratch = new PropScratch();
        }

        /// <summary>지금 라운드의 맵. 라운드가 바뀌면(RoundTransition → Countdown) 다른 맵일 수 있다.</summary>
        public ArenaDef Arena => arenas[State.Match.Rotation.Current];

        public int ArenaCount => arenas.Length;

        public ArenaDef GetArena(int index) => arenas[index];

        /// <summary>라운드 동안 바뀌지 않는다. RoundTransition 단계에서만 ReplaceConfig로 바꿀 수 있다.</summary>
        public SimConfig Config { get; private set; }

        public SimEventBuffer Events { get; }

        /// <summary>구조물 물리의 작업 공간(상태 아님).</summary>
        public PropScratch PropScratch { get; }

        SimState ISimStepper.State
        {
            get => State;
            set => State = value;
        }

        /// <summary>
        /// RoundTransition 훅(06-match-and-tools.md 1절): 다음 라운드의 설정을 바꾼다.
        /// RoundTransition 이벤트를 받은 직후(다음 Step 전)에만 부를 수 있다.
        /// </summary>
        public void ReplaceConfig(SimConfig config)
        {
            if (State.Match.Phase != MatchPhase.RoundTransition)
                throw new InvalidOperationException($"설정은 RoundTransition 단계에서만 바꿀 수 있음(지금 {State.Match.Phase})");
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public void Step(in InputFrame p1, in InputFrame p2)
        {
            Events.Clear();
            ref var s = ref State;
            s.Tick++;
            var phase = s.Match.Phase;
            s.Match.PhaseTick++;

            // 구조물은 카운트다운·싸움·KO 동안 움직인다(히트스탑과 상관없이, 09 4절).
            if (phase == MatchPhase.Countdown || phase == MatchPhase.Fighting || phase == MatchPhase.KO)
                PropPhysics.Step(ref s.Props, Arena, Config, PropScratch, s.Tick, Events);

            switch (phase)
            {
                case MatchPhase.Countdown:
                    StepCountdown(ref s);
                    break;
                case MatchPhase.Fighting:
                    StepFighting(ref s, p1, p2);
                    break;
                case MatchPhase.KO:
                    // 두 파이터 모두 정지. 연출이 KO 순간을 보여준다.
                    if (s.Match.PhaseTick >= Config.Match.KoTicks)
                        EndRound(ref s);
                    break;
                case MatchPhase.RoundEnd:
                    AfterRoundEnd(ref s);
                    break;
                case MatchPhase.RoundTransition:
                    s.Match.Rotation.Advance();
                    SimState.StartNextRound(ref s, Config, Arena);
                    break;
                case MatchPhase.MatchEnd:
                    var pressed = InputFrame.Pressed(p1.Buttons, s.Fighter0.PrevButtons) | InputFrame.Pressed(p2.Buttons, s.Fighter1.PrevButtons);
                    if (s.Match.PhaseTick >= Config.Match.MatchEndMinTicks && pressed != InputButtons.None)
                    {
                        s.Match.Rotation.Advance();
                        SimState.StartNewMatch(ref s, Config, Arena);
                    }
                    break;
            }

            s.Fighter0.PrevButtons = p1.Buttons;
            s.Fighter1.PrevButtons = p2.Buttons;
        }

        /// <summary>
        /// 한 틱(03-combat.md 3절 판정 순서): 히트스탑 중인 파이터는 멈춘다 → 행동 전환 → 이동과 지형 충돌(각자) →
        /// 몸 겹침 → 접촉 판정 → 상호작용 판정(동시) → KO 확인.
        /// 어느 단계에서도 번호로 우선순위를 정하지 않는다(01-architecture.md 4절 7항).
        /// </summary>
        void StepFighting(ref SimState s, in InputFrame p1, in InputFrame p2)
        {
            bool frozen0 = s.Fighter0.HitstopTicks > 0;
            bool frozen1 = s.Fighter1.HitstopTicks > 0;
            var prev0 = s.Fighter0;
            var prev1 = s.Fighter1;
            MovementScratch scratch0 = default, scratch1 = default;
            if (frozen0)
                FighterCombat.Freeze(ref s.Fighter0, p1, Config);
            else
                StepFighter(ref s.Fighter0, p1, 0, s.Tick, out scratch0);
            if (frozen1)
                FighterCombat.Freeze(ref s.Fighter1, p2, Config);
            else
                StepFighter(ref s.Fighter1, p2, 1, s.Tick, out scratch1);

            // 구조물과의 상호작용(09 4절): 이동 직후, 캐릭터끼리 밀어내기 전.
            PropInteraction.Resolve(ref s, Arena, Config, prev0, prev1, frozen0, frozen1, Events, PropScratch);

            // 몸 충돌 판정은 이동 직후(밀어내기 전) 스냅숏의 접촉과 법선으로 한다.
            var contact = BodyContact.Measure(s.Fighter0, s.Fighter1, Config);
            // 붙잡기 동안은 둘이 붙어 있으므로 밀어내지 않는다.
            bool attached = s.Fighter0.Action == ActionState.Holding || s.Fighter0.Action == ActionState.Held
                            || s.Fighter1.Action == ActionState.Holding || s.Fighter1.Action == ActionState.Held;
            if (!attached)
                FighterPush.Resolve(ref s.Fighter0, ref s.Fighter1, prev0, prev1, frozen0, frozen1, Config, Arena);
            if (!frozen0)
                FighterMovement.ResolveContacts(ref s.Fighter0, s.Fighter1, scratch0, Config, Arena, 0, s.Tick, Events);
            if (!frozen1)
                FighterMovement.ResolveContacts(ref s.Fighter1, s.Fighter0, scratch1, Config, Arena, 1, s.Tick, Events);

            FighterCombat.ResolveInteractions(ref s.Fighter0, ref s.Fighter1, frozen0, frozen1, contact, Config, Arena, s.Tick, Events);
            // 투사체(CP19): 이번 틱의 발사를 넣고, 이동이 끝난 파이터와 지형에 대해 한 틱 움직인다.
            Projectiles.SpawnPending(ref s, Config, s.Tick, Events);
            Projectiles.Step(ref s, Arena, Config, prev0, prev1, s.Tick, Events);
            CheckKnockout(ref s);
        }

        void StepFighter(ref FighterState f, in InputFrame input, int index, int tick, out MovementScratch scratch)
        {
            // 행동이 바뀌면 시스템이 0으로 되돌린다. 그래서 바뀐 틱에는 0, 다음 틱에 1이 된다.
            f.ActionTick++;
            var pressed = FighterCombat.BeginTick(ref f, input, Config, index, tick, Events);
            FighterMovement.Integrate(ref f, input, pressed, Config, Arena, index, tick, Events, out scratch);
        }

        /// <summary>체력이 0 이하면 KO. 같은 틱에 둘 다면 무승부 라운드(점수 없음).</summary>
        void CheckKnockout(ref SimState s)
        {
            bool down0 = s.Fighter0.Health <= 0;
            bool down1 = s.Fighter1.Health <= 0;
            if (!down0 && !down1)
                return;
            int koHitstop = Config.Combat.KoHitstop;
            s.Fighter0.HitstopTicks = koHitstop;
            s.Fighter1.HitstopTicks = koHitstop;
            if (down0)
            {
                s.Fighter0.RestartAction(ActionState.KO);
                Events.Add(new SimEvent(s.Tick, SimEventType.KO, 0, s.Fighter0.Position, s.Fighter0.Velocity, koHitstop));
            }
            if (down1)
            {
                s.Fighter1.RestartAction(ActionState.KO);
                Events.Add(new SimEvent(s.Tick, SimEventType.KO, 1, s.Fighter1.Position, s.Fighter1.Velocity, koHitstop));
            }
            s.Match.RoundWinner = down0 && down1 ? -1 : down0 ? 1 : 0;
            s.Match.SetPhase(MatchPhase.KO);
        }

        void EndRound(ref SimState s)
        {
            if (s.Match.RoundWinner == 0)
                s.Match.Score0++;
            else if (s.Match.RoundWinner == 1)
                s.Match.Score1++;
            s.Match.SetPhase(MatchPhase.RoundEnd);
            Events.Add(new SimEvent(s.Tick, SimEventType.RoundEnd, s.Match.RoundWinner, Vec2.Zero, Vec2.Zero, s.Match.Round));
        }

        void AfterRoundEnd(ref SimState s)
        {
            int target = Config.Match.RoundsToWin;
            if (s.Match.Score0 >= target || s.Match.Score1 >= target)
            {
                int winner = s.Match.Score0 >= target ? 0 : 1;
                s.Match.SetPhase(MatchPhase.MatchEnd);
                Events.Add(new SimEvent(s.Tick, SimEventType.MatchEnd, winner, Vec2.Zero, Vec2.Zero, s.Match.Round));
                return;
            }
            s.Match.SetPhase(MatchPhase.RoundTransition);
            Events.Add(new SimEvent(s.Tick, SimEventType.RoundTransition, SimEvent.NoFighter, Vec2.Zero, Vec2.Zero, s.Match.Round));
        }

        void StepCountdown(ref SimState s)
        {
            // 카운트다운 동안 파이터는 시작 위치에 고정된다. 버튼 기록은 계속 갱신해서
            // GO 순간에 누르고 있던 버튼이 "새로 눌림"으로 잡히지 않게 한다.
            s.Fighter0.ActionTick++;
            s.Fighter1.ActionTick++;
            if (s.Match.PhaseTick >= Config.Match.CountdownTicks)
            {
                s.Match.SetPhase(MatchPhase.Fighting);
                Events.Add(new SimEvent(s.Tick, SimEventType.RoundStart, SimEvent.NoFighter, Vec2.Zero, Vec2.Zero, s.Match.Round));
            }
        }

        public uint ComputeChecksum() => State.ComputeChecksum();
    }
}

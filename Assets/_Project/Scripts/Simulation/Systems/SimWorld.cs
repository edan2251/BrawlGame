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
        public readonly SimConfig Config;
        public readonly ArenaDef Arena;

        public SimWorld(SimConfig config, ArenaDef arena, in SimState initial, int eventCapacity = SimEventBuffer.DefaultCapacity)
        {
            Config = config;
            Arena = arena;
            State = initial;
            Events = new SimEventBuffer(eventCapacity);
        }

        public SimEventBuffer Events { get; }

        SimState ISimStepper.State
        {
            get => State;
            set => State = value;
        }

        public void Step(in InputFrame p1, in InputFrame p2)
        {
            Events.Clear();
            ref var s = ref State;
            s.Tick++;
            // 행동이 바뀌면 시스템이 0으로 되돌린다. 그래서 바뀐 틱에는 0, 다음 틱에 1이 된다.
            s.Fighter0.ActionTick++;
            s.Fighter1.ActionTick++;

            switch (s.Match.Phase)
            {
                case MatchPhase.Countdown:
                    StepCountdown(ref s);
                    break;
                case MatchPhase.Fighting:
                    StepFighting(ref s, p1, p2);
                    s.Match.PhaseTick++;
                    break;
                default:
                    s.Match.PhaseTick++;
                    break;
            }

            s.Fighter0.PrevButtons = p1.Buttons;
            s.Fighter1.PrevButtons = p2.Buttons;
        }

        /// <summary>
        /// 두 파이터를 같은 이전 상태에서 따로 움직인 뒤, 몸 겹침을 동시에 풀고, 접촉을 판정한다.
        /// 어느 단계에서도 번호로 우선순위를 정하지 않는다(01-architecture.md 4절 7항).
        /// </summary>
        void StepFighting(ref SimState s, in InputFrame p1, in InputFrame p2)
        {
            var prev0 = s.Fighter0;
            var prev1 = s.Fighter1;
            FighterMovement.Integrate(ref s.Fighter0, p1, Config, Arena, 0, s.Tick, Events, out var scratch0);
            FighterMovement.Integrate(ref s.Fighter1, p2, Config, Arena, 1, s.Tick, Events, out var scratch1);
            FighterPush.Resolve(ref s.Fighter0, ref s.Fighter1, prev0, prev1, Config, Arena);
            FighterMovement.ResolveContacts(ref s.Fighter0, s.Fighter1, scratch0, Config, Arena, 0, s.Tick, Events);
            FighterMovement.ResolveContacts(ref s.Fighter1, s.Fighter0, scratch1, Config, Arena, 1, s.Tick, Events);
        }

        void StepCountdown(ref SimState s)
        {
            // 카운트다운 동안 파이터는 시작 위치에 고정된다. 버튼 기록은 계속 갱신해서
            // GO 순간에 누르고 있던 버튼이 "새로 눌림"으로 잡히지 않게 한다.
            s.Match.PhaseTick++;
            if (s.Match.PhaseTick >= Config.Match.CountdownTicks)
            {
                s.Match.SetPhase(MatchPhase.Fighting);
                Events.Add(new SimEvent(s.Tick, SimEventType.RoundStart, SimEvent.NoFighter, Vec2.Zero, Vec2.Zero, s.Match.Round));
            }
        }

        public uint ComputeChecksum() => State.ComputeChecksum();
    }
}

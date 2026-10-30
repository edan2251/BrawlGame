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

        public SimWorld(SimConfig config, in SimState initial, int eventCapacity = SimEventBuffer.DefaultCapacity)
        {
            Config = config;
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
                    // 두 파이터는 서로 영향을 주지 않으므로 순서와 무관하다.
                    // 상호작용이 생기면(CP2 밀어내기, CP4 판정) 이전 상태에서 동시에 계산하고 동시에 적용한다.
                    TempMovement.Step(ref s.Fighter0, p1, Config, 0, s.Tick, Events);
                    TempMovement.Step(ref s.Fighter1, p2, Config, 1, s.Tick, Events);
                    s.Match.PhaseTick++;
                    break;
                default:
                    s.Match.PhaseTick++;
                    break;
            }

            s.Fighter0.PrevButtons = p1.Buttons;
            s.Fighter1.PrevButtons = p2.Buttons;
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

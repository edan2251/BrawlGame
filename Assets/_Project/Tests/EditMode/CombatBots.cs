using Knockbox.Simulation;

namespace Knockbox.Tests.EditMode
{
    /// <summary>
    /// 테스트용 싸움꾼: 상대 상태를 보고 다가가 공격하고, 가드·물러나기·점프 공격·대시를 섞는다.
    /// 무작위 입력보다 공격·가드·패링·클래시·벽꽝이 훨씬 자주 나와서 전투 결정론과 대칭성을 시험하기 좋다.
    /// 행동 선택은 고정 시드 SimRng라 같은 상태 흐름이면 같은 입력을 낸다.
    /// </summary>
    public struct CombatBot
    {
        SimRng rng;
        int mode;
        int ticksLeft;

        public CombatBot(uint seed)
        {
            rng = new SimRng(seed);
            mode = 0;
            ticksLeft = 0;
        }

        public InputFrame Next(in FighterState self, in FighterState other, int tick)
        {
            if (ticksLeft-- <= 0)
            {
                mode = rng.Range(0, 6);
                ticksLeft = rng.Range(6, 32);
            }
            float dx = other.Position.X - self.Position.X;
            int toward = dx > 0f ? 1 : dx < 0f ? -1 : self.Facing;
            float distance = dx < 0f ? -dx : dx;
            bool close = distance < 2.2f && (other.Position.Y - self.Position.Y) * (other.Position.Y - self.Position.Y) < 2.5f;
            var buttons = InputButtons.None;
            int moveX = toward;
            int moveY = 0;
            switch (mode)
            {
                case 0: // 다가가서 연타
                    if (close && tick % 5 == 0)
                        buttons |= InputButtons.Attack;
                    break;
                case 1: // 가드를 누르고 버틴다(가끔 다시 눌러 패링 구간을 연다)
                    moveX = 0;
                    if (tick % 26 != 0)
                        buttons |= InputButtons.Guard;
                    break;
                case 2: // 물러나기
                    moveX = -toward;
                    break;
                case 3: // 점프해서 공격(점프를 길게 눌러 Box 가운데 기둥도 넘는다)
                    if (tick % 20 < 14)
                        buttons |= InputButtons.Jump;
                    if (close && tick % 4 == 0)
                        buttons |= InputButtons.Attack;
                    break;
                case 4: // 대시로 붙어서 공격
                    if (!close && tick % 19 == 0)
                        buttons |= InputButtons.Dash;
                    if (close && tick % 3 == 0)
                        buttons |= InputButtons.Attack;
                    break;
                default: // 상대 공격 시작을 보고 가드
                    moveX = close ? 0 : toward;
                    if (close && other.Action == ActionState.Attack)
                        buttons |= InputButtons.Guard;
                    else if (close && tick % 7 == 0)
                        buttons |= InputButtons.Attack;
                    break;
            }
            return new InputFrame(moveX, moveY, buttons);
        }
    }

    /// <summary>한 경기 동안의 전투 이벤트 수.</summary>
    public struct CombatEventCounts
    {
        public int Hit, GuardHit, Parry, Clash, WallSplat, Bounce, KO, RoundTransition, MatchEnd;

        public void Add(SimEventBuffer events)
        {
            for (int i = 0; i < events.Count; i++)
            {
                switch (events[i].Type)
                {
                    case SimEventType.Hit: Hit++; break;
                    case SimEventType.GuardHit: GuardHit++; break;
                    case SimEventType.Parry: Parry++; break;
                    case SimEventType.Clash: Clash++; break;
                    case SimEventType.WallSplat: WallSplat++; break;
                    case SimEventType.BounceRebound: Bounce++; break;
                    case SimEventType.KO: KO++; break;
                    case SimEventType.RoundTransition: RoundTransition++; break;
                    case SimEventType.MatchEnd: MatchEnd++; break;
                }
            }
        }

        public override string ToString() =>
            $"명중 {Hit}, 가드 {GuardHit}, 패링 {Parry}, 클래시 {Clash}, 벽꽝 {WallSplat}, 튕김 {Bounce}, KO {KO}, 라운드 전환 {RoundTransition}, 경기 종료 {MatchEnd}";
    }
}

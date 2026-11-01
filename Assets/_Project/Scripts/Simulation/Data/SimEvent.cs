namespace Knockbox.Simulation
{
    public enum SimEventType : byte
    {
        Jump,
        Land,
        WallSlideStart,
        WallJump,
        Dash,
        Hit,
        Clash,
        Parry,
        GuardHit,
        Grab,
        Throw,
        WallSplat,
        BounceRebound,
        KO,
        RoundStart,
        RoundEnd,
        RoundTransition,
        AirJump,
        PadBounce,
        /// <summary>경기장 밖으로 나가 되돌려짐. 벽으로 닫힌 경기장에서는 일어나면 안 되는 버그 신호.</summary>
        OutOfBounds,
        /// <summary>공격 시작(연출이 팔을 내지르는 시점). Strength는 공격 번호.</summary>
        AttackStart,
        /// <summary>패링 구간이 열림.</summary>
        ParryWindow,
        MatchEnd,
        /// <summary>모드 A 돌진 발사. Direction은 발사 방향, Strength는 속도.</summary>
        LungeStart,
        /// <summary>몸 충돌 판정에서 계산한 위력(F3 오버레이용). Fighter의 위력이 Strength.</summary>
        PowerCheck,
        /// <summary>잡기 시작(손을 뻗는 시점).</summary>
        GrabStart,
        /// <summary>잡기 풀기: 서로 잡아 반대로 튕김.</summary>
        GrabBreak,
        /// <summary>가시에 닿음. Strength는 데미지.</summary>
        Spike,
        /// <summary>경계 튕김(CR-002). Position은 닿은 점, Direction은 경계 안쪽 법선, Strength는 데미지(면역 중 0).</summary>
        BoundaryHit,
        /// <summary>구조물에 맞음(CP14). Fighter는 맞은 쪽, Direction은 넉백 방향, Strength는 데미지.</summary>
        PropHit,
        /// <summary>돌진이 구조물을 침(CP14). Fighter는 친 쪽, Direction은 구조물 쪽, Strength는 위력.</summary>
        PropStrike,
        /// <summary>구조물의 줄·축·경첩이 끊어짐(CP15). Fighter는 마지막 접촉자(없으면 NoFighter), Position은 연결점, Strength는 받은 충격.</summary>
        JointBreak,
        /// <summary>상자에 맞음(CP16). PropHit과 같은 뜻이지만 상자 피해 비중을 따로 재기 위해 나눈다. Strength는 데미지(상한 적용 뒤).</summary>
        BoxHit,
        /// <summary>돌진이 상자를 기둥에서 쳐냄(CP16 부분 붕괴). Fighter는 친 쪽, Position은 상자 중심, Direction은 친 방향, Strength는 위력.</summary>
        BoxKnockout,
        /// <summary>구조물이 지형·다른 구조물에 세게 부딪힘(CP16 연출용). Fighter는 NoFighter, Position은 접촉점, Direction은 법선, Strength는 다가온 속도. 몸마다 틱에 한 번.</summary>
        PropImpact,
        /// <summary>발사 틱에 에너지가 비용보다 적어 행동을 취소함(CR-003 4.4 규칙 4). Fighter는 취소한 쪽, Strength는 그때 에너지.</summary>
        EnergyShort,
    }

    /// <summary>
    /// 연출이 소비하는 사건. 상태에는 포함하지 않는다.
    /// 롤백 대비로 (Tick, Type, Fighter)로 식별할 수 있다.
    /// 전투 이벤트의 Fighter는 맞은 쪽(Hit, GuardHit, WallSplat, BounceRebound, KO) 또는 패링한 쪽(Parry)이고,
    /// 클래시는 NoFighter다. Position은 접촉점, Direction은 넉백 방향 또는 지형 법선, Strength는 데미지나 속도.
    /// RoundEnd·MatchEnd의 Fighter는 이긴 쪽(무승부면 NoFighter).
    /// </summary>
    public struct SimEvent
    {
        public const sbyte NoFighter = -1;

        public int Tick;
        public SimEventType Type;
        public sbyte Fighter;
        public Vec2 Position;
        public Vec2 Direction;
        public float Strength;

        public SimEvent(int tick, SimEventType type, int fighter, Vec2 position, Vec2 direction, float strength)
        {
            Tick = tick;
            Type = type;
            Fighter = (sbyte)fighter;
            Position = position;
            Direction = direction;
            Strength = strength;
        }

        public override string ToString() => $"[{Tick}] {Type} P{Fighter + 1} {Position}";
    }

    /// <summary>고정 용량 이벤트 버퍼. 매 Step 시작 시 비워진다. 넘치면 버리고 개수를 센다.</summary>
    public sealed class SimEventBuffer
    {
        public const int DefaultCapacity = 64;

        readonly SimEvent[] items;

        public SimEventBuffer(int capacity = DefaultCapacity)
        {
            items = new SimEvent[capacity < 32 ? 32 : capacity];
        }

        public int Count { get; private set; }
        public int Capacity => items.Length;

        /// <summary>마지막 Clear 이후 용량 초과로 버린 개수.</summary>
        public int Dropped { get; private set; }

        public ref readonly SimEvent this[int index] => ref items[index];

        public void Clear()
        {
            Count = 0;
            Dropped = 0;
        }

        public void Add(in SimEvent e)
        {
            if (Count < items.Length)
                items[Count++] = e;
            else
                Dropped++;
        }
    }
}

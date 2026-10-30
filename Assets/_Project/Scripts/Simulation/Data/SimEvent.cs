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
    }

    /// <summary>
    /// 연출이 소비하는 사건. 상태에는 포함하지 않는다.
    /// 롤백 대비로 (Tick, Type, Fighter)로 식별할 수 있다.
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

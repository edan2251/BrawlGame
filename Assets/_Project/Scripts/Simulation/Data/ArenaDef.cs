namespace Knockbox.Simulation
{
    public enum TerrainKind : byte
    {
        Solid,
        OneWay,
        Bounce,
        Spike,
    }

    /// <summary>축 정렬 지형 상자. 좌표는 경기장 중심이 원점, 위가 +y.</summary>
    public struct ArenaBox
    {
        public TerrainKind Kind;
        public float MinX;
        public float MinY;
        public float MaxX;
        public float MaxY;

        public ArenaBox(TerrainKind kind, float minX, float minY, float maxX, float maxY)
        {
            Kind = kind;
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        /// <summary>단방향 발판을 제외한 막는 지형(옆과 아래에서도 막는다).</summary>
        public bool BlocksAllSides => Kind != TerrainKind.OneWay;

        public override string ToString() => $"{Kind} [{MinX}, {MinY}] - [{MaxX}, {MaxY}]";
    }

    /// <summary>
    /// 경기장(04-arena.md). 항상 벽으로 닫혀 있다. 크기는 맵마다: v1(테스트 고정 자료) 32 × 18, v2(경기 순환) 36 × 20.
    /// 시뮬레이션과 연출이 같은 상자 목록을 쓴다. 상태가 아니라 설정처럼 라운드 동안 바뀌지 않는다.
    /// </summary>
    public sealed class ArenaDef
    {
        public const int ClassicColumns = 32;
        public const int ClassicRows = 18;

        readonly ArenaBox[] boxes;

        /// <param name="spawnFoot0">P1 시작 칸의 가로 중심과 칸 바닥 높이. 파이터는 이 점 위에 선다.</param>
        public ArenaDef(string name, ArenaBox[] boxes, Vec2 spawnFoot0, Vec2 spawnFoot1)
            : this(name, ClassicColumns, ClassicRows, boxes, spawnFoot0, spawnFoot1)
        {
        }

        public ArenaDef(string name, int columns, int rows, ArenaBox[] boxes, Vec2 spawnFoot0, Vec2 spawnFoot1)
        {
            Name = name;
            Columns = columns;
            Rows = rows;
            this.boxes = boxes;
            SpawnFoot0 = spawnFoot0;
            SpawnFoot1 = spawnFoot1;
        }

        public string Name { get; }
        public int Columns { get; }
        public int Rows { get; }
        public float HalfWidth => Columns * 0.5f;
        public float HalfHeight => Rows * 0.5f;
        public Vec2 SpawnFoot0 { get; }
        public Vec2 SpawnFoot1 { get; }
        public int BoxCount => boxes.Length;

        public ref readonly ArenaBox GetBox(int index) => ref boxes[index];

        public Vec2 SpawnFoot(int fighter) => fighter == 0 ? SpawnFoot0 : SpawnFoot1;

        public int CountBoxes(TerrainKind kind)
        {
            int n = 0;
            for (int i = 0; i < boxes.Length; i++)
            {
                if (boxes[i].Kind == kind)
                    n++;
            }
            return n;
        }

        /// <summary>입력 기록에 넣어 같은 맵인지 확인한다.</summary>
        public uint ComputeChecksum()
        {
            var h = Hasher.Create();
            h.AddInt(Columns);
            h.AddInt(Rows);
            h.AddInt(boxes.Length);
            for (int i = 0; i < boxes.Length; i++)
            {
                h.AddByte((byte)boxes[i].Kind);
                h.AddFloat(boxes[i].MinX);
                h.AddFloat(boxes[i].MinY);
                h.AddFloat(boxes[i].MaxX);
                h.AddFloat(boxes[i].MaxY);
            }
            h.AddFloat(SpawnFoot0.X);
            h.AddFloat(SpawnFoot0.Y);
            h.AddFloat(SpawnFoot1.X);
            h.AddFloat(SpawnFoot1.Y);
            return h.Hash;
        }
    }
}

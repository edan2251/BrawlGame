namespace Knockbox.Simulation
{
    public enum TerrainKind : byte
    {
        Solid,
        OneWay,
        Bounce,
        Spike,
        /// <summary>
        /// 구조물의 임시 물체(CR-002, CP13): 아직 물리가 없는 구조물이 움직이지 않는 단단한 상자로 충돌한다.
        /// 동작은 Solid와 같고, 연출과 맵 측정만 구분한다.
        /// </summary>
        Prop,
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

        /// <summary>벽 타기·벽 점프가 되는 단단한 상자(고정 지형 또는 구조물 임시 물체).</summary>
        public bool IsSolidBlock => Kind == TerrainKind.Solid || Kind == TerrainKind.Prop;

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
        readonly PropDef[] props = new PropDef[0];

        /// <param name="spawnFoot0">P1 시작 칸의 가로 중심과 칸 바닥 높이. 파이터는 이 점 위에 선다.</param>
        public ArenaDef(string name, ArenaBox[] boxes, Vec2 spawnFoot0, Vec2 spawnFoot1)
            : this(name, ClassicColumns, ClassicRows, boxes, spawnFoot0, spawnFoot1)
        {
        }

        public ArenaDef(string name, int columns, int rows, ArenaBox[] boxes, Vec2 spawnFoot0, Vec2 spawnFoot1)
            : this(name, columns, rows, false, boxes, spawnFoot0, spawnFoot1)
        {
        }

        /// <param name="hasBoundary">
        /// 맵 v2(CR-002): 바깥 테두리 칸은 지형이 아니고, 테두리 안쪽 면이 경계 튕김(03-combat.md 3절)의 경계가 된다.
        /// </param>
        public ArenaDef(string name, int columns, int rows, bool hasBoundary, ArenaBox[] boxes, Vec2 spawnFoot0, Vec2 spawnFoot1)
            : this(name, columns, rows, hasBoundary, boxes, spawnFoot0, spawnFoot1, new PropDef[0])
        {
        }

        ArenaDef(string name, int columns, int rows, bool hasBoundary, ArenaBox[] boxes, Vec2 spawnFoot0, Vec2 spawnFoot1, PropDef[] props)
        {
            this.props = props;
            Name = name;
            Columns = columns;
            Rows = rows;
            HasBoundary = hasBoundary;
            this.boxes = boxes;
            SpawnFoot0 = spawnFoot0;
            SpawnFoot1 = spawnFoot1;
        }

        public string Name { get; }
        public int Columns { get; }
        public int Rows { get; }
        public float HalfWidth => Columns * 0.5f;
        public float HalfHeight => Rows * 0.5f;
        /// <summary>경계 튕김을 쓰는 맵(테두리 벽 없음).</summary>
        public bool HasBoundary { get; }
        /// <summary>경계 사각형의 반폭·반높이: 테두리 칸의 안쪽 면. 36 × 20이면 ±17 × ±9(16:9 화면 ±17.8 × ±10 안쪽).</summary>
        public float BoundaryHalfWidth => HalfWidth - 1f;
        public float BoundaryHalfHeight => HalfHeight - 1f;
        public Vec2 SpawnFoot0 { get; }
        public Vec2 SpawnFoot1 { get; }
        public int BoxCount => boxes.Length;

        /// <summary>구조물(짝 파일, 09-props-physics.md 5절). 상자 목록의 PropBoxStart부터 같은 순서로 임시 물체 상자가 있다.</summary>
        public int PropCount => props.Length;
        public ref readonly PropDef GetProp(int index) => ref props[index];
        public int PropBoxStart => boxes.Length - props.Length;

        /// <summary>
        /// 같은 맵에 구조물을 더한 새 맵. CP13: 구조물마다 움직이지 않는 임시 물체 상자(TerrainKind.Prop)를 지형 뒤에 붙인다.
        /// </summary>
        public ArenaDef WithProps(PropDef[] newProps)
        {
            int terrain = PropBoxStart;
            var all = new ArenaBox[terrain + newProps.Length];
            for (int i = 0; i < terrain; i++)
                all[i] = boxes[i];
            for (int i = 0; i < newProps.Length; i++)
            {
                ref readonly var p = ref newProps[i];
                all[terrain + i] = new ArenaBox(TerrainKind.Prop, p.MinX, p.MinY, p.MaxX, p.MaxY);
            }
            return new ArenaDef(Name, Columns, Rows, HasBoundary, all, SpawnFoot0, SpawnFoot1, (PropDef[])newProps.Clone());
        }

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
            h.AddBool(HasBoundary);
            h.AddInt(boxes.Length);
            for (int i = 0; i < boxes.Length; i++)
            {
                h.AddByte((byte)boxes[i].Kind);
                h.AddFloat(boxes[i].MinX);
                h.AddFloat(boxes[i].MinY);
                h.AddFloat(boxes[i].MaxX);
                h.AddFloat(boxes[i].MaxY);
            }
            h.AddInt(props.Length);
            for (int i = 0; i < props.Length; i++)
            {
                h.AddByte((byte)props[i].Kind);
                h.AddInt(props[i].Group);
                h.AddBool(props[i].HasAnchor);
                h.AddFloat(props[i].Anchor.X);
                h.AddFloat(props[i].Anchor.Y);
                h.AddInt(props[i].AnchorProp);
            }
            h.AddFloat(SpawnFoot0.X);
            h.AddFloat(SpawnFoot0.Y);
            h.AddFloat(SpawnFoot1.X);
            h.AddFloat(SpawnFoot1.Y);
            return h.Hash;
        }
    }
}

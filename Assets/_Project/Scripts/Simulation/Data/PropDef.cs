namespace Knockbox.Simulation
{
    /// <summary>
    /// 구조물 종류(09-props-physics.md 2절). 맵은 CP13부터 모든 종류를 선언하고(D-030),
    /// 아직 물리가 없는 종류는 그 단계가 올 때까지 움직이지 않는 임시 물체로 충돌한다.
    /// </summary>
    public enum PropKind : byte
    {
        /// <summary>1단계: 고정점(또는 다른 구조물)에 줄로 매달린 블록.</summary>
        Hanging,
        /// <summary>1단계: 축에 고정돼 도는 블록. 축은 Anchor(없으면 가운데).</summary>
        Spinner,
        /// <summary>2단계: 한쪽 끝 경첩(Anchor)으로 기우는 발판.</summary>
        Hinged,
        /// <summary>2단계: 가운데 경첩(Anchor)으로 기우는 긴 발판.</summary>
        Seesaw,
        /// <summary>2단계: 줄·경첩으로 이은 여러 블록(틀, 사슬). 같은 Group이 한 구조.</summary>
        Linked,
        /// <summary>3단계: 받침 위에 쌓은 상자 기둥. 같은 Group의 상자들이 아래에서 위 순서.</summary>
        BoxPillar,
        /// <summary>3단계: 자유롭게 놓인 상자.</summary>
        LooseBox,
    }

    /// <summary>구조물 하나(강체 하나). 각도는 CP13에서 모두 0(축 정렬). 회전 상태는 CP14부터 시뮬레이션 상태에 둔다.</summary>
    public struct PropDef
    {
        public const int KindCount = 7;

        public PropKind Kind;
        /// <summary>떨어진 물체 묶음 번호(04-arena.md 6절 원칙 3). 기둥·연결 구조는 여러 강체가 한 묶음.</summary>
        public int Group;
        public Vec2 Center;
        public Vec2 HalfSize;
        /// <summary>줄의 고정점(매달린 블록, 연결 구조) 또는 축(회전 블록, 경첩 발판, 시소).</summary>
        public bool HasAnchor;
        public Vec2 Anchor;
        /// <summary>-1이면 Anchor는 월드 점. 아니면 줄이 이어진 구조물 번호(사슬), Anchor는 그 구조물 쪽 끝의 월드 위치.</summary>
        public int AnchorProp;

        public float MinX => Center.X - HalfSize.X;
        public float MaxX => Center.X + HalfSize.X;
        public float MinY => Center.Y - HalfSize.Y;
        public float MaxY => Center.Y + HalfSize.Y;

        /// <summary>줄로 매달리는 종류(축이 아니라 줄로 Anchor에 이어짐).</summary>
        public bool HangsByRope => HasAnchor && (Kind == PropKind.Hanging || Kind == PropKind.Linked);

        public static int Stage(PropKind kind) => kind switch
        {
            PropKind.Hanging => 1,
            PropKind.Spinner => 1,
            PropKind.BoxPillar => 3,
            PropKind.LooseBox => 3,
            _ => 2,
        };

        public override string ToString() => $"{Kind} g{Group} {Center} ±{HalfSize}";
    }
}

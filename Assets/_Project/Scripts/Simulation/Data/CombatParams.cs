using System;

namespace Knockbox.Simulation
{
    /// <summary>
    /// 공격 버튼이 무엇을 하는가(03-combat.md 1절). CP4는 테스트용 공격 하나만 있다.
    /// 모드 A(돌진)와 B(휘두르기)는 CP5·CP6에서 추가한다.
    /// </summary>
    public enum CombatMode : byte
    {
        Test,
    }

    /// <summary>공격 판정 원 하나. 위치는 파이터 중심 기준이고 x는 바라보는 방향이다.</summary>
    [Serializable]
    public struct HitCircle
    {
        /// <summary>활성 첫 틱의 위치.</summary>
        public Vec2 Start;
        /// <summary>활성 마지막 틱의 위치. 사이 틱은 직선으로 옮긴다.</summary>
        public Vec2 End;
        public float Radius;

        public HitCircle(Vec2 start, Vec2 end, float radius)
        {
            Start = start;
            End = end;
            Radius = radius;
        }

        public readonly void Visit<T>(ref T v) where T : struct, IStateVisitor
        {
            v.Value("Start.X", Start.X);
            v.Value("Start.Y", Start.Y);
            v.Value("End.X", End.X);
            v.Value("End.Y", End.Y);
            v.Value(nameof(Radius), Radius);
        }
    }

    /// <summary>
    /// 공격 하나의 데이터. 시작 → 활성 → 후딜 틱, 데미지, 넉백, 판정 원(최대 3개).
    /// 넉백 방향은 바라보는 방향 기준 단위 벡터이며 SimConfig를 만들 때 정규화한다(삼각함수를 쓰지 않는다).
    /// </summary>
    [Serializable]
    public struct AttackDef
    {
        public const int MaxCircles = 3;

        public int StartupTicks;
        public int ActiveTicks;
        public int RecoveryTicks;
        public int Damage;
        public float KnockbackStrength;
        public Vec2 KnockbackDirection;
        public int CircleCount;
        public HitCircle Circle0;
        public HitCircle Circle1;
        public HitCircle Circle2;

        public readonly int TotalTicks => StartupTicks + ActiveTicks + RecoveryTicks;

        public readonly HitCircle GetCircle(int index) => index == 0 ? Circle0 : index == 1 ? Circle1 : Circle2;

        /// <summary>
        /// 테스트용 공격(CP4): 앞으로 뻗는 주먹. 시작 5 / 활성 4 / 후딜 14, 데미지 12, 넉백 14 앞쪽 위 30°.
        /// 프레임 수와 넉백은 모드 B의 앞 휘두르기와 같게 두어 공통 규칙을 현실적인 값으로 시험한다.
        /// </summary>
        public static AttackDef TestJab => new AttackDef
        {
            StartupTicks = 5,
            ActiveTicks = 4,
            RecoveryTicks = 14,
            Damage = 12,
            KnockbackStrength = 14f,
            KnockbackDirection = new Vec2(0.8660254f, 0.5f),
            CircleCount = 2,
            Circle0 = new HitCircle(new Vec2(0.55f, 0.05f), new Vec2(0.95f, 0.05f), 0.3f),
            Circle1 = new HitCircle(new Vec2(0.35f, 0.05f), new Vec2(0.6f, 0.05f), 0.22f),
        };

        public readonly void Visit<T>(ref T v) where T : struct, IStateVisitor
        {
            v.Value(nameof(StartupTicks), StartupTicks);
            v.Value(nameof(ActiveTicks), ActiveTicks);
            v.Value(nameof(RecoveryTicks), RecoveryTicks);
            v.Value(nameof(Damage), Damage);
            v.Value(nameof(KnockbackStrength), KnockbackStrength);
            v.Value("KnockbackDirection.X", KnockbackDirection.X);
            v.Value("KnockbackDirection.Y", KnockbackDirection.Y);
            v.Value(nameof(CircleCount), CircleCount);
            v.Begin(nameof(Circle0));
            Circle0.Visit(ref v);
            v.End();
            v.Begin(nameof(Circle1));
            Circle1.Visit(ref v);
            v.End();
            v.Begin(nameof(Circle2));
            Circle2.Visit(ref v);
            v.End();
        }
    }

    /// <summary>
    /// 전투 공통 규칙의 튜닝값(03-combat.md 3절). 시간은 틱, 속도는 유닛/초.
    /// CombatTuning(ScriptableObject)이 이 구조체를 그대로 직렬화한다.
    /// </summary>
    [Serializable]
    public struct CombatParams
    {
        public int MaxHealth;

        // 히트스탑: 일반 명중은 clamp(Base + 데미지 × PerDamage, Min, Max).
        public float HitstopBase;
        public float HitstopPerDamage;
        public int HitstopMin;
        public int HitstopMax;
        public int ClashHitstop;
        public int ParryHitstop;
        public int WallSplatHitstop;
        public int KoHitstop;

        // 넉백과 피격 경직: 경직 = clamp(round(넉백 × PerKnockback), Min, Max).
        public float HitstunPerKnockback;
        public int HitstunMin;
        public int HitstunMax;
        public float ComboKnockbackDecay;
        public float ComboHitstunDecay;
        /// <summary>연속으로 맞을 수 있는 최대 횟수. 이만큼 맞은 파이터는 경직이 풀릴 때까지 공격에 맞지 않는다.</summary>
        public int MaxComboHits;

        // 벽꽝과 튕김
        public float WallSplatSpeed;
        public int WallSplatDamage;
        public float WallSplatRestitution;
        public int WallSplatHitstun;
        public float BounceRestitution;
        /// <summary>법선 속도가 이보다 작으면 튕기지 않고 그냥 닿는다(바닥에서 잔떨림 방지).</summary>
        public float BounceMinSpeed;

        // 가드와 패링
        public int ParryWindowTicks;
        public int ParryCooldownTicks;
        public float GuardMaxSpeed;
        public float GuardDamageMultiplier;
        public float GuardKnockbackMultiplier;
        public int GuardStunTicks;
        public float GuardHitstopMultiplier;
        public int StaggerTicks;
        public float StaggerPushSpeed;

        // 클래시
        public float ClashSpeed;
        public int RecoilTicks;

        /// <summary>공격 중 공중 조작 배율. 지상에서는 입력 가속 없이 감속만 한다.</summary>
        public float AttackAirControlMultiplier;

        public AttackDef TestAttack;

        public static CombatParams Default => new CombatParams
        {
            MaxHealth = 100,

            HitstopBase = 4f,
            HitstopPerDamage = 0.25f,
            HitstopMin = 4,
            HitstopMax = 12,
            ClashHitstop = 8,
            ParryHitstop = 10,
            WallSplatHitstop = 6,
            KoHitstop = 20,

            HitstunPerKnockback = 1.1f,
            HitstunMin = 8,
            HitstunMax = 45,
            ComboKnockbackDecay = 0.85f,
            ComboHitstunDecay = 0.8f,
            MaxComboHits = 3,

            WallSplatSpeed = 14f,
            WallSplatDamage = 6,
            WallSplatRestitution = 0.45f,
            WallSplatHitstun = 18,
            BounceRestitution = 0.35f,
            BounceMinSpeed = 2f,

            ParryWindowTicks = 6,
            ParryCooldownTicks = 24,
            GuardMaxSpeed = 3f,
            GuardDamageMultiplier = 0.2f,
            GuardKnockbackMultiplier = 0.35f,
            GuardStunTicks = 10,
            GuardHitstopMultiplier = 0.5f,
            StaggerTicks = 28,
            StaggerPushSpeed = 8f,

            ClashSpeed = 8f,
            RecoilTicks = 12,

            AttackAirControlMultiplier = 0.5f,

            TestAttack = AttackDef.TestJab,
        };

        public readonly void Visit<T>(ref T v) where T : struct, IStateVisitor
        {
            v.Value(nameof(MaxHealth), MaxHealth);
            v.Value(nameof(HitstopBase), HitstopBase);
            v.Value(nameof(HitstopPerDamage), HitstopPerDamage);
            v.Value(nameof(HitstopMin), HitstopMin);
            v.Value(nameof(HitstopMax), HitstopMax);
            v.Value(nameof(ClashHitstop), ClashHitstop);
            v.Value(nameof(ParryHitstop), ParryHitstop);
            v.Value(nameof(WallSplatHitstop), WallSplatHitstop);
            v.Value(nameof(KoHitstop), KoHitstop);
            v.Value(nameof(HitstunPerKnockback), HitstunPerKnockback);
            v.Value(nameof(HitstunMin), HitstunMin);
            v.Value(nameof(HitstunMax), HitstunMax);
            v.Value(nameof(ComboKnockbackDecay), ComboKnockbackDecay);
            v.Value(nameof(ComboHitstunDecay), ComboHitstunDecay);
            v.Value(nameof(MaxComboHits), MaxComboHits);
            v.Value(nameof(WallSplatSpeed), WallSplatSpeed);
            v.Value(nameof(WallSplatDamage), WallSplatDamage);
            v.Value(nameof(WallSplatRestitution), WallSplatRestitution);
            v.Value(nameof(WallSplatHitstun), WallSplatHitstun);
            v.Value(nameof(BounceRestitution), BounceRestitution);
            v.Value(nameof(BounceMinSpeed), BounceMinSpeed);
            v.Value(nameof(ParryWindowTicks), ParryWindowTicks);
            v.Value(nameof(ParryCooldownTicks), ParryCooldownTicks);
            v.Value(nameof(GuardMaxSpeed), GuardMaxSpeed);
            v.Value(nameof(GuardDamageMultiplier), GuardDamageMultiplier);
            v.Value(nameof(GuardKnockbackMultiplier), GuardKnockbackMultiplier);
            v.Value(nameof(GuardStunTicks), GuardStunTicks);
            v.Value(nameof(GuardHitstopMultiplier), GuardHitstopMultiplier);
            v.Value(nameof(StaggerTicks), StaggerTicks);
            v.Value(nameof(StaggerPushSpeed), StaggerPushSpeed);
            v.Value(nameof(ClashSpeed), ClashSpeed);
            v.Value(nameof(RecoilTicks), RecoilTicks);
            v.Value(nameof(AttackAirControlMultiplier), AttackAirControlMultiplier);
            v.Begin(nameof(TestAttack));
            TestAttack.Visit(ref v);
            v.End();
        }
    }
}

using System;

namespace Knockbox.Simulation
{
    /// <summary>
    /// 공격 버튼이 무엇을 하는가(03-combat.md 1절).
    /// Test: 테스트용 공격(CP4). Charge: 모드 A 돌진 충돌(CP5). Swing: 모드 B 무기 휘두르기(CP6).
    /// </summary>
    public enum CombatMode : byte
    {
        Test,
        Charge,
        Swing,
    }

    /// <summary>SimConfig.GetAttack의 공격 번호.</summary>
    public static class AttackIds
    {
        public const int Test = 0;
        public const int SwingForward = 1;
        public const int SwingUp = 2;
        public const int SwingDown = 3;
    }

    /// <summary>모드 B: 무기 휘두르기(03-combat.md 5절).</summary>
    [Serializable]
    public struct SwingModeParams
    {
        public AttackDef Forward;
        public AttackDef Up;
        public AttackDef Down;
        /// <summary>대시 시작 뒤 이 틱이 지나면 공격으로 대시를 캔슬할 수 있다(대시의 관성을 실은 공격).</summary>
        public bool DashAttackCancelEnabled;
        public int DashAttackCancelMinTicks;
        /// <summary>공중에서 시작한 공격 도중 착지하면 이만큼 조작할 수 없다.</summary>
        public int LandingLagTicks;

        public static SwingModeParams Default => new SwingModeParams
        {
            Forward = AttackDef.SwingForward,
            Up = AttackDef.SwingUp,
            Down = AttackDef.SwingDown,
            DashAttackCancelEnabled = true,
            DashAttackCancelMinTicks = 4,
            LandingLagTicks = 4,
        };

        public readonly void Visit<T>(ref T v) where T : struct, IStateVisitor
        {
            v.Begin(nameof(Forward));
            Forward.Visit(ref v);
            v.End();
            v.Begin(nameof(Up));
            Up.Visit(ref v);
            v.End();
            v.Begin(nameof(Down));
            Down.Visit(ref v);
            v.End();
            v.Value(nameof(DashAttackCancelEnabled), DashAttackCancelEnabled);
            v.Value(nameof(DashAttackCancelMinTicks), DashAttackCancelMinTicks);
            v.Value(nameof(LandingLagTicks), LandingLagTicks);
        }
    }

    /// <summary>모드 A: 돌진 충돌(03-combat.md 4절). 위력 = max(0, 속도·법선) × 공격성 가중치.</summary>
    [Serializable]
    public struct ChargeModeParams
    {
        /// <summary>충전 중 지상(과 공중) 최대 이동 속도.</summary>
        public float ChargeMoveMaxSpeed;
        /// <summary>충전 중 공중 최대 낙하 속도(살짝 떠 있음).</summary>
        public float ChargeFallMaxSpeed;
        /// <summary>이보다 짧게 누르고 떼면 최소 돌진.</summary>
        public int MinChargeTicks;
        public int FullChargeTicks;
        /// <summary>최대 충전 뒤 이만큼 더 누르고 있으면 자동 발사.</summary>
        public int FullChargeHoldTicks;
        public float LungeMinSpeed;
        public float LungeMaxSpeed;
        public float LungeGravityMultiplier;
        /// <summary>속도가 이보다 작아지면 돌진이 끝난다.</summary>
        public float LungeEndSpeed;
        public int LungeMaxTicks;
        public float LungeBounceRestitution;
        public int LungeMaxBounces;
        public float LungeAggression;
        public float DashAggression;
        /// <summary>위력이 이 이상이어야 피해를 준다.</summary>
        public float PowerThreshold;
        /// <summary>둘 다 기준 이상이고 위력 차이가 이보다 작으면 클래시.</summary>
        public float ClashPowerMargin;
        /// <summary>같은 두 파이터 사이의 몸 충돌 판정 간격.</summary>
        public int ContactCooldownTicks;
        /// <summary>몸이 이 거리 안이면 닿은 것으로 본다(밀어내기로 딱 붙은 상태 포함).</summary>
        public float ContactSlop;
        public float DamagePerPower;
        public int MinDamage;
        public int MaxDamage;
        public float KnockbackPerPower;
        public float MinKnockback;
        /// <summary>이긴 쪽 속도 배율(살짝 튕겨 나옴).</summary>
        public float WinnerVelocityScale;
        /// <summary>패링당한 돌진의 반사 배율.</summary>
        public float ParryReflectScale;
        /// <summary>클래시 반동 속도 = max(ClashSpeed, 큰 위력 × 이 값).</summary>
        public float ClashSpeedPerPower;

        public static ChargeModeParams Default => new ChargeModeParams
        {
            ChargeMoveMaxSpeed = 3f,
            ChargeFallMaxSpeed = 4f,
            MinChargeTicks = 4,
            FullChargeTicks = 36,
            FullChargeHoldTicks = 24,
            LungeMinSpeed = 18f,
            LungeMaxSpeed = 34f,
            LungeGravityMultiplier = 0.3f,
            LungeEndSpeed = 12f,
            LungeMaxTicks = 30,
            LungeBounceRestitution = 0.6f,
            LungeMaxBounces = 2,
            LungeAggression = 1f,
            DashAggression = 0.6f,
            PowerThreshold = 6f,
            ClashPowerMargin = 4f,
            ContactCooldownTicks = 10,
            ContactSlop = 0.05f,
            DamagePerPower = 0.6f,
            MinDamage = 4,
            MaxDamage = 24,
            KnockbackPerPower = 0.9f,
            MinKnockback = 10f,
            WinnerVelocityScale = -0.25f,
            ParryReflectScale = 0.8f,
            ClashSpeedPerPower = 0.35f,
        };

        /// <summary>충전 틱 → 돌진 속도(18 → 34 선형, MinChargeTicks 미만은 최소).</summary>
        public readonly float LungeSpeed(int chargeTicks)
        {
            float ratio = chargeTicks < MinChargeTicks ? 0f : SimMath.Min(chargeTicks, FullChargeTicks) / (float)FullChargeTicks;
            return LungeMinSpeed + (LungeMaxSpeed - LungeMinSpeed) * ratio;
        }

        public readonly void Visit<T>(ref T v) where T : struct, IStateVisitor
        {
            v.Value(nameof(ChargeMoveMaxSpeed), ChargeMoveMaxSpeed);
            v.Value(nameof(ChargeFallMaxSpeed), ChargeFallMaxSpeed);
            v.Value(nameof(MinChargeTicks), MinChargeTicks);
            v.Value(nameof(FullChargeTicks), FullChargeTicks);
            v.Value(nameof(FullChargeHoldTicks), FullChargeHoldTicks);
            v.Value(nameof(LungeMinSpeed), LungeMinSpeed);
            v.Value(nameof(LungeMaxSpeed), LungeMaxSpeed);
            v.Value(nameof(LungeGravityMultiplier), LungeGravityMultiplier);
            v.Value(nameof(LungeEndSpeed), LungeEndSpeed);
            v.Value(nameof(LungeMaxTicks), LungeMaxTicks);
            v.Value(nameof(LungeBounceRestitution), LungeBounceRestitution);
            v.Value(nameof(LungeMaxBounces), LungeMaxBounces);
            v.Value(nameof(LungeAggression), LungeAggression);
            v.Value(nameof(DashAggression), DashAggression);
            v.Value(nameof(PowerThreshold), PowerThreshold);
            v.Value(nameof(ClashPowerMargin), ClashPowerMargin);
            v.Value(nameof(ContactCooldownTicks), ContactCooldownTicks);
            v.Value(nameof(ContactSlop), ContactSlop);
            v.Value(nameof(DamagePerPower), DamagePerPower);
            v.Value(nameof(MinDamage), MinDamage);
            v.Value(nameof(MaxDamage), MaxDamage);
            v.Value(nameof(KnockbackPerPower), KnockbackPerPower);
            v.Value(nameof(MinKnockback), MinKnockback);
            v.Value(nameof(WinnerVelocityScale), WinnerVelocityScale);
            v.Value(nameof(ParryReflectScale), ParryReflectScale);
            v.Value(nameof(ClashSpeedPerPower), ClashSpeedPerPower);
        }
    }

    /// <summary>
    /// 공격 판정 원 하나. 위치는 파이터 중심 기준이고 x는 바라보는 방향이다.
    /// 활성 구간 동안 Start → Mid → End를 꺾은선으로 따라간다(휘두르는 호를 삼각함수 없이 근사).
    /// </summary>
    [Serializable]
    public struct HitCircle
    {
        /// <summary>활성 첫 틱의 위치.</summary>
        public Vec2 Start;
        /// <summary>활성 구간 가운데의 위치.</summary>
        public Vec2 Mid;
        /// <summary>활성 마지막 틱의 위치.</summary>
        public Vec2 End;
        public float Radius;

        public HitCircle(Vec2 start, Vec2 mid, Vec2 end, float radius)
        {
            Start = start;
            Mid = mid;
            End = end;
            Radius = radius;
        }

        /// <summary>직선으로 움직이는 원(가운데 = 두 끝의 중간).</summary>
        public HitCircle(Vec2 start, Vec2 end, float radius)
            : this(start, (start + end) * 0.5f, end, radius)
        {
        }

        /// <summary>활성 진행도 t(0 = 첫 틱, 1 = 마지막 틱)의 위치.</summary>
        public readonly Vec2 At(float t)
        {
            if (t <= 0.5f)
                return Start + (Mid - Start) * (t * 2f);
            return Mid + (End - Mid) * ((t - 0.5f) * 2f);
        }

        public readonly void Visit<T>(ref T v) where T : struct, IStateVisitor
        {
            v.Value("Start.X", Start.X);
            v.Value("Start.Y", Start.Y);
            v.Value("Mid.X", Mid.X);
            v.Value("Mid.Y", Mid.Y);
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
        /// <summary>0보다 크면 명중했을 때 공격자 vy를 이 값으로 바꾸고 공중 점프·대시를 회복한다(내려찍기 포고 튕김).</summary>
        public float PogoSpeed;

        public readonly int TotalTicks => StartupTicks + ActiveTicks + RecoveryTicks;

        /// <summary>실제로 쓰는 판정 원 개수(0~3). 설정 값은 그대로 두고 쓸 때 자른다(설정 체크섬이 모든 값을 덮게).</summary>
        public readonly int UsedCircles => SimMath.Clamp(CircleCount, 0, MaxCircles);

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

        // 모드 B 기술. 판정 원 3개는 무기를 따라 손잡이 쪽(0.45)부터 끝(1.15)까지 놓이고,
        // 어깨 근처 회전축을 도는 호의 세 각도(시작·가운데·끝)를 키프레임으로 쓴다(값은 미리 계산한 상수).

        /// <summary>앞 휘두르기: 5/4/14, 데미지 12, 넉백 14 앞쪽 위 30°. 머리 위 앞(70°)에서 앞 아래(-25°)로.</summary>
        public static AttackDef SwingForward => new AttackDef
        {
            StartupTicks = 5,
            ActiveTicks = 4,
            RecoveryTicks = 14,
            Damage = 12,
            KnockbackStrength = 14f,
            KnockbackDirection = new Vec2(0.8660254f, 0.5f),
            CircleCount = 3,
            Circle0 = new HitCircle(new Vec2(0.504f, 0.673f), new Vec2(0.773f, 0.404f), new Vec2(0.758f, 0.06f), 0.22f),
            Circle1 = new HitCircle(new Vec2(0.624f, 1.002f), new Vec2(1.102f, 0.524f), new Vec2(1.075f, -0.088f), 0.24f),
            Circle2 = new HitCircle(new Vec2(0.743f, 1.331f), new Vec2(1.431f, 0.643f), new Vec2(1.392f, -0.236f), 0.26f),
        };

        /// <summary>위 휘두르기: 5/4/16, 데미지 10, 넉백 16 위 80°. 앞(20°)에서 머리 위(90°)를 지나 뒤(160°)로.</summary>
        public static AttackDef SwingUp => new AttackDef
        {
            StartupTicks = 5,
            ActiveTicks = 4,
            RecoveryTicks = 16,
            Damage = 10,
            KnockbackStrength = 16f,
            KnockbackDirection = new Vec2(0.1736482f, 0.9848078f),
            CircleCount = 3,
            Circle0 = new HitCircle(new Vec2(0.573f, 0.454f), new Vec2(0.15f, 0.75f), new Vec2(-0.273f, 0.454f), 0.22f),
            Circle1 = new HitCircle(new Vec2(0.902f, 0.574f), new Vec2(0.15f, 1.1f), new Vec2(-0.602f, 0.574f), 0.24f),
            Circle2 = new HitCircle(new Vec2(1.231f, 0.693f), new Vec2(0.15f, 1.45f), new Vec2(-0.931f, 0.693f), 0.26f),
        };

        /// <summary>
        /// 아래 내려찍기(공중 전용): 6/5/16, 데미지 10, 넉백 12 아래 앞 -70°. 앞 위(40°)에서 바로 아래(-90°)로.
        /// 명중하면 공격자 vy = 18(포고 튕김), 공중 점프·대시 회복.
        /// </summary>
        public static AttackDef SwingDown => new AttackDef
        {
            StartupTicks = 6,
            ActiveTicks = 5,
            RecoveryTicks = 16,
            Damage = 10,
            KnockbackStrength = 12f,
            KnockbackDirection = new Vec2(0.3420201f, -0.9396926f),
            CircleCount = 3,
            Circle0 = new HitCircle(new Vec2(0.645f, 0.389f), new Vec2(0.69f, -0.125f), new Vec2(0.3f, -0.35f), 0.22f),
            Circle1 = new HitCircle(new Vec2(0.913f, 0.614f), new Vec2(0.993f, -0.3f), new Vec2(0.3f, -0.7f), 0.24f),
            Circle2 = new HitCircle(new Vec2(1.181f, 0.839f), new Vec2(1.296f, -0.475f), new Vec2(0.3f, -1.05f), 0.26f),
            PogoSpeed = 18f,
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
            v.Value(nameof(PogoSpeed), PogoSpeed);
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
        public ChargeModeParams Charge;
        public SwingModeParams Swing;

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
            Charge = ChargeModeParams.Default,
            Swing = SwingModeParams.Default,
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
            v.Begin(nameof(Charge));
            Charge.Visit(ref v);
            v.End();
            v.Begin(nameof(Swing));
            Swing.Visit(ref v);
            v.End();
        }
    }
}

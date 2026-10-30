using System;

namespace Knockbox.Simulation
{
    /// <summary>
    /// 이동 튜닝값(02-movement.md). 시간은 틱, 속도는 유닛/초, 가속도는 유닛/초².
    /// MovementTuning(ScriptableObject)이 이 구조체를 그대로 직렬화한다.
    /// CP1의 임시 이동은 일부만 쓰고, 나머지는 CP2에서 쓰기 시작한다.
    /// </summary>
    [Serializable]
    public struct MovementParams
    {
        public float BodySize;

        public float MaxRunSpeed;
        public float GroundAccel;
        public float GroundDecel;
        public float TurnAccelMultiplier;
        public float AirAccel;
        public float AirDecel;

        public float JumpSpeed;
        public float RiseGravity;
        public float FallGravityMultiplier;
        public float EarlyReleaseGravityMultiplier;
        public float ApexSpeedThreshold;
        public float ApexGravityMultiplier;
        public float ApexAirAccelMultiplier;
        public float MaxFallSpeed;
        public float FastFallMaxSpeed;
        public float FastFallGravityMultiplier;
        public int AirJumps;
        public float AirJumpSpeed;
        public int CoyoteTicks;
        public int JumpBufferTicks;

        public float WallSlideMaxSpeed;
        public float WallJumpSpeedX;
        public float WallJumpSpeedY;
        public int WallJumpInputLockTicks;
        public int WallCoyoteTicks;

        public float DashSpeed;
        public int DashTicks;
        public float DashEndSpeedRetention;
        public int DashCharges;
        public int DashCooldownTicks;
        public bool DashJumpCancelEnabled;
        public int DashJumpCancelMinTicks;

        public float SubstepMaxDistance;
        public float GroundProbeDistance;
        public int DropThroughTicks;
        public float BouncePadSpeed;

        public static MovementParams Default => new MovementParams
        {
            BodySize = 0.9f,

            MaxRunSpeed = 11f,
            GroundAccel = 110f,
            GroundDecel = 80f,
            TurnAccelMultiplier = 1.5f,
            AirAccel = 70f,
            AirDecel = 25f,

            JumpSpeed = 22.9f,
            RiseGravity = 65f,
            FallGravityMultiplier = 1.6f,
            EarlyReleaseGravityMultiplier = 2.5f,
            ApexSpeedThreshold = 3f,
            ApexGravityMultiplier = 0.5f,
            ApexAirAccelMultiplier = 1.2f,
            MaxFallSpeed = 30f,
            FastFallMaxSpeed = 40f,
            FastFallGravityMultiplier = 1.4f,
            AirJumps = 1,
            AirJumpSpeed = 19f,
            CoyoteTicks = 6,
            JumpBufferTicks = 8,

            WallSlideMaxSpeed = 5f,
            WallJumpSpeedX = 13f,
            WallJumpSpeedY = 20f,
            WallJumpInputLockTicks = 7,
            WallCoyoteTicks = 5,

            DashSpeed = 26f,
            DashTicks = 8,
            DashEndSpeedRetention = 0.55f,
            DashCharges = 1,
            DashCooldownTicks = 18,
            DashJumpCancelEnabled = true,
            DashJumpCancelMinTicks = 3,

            SubstepMaxDistance = 0.2f,
            GroundProbeDistance = 0.02f,
            DropThroughTicks = 12,
            BouncePadSpeed = 30f,
        };

        public readonly void Visit<T>(ref T v) where T : struct, IStateVisitor
        {
            v.Value(nameof(BodySize), BodySize);
            v.Value(nameof(MaxRunSpeed), MaxRunSpeed);
            v.Value(nameof(GroundAccel), GroundAccel);
            v.Value(nameof(GroundDecel), GroundDecel);
            v.Value(nameof(TurnAccelMultiplier), TurnAccelMultiplier);
            v.Value(nameof(AirAccel), AirAccel);
            v.Value(nameof(AirDecel), AirDecel);
            v.Value(nameof(JumpSpeed), JumpSpeed);
            v.Value(nameof(RiseGravity), RiseGravity);
            v.Value(nameof(FallGravityMultiplier), FallGravityMultiplier);
            v.Value(nameof(EarlyReleaseGravityMultiplier), EarlyReleaseGravityMultiplier);
            v.Value(nameof(ApexSpeedThreshold), ApexSpeedThreshold);
            v.Value(nameof(ApexGravityMultiplier), ApexGravityMultiplier);
            v.Value(nameof(ApexAirAccelMultiplier), ApexAirAccelMultiplier);
            v.Value(nameof(MaxFallSpeed), MaxFallSpeed);
            v.Value(nameof(FastFallMaxSpeed), FastFallMaxSpeed);
            v.Value(nameof(FastFallGravityMultiplier), FastFallGravityMultiplier);
            v.Value(nameof(AirJumps), AirJumps);
            v.Value(nameof(AirJumpSpeed), AirJumpSpeed);
            v.Value(nameof(CoyoteTicks), CoyoteTicks);
            v.Value(nameof(JumpBufferTicks), JumpBufferTicks);
            v.Value(nameof(WallSlideMaxSpeed), WallSlideMaxSpeed);
            v.Value(nameof(WallJumpSpeedX), WallJumpSpeedX);
            v.Value(nameof(WallJumpSpeedY), WallJumpSpeedY);
            v.Value(nameof(WallJumpInputLockTicks), WallJumpInputLockTicks);
            v.Value(nameof(WallCoyoteTicks), WallCoyoteTicks);
            v.Value(nameof(DashSpeed), DashSpeed);
            v.Value(nameof(DashTicks), DashTicks);
            v.Value(nameof(DashEndSpeedRetention), DashEndSpeedRetention);
            v.Value(nameof(DashCharges), DashCharges);
            v.Value(nameof(DashCooldownTicks), DashCooldownTicks);
            v.Value(nameof(DashJumpCancelEnabled), DashJumpCancelEnabled);
            v.Value(nameof(DashJumpCancelMinTicks), DashJumpCancelMinTicks);
            v.Value(nameof(SubstepMaxDistance), SubstepMaxDistance);
            v.Value(nameof(GroundProbeDistance), GroundProbeDistance);
            v.Value(nameof(DropThroughTicks), DropThroughTicks);
            v.Value(nameof(BouncePadSpeed), BouncePadSpeed);
        }
    }

    /// <summary>경기 흐름 설정(06-match-and-tools.md 1절).</summary>
    [Serializable]
    public struct MatchParams
    {
        public int CountdownTicks;
        public int KoTicks;
        public int RoundsToWin;

        public static MatchParams Default => new MatchParams
        {
            CountdownTicks = 90,
            KoTicks = 60,
            RoundsToWin = 5,
        };

        public readonly void Visit<T>(ref T v) where T : struct, IStateVisitor
        {
            v.Value(nameof(CountdownTicks), CountdownTicks);
            v.Value(nameof(KoTicks), KoTicks);
            v.Value(nameof(RoundsToWin), RoundsToWin);
        }
    }

    /// <summary>
    /// 시뮬레이션 설정. 라운드 시작 시점에 튜닝 데이터에서 만들고 그 뒤로 바뀌지 않는다.
    /// </summary>
    public sealed class SimConfig
    {
        public const int TickRate = 60;
        public const float Dt = 1f / TickRate;

        public readonly MovementParams Movement;
        public readonly MatchParams Match;
        public readonly float HalfBody;

        public SimConfig(in MovementParams movement, in MatchParams match)
        {
            Movement = movement;
            Match = match;
            HalfBody = movement.BodySize * 0.5f;
        }

        public static SimConfig CreateDefault() => new SimConfig(MovementParams.Default, MatchParams.Default);

        /// <summary>입력 기록에 넣어 재생 시 같은 설정인지 확인하는 데 쓴다.</summary>
        public uint ComputeChecksum()
        {
            var h = Hasher.Create();
            Visit(ref h);
            return h.Hash;
        }

        public void Visit<T>(ref T v) where T : struct, IStateVisitor
        {
            v.Begin(nameof(Movement));
            Movement.Visit(ref v);
            v.End();
            v.Begin(nameof(Match));
            Match.Visit(ref v);
            v.End();
        }
    }
}

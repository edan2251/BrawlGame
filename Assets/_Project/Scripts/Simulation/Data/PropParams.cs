using System;

namespace Knockbox.Simulation
{
    /// <summary>구조물 물리와 캐릭터 상호작용 수치(09-props-physics.md 4·8절). 초기값이며 튜닝은 자체 결정.</summary>
    [Serializable]
    public struct PropParams
    {
        // 물리
        public float Gravity;
        public float LinearDamping;
        public float AngularDamping;
        /// <summary>면적당 질량(1 × 1 블록 = 1.0).</summary>
        public float Density;
        public float Restitution;
        public float Friction;
        /// <summary>화면 경계에서 피해 없이 튕기는 반발 계수(CR-002 3절).</summary>
        public float BoundaryRestitution;
        public int Substeps;
        public int Iterations;
        /// <summary>관통·줄 늘어남 보정(서브스텝마다 오차의 이 비율을 속도로 되돌린다).</summary>
        public float Correction;
        public float Slop;
        /// <summary>잠들기: 속도·각속도가 이보다 작은 틱이 SleepTicks번 이어지면 잠든다(처음엔 잠든 채로 놓인다).</summary>
        public float SleepSpeed;
        public float SleepAngularSpeed;
        public int SleepTicks;

        // 캐릭터 상호작용
        public float FighterMass;
        public float FighterRestitution;
        /// <summary>구조물 위에 섰다고 보는 접촉 법선의 위쪽 각도 한계의 코사인(50°).</summary>
        public float StandCos;
        /// <summary>돌진이 구조물을 칠 때 위력에 곱해 충격량으로 주는 배율.</summary>
        public float LungeImpulseScale;
        /// <summary>돌진이 이 질량 이상의 구조물을 치면 지형처럼 반사(× LungeHeavyRestitution), 아래면 계속 나아간다.</summary>
        public float LungeHeavyMass;
        public float LungeHeavyRestitution;
        /// <summary>구조물이 캐릭터를 침: 접촉점 상대 법선 속도 하한, 피해 clamp(round(속도 × 질량 × 배율), 최소, 최대).</summary>
        public float HitMinSpeed;
        public float HitDamageScale;
        public int HitMinDamage;
        public int HitMaxDamage;
        public float HitKnockbackMin;
        public float HitKnockbackScale;
        public int HitCooldownTicks;
        /// <summary>날아가는 캐릭터가 이 질량 이상의 구조물에 부딪히면 벽꽝 대상.</summary>
        public float SplatMinMass;
        public float ParryRestitution;

        public static PropParams Default => new PropParams
        {
            Gravity = 50f,
            LinearDamping = 0.15f,
            AngularDamping = 0.4f,
            Density = 1f,
            Restitution = 0.2f,
            Friction = 0.5f,
            BoundaryRestitution = 0.5f,
            Substeps = 4,
            Iterations = 4,
            Correction = 0.2f,
            Slop = 0.005f,
            SleepSpeed = 0.05f,
            SleepAngularSpeed = 0.05f,
            SleepTicks = 30,

            FighterMass = 1f,
            FighterRestitution = 0.15f,
            StandCos = 0.6427876f,
            LungeImpulseScale = 0.5f,
            LungeHeavyMass = 4f,
            LungeHeavyRestitution = 0.6f,
            HitMinSpeed = 10f,
            HitDamageScale = 0.15f,
            HitMinDamage = 3,
            HitMaxDamage = 18,
            HitKnockbackMin = 10f,
            HitKnockbackScale = 0.8f,
            HitCooldownTicks = 20,
            SplatMinMass = 4f,
            ParryRestitution = 0.8f,
        };

        public readonly void Visit<T>(ref T v) where T : struct, IStateVisitor
        {
            v.Value(nameof(Gravity), Gravity);
            v.Value(nameof(LinearDamping), LinearDamping);
            v.Value(nameof(AngularDamping), AngularDamping);
            v.Value(nameof(Density), Density);
            v.Value(nameof(Restitution), Restitution);
            v.Value(nameof(Friction), Friction);
            v.Value(nameof(BoundaryRestitution), BoundaryRestitution);
            v.Value(nameof(Substeps), Substeps);
            v.Value(nameof(Iterations), Iterations);
            v.Value(nameof(Correction), Correction);
            v.Value(nameof(Slop), Slop);
            v.Value(nameof(SleepSpeed), SleepSpeed);
            v.Value(nameof(SleepAngularSpeed), SleepAngularSpeed);
            v.Value(nameof(SleepTicks), SleepTicks);
            v.Value(nameof(FighterMass), FighterMass);
            v.Value(nameof(FighterRestitution), FighterRestitution);
            v.Value(nameof(StandCos), StandCos);
            v.Value(nameof(LungeImpulseScale), LungeImpulseScale);
            v.Value(nameof(LungeHeavyMass), LungeHeavyMass);
            v.Value(nameof(LungeHeavyRestitution), LungeHeavyRestitution);
            v.Value(nameof(HitMinSpeed), HitMinSpeed);
            v.Value(nameof(HitDamageScale), HitDamageScale);
            v.Value(nameof(HitMinDamage), HitMinDamage);
            v.Value(nameof(HitMaxDamage), HitMaxDamage);
            v.Value(nameof(HitKnockbackMin), HitKnockbackMin);
            v.Value(nameof(HitKnockbackScale), HitKnockbackScale);
            v.Value(nameof(HitCooldownTicks), HitCooldownTicks);
            v.Value(nameof(SplatMinMass), SplatMinMass);
            v.Value(nameof(ParryRestitution), ParryRestitution);
        }
    }
}

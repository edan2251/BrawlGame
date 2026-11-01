namespace Knockbox.Simulation
{
    /// <summary>접촉 하나(한 틱의 계산 결과, 상태 아님).</summary>
    public struct PropContact
    {
        public int Body;
        /// <summary>상대 강체(-1이면 지형·경계). 법선은 상대 → Body.</summary>
        public int Other;
        /// <summary>Body를 밀어내는 방향.</summary>
        public Vec2 Normal;
        public Vec2 Point;
        public float Depth;
        public float Restitution;
        public float NormalImpulse;
        public float TangentImpulse;
        public float TargetSpeed;
    }

    /// <summary>구조물 물리의 작업 공간. SimWorld가 한 번 만들고 매 틱 다시 쓴다(할당 없음).</summary>
    public sealed class PropScratch
    {
        public const int MaxContacts = 512;
        public readonly PropContact[] Contacts = new PropContact[MaxContacts];
        public int ContactCount;
        /// <summary>이번 틱에 몸마다 받은 바깥 충격(접촉·캐릭터)의 합. 끊어짐 판정에 쓴다.</summary>
        public readonly float[] TickImpulse = new float[PropState.Capacity];
    }

    /// <summary>
    /// 구조물 물리(09-props-physics.md 2절). 1단계(CP14): 회전하는 직사각형 강체, 줄(최대 거리), 축(점 고정),
    /// 고정 지형·임시 물체·화면 경계와의 충돌. 2단계(CP15): 경첩·시소(점 고정 + 각도 한계 + 복원 스프링), 몸-몸 줄(연결 구조),
    /// 강체끼리의 충돌(마찰 포함), 기준 충격 이상에서 끊어짐.
    /// 서브스텝마다 접촉을 찾고, 연결과 접촉을 같은 반복에서 순차 충격량으로 푼다(Box2D v3의 접근을 참고, 코드는 직접 작성).
    /// 회전은 (cos, sin)으로 적분해 정규화한다. 몸 번호·쌍 번호 순서대로 처리하고 반복 횟수가 고정이라 결정론적이다.
    /// </summary>
    public static class PropPhysics
    {
        /// <summary>지금 강체로 움직이는 구조물 단계(09 2절 표). 이 단계 이하의 종류만 SimState.Props에 들어간다.</summary>
        public const int ImplementedStage = 2;

        /// <summary>맵의 강체 구조물을 처음 놓인 모양(정지, 각도 0, 잠든 채)으로.</summary>
        public static void Init(ref PropState props, ArenaDef arena, SimConfig config)
        {
            props = default;
            props.Count = arena.DynamicPropCount;
            for (int i = 0; i < props.Count; i++)
            {
                ref var b = ref PropState.Get(ref props, i);
                b.Position = arena.GetProp(arena.DynamicProp(i)).Center;
                b.Rotation = new Vec2(1f, 0f);
                b.LastToucher = -1;
                b.SleepTicks = config.Combat.Props.SleepTicks;
            }
        }

        public static bool Asleep(in PropBody b, in PropParams c) => b.SleepTicks >= c.SleepTicks;

        public static float Mass(in PropDef p, in PropParams c) => c.Density * 4f * p.HalfSize.X * p.HalfSize.Y;

        public static float Inertia(in PropDef p, float mass) =>
            mass * (4f * p.HalfSize.X * p.HalfSize.X + 4f * p.HalfSize.Y * p.HalfSize.Y) / 12f;

        static void InverseMass(ArenaDef arena, in PropParams c, int body, out float invM, out float invI)
        {
            ref readonly var def = ref arena.GetProp(arena.DynamicProp(body));
            float m = Mass(def, c);
            invM = 1f / m;
            invI = 1f / Inertia(def, m);
        }

        /// <summary>몸이 깨어 있으면 줄로 이어진 상대도 깨운다(사슬이 함께 움직이게). 번호 순서로 두 번 훑어 양방향으로 전한다.</summary>
        static void WakeLinked(ref PropState props, ArenaDef arena)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < props.Count; i++)
                {
                    ref readonly var joint = ref arena.GetJoint(i);
                    if (joint.OtherBody < 0)
                        continue;
                    ref var b = ref PropState.Get(ref props, i);
                    ref var o = ref PropState.Get(ref props, joint.OtherBody);
                    if (b.Broken != 0)
                        continue;
                    if (b.SleepTicks == 0 || o.SleepTicks == 0)
                    {
                        b.SleepTicks = 0;
                        o.SleepTicks = 0;
                    }
                }
            }
        }

        /// <summary>한 틱. 히트스탑과 상관없이 계속 움직인다(09 4절 초기 결정).</summary>
        public static void Step(ref PropState props, ArenaDef arena, SimConfig config, PropScratch scratch, int tick, SimEventBuffer events)
        {
            if (props.Count == 0)
                return;
            ref readonly var c = ref config.Combat.Props;
            int substeps = SimMath.Max(1, c.Substeps);
            float h = SimConfig.Dt / substeps;
            bool anyAwake = false;
            for (int i = 0; i < props.Count; i++)
            {
                ref var b = ref PropState.Get(ref props, i);
                scratch.TickImpulse[i] = 0f;
                if (b.HitCooldown0 > 0)
                    b.HitCooldown0--;
                if (b.HitCooldown1 > 0)
                    b.HitCooldown1--;
                if (b.StrikeCooldown0 > 0)
                    b.StrikeCooldown0--;
                if (b.StrikeCooldown1 > 0)
                    b.StrikeCooldown1--;
                anyAwake |= !Asleep(b, c);
            }
            if (!anyAwake)
                return;
            WakeLinked(ref props, arena);
            for (int s = 0; s < substeps; s++)
            {
                for (int i = 0; i < props.Count; i++)
                {
                    ref var b = ref PropState.Get(ref props, i);
                    if (Asleep(b, c))
                        continue;
                    b.Velocity.Y -= c.Gravity * h;
                    b.Velocity *= 1f / (1f + h * c.LinearDamping);
                    b.AngularVelocity *= 1f / (1f + h * c.AngularDamping);
                    ApplyPivotSpring(ref b, arena, c, i, h);
                }
                FindContacts(ref props, arena, c, scratch, h);
                for (int it = 0; it < c.Iterations; it++)
                {
                    for (int i = 0; i < props.Count; i++)
                    {
                        if (!Asleep(PropState.At(in props, i), c))
                            SolveJoint(ref props, arena, c, i, h);
                    }
                    SolveContacts(ref props, arena, c, scratch);
                }
                for (int k = 0; k < scratch.ContactCount; k++)
                {
                    ref readonly var contact = ref scratch.Contacts[k];
                    scratch.TickImpulse[contact.Body] += contact.NormalImpulse;
                    if (contact.Other >= 0)
                        scratch.TickImpulse[contact.Other] += contact.NormalImpulse;
                }
                for (int i = 0; i < props.Count; i++)
                {
                    ref var b = ref PropState.Get(ref props, i);
                    if (Asleep(b, c))
                        continue;
                    b.Position += b.Velocity * h;
                    b.Rotation = IntegrateRotation(b.Rotation, b.AngularVelocity * h);
                }
            }
            for (int i = 0; i < props.Count; i++)
            {
                ref var b = ref PropState.Get(ref props, i);
                if (Asleep(b, c))
                    continue;
                bool slow = b.Velocity.LengthSquared < c.SleepSpeed * c.SleepSpeed && b.AngularVelocity * b.AngularVelocity < c.SleepAngularSpeed * c.SleepAngularSpeed;
                b.SleepTicks = slow ? b.SleepTicks + 1 : 0;
                if (Asleep(b, c))
                {
                    b.Velocity = Vec2.Zero;
                    b.AngularVelocity = 0f;
                }
            }
            CheckBreaks(ref props, arena, scratch, tick, events);
        }

        /// <summary>이번 틱에 받은 바깥 충격이 기준 이상이면 연결을 끊는다(끊어진 몸은 깨어 자유롭게 떨어진다).</summary>
        public static void CheckBreaks(ref PropState props, ArenaDef arena, PropScratch scratch, int tick, SimEventBuffer events)
        {
            for (int i = 0; i < props.Count; i++)
            {
                ref readonly var joint = ref arena.GetJoint(i);
                ref var b = ref PropState.Get(ref props, i);
                if (b.Broken != 0 || joint.BreakImpulse <= 0f || scratch.TickImpulse[i] < joint.BreakImpulse)
                    continue;
                b.Broken = 1;
                b.SleepTicks = 0;
                var at = joint.OtherBody >= 0
                    ? PropState.At(in props, joint.OtherBody).ToWorld(joint.OtherLocal)
                    : joint.Pinned ? joint.WorldAnchor : b.ToWorld(joint.LocalAnchor);
                events.Add(new SimEvent(tick, SimEventType.JointBreak, b.LastToucher >= 0 ? b.LastToucher : SimEvent.NoFighter, at, Vec2.Zero, scratch.TickImpulse[i]));
            }
        }

        /// <summary>q + h·ω·(−s, c)를 정규화(삼각함수 없음).</summary>
        public static Vec2 IntegrateRotation(Vec2 q, float angle)
        {
            var r = new Vec2(q.X - angle * q.Y, q.Y + angle * q.X);
            float length = SimMath.Sqrt(r.LengthSquared);
            return length > 1e-6f ? r / length : new Vec2(1f, 0f);
        }

        // 연결 ----------------------------------------------------------------------

        /// <summary>
        /// 경첩·시소의 복원: 토크 = −강성 × sin θ − 감쇠 × ω + 처음 자세의 중력 토크를 상쇄하는 토크
        /// (비어 있으면 수평을 유지하고, 밟으면 기울었다가 돌아온다). 처음 각도는 0이므로 sin θ = 회전의 Y.
        /// </summary>
        static void ApplyPivotSpring(ref PropBody b, ArenaDef arena, in PropParams c, int i, float h)
        {
            ref readonly var joint = ref arena.GetJoint(i);
            if (b.Broken != 0 || (joint.Kind != PropJointKind.Hinge && joint.Kind != PropJointKind.Seesaw))
                return;
            ref readonly var def = ref arena.GetProp(arena.DynamicProp(i));
            float m = Mass(def, c);
            float stiffness = joint.Kind == PropJointKind.Hinge ? c.HingeStiffness : c.SeesawStiffness;
            // 축 → 무게중심(처음 자세). 중력 토크 = r × (0, −mg) = −r.x·m·g, 이를 상쇄.
            float compensation = -joint.LocalAnchor.X * m * c.Gravity;
            float torque = compensation - stiffness * b.Rotation.Y - c.PivotDamping * b.AngularVelocity * m;
            b.AngularVelocity += torque / Inertia(def, m) * h;
        }

        static void SolveJoint(ref PropState props, ArenaDef arena, in PropParams c, int i, float h)
        {
            ref readonly var joint = ref arena.GetJoint(i);
            if (joint.Kind == PropJointKind.None)
                return;
            ref var b = ref PropState.Get(ref props, i);
            if (b.Broken != 0)
                return;
            InverseMass(arena, c, i, out float invM, out float invI);
            var r = PropBody.Rotate(b.Rotation, joint.LocalAnchor);
            var p = b.Position + r;
            if (joint.Pinned)
            {
                // 점 고정: 몸의 축 위치 속도를 0으로, 위치 오차는 일부만 속도로 되돌린다.
                var error = p - joint.WorldAnchor;
                var cdot = b.Velocity + PropBody.Cross(b.AngularVelocity, r) + error * (c.Correction / h);
                float k11 = invM + invI * r.Y * r.Y, k12 = -invI * r.X * r.Y, k22 = invM + invI * r.X * r.X;
                float det = k11 * k22 - k12 * k12;
                if (det > 1e-9f)
                {
                    float invDet = 1f / det;
                    var impulse = new Vec2(-(k22 * cdot.X - k12 * cdot.Y) * invDet, -(-k12 * cdot.X + k11 * cdot.Y) * invDet);
                    b.Velocity += impulse * invM;
                    b.AngularVelocity += invI * PropBody.Cross(r, impulse);
                }
                // 각도 한계(처음 자세 기준, sin θ로 비교). 넘어가는 쪽 각속도를 막고 넘은 만큼 되돌린다.
                float limit = joint.Kind == PropJointKind.Hinge ? c.HingeLimitSin : joint.Kind == PropJointKind.Seesaw ? c.SeesawLimitSin : 0f;
                if (limit > 0f && b.Rotation.X > 0f)
                {
                    float sin = b.Rotation.Y;
                    if (sin > limit)
                        b.AngularVelocity = SimMath.Min(b.AngularVelocity, -(sin - limit) / b.Rotation.X * (c.Correction / h));
                    else if (sin < -limit)
                        b.AngularVelocity = SimMath.Max(b.AngularVelocity, (-limit - sin) / b.Rotation.X * (c.Correction / h));
                }
                return;
            }
            // 줄: 최대 거리. 팽팽할 때만 안쪽으로 당긴다. 다른 강체에 이어졌으면 두 몸에 반대로.
            bool linked = joint.OtherBody >= 0;
            var anchor = joint.WorldAnchor;
            var rOther = Vec2.Zero;
            float invMO = 0f, invIO = 0f;
            var vOther = Vec2.Zero;
            if (linked)
            {
                ref readonly var o = ref PropState.At(in props, joint.OtherBody);
                rOther = PropBody.Rotate(o.Rotation, joint.OtherLocal);
                anchor = o.Position + rOther;
                vOther = o.PointVelocity(anchor);
                InverseMass(arena, c, joint.OtherBody, out invMO, out invIO);
            }
            var d = p - anchor;
            float length = SimMath.Sqrt(d.LengthSquared);
            if (length < joint.Length - 1e-4f || length < 1e-6f)
                return;
            var n = d / length;
            float rn = PropBody.Cross(r, n);
            float ro = PropBody.Cross(rOther, n);
            float kn = invM + invI * rn * rn + invMO + invIO * ro * ro;
            float vn = Vec2.Dot(n, b.Velocity + PropBody.Cross(b.AngularVelocity, r) - vOther);
            float lambda = -(vn + (length - joint.Length) * (c.Correction / h)) / kn;
            if (lambda >= 0f)
                return;
            var pull = n * lambda;
            b.Velocity += pull * invM;
            b.AngularVelocity += invI * PropBody.Cross(r, pull);
            if (linked)
            {
                ref var o = ref PropState.Get(ref props, joint.OtherBody);
                o.Velocity -= pull * invMO;
                o.AngularVelocity -= invIO * PropBody.Cross(rOther, pull);
            }
        }

        /// <summary>두 몸이 줄로 바로 이어졌는지(이어진 부품끼리는 충돌하지 않는다).</summary>
        static bool Connected(ArenaDef arena, in PropState props, int a, int b) =>
            (arena.GetJoint(a).OtherBody == b && PropState.At(in props, a).Broken == 0)
            || (arena.GetJoint(b).OtherBody == a && PropState.At(in props, b).Broken == 0);

        // 접촉 ----------------------------------------------------------------------

        static void FindContacts(ref PropState props, ArenaDef arena, in PropParams c, PropScratch scratch, float h)
        {
            scratch.ContactCount = 0;
            for (int i = 0; i < props.Count; i++)
            {
                ref readonly var b = ref PropState.At(in props, i);
                if (Asleep(b, c))
                    continue;
                ref readonly var def = ref arena.GetProp(arena.DynamicProp(i));
                var half = def.HalfSize;
                Extent(b, half, out float ex, out float ey);
                float margin = 0.05f;
                for (int k = 0; k < arena.BoxCount; k++)
                {
                    ref readonly var box = ref arena.GetBox(k);
                    if (box.Kind == TerrainKind.Spike)
                        continue;
                    if (b.Position.X + ex + margin < box.MinX || b.Position.X - ex - margin > box.MaxX
                        || b.Position.Y + ey + margin < box.MinY || b.Position.Y - ey - margin > box.MaxY)
                        continue;
                    // 단방향 발판은 위에서 내려올 때만 막는다.
                    if (box.Kind == TerrainKind.OneWay && b.Position.Y - ey < box.MaxY - 0.25f)
                        continue;
                    CollideBox(i, b, half, box, c.Restitution, scratch);
                }
                if (arena.HasBoundary)
                    CollideBoundary(i, b, half, arena, c.BoundaryRestitution, scratch);
            }
            // 강체끼리(2단계). 깨어 있는 몸이 잠든 몸에 닿으면 깨운다.
            for (int i = 0; i < props.Count; i++)
            {
                for (int j = i + 1; j < props.Count; j++)
                {
                    ref var a = ref PropState.Get(ref props, i);
                    ref var bb = ref PropState.Get(ref props, j);
                    bool asleepA = Asleep(a, c), asleepB = Asleep(bb, c);
                    if ((asleepA && asleepB) || Connected(arena, props, i, j))
                        continue;
                    var halfA = arena.GetProp(arena.DynamicProp(i)).HalfSize;
                    var halfB = arena.GetProp(arena.DynamicProp(j)).HalfSize;
                    Extent(a, halfA, out float ax, out float ay);
                    Extent(bb, halfB, out float bx, out float by);
                    if (SimMath.Abs(a.Position.X - bb.Position.X) > ax + bx + 0.05f || SimMath.Abs(a.Position.Y - bb.Position.Y) > ay + by + 0.05f)
                        continue;
                    int before = scratch.ContactCount;
                    CollideBodies(i, a, halfA, j, bb, halfB, c.Restitution, scratch);
                    if (scratch.ContactCount > before)
                    {
                        a.SleepTicks = 0;
                        bb.SleepTicks = 0;
                    }
                }
            }
            for (int k = 0; k < scratch.ContactCount; k++)
            {
                ref var contact = ref scratch.Contacts[k];
                ref readonly var b = ref PropState.At(in props, contact.Body);
                var v = b.PointVelocity(contact.Point);
                if (contact.Other >= 0)
                    v -= PropState.At(in props, contact.Other).PointVelocity(contact.Point);
                float vn = Vec2.Dot(contact.Normal, v);
                float bounce = vn < -1f ? -contact.Restitution * vn : 0f;
                float push = SimMath.Max(0f, contact.Depth - c.Slop) * (c.Correction / h);
                contact.TargetSpeed = SimMath.Max(bounce, push);
            }
        }

        static void Extent(in PropBody b, Vec2 half, out float ex, out float ey)
        {
            var u = b.Rotation;
            ex = SimMath.Abs(u.X) * half.X + SimMath.Abs(u.Y) * half.Y;
            ey = SimMath.Abs(u.Y) * half.X + SimMath.Abs(u.X) * half.Y;
        }

        static void Add(PropScratch scratch, int body, int other, Vec2 normal, Vec2 point, float depth, float restitution)
        {
            if (scratch.ContactCount >= PropScratch.MaxContacts)
                return;
            scratch.Contacts[scratch.ContactCount++] = new PropContact
            {
                Body = body, Other = other, Normal = normal, Point = point, Depth = depth, Restitution = restitution,
            };
        }

        static void Corner(in PropBody b, Vec2 half, int k, out Vec2 corner)
        {
            var local = new Vec2((k & 1) == 0 ? -half.X : half.X, (k & 2) == 0 ? -half.Y : half.Y);
            corner = b.ToWorld(local);
        }

        /// <summary>회전한 직사각형(몸)과 축 정렬 상자: 분리축으로 가장 얕은 축을 찾고, 그 면에 들어간 꼭짓점(최대 2개)을 접촉으로.</summary>
        static void CollideBox(int body, in PropBody b, Vec2 half, in ArenaBox box, float restitution, PropScratch scratch)
        {
            var boxBody = new PropBody
            {
                Position = new Vec2((box.MinX + box.MaxX) * 0.5f, (box.MinY + box.MaxY) * 0.5f),
                Rotation = new Vec2(1f, 0f),
            };
            var boxHalf = new Vec2((box.MaxX - box.MinX) * 0.5f, (box.MaxY - box.MinY) * 0.5f);
            Collide(body, b, half, -1, boxBody, boxHalf, restitution, scratch);
        }

        static void CollideBodies(int i, in PropBody a, Vec2 halfA, int j, in PropBody b, Vec2 halfB, float restitution, PropScratch scratch) =>
            Collide(i, a, halfA, j, b, halfB, restitution, scratch);

        /// <summary>
        /// 회전한 직사각형 두 개(Body, 상대). 분리축 4개 중 가장 얕은 축의 면을 기준 면으로, 다른 쪽 꼭짓점 중 그 면에 들어간 것(최대 2개)을 접촉점으로.
        /// 법선은 상대 → Body. 같은 깊이면 상대 쪽 축을 고른다(평평한 지형 위에서 흔들림이 적다).
        /// </summary>
        static void Collide(int body, in PropBody b, Vec2 half, int other, in PropBody o, Vec2 otherHalf, float restitution, PropScratch scratch)
        {
            var u = b.Rotation;
            var v = new Vec2(-u.Y, u.X);
            var ou = o.Rotation;
            var ov = new Vec2(-ou.Y, ou.X);
            var delta = b.Position - o.Position;
            float best = float.MaxValue;
            var normal = Vec2.Zero;
            bool bodyFace = false;
            for (int axis = 0; axis < 4; axis++)
            {
                var a = axis == 0 ? ou : axis == 1 ? ov : axis == 2 ? u : v;
                float rBody = half.X * SimMath.Abs(Vec2.Dot(a, u)) + half.Y * SimMath.Abs(Vec2.Dot(a, v));
                float rOther = otherHalf.X * SimMath.Abs(Vec2.Dot(a, ou)) + otherHalf.Y * SimMath.Abs(Vec2.Dot(a, ov));
                float d = Vec2.Dot(delta, a);
                float overlap = rBody + rOther - SimMath.Abs(d);
                if (overlap <= 0f)
                    return;
                if (overlap < best - 1e-5f)
                {
                    best = overlap;
                    normal = d >= 0f ? a : -a;
                    bodyFace = axis >= 2;
                }
            }
            var tangent = new Vec2(-normal.Y, normal.X);
            int added = 0;
            if (!bodyFace)
            {
                // 상대의 면에 몸의 꼭짓점이 들어감.
                float face = Vec2.Dot(o.Position, normal) + otherHalf.X * SimMath.Abs(Vec2.Dot(normal, ou)) + otherHalf.Y * SimMath.Abs(Vec2.Dot(normal, ov));
                float lateral = otherHalf.X * SimMath.Abs(Vec2.Dot(tangent, ou)) + otherHalf.Y * SimMath.Abs(Vec2.Dot(tangent, ov)) + 0.05f;
                for (int k = 0; k < 4 && added < 2; k++)
                {
                    Corner(b, half, k, out var corner);
                    float depth = face - Vec2.Dot(corner, normal);
                    if (depth > 0f && SimMath.Abs(Vec2.Dot(corner - o.Position, tangent)) <= lateral)
                    {
                        Add(scratch, body, other, normal, corner, depth, restitution);
                        added++;
                    }
                }
            }
            else
            {
                // 몸의 면에 상대의 꼭짓점이 들어감.
                float face = Vec2.Dot(b.Position, normal) - (half.X * SimMath.Abs(Vec2.Dot(normal, u)) + half.Y * SimMath.Abs(Vec2.Dot(normal, v)));
                float lateral = half.X * SimMath.Abs(Vec2.Dot(tangent, u)) + half.Y * SimMath.Abs(Vec2.Dot(tangent, v)) + 0.05f;
                for (int k = 0; k < 4 && added < 2; k++)
                {
                    Corner(o, otherHalf, k, out var corner);
                    float depth = Vec2.Dot(corner, normal) - face;
                    if (depth > 0f && SimMath.Abs(Vec2.Dot(corner - b.Position, tangent)) <= lateral)
                    {
                        Add(scratch, body, other, normal, corner, depth, restitution);
                        added++;
                    }
                }
            }
            if (added == 0)
                Add(scratch, body, other, normal, b.Position - normal * (best * 0.5f), best, restitution);
        }

        static void CollideBoundary(int body, in PropBody b, Vec2 half, ArenaDef arena, float restitution, PropScratch scratch)
        {
            float bx = arena.BoundaryHalfWidth, by = arena.BoundaryHalfHeight;
            for (int k = 0; k < 4; k++)
            {
                Corner(b, half, k, out var corner);
                if (corner.X > bx)
                    Add(scratch, body, -1, new Vec2(-1f, 0f), corner, corner.X - bx, restitution);
                else if (corner.X < -bx)
                    Add(scratch, body, -1, new Vec2(1f, 0f), corner, -bx - corner.X, restitution);
                if (corner.Y > by)
                    Add(scratch, body, -1, new Vec2(0f, -1f), corner, corner.Y - by, restitution);
                else if (corner.Y < -by)
                    Add(scratch, body, -1, new Vec2(0f, 1f), corner, -by - corner.Y, restitution);
            }
        }

        static void SolveContacts(ref PropState props, ArenaDef arena, in PropParams c, PropScratch scratch)
        {
            for (int k = 0; k < scratch.ContactCount; k++)
            {
                ref var contact = ref scratch.Contacts[k];
                ref var b = ref PropState.Get(ref props, contact.Body);
                InverseMass(arena, c, contact.Body, out float invM, out float invI);
                float invMO = 0f, invIO = 0f;
                var rO = Vec2.Zero;
                bool two = contact.Other >= 0;
                if (two)
                {
                    InverseMass(arena, c, contact.Other, out invMO, out invIO);
                    rO = contact.Point - PropState.At(in props, contact.Other).Position;
                }
                var r = contact.Point - b.Position;
                var n = contact.Normal;
                float rn = PropBody.Cross(r, n), ron = PropBody.Cross(rO, n);
                float kn = invM + invI * rn * rn + invMO + invIO * ron * ron;
                float vn = Vec2.Dot(n, RelativeVelocity(props, contact));
                float lambda = (contact.TargetSpeed - vn) / kn;
                float total = SimMath.Max(contact.NormalImpulse + lambda, 0f);
                lambda = total - contact.NormalImpulse;
                contact.NormalImpulse = total;
                Apply(ref props, contact, n * lambda, invM, invI, invMO, invIO, r, rO);

                var t = new Vec2(-n.Y, n.X);
                float rt = PropBody.Cross(r, t), rot = PropBody.Cross(rO, t);
                float kt = invM + invI * rt * rt + invMO + invIO * rot * rot;
                float vt = Vec2.Dot(t, RelativeVelocity(props, contact));
                float friction = c.Friction * contact.NormalImpulse;
                float lambdaT = -vt / kt;
                float totalT = SimMath.Clamp(contact.TangentImpulse + lambdaT, -friction, friction);
                lambdaT = totalT - contact.TangentImpulse;
                contact.TangentImpulse = totalT;
                Apply(ref props, contact, t * lambdaT, invM, invI, invMO, invIO, r, rO);
            }
        }

        static Vec2 RelativeVelocity(in PropState props, in PropContact contact)
        {
            var v = PropState.At(in props, contact.Body).PointVelocity(contact.Point);
            if (contact.Other >= 0)
                v -= PropState.At(in props, contact.Other).PointVelocity(contact.Point);
            return v;
        }

        static void Apply(ref PropState props, in PropContact contact, Vec2 impulse, float invM, float invI, float invMO, float invIO, Vec2 r, Vec2 rO)
        {
            ref var b = ref PropState.Get(ref props, contact.Body);
            b.Velocity += impulse * invM;
            b.AngularVelocity += invI * PropBody.Cross(r, impulse);
            if (contact.Other < 0)
                return;
            ref var o = ref PropState.Get(ref props, contact.Other);
            o.Velocity -= impulse * invMO;
            o.AngularVelocity -= invIO * PropBody.Cross(rO, impulse);
        }
    }
}

namespace Knockbox.Simulation
{
    /// <summary>접촉 하나(한 틱의 계산 결과, 상태 아님).</summary>
    public struct PropContact
    {
        public int Body;
        /// <summary>몸을 밀어내는 방향(지형·경계 → 몸).</summary>
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
        public const int MaxContacts = 256;
        public readonly PropContact[] Contacts = new PropContact[MaxContacts];
        public int ContactCount;
    }

    /// <summary>
    /// 구조물 물리 1단계(09-props-physics.md 2절, CP14): 회전하는 직사각형 강체, 줄(최대 거리), 축(점 고정),
    /// 고정 지형·임시 물체·화면 경계와의 충돌. 서브스텝마다 접촉을 찾고, 연결과 접촉을 같은 반복에서 순차 충격량으로 푼다
    /// (Box2D v3의 접근을 참고, 코드는 직접 작성). 회전은 (cos, sin)으로 적분해 정규화한다. 강체끼리의 충돌은 2단계(CP15).
    /// 몸 번호 순서대로 처리하고 반복 횟수가 고정이라 결정론적이다.
    /// </summary>
    public static class PropPhysics
    {
        /// <summary>지금 강체로 움직이는 구조물 단계(09 2절 표). 이 단계 이하의 종류만 SimState.Props에 들어간다.</summary>
        public const int ImplementedStage = 1;

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

        /// <summary>한 틱. 히트스탑과 상관없이 계속 움직인다(09 4절 초기 결정).</summary>
        public static void Step(ref PropState props, ArenaDef arena, SimConfig config, PropScratch scratch)
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
        }

        /// <summary>q + h·ω·(−s, c)를 정규화(삼각함수 없음).</summary>
        public static Vec2 IntegrateRotation(Vec2 q, float angle)
        {
            var r = new Vec2(q.X - angle * q.Y, q.Y + angle * q.X);
            float length = SimMath.Sqrt(r.LengthSquared);
            return length > 1e-6f ? r / length : new Vec2(1f, 0f);
        }

        // 연결 ----------------------------------------------------------------------

        static void SolveJoint(ref PropState props, ArenaDef arena, in PropParams c, int i, float h)
        {
            ref readonly var joint = ref arena.GetJoint(i);
            if (joint.Kind == PropJointKind.None)
                return;
            ref var b = ref PropState.Get(ref props, i);
            ref readonly var def = ref arena.GetProp(arena.DynamicProp(i));
            float m = Mass(def, c), invM = 1f / m, invI = 1f / Inertia(def, m);
            var r = PropBody.Rotate(b.Rotation, joint.LocalAnchor);
            var p = b.Position + r;
            if (joint.Kind == PropJointKind.Axis)
            {
                // 점 고정: 몸의 축 위치 속도를 0으로, 위치 오차는 일부만 속도로 되돌린다.
                var error = p - joint.WorldAnchor;
                var cdot = b.Velocity + PropBody.Cross(b.AngularVelocity, r) + error * (c.Correction / h);
                float k11 = invM + invI * r.Y * r.Y, k12 = -invI * r.X * r.Y, k22 = invM + invI * r.X * r.X;
                float det = k11 * k22 - k12 * k12;
                if (det <= 1e-9f)
                    return;
                float invDet = 1f / det;
                var impulse = new Vec2(-(k22 * cdot.X - k12 * cdot.Y) * invDet, -(-k12 * cdot.X + k11 * cdot.Y) * invDet);
                b.Velocity += impulse * invM;
                b.AngularVelocity += invI * PropBody.Cross(r, impulse);
                return;
            }
            // 줄: 최대 거리. 팽팽할 때만 안쪽으로 당긴다.
            var d = p - joint.WorldAnchor;
            float length = SimMath.Sqrt(d.LengthSquared);
            if (length < joint.Length - 1e-4f || length < 1e-6f)
                return;
            var n = d / length;
            float rn = PropBody.Cross(r, n);
            float kn = invM + invI * rn * rn;
            float vn = Vec2.Dot(n, b.Velocity + PropBody.Cross(b.AngularVelocity, r));
            float lambda = -(vn + (length - joint.Length) * (c.Correction / h)) / kn;
            if (lambda >= 0f)
                return;
            var pull = n * lambda;
            b.Velocity += pull * invM;
            b.AngularVelocity += invI * PropBody.Cross(r, pull);
        }

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
                var u = b.Rotation;
                var v = new Vec2(-u.Y, u.X);
                float ex = SimMath.Abs(u.X) * half.X + SimMath.Abs(v.X) * half.Y;
                float ey = SimMath.Abs(u.Y) * half.X + SimMath.Abs(v.Y) * half.Y;
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
            for (int k = 0; k < scratch.ContactCount; k++)
            {
                ref var contact = ref scratch.Contacts[k];
                ref readonly var b = ref PropState.At(in props, contact.Body);
                float vn = Vec2.Dot(contact.Normal, b.PointVelocity(contact.Point));
                float bounce = vn < -1f ? -contact.Restitution * vn : 0f;
                float push = SimMath.Max(0f, contact.Depth - c.Slop) * (c.Correction / h);
                contact.TargetSpeed = SimMath.Max(bounce, push);
            }
        }

        static void Add(PropScratch scratch, int body, Vec2 normal, Vec2 point, float depth, float restitution)
        {
            if (scratch.ContactCount >= PropScratch.MaxContacts)
                return;
            scratch.Contacts[scratch.ContactCount++] = new PropContact { Body = body, Normal = normal, Point = point, Depth = depth, Restitution = restitution };
        }

        static void Corner(in PropBody b, Vec2 half, int k, out Vec2 corner)
        {
            var local = new Vec2((k & 1) == 0 ? -half.X : half.X, (k & 2) == 0 ? -half.Y : half.Y);
            corner = b.ToWorld(local);
        }

        /// <summary>회전한 직사각형(몸)과 축 정렬 상자: 분리축으로 가장 얕은 축을 찾고, 그 면에 들어간 꼭짓점(최대 2개)을 접촉으로.</summary>
        static void CollideBox(int body, in PropBody b, Vec2 half, in ArenaBox box, float restitution, PropScratch scratch)
        {
            var u = b.Rotation;
            var v = new Vec2(-u.Y, u.X);
            var boxCenter = new Vec2((box.MinX + box.MaxX) * 0.5f, (box.MinY + box.MaxY) * 0.5f);
            var boxHalf = new Vec2((box.MaxX - box.MinX) * 0.5f, (box.MaxY - box.MinY) * 0.5f);
            var delta = b.Position - boxCenter;
            float best = float.MaxValue;
            var normal = Vec2.Zero;
            bool bodyFace = false;
            for (int axis = 0; axis < 4; axis++)
            {
                var a = axis == 0 ? new Vec2(1f, 0f) : axis == 1 ? new Vec2(0f, 1f) : axis == 2 ? u : v;
                float rBody = half.X * SimMath.Abs(Vec2.Dot(a, u)) + half.Y * SimMath.Abs(Vec2.Dot(a, v));
                float rBox = boxHalf.X * SimMath.Abs(a.X) + boxHalf.Y * SimMath.Abs(a.Y);
                float d = Vec2.Dot(delta, a);
                float overlap = rBody + rBox - SimMath.Abs(d);
                if (overlap <= 0f)
                    return;
                // 같은 깊이면 상자 쪽 축을 고른다(평평한 지형 위에서 흔들림이 적다).
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
                // 상자의 면에 몸의 꼭짓점이 들어감.
                float face = Vec2.Dot(boxCenter, normal) + boxHalf.X * SimMath.Abs(normal.X) + boxHalf.Y * SimMath.Abs(normal.Y);
                float lateral = boxHalf.X * SimMath.Abs(tangent.X) + boxHalf.Y * SimMath.Abs(tangent.Y) + 0.05f;
                for (int k = 0; k < 4 && added < 2; k++)
                {
                    Corner(b, half, k, out var corner);
                    float depth = face - Vec2.Dot(corner, normal);
                    if (depth > 0f && SimMath.Abs(Vec2.Dot(corner - boxCenter, tangent)) <= lateral)
                    {
                        Add(scratch, body, normal, corner, depth, restitution);
                        added++;
                    }
                }
            }
            else
            {
                // 몸의 면에 상자의 꼭짓점이 들어감.
                float face = Vec2.Dot(b.Position, normal) - (half.X * SimMath.Abs(Vec2.Dot(normal, u)) + half.Y * SimMath.Abs(Vec2.Dot(normal, v)));
                float lateral = half.X * SimMath.Abs(Vec2.Dot(tangent, u)) + half.Y * SimMath.Abs(Vec2.Dot(tangent, v)) + 0.05f;
                for (int k = 0; k < 4 && added < 2; k++)
                {
                    var corner = new Vec2((k & 1) == 0 ? box.MinX : box.MaxX, (k & 2) == 0 ? box.MinY : box.MaxY);
                    float depth = Vec2.Dot(corner, normal) - face;
                    if (depth > 0f && SimMath.Abs(Vec2.Dot(corner - b.Position, tangent)) <= lateral)
                    {
                        Add(scratch, body, normal, corner, depth, restitution);
                        added++;
                    }
                }
            }
            if (added == 0)
                Add(scratch, body, normal, b.Position - normal * (best * 0.5f), best, restitution);
        }

        static void CollideBoundary(int body, in PropBody b, Vec2 half, ArenaDef arena, float restitution, PropScratch scratch)
        {
            float bx = arena.BoundaryHalfWidth, by = arena.BoundaryHalfHeight;
            for (int k = 0; k < 4; k++)
            {
                Corner(b, half, k, out var corner);
                if (corner.X > bx)
                    Add(scratch, body, new Vec2(-1f, 0f), corner, corner.X - bx, restitution);
                else if (corner.X < -bx)
                    Add(scratch, body, new Vec2(1f, 0f), corner, -bx - corner.X, restitution);
                if (corner.Y > by)
                    Add(scratch, body, new Vec2(0f, -1f), corner, corner.Y - by, restitution);
                else if (corner.Y < -by)
                    Add(scratch, body, new Vec2(0f, 1f), corner, -by - corner.Y, restitution);
            }
        }

        static void SolveContacts(ref PropState props, ArenaDef arena, in PropParams c, PropScratch scratch)
        {
            for (int k = 0; k < scratch.ContactCount; k++)
            {
                ref var contact = ref scratch.Contacts[k];
                ref var b = ref PropState.Get(ref props, contact.Body);
                ref readonly var def = ref arena.GetProp(arena.DynamicProp(contact.Body));
                float m = Mass(def, c), invM = 1f / m, invI = 1f / Inertia(def, m);
                var r = contact.Point - b.Position;
                var n = contact.Normal;
                float rn = PropBody.Cross(r, n);
                float kn = invM + invI * rn * rn;
                float vn = Vec2.Dot(n, b.PointVelocity(contact.Point));
                float lambda = (contact.TargetSpeed - vn) / kn;
                float total = SimMath.Max(contact.NormalImpulse + lambda, 0f);
                lambda = total - contact.NormalImpulse;
                contact.NormalImpulse = total;
                var impulse = n * lambda;
                b.Velocity += impulse * invM;
                b.AngularVelocity += invI * PropBody.Cross(r, impulse);

                var t = new Vec2(-n.Y, n.X);
                float rt = PropBody.Cross(r, t);
                float kt = invM + invI * rt * rt;
                float vt = Vec2.Dot(t, b.PointVelocity(contact.Point));
                float friction = c.Friction * contact.NormalImpulse;
                float lambdaT = -vt / kt;
                float totalT = SimMath.Clamp(contact.TangentImpulse + lambdaT, -friction, friction);
                lambdaT = totalT - contact.TangentImpulse;
                contact.TangentImpulse = totalT;
                var impulseT = t * lambdaT;
                b.Velocity += impulseT * invM;
                b.AngularVelocity += invI * PropBody.Cross(r, impulseT);
            }
        }
    }
}

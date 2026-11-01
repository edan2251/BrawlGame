namespace Knockbox.Simulation
{
    /// <summary>ProjectileEnd 이벤트의 Strength.</summary>
    public enum ProjectileEndReason
    {
        Terrain = 0,
        Prop = 1,
        Boundary = 2,
        Expired = 3,
        /// <summary>풀이 가득 차 가장 오래된 것을 지움(S-151).</summary>
        Replaced = 4,
        Fighter = 5,
    }

    /// <summary>
    /// 투사체(10-forms-and-growth.md 4.3, CP19). 한 틱의 이동을 선분으로 보고 지형·경계·구조물·파이터와의 첫 교차를 찾는다(터널링 없음).
    /// 파이터와는 그 파이터의 이번 틱 움직임을 뺀 상대 선분으로 잰다. 지형·경계는 항상 막고, 구조물·상자는 PierceProp이 아니면 막는다.
    /// 구조물에 충격량을 전달하지 않는다.
    /// </summary>
    public static class Projectiles
    {
        const float Dt = SimConfig.Dt;

        /// <summary>이동이 끝난 파이터의 발사 요청을 풀에 넣는다. 파이터 번호 순서지만 칸이 파이터마다 나뉘어 서로 영향이 없다.</summary>
        public static void SpawnPending(ref SimState s, SimConfig config, int tick, SimEventBuffer events)
        {
            SpawnPending(ref s.Fighter0, 0, ref s.Projectiles, config, tick, events);
            SpawnPending(ref s.Fighter1, 1, ref s.Projectiles, config, tick, events);
        }

        static void SpawnPending(ref FighterState f, int owner, ref ProjectileState pool, SimConfig config, int tick, SimEventBuffer events)
        {
            if (!f.ShotPending)
                return;
            f.ShotPending = false;
            ref readonly var rock = ref config.Combat.Rock;
            int first = owner * ProjectileState.PerFighter;
            int slot = -1;
            int oldest = -1;
            for (int i = first; i < first + ProjectileState.PerFighter; i++)
            {
                ref readonly var p = ref ProjectileState.At(pool, i);
                if (!p.Active)
                {
                    slot = i;
                    break;
                }
                if (oldest < 0 || p.SpawnTick < ProjectileState.At(pool, oldest).SpawnTick)
                    oldest = i;
            }
            if (slot < 0)
            {
                // 풀이 가득 차면 가장 오래된 자기 투사체를 지우고 그 칸에 쏜다(S-151).
                slot = oldest;
                End(ref ProjectileState.Get(ref pool, slot), ProjectileEndReason.Replaced, tick, events);
            }
            var dir = f.ShotDirection;
            ref var n = ref ProjectileState.Get(ref pool, slot);
            n = new Projectile
            {
                Active = true,
                Owner = owner,
                Position = f.Position + dir * rock.SpawnOffset,
                Velocity = dir * rock.Speed,
                Reflectable = rock.Reflectable,
                PierceFighter = rock.PierceFighter,
                PierceProp = rock.PierceProp,
                SpawnTick = tick,
            };
            events.Add(new SimEvent(tick, SimEventType.ProjectileFire, owner, n.Position, dir, rock.Speed));
        }

        static void End(ref Projectile p, ProjectileEndReason reason, int tick, SimEventBuffer events)
        {
            events.Add(new SimEvent(tick, SimEventType.ProjectileEnd, p.Owner, p.Position, SimMath.Normalize(p.Velocity), (int)reason));
            p = default;
        }

        /// <summary>
        /// 모든 투사체를 한 틱 움직인다. prev0·prev1은 이번 틱 이동 전의 파이터 상태(상대 선분용).
        /// </summary>
        public static void Step(ref SimState s, ArenaDef arena, SimConfig config, in FighterState prev0, in FighterState prev1, int tick, SimEventBuffer events)
        {
            for (int i = 0; i < ProjectileState.Capacity; i++)
            {
                ref var p = ref ProjectileState.Get(ref s.Projectiles, i);
                if (p.Active)
                    StepOne(ref p, ref s, arena, config, prev0, prev1, tick, events);
            }
        }

        static void StepOne(ref Projectile p, ref SimState s, ArenaDef arena, SimConfig config, in FighterState prev0, in FighterState prev1, int tick, SimEventBuffer events)
        {
            ref readonly var rock = ref config.Combat.Rock;
            float r = rock.Radius;
            var from = p.Position;
            var delta = p.Velocity * Dt;

            float block = BlockingTime(from, delta, r, p.PierceProp, ref s, arena, out var reason);

            // 파이터: 막히기 전에 닿는 것만, 이른 순서로.
            float t0 = FighterTime(p, 0, from, delta, r, s.Fighter0, prev0, config);
            float t1 = FighterTime(p, 1, from, delta, r, s.Fighter1, prev1, config);
            for (int k = 0; k < 2; k++)
            {
                int target = (t0 <= t1) == (k == 0) ? 0 : 1;
                float t = target == 0 ? t0 : t1;
                if (t > block || t > 1f)
                    continue;
                var point = from + delta * t;
                ref var victim = ref target == 0 ? ref s.Fighter0 : ref s.Fighter1;
                var dir = SimMath.Normalize(SimMath.Normalize(p.Velocity) + new Vec2(0f, rock.KnockbackLift));
                FighterCombat.ProjectileHit(ref victim, target, rock.Damage, dir, rock.Knockback, point, config, tick, events);
                p.HitMask |= 1 << target;
                if (!p.PierceFighter)
                {
                    p.Position = point;
                    End(ref p, ProjectileEndReason.Fighter, tick, events);
                    return;
                }
            }

            if (block <= 1f)
            {
                p.Position = from + delta * block;
                End(ref p, reason, tick, events);
                return;
            }
            p.Position = from + delta;
            if (++p.Age >= rock.LifetimeTicks)
                End(ref p, ProjectileEndReason.Expired, tick, events);
        }

        /// <summary>선분이 막히는 가장 이른 비율(0~1). 없으면 2.</summary>
        static float BlockingTime(Vec2 from, Vec2 delta, float r, bool pierceProp, ref SimState s, ArenaDef arena, out ProjectileEndReason reason)
        {
            float best = 2f;
            reason = ProjectileEndReason.Terrain;
            for (int b = 0; b < arena.BoxCount; b++)
            {
                ref readonly var box = ref arena.GetBox(b);
                bool prop = box.Kind == TerrainKind.Prop;
                if (prop && pierceProp)
                    continue;
                float t = SegmentBox(from, delta, box.MinX - r, box.MinY - r, box.MaxX + r, box.MaxY + r);
                if (t < best)
                {
                    best = t;
                    reason = prop ? ProjectileEndReason.Prop : ProjectileEndReason.Terrain;
                }
            }
            if (!pierceProp)
            {
                for (int i = 0; i < s.Props.Count; i++)
                {
                    ref readonly var body = ref PropState.At(s.Props, i);
                    ref readonly var def = ref arena.GetProp(arena.DynamicProp(i));
                    // 몸의 좌표계로 옮긴 선분과 반폭 + 반지름의 상자.
                    var a = body.ToLocal(from);
                    var d = PropBody.InverseRotate(body.Rotation, delta);
                    var h = def.HalfSize;
                    float t = SegmentBox(a, d, -h.X - r, -h.Y - r, h.X + r, h.Y + r);
                    if (t < best)
                    {
                        best = t;
                        reason = ProjectileEndReason.Prop;
                    }
                }
            }
            float bt = BoundaryTime(from, delta, r, arena);
            if (bt < best)
            {
                best = bt;
                reason = ProjectileEndReason.Boundary;
            }
            return best;
        }

        /// <summary>경계(없는 맵은 경기장 가장자리) 밖으로 나가는 비율. 없으면 2.</summary>
        static float BoundaryTime(Vec2 from, Vec2 delta, float r, ArenaDef arena)
        {
            float hx = (arena.HasBoundary ? arena.BoundaryHalfWidth : arena.HalfWidth) - r;
            float hy = (arena.HasBoundary ? arena.BoundaryHalfHeight : arena.HalfHeight) - r;
            float t = 2f;
            t = SimMath.Min(t, Exit(from.X, delta.X, hx));
            t = SimMath.Min(t, Exit(from.Y, delta.Y, hy));
            return t;
        }

        static float Exit(float x, float dx, float h)
        {
            if (x > h || x < -h)
                return 0f;
            if (dx > 0f && x + dx > h)
                return (h - x) / dx;
            if (dx < 0f && x + dx < -h)
                return (-h - x) / dx;
            return 2f;
        }

        /// <summary>
        /// 파이터와 닿는 비율. 맞힐 수 없으면(자기 투사체, 이미 맞힘, KO 등) 2.
        /// 파이터의 이번 틱 움직임을 빼서 상대 선분으로 잰다.
        /// </summary>
        static float FighterTime(in Projectile p, int target, Vec2 from, Vec2 delta, float r, in FighterState f, in FighterState prev, SimConfig config)
        {
            if (p.Owner == target || (p.HitMask & (1 << target)) != 0 || !FighterCombat.CanBeHit(f, config))
                return 2f;
            float h = config.HalfBody + r;
            var start = from - prev.Position;
            var rel = delta - (f.Position - prev.Position);
            return SegmentBox(start, rel, -h, -h, h, h);
        }

        /// <summary>선분 from + delta·t(0~1)가 상자에 들어가는 가장 이른 t. 시작점이 안이면 0, 닿지 않으면 2.</summary>
        public static float SegmentBox(Vec2 from, Vec2 delta, float minX, float minY, float maxX, float maxY)
        {
            float enter = 0f, exit = 1f;
            if (!Slab(from.X, delta.X, minX, maxX, ref enter, ref exit))
                return 2f;
            if (!Slab(from.Y, delta.Y, minY, maxY, ref enter, ref exit))
                return 2f;
            return enter;
        }

        static bool Slab(float x, float dx, float min, float max, ref float enter, ref float exit)
        {
            if (dx == 0f)
                return x >= min && x <= max;
            float inv = 1f / dx;
            float a = (min - x) * inv;
            float b = (max - x) * inv;
            if (a > b)
            {
                float tmp = a;
                a = b;
                b = tmp;
            }
            enter = SimMath.Max(enter, a);
            exit = SimMath.Min(exit, b);
            return enter <= exit;
        }
    }
}

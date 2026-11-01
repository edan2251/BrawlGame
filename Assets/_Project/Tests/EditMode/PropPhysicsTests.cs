using Knockbox.Game;
using Knockbox.Simulation;
using NUnit.Framework;

namespace Knockbox.Tests.EditMode
{
    /// <summary>CP14 구조물 물리 1단계(09-props-physics.md): 결정론, 진자·에너지·줄 길이, 터널링, 캐릭터 상호작용, 대칭성.</summary>
    public class PropPhysicsTests
    {
        static SimConfig Config() =>
            new SimConfig(MovementParams.Default, MatchParams.Default, CombatParams.Default, CombatMode.Charge, true);

        /// <summary>경계 튕김 빈 맵 + 가운데 매달린 블록(줄 고정점 (0, 8), 2 × 1, 몸 가운데 (0, 3)) + 오른쪽 회전 막대(축 (8, 0), 4 × 0.6) + 왼쪽 거울 회전 막대.</summary>
        static ArenaDef Lab()
        {
            var props = new[]
            {
                new PropDef { Kind = PropKind.Hanging, Group = 0, Center = new Vec2(0f, 3f), HalfSize = new Vec2(1f, 0.5f), HasAnchor = true, Anchor = new Vec2(0f, 8f), AnchorProp = -1 },
                new PropDef { Kind = PropKind.Spinner, Group = 1, Center = new Vec2(8f, 0f), HalfSize = new Vec2(2f, 0.3f), HasAnchor = true, Anchor = new Vec2(8f, 0f), AnchorProp = -1 },
                new PropDef { Kind = PropKind.Spinner, Group = 2, Center = new Vec2(-8f, 0f), HalfSize = new Vec2(2f, 0.3f), HasAnchor = true, Anchor = new Vec2(-8f, 0f), AnchorProp = -1 },
            };
            return BoundaryTests.Open().WithProps(props);
        }

        static ref PropBody Body(ref SimState s, int i) => ref PropState.Get(ref s.Props, i);

        static SimWorld World(ArenaDef arena, Vec2 p0, Vec2 p1)
        {
            var config = Config();
            return TestWorlds.CreateAt(p0, p1, config, arena);
        }

        static void Push(SimWorld w, int body, Vec2 velocity, float angular)
        {
            var s = w.State;
            ref var b = ref Body(ref s, body);
            b.Velocity = velocity;
            b.AngularVelocity = angular;
            b.SleepTicks = 0;
            w.State = s;
        }

        static double KineticEnergy(SimWorld w, ArenaDef arena)
        {
            double e = 0;
            var c = w.Config.Combat.Props;
            for (int i = 0; i < w.State.Props.Count; i++)
            {
                var b = PropState.At(w.State.Props, i);
                var def = arena.GetProp(arena.DynamicProp(i));
                float m = PropPhysics.Mass(def, c);
                e += 0.5 * m * b.Velocity.LengthSquared + 0.5 * PropPhysics.Inertia(def, m) * b.AngularVelocity * b.AngularVelocity;
            }
            return e;
        }

        // CP14-2 ----------------------------------------------------------------------

        [Test]
        public void Pendulum_SwingsLosesEnergyAndKeepsRopeLength()
        {
            var arena = Lab();
            var w = World(arena, new Vec2(-14f, -8f), new Vec2(14f, -8f));
            Assert.AreEqual(3, w.State.Props.Count);
            Push(w, 0, new Vec2(9f, 0f), 0f);
            double first = KineticEnergy(w, arena);
            var joint = arena.GetJoint(0);
            int crossings = 0;
            float lastX = 0f, worstRope = 0f;
            for (int t = 0; t < 3600; t++)
            {
                w.Step(default, default);
                Assert.LessOrEqual(KineticEnergy(w, arena), first + 1e-3, $"틱 {t}: 운동 에너지가 처음 값을 넘음");
                var b = PropState.At(w.State.Props, 0);
                var d = b.ToWorld(joint.LocalAnchor) - joint.WorldAnchor;
                worstRope = System.Math.Max(worstRope, System.Math.Abs(SimMath.Sqrt(d.LengthSquared) - joint.Length) / joint.Length);
                if (t > 0 && System.Math.Sign(b.Position.X) != System.Math.Sign(lastX))
                    crossings++;
                lastX = b.Position.X;
                Assert.IsFalse(float.IsNaN(b.Position.X) || float.IsInfinity(b.Position.X));
            }
            TestContext.WriteLine($"[Knockbox][CP14-2] 진자: 처음 에너지 {first:F1}, 가운데 지남 {crossings}회, 줄 길이 최대 오차 {worstRope:P2}, 끝 {(PropPhysics.Asleep(PropState.At(w.State.Props, 0), w.Config.Combat.Props) ? "잠듦" : "움직임")}");
            Assert.GreaterOrEqual(crossings, 4, "진자처럼 여러 번 흔들림");
            Assert.LessOrEqual(worstRope, 0.02f, "줄 길이 오차 2% 이하");
            Assert.IsTrue(PropPhysics.Asleep(PropState.At(w.State.Props, 0), w.Config.Combat.Props), "감쇠로 멈춘 뒤 잠듦");
        }

        [Test]
        public void Spinner_TurnsAroundFixedAxisAndSlowsDown()
        {
            var arena = Lab();
            var w = World(arena, new Vec2(-14f, -8f), new Vec2(14f, -8f));
            Push(w, 1, Vec2.Zero, 6f);
            float turned = 0f, worstDrift = 0f;
            var q = PropState.At(w.State.Props, 1).Rotation;
            for (int t = 0; t < 600; t++)
            {
                w.Step(default, default);
                var b = PropState.At(w.State.Props, 1);
                var d = b.Position - new Vec2(8f, 0f);
                worstDrift = System.Math.Max(worstDrift, SimMath.Sqrt(d.LengthSquared));
                turned += System.Math.Abs(PropBody.Cross(q, b.Rotation));
                q = b.Rotation;
            }
            Assert.Less(worstDrift, 0.02f, "축에서 벗어나지 않음");
            Assert.Greater(turned, 3f, "돈다");
            Assert.Less(System.Math.Abs(PropState.At(w.State.Props, 1).AngularVelocity), 6f, "감쇠로 느려짐");
        }

        [Test]
        public void RestingProps_StayStillAndAsleep()
        {
            foreach (string name in MapV2Tests.NewMaps)
            {
                var arena = ArenaFiles.LoadByName(name);
                var w = new SimWorld(Config(), arena, SimState.CreateRound(Config(), arena, 1, MatchPhase.Countdown));
                var start = w.State.Props;
                for (int t = 0; t < 120; t++)
                    w.Step(default, default);
                for (int i = 0; i < start.Count; i++)
                {
                    var a = PropState.At(start, i);
                    var b = PropState.At(w.State.Props, i);
                    Assert.AreEqual(a.Position, b.Position, $"{name} 구조물 {i}");
                }
            }
        }

        // CP14-1 ----------------------------------------------------------------------

        static void RunBots(SimWorld w, int ticks, uint seed)
        {
            var p1 = new BotInputSource(() => w, 0, seed);
            var p2 = new BotInputSource(() => w, 1, seed * 7u + 3u);
            for (int t = 0; t < ticks; t++)
            {
                int tick = w.State.Tick;
                w.Step(p1.Poll(tick), p2.Poll(tick));
            }
        }

        [TestCaseSource(typeof(MapV2Tests), nameof(MapV2Tests.NewMaps))]
        public void PropsMatch_IsDeterministic_SaveRestore_SyncTest(string name)
        {
            var config = Config();
            var arena = ArenaFiles.LoadByName(name);
            var a = new SimWorld(config, arena, SimState.CreateRound(config, arena, 3, MatchPhase.Fighting));
            var b = new SimWorld(config, arena, SimState.CreateRound(config, arena, 3, MatchPhase.Fighting));
            RunBots(a, 3600, 41);
            RunBots(b, 3600, 41);
            Assert.AreEqual(a.ComputeChecksum(), b.ComputeChecksum(), "같은 입력 3600틱 두 번");

            // 저장·복원: 중간 상태를 저장했다가 되돌려 같은 입력을 다시 주면 같은 결과.
            TestWorlds.RandomInputs(17, 600, out var r1, out var r2);
            var saved = a.State;
            for (int t = 0; t < 600; t++)
                a.Step(r1[t], r2[t]);
            uint after = a.ComputeChecksum();
            a.State = saved;
            for (int t = 0; t < 600; t++)
                a.Step(r1[t], r2[t]);
            Assert.AreEqual(after, a.ComputeChecksum(), "저장·복원");

            var sync = new SyncTest<SimWorld>(b);
            var p1 = new BotInputSource(() => b, 0, 5u);
            var p2 = new BotInputSource(() => b, 1, 9u);
            int awake = 0;
            for (int t = 0; t < 1800; t++)
            {
                int tick = b.State.Tick;
                if (!sync.Advance(p1.Poll(tick), p2.Poll(tick)))
                    Assert.Fail(sync.LastMismatch.ToString());
                for (int i = 0; i < b.State.Props.Count; i++)
                {
                    if (!PropPhysics.Asleep(PropState.At(b.State.Props, i), config.Combat.Props))
                        awake++;
                }
            }
            TestContext.WriteLine($"[Knockbox][CP14-1] {name}: 결정론·저장복원·SyncTest(7틱 1800틱) 통과, 깨어 있던 구조물-틱 {awake}");
        }

        // CP14-3 ----------------------------------------------------------------------

        /// <summary>빠르게 도는 막대에 최대 돌진 속도(34)로 여러 각도에서 들이받아도 캐릭터가 막대를 지나치지 않는다.</summary>
        [Test]
        public void MaxSpeedLunges_IntoSpinningBar_NeverTunnel()
        {
            var arena = Lab();
            var config = Config();
            float speed = config.Combat.Charge.LungeMaxSpeed;
            int cases = 0, hits = 0;
            for (int angle = 0; angle < 360; angle += 15)
            {
                for (int spin = -1; spin <= 1; spin++)
                {
                    double t = angle * System.Math.PI / 180.0;
                    var dir = new Vec2((float)System.Math.Cos(t), (float)System.Math.Sin(t));
                    var start = new Vec2(8f, 0f) - dir * 4f;
                    var w = World(arena, start, new Vec2(-14f, -8f));
                    Push(w, 1, Vec2.Zero, spin * 12f);
                    var s = w.State;
                    s.Fighter0.Velocity = dir * speed;
                    s.Fighter0.SetAction(ActionState.Lunge);
                    s.Fighter0.Grounded = false;
                    w.State = s;
                    bool struck = false;
                    for (int k = 0; k < 20; k++)
                    {
                        w.Step(default, default);
                        for (int e = 0; e < w.Events.Count; e++)
                            struck |= w.Events[e].Type == SimEventType.PropStrike || w.Events[e].Type == SimEventType.PropHit;
                        var f = w.State.Fighter0;
                        var bar = PropState.At(w.State.Props, 1);
                        // 막대 좌표에서 캐릭터 가운데가 막대 몸 안(외곽에서 0.1 이상 안쪽)에 있으면 뚫은 것.
                        var local = bar.ToLocal(f.Position);
                        Assert.IsFalse(System.Math.Abs(local.X) < 2f - 0.1f && System.Math.Abs(local.Y) < 0.3f - 0.1f,
                            $"각도 {angle} 회전 {spin} 틱 {k}: 막대 속 {local}");
                    }
                    cases++;
                    if (struck)
                        hits++;
                }
            }
            TestContext.WriteLine($"[Knockbox][CP14-3] 각도 24 × 회전 3 = {cases}경우, 막대에 닿음 {hits}, 통과 0");
            Assert.Greater(hits, cases / 2);
        }

        // CP14-4 ----------------------------------------------------------------------

        [Test]
        public void Fighter_StandsOnSwingingBlock_AndMovesWithIt()
        {
            var arena = Lab();
            var config = Config();
            var w = World(arena, new Vec2(0f, 3.5f + config.HalfBody + 0.05f), new Vec2(14f, -8f));
            for (int t = 0; t < 10; t++)
                w.Step(default, default);
            Assert.IsTrue(w.State.Fighter0.Grounded, "블록 위에 선다");
            Assert.AreEqual(1, w.State.Fighter0.OnProp);
            Push(w, 0, new Vec2(4f, 0f), 0f);
            float before = w.State.Fighter0.Position.X;
            for (int t = 0; t < 12; t++)
                w.Step(default, default);
            Assert.Greater(w.State.Fighter0.Position.X - before, 0.3f, "블록과 함께 움직임");
            Assert.AreEqual(1, w.State.Fighter0.OnProp, "여전히 그 위");
        }

        [Test]
        public void Lunge_PushesBlock_AndHeavyBlockReflectsLunge()
        {
            var arena = Lab();
            var config = Config();
            var w = World(arena, new Vec2(-3f, 3f), new Vec2(14f, -8f));
            var s = w.State;
            s.Fighter0.Velocity = new Vec2(30f, 0f);
            s.Fighter0.SetAction(ActionState.Lunge);
            s.Fighter0.Grounded = false;
            w.State = s;
            bool strike = false;
            for (int t = 0; t < 10 && !strike; t++)
            {
                w.Step(default, default);
                for (int e = 0; e < w.Events.Count; e++)
                    strike |= w.Events[e].Type == SimEventType.PropStrike;
            }
            Assert.IsTrue(strike, "돌진이 블록을 침");
            var block = PropState.At(w.State.Props, 0);
            Assert.Greater(block.Velocity.X, 2f, "블록이 밀려남");
            Assert.AreEqual(0, block.LastToucher, "마지막 접촉자");
            // 블록 질량 2(< 4)라 돌진은 계속 나아간다.
            Assert.Greater(w.State.Fighter0.Velocity.X, 0f);
        }

        [Test]
        public void FastBlock_HitsFighter_WithFormulaDamage_AndCooldown()
        {
            var arena = Lab();
            var config = Config();
            var w = World(arena, new Vec2(2.2f, 3f), new Vec2(14f, -8f));
            Push(w, 0, new Vec2(16f, 0f), 0f);
            SimEvent hit = default;
            bool found = false;
            for (int t = 0; t < 10 && !found; t++)
            {
                w.Step(default, default);
                for (int e = 0; e < w.Events.Count; e++)
                {
                    if (w.Events[e].Type == SimEventType.PropHit)
                    {
                        hit = w.Events[e];
                        found = true;
                    }
                }
            }
            Assert.IsTrue(found, "블록이 캐릭터를 침");
            var p = config.Combat.Props;
            // 질량 2, 속도 약 15~16: round(16 × 2 × 0.15) = 5 안팎.
            Assert.That((int)hit.Strength, Is.InRange(p.HitMinDamage, 6));
            Assert.AreEqual(ActionState.Hitstun, w.State.Fighter0.Action);
            Assert.GreaterOrEqual(w.State.Fighter0.Velocity.X, p.HitKnockbackMin - 1e-3f);
            Assert.AreEqual(p.HitCooldownTicks, PropState.At(w.State.Props, 0).HitCooldown0);
        }

        [Test]
        public void Guard_And_Parry_AgainstBlock()
        {
            var arena = Lab();
            var config = Config();
            foreach (bool parry in new[] { false, true })
            {
                var w = World(arena, new Vec2(2.2f, 3f), new Vec2(14f, -8f));
                Push(w, 0, new Vec2(16f, 0f), 0f);
                var s = w.State;
                s.Fighter0.SetAction(ActionState.Guard);
                s.Fighter0.ParryTicks = parry ? 10 : 0;
                // 가드 버튼을 이미 누르고 있던 것으로(새로 누르면 패링 구간이 열린다).
                s.Fighter0.PrevButtons = InputButtons.Guard;
                s.Fighter0.Grounded = false;
                w.State = s;
                var type = SimEventType.Jump;
                for (int t = 0; t < 10 && type == SimEventType.Jump; t++)
                {
                    w.Step(new InputFrame(0, 0, InputButtons.Guard), default);
                    for (int e = 0; e < w.Events.Count; e++)
                    {
                        var et = w.Events[e].Type;
                        if (et == SimEventType.Parry || et == SimEventType.GuardHit || et == SimEventType.PropHit)
                            type = et;
                    }
                }
                Assert.AreEqual(parry ? SimEventType.Parry : SimEventType.GuardHit, type);
                if (parry)
                    Assert.Less(PropState.At(w.State.Props, 0).Velocity.X, 0f, "패링: 블록 속도 반사");
            }
        }

        // CP14-5 ----------------------------------------------------------------------

        /// <summary>오른쪽 막대를 오른쪽에서 들이받은 결과와 왼쪽 막대를 왼쪽에서 들이받은 결과가 거울처럼 같다.</summary>
        [Test]
        public void MirroredLunges_IntoMirroredBars_GiveMirroredResults()
        {
            var arena = Lab();
            var w = World(arena, new Vec2(12f, 1f), new Vec2(-12f, 1f));
            var s = w.State;
            s.Fighter0.Velocity = new Vec2(-30f, -4f);
            s.Fighter1.Velocity = new Vec2(30f, -4f);
            s.Fighter0.SetAction(ActionState.Lunge);
            s.Fighter1.SetAction(ActionState.Lunge);
            s.Fighter0.Grounded = s.Fighter1.Grounded = false;
            w.State = s;
            for (int t = 0; t < 240; t++)
            {
                w.Step(default, default);
                var f0 = w.State.Fighter0;
                var f1 = w.State.Fighter1;
                Assert.AreEqual(f0.Position.X, -f1.Position.X, 1e-4f, $"틱 {t}");
                Assert.AreEqual(f0.Position.Y, f1.Position.Y, 1e-4f, $"틱 {t}");
                var right = PropState.At(w.State.Props, 1);
                var left = PropState.At(w.State.Props, 2);
                Assert.AreEqual(right.Position.X, -left.Position.X, 1e-4f, $"틱 {t}");
                Assert.AreEqual(right.AngularVelocity, -left.AngularVelocity, 1e-4f, $"틱 {t}");
                Assert.AreEqual(f0.Health, f1.Health, $"틱 {t}");
            }
        }
    }
}

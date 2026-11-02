using Knockbox.Game;
using Knockbox.Simulation;
using NUnit.Framework;

namespace Knockbox.Tests.EditMode
{
    /// <summary>
    /// CR-004 D/E: 표면 이동(7절). 규칙 표의 행마다 결정적 시나리오 하나. D = 붙기만, E = 붙기 + 외곽 표면 달리기.
    /// Brawl 맵: 외곽 안쪽 면 x ±17, 바닥 y −9, 천장 y 9. 내부 지형 가운데 x[−3, 3] y[1, 4], 좌 x[−12, −7] y[−5, −2], 우 x[7, 12] y[−5, −2].
    /// </summary>
    public class BrawlSurfaceTests
    {
        const int D = 1, E = 2;
        const float Half = 0.45f;
        const float WallX = 17f - Half;
        const float CeilY = 9f - Half;
        const float FloorY = -9f + Half;

        static ArenaDef arena;
        static ArenaDef Arena => arena ??= ArenaFiles.LoadByName(MatchRunner.BrawlArenaName);

        static SimConfig Config(int mode) =>
            new SimConfig(MovementParams.Default, BrawlParams.Match(MatchParams.Default), BrawlParams.Combat(CombatParams.Default, mode), RuleSet.Brawl);

        /// <summary>P1을 pos에 속도 vel로 두고, P2는 길을 막지 않게 가운데 블록 위에 세운다.</summary>
        static SimWorld World(int mode, Vec2 pos, Vec2 vel = default, Vec2? p2 = null)
        {
            var config = Config(mode);
            var w = TestWorlds.CreateAt(pos, p2 ?? new Vec2(1.5f, 4f + Half), config, Arena);
            var s = w.State;
            s.Fighter0.Velocity = vel;
            s.Fighter0.Grounded = false;
            s.Fighter0.GroundBox = FighterState.NoBox;
            s.Fighter0.RestartAction(ActionState.Airborne);
            w.State = s;
            return w;
        }

        static InputFrame Keys(int keys, InputButtons buttons = InputButtons.None, int moveY = 0)
        {
            int x = keys == 2 ? 1 : keys == 1 ? -1 : 0;
            if (keys == 3)
                buttons |= InputButtons.BothHorizontal;
            return new InputFrame(x, moveY, buttons);
        }

        static void Run(SimWorld w, InputFrame input, int ticks)
        {
            for (int i = 0; i < ticks; i++)
                w.Step(input, default);
        }

        static void RunUntil(SimWorld w, InputFrame input, System.Func<FighterState, bool> done, int limit = 90)
        {
            for (int i = 0; i < limit && !done(w.State.Fighter0); i++)
                w.Step(input, default);
            Assert.IsTrue(done(w.State.Fighter0), $"조건에 닿지 못함: {w.State.Fighter0.Position} {w.State.Fighter0.Action} 표면 {w.State.Fighter0.Surface}");
        }

        static FighterState P1(SimWorld w) => w.State.Fighter0;

        /// <summary>외곽 오른벽(높이 y)에 붙은 월드. 키를 뗀 상태로 끝난다.</summary>
        static SimWorld OnRightWall(int mode, float y = 0f)
        {
            var w = World(mode, new Vec2(WallX - 0.6f, y), new Vec2(12f, 0f));
            RunUntil(w, Keys(2), f => f.Surface == FighterState.SurfaceWallRight);
            Run(w, Keys(0), 2);
            return w;
        }

        static SimWorld OnLeftWall(int mode, float y = 0f)
        {
            var w = World(mode, new Vec2(-WallX + 0.6f, y), new Vec2(-12f, 0f));
            RunUntil(w, Keys(1), f => f.Surface == FighterState.SurfaceWallLeft);
            Run(w, Keys(0), 2);
            return w;
        }

        static SimWorld OnCeiling(int mode, float x = -9f)
        {
            var w = World(mode, new Vec2(x, CeilY - 0.6f), new Vec2(0f, 12f));
            RunUntil(w, Keys(0), f => f.Surface == FighterState.SurfaceCeiling);
            Run(w, Keys(0), 2);
            return w;
        }

        // 붙기 조건 -------------------------------------------------------------------

        [Test]
        public void Attach_OuterWallsAndCeiling_InnerSidesAndBottom_NotInnerTop()
        {
            Assert.IsTrue(P1(OnRightWall(D)).SurfaceOuter, "외곽 오른벽");
            Assert.IsTrue(P1(OnLeftWall(D)).SurfaceOuter, "외곽 왼벽");
            Assert.IsTrue(P1(OnCeiling(D)).SurfaceOuter, "외곽 천장");

            var side = World(D, new Vec2(3f + Half + 0.5f, 2.5f), new Vec2(-12f, 0f));
            RunUntil(side, Keys(1), f => f.Surface == FighterState.SurfaceWallLeft);
            Assert.IsFalse(P1(side).SurfaceOuter, "내부 지형 옆면");

            var bottom = World(D, new Vec2(0f, 1f - Half - 0.5f), new Vec2(0f, 12f));
            RunUntil(bottom, Keys(0), f => f.Surface == FighterState.SurfaceCeiling);
            Assert.IsFalse(P1(bottom).SurfaceOuter, "내부 지형 아랫면");

            var top = World(D, new Vec2(0f, 4f + Half + 0.8f), new Vec2(0f, -2f));
            RunUntil(top, Keys(0), f => f.Grounded);
            Assert.AreEqual(FighterState.SurfaceNone, P1(top).Surface, "윗면은 기존 서기");
        }

        [Test]
        public void Attach_GravityOff_StaysPut()
        {
            var w = OnRightWall(D, 2f);
            float y = P1(w).Position.Y;
            Run(w, Keys(0), 60);
            Assert.AreEqual(FighterState.SurfaceWallRight, P1(w).Surface);
            Assert.AreEqual(y, P1(w).Position.Y, 1e-4f, "붙은 동안 중력 없음");
        }

        [Test]
        public void HitCollision_DoesNotAttach()
        {
            var w = World(E, new Vec2(WallX - 1f, 0f), new Vec2(30f, 0f));
            var s = w.State;
            s.Fighter0.StunTicks = 20;
            s.Fighter0.RestartAction(ActionState.Hitstun);
            w.State = s;
            Run(w, Keys(2), 6);
            Assert.AreEqual(FighterState.SurfaceNone, P1(w).Surface, "피격 충돌은 붙지 않고 벽꽝");
        }

        [Test]
        public void Standing_PushingWall_InD_DoesNotAttach()
        {
            var w = World(D, new Vec2(WallX - 1f, FloorY));
            Run(w, Keys(0), 2);
            Run(w, Keys(2), 30);
            Assert.AreEqual(FighterState.SurfaceNone, P1(w).Surface);
            Assert.IsTrue(P1(w).Grounded);
        }

        // 새로 누를 때의 매핑(E) -----------------------------------------------------------

        [TestCase("ceiling", 2, 1f, 0f)]
        [TestCase("ceiling", 1, -1f, 0f)]
        [TestCase("right", 2, 0f, 1f)]
        [TestCase("right", 1, 0f, -1f)]
        [TestCase("left", 2, 0f, 1f)]
        [TestCase("left", 1, 0f, -1f)]
        public void NewPress_MapsToScreenDirection(string surface, int key, float dx, float dy)
        {
            var w = surface == "ceiling" ? OnCeiling(E) : surface == "right" ? OnRightWall(E) : OnLeftWall(E);
            var p0 = P1(w).Position;
            Run(w, Keys(key), 10);
            var d = P1(w).Position - p0;
            Assert.AreNotEqual(FighterState.SurfaceNone, P1(w).Surface);
            if (dx != 0f) Assert.AreEqual(dx, SimMath.Sign(d.X), $"{d}");
            if (dy != 0f) Assert.AreEqual(dy, SimMath.Sign(d.Y), $"{d}");
            Assert.AreEqual(Config(E).Movement.MaxRunSpeed, SimMath.Abs(dx != 0f ? P1(w).Velocity.X : P1(w).Velocity.Y), 1e-3f, "바닥 달리기와 같은 속도");
        }

        [Test]
        public void Floor_KeysAreScreenLeftRight()
        {
            var w = World(E, new Vec2(0f, FloorY - 0f));
            Run(w, Keys(0), 2);
            float x0 = P1(w).Position.X;
            Run(w, Keys(2), 10);
            Assert.Greater(P1(w).Position.X, x0);
        }

        // 모서리 ---------------------------------------------------------------------

        [Test]
        public void Corner_KeepsDirection_SwitchesSurface_FullLoopCcw()
        {
            // D를 계속: 오른벽 위 → 천장 왼쪽 → 왼벽 아래 → 바닥 오른쪽.
            var w = OnRightWall(E, 6f);
            RunUntil(w, Keys(2), f => f.Surface == FighterState.SurfaceCeiling, 60);
            float x0 = P1(w).Position.X;
            Run(w, Keys(2), 5);
            Assert.Less(P1(w).Position.X, x0, "천장에서 왼쪽");
            RunUntil(w, Keys(2), f => f.Surface == FighterState.SurfaceWallLeft, 400);
            float y0 = P1(w).Position.Y;
            Run(w, Keys(2), 5);
            Assert.Less(P1(w).Position.Y, y0, "왼벽에서 아래");
            RunUntil(w, Keys(2), f => f.Grounded, 300);
            float fx = P1(w).Position.X;
            Run(w, Keys(2), 5);
            Assert.Greater(P1(w).Position.X, fx, "바닥에서 오른쪽");
            RunUntil(w, Keys(2), f => f.Surface == FighterState.SurfaceWallRight, 300);
        }

        [Test]
        public void Corner_AHeld_LoopsClockwise()
        {
            // A를 계속: 바닥 왼쪽 → 왼벽 위 → 천장 오른쪽 → 오른벽 아래.
            var w = World(E, new Vec2(-12f, FloorY));
            Run(w, Keys(0), 2);
            RunUntil(w, Keys(1), f => f.Surface == FighterState.SurfaceWallLeft, 120);
            float y0 = P1(w).Position.Y;
            Run(w, Keys(1), 5);
            Assert.Greater(P1(w).Position.Y, y0, "왼벽에서 위(바닥에서 이어온 방향)");
            RunUntil(w, Keys(1), f => f.Surface == FighterState.SurfaceCeiling, 200);
            float x0 = P1(w).Position.X;
            Run(w, Keys(1), 5);
            Assert.Greater(P1(w).Position.X, x0, "천장에서 오른쪽");
            RunUntil(w, Keys(1), f => f.Surface == FighterState.SurfaceWallRight, 400);
            float y1 = P1(w).Position.Y;
            Run(w, Keys(1), 5);
            Assert.Less(P1(w).Position.Y, y1, "오른벽에서 아래");
        }

        // 기억 -----------------------------------------------------------------------

        [Test]
        public void AttackPause_KeepsMemory_ResumesSameDirection()
        {
            var w = OnRightWall(E, -2f);
            Run(w, Keys(2), 4);
            Assert.AreEqual(1, P1(w).SurfaceCirc);
            w.Step(Keys(2, InputButtons.Attack), default);
            w.Step(Keys(2), default);
            Assert.AreEqual(ActionState.Attack, P1(w).Action);
            float y = P1(w).Position.Y;
            for (int i = 0; i < 40 && P1(w).Action == ActionState.Attack; i++)
            {
                Assert.AreEqual(y, P1(w).Position.Y, 1e-4f, "공격 중 멈춤");
                Assert.AreEqual(FighterState.SurfaceWallRight, P1(w).Surface);
                w.Step(Keys(2), default);
            }
            // 공격이 끝나는 틱부터 같은 방향으로 다시 움직인다.
            Assert.AreEqual(1, P1(w).SurfaceCirc, "기억 유지");
            Run(w, Keys(2), 5);
            Assert.Greater(P1(w).Position.Y, y, "같은 방향으로 재개");
        }

        [Test]
        public void ReleaseBoth_StopsAndForgets_NewPressDecidesAgain()
        {
            var w = OnRightWall(E);
            Run(w, Keys(2), 4);
            Run(w, Keys(0), 1);
            Assert.AreEqual(0, P1(w).SurfaceCirc, "기억 삭제");
            float y = P1(w).Position.Y;
            Run(w, Keys(0), 5);
            Assert.AreEqual(y, P1(w).Position.Y, 1e-4f, "정지");
            Run(w, Keys(1), 5);
            Assert.Less(P1(w).Position.Y, y, "A를 새로 누르면 아래");
        }

        [Test]
        public void BothPressed_NeutralStopKeepsMemory_ReleaseOneRedecides()
        {
            var w = OnRightWall(E);
            Run(w, Keys(2), 4);
            Run(w, Keys(3), 1);
            float y = P1(w).Position.Y;
            Run(w, Keys(3), 5);
            Assert.AreEqual(y, P1(w).Position.Y, 1e-4f, "양쪽 동시: 중립 정지");
            Assert.AreEqual(1, P1(w).SurfaceCirc, "기억 유지");
            Run(w, Keys(1), 5);
            Assert.AreEqual(-1, P1(w).SurfaceCirc, "남은 A로 재결정");
            Assert.Less(P1(w).Position.Y, y);
        }

        [Test]
        public void Jump_DetachesAndForgets_AirKeysAreScreen()
        {
            var w = OnRightWall(E);
            Run(w, Keys(2), 4);
            w.Step(Keys(1, InputButtons.Jump), default);
            Assert.AreEqual(FighterState.SurfaceNone, P1(w).Surface);
            Assert.AreEqual(0, P1(w).SurfaceCirc);
            Assert.Less(P1(w).Velocity.X, 0f, "벽에서 떨어져 나감");
            Run(w, Keys(1), 3);
            Assert.Less(P1(w).Velocity.X, 0f, "공중 A = 화면 왼쪽");
        }

        [Test]
        public void SKey_DropsOff()
        {
            var w = OnCeiling(D);
            Run(w, Keys(0, InputButtons.None, -1), 2);
            Assert.AreEqual(FighterState.SurfaceNone, P1(w).Surface);
            Assert.Less(P1(w).Position.Y, CeilY - 0.01f, "떨어짐");

            var wall = OnRightWall(D);
            Run(wall, Keys(2, InputButtons.None, -1), 1);
            Run(wall, Keys(2), 4);
            Assert.AreEqual(FighterState.SurfaceNone, P1(wall).Surface, "S로 놓으면 벽 쪽 입력이 잠깐 무시되어 바로 다시 붙지 않음");
        }

        [Test]
        public void HitWhileAttached_DetachesAndKeysCountAsNewAfter()
        {
            var w = OnRightWall(E);
            Run(w, Keys(2), 4);
            var s = w.State;
            s.Fighter0.StunTicks = 10;
            s.Fighter0.Velocity = new Vec2(-8f, 0f);
            s.Fighter0.RestartAction(ActionState.Hitstun);
            w.State = s;
            w.Step(Keys(2), default);
            Assert.AreEqual(FighterState.SurfaceNone, P1(w).Surface);
            Assert.AreEqual(0, P1(w).SurfaceCirc);
        }

        // 박치기와 표면 -----------------------------------------------------------------

        [Test]
        public void LungeFromSurface_Detaches_AndAimedIntoSurfaceReattaches()
        {
            var config = Config(E);
            int hold = config.Combat.Body.TapHoldTicks + 4;
            // 천장에서 아래로 박치기: 떨어지고 기억 삭제.
            var w = OnCeiling(E);
            for (int i = 0; i <= hold; i++)
                w.Step(new InputFrame(0, 0, InputButtons.Attack, AimDirections.Encode(48)), default);
            w.Step(new InputFrame(0, 0, InputButtons.None, AimDirections.Encode(48)), default);
            Assert.AreEqual(ActionState.Lunge, P1(w).Action);
            Assert.AreEqual(FighterState.SurfaceNone, P1(w).Surface);

            // 오른벽에서 벽 안쪽(오른쪽)으로 겨눈 박치기: 조준 그대로 발사 → 벽에 닿아 멈춤 → 다시 붙음.
            var into = OnRightWall(E, 2f);
            for (int i = 0; i <= hold; i++)
                into.Step(new InputFrame(0, 0, InputButtons.Attack, AimDirections.Encode(0)), default);
            into.Step(new InputFrame(0, 0, InputButtons.None, AimDirections.Encode(0)), default);
            RunUntil(into, default, f => f.Surface == FighterState.SurfaceWallRight && f.Action != ActionState.Lunge, 20);
        }

        // 잡은 쪽 / 잡힌 쪽 ---------------------------------------------------------------

        [Test]
        public void Holder_StaysAttached_HeldDetaches()
        {
            var config = Config(E);
            // P1은 바닥 바로 위 오른벽, P2는 그 왼쪽 바닥에 붙어 선다.
            var w = World(E, new Vec2(WallX - 0.6f, FloorY + 0.4f), new Vec2(12f, 0f), new Vec2(WallX - 1.4f, FloorY));
            RunUntil(w, Keys(2), f => f.Surface == FighterState.SurfaceWallRight, 10);
            w.Step(new InputFrame(0, 0, InputButtons.Grab, AimDirections.Encode(32)), default);
            RunUntil(w, new InputFrame(0, 0, InputButtons.None, AimDirections.Encode(32)), f => f.Action == ActionState.Holding, 20);
            Run(w, new InputFrame(0, 0, InputButtons.None, AimDirections.Encode(32)), 3);
            Assert.AreEqual(FighterState.SurfaceWallRight, P1(w).Surface, "잡은 쪽은 붙은 채 멈춤");
            Assert.AreEqual(ActionState.Held, w.State.Fighter1.Action);
            Assert.AreEqual(FighterState.SurfaceNone, w.State.Fighter1.Surface, "잡힌 쪽은 붙지 않음");
        }

        // 내부 지형과 D 모드 ------------------------------------------------------------

        [Test]
        public void InnerSurface_IgnoresKeys_InBothModes()
        {
            foreach (int mode in new[] { D, E })
            {
                var w = World(mode, new Vec2(3f + Half + 0.5f, 2.5f), new Vec2(-12f, 0f));
                RunUntil(w, Keys(1), f => f.Surface == FighterState.SurfaceWallLeft);
                var p = P1(w).Position;
                Run(w, Keys(2), 10);
                Run(w, Keys(1), 30);
                Assert.AreEqual(FighterState.SurfaceWallLeft, P1(w).Surface, $"모드 {mode}");
                Assert.AreEqual(p.Y, P1(w).Position.Y, 1e-4f, $"모드 {mode}: 내부 지형에서는 달리지 않음");
            }
        }

        [Test]
        public void ModeD_OuterSurface_IgnoresKeys()
        {
            var w = OnRightWall(D);
            float y = P1(w).Position.Y;
            Run(w, Keys(2), 20);
            Run(w, Keys(1), 20);
            Assert.AreEqual(y, P1(w).Position.Y, 1e-4f);
        }

        [Test]
        public void ModeOff_NoAttach()
        {
            var w = World(0, new Vec2(WallX - 0.6f, 0f), new Vec2(12f, 0f));
            Run(w, Keys(2), 20);
            Assert.AreEqual(FighterState.SurfaceNone, P1(w).Surface);
        }

        // 3.3 안전 검사와 봇 스모크 ------------------------------------------------------

        [Test]
        public void SurfaceScenario_DeterministicSaveRestoreSyncTestReplay_BotSmoke()
        {
            var config = Config(E);
            SimWorld world = null;
            var bot0 = new BrawlBot(() => world, 0, 3u);
            var bot1 = new BrawlBot(() => world, 1, 4u);
            world = new SimWorld(config, Arena, SimState.CreateMatch(config, new[] { Arena }, 7, MatchPhase.Countdown));
            const int ticks = 1800;
            var p1 = new InputFrame[ticks];
            var p2 = new InputFrame[ticks];
            var rng = new SimRng(55);
            int attaches = 0;
            for (int t = 0; t < ticks; t++)
            {
                int tick = world.State.Tick;
                p1[t] = bot0.Poll(tick);
                p2[t] = bot1.Poll(tick);
                // 봇은 벽을 찾지 않으므로 P1에 무작위 점프·좌우를 섞어 표면에 붙게 한다.
                if (rng.Range(0, 3) == 0)
                    p1[t] = new InputFrame(rng.Range(-1, 2), rng.Range(0, 6) == 0 ? -1 : 0, rng.Range(0, 4) == 0 ? InputButtons.Jump : p1[t].Buttons & ~InputButtons.Jump, p1[t].Aim);
                world.Step(p1[t], p2[t]);
                var s = world.State;
                for (int k = 0; k < 2; k++)
                {
                    var f = s.GetFighter(k);
                    Assert.IsFalse(float.IsNaN(f.Position.X) || float.IsNaN(f.Position.Y), $"NaN 틱 {t}");
                    Assert.LessOrEqual(SimMath.Abs(f.Position.X), WallX + 1e-3f, $"틱 {t}");
                    Assert.LessOrEqual(SimMath.Abs(f.Position.Y), CeilY + 1e-3f, $"틱 {t}");
                }
                for (int e = 0; e < world.Events.Count; e++)
                {
                    Assert.AreNotEqual(SimEventType.OutOfBounds, world.Events[e].Type, $"틱 {t}");
                    if (world.Events[e].Type == SimEventType.SurfaceAttach)
                        attaches++;
                }
            }
            TestContext.WriteLine($"[CR-004 D/E] 봇 30초(E): 붙기 {attaches}회");
            Assert.Greater(attaches, 0);

            SimWorld Fresh() => new SimWorld(config, Arena, SimState.CreateMatch(config, new[] { Arena }, 7, MatchPhase.Countdown));
            var a = Fresh();
            var b = Fresh();
            for (int t = 0; t < ticks; t++)
            {
                a.Step(p1[t], p2[t]);
                b.Step(p1[t], p2[t]);
                Assert.AreEqual(a.ComputeChecksum(), b.ComputeChecksum(), $"틱 {t + 1}\n{StateDiff.Describe(a.State, b.State)}");
            }
            Assert.AreEqual(world.ComputeChecksum(), a.ComputeChecksum(), "봇으로 진행한 결과와 같음");

            var w = Fresh();
            for (int t = 0; t < 900; t++)
                w.Step(p1[t], p2[t]);
            var snapshot = w.State;
            for (int t = 900; t < ticks; t++)
                w.Step(p1[t], p2[t]);
            uint first = w.ComputeChecksum();
            w.State = snapshot;
            for (int t = 900; t < ticks; t++)
                w.Step(p1[t], p2[t]);
            Assert.AreEqual(first, w.ComputeChecksum(), "저장/복원 뒤 재진행");

            var synced = Fresh();
            var sync = new SyncTest<SimWorld>(synced, 7);
            for (int t = 0; t < ticks; t++)
                sync.Advance(p1[t], p2[t]);
            Assert.AreEqual(0, sync.MismatchCount, sync.FirstMismatch?.ToString());

            var spawn0 = SimState.SpawnCenter(config, Arena, 0);
            var spawn1 = SimState.SpawnCenter(config, Arena, 1);
            var recording = InputRecording.Begin(config, Arena, 7, spawn0, spawn1, MatchPhase.Countdown, RuleSet.Brawl.Format());
            var rw = new SimWorld(config, Arena, SimState.CreateRound(config, Arena, 7, spawn0, spawn1, MatchPhase.Countdown));
            for (int t = 0; t < ticks; t++)
            {
                recording.Append(p1[t], p2[t]);
                rw.Step(p1[t], p2[t]);
            }
            recording.Finish(rw.ComputeChecksum());
            Assert.AreEqual(recording.FinalChecksum, InputRecording.FromBytes(recording.ToBytes()).Replay(config, Arena));
        }
    }
}

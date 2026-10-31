using System.Collections.Generic;
using System.IO;
using Knockbox.Game;
using Knockbox.Simulation;
using NUnit.Framework;

namespace Knockbox.Tests.EditMode
{
    /// <summary>CP12 맵 v2(04-arena.md 6절): 새 맵 6개, 짝 파일, 라운드마다 맵 교체.</summary>
    public class MapV2Tests
    {
        public static readonly string[] NewMaps = { "Crossing", "Foundry", "Gallery", "Pillars", "Terrace", "Vault" };
        const int MatchTicks = 7200;

        static SimConfig Config() =>
            new SimConfig(MovementParams.Default, MatchParams.Default, CombatParams.Default, CombatMode.Charge, true);

        static ArenaMeta Meta(string name) =>
            ArenaMeta.Parse(File.ReadAllText(Path.Combine(ArenaFiles.Folder, name + ".json")), name + ".json");

        static ArenaDef[] LoadAll()
        {
            var arenas = new ArenaDef[NewMaps.Length];
            for (int i = 0; i < NewMaps.Length; i++)
                arenas[i] = ArenaFiles.LoadByName(NewMaps[i]);
            return arenas;
        }

        // CP12-1 ------------------------------------------------------------------

        /// <summary>파서 검증(크기·테두리·시작 위치·문자·좌우 대칭)을 통과하고, 상자와 시작 위치가 좌우로 짝을 이룬다.</summary>
        [TestCaseSource(nameof(NewMaps))]
        public void NewMap_ParsesAsMirroredV2(string name)
        {
            var arena = ArenaFiles.LoadByName(name);
            Assert.AreEqual(36, arena.Columns);
            Assert.AreEqual(20, arena.Rows);
            Assert.AreEqual(-arena.SpawnFoot0.X, arena.SpawnFoot1.X, 1e-5f);
            Assert.AreEqual(arena.SpawnFoot0.Y, arena.SpawnFoot1.Y, 1e-5f);
            for (int i = 0; i < arena.BoxCount; i++)
            {
                var b = arena.GetBox(i);
                bool mirrored = false;
                for (int j = 0; j < arena.BoxCount && !mirrored; j++)
                {
                    var m = arena.GetBox(j);
                    mirrored = m.Kind == b.Kind && Near(m.MinX, -b.MaxX) && Near(m.MaxX, -b.MinX) && Near(m.MinY, b.MinY) && Near(m.MaxY, b.MaxY);
                }
                Assert.IsTrue(mirrored, $"{name}: 좌우 짝이 없는 상자 {b}");
            }
        }

        static bool Near(float a, float b) => System.Math.Abs(a - b) < 1e-4f;

        /// <summary>어떤 지형과도 겹치지 않는 칸만으로 된 가장 큰 정사각형의 한 변(칸).</summary>
        static int LargestEmptySquare(ArenaDef arena)
        {
            var size = new int[arena.Rows + 1, arena.Columns + 1];
            int best = 0;
            for (int r = 1; r <= arena.Rows; r++)
            {
                for (int c = 1; c <= arena.Columns; c++)
                {
                    float minX = c - 1 - arena.HalfWidth, maxY = arena.HalfHeight - (r - 1);
                    bool empty = true;
                    for (int i = 0; i < arena.BoxCount && empty; i++)
                    {
                        var b = arena.GetBox(i);
                        empty = !(b.MinX < minX + 1f - 1e-4f && b.MaxX > minX + 1e-4f && b.MinY < maxY - 1e-4f && b.MaxY > maxY - 1f + 1e-4f);
                    }
                    size[r, c] = empty ? 1 + System.Math.Min(size[r - 1, c], System.Math.Min(size[r, c - 1], size[r - 1, c - 1])) : 0;
                    best = System.Math.Max(best, size[r, c]);
                }
            }
            return best;
        }

        /// <summary>
        /// 화면 가득 구조물: 경기장 안쪽 칸의 15% 이상이 테두리 아닌 지형이고, 빈 칸으로만 된 정사각형이 7 × 7보다 크지 않다
        /// (v1 Box는 3%, 8 × 8). 가시는 바닥 전체를 덮지 않는다(바닥 폭의 절반 미만).
        /// </summary>
        [TestCaseSource(nameof(NewMaps))]
        public void NewMap_IsDenseAndSpikesStayPartial(string name)
        {
            var arena = ArenaFiles.LoadByName(name);
            float inner = (arena.Columns - 2) * (arena.Rows - 2);
            float filled = 0f, spikeOnFloor = 0f;
            float floorTop = -arena.HalfHeight + 1f;
            for (int i = 0; i < arena.BoxCount; i++)
            {
                var b = arena.GetBox(i);
                float minX = System.Math.Max(b.MinX, -arena.HalfWidth + 1f), maxX = System.Math.Min(b.MaxX, arena.HalfWidth - 1f);
                float minY = System.Math.Max(b.MinY, floorTop), maxY = System.Math.Min(b.MaxY, arena.HalfHeight - 1f);
                if (maxX > minX && maxY > minY && b.Kind != TerrainKind.Spike)
                    filled += (maxX - minX) * (maxY - minY);
                if (b.Kind == TerrainKind.Spike && Near(b.MinY, floorTop))
                    spikeOnFloor += b.MaxX - b.MinX;
            }
            float density = filled / inner;
            int square = LargestEmptySquare(arena);
            TestContext.WriteLine($"[Knockbox][CP12-1] {name}: 구조물 {density:P1}, 가장 큰 빈 정사각형 {square}, 바닥 가시 {spikeOnFloor}/{arena.Columns - 2}");
            Assert.GreaterOrEqual(density, 0.15f, "구조물이 너무 적음");
            Assert.LessOrEqual(square, 7, "빈 하늘이 너무 넓음");
            Assert.Less(spikeOnFloor, (arena.Columns - 2) * 0.5f, "가시가 바닥을 너무 많이 덮음");
        }

        /// <summary>짝 파일: 테마당 2개, 모두 순환에 들어가고 v1 맵(테스트 고정 자료)은 빠진다.</summary>
        [Test]
        public void PairFiles_TwoMapsPerTheme_V1OutOfRotation()
        {
            var perTheme = new int[3];
            foreach (string name in NewMaps)
            {
                var meta = Meta(name);
                Assert.IsTrue(meta.Rotation, name);
                Assert.IsTrue(meta.HasLight, name);
                perTheme[meta.Theme]++;
            }
            CollectionAssert.AreEqual(new[] { 2, 2, 2 }, perTheme);
            foreach (string name in new[] { "Box", "Towers", "Pit" })
                Assert.IsFalse(Meta(name).Rotation, name);
        }

        [Test]
        public void PairFile_RejectsBadValues()
        {
            Assert.Throws<System.FormatException>(() => ArenaMeta.Parse("{\"theme\": \"Z\"}", "bad"));
            Assert.Throws<System.FormatException>(() => ArenaMeta.Parse("{\"light\": [1]}", "bad"));
            Assert.Throws<System.FormatException>(() => ArenaMeta.Parse("{\"rotation\": 1}", "bad"));
            Assert.Throws<System.FormatException>(() => ArenaMeta.Parse("[]", "bad"));
        }

        // CP12-2 ------------------------------------------------------------------

        static int[] Sequence(uint seed, int count, int rounds)
        {
            var r = ArenaRotation.Create(seed, count);
            var list = new int[rounds];
            for (int i = 0; i < rounds; i++)
            {
                list[i] = r.Current;
                r.Advance();
            }
            return list;
        }

        /// <summary>같은 시드면 같은 순서, 한 바퀴(6라운드) 안에 중복 없음, 바퀴가 바뀔 때도 같은 맵이 이어지지 않음.</summary>
        [Test]
        public void Rotation_SameSeedSameOrder_NoRepeatWithinSix()
        {
            var firsts = new HashSet<string>();
            for (uint seed = 0; seed < 300; seed++)
            {
                int[] a = Sequence(seed, 6, 36), b = Sequence(seed, 6, 36);
                CollectionAssert.AreEqual(a, b, $"시드 {seed}");
                for (int cycle = 0; cycle < 6; cycle++)
                {
                    var seen = new HashSet<int>();
                    for (int k = 0; k < 6; k++)
                        Assert.IsTrue(seen.Add(a[cycle * 6 + k]), $"시드 {seed} {cycle}바퀴 중복");
                }
                for (int i = 1; i < a.Length; i++)
                    Assert.AreNotEqual(a[i - 1], a[i], $"시드 {seed} {i}라운드 같은 맵 연속");
                firsts.Add($"{a[0]}{a[1]}{a[2]}{a[3]}{a[4]}{a[5]}");
            }
            Assert.Greater(firsts.Count, 100, "시드마다 순서가 달라야 함");
            CollectionAssert.AreEqual(new[] { 0, 0, 0 }, Sequence(5, 1, 3), "맵 하나면 고정");
        }

        /// <summary>순환 경기: 라운드마다 맵이 바뀌고, 7틱 되감기 SyncTest가 라운드 교체를 지나도 일치한다.</summary>
        [Test]
        public void RotationMatch_ChangesMapEachRound_SyncTestPasses()
        {
            var config = Config();
            var arenas = LoadAll();
            var world = new SimWorld(config, arenas, SimState.CreateMatch(config, arenas, 7, MatchPhase.Countdown));
            var sync = new SyncTest<SimWorld>(world);
            var p1 = new BotInputSource(() => world, 0, 419u);
            var p2 = new BotInputSource(() => world, 1, 917u);
            var played = new List<string> { world.Arena.Name };
            int round = world.State.Match.Round;
            for (int t = 0; t < MatchTicks * 2 && played.Count < 7; t++)
            {
                int tick = world.State.Tick;
                if (!sync.Advance(p1.Poll(tick), p2.Poll(tick)))
                    Assert.Fail(sync.LastMismatch.ToString());
                if (world.State.Match.Round != round)
                {
                    round = world.State.Match.Round;
                    Assert.AreNotEqual(played[played.Count - 1], world.Arena.Name, "라운드가 바뀌면 맵도 바뀜");
                    Assert.AreEqual(world.Arena.SpawnFoot0.X, world.State.Fighter0.Position.X, 1e-4f, "새 맵의 시작 위치");
                    played.Add(world.Arena.Name);
                }
            }
            TestContext.WriteLine($"[Knockbox][CP12-2] 시드 7 순서: {string.Join(" → ", played)}");
            Assert.GreaterOrEqual(played.Count, 3, "라운드가 충분히 진행되지 않음");
            var firstSix = new HashSet<string>();
            for (int i = 0; i < System.Math.Min(6, played.Count); i++)
                Assert.IsTrue(firstSix.Add(played[i]), $"6라운드 안에 중복: {string.Join(", ", played)}");
        }

        /// <summary>순환 경기의 입력 기록은 순환 상태를 담아 같은 결과로 재생된다(파일 왕복 포함).</summary>
        [Test]
        public void RotationRecording_ReplaysToSameChecksum()
        {
            var config = Config();
            var arenas = LoadAll();
            var world = new SimWorld(config, arenas, SimState.CreateMatch(config, arenas, 11, MatchPhase.Fighting));
            var s = world.State;
            var rec = InputRecording.Begin(config, arenas, s.Match.Rotation, s.Rng.State, s.Fighter0.Position, s.Fighter1.Position, s.Match.Phase, "Charge");
            Assert.AreEqual(world.ComputeChecksum(), rec.InitialChecksum);
            var p1 = new BotInputSource(() => world, 0, 5u);
            var p2 = new BotInputSource(() => world, 1, 6u);
            int startRound = world.State.Match.Round;
            for (int t = 0; t < MatchTicks; t++)
            {
                int tick = world.State.Tick;
                var a = p1.Poll(tick);
                var b = p2.Poll(tick);
                rec.Append(a, b);
                world.Step(a, b);
            }
            Assert.Greater(world.State.Match.Round, startRound, "라운드 교체가 기록에 들어가야 함");
            rec.Finish(world.ComputeChecksum());
            var loaded = InputRecording.FromBytes(rec.ToBytes());
            Assert.AreEqual(rec.Rotation.Order, loaded.Rotation.Order);
            CollectionAssert.AreEqual(NewMaps, loaded.ArenaNames);
            Assert.AreEqual(rec.FinalChecksum, loaded.Replay(config, LoadAll()));
        }

        // CP12-3 ------------------------------------------------------------------

        /// <summary>모드 A Smart 봇으로 맵마다 시드 6 × 2분: 라운드 평균 10~30초, 지형 데미지(벽꽝·가시 등) 비율 기록.</summary>
        [TestCaseSource(nameof(NewMaps))]
        public void SmartBots_RoundLengthAndTerrainShare(string name)
        {
            var config = Config();
            var arena = ArenaFiles.LoadByName(name);
            int rounds = 0, fightingTicks = 0, outOfBounds = 0, wallSplats = 0;
            double total = 0, terrain = 0, spike = 0;
            for (uint seed = 1; seed <= 6; seed++)
            {
                var world = TestWorlds.Create(MatchPhase.Countdown, seed, config, arena);
                var p1 = new BotInputSource(() => world, 0, 300u + seed * 17u);
                var p2 = new BotInputSource(() => world, 1, 700u + seed * 31u);
                for (int t = 0; t < MatchTicks; t++)
                {
                    int h0 = world.State.Fighter0.Health, h1 = world.State.Fighter1.Health;
                    int tick = world.State.Tick;
                    world.Step(p1.Poll(tick), p2.Poll(tick));
                    if (world.State.Match.Phase == MatchPhase.Fighting)
                        fightingTicks++;
                    float hit0 = 0, hit1 = 0;
                    bool spike0 = false, spike1 = false;
                    for (int i = 0; i < world.Events.Count; i++)
                    {
                        var e = world.Events[i];
                        switch (e.Type)
                        {
                            case SimEventType.Hit:
                            case SimEventType.Throw:
                                if (e.Fighter == 0) hit0 += e.Strength; else hit1 += e.Strength;
                                break;
                            case SimEventType.Spike:
                                if (e.Fighter == 0) spike0 = true; else spike1 = true;
                                break;
                            case SimEventType.WallSplat:
                                wallSplats++;
                                break;
                            case SimEventType.RoundEnd:
                                rounds++;
                                break;
                            case SimEventType.OutOfBounds:
                                outOfBounds++;
                                break;
                        }
                    }
                    Account(h0 - world.State.Fighter0.Health, hit0, spike0, ref total, ref terrain, ref spike);
                    Account(h1 - world.State.Fighter1.Health, hit1, spike1, ref total, ref terrain, ref spike);
                }
            }
            float average = rounds > 0 ? fightingTicks / 60f / rounds : float.PositiveInfinity;
            string report = $"[Knockbox][CP12-3] {name}: 라운드 {rounds}, 평균 {average:F1}초, 지형 데미지 {(total > 0 ? terrain / total : 0):P0}"
                            + $"(가시 {(total > 0 ? spike / total : 0):P0}), 벽꽝 {wallSplats}, 경기장 이탈 {outOfBounds}";
            TestContext.WriteLine(report);
            UnityEngine.Debug.Log(report);
            Assert.AreEqual(0, outOfBounds, "경기장 이탈");
            Assert.That(average, Is.InRange(10f, 30f), report);
        }

        /// <summary>체력 감소 중 명중·던지기 데미지를 뺀 나머지가 지형 데미지(벽꽝 반동, 가시).</summary>
        static void Account(int drop, float hitDamage, bool spiked, ref double total, ref double terrain, ref double spike)
        {
            if (drop <= 0)
                return;
            total += drop;
            double rest = drop - System.Math.Min(drop, hitDamage);
            if (rest <= 0)
                return;
            terrain += rest;
            if (spiked)
                spike += rest;
        }
    }
}

using Knockbox.Simulation;

namespace Knockbox.Tests.EditMode
{
    /// <summary>테스트 공용: 기본 설정의 월드와 고정 시드 무작위 입력.</summary>
    public static class TestWorlds
    {
        public static SimWorld Create(MatchPhase startPhase = MatchPhase.Fighting, uint seed = 1, SimConfig config = null)
        {
            config ??= SimConfig.CreateDefault();
            var state = SimState.CreateRound(config, seed, TempArena.DefaultSpawn(config, 0), TempArena.DefaultSpawn(config, 1), startPhase);
            return new SimWorld(config, state);
        }

        public static void RandomInputs(uint seed, int ticks, out InputFrame[] p1, out InputFrame[] p2)
        {
            var g1 = new RandomInputGenerator(seed);
            var g2 = new RandomInputGenerator(seed ^ 0xA5A5A5A5u);
            p1 = new InputFrame[ticks];
            p2 = new InputFrame[ticks];
            for (int i = 0; i < ticks; i++)
            {
                p1[i] = g1.Next();
                p2[i] = g2.Next();
            }
        }
    }
}

namespace Knockbox.Simulation
{
    /// <summary>
    /// CP1 임시 경기장: Box 맵의 안쪽 경계와 같은 평평한 바닥과 좌우 벽.
    /// CP2에서 ArenaDef(맵 파서)로 교체한다.
    /// </summary>
    public static class TempArena
    {
        public const float FloorY = -8f;
        public const float CeilingY = 8f;
        public const float WallX = 15f;
        public const float DefaultSpawnX = 6f;

        public static float StandingY(SimConfig config) => FloorY + config.HalfBody;

        public static Vec2 DefaultSpawn(SimConfig config, int fighter) =>
            new Vec2(fighter == 0 ? -DefaultSpawnX : DefaultSpawnX, StandingY(config));

        public static bool IsOnFloor(SimConfig config, Vec2 position) =>
            position.Y - config.HalfBody <= FloorY + config.Movement.GroundProbeDistance;
    }
}

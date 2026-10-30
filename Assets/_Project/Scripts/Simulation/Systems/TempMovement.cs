namespace Knockbox.Simulation
{
    /// <summary>
    /// CP1 임시 이동: 좌우 가감속, 단순 점프, 중력, 임시 바닥·벽·천장.
    /// 결정론 도구를 검증할 때 의미 있는 상태 변화를 주기 위한 것이며 CP2에서 이동 명세 전체로 교체한다.
    /// </summary>
    public static class TempMovement
    {
        public static void Step(ref FighterState f, in InputFrame input, SimConfig config, int fighterIndex, int tick, SimEventBuffer events)
        {
            ref readonly var m = ref config.Movement;
            const float dt = SimConfig.Dt;
            var pressed = InputFrame.Pressed(input.Buttons, f.PrevButtons);

            // 좌우 가감속
            int moveX = input.MoveX;
            if (moveX != 0)
            {
                float accel = f.Grounded ? m.GroundAccel : m.AirAccel;
                int velSign = SimMath.Sign(f.Velocity.X);
                if (velSign != 0 && velSign != moveX)
                    accel *= m.TurnAccelMultiplier;
                f.Velocity.X = SimMath.MoveTowards(f.Velocity.X, moveX * m.MaxRunSpeed, accel * dt);
                f.Facing = (sbyte)moveX;
            }
            else
            {
                float decel = f.Grounded ? m.GroundDecel : m.AirDecel;
                f.Velocity.X = SimMath.MoveTowards(f.Velocity.X, 0f, decel * dt);
            }

            // 점프: 땅에서만
            if (f.Grounded && (pressed & InputButtons.Jump) != 0)
            {
                f.Velocity.Y = m.JumpSpeed;
                f.Grounded = false;
                events.Add(new SimEvent(tick, SimEventType.Jump, fighterIndex, f.Position, new Vec2(0f, 1f), m.JumpSpeed));
            }

            // 중력
            if (!f.Grounded)
            {
                float gravity = m.RiseGravity * (f.Velocity.Y <= 0f ? m.FallGravityMultiplier : 1f);
                f.Velocity.Y = SimMath.Max(f.Velocity.Y - gravity * dt, -m.MaxFallSpeed);
            }

            f.Position += f.Velocity * dt;

            // 임시 경계
            float half = config.HalfBody;
            float minX = -TempArena.WallX + half;
            float maxX = TempArena.WallX - half;
            if (f.Position.X < minX || f.Position.X > maxX)
            {
                f.Position.X = SimMath.Clamp(f.Position.X, minX, maxX);
                f.Velocity.X = 0f;
            }

            float ceiling = TempArena.CeilingY - half;
            if (f.Position.Y > ceiling)
            {
                f.Position.Y = ceiling;
                if (f.Velocity.Y > 0f)
                    f.Velocity.Y = 0f;
            }

            float standY = TempArena.StandingY(config);
            if (f.Position.Y <= standY)
            {
                f.Position.Y = standY;
                if (!f.Grounded)
                {
                    events.Add(new SimEvent(tick, SimEventType.Land, fighterIndex, f.Position, new Vec2(0f, -1f), -f.Velocity.Y));
                    f.Grounded = true;
                }
                f.Velocity.Y = 0f;
            }

            if (!f.Grounded)
                f.SetAction(ActionState.Airborne);
            else if (SimMath.Abs(f.Velocity.X) > SimMath.Epsilon)
                f.SetAction(ActionState.Run);
            else
                f.SetAction(ActionState.Idle);
        }
    }
}

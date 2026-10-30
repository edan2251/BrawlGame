namespace Knockbox.Simulation
{
    /// <summary>한 틱 동안 이동 단계 사이에 넘기는 값. 상태가 아니다.</summary>
    public struct MovementScratch
    {
        public MoveContacts Contacts;
        /// <summary>벽 점프 입력 잠금을 적용한 뒤의 좌우 입력.</summary>
        public int MoveX;
        public int MoveY;
        public bool WasGrounded;
    }

    /// <summary>
    /// 이동(02-movement.md). 한 틱은 두 단계다.
    /// Integrate: 입력 → 속도 → 지형 충돌 이동. 두 파이터 모두 같은 이전 상태에서 따로 계산한다.
    /// (그 사이에 FighterPush가 몸 겹침을 푼다.)
    /// ResolveContacts: 접지·벽 접촉 판정, 착지·튕김 처리, 착지 틱의 버퍼 점프, 행동 상태와 이벤트.
    /// </summary>
    public static class FighterMovement
    {
        public static void Integrate(ref FighterState f, in InputFrame input, SimConfig config, ArenaDef arena, int index, int tick, SimEventBuffer events, out MovementScratch scratch)
        {
            ref readonly var m = ref config.Movement;
            var pressed = InputFrame.Pressed(input.Buttons, f.PrevButtons);
            bool jumpHeld = input.Held(InputButtons.Jump);
            int moveX = input.MoveX;
            int moveY = input.MoveY;

            scratch = default;
            scratch.WasGrounded = f.Grounded;

            // 타이머
            if ((pressed & InputButtons.Jump) != 0)
                f.JumpBuffer = m.JumpBufferTicks;
            else if (f.JumpBuffer > 0)
                f.JumpBuffer--;
            if (f.DashCooldown > 0)
                f.DashCooldown--;
            if (f.WallJumpLockTicks > 0)
            {
                // 벽 점프 직후에는 떠난 벽 쪽으로 되돌아가는 입력만 무시한다.
                if (moveX == f.WallJumpLockDirection)
                    moveX = 0;
                f.WallJumpLockTicks--;
            }
            if (f.DropThroughTicks > 0 && --f.DropThroughTicks == 0)
                f.DropThroughBox = FighterState.NoBox;

            scratch.MoveX = moveX;
            scratch.MoveY = moveY;

            bool dashing = f.Action == ActionState.Dash && f.DashTicksLeft > 0;

            // 대시 시작
            if (!dashing && (pressed & InputButtons.Dash) != 0 && f.DashCharges > 0 && f.DashCooldown == 0)
            {
                var direction = moveX == 0 && moveY == 0 ? new Vec2(f.Facing, 0f) : SimMath.Normalize(new Vec2(moveX, moveY));
                f.DashDirection = direction;
                // 대시를 시작하는 틱은 아직 조작 가능한 상태이므로 대시 쪽을 바라본다. 대시 중에는 바꾸지 않는다.
                if (moveX != 0)
                    f.Facing = (sbyte)moveX;
                f.DashTicksLeft = m.DashTicks;
                f.DashCharges--;
                f.DashCooldown = m.DashCooldownTicks;
                f.DashFromGround = f.Grounded;
                f.JumpCutEligible = false;
                f.SetAction(ActionState.Dash);
                dashing = true;
                events.Add(new SimEvent(tick, SimEventType.Dash, index, f.Position, direction, m.DashSpeed));
            }

            if (dashing)
            {
                int elapsed = m.DashTicks - f.DashTicksLeft;
                bool cancel = m.DashJumpCancelEnabled && f.DashFromGround && f.Grounded
                              && elapsed >= m.DashJumpCancelMinTicks && f.JumpBuffer > 0;
                if (cancel)
                {
                    // 대시 점프 캔슬: 수평 속도는 그대로 두고 점프로 바꾼다.
                    f.DashTicksLeft = 0;
                    dashing = false;
                    f.Velocity = new Vec2(f.DashDirection.X * m.DashSpeed, 0f);
                    GroundJump(ref f, m, index, tick, events);
                    ApplyGravity(ref f, m, jumpHeld, moveX, moveY);
                }
                else
                {
                    f.Velocity = f.DashDirection * m.DashSpeed;
                }
            }
            else
            {
                bool apex = !f.Grounded && SimMath.Abs(f.Velocity.Y) < m.ApexSpeedThreshold && jumpHeld;
                ApplyHorizontal(ref f, m, moveX, apex);
                ResolveJumpRequest(ref f, m, arena, moveY, index, tick, events);
                ApplyGravity(ref f, m, jumpHeld, moveX, moveY);
            }

            TerrainCollision.Move(ref f.Position, ref f.Velocity, config.HalfBody, arena, m.SubstepMaxDistance, f.DropThroughBox, ref scratch.Contacts);

            if (dashing && --f.DashTicksLeft <= 0)
            {
                f.DashTicksLeft = 0;
                f.Velocity *= m.DashEndSpeedRetention;
            }
        }

        static void ApplyHorizontal(ref FighterState f, in MovementParams m, int moveX, bool apex)
        {
            const float dt = SimConfig.Dt;
            float vx = f.Velocity.X;
            if (moveX != 0)
            {
                int velocitySign = SimMath.Sign(vx);
                if (velocitySign == moveX && SimMath.Abs(vx) > m.MaxRunSpeed)
                {
                    // 최대 속도를 넘는 관성(대시 뒤, 대시 점프)은 같은 방향 입력이면 감속률로만 줄인다.
                    vx = SimMath.MoveTowards(vx, moveX * m.MaxRunSpeed, (f.Grounded ? m.GroundDecel : m.AirDecel) * dt);
                }
                else
                {
                    float accel = f.Grounded ? m.GroundAccel : m.AirAccel;
                    if (apex)
                        accel *= m.ApexAirAccelMultiplier;
                    if (velocitySign != 0 && velocitySign != moveX)
                        accel *= m.TurnAccelMultiplier;
                    vx = SimMath.MoveTowards(vx, moveX * m.MaxRunSpeed, accel * dt);
                }
            }
            else
            {
                vx = SimMath.MoveTowards(vx, 0f, (f.Grounded ? m.GroundDecel : m.AirDecel) * dt);
            }
            f.Velocity.X = vx;
        }

        /// <summary>점프 버퍼가 있으면 우선순위대로 처리한다: 발판 하강 → 땅·코요테 점프 → 벽 점프 → 공중 점프.</summary>
        static void ResolveJumpRequest(ref FighterState f, in MovementParams m, ArenaDef arena, int moveY, int index, int tick, SimEventBuffer events)
        {
            if (f.JumpBuffer <= 0)
                return;

            if (TryDropThrough(ref f, m, arena, moveY))
                return;
            if (f.Grounded || f.TicksSinceGrounded <= m.CoyoteTicks)
            {
                GroundJump(ref f, m, index, tick, events);
                return;
            }
            if (f.WallSide != 0 || f.TicksSinceWall <= m.WallCoyoteTicks)
            {
                WallJump(ref f, m, f.WallSide != 0 ? f.WallSide : f.LastWallSide, index, tick, events);
                return;
            }
            if (f.AirJumpsLeft > 0)
            {
                f.AirJumpsLeft--;
                f.Velocity.Y = m.AirJumpSpeed;
                f.JumpBuffer = 0;
                f.JumpCutEligible = true;
                events.Add(new SimEvent(tick, SimEventType.AirJump, index, f.Position, new Vec2(0f, 1f), m.AirJumpSpeed));
            }
        }

        /// <summary>단방향 발판 위에서 아래 + 점프면 그 발판을 잠시 무시하고 떨어진다.</summary>
        static bool TryDropThrough(ref FighterState f, in MovementParams m, ArenaDef arena, int moveY)
        {
            if (moveY >= 0 || !f.Grounded || f.GroundBox < 0 || arena.GetBox(f.GroundBox).Kind != TerrainKind.OneWay)
                return false;
            f.DropThroughBox = f.GroundBox;
            f.DropThroughTicks = m.DropThroughTicks;
            f.JumpBuffer = 0;
            f.Grounded = false;
            f.GroundBox = FighterState.NoBox;
            f.TicksSinceGrounded = m.CoyoteTicks + 1;
            return true;
        }

        static void GroundJump(ref FighterState f, in MovementParams m, int index, int tick, SimEventBuffer events)
        {
            f.Velocity.Y = m.JumpSpeed;
            f.Grounded = false;
            f.GroundBox = FighterState.NoBox;
            f.TicksSinceGrounded = m.CoyoteTicks + 1;
            f.JumpBuffer = 0;
            f.JumpCutEligible = true;
            events.Add(new SimEvent(tick, SimEventType.Jump, index, f.Position, new Vec2(0f, 1f), m.JumpSpeed));
        }

        static void WallJump(ref FighterState f, in MovementParams m, int wallSide, int index, int tick, SimEventBuffer events)
        {
            f.Velocity = new Vec2(-wallSide * m.WallJumpSpeedX, m.WallJumpSpeedY);
            f.Facing = (sbyte)-wallSide;
            f.WallJumpLockTicks = m.WallJumpInputLockTicks;
            f.WallJumpLockDirection = (sbyte)wallSide;
            f.TicksSinceWall = FighterState.TimerCap;
            f.AirJumpsLeft = m.AirJumps;
            f.DashCharges = m.DashCharges;
            f.JumpBuffer = 0;
            f.JumpCutEligible = true;
            events.Add(new SimEvent(tick, SimEventType.WallJump, index, f.Position, new Vec2(-wallSide, 1f), m.WallJumpSpeedY));
        }

        static void ApplyGravity(ref FighterState f, in MovementParams m, bool jumpHeld, int moveX, int moveY)
        {
            if (f.Grounded)
                return;
            const float dt = SimConfig.Dt;
            float vy = f.Velocity.Y;
            bool apex = SimMath.Abs(vy) < m.ApexSpeedThreshold && jumpHeld;
            float gravity = m.RiseGravity;
            if (vy > 0f)
            {
                if (f.JumpCutEligible && !jumpHeld)
                    gravity *= m.EarlyReleaseGravityMultiplier;
            }
            else
            {
                gravity *= m.FallGravityMultiplier;
            }
            if (apex)
                gravity *= m.ApexGravityMultiplier;
            bool fastFall = moveY < 0 && vy <= 0f;
            if (fastFall)
                gravity *= m.FastFallGravityMultiplier;
            float maxFall = fastFall ? m.FastFallMaxSpeed : m.MaxFallSpeed;
            vy = SimMath.Max(vy - gravity * dt, -maxFall);

            // 벽 미끄러짐: 공중, 벽에 닿음, 벽 쪽 입력, 내려가는 중
            if (f.WallSide != 0 && moveX == f.WallSide && vy < 0f)
                vy = SimMath.Max(vy, -m.WallSlideMaxSpeed);

            f.Velocity.Y = vy;
            if (vy <= 0f)
                f.JumpCutEligible = false;
        }

        public static void ResolveContacts(ref FighterState f, in FighterState other, in MovementScratch scratch, SimConfig config, ArenaDef arena, int index, int tick, SimEventBuffer events)
        {
            ref readonly var m = ref config.Movement;
            float half = config.HalfBody;
            bool dashing = f.Action == ActionState.Dash && f.DashTicksLeft > 0;
            ref readonly var contacts = ref scratch.Contacts;

            if (contacts.OutOfBounds)
                events.Add(new SimEvent(tick, SimEventType.OutOfBounds, index, f.Position, Vec2.Zero, 0f));

            bool bounced = false;
            if (contacts.Landed && contacts.LandedKind == TerrainKind.Bounce)
            {
                // 튕김 패드: 윗면에 닿으면 vy = 30, 공중 점프·대시 회복.
                f.Velocity.Y = m.BouncePadSpeed;
                f.AirJumpsLeft = m.AirJumps;
                f.DashCharges = m.DashCharges;
                f.JumpCutEligible = false;
                f.DashTicksLeft = 0;
                dashing = false;
                bounced = true;
                events.Add(new SimEvent(tick, SimEventType.PadBounce, index, f.Position, new Vec2(0f, 1f), m.BouncePadSpeed));
            }

            int ground = FighterState.NoBox;
            if (!bounced && f.Velocity.Y <= 0f)
            {
                ground = TerrainCollision.ProbeGround(f.Position, half, arena, m.GroundProbeDistance, f.DropThroughBox);
                if (ground == FighterState.NoBox && StandsOn(f.Position, other.Position, config))
                    ground = FighterState.OnFighter;
            }
            f.Grounded = ground != FighterState.NoBox;
            f.GroundBox = ground;
            f.WallSide = (sbyte)TerrainCollision.ProbeWall(f.Position, half, arena, m.GroundProbeDistance);

            if (f.Grounded)
            {
                if (!scratch.WasGrounded)
                {
                    float impact = contacts.Landed ? contacts.ImpactSpeed : -f.Velocity.Y;
                    events.Add(new SimEvent(tick, SimEventType.Land, index, f.Position, new Vec2(0f, -1f), impact));
                }
                if (f.Velocity.Y < 0f)
                    f.Velocity.Y = 0f;
                f.AirJumpsLeft = m.AirJumps;
                f.DashCharges = m.DashCharges;
                f.TicksSinceGrounded = 0;
                f.JumpCutEligible = false;

                // 착지 직전에 누른 점프(버퍼)는 착지한 틱에 실행한다.
                if (!scratch.WasGrounded && f.JumpBuffer > 0 && !dashing && !TryDropThrough(ref f, m, arena, scratch.MoveY))
                    GroundJump(ref f, m, index, tick, events);
            }
            else
            {
                f.TicksSinceGrounded = SimMath.Min(f.TicksSinceGrounded + 1, FighterState.TimerCap);
            }

            if (f.WallSide != 0 && !f.Grounded)
            {
                f.TicksSinceWall = 0;
                f.LastWallSide = f.WallSide;
                f.AirJumpsLeft = m.AirJumps;
                f.DashCharges = m.DashCharges;
            }
            else
            {
                f.TicksSinceWall = SimMath.Min(f.TicksSinceWall + 1, FighterState.TimerCap);
            }

            if (dashing)
            {
                f.SetAction(ActionState.Dash);
            }
            else if (f.Grounded)
            {
                f.SetAction(SimMath.Abs(f.Velocity.X) > SimMath.Epsilon || scratch.MoveX != 0 ? ActionState.Run : ActionState.Idle);
            }
            else if (f.WallSide != 0 && scratch.MoveX == f.WallSide && f.Velocity.Y < 0f)
            {
                if (f.Action != ActionState.WallSlide)
                    events.Add(new SimEvent(tick, SimEventType.WallSlideStart, index, f.Position, new Vec2(f.WallSide, 0f), -f.Velocity.Y));
                f.SetAction(ActionState.WallSlide);
            }
            else
            {
                f.SetAction(ActionState.Airborne);
            }

            if (!dashing && scratch.MoveX != 0)
                f.Facing = (sbyte)scratch.MoveX;
        }

        /// <summary>상대 머리 위(단방향 발판처럼)에 서 있는지.</summary>
        public static bool StandsOn(Vec2 self, Vec2 other, SimConfig config)
        {
            float size = config.Movement.BodySize;
            if (SimMath.Abs(self.X - other.X) >= size - TerrainCollision.Epsilon)
                return false;
            float gap = self.Y - other.Y - size;
            return gap >= -TerrainCollision.Epsilon && gap <= config.Movement.GroundProbeDistance;
        }
    }

    /// <summary>
    /// 파이터끼리의 몸 충돌(02-movement.md 7절). 겹치면 수평으로 절반씩 밀어낸다. 속도는 바꾸지 않는다.
    /// 직전 틱에 완전히 위에 있었고 내려오는 중이면 상대 머리 위에 세운다.
    /// 두 파이터를 같은 규칙으로 동시에 다루며, 번호로 우선순위를 정하지 않는다.
    /// </summary>
    public static class FighterPush
    {
        public static void Resolve(ref FighterState a, ref FighterState b, in FighterState prevA, in FighterState prevB, SimConfig config, ArenaDef arena)
        {
            float size = config.Movement.BodySize;
            float half = config.HalfBody;
            float dx = b.Position.X - a.Position.X;
            float dy = b.Position.Y - a.Position.Y;
            const float eps = TerrainCollision.Epsilon;
            if (SimMath.Abs(dx) >= size - eps || SimMath.Abs(dy) >= size - eps)
                return;

            bool aWasAbove = prevA.Position.Y - prevB.Position.Y >= size - eps && a.Velocity.Y <= 0f;
            bool bWasAbove = prevB.Position.Y - prevA.Position.Y >= size - eps && b.Velocity.Y <= 0f;
            if (aWasAbove)
            {
                a.Position.Y = b.Position.Y + size;
                a.Velocity.Y = 0f;
                return;
            }
            if (bWasAbove)
            {
                b.Position.Y = a.Position.Y + size;
                b.Velocity.Y = 0f;
                return;
            }

            int direction = SimMath.Sign(dx);
            if (direction == 0)
                direction = SimMath.Sign(prevB.Position.X - prevA.Position.X);
            if (direction == 0)
                return;
            float push = (size - SimMath.Abs(dx)) * 0.5f;
            a.Position.X -= direction * push;
            b.Position.X += direction * push;
            TerrainCollision.ResolveX(ref a.Position, -direction, half, arena);
            TerrainCollision.ResolveX(ref b.Position, direction, half, arena);
        }
    }
}

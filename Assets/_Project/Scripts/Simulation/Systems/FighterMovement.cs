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
    /// 전투 상태(공격, 가드, 경직)에서는 이동 규칙 대신 IntegrateCombatState가 속도를 정하고,
    /// 피격 경직 중 지형에 부딪히면 벽꽝·튕김(03-combat.md 3절)을 처리한다.
    /// </summary>
    public static class FighterMovement
    {
        /// <param name="pressed">이번 틱에 새로 눌린 버튼(히트스탑 동안 눌러 둔 버튼 포함). FighterCombat.BeginTick이 계산한다.</param>
        public static void Integrate(ref FighterState f, in InputFrame input, InputButtons pressed, SimConfig config, ArenaDef arena, int index, int tick, SimEventBuffer events, out MovementScratch scratch)
        {
            ref readonly var m = ref config.Movement;
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

            if (!f.InMovementState)
            {
                IntegrateCombatState(ref f, config, moveX, moveY, jumpHeld);
                TerrainCollision.Move(ref f.Position, ref f.Velocity, config.HalfBody, arena, m.SubstepMaxDistance, f.DropThroughBox, ref scratch.Contacts);
                return;
            }

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
                ApplyHorizontal(ref f, m, moveX, m.MaxRunSpeed, apex ? m.ApexAirAccelMultiplier : 1f, 1f);
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

        /// <summary>좌우 입력으로 maxSpeed까지 가속하거나 감속한다. 배율은 정점(1.2)과 공격 중 공중 조작(0.5)에 쓴다.</summary>
        static void ApplyHorizontal(ref FighterState f, in MovementParams m, int moveX, float maxSpeed, float accelMultiplier, float decelMultiplier)
        {
            const float dt = SimConfig.Dt;
            float vx = f.Velocity.X;
            float decel = (f.Grounded ? m.GroundDecel : m.AirDecel) * decelMultiplier;
            if (moveX != 0)
            {
                int velocitySign = SimMath.Sign(vx);
                if (velocitySign == moveX && SimMath.Abs(vx) > maxSpeed)
                {
                    // 최대 속도를 넘는 관성(대시 뒤, 대시 점프)은 같은 방향 입력이면 감속률로만 줄인다.
                    vx = SimMath.MoveTowards(vx, moveX * maxSpeed, decel * dt);
                }
                else
                {
                    float accel = f.Grounded ? m.GroundAccel : m.AirAccel;
                    accel *= accelMultiplier;
                    if (velocitySign != 0 && velocitySign != moveX)
                        accel *= m.TurnAccelMultiplier;
                    vx = SimMath.MoveTowards(vx, moveX * maxSpeed, accel * dt);
                }
            }
            else
            {
                vx = SimMath.MoveTowards(vx, 0f, decel * dt);
            }
            f.Velocity.X = vx;
        }

        /// <summary>
        /// 전투 상태의 속도. 가드: 최대 속도 GuardMaxSpeed, 점프·대시 불가. 공격: 지상은 감속만, 공중은 조작 × 0.5.
        /// 충전(모드 A): 최대 속도 3, 공중 낙하 최대 4. 돌진: 조작 없음, 공중에서 중력 × 0.3.
        /// 피격 경직: 조작 불가, 중력 정상, 공중 감속 0(날아가는 관성 유지). 그 밖의 경직: 조작 없이 감속.
        /// </summary>
        static void IntegrateCombatState(ref FighterState f, SimConfig config, int moveX, int moveY, bool jumpHeld)
        {
            const float dt = SimConfig.Dt;
            ref readonly var m = ref config.Movement;
            ref readonly var c = ref config.Combat;
            switch (f.Action)
            {
                case ActionState.Guard:
                    ApplyHorizontal(ref f, m, moveX, c.GuardMaxSpeed, 1f, 1f);
                    ApplyGravity(ref f, m, false, 0, 0);
                    break;
                case ActionState.Attack:
                case ActionState.Grab:
                case ActionState.Shoot:
                    if (f.Grounded)
                        f.Velocity.X = SimMath.MoveTowards(f.Velocity.X, 0f, m.GroundDecel * dt);
                    else
                        ApplyHorizontal(ref f, m, moveX, m.MaxRunSpeed, c.AttackAirControlMultiplier, c.AttackAirControlMultiplier);
                    ApplyGravity(ref f, m, jumpHeld, 0, moveY);
                    break;
                case ActionState.Hitstun:
                case ActionState.Thrown:
                    if (f.Grounded)
                        f.Velocity.X = SimMath.MoveTowards(f.Velocity.X, 0f, m.GroundDecel * dt);
                    ApplyGravity(ref f, m, false, 0, 0);
                    break;
                case ActionState.Holding when f.Form == FighterForm.Body:
                    // 몸 폼(5.2): 잡은 쪽은 느리게 걷고 중력을 받는다. 잡힌 쪽은 상호작용 판정에서 따라온다.
                    ApplyHorizontal(ref f, m, moveX, c.Body.HoldMoveSpeed, 1f, 1f);
                    ApplyGravity(ref f, m, false, 0, 0);
                    break;
                case ActionState.Holding:
                case ActionState.Held:
                    // 붙잡기 동안 둘 다 움직이지 않는다(공중이어도 떠 있음).
                    f.Velocity = Vec2.Zero;
                    break;
                case ActionState.Charge:
                    ApplyHorizontal(ref f, m, moveX, c.Charge.ChargeMoveMaxSpeed, 1f, 1f);
                    ApplyGravity(ref f, m, jumpHeld, 0, 0);
                    if (!f.Grounded && f.Velocity.Y < -c.Charge.ChargeFallMaxSpeed)
                        f.Velocity.Y = -c.Charge.ChargeFallMaxSpeed;
                    break;
                case ActionState.Lunge:
                    if (!f.Grounded)
                        f.Velocity.Y -= m.RiseGravity * c.Charge.LungeGravityMultiplier * dt;
                    if (c.Charge.LungeDrag > 0f)
                    {
                        float speed = SimMath.Length(f.Velocity);
                        if (speed > SimMath.Epsilon)
                            f.Velocity = f.Velocity * (SimMath.Max(0f, speed - c.Charge.LungeDrag * dt) / speed);
                    }
                    break;
                default:
                    f.Velocity.X = SimMath.MoveTowards(f.Velocity.X, 0f, (f.Grounded ? m.GroundDecel : m.AirDecel) * dt);
                    ApplyGravity(ref f, m, false, 0, 0);
                    break;
            }
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
            if (contacts.BoundaryX != 0 || contacts.BoundaryY != 0)
            {
                BoundaryHit(ref f, contacts, config, index, tick, events);
                dashing = false;
                bounced = true;
            }
            else if (contacts.Landed && contacts.LandedKind == TerrainKind.Bounce)
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
            else if (contacts.Landed && contacts.LandedKind == TerrainKind.Spike && CanTouchSpikes(f))
            {
                SpikeHit(ref f, config, index, tick, events);
                bounced = true;
            }
            else if (f.IsLaunched)
            {
                bounced = Rebound(ref f, contacts, config, index, tick, events);
            }
            else if (f.Action == ActionState.Lunge)
            {
                bounced = LungeBounce(ref f, contacts, config, index, tick, events);
            }

            int ground = FighterState.NoBox;
            if (!bounced && f.Velocity.Y <= 0f)
            {
                ground = TerrainCollision.ProbeGround(f.Position, half, arena, m.GroundProbeDistance, f.DropThroughBox);
                if (ground == FighterState.NoBox && f.OnProp > 0)
                    ground = FighterState.OnPropBox;
                if (ground == FighterState.NoBox && StandsOn(f.Position, other.Position, config))
                    ground = FighterState.OnFighter;
            }
            // 면역이 끝난 뒤 가시 위에 서 있으면 다시 닿은 것이다.
            if (ground >= 0 && arena.GetBox(ground).Kind == TerrainKind.Spike && CanTouchSpikes(f))
            {
                SpikeHit(ref f, config, index, tick, events);
                ground = FighterState.NoBox;
                bounced = true;
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
                if (!scratch.WasGrounded && f.JumpBuffer > 0 && !dashing && f.InMovementState && !TryDropThrough(ref f, m, arena, scratch.MoveY))
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

            // 모드 B: 공중에서 시작한 공격 도중 착지하면 공격이 끝나고 착지 경직.
            if (f.Action == ActionState.Attack && f.AttackFromAir && f.Grounded && !scratch.WasGrounded && f.Form == FighterForm.Paper)
            {
                f.RestartAction(ActionState.LandingLag);
                f.StunTicks = config.Combat.Swing.LandingLagTicks;
                f.AttackId = FighterState.NoAttack;
                f.AttackHasHit = false;
            }

            // 돌진은 속도가 12 미만이 되거나 30틱이 지나면 끝나고 이동 상태로 돌아간다.
            if (f.Action == ActionState.Lunge)
            {
                ref readonly var charge = ref config.Combat.Charge;
                if (f.Velocity.LengthSquared < charge.LungeEndSpeed * charge.LungeEndSpeed || f.ActionTick >= charge.LungeMaxTicks)
                    f.RestartAction(ActionState.Airborne);
            }

            if (!f.InMovementState)
                return;

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

        /// <summary>
        /// 피격 경직 중 지형에 부딪힘. 법선 속도가 WallSplatSpeed(14) 이상이면 벽꽝(데미지, 반사 × 0.45, 경직 +18, 히트스탑 6),
        /// BounceMinSpeed 이상이면 튕김(반사 × 0.35). 튕겼으면 true.
        /// 지형 이동이 부딪힌 축의 속도를 0으로 만들었으므로 부딪히기 직전 값으로 되돌려 반사한다.
        /// </summary>
        static bool Rebound(ref FighterState f, in MoveContacts contacts, SimConfig config, int index, int tick, SimEventBuffer events)
        {
            ref readonly var c = ref config.Combat;
            float half = config.HalfBody;
            float wall = contacts.HitWall != 0 ? contacts.WallImpactSpeed : 0f;
            float floor = contacts.Landed ? contacts.ImpactSpeed : 0f;
            float ceiling = contacts.HitCeiling ? contacts.CeilingImpactSpeed : 0f;
            float strongest = SimMath.Max(wall, SimMath.Max(floor, ceiling));
            if (strongest < c.BounceMinSpeed)
                return false;
            // 던져진 상태는 벽꽝 기준이 낮고(12) 데미지가 크다(10).
            bool thrown = f.Action == ActionState.Thrown;
            float splatSpeed = thrown ? c.Grab.ThrownSplatSpeed : c.WallSplatSpeed;
            int splatDamage = thrown ? c.Grab.ThrownSplatDamage : c.WallSplatDamage;

            var v = f.Velocity;
            if (contacts.HitWall != 0)
                v.X = -contacts.HitWall * wall;
            if (contacts.Landed)
                v.Y = floor;
            else if (contacts.HitCeiling)
                v.Y = -ceiling;

            Vec2 normal, point;
            if (wall >= floor && wall >= ceiling)
            {
                normal = new Vec2(-contacts.HitWall, 0f);
                point = new Vec2(f.Position.X + contacts.HitWall * half, f.Position.Y);
            }
            else if (floor >= ceiling)
            {
                normal = new Vec2(0f, 1f);
                point = new Vec2(f.Position.X, f.Position.Y - half);
            }
            else
            {
                normal = new Vec2(0f, -1f);
                point = new Vec2(f.Position.X, f.Position.Y + half);
            }

            bool splat = strongest >= splatSpeed;
            f.Velocity = v * (splat ? c.WallSplatRestitution : c.BounceRestitution);
            f.Grounded = false;
            f.GroundBox = FighterState.NoBox;
            if (splat && c.Body.Enabled)
            {
                // CR-004(5.2): 벽꽝 추가 피해는 충돌 속도에 비례하고, 한 번 날아가는 동안의 합이 상한을 넘지 않는다. 경직은 고정(누적하지 않음).
                int damage = SimMath.Min(c.Body.SplatDamageMax - f.SplatDamageTaken, (int)(strongest * c.Body.SplatDamagePerSpeed + 0.5f));
                damage = SimMath.Max(0, damage);
                f.SplatDamageTaken += damage;
                f.Health = SimMath.Max(0, f.Health - damage);
                f.StunTicks = SimMath.Max(f.StunTicks, c.Body.SplatHitstun);
                f.HitstopTicks = SimMath.Max(f.HitstopTicks, c.WallSplatHitstop);
                events.Add(new SimEvent(tick, SimEventType.WallSplat, index, point, normal, strongest));
            }
            else if (splat)
            {
                f.Health = SimMath.Max(0, f.Health - splatDamage);
                f.StunTicks += c.WallSplatHitstun;
                f.HitstopTicks = SimMath.Max(f.HitstopTicks, c.WallSplatHitstop);
                events.Add(new SimEvent(tick, SimEventType.WallSplat, index, point, normal, strongest));
            }
            else
            {
                events.Add(new SimEvent(tick, SimEventType.BounceRebound, index, point, normal, strongest));
            }
            return true;
        }

        /// <summary>
        /// 돌진 중 지형에 부딪힘: 법선 기준 반사 × 0.6, 최대 2회. 그 뒤로 부딪히면 돌진이 끝난다.
        /// 바닥을 미끄러지듯 스치는 접촉(법선 속도 BounceMinSpeed 미만)은 튕김으로 세지 않는다. 튕겼으면 true.
        /// </summary>
        static bool LungeBounce(ref FighterState f, in MoveContacts contacts, SimConfig config, int index, int tick, SimEventBuffer events)
        {
            ref readonly var c = ref config.Combat;
            float half = config.HalfBody;
            float wall = contacts.HitWall != 0 ? contacts.WallImpactSpeed : 0f;
            float floor = contacts.Landed ? contacts.ImpactSpeed : 0f;
            float ceiling = contacts.HitCeiling ? contacts.CeilingImpactSpeed : 0f;
            float strongest = SimMath.Max(wall, SimMath.Max(floor, ceiling));
            if (strongest < c.BounceMinSpeed)
                return false;
            if (f.LungeBounces >= c.Charge.LungeMaxBounces)
            {
                f.RestartAction(ActionState.Airborne);
                if (c.Charge.LungeStopRecoil <= 0f)
                    return false;
                return LungeStop(ref f, contacts, wall, floor, ceiling, strongest, config, index, tick, events);
            }

            var v = f.Velocity;
            if (contacts.HitWall != 0)
                v.X = -contacts.HitWall * wall;
            if (contacts.Landed)
                v.Y = floor;
            else if (contacts.HitCeiling)
                v.Y = -ceiling;
            f.Velocity = v * c.Charge.LungeBounceRestitution;
            f.LungeBounces++;
            f.Grounded = false;
            f.GroundBox = FighterState.NoBox;
            if (contacts.HitWall != 0)
                f.Facing = (sbyte)-contacts.HitWall;

            Vec2 normal, point;
            if (wall >= floor && wall >= ceiling)
            {
                normal = new Vec2(-contacts.HitWall, 0f);
                point = new Vec2(f.Position.X + contacts.HitWall * half, f.Position.Y);
            }
            else if (floor >= ceiling)
            {
                normal = new Vec2(0f, 1f);
                point = new Vec2(f.Position.X, f.Position.Y - half);
            }
            else
            {
                normal = new Vec2(0f, -1f);
                point = new Vec2(f.Position.X, f.Position.Y + half);
            }
            events.Add(new SimEvent(tick, SimEventType.BounceRebound, index, point, normal, strongest));
            return true;
        }

        /// <summary>
        /// CR-004: 튕김 대신 멈춤. 부딪힌 면의 법선 쪽으로 LungeStopRecoil만큼 작은 반동, 접선 속도는 LungeStopKeep만 남긴다.
        /// 바닥에 닿았으면 반동 없이 착지한다. 충격 연출용으로 BounceRebound 이벤트를 낸다.
        /// </summary>
        static bool LungeStop(ref FighterState f, in MoveContacts contacts, float wall, float floor, float ceiling, float strongest,
            SimConfig config, int index, int tick, SimEventBuffer events)
        {
            ref readonly var p = ref config.Combat.Charge;
            float half = config.HalfBody;
            var v = f.Velocity * p.LungeStopKeep;
            Vec2 normal, point;
            if (wall >= floor && wall >= ceiling)
            {
                normal = new Vec2(-contacts.HitWall, 0f);
                point = new Vec2(f.Position.X + contacts.HitWall * half, f.Position.Y);
                v.X = normal.X * p.LungeStopRecoil;
                f.Facing = (sbyte)-contacts.HitWall;
            }
            else if (floor >= ceiling)
            {
                normal = new Vec2(0f, 1f);
                point = new Vec2(f.Position.X, f.Position.Y - half);
                v.Y = 0f;
            }
            else
            {
                normal = new Vec2(0f, -1f);
                point = new Vec2(f.Position.X, f.Position.Y + half);
                v.Y = -p.LungeStopRecoil;
            }
            f.Velocity = v;
            events.Add(new SimEvent(tick, SimEventType.BounceRebound, index, point, normal, strongest));
            // 반사가 아니므로 땅 확인을 그대로 한다(바닥을 달리던 돌진이 벽에 멈추면 서 있다).
            return false;
        }

        /// <summary>면역 중이거나 붙잡기 중(둘이 함께 멈춰 있음)이면 가시에 닿지 않는다.</summary>
        static bool CanTouchSpikes(in FighterState f) =>
            f.SpikeImmunityTicks == 0 && f.Action != ActionState.Held && f.Action != ActionState.Holding;

        /// <summary>
        /// 가시: 데미지 15, vy = 20으로 튕겨 올림, 경직 20틱, 45틱 면역, 공중 점프·대시 회복(빠져나올 기회).
        /// 이미 날아가는 경직 중이면 연속 명중 수는 그대로 둔다.
        /// </summary>
        static void SpikeHit(ref FighterState f, SimConfig config, int index, int tick, SimEventBuffer events)
        {
            ref readonly var c = ref config.Combat;
            ref readonly var m = ref config.Movement;
            int combo = f.IsLaunched ? f.ComboHits : 0;
            f.Velocity = new Vec2(f.Velocity.X, c.SpikeBounceSpeed);
            f.Health = SimMath.Max(0, f.Health - c.SpikeDamage);
            f.StunTicks = c.SpikeHitstun;
            FighterCombat.EnterStun(ref f, ActionState.Hitstun);
            f.ComboHits = combo;
            f.SpikeImmunityTicks = c.SpikeImmunityTicks;
            f.AirJumpsLeft = m.AirJumps;
            f.DashCharges = m.DashCharges;
            f.Grounded = false;
            f.GroundBox = FighterState.NoBox;
            events.Add(new SimEvent(tick, SimEventType.Spike, index, new Vec2(f.Position.X, f.Position.Y - config.HalfBody), new Vec2(0f, 1f), c.SpikeDamage));
        }

        /// <summary>
        /// 경계 튕김(03-combat.md 3절, CR-002): 피해 clamp(round(4 + 법선 속도 × 0.35), 4, 15), 법선 속도를 반사해 × 1.1(안쪽으로 최소 14),
        /// 경직 14, 히트스탑 5, 면역 45틱. 면역 중에는 피해 없이 반사 × 0.5. 가드·패링은 적용하지 않는다.
        /// 잡고 있거나 잡힌 동안은 피해 없이 멈추기만 한다. 공중 점프·대시는 회복한다(아래 경계에서 빠져나올 수 있게, S-114).
        /// </summary>
        static void BoundaryHit(ref FighterState f, in MoveContacts contacts, SimConfig config, int index, int tick, SimEventBuffer events)
        {
            ref readonly var c = ref config.Combat;
            ref readonly var m = ref config.Movement;
            float half = config.HalfBody;
            var normal = SimMath.Normalize(new Vec2(-contacts.BoundaryX, -contacts.BoundaryY));
            var point = new Vec2(f.Position.X + contacts.BoundaryX * half, f.Position.Y + contacts.BoundaryY * half);
            float speed = SimMath.Max(contacts.BoundarySpeedX, contacts.BoundarySpeedY);
            if (f.Action == ActionState.Held || f.Action == ActionState.Holding)
            {
                if (contacts.BoundaryX != 0)
                    f.Velocity.X = 0f;
                if (contacts.BoundaryY != 0)
                    f.Velocity.Y = 0f;
                return;
            }
            bool immune = f.BoundaryImmunityTicks > 0;
            if (contacts.BoundaryX != 0)
                f.Velocity.X = -contacts.BoundaryX * BoundaryReboundSpeed(contacts.BoundarySpeedX, immune, c);
            if (contacts.BoundaryY != 0)
                f.Velocity.Y = -contacts.BoundaryY * BoundaryReboundSpeed(contacts.BoundarySpeedY, immune, c);
            int damage = 0;
            if (!immune)
            {
                damage = BoundaryDamage(speed, c);
                int combo = f.IsLaunched ? f.ComboHits : 0;
                f.Health = SimMath.Max(0, f.Health - damage);
                f.StunTicks = c.BoundaryHitstun;
                FighterCombat.EnterStun(ref f, ActionState.Hitstun);
                f.ComboHits = combo;
                f.HitstopTicks = SimMath.Max(f.HitstopTicks, c.BoundaryHitstop);
                f.BoundaryImmunityTicks = c.BoundaryImmunityTicks;
            }
            f.DashTicksLeft = 0;
            f.AirJumpsLeft = m.AirJumps;
            f.DashCharges = m.DashCharges;
            f.Grounded = false;
            f.GroundBox = FighterState.NoBox;
            events.Add(new SimEvent(tick, SimEventType.BoundaryHit, index, point, normal, damage));
        }

        public static int BoundaryDamage(float normalSpeed, in CombatParams c) =>
            SimMath.Clamp(SimMath.RoundToInt(c.BoundaryBaseDamage + normalSpeed * c.BoundaryDamagePerSpeed), c.BoundaryMinDamage, c.BoundaryMaxDamage);

        public static float BoundaryReboundSpeed(float normalSpeed, bool immune, in CombatParams c) =>
            immune ? normalSpeed * c.BoundaryImmuneRestitution : SimMath.Max(normalSpeed * c.BoundaryRestitution, c.BoundaryMinReboundSpeed);

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
    /// 히트스탑으로 멈춘 쪽은 움직이지 않고, 움직이는 쪽이 겹침을 모두 떠맡는다.
    /// 두 파이터를 같은 규칙으로 동시에 다루며, 번호로 우선순위를 정하지 않는다.
    /// </summary>
    public static class FighterPush
    {
        public static void Resolve(ref FighterState a, ref FighterState b, in FighterState prevA, in FighterState prevB, SimConfig config, ArenaDef arena)
            => Resolve(ref a, ref b, prevA, prevB, false, false, config, arena);

        public static void Resolve(ref FighterState a, ref FighterState b, in FighterState prevA, in FighterState prevB, bool frozenA, bool frozenB, SimConfig config, ArenaDef arena)
        {
            if (frozenA && frozenB)
                return;
            float size = config.Movement.BodySize;
            float half = config.HalfBody;
            float dx = b.Position.X - a.Position.X;
            float dy = b.Position.Y - a.Position.Y;
            const float eps = TerrainCollision.Epsilon;
            if (SimMath.Abs(dx) >= size - eps || SimMath.Abs(dy) >= size - eps)
                return;

            bool aWasAbove = !frozenA && prevA.Position.Y - prevB.Position.Y >= size - eps && a.Velocity.Y <= 0f;
            bool bWasAbove = !frozenB && prevB.Position.Y - prevA.Position.Y >= size - eps && b.Velocity.Y <= 0f;
            if (aWasAbove && TryStack(ref a, ref b, frozenB, size, half, arena))
                return;
            if (bWasAbove && TryStack(ref b, ref a, frozenA, size, half, arena))
                return;

            int direction = SimMath.Sign(dx);
            if (direction == 0)
                direction = SimMath.Sign(prevB.Position.X - prevA.Position.X);
            if (direction == 0)
                return;
            float overlap = size - SimMath.Abs(dx);
            if (!frozenA)
            {
                float fromX = a.Position.X;
                a.Position.X -= direction * (overlap * (frozenB ? 1f : 0.5f));
                TerrainCollision.ResolveX(ref a.Position, -direction, half, arena, fromX);
                TerrainCollision.ClampToBoundary(ref a.Position, half, arena);
            }
            if (!frozenB)
            {
                float fromX = b.Position.X;
                b.Position.X += direction * (overlap * (frozenA ? 1f : 0.5f));
                TerrainCollision.ResolveX(ref b.Position, direction, half, arena, fromX);
                TerrainCollision.ClampToBoundary(ref b.Position, half, arena);
            }
        }

        /// <summary>
        /// 위에서 내려온 파이터(top)를 아래 파이터 머리 위에 세운다(02-movement.md 7절). 아래 파이터가 솟아올라 위 파이터가
        /// 천장 속으로 들어가게 되면, 위 파이터는 천장 바로 아래에 두고 아래 파이터를 그만큼 내리며 위로 가던 속도를 멈춘다(CP9 QA F-1).
        /// 둘이 쌓일 높이가 없거나 아래 파이터가 히트스탑 중이면 false: 옆으로 밀어내는 규칙으로 넘긴다.
        /// </summary>
        static bool TryStack(ref FighterState top, ref FighterState bottom, bool bottomFrozen, float size, float half, ArenaDef arena)
        {
            var place = new Vec2(top.Position.X, bottom.Position.Y + size);
            if (arena == null || !TerrainCollision.ResolveCeiling(ref place, half, arena, top.Position.Y))
            {
                top.Position.Y = place.Y;
                top.Velocity.Y = 0f;
                return true;
            }
            var below = new Vec2(bottom.Position.X, place.Y - size);
            if (bottomFrozen || TerrainCollision.OverlapsSolid(below, half, arena))
                return false;
            top.Position.Y = place.Y;
            top.Velocity.Y = 0f;
            bottom.Position.Y = below.Y;
            if (bottom.Velocity.Y > 0f)
                bottom.Velocity.Y = 0f;
            return true;
        }
    }
}

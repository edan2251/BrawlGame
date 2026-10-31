namespace Knockbox.Simulation
{
    /// <summary>이동이 끝난 직후(몸 밀어내기 전) 두 파이터의 몸 접촉. 상호작용 판정의 몸 충돌에 쓴다.</summary>
    public struct BodyContact
    {
        public bool Touching;
        /// <summary>A → B 방향 단위 벡터. 위치가 완전히 같으면 (0, 1).</summary>
        public Vec2 Normal;
        public Vec2 Point;

        public static BodyContact Measure(in FighterState a, in FighterState b, SimConfig config)
        {
            float size = config.Movement.BodySize;
            float reach = size + config.Combat.Charge.ContactSlop;
            var d = b.Position - a.Position;
            var contact = new BodyContact
            {
                Touching = SimMath.Abs(d.X) < reach && SimMath.Abs(d.Y) < reach,
                Normal = SimMath.Normalize(d),
                Point = (a.Position + b.Position) * 0.5f,
            };
            if (contact.Normal.LengthSquared == 0f)
                contact.Normal = new Vec2(0f, 1f);
            return contact;
        }
    }

    /// <summary>몸 충돌 판정 결과.</summary>
    public enum BodyOutcome : byte
    {
        /// <summary>둘 다 위력 기준 미만: 밀어내기만.</summary>
        None,
        Clash,
        FirstWins,
        SecondWins,
    }

    /// <summary>
    /// 전투 공통 규칙(03-combat.md 3절)과 모드 A 돌진(4절).
    /// BeginTick: 각 파이터가 자기 상태와 입력만 보고 행동을 바꾼다(경직 진행, 공격 종료, 가드·패링, 공격·충전 시작, 돌진 발사).
    /// Freeze: 히트스탑 중인 파이터는 위치, 속도, 행동 타이머가 멈추고 새로 누른 버튼만 기록한다.
    /// ResolveInteractions: 이동이 끝난 두 파이터의 스냅숏 하나로 양쪽 판정(공격 판정 원, 몸 충돌 위력)을 모두 계산한 뒤
    /// 결과를 동시에 적용한다. 어느 단계에서도 파이터 번호로 우선순위를 정하지 않는다. 서로 동시에 맞히면 클래시다.
    /// </summary>
    public static class FighterCombat
    {
        /// <summary>히트스탑 중인 틱. 타이머를 하나 줄이고, 새로 누른 버튼을 히트스탑이 끝난 뒤로 미룬다.</summary>
        public static void Freeze(ref FighterState f, in InputFrame input, SimConfig config)
        {
            f.HitstopTicks--;
            var pressed = InputFrame.Pressed(input.Buttons, f.PrevButtons);
            f.PendingPresses |= pressed;
            if ((pressed & InputButtons.Jump) != 0)
                f.JumpBuffer = config.Movement.JumpBufferTicks;
        }

        /// <summary>
        /// 행동 전환. 이번 틱에 새로 눌린 버튼(히트스탑 동안 눌러 둔 것 포함)을 돌려준다.
        /// 경직이 끝나는 틱과 공격이 끝나는 틱에는 바로 다음 행동을 할 수 있다.
        /// </summary>
        public static InputButtons BeginTick(ref FighterState f, in InputFrame input, SimConfig config, int index, int tick, SimEventBuffer events)
        {
            ref readonly var c = ref config.Combat;
            var pressed = InputFrame.Pressed(input.Buttons, f.PrevButtons) | f.PendingPresses;
            f.PendingPresses = InputButtons.None;

            if (f.ParryTicks > 0)
                f.ParryTicks--;
            if (f.ContactCooldown > 0)
                f.ContactCooldown--;
            // 가드 입력은 조작할 수 없을 때 눌러도 센다(경직 중 연타로 패링을 준비하지 못하게).
            bool guardPressed = (pressed & InputButtons.Guard) != 0;
            int sinceGuardPress = SimMath.Min(f.TicksSinceGuardPress + 1, FighterState.TimerCap);
            f.TicksSinceGuardPress = guardPressed ? 0 : sinceGuardPress;

            switch (f.Action)
            {
                case ActionState.Hitstun:
                case ActionState.GuardStun:
                case ActionState.Stagger:
                case ActionState.Recoil:
                    if (f.StunTicks > 0)
                    {
                        f.StunTicks--;
                        return pressed;
                    }
                    if (f.Action == ActionState.Hitstun)
                        f.ComboHits = 0;
                    f.RestartAction(ActionState.Airborne);
                    break;
                case ActionState.Attack:
                    if (f.ActionTick < config.GetAttack(f.AttackId).TotalTicks)
                        return pressed;
                    f.AttackId = FighterState.NoAttack;
                    f.AttackHasHit = false;
                    f.RestartAction(ActionState.Airborne);
                    break;
                case ActionState.Charge:
                    // 누르고 있는 동안 충전. 떼거나 최대 충전 뒤 24틱이 지나면 발사.
                    if (input.Held(InputButtons.Attack) && f.ActionTick < c.Charge.FullChargeTicks + c.Charge.FullChargeHoldTicks)
                        return pressed;
                    Launch(ref f, input, config, index, tick, events);
                    return pressed;
                case ActionState.Lunge:
                case ActionState.KO:
                    return pressed;
            }

            if (f.Action == ActionState.Dash && f.DashTicksLeft > 0)
                return pressed;

            if (input.Held(InputButtons.Guard))
            {
                f.SetAction(ActionState.Guard);
                if (guardPressed && sinceGuardPress >= c.ParryCooldownTicks)
                {
                    f.ParryTicks = c.ParryWindowTicks;
                    events.Add(new SimEvent(tick, SimEventType.ParryWindow, index, f.Position, new Vec2(f.Facing, 0f), c.ParryWindowTicks));
                }
                return pressed;
            }
            if (f.Action == ActionState.Guard)
            {
                f.ParryTicks = 0;
                f.RestartAction(ActionState.Airborne);
            }

            if ((pressed & InputButtons.Attack) != 0)
            {
                if (config.Mode == CombatMode.Charge)
                    StartCharge(ref f, input.MoveX);
                else
                    StartAttack(ref f, input.MoveX, 0, index, tick, events);
            }
            return pressed;
        }

        static void StartAttack(ref FighterState f, int moveX, int attackId, int index, int tick, SimEventBuffer events)
        {
            // 공격을 시작하는 틱은 아직 조작 가능한 상태이므로 입력 쪽을 바라본다. 공격 중에는 바꾸지 않는다.
            if (moveX != 0)
                f.Facing = (sbyte)moveX;
            f.AttackId = attackId;
            f.AttackHasHit = false;
            f.DashTicksLeft = 0;
            f.RestartAction(ActionState.Attack);
            events.Add(new SimEvent(tick, SimEventType.AttackStart, index, f.Position, new Vec2(f.Facing, 0f), attackId));
        }

        static void StartCharge(ref FighterState f, int moveX)
        {
            if (moveX != 0)
                f.Facing = (sbyte)moveX;
            f.DashTicksLeft = 0;
            f.RestartAction(ActionState.Charge);
        }

        /// <summary>
        /// 돌진 발사: 충전 틱(행동 틱)에 따라 18 → 34, 방향은 이번 틱의 8방향 입력(중립이면 바라보는 쪽).
        /// </summary>
        static void Launch(ref FighterState f, in InputFrame input, SimConfig config, int index, int tick, SimEventBuffer events)
        {
            float speed = config.Combat.Charge.LungeSpeed(f.ActionTick);
            var direction = input.MoveX == 0 && input.MoveY == 0
                ? new Vec2(f.Facing, 0f)
                : SimMath.Normalize(new Vec2(input.MoveX, input.MoveY));
            if (input.MoveX != 0)
                f.Facing = (sbyte)input.MoveX;
            f.Velocity = direction * speed;
            f.LungeBounces = 0;
            f.JumpCutEligible = false;
            if (f.Velocity.Y > 0f)
            {
                f.Grounded = false;
                f.GroundBox = FighterState.NoBox;
            }
            f.RestartAction(ActionState.Lunge);
            events.Add(new SimEvent(tick, SimEventType.LungeStart, index, f.Position, direction, speed));
        }

        /// <summary>공격 판정이 활성 틱인지.</summary>
        public static bool IsHitboxActive(in FighterState f, SimConfig config)
        {
            if (f.Action != ActionState.Attack || f.AttackId == FighterState.NoAttack)
                return false;
            ref readonly var attack = ref config.GetAttack(f.AttackId);
            int k = f.ActionTick - attack.StartupTicks;
            return k >= 0 && k < attack.ActiveTicks;
        }

        /// <summary>활성 판정 원의 월드 위치. 활성 첫 틱에 Start, 마지막 틱에 End.</summary>
        public static Vec2 CircleCenter(in FighterState f, in AttackDef attack, in HitCircle circle)
        {
            int k = f.ActionTick - attack.StartupTicks;
            float t = attack.ActiveTicks > 1 ? (float)k / (attack.ActiveTicks - 1) : 0f;
            var offset = circle.Start + (circle.End - circle.Start) * t;
            return new Vec2(f.Position.X + offset.X * f.Facing, f.Position.Y + offset.Y);
        }

        /// <summary>몸 충돌의 공격성 가중치: 모드 A에서 돌진 1.0, 대시 0.6, 그 외 0.</summary>
        public static float Aggression(in FighterState f, SimConfig config)
        {
            if (config.Mode != CombatMode.Charge)
                return 0f;
            if (f.Action == ActionState.Lunge)
                return config.Combat.Charge.LungeAggression;
            if (f.Action == ActionState.Dash && f.DashTicksLeft > 0)
                return config.Combat.Charge.DashAggression;
            return 0f;
        }

        /// <summary>법선 쪽 속도 × 가중치. 상대에게서 멀어지는 속도는 0.</summary>
        public static float Power(in FighterState f, Vec2 towardOther, SimConfig config) =>
            SimMath.Max(0f, Vec2.Dot(f.Velocity, towardOther)) * Aggression(f, config);

        /// <summary>공격을 받을 수 있는지. KO, 그리고 연속 명중 상한에 닿은 피격 경직 중에는 맞지 않는다.</summary>
        public static bool CanBeHit(in FighterState f, SimConfig config) =>
            f.Action != ActionState.KO && !(f.Action == ActionState.Hitstun && f.ComboHits >= config.Combat.MaxComboHits);

        static bool TouchesBody(in FighterState attacker, SimConfig config, Vec2 target, out Vec2 point)
        {
            ref readonly var attack = ref config.GetAttack(attacker.AttackId);
            float half = config.HalfBody;
            for (int i = 0; i < attack.CircleCount; i++)
            {
                var circle = attack.GetCircle(i);
                var center = CircleCenter(attacker, attack, circle);
                var closest = new Vec2(
                    SimMath.Clamp(center.X, target.X - half, target.X + half),
                    SimMath.Clamp(center.Y, target.Y - half, target.Y + half));
                if ((closest - center).LengthSquared <= circle.Radius * circle.Radius)
                {
                    point = closest;
                    return true;
                }
            }
            point = default;
            return false;
        }

        static bool CirclesTouch(in FighterState a, in FighterState b, SimConfig config, out Vec2 point)
        {
            ref readonly var attackA = ref config.GetAttack(a.AttackId);
            ref readonly var attackB = ref config.GetAttack(b.AttackId);
            for (int i = 0; i < attackA.CircleCount; i++)
            {
                var circleA = attackA.GetCircle(i);
                var centerA = CircleCenter(a, attackA, circleA);
                for (int j = 0; j < attackB.CircleCount; j++)
                {
                    var circleB = attackB.GetCircle(j);
                    var centerB = CircleCenter(b, attackB, circleB);
                    float reach = circleA.Radius + circleB.Radius;
                    if ((centerB - centerA).LengthSquared <= reach * reach)
                    {
                        point = (centerA + centerB) * 0.5f;
                        return true;
                    }
                }
            }
            point = default;
            return false;
        }

        /// <summary>
        /// 상호작용 판정. 이동 후 스냅숏에서 양쪽 공격을 모두 계산한다.
        /// 공격 판정 원: 판정 원끼리 닿거나 서로의 몸을 동시에 맞히면 클래시, 한쪽만 맞히면 받는 쪽 상태에 따라 패링·가드·명중.
        /// 몸 충돌(모드 A): 위력이 둘 다 기준 미만이면 밀어내기만, 둘 다 기준 이상이고 차이가 작으면 클래시, 그 외 높은 쪽 승리.
        /// 결과가 서로 배타적이므로 스냅숏에서 계산해 적용하면 동시 적용과 같다.
        /// </summary>
        public static void ResolveInteractions(ref FighterState a, ref FighterState b, bool frozenA, bool frozenB, in BodyContact contact, SimConfig config, int tick, SimEventBuffer events)
        {
            var snapA = a;
            var snapB = b;
            if (ResolveHitboxes(ref a, ref b, snapA, snapB, frozenA, frozenB, config, tick, events))
                return;
            if (!frozenA && !frozenB && contact.Touching)
                ResolveBodies(ref a, ref b, snapA, snapB, contact, config, tick, events);
        }

        public static void ResolveInteractions(ref FighterState a, ref FighterState b, bool frozenA, bool frozenB, SimConfig config, int tick, SimEventBuffer events)
            => ResolveInteractions(ref a, ref b, frozenA, frozenB, default, config, tick, events);

        static bool ResolveHitboxes(ref FighterState a, ref FighterState b, in FighterState snapA, in FighterState snapB, bool frozenA, bool frozenB, SimConfig config, int tick, SimEventBuffer events)
        {
            bool activeA = !frozenA && !snapA.AttackHasHit && IsHitboxActive(snapA, config);
            bool activeB = !frozenB && !snapB.AttackHasHit && IsHitboxActive(snapB, config);
            if (!activeA && !activeB)
                return false;

            Vec2 pointAB = default, pointBA = default;
            bool aHitsB = activeA && CanBeHit(snapB, config) && TouchesBody(snapA, config, snapB.Position, out pointAB);
            bool bHitsA = activeB && CanBeHit(snapA, config) && TouchesBody(snapB, config, snapA.Position, out pointBA);
            if (activeA && activeB && CirclesTouch(snapA, snapB, config, out var clashPoint))
            {
                Clash(ref a, ref b, snapA, snapB, clashPoint, config.Combat.ClashSpeed, config, tick, events);
                return true;
            }
            if (aHitsB && bHitsA)
            {
                Clash(ref a, ref b, snapA, snapB, (pointAB + pointBA) * 0.5f, config.Combat.ClashSpeed, config, tick, events);
                return true;
            }
            if (aHitsB)
            {
                HitboxStrike(ref a, ref b, snapA, snapB, pointAB, 1, config, tick, events);
                return true;
            }
            if (bHitsA)
            {
                HitboxStrike(ref b, ref a, snapB, snapA, pointBA, 0, config, tick, events);
                return true;
            }
            return false;
        }

        static void HitboxStrike(ref FighterState attacker, ref FighterState defender, in FighterState snapAttacker, in FighterState snapDefender, Vec2 point, int defenderIndex, SimConfig config, int tick, SimEventBuffer events)
        {
            ref readonly var attack = ref config.GetAttack(snapAttacker.AttackId);
            var direction = new Vec2(attack.KnockbackDirection.X * snapAttacker.Facing, attack.KnockbackDirection.Y);
            attacker.AttackHasHit = true;
            var strike = new Strike
            {
                Damage = attack.Damage,
                Direction = direction,
                Knockback = attack.KnockbackStrength,
                Point = point,
            };
            ApplyStrike(ref attacker, ref defender, snapAttacker, snapDefender, strike, defenderIndex, config, tick, events);
        }

        /// <summary>
        /// 모드 A 몸 충돌(03-combat.md 4절). n = A → B, 위력A = max(0, A.vel·n) × 가중치A, 위력B = max(0, B.vel·(-n)) × 가중치B.
        /// 같은 두 파이터 사이의 판정은 결과가 난 뒤 10틱 동안 다시 하지 않는다.
        /// </summary>
        static void ResolveBodies(ref FighterState a, ref FighterState b, in FighterState snapA, in FighterState snapB, in BodyContact contact, SimConfig config, int tick, SimEventBuffer events)
        {
            ref readonly var p = ref config.Combat.Charge;
            if (snapA.ContactCooldown > 0 || snapB.ContactCooldown > 0)
                return;
            var n = contact.Normal;
            float powerA = Power(snapA, n, config);
            float powerB = Power(snapB, -n, config);
            if (powerA <= 0f && powerB <= 0f)
                return;
            events.Add(new SimEvent(tick, SimEventType.PowerCheck, 0, snapA.Position, n, powerA));
            events.Add(new SimEvent(tick, SimEventType.PowerCheck, 1, snapB.Position, -n, powerB));

            switch (JudgeBodies(powerA, powerB, p))
            {
                case BodyOutcome.Clash:
                    float speed = SimMath.Max(config.Combat.ClashSpeed, SimMath.Max(powerA, powerB) * p.ClashSpeedPerPower);
                    Clash(ref a, ref b, snapA, snapB, contact.Point, speed, config, tick, events);
                    a.ContactCooldown = p.ContactCooldownTicks;
                    b.ContactCooldown = p.ContactCooldownTicks;
                    break;
                case BodyOutcome.FirstWins:
                    BodyStrike(ref a, ref b, snapA, snapB, n, powerA, contact.Point, 1, config, tick, events);
                    break;
                case BodyOutcome.SecondWins:
                    BodyStrike(ref b, ref a, snapB, snapA, -n, powerB, contact.Point, 0, config, tick, events);
                    break;
            }
        }

        /// <summary>
        /// 둘 다 위력 6 미만이면 밀어내기만, 둘 다 6 이상이고 차이가 4 미만이면 클래시, 그 외 위력이 높은 쪽 승리.
        /// 위력이 같으면 앞의 두 규칙 중 하나에 걸리므로 번호로 승자를 정하는 일이 없다.
        /// </summary>
        public static BodyOutcome JudgeBodies(float powerA, float powerB, in ChargeModeParams p)
        {
            bool strongA = powerA >= p.PowerThreshold;
            bool strongB = powerB >= p.PowerThreshold;
            if (!strongA && !strongB)
                return BodyOutcome.None;
            if (strongA && strongB && SimMath.Abs(powerA - powerB) < p.ClashPowerMargin)
                return BodyOutcome.Clash;
            return powerA > powerB ? BodyOutcome.FirstWins : BodyOutcome.SecondWins;
        }

        /// <summary>
        /// 이긴 쪽 W가 진 쪽 L에게 주는 것: 데미지 clamp(round(위력 × 0.6), 4, 24),
        /// 넉백 방향 normalize(0.5 × normalize(W.vel) + 0.5 × n(W → L)), 세기 max(10, 위력 × 0.9).
        /// </summary>
        public static void BodyStrikeValues(Vec2 winnerVelocity, Vec2 towardLoser, float power, in ChargeModeParams p, out int damage, out Vec2 direction, out float knockback)
        {
            damage = SimMath.Clamp(SimMath.RoundToInt(power * p.DamagePerPower), p.MinDamage, p.MaxDamage);
            direction = SimMath.Normalize(SimMath.Normalize(winnerVelocity) * 0.5f + towardLoser * 0.5f);
            if (direction.LengthSquared == 0f)
                direction = towardLoser;
            knockback = SimMath.Max(p.MinKnockback, power * p.KnockbackPerPower);
        }

        static void BodyStrike(ref FighterState winner, ref FighterState loser, in FighterState snapWinner, in FighterState snapLoser, Vec2 towardLoser, float power, Vec2 point, int loserIndex, SimConfig config, int tick, SimEventBuffer events)
        {
            ref readonly var p = ref config.Combat.Charge;
            if (!CanBeHit(snapLoser, config))
                return;
            BodyStrikeValues(snapWinner.Velocity, towardLoser, power, p, out int damage, out var direction, out float knockback);
            var strike = new Strike
            {
                Damage = damage,
                Direction = direction,
                Knockback = knockback,
                Point = point,
                Body = true,
                ContactNormal = towardLoser,
            };
            ApplyStrike(ref winner, ref loser, snapWinner, snapLoser, strike, loserIndex, config, tick, events);
            winner.ContactCooldown = p.ContactCooldownTicks;
            loser.ContactCooldown = p.ContactCooldownTicks;
        }

        /// <summary>한쪽이 다른 쪽을 친 결과. 판정 원 공격과 몸 충돌이 같은 적용 규칙을 쓴다.</summary>
        struct Strike
        {
            public int Damage;
            public Vec2 Direction;
            public float Knockback;
            public Vec2 Point;
            /// <summary>몸 충돌(돌진·대시)이면 이긴 쪽이 튕겨 나오고 돌진이 끝나며, 패링당하면 반사된다.</summary>
            public bool Body;
            /// <summary>몸 충돌의 접촉 법선(이긴 쪽 → 진 쪽).</summary>
            public Vec2 ContactNormal;
        }

        static void ApplyStrike(ref FighterState attacker, ref FighterState defender, in FighterState snapAttacker, in FighterState snapDefender, in Strike strike, int defenderIndex, SimConfig config, int tick, SimEventBuffer events)
        {
            ref readonly var c = ref config.Combat;

            if (snapDefender.Action == ActionState.Guard && snapDefender.ParryTicks > 0)
            {
                // 패링: 공격한 쪽은 비틀거린다. 판정 원 공격은 반대로 8 밀려나고, 돌진은 접촉 법선 기준 반사 × 0.8.
                int away = SimMath.Sign(snapAttacker.Position.X - snapDefender.Position.X);
                if (away == 0)
                    away = -snapAttacker.Facing;
                if (strike.Body)
                {
                    var v = snapAttacker.Velocity;
                    var nrm = strike.ContactNormal;
                    attacker.Velocity = (v - nrm * (2f * Vec2.Dot(v, nrm))) * c.Charge.ParryReflectScale;
                }
                else
                {
                    attacker.Velocity = new Vec2(away * c.StaggerPushSpeed, 0f);
                }
                attacker.StunTicks = c.StaggerTicks;
                EnterStun(ref attacker, ActionState.Stagger);
                defender.ParryTicks = 0;
                attacker.HitstopTicks = c.ParryHitstop;
                defender.HitstopTicks = c.ParryHitstop;
                events.Add(new SimEvent(tick, SimEventType.Parry, defenderIndex, strike.Point, new Vec2(away, 0f), strike.Damage));
                return;
            }

            int hitstop = HitstopFor(strike.Damage, c);
            if (snapDefender.IsGuarding)
            {
                // 가드: 데미지 × 0.2, 넉백 × 0.35(수평만), 가드 경직, 히트스탑 절반.
                int damage = SimMath.Max(1, SimMath.RoundToInt(strike.Damage * c.GuardDamageMultiplier));
                defender.Health = SimMath.Max(0, defender.Health - damage);
                defender.Velocity.X = strike.Direction.X * (strike.Knockback * c.GuardKnockbackMultiplier);
                defender.StunTicks = c.GuardStunTicks;
                EnterStun(ref defender, ActionState.GuardStun);
                int guardHitstop = SimMath.RoundToInt(hitstop * c.GuardHitstopMultiplier);
                attacker.HitstopTicks = guardHitstop;
                defender.HitstopTicks = guardHitstop;
                if (strike.Body)
                    Rebound(ref attacker, snapAttacker, config);
                events.Add(new SimEvent(tick, SimEventType.GuardHit, defenderIndex, strike.Point, strike.Direction, damage));
                return;
            }

            // 명중: 넉백은 속도를 대체한다. 경직이 풀리기 전에 다시 맞으면 넉백 × 0.85^n, 경직 × 0.8^n.
            int n = snapDefender.Action == ActionState.Hitstun ? snapDefender.ComboHits : 0;
            float knockback = strike.Knockback * SimMath.Pow(c.ComboKnockbackDecay, n);
            defender.Velocity = strike.Direction * knockback;
            defender.Health = SimMath.Max(0, defender.Health - strike.Damage);
            defender.StunTicks = HitstunFor(knockback, n, c);
            defender.ComboHits = n + 1;
            EnterStun(ref defender, ActionState.Hitstun);
            attacker.HitstopTicks = hitstop;
            defender.HitstopTicks = hitstop;
            if (strike.Body)
                Rebound(ref attacker, snapAttacker, config);
            events.Add(new SimEvent(tick, SimEventType.Hit, defenderIndex, strike.Point, strike.Direction, strike.Damage));
        }

        /// <summary>몸 충돌에서 이긴 쪽: 속도 × -0.25로 살짝 튕겨 나오고 돌진(대시)이 끝난다.</summary>
        static void Rebound(ref FighterState winner, in FighterState snapWinner, SimConfig config)
        {
            winner.Velocity = snapWinner.Velocity * config.Combat.Charge.WinnerVelocityScale;
            winner.DashTicksLeft = 0;
            if (winner.Action == ActionState.Lunge || winner.Action == ActionState.Dash)
                winner.RestartAction(ActionState.Airborne);
        }

        static void Clash(ref FighterState a, ref FighterState b, in FighterState snapA, in FighterState snapB, Vec2 point, float speed, SimConfig config, int tick, SimEventBuffer events)
        {
            ref readonly var c = ref config.Combat;
            var n = SimMath.Normalize(snapB.Position - snapA.Position);
            if (n.LengthSquared == 0f)
                n = new Vec2(0f, 1f);
            a.Velocity = -n * speed;
            b.Velocity = n * speed;
            a.StunTicks = c.RecoilTicks;
            b.StunTicks = c.RecoilTicks;
            EnterStun(ref a, ActionState.Recoil);
            EnterStun(ref b, ActionState.Recoil);
            a.HitstopTicks = c.ClashHitstop;
            b.HitstopTicks = c.ClashHitstop;
            events.Add(new SimEvent(tick, SimEventType.Clash, SimEvent.NoFighter, point, n, speed));
        }

        /// <summary>경직 계열 상태로 들어간다. 진행 중이던 공격, 충전·돌진, 패링 구간, 대시를 끊는다. StunTicks는 부르는 쪽이 정한다.</summary>
        static void EnterStun(ref FighterState f, ActionState action)
        {
            f.RestartAction(action);
            f.AttackId = FighterState.NoAttack;
            f.AttackHasHit = false;
            f.ParryTicks = 0;
            f.DashTicksLeft = 0;
            f.JumpCutEligible = false;
            if (f.Velocity.Y > 0f)
            {
                f.Grounded = false;
                f.GroundBox = FighterState.NoBox;
            }
        }

        /// <summary>일반 명중 히트스탑: clamp(4 + 데미지 × 0.25, 4, 12), 반올림.</summary>
        public static int HitstopFor(int damage, in CombatParams c) =>
            SimMath.Clamp(SimMath.RoundToInt(c.HitstopBase + damage * c.HitstopPerDamage), c.HitstopMin, c.HitstopMax);

        /// <summary>피격 경직: clamp(round(넉백 × 1.1), 8, 45) × 0.8^n, 반올림(최소 1). 넉백은 이미 감쇠한 값.</summary>
        public static int HitstunFor(float knockback, int comboIndex, in CombatParams c)
        {
            int stun = SimMath.Clamp(SimMath.RoundToInt(knockback * c.HitstunPerKnockback), c.HitstunMin, c.HitstunMax);
            return SimMath.Max(1, SimMath.RoundToInt(stun * SimMath.Pow(c.ComboHitstunDecay, comboIndex)));
        }
    }
}

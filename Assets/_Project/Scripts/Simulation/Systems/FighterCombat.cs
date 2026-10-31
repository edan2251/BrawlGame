namespace Knockbox.Simulation
{
    /// <summary>
    /// 전투 공통 규칙(03-combat.md 3절).
    /// BeginTick: 각 파이터가 자기 상태와 입력만 보고 행동을 바꾼다(경직 진행, 공격 종료, 가드·패링, 공격 시작).
    /// Freeze: 히트스탑 중인 파이터는 위치, 속도, 행동 타이머가 멈추고 새로 누른 버튼만 기록한다.
    /// ResolveInteractions: 이동이 끝난 두 파이터의 스냅숏 하나로 양쪽 판정을 모두 계산한 뒤 결과를 동시에 적용한다.
    /// 어느 단계에서도 파이터 번호로 우선순위를 정하지 않는다. 서로 동시에 맞히면 클래시다.
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
                StartAttack(ref f, input.MoveX, 0, index, tick, events);
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
        /// 판정 원끼리 닿거나 서로의 몸을 동시에 맞히면 클래시, 한쪽만 맞히면 받는 쪽 상태에 따라 패링·가드·명중.
        /// 결과가 서로 배타적이므로 스냅숏에서 계산해 적용하면 동시 적용과 같다.
        /// </summary>
        public static void ResolveInteractions(ref FighterState a, ref FighterState b, bool frozenA, bool frozenB, SimConfig config, int tick, SimEventBuffer events)
        {
            var snapA = a;
            var snapB = b;
            bool activeA = !frozenA && !snapA.AttackHasHit && IsHitboxActive(snapA, config);
            bool activeB = !frozenB && !snapB.AttackHasHit && IsHitboxActive(snapB, config);
            if (!activeA && !activeB)
                return;

            Vec2 pointAB = default, pointBA = default;
            bool aHitsB = activeA && CanBeHit(snapB, config) && TouchesBody(snapA, config, snapB.Position, out pointAB);
            bool bHitsA = activeB && CanBeHit(snapA, config) && TouchesBody(snapB, config, snapA.Position, out pointBA);
            if (activeA && activeB && CirclesTouch(snapA, snapB, config, out var clashPoint))
            {
                Clash(ref a, ref b, snapA, snapB, clashPoint, config, tick, events);
                return;
            }
            if (aHitsB && bHitsA)
            {
                Clash(ref a, ref b, snapA, snapB, (pointAB + pointBA) * 0.5f, config, tick, events);
                return;
            }
            if (aHitsB)
                Connect(ref a, ref b, snapA, snapB, pointAB, 1, config, tick, events);
            else if (bHitsA)
                Connect(ref b, ref a, snapB, snapA, pointBA, 0, config, tick, events);
        }

        static void Connect(ref FighterState attacker, ref FighterState defender, in FighterState snapAttacker, in FighterState snapDefender, Vec2 point, int defenderIndex, SimConfig config, int tick, SimEventBuffer events)
        {
            ref readonly var c = ref config.Combat;
            ref readonly var attack = ref config.GetAttack(snapAttacker.AttackId);
            var direction = new Vec2(attack.KnockbackDirection.X * snapAttacker.Facing, attack.KnockbackDirection.Y);
            attacker.AttackHasHit = true;

            if (snapDefender.Action == ActionState.Guard && snapDefender.ParryTicks > 0)
            {
                // 패링: 공격한 쪽은 비틀거리며 반대로 밀려난다. 패링한 쪽은 히트스탑 직후 바로 행동할 수 있다.
                int away = SimMath.Sign(snapAttacker.Position.X - snapDefender.Position.X);
                if (away == 0)
                    away = -snapAttacker.Facing;
                attacker.Velocity = new Vec2(away * c.StaggerPushSpeed, 0f);
                attacker.StunTicks = c.StaggerTicks;
                EnterStun(ref attacker, ActionState.Stagger);
                defender.ParryTicks = 0;
                attacker.HitstopTicks = c.ParryHitstop;
                defender.HitstopTicks = c.ParryHitstop;
                events.Add(new SimEvent(tick, SimEventType.Parry, defenderIndex, point, new Vec2(away, 0f), attack.Damage));
                return;
            }

            int hitstop = HitstopFor(attack.Damage, c);
            if (snapDefender.IsGuarding)
            {
                // 가드: 데미지 × 0.2, 넉백 × 0.35(수평만), 가드 경직, 히트스탑 절반.
                int damage = SimMath.Max(1, SimMath.RoundToInt(attack.Damage * c.GuardDamageMultiplier));
                defender.Health = SimMath.Max(0, defender.Health - damage);
                defender.Velocity.X = direction.X * (attack.KnockbackStrength * c.GuardKnockbackMultiplier);
                defender.StunTicks = c.GuardStunTicks;
                EnterStun(ref defender, ActionState.GuardStun);
                int guardHitstop = SimMath.RoundToInt(hitstop * c.GuardHitstopMultiplier);
                attacker.HitstopTicks = guardHitstop;
                defender.HitstopTicks = guardHitstop;
                events.Add(new SimEvent(tick, SimEventType.GuardHit, defenderIndex, point, direction, damage));
                return;
            }

            // 명중: 넉백은 속도를 대체한다. 경직이 풀리기 전에 다시 맞으면 넉백 × 0.85^n, 경직 × 0.8^n.
            int n = snapDefender.Action == ActionState.Hitstun ? snapDefender.ComboHits : 0;
            float knockback = attack.KnockbackStrength * SimMath.Pow(c.ComboKnockbackDecay, n);
            defender.Velocity = direction * knockback;
            defender.Health = SimMath.Max(0, defender.Health - attack.Damage);
            defender.StunTicks = HitstunFor(knockback, n, c);
            defender.ComboHits = n + 1;
            EnterStun(ref defender, ActionState.Hitstun);
            attacker.HitstopTicks = hitstop;
            defender.HitstopTicks = hitstop;
            events.Add(new SimEvent(tick, SimEventType.Hit, defenderIndex, point, direction, attack.Damage));
        }

        static void Clash(ref FighterState a, ref FighterState b, in FighterState snapA, in FighterState snapB, Vec2 point, SimConfig config, int tick, SimEventBuffer events)
        {
            ref readonly var c = ref config.Combat;
            var n = SimMath.Normalize(snapB.Position - snapA.Position);
            if (n.LengthSquared == 0f)
                n = new Vec2(0f, 1f);
            a.Velocity = -n * c.ClashSpeed;
            b.Velocity = n * c.ClashSpeed;
            a.StunTicks = c.RecoilTicks;
            b.StunTicks = c.RecoilTicks;
            EnterStun(ref a, ActionState.Recoil);
            EnterStun(ref b, ActionState.Recoil);
            a.HitstopTicks = c.ClashHitstop;
            b.HitstopTicks = c.ClashHitstop;
            events.Add(new SimEvent(tick, SimEventType.Clash, SimEvent.NoFighter, point, n, c.ClashSpeed));
        }

        /// <summary>경직 계열 상태로 들어간다. 진행 중이던 공격, 패링 구간, 대시를 끊는다. StunTicks는 부르는 쪽이 정한다.</summary>
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

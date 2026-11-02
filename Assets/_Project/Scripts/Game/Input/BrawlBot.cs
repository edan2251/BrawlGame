using System;
using Knockbox.Simulation;
using UnityEngine;

namespace Knockbox.Game
{
    /// <summary>
    /// CR-004의 단순 봇(몸 폼): 상대에게 다가가 가까우면 주먹(탭), 중거리에서 가끔 박치기(홀드 뒤 상대 쪽으로 발사).
    /// 단계 C: 박치기로 다가오는 상대를 잡기로 받아내려 하고, 붙어 있으면 가끔 잡아서 가까운 옆벽 쪽으로 던진다.
    /// 상대가 높이 있으면 가끔 점프한다. 조준은 상대 몸 중심. 같은 시드면 같은 상황에서 같은 입력을 낸다.
    /// </summary>
    public sealed class BrawlBot : IInputSource
    {
        const float PunchRange = 1.35f;
        const float LungeMin = 3f;
        const float LungeMax = 8f;
        /// <summary>틱당 박치기를 시작할 확률(중거리에서).</summary>
        const float LungeChance = 1f / 35f;

        readonly Func<SimWorld> world;
        readonly int fighter;
        readonly uint seed;
        SimRng rng;
        int holdLeft;
        int cooldown;
        bool jumpHeld;
        int holdWait;

        public BrawlBot(Func<SimWorld> world, int fighter, uint seed)
        {
            this.world = world;
            this.fighter = fighter;
            this.seed = seed;
            Reset();
        }

        /// <summary>새 경기: 처음 상태로.</summary>
        public void Reset()
        {
            rng = new SimRng(seed);
            holdLeft = 0;
            cooldown = 0;
            jumpHeld = false;
            holdWait = 0;
        }

        public InputFrame Poll(int tick)
        {
            var w = world();
            if (w == null)
                return default;
            var me = w.State.GetFighter(fighter);
            var other = w.State.GetFighter(1 - fighter);
            var delta = new Vector2(other.Position.X - me.Position.X, other.Position.Y - me.Position.Y);
            byte aim = AimInput.Quantize(delta);
            float distance = delta.magnitude;
            int moveX = Mathf.Abs(delta.x) > 0.6f ? (delta.x > 0f ? 1 : -1) : 0;
            var buttons = InputButtons.None;
            if (cooldown > 0)
                cooldown--;

            if (holdLeft > 0)
            {
                // 박치기 충전 중: 누른 채 기다렸다가 0이 되는 틱에 뗀다(뗀 틱의 조준으로 발사).
                holdLeft--;
                if (holdLeft > 0)
                    buttons |= InputButtons.Attack;
                return new InputFrame(0, 0, buttons, aim);
            }

            if (me.Action == ActionState.Holding)
            {
                // 잡았으면 잠깐 들고 있다가 가까운 옆벽 쪽(조금 위)으로 던진다(좌클릭 눌림).
                if (holdWait == 0)
                    holdWait = rng.Range(10, 41);
                if (--holdWait > 0)
                    return new InputFrame(0, 0, InputButtons.None, aim);
                byte wallAim = AimInput.Quantize(new Vector2(me.Position.X >= 0f ? 1f : -1f, 0.3f));
                return new InputFrame(0, 0, InputButtons.Attack, wallAim);
            }
            holdWait = 0;

            bool free = me.InMovementState || me.Action == ActionState.Charge;
            // 받아내기: 박치기로 다가오는 상대가 잡기 판정이 켜질 즈음(선딜 틱 근처) 닿을 것 같으면 우클릭(충전 중이어도 우선).
            if (free && other.Action == ActionState.Lunge && distance > 1e-3f)
            {
                float closing = -(other.Velocity.X * delta.x + other.Velocity.Y * delta.y) / distance;
                if (closing > 5f)
                {
                    float arrive = (distance - 1.2f) / (closing / 60f);
                    int startup = w.Config.Combat.Grab.StartupTicks;
                    if (arrive >= startup - 2 && arrive <= startup + 2)
                        return new InputFrame(0, 0, InputButtons.Grab, aim);
                }
            }
            free = me.InMovementState;
            // 상대가 박치기를 준비했으면 바로 붙지 않고 물러나 받아낼 거리를 만든다.
            if (free && FighterCombat.IsLungeReady(other, w.Config) && distance < 3f)
                return new InputFrame(delta.x > 0f ? -1 : 1, 0, InputButtons.None, aim);
            if (free && cooldown == 0 && distance < 1.2f && rng.NextFloat01() < 0.15f)
            {
                cooldown = rng.Range(20, 40);
                return new InputFrame(0, 0, InputButtons.Grab, aim);
            }
            if (free && cooldown == 0)
            {
                if (distance < PunchRange)
                {
                    // 탭: 한 틱 누르고 다음 틱에 뗀다.
                    buttons |= InputButtons.Attack;
                    holdLeft = 1;
                    cooldown = rng.Range(10, 26);
                }
                else if (distance > LungeMin && distance < LungeMax && rng.NextFloat01() < LungeChance)
                {
                    buttons |= InputButtons.Attack;
                    holdLeft = w.Config.Combat.Body.TapHoldTicks + rng.Range(4, 21);
                    cooldown = rng.Range(30, 60);
                }
            }

            // 상대가 높이 있으면 가끔 점프(누르고 있으면 더 높이).
            if (jumpHeld)
            {
                buttons |= InputButtons.Jump;
                jumpHeld = !me.Grounded && me.Velocity.Y > 0f;
            }
            else if (me.Grounded && delta.y > 1.5f && rng.NextFloat01() < 0.08f)
            {
                buttons |= InputButtons.Jump;
                jumpHeld = true;
            }
            return new InputFrame(moveX, 0, buttons, aim);
        }
    }

    /// <summary>
    /// CR-004 단계 C의 연습용 봇: 제자리에서 상대를 보고, 일정 간격(Interval)마다 박치기를 상대 쪽으로 쏜다(받아내기 연습).
    /// 충전 길이는 간격마다 짧게·길게 번갈아 바뀐다.
    /// </summary>
    public sealed class LungePracticeBot : IInputSource
    {
        public const int Interval = 90;
        const int ShortCharge = 4;
        const int LongCharge = 20;

        readonly Func<SimWorld> world;
        readonly int fighter;
        int wait;
        int holdLeft;
        int shots;

        public LungePracticeBot(Func<SimWorld> world, int fighter)
        {
            this.world = world;
            this.fighter = fighter;
            Reset();
        }

        public void Reset()
        {
            wait = Interval;
            holdLeft = 0;
            shots = 0;
        }

        public InputFrame Poll(int tick)
        {
            var w = world();
            if (w == null)
                return default;
            var me = w.State.GetFighter(fighter);
            var other = w.State.GetFighter(1 - fighter);
            byte aim = AimInput.Quantize(new Vector2(other.Position.X - me.Position.X, other.Position.Y - me.Position.Y));
            if (holdLeft > 0)
            {
                holdLeft--;
                return new InputFrame(0, 0, holdLeft > 0 ? InputButtons.Attack : InputButtons.None, aim);
            }
            if (!me.InMovementState || --wait > 0)
                return new InputFrame(0, 0, InputButtons.None, aim);
            wait = Interval;
            holdLeft = w.Config.Combat.Body.TapHoldTicks + (shots++ % 2 == 0 ? ShortCharge : LongCharge);
            return new InputFrame(0, 0, InputButtons.Attack, aim);
        }
    }
}

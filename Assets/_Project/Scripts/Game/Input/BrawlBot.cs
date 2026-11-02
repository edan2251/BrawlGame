using System;
using Knockbox.Simulation;
using UnityEngine;

namespace Knockbox.Game
{
    /// <summary>
    /// CR-004 단계 B의 단순 봇(몸 폼): 상대에게 다가가 가까우면 주먹(탭), 중거리에서 가끔 박치기(홀드 뒤 상대 쪽으로 발사).
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

            bool free = me.InMovementState;
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
}

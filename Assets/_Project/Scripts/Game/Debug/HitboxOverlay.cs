using Knockbox.Presentation;
using Knockbox.Simulation;
using UnityEngine;

namespace Knockbox.Game
{
    /// <summary>
    /// F3 판정 오버레이(06-match-and-tools.md 4절): 시뮬레이션의 몸 상자, 활성 공격 판정 원, 속도 벡터,
    /// 패링 구간을 실제 시뮬레이션 위치(보간 없음)에 그린다. 상태 이름과 틱 번호는 DebugStateLabels가 함께 보여 준다.
    /// 잡기 상자(CP7)와 돌진 위력(CP5)은 해당 CP에서 더한다.
    /// </summary>
    public sealed class HitboxOverlay : MonoBehaviour
    {
        const float Line = 0.04f;
        const float VelocityScale = 0.06f;
        const int SortingOrder = 50;

        struct FighterMarks
        {
            public SpriteRenderer[] Box;
            public SpriteRenderer[] Circles;
            public SpriteRenderer Velocity;
            public SpriteRenderer Parry;
        }

        readonly FighterMarks[] marks = new FighterMarks[2];

        public void Setup(PaletteAsset palette, ShapeLibrary shapes)
        {
            for (int k = 0; k < 2; k++)
            {
                var m = new FighterMarks
                {
                    Box = new SpriteRenderer[4],
                    Circles = new SpriteRenderer[AttackDef.MaxCircles],
                };
                for (int i = 0; i < 4; i++)
                    m.Box[i] = ShapeLibrary.Create(transform, $"P{k + 1}Box{i}", shapes.Square, SortingLayers.UI, SortingOrder, palette.ink);
                for (int i = 0; i < m.Circles.Length; i++)
                    m.Circles[i] = ShapeLibrary.Create(transform, $"P{k + 1}Hit{i}", shapes.Ring, SortingLayers.UI, SortingOrder + 1, palette.hazard);
                m.Velocity = ShapeLibrary.Create(transform, $"P{k + 1}Velocity", shapes.Square, SortingLayers.UI, SortingOrder + 2, palette.bouncy);
                m.Parry = ShapeLibrary.Create(transform, $"P{k + 1}Parry", shapes.Ring, SortingLayers.UI, SortingOrder, palette.spark);
                marks[k] = m;
            }
        }

        public void Render(in SimState state, SimConfig config)
        {
            Render(marks[0], state.Fighter0, config);
            Render(marks[1], state.Fighter1, config);
        }

        static void Render(in FighterMarks m, in FighterState f, SimConfig config)
        {
            float half = config.HalfBody;
            var c = new Vector2(f.Position.X, f.Position.Y);
            float size = half * 2f;
            PlaceRect(m.Box[0], c + new Vector2(0f, half), size + Line, Line);
            PlaceRect(m.Box[1], c - new Vector2(0f, half), size + Line, Line);
            PlaceRect(m.Box[2], c + new Vector2(half, 0f), Line, size + Line);
            PlaceRect(m.Box[3], c - new Vector2(half, 0f), Line, size + Line);

            bool active = FighterCombat.IsHitboxActive(f, config);
            int count = 0;
            if (active)
            {
                ref readonly var attack = ref config.GetAttack(f.AttackId);
                count = attack.CircleCount;
                for (int i = 0; i < count; i++)
                {
                    var circle = attack.GetCircle(i);
                    var center = FighterCombat.CircleCenter(f, attack, circle);
                    var sr = m.Circles[i];
                    sr.enabled = true;
                    sr.transform.localPosition = new Vector3(center.X, center.Y, 0f);
                    ShapeLibrary.SetSize(sr, circle.Radius * 2f, circle.Radius * 2f);
                }
            }
            for (int i = count; i < m.Circles.Length; i++)
                m.Circles[i].enabled = false;

            var v = new Vector2(f.Velocity.X, f.Velocity.Y);
            float length = v.magnitude * VelocityScale;
            m.Velocity.enabled = length > 0.01f;
            if (m.Velocity.enabled)
            {
                m.Velocity.transform.localPosition = c + v.normalized * (length * 0.5f);
                m.Velocity.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
                ShapeLibrary.SetSize(m.Velocity, length, Line);
            }

            m.Parry.enabled = f.ParryTicks > 0;
            if (m.Parry.enabled)
            {
                m.Parry.transform.localPosition = c;
                ShapeLibrary.SetSize(m.Parry, size * 1.6f, size * 1.6f);
            }
        }

        static void PlaceRect(SpriteRenderer sr, Vector2 center, float width, float height)
        {
            sr.transform.localPosition = center;
            ShapeLibrary.SetSize(sr, width, height);
        }
    }
}

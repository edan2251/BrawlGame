using Knockbox.Simulation;
using UnityEngine;

namespace Knockbox.Presentation
{
    /// <summary>
    /// 파이터 그리기(05-presentation.md 1~3절): 둥근 몸 + 잉크 외곽선 + 평면 그림자, 눈, 팔다리,
    /// 스프링 몸 변형과 기울기, 대시 잔상. 직전 상태와 현재 상태를 보간해서 그린다.
    /// 시간은 시뮬레이션 시간으로 받는다(dt). 그래서 캡처와 실제 플레이가 같은 모습이 된다.
    /// </summary>
    public sealed class FighterView : MonoBehaviour
    {
        /// <summary>몸 그림 중심이 충돌 상자 중심보다 이만큼 위에 있다. 그 아래로 다리가 보인다.</summary>
        public const float VisualLift = 0.2f;
        const float BodySize = 1f;
        const float OutlineSize = 1.12f;
        const float ShadowOffset = 0.08f;
        const float MaxTiltDegrees = 8f;
        /// <summary>벽에 닿아 있을 때 몸 그림을 벽에서 떼는 거리(그림 1.12와 충돌 0.9의 차이만큼).</summary>
        const float WallClearance = 0.11f;
        // 착지 찌그러짐이 3프레임쯤에 처음 1로 돌아오고 8프레임 안팎에 거의 가라앉는 값.
        const float SpringStiffness = 900f;
        const float SpringDamping = 21f;
        const int GhostCount = 3;
        const float GhostLife = 0.25f;
        const float GhostAlpha = 0.4f;
        const float GhostInterval = 0.045f;

        int index;
        PaletteAsset palette;
        Transform pose;
        SpriteRenderer body;
        SpriteRenderer outline;
        SpriteRenderer shadow;
        readonly SpriteRenderer[] eyes = new SpriteRenderer[2];
        readonly SpriteRenderer[] ghosts = new SpriteRenderer[GhostCount];
        readonly float[] ghostAge = new float[GhostCount];
        LimbRig rig;

        Vector2 scale = Vector2.one;
        Vector2 scaleVelocity;
        bool holdImpulseFrame;
        float tilt;
        float wallShift;
        float blinkIn;
        float blinkLeft;
        float ghostEmitLeft;
        float ghostEmitTimer;
        int ghostNext;
        FxRandom random;
        Vector2 eyeSize;

        public LimbRig Rig => rig;
        public Vector2 BodyScale => scale;
        public SpriteRenderer Body => body;

        public void Setup(int fighter, PaletteAsset paletteAsset, ShapeLibrary shapes)
        {
            index = fighter;
            palette = paletteAsset;
            random = new FxRandom(0x9E37u + (uint)fighter * 7919u);
            blinkIn = random.Range(1.5f, 3.5f);

            pose = new GameObject("Pose").transform;
            pose.SetParent(transform, false);
            pose.localPosition = new Vector3(0f, VisualLift - BodySize * 0.5f, 0f);

            shadow = ShapeLibrary.CreateSliced(pose, "Shadow", shapes.RoundedOutline, SortingLayers.Shadow, 0, palette.shadow);
            shadow.transform.localPosition = new Vector3(ShadowOffset, BodySize * 0.5f - ShadowOffset, 0f);
            ShapeLibrary.SetSize(shadow, OutlineSize, OutlineSize);
            outline = ShapeLibrary.CreateSliced(pose, "Outline", shapes.RoundedOutline, SortingLayers.Fighter, 0, palette.ink);
            outline.transform.localPosition = new Vector3(0f, BodySize * 0.5f, 0f);
            ShapeLibrary.SetSize(outline, OutlineSize, OutlineSize);
            body = ShapeLibrary.CreateSliced(pose, "Body", shapes.RoundedBody, SortingLayers.Fighter, 1, palette.FighterBody(fighter));
            body.transform.localPosition = new Vector3(0f, BodySize * 0.5f, 0f);
            ShapeLibrary.SetSize(body, BodySize, BodySize);

            // 모양으로도 구분: P1은 둥근 점, P2는 가로로 긴 캡슐.
            eyeSize = fighter == 0 ? new Vector2(0.16f, 0.16f) : new Vector2(0.24f, 0.1f);
            for (int i = 0; i < 2; i++)
            {
                eyes[i] = fighter == 0
                    ? ShapeLibrary.Create(pose, $"Eye{i}", shapes.Circle, SortingLayers.Fighter, 2, palette.ink)
                    : ShapeLibrary.CreateSliced(pose, $"Eye{i}", shapes.EyeCapsule, SortingLayers.Fighter, 2, palette.ink);
                ShapeLibrary.SetSize(eyes[i], eyeSize.x, eyeSize.y);
            }

            for (int i = 0; i < GhostCount; i++)
            {
                // 잔상은 팔다리보다도 뒤에 그린다.
                ghosts[i] = ShapeLibrary.CreateSliced(transform.parent, $"Ghost{fighter}_{i}", shapes.RoundedBody, SortingLayers.LimbBack, -10, palette.FighterBody(fighter));
                ShapeLibrary.SetSize(ghosts[i], BodySize, BodySize);
                ghosts[i].enabled = false;
                ghostAge[i] = GhostLife;
            }

            rig = new GameObject("Limbs").AddComponent<LimbRig>();
            rig.transform.SetParent(transform, false);
            rig.Setup(shapes, palette.ink);
        }

        /// <summary>순간 이동 뒤(라운드·시나리오 시작) 변형과 발 위치를 처음 상태로.</summary>
        public void ResetPose()
        {
            scale = Vector2.one;
            scaleVelocity = Vector2.zero;
            tilt = 0f;
            wallShift = 0f;
            ghostEmitLeft = 0f;
            for (int i = 0; i < GhostCount; i++)
            {
                ghostAge[i] = GhostLife;
                ghosts[i].enabled = false;
            }
            rig.ResetPose();
        }

        public void OnEvent(in SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.Land:
                    // 착지: 가로 1.3 × 세로 0.7에서 튕기며 복원(세게 떨어질수록 조금 더).
                    float impact = Mathf.Clamp01(e.Strength / 30f);
                    scale = new Vector2(Mathf.Lerp(1.12f, 1.3f, impact), Mathf.Lerp(0.88f, 0.7f, impact));
                    scaleVelocity = Vector2.zero;
                    holdImpulseFrame = true;
                    break;
                case SimEventType.Jump:
                case SimEventType.AirJump:
                case SimEventType.WallJump:
                case SimEventType.PadBounce:
                    scale = new Vector2(0.8f, 1.25f);
                    scaleVelocity = Vector2.zero;
                    holdImpulseFrame = true;
                    break;
                case SimEventType.Dash:
                    ghostEmitLeft = GhostCount;
                    ghostEmitTimer = 0f;
                    break;
            }
        }

        public void Render(in FighterState previous, in FighterState current, float alpha, float dt, ArenaDef arena, float halfBody, float maxRunSpeed)
        {
            var position = new Vector2(
                Mathf.LerpUnclamped(previous.Position.X, current.Position.X, alpha),
                Mathf.LerpUnclamped(previous.Position.Y, current.Position.Y, alpha));
            transform.localPosition = new Vector3(position.x, position.y, 0f);

            UpdateSquash(current, dt);
            UpdateTilt(current, dt, maxRunSpeed);
            // 벽에 붙으면 몸 그림만 살짝 떼어 외곽선이 벽에 묻히지 않게 한다(시뮬레이션 위치는 그대로).
            float wallTarget = current.WallSide != 0 ? -current.WallSide * WallClearance : 0f;
            wallShift = Mathf.Lerp(wallShift, wallTarget, 1f - Mathf.Exp(-30f * dt));
            pose.localPosition = new Vector3(wallShift, VisualLift - BodySize * 0.5f, 0f);
            pose.localScale = new Vector3(scale.x, scale.y, 1f);
            pose.localRotation = Quaternion.Euler(0f, 0f, tilt);

            UpdateEyes(current, dt);
            UpdateGhosts(dt);

            Vector2 hip0 = pose.TransformPoint(new Vector3(-0.17f, 0.04f, 0f));
            Vector2 hip1 = pose.TransformPoint(new Vector3(0.17f, 0.04f, 0f));
            Vector2 shoulder0 = pose.TransformPoint(new Vector3(-0.46f, 0.5f, 0f));
            Vector2 shoulder1 = pose.TransformPoint(new Vector3(0.46f, 0.5f, 0f));
            rig.UpdateRig(hip0, hip1, shoulder0, shoulder1, position, current, arena, halfBody, dt);
        }

        void UpdateSquash(in FighterState f, float dt)
        {
            // 이동 속도에 따라 진행 방향으로 최대 1.25 늘어난다.
            float vx = Mathf.Abs(f.Velocity.X), vy = Mathf.Abs(f.Velocity.Y);
            float stretch = 1f + 0.25f * Mathf.Clamp01((Mathf.Max(vx, vy) - 6f) / 20f);
            var target = vx >= vy ? new Vector2(stretch, 1f / stretch) : new Vector2(1f / stretch, stretch);

            // 사건이 일어난 프레임에는 찌그러진 모양을 그대로 보여 준다.
            if (holdImpulseFrame)
            {
                holdImpulseFrame = false;
                return;
            }
            // 스프링-댐퍼. 큰 dt에서도 안정하게 잘게 나눠 적분한다.
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / (1f / 240f)), 1, 16);
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                var accel = (target - scale) * SpringStiffness - scaleVelocity * SpringDamping;
                scaleVelocity += accel * h;
                scale += scaleVelocity * h;
            }
        }

        void UpdateTilt(in FighterState f, float dt, float maxRunSpeed)
        {
            float target = f.Action == ActionState.WallSlide ? 0f : -Mathf.Clamp(f.Velocity.X / maxRunSpeed, -1f, 1f) * MaxTiltDegrees;
            tilt = Mathf.Lerp(tilt, target, 1f - Mathf.Exp(-12f * dt));
        }

        void UpdateEyes(in FighterState f, float dt)
        {
            blinkIn -= dt;
            if (blinkIn <= 0f)
            {
                blinkLeft = 0.1f;
                blinkIn = random.Range(2.2f, 4f);
            }
            blinkLeft = Mathf.Max(0f, blinkLeft - dt);
            float open = blinkLeft > 0f ? 0.15f : 1f;

            // 바라보는 쪽과 속도 쪽을 본다.
            float lookX = f.Facing * 0.08f + Mathf.Clamp(f.Velocity.X * 0.004f, -0.04f, 0.04f);
            float lookY = Mathf.Clamp(f.Velocity.Y * 0.004f, -0.06f, 0.06f);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -0.17f : 0.17f;
                eyes[i].transform.localPosition = new Vector3(side + lookX, 0.6f + lookY, 0f);
                ShapeLibrary.SetSize(eyes[i], eyeSize.x, eyeSize.y * open);
            }
        }

        void UpdateGhosts(float dt)
        {
            if (ghostEmitLeft > 0f)
            {
                ghostEmitTimer -= dt;
                if (ghostEmitTimer <= 0f)
                {
                    var ghost = ghosts[ghostNext];
                    ghost.transform.position = body.transform.position;
                    ghost.transform.rotation = pose.rotation;
                    ShapeLibrary.SetSize(ghost, BodySize * scale.x, BodySize * scale.y);
                    ghost.enabled = true;
                    ghostAge[ghostNext] = 0f;
                    ghostNext = (ghostNext + 1) % GhostCount;
                    ghostEmitLeft -= 1f;
                    ghostEmitTimer = GhostInterval;
                }
            }
            var color = palette.FighterBody(index);
            for (int i = 0; i < GhostCount; i++)
            {
                if (!ghosts[i].enabled)
                    continue;
                ghostAge[i] += dt;
                if (ghostAge[i] >= GhostLife)
                {
                    ghosts[i].enabled = false;
                    continue;
                }
                color.a = GhostAlpha * (1f - ghostAge[i] / GhostLife);
                ghosts[i].color = color;
            }
        }
    }
}

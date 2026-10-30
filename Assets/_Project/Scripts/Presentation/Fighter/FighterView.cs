using Knockbox.Simulation;
using UnityEngine;

namespace Knockbox.Presentation
{
    /// <summary>
    /// 파이터 그리기(임시): 색 사각형 몸과 바라보는 쪽을 알려주는 눈 하나.
    /// 직전 상태와 현재 상태를 보간해서 그린다. CP3에서 외곽선, 얼굴, 몸 변형, 팔다리로 바꾼다.
    /// </summary>
    public sealed class FighterView : MonoBehaviour
    {
        public const int SortingOrder = 10;
        const float VisualSize = 1f;
        const float EyeSize = 0.22f;
        const float EyeOffsetX = 0.22f;
        const float EyeOffsetY = 0.15f;

        SpriteRenderer body;
        Transform eye;

        public void Setup(int fighter, PaletteAsset palette)
        {
            if (body == null)
            {
                body = gameObject.AddComponent<SpriteRenderer>();
                body.sprite = ShapeSprites.Square;
                body.sortingOrder = SortingOrder;
                transform.localScale = new Vector3(VisualSize, VisualSize, 1f);

                var eyeObject = new GameObject("Eye");
                eyeObject.transform.SetParent(transform, false);
                eyeObject.transform.localScale = new Vector3(EyeSize, EyeSize, 1f);
                var eyeRenderer = eyeObject.AddComponent<SpriteRenderer>();
                eyeRenderer.sprite = ShapeSprites.Square;
                eyeRenderer.sortingOrder = SortingOrder + 1;
                eyeRenderer.color = palette.ink;
                eye = eyeObject.transform;
            }
            body.color = palette.FighterBody(fighter);
        }

        public void Render(in FighterState previous, in FighterState current, float alpha)
        {
            float x = Mathf.LerpUnclamped(previous.Position.X, current.Position.X, alpha);
            float y = Mathf.LerpUnclamped(previous.Position.Y, current.Position.Y, alpha);
            transform.localPosition = new Vector3(x, y, 0f);
            eye.localPosition = new Vector3(EyeOffsetX * current.Facing, EyeOffsetY, 0f);
        }
    }
}

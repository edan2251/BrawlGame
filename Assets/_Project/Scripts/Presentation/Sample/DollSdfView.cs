using UnityEngine;

namespace Knockbox.Presentation
{
    /// <summary>
    /// 외형 후보 R3(8절, S2): 부위를 거리장(SDF)으로 매끄럽게 합친다(셰이더 Knockbox/SdfDoll).
    /// 사용자 레퍼런스(말랑한 콩 모양 인형)에 맞춰 머리·몸통·다리는 크게 녹여 한 덩어리로, 팔은 조금만 녹인다.
    /// 슬라임식 융합으로 팔이 몸통에 묻히지 않게 겹을 셋으로 나눈다: 뒤 팔(몸 뒤), 몸(머리·몸통·두 다리), 앞 팔(몸 앞).
    /// 겹마다 외곽선이 따로라 앞 팔이 몸통 앞을 지나면 테두리로 구분된다. 눈은 흰 점 스프라이트.
    /// </summary>
    public sealed class DollSdfView : MonoBehaviour
    {
        const int MaxParts = 12;
        const float QuadHalf = 1.6f;

        sealed class Layer
        {
            public Material Material;
            public readonly Vector4[] A = new Vector4[MaxParts];
            public readonly Vector4[] R = new Vector4[MaxParts];
            public int Count;
        }

        Transform root;
        Layer back, body, front;
        SpriteRenderer eye0, eye1, eyeInk0, eyeInk1;
        Mesh quad;

        public bool ShaderFound { get; private set; }

        public void Setup(ShapeLibrary shapes, Color fill, Color shade, Color ink, Color eye)
        {
            root = new GameObject("R3 Root").transform;
            root.SetParent(transform, false);
            var shader = Shader.Find("Knockbox/SdfDoll");
            ShaderFound = shader != null;
            if (!ShaderFound)
            {
                Debug.LogWarning("[Knockbox] SdfDoll 셰이더를 찾지 못해 R3를 그리지 않음");
                return;
            }
            quad = new Mesh { name = "SdfQuad" };
            quad.vertices = new[]
            {
                new Vector3(-QuadHalf, -QuadHalf, 0f), new Vector3(QuadHalf, -QuadHalf, 0f),
                new Vector3(QuadHalf, QuadHalf, 0f), new Vector3(-QuadHalf, QuadHalf, 0f),
            };
            quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            quad.bounds = new Bounds(Vector3.zero, new Vector3(QuadHalf * 2f, QuadHalf * 2f, 1f));
            back = MakeLayer("R3 Back", shader, SortingLayers.LimbBack, 0, shade, ink, 0.05f);
            body = MakeLayer("R3 Body", shader, SortingLayers.Fighter, 0, fill, ink, 0.16f);
            front = MakeLayer("R3 Front", shader, SortingLayers.LimbFront, 0, fill, ink, 0.05f);
            eyeInk0 = ShapeLibrary.Create(root, "EyeInk0", shapes.Circle, SortingLayers.Fighter, 4, ink);
            eyeInk1 = ShapeLibrary.Create(root, "EyeInk1", shapes.Circle, SortingLayers.Fighter, 4, ink);
            eye0 = ShapeLibrary.Create(root, "Eye0", shapes.Circle, SortingLayers.Fighter, 5, eye);
            eye1 = ShapeLibrary.Create(root, "Eye1", shapes.Circle, SortingLayers.Fighter, 5, eye);
        }

        Layer MakeLayer(string name, Shader shader, string sortingLayer, int order, Color fill, Color ink, float blend)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var renderer = go.AddComponent<MeshRenderer>();
            var material = new Material(shader) { name = name };
            material.SetColor("_Fill", fill);
            material.SetColor("_Ink", ink);
            material.SetFloat("_Outline", 0.05f);
            material.SetFloat("_Blend", blend);
            renderer.sharedMaterial = material;
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = order;
            return new Layer { Material = material };
        }

        static void Add(Layer layer, Vector2 a, Vector2 b, float ra, float rb)
        {
            if (layer.Count >= MaxParts)
                return;
            layer.A[layer.Count] = new Vector4(a.x, a.y, b.x, b.y);
            layer.R[layer.Count] = new Vector4(ra, rb, 0f, 0f);
            layer.Count++;
        }

        static void Apply(Layer layer)
        {
            layer.Material.SetVectorArray("_A", layer.A);
            layer.Material.SetVectorArray("_R", layer.R);
            layer.Material.SetFloat("_Count", layer.Count);
            layer.Count = 0;
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(v.x * Mathf.Cos(r) - v.y * Mathf.Sin(r), v.x * Mathf.Sin(r) + v.y * Mathf.Cos(r));
        }

        public void Render(in DollSkeleton k, float worldAngle)
        {
            if (!ShaderFound)
                return;
            root.localRotation = Quaternion.Euler(0f, 0f, worldAngle);
            Vector2 Local(Vector2 v, in DollSkeleton s) => s.Torso + Rotate(new Vector2(v.x * s.Scale.x, v.y * s.Scale.y), s.Angle);

            // 몸: 아래가 넓은 콩 모양 몸통 + 녹아 붙은 머리 + 짧고 뭉툭한 다리(무릎 없이 엉덩이 → 발 한 마디).
            float sx = k.Scale.x;
            Add(body, Local(new Vector2(0f, -0.1f), k), Local(new Vector2(0f, 0.14f), k), 0.38f * sx, 0.3f * sx);
            Add(body, k.Head, k.Head, DollSkeleton.HeadRadius * 1.05f, DollSkeleton.HeadRadius * 1.05f);
            Add(body, k.Hip0, k.Foot0, 0.15f, 0.13f);
            Add(body, k.Hip1, k.Foot1, 0.15f, 0.13f);
            Add(body, k.Foot0 + new Vector2(0.03f, 0f), k.Foot0 + new Vector2(0.08f, 0f), 0.13f, 0.12f);
            Add(body, k.Foot1 + new Vector2(0.03f, 0f), k.Foot1 + new Vector2(0.08f, 0f), 0.13f, 0.12f);
            Apply(body);

            // 팔: 팔꿈치가 드러나지 않는 굵은 튜브와 벙어리장갑 같은 손.
            Add(front, k.Shoulder0, k.Elbow0, 0.115f, 0.1f);
            Add(front, k.Elbow0, k.Hand0, 0.1f, 0.095f);
            Add(front, k.Hand0, k.Hand0, DollSkeleton.HandRadius * 1.05f, DollSkeleton.HandRadius * 1.05f);
            Apply(front);
            Add(back, k.Shoulder1, k.Elbow1, 0.115f, 0.1f);
            Add(back, k.Elbow1, k.Hand1, 0.1f, 0.095f);
            Add(back, k.Hand1, k.Hand1, DollSkeleton.HandRadius * 1.05f, DollSkeleton.HandRadius * 1.05f);
            Apply(back);

            // 눈: 얼굴 앞쪽의 흰 점 두 개.
            var forward = Rotate(Vector2.right, k.Angle);
            var up = Rotate(Vector2.up, k.Angle);
            Eye(eyeInk0, eye0, k.Head + forward * 0.13f + up * 0.03f);
            Eye(eyeInk1, eye1, k.Head - forward * 0.02f + up * 0.03f);
        }

        static void Eye(SpriteRenderer ink, SpriteRenderer white, Vector2 at)
        {
            ink.transform.localPosition = new Vector3(at.x, at.y, 0f);
            white.transform.localPosition = new Vector3(at.x, at.y, 0f);
            ShapeLibrary.SetSize(ink, 0.11f, 0.13f);
            ShapeLibrary.SetSize(white, 0.08f, 0.1f);
        }

        void OnDestroy()
        {
            if (quad != null)
                Destroy(quad);
            foreach (var layer in new[] { back, body, front })
            {
                if (layer != null && layer.Material != null)
                    Destroy(layer.Material);
            }
        }
    }
}

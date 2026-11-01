using Knockbox.Simulation;
using UnityEngine;

namespace Knockbox.Presentation
{
    /// <summary>
    /// 움직이는 구조물의 그림자(08-visual-direction.md 3절, CP14-7). 고정 지형·임시 물체의 그림자는 배경 텍스처에 구워 두고
    /// (BackgroundView), 강체 구조물은 매 프레임 광원에서 본 실루엣을 화면 밖까지 늘린 사다리꼴로 그린다.
    /// 넓은 광원을 흉내 내 광원 세 점(가운데, 좌우 LightHalfWidth)에서 각각 옅게 그려, 겹친 곳(본그림자)은 배경 그림자와 같은 25% 어둡게,
    /// 한두 개만 덮인 곳은 반그림자가 된다. 메시 하나를 미리 만든 배열로 고쳐 쓰므로 프레임마다 할당이 없다.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PropShadowView : MonoBehaviour
    {
        const int Samples = 3;
        const float LightHalfWidth = 2f;
        const float Reach = 60f;
        const int QuadCount = PropState.Capacity * Samples;

        readonly Vector3[] vertices = new Vector3[QuadCount * 4];
        readonly Color[] colors = new Color[QuadCount * 4];
        Mesh mesh;
        ArenaDef arena;
        Vector2 lightPoint;

        /// <summary>그려진 그림자 사다리꼴 수(테스트·캡처 확인용).</summary>
        public int VisibleQuads { get; private set; }

        public void Setup(PaletteAsset palette, Material material)
        {
            mesh = new Mesh { name = "PropShadows" };
            mesh.MarkDynamic();
            var triangles = new int[QuadCount * 6];
            for (int q = 0; q < QuadCount; q++)
            {
                triangles[q * 6 + 0] = q * 4 + 0;
                triangles[q * 6 + 1] = q * 4 + 1;
                triangles[q * 6 + 2] = q * 4 + 2;
                triangles[q * 6 + 3] = q * 4 + 0;
                triangles[q * 6 + 4] = q * 4 + 2;
                triangles[q * 6 + 5] = q * 4 + 3;
            }
            // 세 겹이 다 덮으면 1 − (1 − a)^3 = 배경 그림자 세기(0.25).
            float a = 1f - Mathf.Pow(1f - BackgroundView.ShadowStrength, 1f / Samples);
            var shade = PaletteAsset.WithAlpha(palette.ink, a);
            for (int i = 0; i < colors.Length; i++)
                colors[i] = shade;
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(200f, 200f, 10f));
            GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingLayerName = SortingLayers.Background;
            renderer.sortingOrder = 1;
        }

        public void SetArena(ArenaDef value, Vector2 lightPosition)
        {
            arena = value;
            lightPoint = lightPosition;
            Clear();
        }

        public void Clear()
        {
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = Vector3.zero;
            VisibleQuads = 0;
            if (mesh != null)
                mesh.vertices = vertices;
        }

        public void Render(PropView view, in PropState state)
        {
            if (mesh == null || arena == null)
                return;
            int count = Mathf.Min(arena.DynamicPropCount, state.Count);
            int q = 0;
            for (int b = 0; b < count; b++)
            {
                ref readonly var p = ref arena.GetProp(arena.DynamicProp(b));
                var tr = view.BodyTransform(b);
                Vector2 center = tr.localPosition;
                float angle = tr.localEulerAngles.z * Mathf.Deg2Rad;
                var u = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var v = new Vector2(-u.y, u.x);
                var hx = u * p.HalfSize.X;
                var hy = v * p.HalfSize.Y;
                var c0 = center - hx - hy;
                var c1 = center + hx - hy;
                var c2 = center + hx + hy;
                var c3 = center - hx + hy;
                for (int s = 0; s < Samples; s++)
                {
                    var from = lightPoint + new Vector2((s - (Samples - 1) * 0.5f) * LightHalfWidth, 0f);
                    Silhouette(from, c0, c1, c2, c3, out var left, out var right);
                    vertices[q * 4 + 0] = left;
                    vertices[q * 4 + 1] = right;
                    vertices[q * 4 + 2] = right + (right - from).normalized * Reach;
                    vertices[q * 4 + 3] = left + (left - from).normalized * Reach;
                    q++;
                }
            }
            VisibleQuads = q;
            for (int i = q * 4; i < vertices.Length; i++)
                vertices[i] = Vector3.zero;
            mesh.vertices = vertices;
        }

        /// <summary>광원에서 본 볼록 사각형의 양 끝 꼭짓점(각도가 가장 작은 쪽과 큰 쪽).</summary>
        static void Silhouette(Vector2 from, Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 left, out Vector2 right)
        {
            left = a;
            right = a;
            Consider(from, b, ref left, ref right);
            Consider(from, c, ref left, ref right);
            Consider(from, d, ref left, ref right);
        }

        static void Consider(Vector2 from, Vector2 p, ref Vector2 left, ref Vector2 right)
        {
            var dl = left - from;
            var dr = right - from;
            var dp = p - from;
            if (dl.x * dp.y - dl.y * dp.x > 0f)
                left = p;
            if (dr.x * dp.y - dr.y * dp.x < 0f)
                right = p;
        }

        void OnDestroy()
        {
            if (mesh != null)
                Destroy(mesh);
        }
    }
}

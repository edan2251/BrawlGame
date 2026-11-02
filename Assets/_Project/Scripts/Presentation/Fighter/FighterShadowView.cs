using UnityEngine;

namespace Knockbox.Presentation
{
    /// <summary>
    /// 캐릭터 그림자(CR-004 D/E 피드백): 두 파이터의 몸 사각형을 광원에서 본 실루엣으로 화면 밖까지 늘린 사다리꼴로 그린다(PropShadowView와 같은 방식).
    /// 넓은 광원을 흉내 내 광원 세 점에서 각각 옅게 그려 겹친 곳이 배경 그림자 세기와 같아진다. 미리 만든 배열을 고쳐 쓰므로 프레임마다 할당이 없다.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class FighterShadowView : MonoBehaviour
    {
        const int Samples = 3;
        const float LightHalfWidth = 0.6f;
        const float Reach = 60f;
        const int QuadCount = 2 * Samples;

        readonly Vector3[] vertices = new Vector3[QuadCount * 4];
        readonly Color[] colors = new Color[QuadCount * 4];
        Mesh mesh;
        PaletteAsset palette;
        Vector2 lightPoint;
        bool active;

        public void Setup(PaletteAsset paletteAsset, Material material)
        {
            palette = paletteAsset;
            mesh = new Mesh { name = "FighterShadows" };
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

        /// <summary>광원과 그림자 세기. strength가 0이면 그리지 않는다.</summary>
        public void SetLight(Vector2 lightPosition, float strength)
        {
            lightPoint = lightPosition;
            active = strength > 0f;
            float a = 1f - Mathf.Pow(1f - Mathf.Clamp01(strength), 1f / Samples);
            var shade = PaletteAsset.WithAlpha(QualitySettings.activeColorSpace == ColorSpace.Linear ? palette.ink.linear : palette.ink, a);
            for (int i = 0; i < colors.Length; i++)
                colors[i] = shade;
            mesh.colors = colors;
        }

        public void Render(Vector2 center0, Vector2 center1, float half)
        {
            if (mesh == null)
                return;
            int q = 0;
            if (active)
            {
                Quads(center0, half, ref q);
                Quads(center1, half, ref q);
            }
            for (int i = q * 4; i < vertices.Length; i++)
                vertices[i] = Vector3.zero;
            mesh.vertices = vertices;
        }

        void Quads(Vector2 center, float half, ref int q)
        {
            var c0 = center + new Vector2(-half, -half);
            var c1 = center + new Vector2(half, -half);
            var c2 = center + new Vector2(half, half);
            var c3 = center + new Vector2(-half, half);
            for (int s = 0; s < Samples; s++)
            {
                var from = lightPoint + new Vector2((s - (Samples - 1) * 0.5f) * LightHalfWidth, 0f);
                // 광원이 몸 안에 들어오면 그림자를 만들지 않는다.
                if (Mathf.Abs(from.x - center.x) < half && Mathf.Abs(from.y - center.y) < half)
                    continue;
                Silhouette(from, c0, c1, c2, c3, out var left, out var right);
                vertices[q * 4 + 0] = left;
                vertices[q * 4 + 1] = right;
                vertices[q * 4 + 2] = right + (right - from).normalized * Reach;
                vertices[q * 4 + 3] = left + (left - from).normalized * Reach;
                q++;
            }
        }

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

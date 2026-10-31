using System.Collections.Generic;
using Knockbox.Simulation;
using UnityEngine;

namespace Knockbox.Presentation
{
    /// <summary>
    /// 배경(08-visual-direction.md 3·4절): bgBase 바탕 위에, 화면 위 가운데 바깥의 광원 하나가 테마의 빛 색으로 배경을 비추고,
    /// 지형이 광원 반대쪽으로 길고 각진 그림자를 드리운다. 그 위에 미세한 질감과 빛줄기 속 먼지. 지형·캐릭터는 이 위에 플랫하게 그려진다.
    /// URP 2D 광원과 Shadow Caster 대신 빛 원뿔과 그림자 다각형을 직접 계산해 그린다(08 3절이 허용한 대안): 경계가 또렷하고,
    /// 캡처가 재현되며, 화면 픽셀로 그림자를 검사할 수 있다.
    /// </summary>
    public sealed class BackgroundView : MonoBehaviour
    {
        /// <summary>광원 기본 위치: 경기장 위쪽 끝에서 화면 높이의 1/4 위(08 3절).</summary>
        public static readonly Vector2 DefaultLight = new Vector2(0f, ArenaDef.HalfHeight + ArenaDef.Rows * 0.25f);

        /// <summary>빛 원뿔의 반각. 광원이 화면 위 4.5에 있을 때 화면 맨 아래에서 거의 경기장 폭을 덮는다.</summary>
        public const float ConeHalfAngle = 36f;
        const float EdgeSoftness = 2.5f;
        const float ConeLength = 60f;
        /// <summary>광원에서 ConeLength 떨어진 곳의 밝기 배율(가까울수록 밝다).</summary>
        const float FarBrightness = 0.7f;
        const float ShadowLength = 90f;
        const int ConeSegments = 48;
        const int MoteCount = 26;
        const float NoiseAlpha = 0.07f;

        Vector2 lightPoint;
        Mesh coneMesh;
        Mesh shadowMesh;
        SpriteRenderer baseRenderer;
        SpriteRenderer noiseRenderer;
        Texture2D noiseTexture;
        Sprite noiseSprite;
        readonly SpriteRenderer[] motes = new SpriteRenderer[MoteCount];
        readonly Vector2[] motePosition = new Vector2[MoteCount];
        readonly Vector2[] moteVelocity = new Vector2[MoteCount];
        readonly float[] moteSize = new float[MoteCount];
        FxRandom random;
        PaletteAsset palette;
        ArenaDef currentArena;
        string currentTheme;

        // 그림자 다각형 계산용(빌드할 때만 쓴다).
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> triangles = new List<int>();
        readonly List<Vector2> points = new List<Vector2>();
        readonly List<Vector2> hull = new List<Vector2>();

        public Vector2 LightPosition => lightPoint;

        public void Build(ArenaDef arena, ArenaTheme theme, PaletteAsset paletteAsset, ShapeLibrary shapes, Vector2 lightPosition)
        {
            palette = paletteAsset;
            if (baseRenderer == null)
                CreateRenderers(shapes);
            if (arena == currentArena && theme.name == currentTheme && lightPosition == lightPoint)
                return;
            currentArena = arena;
            currentTheme = theme.name;
            lightPoint = lightPosition;
            baseRenderer.color = palette.bgBase;
            noiseRenderer.color = palette.ink;
            BuildCone(theme);
            BuildShadows(arena);
            for (int i = 0; i < MoteCount; i++)
                motes[i].color = PaletteAsset.WithAlpha(palette.mote, 0.4f);
            ResetMotes();
        }

        void CreateRenderers(ShapeLibrary shapes)
        {
            baseRenderer = ShapeLibrary.Create(transform, "Base", shapes.Square, SortingLayers.Background, -10, palette.bgBase);
            ShapeLibrary.SetSize(baseRenderer, ArenaDef.Columns * 3f, ArenaDef.Rows * 3f);
            coneMesh = CreateMesh("LightCone", 0);
            shadowMesh = CreateMesh("Shadows", 1);
            noiseTexture = ShapeSprites.NoiseMask(192, 108, 0.035f, NoiseAlpha);
            noiseSprite = Sprite.Create(noiseTexture, new Rect(0, 0, noiseTexture.width, noiseTexture.height), new Vector2(0.5f, 0.5f), 1f);
            noiseSprite.hideFlags = HideFlags.HideAndDontSave;
            noiseRenderer = ShapeLibrary.Create(transform, "Texture", noiseSprite, SortingLayers.Background, 2, palette.ink);
            noiseRenderer.drawMode = SpriteDrawMode.Simple;
            noiseRenderer.transform.localScale = new Vector3((ArenaDef.Columns + 4f) / noiseTexture.width, (ArenaDef.Rows + 4f) / noiseTexture.height, 1f);
            for (int i = 0; i < MoteCount; i++)
                motes[i] = ShapeLibrary.Create(transform, "Mote", shapes.Circle, SortingLayers.Background, 3, palette.mote);
        }

        Mesh CreateMesh(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ShapeLibrary.ActiveMaterial;
            renderer.sortingLayerName = SortingLayers.Background;
            renderer.sortingOrder = order;
            var mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
            return mesh;
        }

        /// <summary>광원에서 아래로 퍼지는 빛 원뿔. 가까울수록 밝고, 가장자리는 EdgeSoftness 각도만큼 부드럽게 사라진다.</summary>
        void BuildCone(ArenaTheme theme)
        {
            vertices.Clear();
            colors.Clear();
            triangles.Clear();
            var near = PaletteAsset.ForVertex(theme.light);
            var far = PaletteAsset.ForVertex(Color.Lerp(palette.bgBase, theme.light, FarBrightness));
            vertices.Add(lightPoint);
            colors.Add(near);
            float outer = ConeHalfAngle + EdgeSoftness;
            for (int i = 0; i <= ConeSegments; i++)
            {
                float angle = Mathf.Lerp(-outer, outer, i / (float)ConeSegments);
                float rad = angle * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad));
                vertices.Add(lightPoint + dir * ConeLength);
                float edge = Mathf.Clamp01((outer - Mathf.Abs(angle)) / EdgeSoftness);
                colors.Add(PaletteAsset.WithAlpha(far, edge));
                if (i > 0)
                {
                    triangles.Add(0);
                    triangles.Add(i + 1);
                    triangles.Add(i);
                }
            }
            Apply(coneMesh);
        }

        /// <summary>
        /// 지형 상자마다 광원에서 본 그림자: 네 모서리와, 모서리를 광원 반대쪽으로 멀리 민 네 점의 볼록 껍질.
        /// 경기장 위쪽 테두리는 빼고(광원이 그 바깥에 있다), 가시는 낮아서 빼고, 단방향 발판은 그려지는 얇은 판으로 만든다.
        /// </summary>
        void BuildShadows(ArenaDef arena)
        {
            vertices.Clear();
            colors.Clear();
            triangles.Clear();
            var shade = PaletteAsset.ForVertex(palette.bgBase);
            for (int i = 0; i < arena.BoxCount; i++)
            {
                var b = arena.GetBox(i);
                if (b.Kind == TerrainKind.Spike || b.MaxY >= ArenaDef.HalfHeight - 1e-3f)
                    continue;
                float minY = b.Kind == TerrainKind.OneWay ? b.MaxY - ArenaView.OneWayThickness : b.MinY;
                points.Clear();
                AddCorner(new Vector2(b.MinX, minY));
                AddCorner(new Vector2(b.MaxX, minY));
                AddCorner(new Vector2(b.MinX, b.MaxY));
                AddCorner(new Vector2(b.MaxX, b.MaxY));
                ConvexHull(points, hull);
                int start = vertices.Count;
                for (int k = 0; k < hull.Count; k++)
                {
                    vertices.Add(hull[k]);
                    colors.Add(shade);
                }
                for (int k = 1; k + 1 < hull.Count; k++)
                {
                    triangles.Add(start);
                    triangles.Add(start + k + 1);
                    triangles.Add(start + k);
                }
            }
            Apply(shadowMesh);
        }

        void AddCorner(Vector2 corner)
        {
            points.Add(corner);
            points.Add(corner + (corner - lightPoint).normalized * ShadowLength);
        }

        void Apply(Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        /// <summary>Andrew의 단조 사슬. 결과는 반시계 방향.</summary>
        static void ConvexHull(List<Vector2> input, List<Vector2> output)
        {
            input.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            output.Clear();
            for (int pass = 0; pass < 2; pass++)
            {
                int start = output.Count;
                for (int k = 0; k < input.Count; k++)
                {
                    var p = input[pass == 0 ? k : input.Count - 1 - k];
                    while (output.Count >= start + 2 && Cross(output[output.Count - 2], output[output.Count - 1], p) <= 0f)
                        output.RemoveAt(output.Count - 1);
                    output.Add(p);
                }
                output.RemoveAt(output.Count - 1);
            }
        }

        static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        /// <summary>점이 빛 원뿔 안(가장자리 부드러운 부분 제외)인가. 그림자는 보지 않는다.</summary>
        public bool InLightCone(Vector2 point)
        {
            var d = point - lightPoint;
            if (d.y >= 0f)
                return false;
            return Mathf.Abs(d.x) <= -d.y * Mathf.Tan(ConeHalfAngle * Mathf.Deg2Rad);
        }

        /// <summary>먼지 입자를 처음 배치로(라운드·시나리오마다 같은 화면이 나오게).</summary>
        public void ResetMotes()
        {
            random = new FxRandom(0xD057u);
            for (int i = 0; i < MoteCount; i++)
                Respawn(i, true);
            Tick(0f);
        }

        void Respawn(int i, bool anywhere)
        {
            float y = anywhere ? random.Range(-ArenaDef.HalfHeight + 1f, ArenaDef.HalfHeight - 1f) : random.Range(-ArenaDef.HalfHeight + 1f, -2f);
            float half = (lightPoint.y - y) * Mathf.Tan(ConeHalfAngle * 0.85f * Mathf.Deg2Rad);
            motePosition[i] = new Vector2(random.Range(-half, half), y);
            moteVelocity[i] = new Vector2(random.Range(-0.18f, 0.18f), random.Range(0.06f, 0.28f));
            moteSize[i] = random.Range(0.06f, 0.12f);
        }

        /// <summary>먼지가 천천히 떠오른다. 원뿔이나 화면을 벗어나면 아래쪽에서 다시 생긴다(연출 시간 dt).</summary>
        public void Tick(float dt)
        {
            if (palette == null)
                return;
            for (int i = 0; i < MoteCount; i++)
            {
                motePosition[i] += moteVelocity[i] * dt;
                if (motePosition[i].y > ArenaDef.HalfHeight - 0.5f || !InLightCone(motePosition[i]))
                    Respawn(i, false);
                motes[i].transform.localPosition = motePosition[i];
                ShapeLibrary.SetSize(motes[i], moteSize[i], moteSize[i]);
            }
        }

        void OnDestroy()
        {
            if (noiseTexture != null)
                Destroy(noiseTexture);
            if (noiseSprite != null)
                Destroy(noiseSprite);
            if (coneMesh != null)
                Destroy(coneMesh);
            if (shadowMesh != null)
                Destroy(shadowMesh);
        }
    }
}

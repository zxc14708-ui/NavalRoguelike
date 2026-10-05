using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// 섬 모양을 코드로 만든다(각진 로우폴리 — 함선·적 모델과 같은 느낌).
    ///   바위섬: 들쭉날쭉한 바위 기둥 1~4개. 높이 5~11m — 함포·어뢰·저공 미사일을 가린다.
    ///   무인도: 모래 해안 + 풀 덮인 언덕 + 나무 몇 그루(+해안 바위). 언덕 높이 3~6m.
    /// 수면선(-0.9)보다 아래까지 내려가 물에 떠 보이지 않게 하고, 둘레에 파도 거품 띠를 두른다.
    /// 콜라이더는 부분마다 볼록 메시(Terrain 레이어). 나무는 가리지 않는다(얇음).
    /// </summary>
    public static partial class IslandBuilder
    {
        public const float SeaY = -0.9f;
        private const float BaseY = -3f;

        private static Material s_rock, s_cliff, s_sand, s_grass, s_tree, s_foam;

        private static void LoadMaterials()
        {
            if (s_rock != null) return;
            s_rock = Resources.Load<Material>("Islands/MAT_IslandRock");
            s_cliff = Resources.Load<Material>("Islands/MAT_IslandCliff");
            s_sand = Resources.Load<Material>("Islands/MAT_IslandSand");
            s_grass = Resources.Load<Material>("Islands/MAT_IslandGrass");
            s_tree = Resources.Load<Material>("Islands/MAT_IslandTree");
            s_foam = Resources.Load<Material>("Water/MAT_WakeKelvin");
        }

        public static IslandInfo Build(Vector3 center, float radius, bool rocky, System.Random rng, Transform parent)
        {
            LoadMaterials();
            var root = new GameObject(rocky ? "Rock islet" : "Island");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(center.x, 0f, center.z);
            root.layer = Islands.Layer;

            float height;
            float shoreRadius;
            if (rocky)
            {
                height = Range(rng, 6f, 11f) * Mathf.Lerp(0.8f, 1.15f, Mathf.InverseLerp(5f, 14f, radius));
                AddRock(root.transform, Vector3.zero, radius, height, rng, s_rock);
                int extra = rng.Next(1, 4);
                for (int i = 0; i < extra; i++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float d = radius * Range(0.7f, 1.15f, rng);
                    float r = radius * Range(0.3f, 0.55f, rng);
                    AddRock(root.transform, new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d), r, height * Range(0.35f, 0.7f, rng), rng, rng.NextDouble() < 0.5 ? s_rock : s_cliff);
                }
                shoreRadius = radius * 1.35f;
            }
            else
            {
                // 모래 해안(낮고 넓게) + 풀 언덕 + 나무
                var sand = Lathe(new[] { (BaseY, 1.18f), (SeaY + 0.15f, 1.0f), (0.35f, 0.86f), (0.75f, 0.7f) }, radius, 14, 0.08f, rng, flatTop: true);
                AddPart(root.transform, "Beach", sand, s_sand, collider: true);

                height = Range(rng, 3f, 6f);
                var hill = Lathe(new[] { (0.5f, 0.74f), (height * 0.45f, 0.6f), (height * 0.8f, 0.36f), (height, 0.1f) }, radius, 12, 0.14f, rng, flatTop: false);
                AddPart(root.transform, "Hill", hill, s_grass, collider: true);

                int trees = rng.Next(3, 8);
                for (int i = 0; i < trees; i++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float d = radius * Range(0.1f, 0.55f, rng);
                    float ground = Mathf.Lerp(height * 0.9f, 0.8f, Mathf.InverseLerp(0f, radius * 0.6f, d));
                    AddTree(root.transform, new Vector3(Mathf.Cos(a) * d, ground - 0.3f, Mathf.Sin(a) * d), rng);
                }

                int shoreRocks = rng.Next(0, 3);
                for (int i = 0; i < shoreRocks; i++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float d = radius * Range(0.95f, 1.15f, rng);
                    AddRock(root.transform, new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d), radius * Range(0.12f, 0.2f, rng), Range(1.2f, 2.4f, rng), rng, s_cliff);
                }
                shoreRadius = radius * 1.12f;
            }

            // 장식(판정 없음): 등대 · 해안에 얹힌 난파선 · 바위 위 물개
            var decor = Decorate(root.transform, radius, height, rocky, new System.Random(rng.Next()));

            var rim = new Vector2[12];
            for (int i = 0; i < rim.Length; i++)
            {
                float a = i * Mathf.PI * 2f / rim.Length;
                rim[i] = new Vector2(center.x + Mathf.Cos(a) * shoreRadius * 0.85f, center.z + Mathf.Sin(a) * shoreRadius * 0.85f);
            }

            AddFoamRing(root.transform, shoreRadius, rng);
            SetLayer(root, Islands.Layer);

            var info = new IslandInfo { Center = root.transform.position, Radius = shoreRadius, Height = height, Rocky = rocky, Rim = rim, Root = root,
                                        Lighthouse = decor.lighthouse, Wreck = decor.wreck, Seals = decor.seals };
            Islands.Register(info);
            return info;
        }

        // ------------------------------------------------------------ 부분

        private static void AddRock(Transform parent, Vector3 local, float radius, float height, System.Random rng, Material mat)
        {
            int segments = rng.Next(7, 11);
            var rings = new[] { (BaseY, 1.12f), (SeaY + 0.3f, 1.0f), (height * 0.35f, Range(0.75f, 0.9f, rng)), (height * 0.7f, Range(0.4f, 0.6f, rng)), (height * 0.92f, Range(0.15f, 0.3f, rng)), (height, 0f) };
            var mesh = Lathe(rings, radius, segments, 0.22f, rng, flatTop: false, apexJitter: radius * 0.25f);
            var part = AddPart(parent, "Rock", mesh, mat, collider: true);
            part.transform.localPosition = local;
            part.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
        }

        private static void AddTree(Transform parent, Vector3 local, System.Random rng)
        {
            float h = Range(3f, 5.2f, rng), r = Range(0.9f, 1.5f, rng);
            var mesh = Lathe(new[] { (0f, 0.35f), (h * 0.3f, 1f), (h * 0.65f, 0.6f), (h, 0f) }, r, 6, 0.1f, rng, flatTop: false);
            var part = AddPart(parent, "Tree", mesh, s_tree, collider: false);
            part.transform.localPosition = local;
        }

        /// <summary>해안 파도 거품 띠(물살 셰이더의 켈빈 줄을 재사용).</summary>
        private static void AddFoamRing(Transform parent, float radius, System.Random rng)
            => AddFoamRing(parent, _ => radius, rng);

        /// <summary>radiusAt(각도 라디안, 로컬 +X에서 반시계) = 그 방향의 해안 반경.</summary>
        private static void AddFoamRing(Transform parent, System.Func<float, float> radiusAt, System.Random rng)
        {
            if (s_foam == null) return;
            const int N = 40;
            var verts = new List<Vector3>(N * 2 + 2);
            var uvs = new List<Vector2>(N * 2 + 2);
            var colors = new List<Color32>(N * 2 + 2);
            var tris = new List<int>(N * 6);
            float along = 0f;
            Vector3 prev = Vector3.zero;
            float phase = (float)rng.NextDouble() * 10f;
            for (int i = 0; i <= N; i++)
            {
                float a = i * Mathf.PI * 2f / N;
                float wobble = 1f + 0.08f * Mathf.Sin(a * 3f + phase) + 0.05f * Mathf.Sin(a * 7f + phase * 2f);
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float radius = radiusAt(a);
                Vector3 inner = dir * radius * 0.9f * wobble + Vector3.up * (SeaY + 0.03f);
                Vector3 outer = dir * radius * 1.22f * wobble + Vector3.up * (SeaY + 0.03f);
                if (i > 0) along += Vector3.Distance(inner, prev);
                prev = inner;
                verts.Add(outer); verts.Add(inner);
                uvs.Add(new Vector2(0f, along)); uvs.Add(new Vector2(1f, along));
                colors.Add(new Color32(255, 255, 255, 150)); colors.Add(new Color32(255, 255, 255, 150));
                if (i == 0) continue;
                int b = (i - 1) * 2;
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
            }
            var mesh = new Mesh { name = "Shore foam" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            AddPart(parent, "Shore foam", mesh, s_foam, collider: false, shadows: false);
        }

        private static GameObject AddPart(Transform parent, string name, Mesh mesh, Material mat, bool collider, bool shadows = true)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = shadows;
            if (collider)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = true;
            }
            return go;
        }

        private static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayer(t.gameObject, layer);
        }

        /// <summary>
        /// 회전체: (높이, 반경 비율) 링을 쌓고 각 정점을 조금씩 흔든다. 면마다 정점을 따로 둬 각진 음영(플랫 셰이딩).
        /// 반경 비율 0이면 꼭짓점 하나로 모은다.
        /// </summary>
        internal static Mesh Lathe((float y, float r)[] rings, float radius, int segments, float jitter, System.Random rng,
                                  bool flatTop, float apexJitter = 0f)
        {
            int R = rings.Length;
            var grid = new Vector3[R, segments];
            float twist = (float)rng.NextDouble() * Mathf.PI * 2f;
            for (int k = 0; k < R; k++)
            {
                for (int s = 0; s < segments; s++)
                {
                    float a = twist + s * Mathf.PI * 2f / segments + ((float)rng.NextDouble() - 0.5f) * 0.25f;
                    float r = rings[k].r * radius * (1f + ((float)rng.NextDouble() - 0.5f) * 2f * jitter);
                    float y = rings[k].y;
                    if (k > 0 && k < R - 1) y += ((float)rng.NextDouble() - 0.5f) * jitter * 1.5f;
                    grid[k, s] = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                }
            }
            Vector3 apex = new(((float)rng.NextDouble() - 0.5f) * apexJitter, rings[R - 1].y, ((float)rng.NextDouble() - 0.5f) * apexJitter);

            var verts = new List<Vector3>();
            var tris = new List<int>();
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            }

            bool pointedTop = rings[R - 1].r <= 0.001f;
            int last = pointedTop ? R - 2 : R - 1;
            for (int k = 0; k < last; k++)
                for (int s = 0; s < segments; s++)
                {
                    int n = (s + 1) % segments;
                    Tri(grid[k, s], grid[k + 1, s], grid[k + 1, n]);
                    Tri(grid[k, s], grid[k + 1, n], grid[k, n]);
                }

            if (pointedTop)
            {
                for (int s = 0; s < segments; s++) Tri(grid[R - 2, s], apex, grid[R - 2, (s + 1) % segments]);
            }
            else
            {
                // 평평한 윗면(중앙 한 점)
                Vector3 c = Vector3.zero;
                for (int s = 0; s < segments; s++) c += grid[R - 1, s];
                c /= segments;
                if (!flatTop) c.y += 0.3f;
                for (int s = 0; s < segments; s++) Tri(grid[R - 1, s], c, grid[R - 1, (s + 1) % segments]);
            }

            var mesh = new Mesh { name = "Island part" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        internal static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
        internal static float Range(float min, float max, System.Random rng) => Range(rng, min, max);
    }
}

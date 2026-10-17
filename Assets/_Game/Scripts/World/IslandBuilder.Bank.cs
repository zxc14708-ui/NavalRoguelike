using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// 좁은 수로의 양안 육지(2026-10-11 점검 K04). 물길 축을 따라 40m 구간마다 한쪽 기슭을 하나씩 만든다.
    ///   - 해안선: 물길 가운데에서 ChannelHalfWidth ±6m(두 겹 펄린 잡음 — 구간 경계가 이어진다)
    ///   - 단면: 물속 바위 → 해변 → (높으면) 절벽 / (낮으면) 풀 언덕 → 내륙 능선, 높이 4~14m. 섬과 같은 재질·각진 면
    ///   - 판정: 8m마다 상자 콜라이더(Terrain 레이어) — 사선 가림·좌초·적 회피·스폰 판정이 섬과 똑같이 동작한다
    ///   - 해안 파도 거품 띠, 낮은 언덕에는 나무, 물가에는 바위
    /// 2026-10-13 점검 G06(각진 느낌 다듬기): 해변 띠는 늘 모래, 그 위는 삼각형마다 실제 경사로 절벽/풀을 정하고(경계가 지형을 따라
    /// 톱니처럼 — 예전엔 4m 칸마다 사각형으로 바뀌어 체크무늬·계단이 보였다), 내륙 열을 늘려 낮은 구릉을 넣고, 열 위치를 조금씩
    /// 흔들어 줄이 곧지 않게 했다. 해안선·판정 상자·거품은 그대로(경계·사선 가림 동작 같음). 나무는 같은 높이 함수에 앉는다.
    /// 섬처럼 Islands에 등록한다(레이더·섬 간격 판정). 중심은 기슭 안쪽 30m, 반경 30m로 둔다 — 물길 안 바위섬이 기슭에 붙지 않게.
    /// </summary>
    public static partial class IslandBuilder
    {
        public const float BankSegmentLength = 40f;
        private const float BankStep = 4f;
        private const float BankDepth = 90f;   // 해안선에서 내륙 끝까지

        /// <summary>그 지점(물길 축 거리)의 해안선이 물길 가운데에서 떨어진 거리(side: −1 왼쪽, +1 오른쪽).</summary>
        public static float BankShore(float along, int side, int seed)
        {
            float s = (seed % 997) * 0.37f + (side > 0 ? 0f : 51.3f);
            float n1 = Mathf.PerlinNoise(along * 0.017f + s, 3.1f + s * 0.5f);
            float n2 = Mathf.PerlinNoise(along * 0.061f + 11.7f, s + 7.9f);
            return SeaRegion.ChannelHalfWidth + (n1 - 0.5f) * 9f + (n2 - 0.5f) * 3.5f;
        }

        /// <summary>그 지점 기슭의 높이(4~14m).</summary>
        public static float BankHeight(float along, int side, int seed)
        {
            float s = (seed % 613) * 0.41f + (side > 0 ? 13.7f : 0f);
            return 4f + 10f * Mathf.Clamp01(Mathf.PerlinNoise(along * 0.011f + s, 1.7f) * 1.2f - 0.1f);
        }

        /// <summary>해안선에서 안쪽으로 u만큼 들어간 곳의 땅 높이(단면 표).</summary>
        private static float BankGround(float u, float h)
        {
            if (u <= 0f) return Mathf.Lerp(SeaY + 0.25f, BaseY, Mathf.Clamp01(-u / 3f));
            if (u <= 2.5f) return Mathf.Lerp(SeaY + 0.25f, 0.4f, u / 2.5f);
            if (u <= 8f) return Mathf.Lerp(0.4f, h * 0.45f, (u - 2.5f) / 5.5f);
            if (u <= 20f) return Mathf.Lerp(h * 0.45f, h, (u - 8f) / 12f);
            if (u <= 45f) return Mathf.Lerp(h, h * 1.05f, (u - 20f) / 25f);
            return Mathf.Lerp(h * 1.05f, h * 0.9f, Mathf.Clamp01((u - 45f) / (BankDepth - 45f)));
        }

        private static readonly float[] BankColumns = { -3f, 0f, 2.5f, 8f, 14f, 20f, 32f, 45f, 65f, BankDepth };

        /// <summary>
        /// 기슭 표면 한 점(기슭 루트 좌표): 단면 표(BankGround)에 열 위치 흔들기와 내륙 구릉을 더한다.
        /// 물길 축 거리(along)로만 정해져 이웃 구간과 꼭짓점이 그대로 이어진다. 물가(u ≤ 0)는 흔들지 않는다 — 해안선·거품과 맞춤.
        /// </summary>
        private static Vector3 BankSurface(float along, float localZ, float u, int side, int seed, float shore, float h)
        {
            float uu = u;
            if (u > 0.5f && u < BankDepth - 0.5f)
                uu += (Mathf.PerlinNoise(along * 0.13f + u * 0.07f, seed % 89 + side * 3.3f) - 0.5f) * Mathf.Min(4f, u * 0.25f);
            float y = BankGround(uu, h);
            if (uu > 10f)
                y += (Mathf.PerlinNoise(along * 0.045f + seed % 37, uu * 0.05f + side * 7.7f) - 0.5f) * Mathf.Min(1f, (uu - 10f) / 15f) * h * 0.5f;
            return new Vector3(side * (shore + uu), y, localZ);
        }

        private const float CliffSlope = 0.8f;   // 칸 법선의 위 성분이 이보다 작으면(약 37° 넘게 가파르면) 절벽

        /// <summary>물길 축 거리 cellAlong에서 시작하는 칸(BankStep 길이)의 c번째 열이 절벽인가. 이웃 구간 칸도 같은 답(축 거리로만 정함).</summary>
        private static bool BankCliff(float cellAlong, int c, int side, int seed)
        {
            float a0 = cellAlong, a1 = cellAlong + BankStep;
            float s0 = BankShore(a0, side, seed), s1 = BankShore(a1, side, seed);
            float h0 = BankHeight(a0, side, seed), h1 = BankHeight(a1, side, seed);
            Vector3 a = BankSurface(a0, 0f, BankColumns[c], side, seed, s0, h0);
            Vector3 b = BankSurface(a0, 0f, BankColumns[c + 1], side, seed, s0, h0);
            Vector3 d = BankSurface(a1, BankStep, BankColumns[c], side, seed, s1, h1);
            Vector3 e = BankSurface(a1, BankStep, BankColumns[c + 1], side, seed, s1, h1);
            var n = Vector3.Cross(e - a, d - b);
            if (n.y < 0f) n = -n;
            float wobble = (Mathf.PerlinNoise((cellAlong + BankStep * 0.5f) * 0.05f + c * 0.9f, side * 4.3f + seed % 53) - 0.5f) * 0.2f;
            return n.normalized.y < CliffSlope + wobble;
        }

        public static IslandInfo BuildBank(int index, int side, int seed, Transform parent)
        {
            LoadMaterials();
            float along0 = index * BankSegmentLength;
            var root = new GameObject(side > 0 ? "Strait bank R" : "Strait bank L");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(SeaRegion.ChannelPoint(along0, 0f), Quaternion.LookRotation(SeaRegion.ChannelAxis, Vector3.up));
            root.layer = Islands.Layer;
            var rng = new System.Random(unchecked(seed * 92821 ^ index * 68917 ^ side * 1931));

            int samples = Mathf.RoundToInt(BankSegmentLength / BankStep) + 1;
            var shore = new float[samples];
            var height = new float[samples];
            float maxH = 0f;
            for (int k = 0; k < samples; k++)
            {
                float a = along0 + k * BankStep;
                shore[k] = BankShore(a, side, seed);
                height[k] = BankHeight(a, side, seed);
                maxH = Mathf.Max(maxH, height[k]);
            }

            // ---- 지형 메시(재질별 부분 메시: 0 물속 바위, 1 해변, 2 절벽, 3 풀)
            var verts = new List<Vector3>();
            var subs = new[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            Vector3 P(int k, int c) => BankSurface(along0 + k * BankStep, k * BankStep, BankColumns[c], side, seed, shore[k], height[k]);
            Vector3 expected = new Vector3(-side, 1.2f, 0f).normalized;   // 물길 쪽·위를 향한 면이 보인다
            void Tri(int sub, Vector3 a, Vector3 b, Vector3 c)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), expected) < 0f) (b, c) = (c, b);
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                subs[sub].Add(i); subs[sub].Add(i + 1); subs[sub].Add(i + 2);
            }
            for (int k = 0; k < samples - 1; k++)
            {
                for (int c = 0; c < BankColumns.Length - 1; c++)
                {
                    // 물속 바위 · 해변 모래 · 그 위는 칸의 평균 경사로 절벽/풀. 절벽 띠의 앞뒤 끝 칸은 대각선으로 반만 절벽(모서리를 깎음)
                    Vector3 a = P(k, c), b = P(k, c + 1), d = P(k + 1, c), e = P(k + 1, c + 1);
                    int near = c == 0 ? 0 : c == 1 ? 1 : 3, far = near;
                    if (c >= 2 && BankCliff(along0 + k * BankStep, c, side, seed))
                    {
                        bool before = BankCliff(along0 + (k - 1) * BankStep, c, side, seed);
                        bool after = BankCliff(along0 + (k + 1) * BankStep, c, side, seed);
                        near = before || !after ? 2 : 3;   // (a,b,e): 이 칸의 앞쪽(k) 변
                        far = after || !before ? 2 : 3;    // (a,e,d): 뒤쪽(k+1) 변
                    }
                    Tri(near, a, b, e);
                    Tri(far, a, e, d);
                }
            }
            var mesh = new Mesh { name = "Strait bank" };
            mesh.SetVertices(verts);
            mesh.subMeshCount = 4;
            for (int s = 0; s < 4; s++) mesh.SetTriangles(subs[s], s);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var land = new GameObject("Bank land", typeof(MeshFilter), typeof(MeshRenderer));
            land.transform.SetParent(root.transform, false);
            land.GetComponent<MeshFilter>().sharedMesh = mesh;
            var lr = land.GetComponent<MeshRenderer>();
            lr.sharedMaterials = new[] { s_rock, s_sand, s_cliff, s_grass };
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            // ---- 판정: 8m마다 상자(해안선 평균 + 0.8m부터 내륙으로)
            for (int k = 0; k < samples - 1; k += 2)
            {
                int k2 = Mathf.Min(k + 2, samples - 1);
                float inner = (shore[k] + shore[(k + k2) / 2] + shore[k2]) / 3f + 0.8f;
                var box = new GameObject("Bank collider");
                box.transform.SetParent(root.transform, false);
                box.transform.localPosition = new Vector3(side * (inner + BankDepth * 0.5f), 4f, (k + k2) * 0.5f * BankStep);
                var col = box.AddComponent<BoxCollider>();
                col.size = new Vector3(BankDepth, 16f, (k2 - k) * BankStep + 0.2f);
            }

            // ---- 해안 거품 띠
            if (s_foam != null)
            {
                var fv = new List<Vector3>(); var fu = new List<Vector2>(); var fc = new List<Color32>(); var ft = new List<int>();
                for (int k = 0; k < samples; k++)
                {
                    float wobble = Mathf.Sin((along0 + k * BankStep) * 0.21f) * 0.6f;
                    fv.Add(new Vector3(side * (shore[k] - 5f + wobble), SeaY + 0.03f, k * BankStep));
                    fv.Add(new Vector3(side * (shore[k] - 0.4f), SeaY + 0.03f, k * BankStep));
                    fu.Add(new Vector2(0f, along0 + k * BankStep)); fu.Add(new Vector2(1f, along0 + k * BankStep));
                    fc.Add(new Color32(255, 255, 255, 150)); fc.Add(new Color32(255, 255, 255, 150));
                    if (k == 0) continue;
                    int b = (k - 1) * 2;
                    if (side > 0) { ft.Add(b); ft.Add(b + 2); ft.Add(b + 1); ft.Add(b + 1); ft.Add(b + 2); ft.Add(b + 3); }
                    else { ft.Add(b); ft.Add(b + 1); ft.Add(b + 2); ft.Add(b + 1); ft.Add(b + 3); ft.Add(b + 2); }
                }
                var foam = new Mesh { name = "Bank foam" };
                foam.SetVertices(fv); foam.SetUVs(0, fu); foam.SetColors(fc); foam.SetTriangles(ft, 0);
                foam.RecalculateBounds();
                AddPart(root.transform, "Shore foam", foam, s_foam, collider: false, shadows: false);
            }

            // ---- 장식: 낮은 기슭엔 나무, 물가엔 바위
            for (int k = 0; k < samples - 1; k++)
            {
                if (height[k] < 9f && rng.NextDouble() < 0.55)
                {
                    float u = Range(rng, 12f, 40f);
                    float z = (k + (float)rng.NextDouble()) * BankStep;
                    var spot = BankSurface(along0 + z, z, u, side, seed, shore[k], height[k]);
                    AddTree(root.transform, spot + Vector3.down * 0.3f, rng);
                }
                if (rng.NextDouble() < 0.18)
                    AddRock(root.transform, new Vector3(side * (shore[k] + Range(rng, -0.5f, 2f)), 0f, (k + 0.5f) * BankStep),
                            Range(rng, 1f, 2.4f), Range(rng, 1.2f, 3f), rng, s_cliff);
            }
            SetLayer(root, Islands.Layer);

            // ---- 등록(레이더·섬 간격): 해안선 표본 + 내륙 끝
            var rim = new List<Vector2>();
            for (int k = 0; k < samples; k++) { var w = root.transform.TransformPoint(new Vector3(side * shore[k], 0f, k * BankStep)); rim.Add(new Vector2(w.x, w.z)); }
            for (int k = samples - 1; k >= 0; k -= samples - 1) { var w = root.transform.TransformPoint(new Vector3(side * (shore[k] + 60f), 0f, k * BankStep)); rim.Add(new Vector2(w.x, w.z)); }
            float mid = (samples - 1) * BankStep * 0.5f;
            var info = new IslandInfo
            {
                Center = root.transform.TransformPoint(new Vector3(side * (SeaRegion.ChannelHalfWidth + 30f), 0f, mid)),
                Radius = 30f, Height = maxH, Rocky = true, Rim = rim.ToArray(), Root = root, Bank = true,
            };
            Islands.Register(info);
            return info;
        }
    }
}

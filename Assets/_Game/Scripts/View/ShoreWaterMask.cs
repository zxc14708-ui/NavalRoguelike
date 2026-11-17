using System.Collections.Generic;
using UnityEngine;
using Game.World;

namespace Game.View
{
    /// <summary>
    /// 해안 물빛(시험, 2026-11-01 — Boat Attack Water의 얕은 물·해안 거품·물 밑 빛 무늬 방식을 참고해 새로 씀, 코드 미복사).
    /// 바다(Naval/Ocean)는 불투명이라 깊이 텍스처로 해안을 알 수 없으므로, 섬 수면 윤곽(IslandInfo.Rim)에서
    /// 해안까지 거리를 작은 텍스처(256², 512m — 2m 간격)에 구워 전역으로 넘긴다. 셰이더는 거리 0~20m에 얕은 물빛·물 밑 빛 무늬,
    /// 0~9m에 해안 거품과 밀려오는 물결 줄을 그린다.
    /// 섬이 바뀌거나(수·위치) 배가 무늬 가운데에서 96m 넘게 벗어나면 다시 굽는다. 꺼져 있으면 아무것도 굽지 않는다.
    /// 군항(시작 화면)은 전투 섬이 아니라서, 시작 화면이 열려 있는 동안 기지 모래 메시(MAT_base_sand)의 바깥 윤곽(볼록 껍질)을 해안으로 더하고
    /// 기지 섬 가운데로 굽는다(기지는 전투 바다와 떨어진 무대에 있다).
    /// 부두(2026-11-03): 기지 안의 모든 메시(여러 물체가 한 메시로 합쳐져 있다)를 이어진 조각마다 나눠, 바닥이 수면에 닿는 조각
    ///   (가장 낮은 점이 수면 −0.9 위 0.5m 안 — 부두·안벽·말뚝·정박한 배)의 볼록 껍질을 부두 윤곽으로 쓴다. 이름으로 고르면 어떤 메시가
    ///   부두인지 틀리기 쉬웠다. 부두는 얕은 물빛 없이 좁은 거품만 — 거리를 두 번째 채널(G)에 따로 굽는다.
    /// </summary>
    public class ShoreWaterMask : MonoBehaviour
    {
        private const int Resolution = 256;
        private const float Size = 512f;         // m
        private const float HarborSize = 220f;   // m — 군항 기지 섬만
        private const float MaxDistance = 32f;   // 텍스처 1 = 이 거리 이상
        private const float Recenter = 96f;

        /// <summary>해안 물빛을 켠다(2026-11-01 스타일 바다와 함께 기본 켬).</summary>
        public static bool Enabled = true;
        // 전투 섬 말고 해안으로 쓰는 윤곽(군항 기지 섬)
        private static readonly List<Vector2[]> s_extraRims = new();
        // 부두 물 닿는 선(월드 xz 선분)
        private static readonly List<(Vector2 a, Vector2 b)> s_pierLines = new();
        private float[] _pierDist;

        /// <summary>검증용: 지금 부두 윤곽 수.</summary>
        public static int PierOutlines => s_pierLines.Count;
        /// <summary>검증용: 부두 윤곽을 낸 메시별 조각 수·넓이 합(진단).</summary>
        public static string PierSources { get; private set; } = "";
        private static MeshFilter s_baseSand;
        private float _baseSearchAt;
        public static ShoreWaterMask Instance { get; private set; }
        /// <summary>검증용: 마지막으로 구운 섬 수 · 해안 20m 안 텍셀 비율.</summary>
        public int BakedIslands { get; private set; }
        public float ShallowFraction { get; private set; }

        private static readonly int MaskId = Shader.PropertyToID("_NavalShoreMask");
        private static readonly int RectId = Shader.PropertyToID("_NavalShoreRect");
        private static readonly int FxId = Shader.PropertyToID("_NavalShoreFx");

        private Texture2D _texture;
        private float[] _dist;
        private Color32[] _pixels;
        private Vector2 _center = new(float.MaxValue, float.MaxValue);
        private int _signature;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            if (Instance != null) return;
            var go = new GameObject("Shore water mask");
            DontDestroyOnLoad(go);
            go.AddComponent<ShoreWaterMask>();
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) { s_extraRims.Clear(); s_pierLines.Clear(); s_baseSand = null; }

        /// <summary>
        /// 군항 기지 섬: 모래 메시(MAT_base_sand)의 월드 정점으로 수면 윤곽(볼록 껍질)을 만든다.
        /// 기지는 시작 화면이 실행 중에 프리팹(Resources/Environment/NavalBaseMenu)으로 만들므로 씬이 열릴 때는 아직 없다 —
        /// 시작 화면(NavalBaseMenu)이 열려 있는 동안 1초마다 찾아보고, 찾으면 멈춘다. 기지가 사라지면(출항) 다시 비운다.
        /// </summary>
        private static void FindBaseShores()
        {
            s_extraRims.Clear();
            s_pierLines.Clear();
            s_baseSand = null;
            var filters = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
            foreach (var mf in filters)
            {
                var mesh = mf.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                if (!mesh.name.Contains("MAT_base_sand") || mesh.name.Contains("wet")) continue;
                var verts = mesh.vertices;
                var pts = new List<Vector2>(verts.Length);
                var m = mf.transform.localToWorldMatrix;
                foreach (var v in verts) { var w = m.MultiplyPoint3x4(v); pts.Add(new Vector2(w.x, w.z)); }
                var hull = ConvexHull(pts);
                if (hull.Length >= 3) { s_extraRims.Add(hull); s_baseSand = mf; }
            }
            // 부두: 기지 구조물(안벽·부두·말뚝·정박한 배)의 삼각형이 수면과 만나는 선(물 닿는 선)을 모은다. 볼록 껍질은 ㄷ자 부두·안벽처럼
            // 오목한 덩어리에서 부두 사이 물까지 덮어 거품 얼룩이 생겼다. 기지는 전투 바다와 떨어진 무대(높이 약 −2000)라 수면 높이는 기지 바다 메시에서 읽는다.
            float waterY = float.NaN;
            if (s_baseSand != null)
                foreach (var mf in s_baseSand.transform.root.GetComponentsInChildren<MeshFilter>())
                    if (mf.sharedMesh != null && mf.sharedMesh.name.Contains("MAT_base_water") && !mf.sharedMesh.name.Contains("shallow"))
                    { waterY = mf.transform.TransformPoint(mf.sharedMesh.bounds.center).y; break; }
            var sources = new Dictionary<string, int>();
            if (s_baseSand != null && !float.IsNaN(waterY))
                foreach (var mf in s_baseSand.transform.root.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh = mf.sharedMesh;
                    if (mesh == null || !mesh.isReadable || !IsPierMesh(mesh.name)) continue;
                    int before = s_pierLines.Count;
                    Waterlines(mesh, mf.transform.localToWorldMatrix, waterY, s_pierLines);
                    if (s_pierLines.Count > before) sources[mesh.name] = s_pierLines.Count - before;
                }
            var sb = new System.Text.StringBuilder();
            sb.Append($"수면 {waterY:0.00} · ");
            foreach (var kv in sources) sb.Append($"{kv.Key.Replace("MESH_combined_MAT_base_", "")} {kv.Value} · ");
            PierSources = sb.ToString();
            if (Instance != null) Instance._signature = 0;   // 다시 굽게
        }

        /// <summary>
        /// 부두 거품을 낼 구조물 메시인가: 안벽·부두 콘크리트, 부두 상판, 말뚝, 정박한 배 선체. 작은 소품(상자·기둥·방현재·계선주·풀 등)은
        /// 물가에 흩어져 있어 넣으면 물 전체에 거품 얼룩이 생겼다.
        /// </summary>
        private static bool IsPierMesh(string name)
            => name.Contains("concrete") || name.Contains("quay_face") || name.Contains("dock_grey") || name.Contains("_pile")
               || name.Contains("hull_") || name.Contains("ProceduralBow");

        /// <summary>삼각형마다 수면(높이 waterY)과 만나는 선분을 모은다(월드 xz).</summary>
        private static void Waterlines(Mesh mesh, Matrix4x4 toWorld, float waterY, List<(Vector2, Vector2)> into)
        {
            var v = mesh.vertices;
            var tris = mesh.triangles;
            var w = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++) w[i] = toWorld.MultiplyPoint3x4(v[i]);
            var hits = new List<Vector2>(3);
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                hits.Clear();
                for (int e = 0; e < 3; e++)
                {
                    Vector3 p = w[tris[t + e]], q = w[tris[t + (e + 1) % 3]];
                    float dp = p.y - waterY, dq = q.y - waterY;
                    if ((dp < 0f) == (dq < 0f) || Mathf.Abs(dp - dq) < 1e-5f) continue;
                    float u = dp / (dp - dq);
                    var x = Vector3.Lerp(p, q, u);
                    hits.Add(new Vector2(x.x, x.z));
                }
                if (hits.Count >= 2 && (hits[0] - hits[1]).sqrMagnitude > 1e-4f) into.Add((hits[0], hits[1]));
            }
        }

        private static Vector2[] ConvexHull(List<Vector2> pts)
        {
            pts.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var hull = new List<Vector2>();
            for (int pass = 0; pass < 2; pass++)
            {
                int start = hull.Count;
                for (int i = 0; i < pts.Count; i++)
                {
                    var p = pass == 0 ? pts[i] : pts[pts.Count - 1 - i];
                    while (hull.Count >= start + 2 && Cross(hull[^2], hull[^1], p) <= 0f) hull.RemoveAt(hull.Count - 1);
                    hull.Add(p);
                }
                hull.RemoveAt(hull.Count - 1);
            }
            return hull.ToArray();
        }

        private static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        /// <summary>무늬 가운데: 군항이 열려 있으면 기지 섬 가운데(기지는 따로 떨어진 무대에 있다), 아니면 플레이어 배, 없으면 카메라가 바라보는 수면.</summary>
        private static Vector2 Focus()
        {
            if (s_extraRims.Count > 0)
            {
                Vector2 c = Vector2.zero;
                foreach (var q in s_extraRims[0]) c += q;
                return c / s_extraRims[0].Length;
            }
            var player = Game.Core.GameManager.Instance != null ? Game.Core.GameManager.Instance.Player : null;
            if (player != null) return new Vector2(player.transform.position.x, player.transform.position.z);
            var cam = Camera.main;
            if (cam == null) return Vector2.zero;
            var o = cam.transform.position; var d = cam.transform.forward;
            float t = d.y < -0.05f ? (-0.9f - o.y) / d.y : 0f;
            var p = o + d * Mathf.Max(0f, t);
            return new Vector2(p.x, p.z);
        }

        private void OnEnable() => Instance = this;

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
            Shader.SetGlobalFloat(FxId, 0f);
        }

        private void OnDestroy()
        {
            if (_texture != null) Destroy(_texture);
        }

        private void LateUpdate()
        {
            Shader.SetGlobalFloat(FxId, Enabled ? 1f : 0f);
            if (!Enabled) return;
            // 군항 기지(시작 화면이 열려 있는 동안만 — 실행 중에 생기고 출항하면 사라진다)
            bool harbor = Game.UI.NavalBaseMenu.Active != null;
            if (s_extraRims.Count > 0 && (!harbor || s_baseSand == null || !s_baseSand.gameObject.activeInHierarchy)) { s_extraRims.Clear(); s_pierLines.Clear(); s_baseSand = null; _signature = 0; }
            if (harbor && s_extraRims.Count == 0 && Time.unscaledTime >= _baseSearchAt)
            {
                _baseSearchAt = Time.unscaledTime + 1f;
                FindBaseShores();
            }
            var here = Focus();
            int sig = Signature();
            if (_texture != null && sig == _signature && Vector2.Distance(here, _center) < Recenter) return;
            Bake(here);
            _signature = sig;
        }

        /// <summary>섬 목록이 바뀌었는지 가볍게 본다(수 + 중심 좌표 합).</summary>
        private static int Signature()
        {
            var all = Islands.All;
            unchecked
            {
                int h = all.Count * 7919 + s_extraRims.Count * 104729 + s_pierLines.Count * 1299709;
                for (int i = 0; i < all.Count; i++)
                    h = h * 31 + Mathf.RoundToInt(all[i].Center.x * 4f) * 17 + Mathf.RoundToInt(all[i].Center.z * 4f);
                return h;
            }
        }

        /// <summary>검증용: 지금 거리 텍스처를 그림으로(R 해안 · G 부두, 밝을수록 멀다).</summary>
        public void SaveMask(string path)
        {
            if (_texture == null) return;
            var png = new Texture2D(Resolution, Resolution, TextureFormat.RGB24, false);
            var px = new Color32[_pixels.Length];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(_pixels[i].r, _pixels[i].g, 0, 255);
            png.SetPixels32(px);
            png.Apply();
            System.IO.File.WriteAllBytes(path, png.EncodeToPNG());
            Destroy(png);
        }

        /// <summary>검증용: 지금 자리에서 바로 다시 굽는다.</summary>
        public void BakeNow()
        {
            Bake(Focus());
            _signature = Signature();
        }

        private void Bake(Vector2 center)
        {
            if (_texture == null)
            {
                _texture = new Texture2D(Resolution, Resolution, TextureFormat.RG16, false, true)
                {
                    name = "Shore distance mask",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
                _dist = new float[Resolution * Resolution];
                _pierDist = new float[Resolution * Resolution];
                _pixels = new Color32[Resolution * Resolution];
            }
            _center = center;
            // 군항은 섬 하나(약 170m)만 덮으면 되므로 220m로 좁혀 촘촘하게(0.9m 간격) — 폭 2~5m 부두 둘레 거품이 보이게
            float size = s_extraRims.Count > 0 ? HarborSize : Size;
            Vector2 min = center - Vector2.one * (size * 0.5f);
            float cell = size / Resolution;
            for (int i = 0; i < _dist.Length; i++) { _dist[i] = MaxDistance; _pierDist[i] = MaxDistance; }

            int baked = 0;
            var rims = new List<(Vector2[] rim, bool closed, bool pier)>();
            foreach (var island in Islands.All)
            {
                if (island.Platform && island.Rim != null)
                {
                    // 유전 플랫폼(4스테이지): 윤곽 = 기둥 중심들 — 기둥 사이 바다를 얕은 물로 칠하지 않고 기둥마다 작은 고리만
                    foreach (var leg in island.Rim)
                        rims.Add((new[] { leg + new Vector2(-1.4f, -1.4f), leg + new Vector2(1.4f, -1.4f), leg + new Vector2(1.4f, 1.4f), leg + new Vector2(-1.4f, 1.4f) }, true, false));
                    continue;
                }
                rims.Add((island.Rim, !island.Bank, false));   // 수로 기슭은 열린 선이라 안쪽 판정을 하지 않는다
            }
            foreach (var extra in s_extraRims) rims.Add((extra, true, false));
            foreach (var (rim, closed, pier) in rims)
            {
                var target = pier ? _pierDist : _dist;
                if (rim == null || rim.Length < 2) continue;
                // 윤곽 상자 + 최대 거리 안의 텍셀만
                Vector2 lo = rim[0], hi = rim[0];
                foreach (var r in rim) { lo = Vector2.Min(lo, r); hi = Vector2.Max(hi, r); }
                int x0 = Mathf.Max(0, Mathf.FloorToInt((lo.x - MaxDistance - min.x) / cell));
                int x1 = Mathf.Min(Resolution - 1, Mathf.CeilToInt((hi.x + MaxDistance - min.x) / cell));
                int y0 = Mathf.Max(0, Mathf.FloorToInt((lo.y - MaxDistance - min.y) / cell));
                int y1 = Mathf.Min(Resolution - 1, Mathf.CeilToInt((hi.y + MaxDistance - min.y) / cell));
                if (x0 > x1 || y0 > y1) continue;
                baked++;
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(min.x + (x + 0.5f) * cell, min.y + (y + 0.5f) * cell);
                    float d = closed && Inside(rim, p) ? 0f : EdgeDistance(rim, p, closed);
                    int k = y * Resolution + x;
                    if (d < target[k]) target[k] = d;
                }
            }

            if (s_pierLines.Count > 0) PierDistance(min, cell);

            int shallow = 0;
            for (int i = 0; i < _dist.Length; i++)
            {
                if (_dist[i] < 20f) shallow++;
                byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(_dist[i] / MaxDistance) * 255f);
                byte g = (byte)Mathf.RoundToInt(Mathf.Clamp01(_pierDist[i] / MaxDistance) * 255f);
                _pixels[i] = new Color32(b, g, 0, 255);
            }
            _texture.SetPixels32(_pixels);
            _texture.Apply(false, false);
            BakedIslands = baked;
            ShallowFraction = shallow / (float)_dist.Length;
            Shader.SetGlobalTexture(MaskId, _texture);
            Shader.SetGlobalVector(RectId, new Vector4(min.x, min.y, 1f / size, 1f / size));
        }

        /// <summary>부두 물 닿는 선을 칸에 찍고(0), 두 번 훑는 거리 변환(3·4 체임퍼)으로 각 칸에서 가장 가까운 선까지 거리(m)를 _pierDist에.</summary>
        private void PierDistance(Vector2 min, float cell)
        {
            const float Far = 1e6f;
            int n = Resolution;
            for (int i = 0; i < _pierDist.Length; i++) _pierDist[i] = Far;
            foreach (var (a, b) in s_pierLines)
            {
                float len = (b - a).magnitude;
                int steps = Mathf.Max(1, Mathf.CeilToInt(len / (cell * 0.5f)));
                for (int k = 0; k <= steps; k++)
                {
                    var p = Vector2.Lerp(a, b, k / (float)steps);
                    int x = Mathf.FloorToInt((p.x - min.x) / cell), y = Mathf.FloorToInt((p.y - min.y) / cell);
                    if (x >= 0 && y >= 0 && x < n && y < n) _pierDist[y * n + x] = 0f;
                }
            }
            float d1 = cell, d2 = cell * 1.41421f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int i = y * n + x;
                    float v = _pierDist[i];
                    if (x > 0) v = Mathf.Min(v, _pierDist[i - 1] + d1);
                    if (y > 0)
                    {
                        v = Mathf.Min(v, _pierDist[i - n] + d1);
                        if (x > 0) v = Mathf.Min(v, _pierDist[i - n - 1] + d2);
                        if (x < n - 1) v = Mathf.Min(v, _pierDist[i - n + 1] + d2);
                    }
                    _pierDist[i] = v;
                }
            for (int y = n - 1; y >= 0; y--)
                for (int x = n - 1; x >= 0; x--)
                {
                    int i = y * n + x;
                    float v = _pierDist[i];
                    if (x < n - 1) v = Mathf.Min(v, _pierDist[i + 1] + d1);
                    if (y < n - 1)
                    {
                        v = Mathf.Min(v, _pierDist[i + n] + d1);
                        if (x < n - 1) v = Mathf.Min(v, _pierDist[i + n + 1] + d2);
                        if (x > 0) v = Mathf.Min(v, _pierDist[i + n - 1] + d2);
                    }
                    _pierDist[i] = Mathf.Min(v, MaxDistance);
                }
        }

        private static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        private static float EdgeDistance(Vector2[] poly, Vector2 p, bool closed)
        {
            float best = float.MaxValue;
            int n = closed ? poly.Length : poly.Length - 1;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length], ab = b - a;
                float u = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                best = Mathf.Min(best, (a + ab * u - p).sqrMagnitude);
            }
            return Mathf.Sqrt(best);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using Game.Ship;
using Game.View;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 응급 차폐 장갑 연출(2026-11-23 사용자 시안 C "장갑판 전개"). 예전엔 배를 감싼 반투명 구 하나라 너무 단순했다.
    ///   - 선체 외곽(선체 조각 상자 · 없으면 모든 부품)을 따라 하늘색 투명 장갑판을 두르고, 선수부터 선미로 차례로 솟는다(살짝 튀어 오름).
    ///   - 판은 수직으로 선다(2026-11-23 사용자: 기울이지 말 것). 위 테두리 · 양 옆 이음매가 밝고, 판마다 가운데에 닻 문양.
    ///     빛줄이 선수 → 선미로 훑고 지나간다.
    ///   - 맞으면 가장 가까운 판이 번쩍이고 이웃 판으로 번진다(<see cref="Hit"/>).
    ///   - 끝나기 0.6초 전부터 깜빡이고, 마지막 0.3초에 선미부터 내려앉는다.
    /// 흔들리는 모델(ShipVisualSway)에 붙여 배와 함께 기운다. 메시 하나(판마다 정점 16개)를 매 프레임 고쳐 쓴다.
    /// </summary>
    public sealed class ArmorPanelFx : MonoBehaviour
    {
        private struct Panel
        {
            public Vector3 A, B;        // 아래 두 끝(이음매만큼 줄임, 앵커 좌표)
            public Vector3 Out;         // 바깥쪽(수평)
            public float Order;         // 솟는 순서(0 = 선수)
            public float U;             // 선수 0 → 선미 1(빛줄)
            public float Flash;
        }

        public const float RiseTime = 0.22f, LowerTime = 0.3f, BlinkTime = 0.6f;

        // 닻 문양(2026-11-23 사용자 요청): 판 가운데 선분들(x −0.5~0.5, y 0 아래 ~ 1 위). 선분마다 얇은 사각형 하나
        private static readonly Vector2[] AnchorSegs = BuildAnchor();
        private static int AnchorQuads => AnchorSegs.Length / 2;
        private static int VertsPer => 16 + AnchorQuads * 4;
        /// <summary>검증용: 닻 문양이 들어간 판 수.</summary>
        public int EmblemCount { get; private set; }

        private static Vector2[] BuildAnchor()
        {
            var s = new List<Vector2>();
            void Seg(Vector2 a, Vector2 b) { s.Add(a); s.Add(b); }
            // 고리
            var rc = new Vector2(0f, 0.9f); const float rr = 0.075f;
            for (int i = 0; i < 14; i++)
            {
                float a0 = i * Mathf.PI * 2f / 14f, a1 = (i + 1) * Mathf.PI * 2f / 14f;
                Seg(rc + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * rr, rc + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * rr);
            }
            Seg(new Vector2(0f, 0.825f), new Vector2(0f, 0.1f));          // 자루
            Seg(new Vector2(-0.22f, 0.72f), new Vector2(0.22f, 0.72f));   // 가로대
            // 팔(아래 반원)
            var ac = new Vector2(0f, 0.42f); const float ar = 0.32f;
            const int arcN = 10;
            for (int i = 0; i < arcN; i++)
            {
                float a0 = Mathf.Deg2Rad * Mathf.Lerp(200f, 340f, i / (float)arcN), a1 = Mathf.Deg2Rad * Mathf.Lerp(200f, 340f, (i + 1) / (float)arcN);
                Seg(ac + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * ar, ac + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * ar);
            }
            // 갈고리 끝(미늘)
            foreach (float side in new[] { -1f, 1f })
            {
                var tip = ac + new Vector2(Mathf.Cos(Mathf.Deg2Rad * (side < 0 ? 200f : 340f)), Mathf.Sin(Mathf.Deg2Rad * 200f)) * ar;
                Seg(tip, tip + new Vector2(side * 0.02f, 0.11f));
                Seg(tip, tip + new Vector2(-side * 0.1f, 0.03f));
            }
            return s.ToArray();
        }

        private readonly List<Panel> _panels = new();
        private Mesh _mesh;
        private Vector3[] _v;
        private Color[] _c;
        private float _start, _duration, _height, _tilt, _stagger, _maxOrder;
        private Color _color;

        /// <summary>검증용: 판 수 · 지금 가장 높이 솟은 비율 · 번쩍인 횟수 · 판 둘레가 배를 감싸는지(외곽 상자).</summary>
        public int PanelCount => _panels.Count;
        public float Tilt => _tilt;
        public float MaxRise { get; private set; }
        public float MinRise { get; private set; }
        public int Hits { get; private set; }
        public Bounds OutlineLocal { get; private set; }
        public static ArmorPanelFx Last { get; private set; }

        public static ArmorPanelFx Deploy(ShipController ship, float duration, Color color)
        {
            if (ship == null) return null;
            // 같은 배에 이미 있으면 바꾼다(겹치지 않게)
            foreach (var old in ship.GetComponentsInChildren<ArmorPanelFx>()) Destroy(old.gameObject);
            var go = new GameObject("ArmorPanelFx");
            var fx = go.AddComponent<ArmorPanelFx>();
            fx.Build(ship, duration, color);
            Last = fx;
            return fx;
        }

        private void Build(ShipController ship, float duration, Color color)
        {
            _start = Time.time; _duration = Mathf.Max(0.5f, duration); _color = color;
            var root = ship.transform;
            Matrix4x4 inv = root.worldToLocalMatrix;

            // 선체 외곽: 블록 선체면 선체 조각만(포신 · 날개처럼 뱃전 밖으로 나온 것 빼고)
            var boxes = new List<Bounds>();
            var hull = ship.GetComponentInChildren<ShipHullBuilder>();
            if (hull != null && hull.HullRoot != null) Collect(hull.HullRoot, inv, boxes);
            if (boxes.Count == 0) Collect(root, inv, boxes);
            var grid = ship.Grid;
            float cell = grid != null ? grid.CellSize : 2f;
            float minX, maxX, minZ, maxZ, top;
            if (boxes.Count > 0)
            {
                minX = minZ = top = float.PositiveInfinity; maxX = maxZ = float.NegativeInfinity; top = float.NegativeInfinity;
                foreach (var b in boxes) { minX = Mathf.Min(minX, b.min.x); maxX = Mathf.Max(maxX, b.max.x); minZ = Mathf.Min(minZ, b.min.z); maxZ = Mathf.Max(maxZ, b.max.z); top = Mathf.Max(top, b.max.y); }
            }
            else
            {
                float hl = grid != null ? (grid.MaxHalfLength + 0.5f) * cell : 8f, hb = grid != null ? (grid.MaxHalfBeam + 0.5f) * cell : 3f;
                minX = -hb; maxX = hb; minZ = -hl; maxZ = hl; top = grid != null ? grid.DeckHeight : 1f;
            }
            float cx = (minX + maxX) * 0.5f, len = Mathf.Max(2f, maxZ - minZ);
            float margin = 0.45f;
            _height = Mathf.Clamp(len * 0.09f, 1.6f, 3.2f);
            _tilt = 0f;   // 수직(2026-11-23 사용자 요청 — 예전엔 바깥으로 20% 기울었다)
            float baseY = top - 0.5f;   // 뱃전을 덮게 갑판보다 조금 아래부터

            // 길이 방향으로 나눠 칸마다 폭(그 칸에 걸친 조각 중 가장 넓은 것)
            int n = Mathf.Clamp(Mathf.RoundToInt(len / 2.2f), 4, 24);
            var half = new float[n];
            for (int i = 0; i < n; i++)
            {
                float z0 = minZ + len * i / n, z1 = minZ + len * (i + 1) / n;
                float w = 0f;
                foreach (var b in boxes)
                    if (b.max.z > z0 && b.min.z < z1) w = Mathf.Max(w, Mathf.Max(cx - b.min.x, b.max.x - cx));
                half[i] = w;
            }
            if (boxes.Count == 0) for (int i = 0; i < n; i++) half[i] = (maxX - minX) * 0.5f;
            for (int i = 1; i < n; i++) if (half[i] <= 0f) half[i] = half[i - 1];
            for (int i = n - 2; i >= 0; i--) if (half[i] <= 0f) half[i] = half[i + 1];
            // 마디(칸 경계)의 폭 = 이웃 두 칸 중 넓은 쪽(외곽 안으로 파고들지 않게)
            var node = new float[n + 1];
            for (int i = 0; i <= n; i++) node[i] = Mathf.Max(i > 0 ? half[i - 1] : 0f, i < n ? half[i] : 0f) + margin;
            node[n] = Mathf.Max(0.3f, node[n] * 0.55f);   // 선수 끝은 좁혀 뾰족하게

            var port = new Vector3[n + 1];
            var star = new Vector3[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float z = minZ + len * i / n;
                port[i] = new Vector3(cx - node[i], baseY, z);
                star[i] = new Vector3(cx + node[i], baseY, z);
            }
            var tip = new Vector3(cx, baseY, maxZ + margin + 0.6f);

            // 앵커(흔들리는 모델) 좌표로
            var sway = ship.GetComponentInChildren<ShipVisualSway>(true);
            Transform anchor = sway != null ? sway.transform : root;
            Matrix4x4 toAnchor = sway != null ? sway.RestLocalToWorld.inverse * root.localToWorldMatrix : Matrix4x4.identity;
            transform.SetParent(anchor, false);
            transform.localPosition = Vector3.zero; transform.localRotation = Quaternion.identity; transform.localScale = Vector3.one;

            var center = new Vector3(cx, baseY, (minZ + maxZ) * 0.5f);
            void AddPanel(Vector3 a, Vector3 b, float order, float u)
            {
                Vector3 d = b - a; d.y = 0f;
                if (d.sqrMagnitude < 0.04f) return;
                Vector3 outDir = Vector3.Cross(Vector3.up, d.normalized);
                Vector3 mid = (a + b) * 0.5f;
                if (Vector3.Dot(outDir, mid - center) < 0f) outDir = -outDir;
                Vector3 gap = d.normalized * Mathf.Min(0.07f, d.magnitude * 0.1f);   // 판 사이 이음매
                _panels.Add(new Panel
                {
                    A = toAnchor.MultiplyPoint3x4(a + gap), B = toAnchor.MultiplyPoint3x4(b - gap),
                    Out = toAnchor.MultiplyVector(outDir).normalized, Order = order, U = u,
                });
            }
            // 선수 V → 양 뱃전(선수에서 선미로) → 선미
            AddPanel(port[n], tip, 0f, 0f);
            AddPanel(tip, star[n], 0f, 0f);
            for (int i = n - 1; i >= 0; i--)
            {
                float order = n - i, u = (n - i) / (float)(n + 1);
                AddPanel(port[i], port[i + 1], order, u);
                AddPanel(star[i + 1], star[i], order, u);
            }
            float sternW = star[0].x - port[0].x;
            int sternN = Mathf.Max(1, Mathf.RoundToInt(sternW / 2.4f));
            for (int k = 0; k < sternN; k++)
                AddPanel(Vector3.Lerp(star[0], port[0], k / (float)sternN), Vector3.Lerp(star[0], port[0], (k + 1) / (float)sternN), n + 1, 1f);
            _maxOrder = n + 1;
            _stagger = Mathf.Min(0.035f, 0.4f / _maxOrder);
            OutlineLocal = new Bounds(center + Vector3.up * _height * 0.5f, new Vector3(sternW, _height, tip.z - minZ));

            // 메시
            int pc = _panels.Count;
            int quads = VertsPer / 4;
            _v = new Vector3[pc * VertsPer];
            _c = new Color[pc * VertsPer];
            var tri = new int[pc * quads * 6];
            for (int p = 0; p < pc; p++)
                for (int q = 0; q < quads; q++)
                {
                    int v = p * VertsPer + q * 4, t = (p * quads + q) * 6;
                    tri[t] = v; tri[t + 1] = v + 1; tri[t + 2] = v + 2; tri[t + 3] = v; tri[t + 4] = v + 2; tri[t + 5] = v + 3;
                }
            _mesh = new Mesh { name = "ArmorPanelFx" };
            _mesh.MarkDynamic();
            _mesh.vertices = _v;
            _mesh.colors = _c;
            _mesh.triangles = tri;
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = SignatureFx.Additive;   // 양면(Cull Off) · 정점 색
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            // 펼치는 순간 수면에 번지는 고리 하나
            SignatureFx.Ring(root.position, len * 0.75f, 0.6f, new Color(color.r, color.g, color.b, 0.7f), 0.5f);
            Refresh();
        }

        private static void Collect(Transform t, Matrix4x4 inv, List<Bounds> boxes)
        {
            foreach (var mf in t.GetComponentsInChildren<MeshFilter>())
            {
                if (mf == null || mf.sharedMesh == null || mf.name == "Night lamp" || mf.GetComponent<ArmorPanelFx>() != null) continue;
                if (!mf.TryGetComponent<MeshRenderer>(out var r) || !r.enabled) continue;
                var mb = mf.sharedMesh.bounds;
                var m = inv * mf.transform.localToWorldMatrix;
                var b = new Bounds(m.MultiplyPoint3x4(mb.center), Vector3.zero);
                for (int i = 0; i < 8; i++)
                    b.Encapsulate(m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
                if (b.size.y > 40f || b.size.x > 60f) continue;   // 바다 · 물보라 같은 큰 것
                boxes.Add(b);
            }
        }

        /// <summary>맞은 자리(월드)에서 가장 가까운 판을 번쩍이고 이웃 판으로 번지게.</summary>
        public void Hit(Vector3 worldPoint)
        {
            if (_panels.Count == 0) return;
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < _panels.Count; i++)
            {
                float d = ((_panels[i].A + _panels[i].B) * 0.5f - local).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            Hits++;
            Bump(best, 1f);
            // 이웃: 같은 뱃전의 앞뒤 판(목록에서 두 칸씩 떨어져 있다 — 왼현 · 오른현이 번갈아 들어감)
            Bump(best - 2, 0.55f); Bump(best + 2, 0.55f);
            Bump(best - 4, 0.25f); Bump(best + 4, 0.25f);
        }

        private void Bump(int i, float f)
        {
            if (i < 0 || i >= _panels.Count) return;
            var p = _panels[i]; p.Flash = Mathf.Max(p.Flash, f); _panels[i] = p;
        }

        private void LateUpdate()
        {
            if (Time.time - _start >= _duration) { Destroy(gameObject); return; }
            for (int i = 0; i < _panels.Count; i++)
            {
                var p = _panels[i];
                if (p.Flash > 0f) { p.Flash = Mathf.Max(0f, p.Flash - Time.deltaTime / 0.5f); _panels[i] = p; }
            }
            Refresh();
        }

        private static float OutBack(float x)
        {
            x = Mathf.Clamp01(x);
            const float c1 = 1.9f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        private void Refresh()
        {
            float age = Time.time - _start, left = _duration - age;
            float blink = left < BlinkTime && left > LowerTime ? (Mathf.Repeat(age * 8f, 1f) < 0.5f ? 1f : 0.45f) : 1f;
            float band = Mathf.Repeat(age / 1.1f, 1f) * 1.4f - 0.2f;   // 선수 → 선미로 훑는 빛줄
            float maxRise = 0f, minRise = float.MaxValue;
            const float rim = 0.12f, edge = 0.08f;
            for (int i = 0; i < _panels.Count; i++)
            {
                var p = _panels[i];
                float rise = OutBack((age - p.Order * _stagger) / RiseTime);
                if (left < LowerTime)   // 선미부터 내려앉음
                {
                    float lo = Mathf.Clamp01((left - (1f - p.U) * 0.08f) / (LowerTime - 0.08f));
                    rise = Mathf.Min(rise, lo * lo);
                }
                rise = Mathf.Max(0f, rise);
                maxRise = Mathf.Max(maxRise, rise); minRise = Mathf.Min(minRise, rise);
                float scan = Mathf.Max(0f, 1f - Mathf.Abs(p.U - band) / 0.12f);
                float a = blink * Mathf.Clamp01(rise * 1.5f);
                Vector3 up = Vector3.up * (_height * rise) + p.Out * (_tilt * rise);
                Vector3 a0 = p.A, b0 = p.B, a1 = p.A + up, b1 = p.B + up;
                Vector3 upN = up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up;
                Vector3 along = (b0 - a0).normalized;
                float hot = p.Flash;
                Color fillTop = Tint(0.4f + 0.4f * scan + 1.3f * hot, a), fillBot = Tint(0.08f + 0.6f * hot, a);
                Color rimC = Tint(1.1f + 0.3f * scan + 0.9f * hot, a), edgeC = Tint(0.65f + 0.8f * hot + 0.35f * scan, a);
                int v = i * VertsPer;
                // 판 면(아래는 옅고 위로 갈수록 진하게)
                Set(v, a0, b0, b1, a1, fillBot, fillBot, fillTop, fillTop);
                // 위 테두리
                Vector3 r = upN * Mathf.Min(rim, _height * rise);
                Set(v + 4, a1 - r, b1 - r, b1, a1, rimC, rimC, rimC, rimC);
                // 양 옆 이음매
                Vector3 e = along * edge;
                Set(v + 8, a0, a0 + e, a1 + e, a1, edgeC * 0.5f, edgeC * 0.5f, edgeC, edgeC);
                Set(v + 12, b0 - e, b0, b1, b1 - e, edgeC * 0.5f, edgeC * 0.5f, edgeC, edgeC);
                // 닻 문양: 판 가운데, 판 높이 · 폭의 70% 안. 좁은 판(1m 미만)은 비운다
                float panelLen = Vector3.Distance(a0, b0);
                float size = Mathf.Min(panelLen * 0.7f, _height * 0.7f);
                bool emblem = panelLen >= 1f;
                Color embC = Tint(0.85f + 0.35f * scan + 0.9f * hot, emblem ? a : 0f);
                Vector3 mid = (a0 + b0) * 0.5f + p.Out * 0.03f;   // 판보다 살짝 바깥(겹쳐 깜빡이지 않게)
                float riseH = _height * rise, off = (_height - size) * 0.5f, stroke = size * 0.07f;
                for (int k = 0; k < AnchorQuads; k++)
                {
                    Vector2 s0 = AnchorSegs[k * 2], s1 = AnchorSegs[k * 2 + 1];
                    Vector3 w0 = mid + along * (s0.x * size) + Vector3.up * ((off + s0.y * size) / _height * riseH);
                    Vector3 w1 = mid + along * (s1.x * size) + Vector3.up * ((off + s1.y * size) / _height * riseH);
                    Vector3 dir = w1 - w0;
                    Vector3 sideV = dir.sqrMagnitude > 1e-8f ? Vector3.Cross(dir.normalized, p.Out).normalized * (stroke * 0.5f) : Vector3.zero;
                    Vector3 ext = dir.sqrMagnitude > 1e-8f ? dir.normalized * (stroke * 0.4f) : Vector3.zero;   // 이음매가 벌어지지 않게 조금 늘림
                    Set(v + 16 + k * 4, w0 - ext - sideV, w0 - ext + sideV, w1 + ext + sideV, w1 + ext - sideV, embC, embC, embC, embC);
                }
            }
            MaxRise = maxRise; MinRise = _panels.Count > 0 ? minRise : 0f;
            int ec = 0;
            foreach (var pp in _panels) if (Vector3.Distance(pp.A, pp.B) >= 1f) ec++;
            EmblemCount = ec;
            _mesh.vertices = _v;
            _mesh.colors = _c;
            _mesh.RecalculateBounds();
        }

        private Color Tint(float k, float a)
        {
            var c = Color.Lerp(_color, Color.white, Mathf.Clamp01(k - 1f) * 0.7f);   // 맞은 판 · 테두리는 흰빛으로
            c.a = Mathf.Clamp01(_color.a * Mathf.Min(k, 1.8f) * a);
            return c;
        }

        private void Set(int v, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color c0, Color c1, Color c2, Color c3)
        {
            _v[v] = p0; _v[v + 1] = p1; _v[v + 2] = p2; _v[v + 3] = p3;
            _c[v] = c0; _c[v + 1] = c1; _c[v + 2] = c2; _c[v + 3] = c3;
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (Last == this) Last = null;
        }
    }
}

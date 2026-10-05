using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Modules;

namespace Game.Ship
{
    /// <summary>
    /// 점유된 칸을 따라 선체를 만들어낸다.
    ///
    /// 모듈만 둥둥 떠 있으면 군함으로 보이지 않는다.
    /// 칸마다 갑판판을 깔고, 바깥으로 트인 면에는 현측(뱃전)을 세워
    /// 어떤 모양으로 자라든 하나의 배처럼 읽히게 한다.
    ///
    /// 함수(뱃머리)는 칸 단위 조각이 아니라 배 앞쪽 윤곽 전체를 감싸는 하나의 메시로 만든다.
    ///   1. 좌우 칸마다 가장 앞선 블록의 앞면을 이어 "앞 윤곽"을 얻는다.
    ///   2. 맨 앞 구간 중앙에서 앞으로 뾰족한 끝점을 낸다.
    ///   3. 윤곽 + 끝점을 바깥에서 감싸는 볼록 껍질을 구한다. 껍질과 윤곽 사이가 함수 갑판이다.
    ///      (앞줄에 틈이나 계단이 있어도 하나의 뱃머리로 덮인다)
    ///   4. 껍질 가장자리에서 아래로 내려가며 안쪽으로 좁아지고 뒤로 물러나는 옆면을 세운다.
    /// 시각 전용이다 — 격자 칸도, 체력도, 충돌도 없고 사격각도 가리지 않는다.
    /// </summary>
    public class ShipHullBuilder : MonoBehaviour
    {
        [SerializeField] private ShipGrid grid;
        [SerializeField] private Transform hullRoot;

        [Header("Pieces")]
        [SerializeField] private GameObject deckPlatePrefab;
        [SerializeField] private GameObject sideSkirtPrefab;

        [Header("Bow (procedural)")]
        [SerializeField] private Material bowDeckMaterial;
        [SerializeField] private Material bowSideMaterial;
        [Tooltip("뱃머리 끝이 맨 앞 블록에서 튀어나오는 길이(칸)")]
        [SerializeField] private float bowTipLengthCells = 1.2f;
        [Tooltip("함수 갑판 높이. 격자 갑판 높이 기준 오프셋. 갑판판 윗면(0.09)보다 살짝 높여 뱃전 윗면과 겹쳐 깜빡이지 않게 한다")]
        [SerializeField] private float deckTopOffset = 0.095f;
        [Tooltip("갑판 윗면에서 뱃전 아래 끝까지의 깊이. 바다 평면(-0.9) 아래까지 내려가야 밑이 비어 보이지 않는다. 뱃전 조각과 맞춘다")]
        [SerializeField] private float hullDepth = 1.6f;   // 흔들림(±0.12)으로 떠올라도 수면 아래에 남도록
        [Tooltip("아래로 내려갈수록 좌우가 중앙으로 모이는 정도(플레어)")]
        [SerializeField, Range(0f, 1f)] private float keelNarrowing = 0.2f;
        [Tooltip("아래로 내려갈수록 끝이 뒤로 물러나는 정도(선수재 기울기)")]
        [SerializeField, Range(0f, 1f)] private float stemRake = 0.3f;
        [Tooltip("선택: 함수 갑판에 올리는 소품(닻 양묘기·계류주). 없으면 생략")]
        [SerializeField] private GameObject bowFittingsPrefab;

        [Header("Bow Anchors")]
        [Tooltip("좌우 뱃전에 붙는 닻. 원점 = 닻줄 구멍 중심, +Z = 선체 바깥")]
        [SerializeField] private GameObject bowAnchorPrefab;
        [Tooltip("닻 위치: 맨 앞 블록 앞면(0) ~ 끝점(1) 사이의 비율")]
        [SerializeField, Range(0.05f, 0.9f)] private float anchorAlongBow = 0.35f;
        [Tooltip("갑판 윗면에서 닻줄 구멍까지 내려오는 거리")]
        [SerializeField] private float anchorDrop = 0.35f;

        private readonly List<GameObject> _spawned = new();
        private Mesh _bowMesh;

        // 메시 생성용 버퍼. 매번 새로 할당하지 않는다.
        private readonly List<Vector3> _verts = new();
        private readonly List<int> _deckTris = new();
        private readonly List<int> _sideTris = new();

        private void OnEnable()
        {
            GameEvents.ModuleInstalled += OnModuleChanged;
            GameEvents.ModuleRemoved += OnModuleChanged;
        }

        private void OnDisable()
        {
            GameEvents.ModuleInstalled -= OnModuleChanged;
            GameEvents.ModuleRemoved -= OnModuleChanged;
        }

        private void OnDestroy()
        {
            if (_bowMesh != null) Destroy(_bowMesh);
        }

        private void Start() => Rebuild();

        private void OnModuleChanged(ModuleInstance _) => Rebuild();

        /// <summary>선체를 통째로 다시 만든다. 배치가 바뀔 때만 불리므로 비용은 문제되지 않는다.</summary>
        public void Rebuild()
        {
            if (grid == null || hullRoot == null) return;

            foreach (var go in _spawned)
                if (go != null) Destroy(go);

            _spawned.Clear();

            float cell = grid.CellSize;

            foreach (var c in grid.OccupiedCells)
            {
                Vector3 center = grid.CoordToLocal(c);

                Spawn(deckPlatePrefab, center, Quaternion.identity);

                // 이웃이 없는 방향에는 뱃전을 세운다
                foreach (var d in GridCoord.Neighbors)
                {
                    if (!grid.IsFree(c + d)) continue;

                    // 격자 X(선수) -> 로컬 Z, 격자 Z(좌우) -> 로컬 X
                    var offset = new Vector3(d.Z * cell * 0.5f, 0f, d.X * cell * 0.5f);
                    var facing = Quaternion.LookRotation(new Vector3(d.Z, 0f, d.X), Vector3.up);

                    Spawn(sideSkirtPrefab, center + offset, facing);
                }
            }

            BuildBow();
        }

        // ------------------------------------------------------------------ 함수

        private void BuildBow()
        {
            // 1. 앞 윤곽: 좌우 칸(격자 Z)마다 가장 앞선 격자 X
            var front = new Dictionary<int, int>();
            foreach (var c in grid.OccupiedCells)
                front[c.Z] = front.TryGetValue(c.Z, out int f) ? Mathf.Max(f, c.X) : c.X;
            if (front.Count == 0) return;

            float s = grid.CellSize;
            int minZ = int.MaxValue, maxZ = int.MinValue, maxFront = int.MinValue;
            foreach (var kv in front)
            {
                minZ = Mathf.Min(minZ, kv.Key);
                maxZ = Mathf.Max(maxZ, kv.Key);
                maxFront = Mathf.Max(maxFront, kv.Value);
            }

            // 2. 끝점: 맨 앞 구간(가장 앞선 칸들)의 좌우 중앙
            float spanMin = float.MaxValue, spanMax = float.MinValue;
            foreach (var kv in front)
            {
                if (kv.Value != maxFront) continue;
                spanMin = Mathf.Min(spanMin, (kv.Key - 0.5f) * s);
                spanMax = Mathf.Max(spanMax, (kv.Key + 0.5f) * s);
            }
            float frontEdge = (maxFront + 0.5f) * s;
            var tip = new Vector2((spanMin + spanMax) * 0.5f, frontEdge + bowTipLengthCells * s);

            // 3. 볼록 껍질 (u = 좌우 로컬 X, v = 선수 로컬 Z). 같은 u는 가장 앞선 점만 남긴다.
            var byU = new SortedDictionary<float, float>();
            void AddPoint(float u, float v) => byU[u] = byU.TryGetValue(u, out float old) ? Mathf.Max(old, v) : v;

            for (int z = minZ; z <= maxZ; z++)
            {
                if (!front.TryGetValue(z, out int f)) continue;
                float v = (f + 0.5f) * s;
                AddPoint((z - 0.5f) * s, v);
                AddPoint((z + 0.5f) * s, v);
            }
            AddPoint(tip.x, tip.y);

            var hull = new List<Vector2>();
            foreach (var kv in byU)
            {
                var p = new Vector2(kv.Key, kv.Value);
                while (hull.Count >= 2 && Cross(hull[^2], hull[^1], p) >= 0f) hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }

            // 4. 좌우로 잘게 나눈 띠마다 갑판과 옆면을 만든다
            var breaks = new SortedSet<float>();
            foreach (var h in hull) breaks.Add(h.x);
            for (int z = minZ; z <= maxZ + 1; z++) breaks.Add((z - 0.5f) * s);

            float minU = (minZ - 0.5f) * s, maxU = (maxZ + 0.5f) * s;
            float maxExtension = Mathf.Max(0.01f, tip.y - frontEdge + s);
            float yTop = grid.DeckHeight + deckTopOffset;
            float yBottom = yTop - hullDepth;

            _verts.Clear();
            _deckTris.Clear();
            _sideTris.Clear();

            float prevU = float.NaN;
            foreach (float u in breaks)
            {
                if (u < minU || u > maxU) continue;
                if (!float.IsNaN(prevU))
                    AddStrip(prevU, u, hull, front, s, tip.x, maxExtension, yTop, yBottom);
                prevU = u;
            }

            if (_deckTris.Count == 0) return;
            ApplyBowMesh();
            PlaceFittings(hull, frontEdge, tip.x, minU, maxU, yTop);
            PlaceAnchors(hull, front, s, frontEdge, tip, minU, maxU, maxExtension, yTop, yBottom);
        }

        private void AddStrip(float u0, float u1, List<Vector2> hull, Dictionary<int, int> front, float s,
                              float tipU, float maxExtension, float yTop, float yBottom)
        {
            // 띠 안에서는 윤곽 높이가 일정하다(칸 경계로 나눴으므로)
            int z = Mathf.RoundToInt(((u0 + u1) * 0.5f) / s);
            if (!front.TryGetValue(z, out int f)) return;
            float pv = (f + 0.5f) * s;

            float hv0 = HullAt(hull, u0), hv1 = HullAt(hull, u1);
            if (hv0 - pv < 0.001f && hv1 - pv < 0.001f) return;   // 앞으로 튀어나온 부분이 없다

            var deckA = new Vector3(u0, yTop, pv);
            var deckB = new Vector3(u0, yTop, hv0);
            var deckC = new Vector3(u1, yTop, hv1);
            var deckD = new Vector3(u1, yTop, pv);

            AddTri(_deckTris, deckA, deckB, deckC, Vector3.up);
            AddTri(_deckTris, deckA, deckC, deckD, Vector3.up);

            // 옆면: 윗변은 껍질, 아랫변은 안쪽·뒤쪽으로 물러난 선
            Vector3 b0 = KeelPoint(u0, hv0, pv, tipU, maxExtension, yBottom);
            Vector3 b1 = KeelPoint(u1, hv1, pv, tipU, maxExtension, yBottom);

            // 껍질을 왼쪽→오른쪽으로 따라갈 때 바깥쪽 법선은 (-dv, du)
            var outward = new Vector3(-(hv1 - hv0), 0f, u1 - u0);
            AddTri(_sideTris, deckB, deckC, b1, outward);
            AddTri(_sideTris, deckB, b1, b0, outward);

            // 바닥: 옆면 아래 끝과 블록 앞면 사이를 막는다
            var bottomA = new Vector3(u0, yBottom, pv);
            var bottomD = new Vector3(u1, yBottom, pv);
            AddTri(_sideTris, bottomA, b0, b1, Vector3.down);
            AddTri(_sideTris, bottomA, b1, bottomD, Vector3.down);
        }

        /// <summary>
        /// 뱃전 아래 끝 점. 앞으로 많이 튀어나온 곳일수록 더 모이고 더 물러나서,
        /// 끝은 날카로운 선수재가 되고 양 끝은 기존 뱃전과 자연스럽게 이어진다.
        /// </summary>
        private Vector3 KeelPoint(float u, float hv, float pv, float tipU, float maxExtension, float yBottom)
        {
            float extension = Mathf.Max(0f, hv - pv);
            float t = Mathf.Clamp01(extension / maxExtension);

            float bu = Mathf.Lerp(u, tipU, keelNarrowing * t);
            float bv = pv + extension * (1f - stemRake);
            return new Vector3(bu, yBottom, bv);
        }

        private static float HullAt(List<Vector2> hull, float u)
        {
            if (u <= hull[0].x) return hull[0].y;
            for (int i = 1; i < hull.Count; i++)
            {
                if (u > hull[i].x) continue;
                float span = hull[i].x - hull[i - 1].x;
                float k = span > 0.0001f ? (u - hull[i - 1].x) / span : 1f;
                return Mathf.Lerp(hull[i - 1].y, hull[i].y, k);
            }
            return hull[^1].y;
        }

        private static float Cross(Vector2 o, Vector2 a, Vector2 b)
            => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        /// <summary>면마다 정점을 따로 둬 각진 음영을 낸다. 법선이 원하는 쪽을 보도록 감는 순서를 맞춘다.</summary>
        private void AddTri(List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 desiredNormal)
        {
            if (Vector3.Cross(b - a, c - a).sqrMagnitude < 1e-8f) return;
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), desiredNormal) < 0f) (b, c) = (c, b);

            int i = _verts.Count;
            _verts.Add(a);
            _verts.Add(b);
            _verts.Add(c);
            tris.Add(i);
            tris.Add(i + 1);
            tris.Add(i + 2);
        }

        private void ApplyBowMesh()
        {
            if (_bowMesh == null) _bowMesh = new Mesh { name = "ProceduralBow" };

            _bowMesh.Clear();
            _bowMesh.SetVertices(_verts);
            _bowMesh.subMeshCount = 2;
            _bowMesh.SetTriangles(_deckTris, 0);
            _bowMesh.SetTriangles(_sideTris, 1);
            _bowMesh.RecalculateNormals();
            _bowMesh.RecalculateBounds();

            var go = new GameObject("Bow", typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = hullRoot.gameObject.layer;   // 정비 화면 카메라에도 보이게
            go.transform.SetParent(hullRoot, false);
            go.GetComponent<MeshFilter>().sharedMesh = _bowMesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = new[] { bowDeckMaterial, bowSideMaterial };
            _spawned.Add(go);
        }

        /// <summary>
        /// 함수 갑판에 소품을 올린다. 뒤끝을 맨 앞 블록 앞면에 붙이고, 앞쪽 끝이 함수 폭 안에 들어가도록
        /// 필요하면 줄인다. 함수가 좁은 배(앞 1칸)에서도 옆으로 삐져나오지 않게 한다.
        /// </summary>
        private void PlaceFittings(List<Vector2> hull, float frontEdge, float tipU, float minU, float maxU, float yTop)
        {
            if (bowFittingsPrefab == null) return;

            var go = Instantiate(bowFittingsPrefab, hullRoot);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            SetLayerRecursive(go, hullRoot.gameObject.layer);
            _spawned.Add(go);

            if (!TryGetLocalBounds(go, out var b)) { Destroy(go); _spawned.Remove(go); return; }

            // 원점 기준 뒤쪽·앞쪽 길이와 폭(배율 1)
            float back = -(b.min.z - go.transform.localPosition.z);
            float front = b.max.z - go.transform.localPosition.z;
            float width = b.size.x;

            // 클수록 앞끝이 뾰족한 쪽으로 나가 쓸 수 있는 폭이 줄어든다(단조). 들어맞는 가장 큰 배율을 이분 탐색한다.
            bool Fits(float k, out float center)
            {
                center = tipU;
                float frontV = frontEdge + (back + front) * k + 0.05f;
                if (!TryHullWidthAt(hull, frontV, minU, maxU, out float left, out float right)) return false;
                center = (left + right) * 0.5f;
                return (right - left) * 0.9f >= width * k;
            }

            float lo = 0f, hi = 1f;
            if (!Fits(1f, out _))
            {
                for (int i = 0; i < 14; i++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (Fits(mid, out _)) lo = mid; else hi = mid;
                }
                hi = lo;
            }
            float scale = hi;
            Fits(scale, out float centerU);

            if (scale < 0.35f) { Destroy(go); _spawned.Remove(go); return; }   // 너무 작아지면 차라리 생략

            go.transform.localScale = Vector3.one * scale;
            go.transform.localPosition = new Vector3(centerU, yTop, frontEdge + back * scale + 0.05f);
        }

        /// <summary>
        /// 좌우 뱃전에 닻을 붙인다. 함수가 넓은 쪽(맨 앞 블록 가까이)의 껍질 위 점을 골라,
        /// 갑판 모서리에서 조금 내려온 옆면 위에 선체 바깥을 보게 놓는다.
        /// </summary>
        private void PlaceAnchors(List<Vector2> hull, Dictionary<int, int> front, float s, float frontEdge,
                                  Vector2 tip, float minU, float maxU, float maxExtension, float yTop, float yBottom)
        {
            if (bowAnchorPrefab == null) return;

            float v = Mathf.Lerp(frontEdge, tip.y, anchorAlongBow);
            if (!TryHullWidthAt(hull, v, minU, maxU, out float left, out float right)) return;
            if (right - left < 0.6f) return;   // 너무 좁으면 좌우 닻이 겹친다

            PlaceAnchor(hull, front, s, left, tip.x, maxExtension, yTop, yBottom);
            PlaceAnchor(hull, front, s, right, tip.x, maxExtension, yTop, yBottom);
        }

        private void PlaceAnchor(List<Vector2> hull, Dictionary<int, int> front, float s, float u,
                                 float tipU, float maxExtension, float yTop, float yBottom)
        {
            int z = Mathf.RoundToInt(u / s);
            if (!front.TryGetValue(z, out int f)) return;
            float pv = (f + 0.5f) * s;

            float hv = HullAt(hull, u);
            var top = new Vector3(u, yTop, hv);
            var keel = KeelPoint(u, hv, pv, tipU, maxExtension, yBottom);
            var onWall = Vector3.Lerp(top, keel, Mathf.Clamp01(anchorDrop / Mathf.Max(0.01f, hullDepth)));

            // 껍질 기울기로 바깥 방향을 구한다(왼→오 순서일 때 바깥 법선은 (-dv/du, 1))
            float slope = (HullAt(hull, u + 0.05f) - HullAt(hull, u - 0.05f)) / 0.1f;
            var outward = new Vector3(-slope, 0f, 1f).normalized;

            var go = Instantiate(bowAnchorPrefab, hullRoot);
            go.transform.localPosition = onWall + outward * 0.03f;
            go.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up);
            SetLayerRecursive(go, hullRoot.gameObject.layer);
            _spawned.Add(go);
        }

        /// <summary>함수 갑판에서 선수 방향 거리 v 위치의 좌우 범위. 껍질이 v보다 앞선 구간이 폭이다.</summary>
        private static bool TryHullWidthAt(List<Vector2> hull, float v, float minU, float maxU,
                                           out float left, out float right)
        {
            left = float.MaxValue;
            right = float.MinValue;

            const int samples = 160;
            for (int i = 0; i <= samples; i++)
            {
                float u = Mathf.Lerp(minU, maxU, i / (float)samples);
                if (HullAt(hull, u) < v) continue;
                left = Mathf.Min(left, u);
                right = Mathf.Max(right, u);
            }
            return right > left;
        }

        /// <summary>소품 메시들의 경계를 hullRoot 공간으로 정확히 잰다(배가 돌아 있어도 틀어지지 않게).</summary>
        private bool TryGetLocalBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            var toLocal = hullRoot.worldToLocalMatrix;

            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var m = toLocal * mf.transform.localToWorldMatrix;

                for (int c = 0; c < 8; c++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents,
                        new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(corner);
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }
            return any;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
        }

        private void Spawn(GameObject prefab, Vector3 localPos, Quaternion localRot)
        {
            if (prefab == null) return;

            var go = Instantiate(prefab, hullRoot);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;

            _spawned.Add(go);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.World;

namespace Game.View
{
    /// <summary>
    /// 바다 생물(장식, 판정 없음 — 콜라이더·표적 등록 없음). 전투 씬이 열리면 스스로 붙는다.
    ///   - 갈매기: 섬마다(70%) 3~6마리가 섬 위를 돌고, 3~4마리가 함선 뒤를 따라다닌다. 날갯짓과 활공을 번갈아 하고 선회할 때 기운다.
    ///   - 돌고래: 함선이 6 m/s(≈12노트) 이상으로 달리면 가끔(18~35초마다) 3~5마리가 뱃머리 옆에서 함께 달리며 뛰어오른다. 물에 들고 날 때 물보라.
    ///   - 고래: 가끔(35~70초마다) 화면 가장자리 멀리서 등을 드러내고 물을 뿜은 뒤, 꼬리를 들어 올리며 잠수한다.
    /// 섬 위나 섬 안에는 나타나지 않는다.
    /// </summary>
    public class SeaLife : MonoBehaviour
    {
        private const float WaterY = -0.9f;
        private const float IslandFlockRange = 170f;

        public static SeaLife Instance { get; private set; }

        /// <summary>검증용</summary>
        public int GullCount { get; private set; }
        public int FollowerCount => _followers.Gulls.Count;
        public int IslandFlockCount => _islandFlocks.Count;
        public int DolphinCount => _dolphins.Count;
        public float DolphinMaxLeap { get; private set; }
        public int WhaleSpouts { get; private set; }
        public bool WhaleActive => _whale != null;
        public Transform WhaleTransform => _whale;
        public float WhaleFlukeMaxY { get; private set; } = float.MinValue;

        private Mesh _gullBody, _gullWing, _dolphinBody, _dolphinFin, _dolphinFluke, _whaleBody, _whaleFluke, _whaleHump;
        private Material _gullMat, _wingMat, _dolphinMat, _whaleMat;

        // ------------------------------------------------------------ 갈매기

        private sealed class Gull
        {
            public Transform Root, WingL, WingR;
            public float Angle, Radius, Height, AngularSpeed, Phase, FlapTimer;
            public bool Flapping;
            public Vector3 Prev;
        }

        private sealed class Flock
        {
            public IslandInfo Island;   // null이면 함선을 따라다님
            public readonly List<Gull> Gulls = new();
        }

        private readonly Flock _followers = new();
        private readonly Dictionary<IslandInfo, Flock> _islandFlocks = new();
        private readonly List<IslandInfo> _gone = new();
        private float _flockScan;

        // ------------------------------------------------------------ 돌고래

        private sealed class Dolphin
        {
            public Transform Root;
            public Vector3 Offset;            // 함선 기준(로컬) 위치
            public float Period, Phase, Height;
            public bool WasAirborne;
        }

        private readonly List<Dolphin> _dolphins = new();
        private float _podTimer, _podLeft;

        // ------------------------------------------------------------ 고래

        private Transform _whale, _whaleFlukePivot;
        private float _whaleAge, _whaleSpoutDebt, _whaleTimer;
        private Vector3 _whaleStart, _whaleDir;
        private bool _whaleSplashed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<SeaLife>() != null) return;
            if (GameObject.Find("Ocean") == null) return;   // 전투 씬에서만
            new GameObject("Sea life").AddComponent<SeaLife>();
        }

        private void Awake()
        {
            Instance = this;
            _gullMat = Resources.Load<Material>("SeaLife/MAT_Gull");
            _wingMat = Resources.Load<Material>("SeaLife/MAT_GullWing");
            _dolphinMat = Resources.Load<Material>("SeaLife/MAT_Dolphin");
            _whaleMat = Resources.Load<Material>("SeaLife/MAT_Whale");
            BuildMeshes();
            _podTimer = Random.Range(12f, 25f);
            _whaleTimer = Random.Range(25f, 50f);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var m in new[] { _gullBody, _gullWing, _dolphinBody, _dolphinFin, _dolphinFluke, _whaleBody, _whaleFluke, _whaleHump })
                if (m != null) Destroy(m);
        }

        private void Update()
        {
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            UpdateFlocks(player.transform, dt);
            UpdateDolphins(player, dt);
            UpdateWhale(player.transform, dt);
        }

        // ============================================================ 갈매기

        private void UpdateFlocks(Transform player, float dt)
        {
            if (_followers.Gulls.Count == 0)
                for (int i = 0; i < 4; i++) _followers.Gulls.Add(NewGull(Random.Range(5f, 9f), Random.Range(8f, 12f)));

            // 섬 무리: 가까운 섬에 만들고, 멀어지거나 사라진 섬은 치운다
            _flockScan -= dt;
            if (_flockScan <= 0f)
            {
                _flockScan = 0.5f;
                foreach (var island in Islands.All)
                {
                    if (_islandFlocks.ContainsKey(island) || island.Root == null) continue;
                    if (Flat(island.Center - player.position) > IslandFlockRange) continue;
                    if (Hash01(island.Center) > 0.7f) { _islandFlocks[island] = new Flock { Island = island }; continue; }   // 새가 없는 섬(빈 무리로 기억)
                    var flock = new Flock { Island = island };
                    int n = 3 + Mathf.FloorToInt(Hash01(island.Center + Vector3.one) * 4f);
                    for (int i = 0; i < n; i++)
                        flock.Gulls.Add(NewGull(island.Radius * 0.6f + Random.Range(4f, 12f), island.Height + Random.Range(4f, 9f)));
                    _islandFlocks[island] = flock;
                }
                _gone.Clear();
                foreach (var kv in _islandFlocks)
                    if (kv.Key.Root == null || Flat(kv.Key.Center - player.position) > IslandFlockRange + 40f) _gone.Add(kv.Key);
                foreach (var key in _gone)
                {
                    foreach (var g in _islandFlocks[key].Gulls) if (g.Root != null) Destroy(g.Root.gameObject);
                    _islandFlocks.Remove(key);
                }
            }

            // 따라오는 무리의 중심: 함선 뒤쪽 위
            Vector3 back = player.position - Flat3(player.forward) * 7f;
            foreach (var g in _followers.Gulls) FlyGull(g, back, dt);
            int count = _followers.Gulls.Count;
            foreach (var flock in _islandFlocks.Values)
            {
                foreach (var g in flock.Gulls) FlyGull(g, flock.Island.Center, dt);
                count += flock.Gulls.Count;
            }
            GullCount = count;
        }

        private Gull NewGull(float radius, float height)
        {
            var root = new GameObject("Gull").transform;
            root.SetParent(transform, false);
            root.localScale = Vector3.one * 1.35f;   // 높은 카메라에서도 보이게 조금 크게
            Piece(root, "Body", _gullBody, _gullMat, Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
            var wl = Piece(root, "Wing L", _gullWing, _wingMat, new Vector3(-0.06f, 0.03f, 0.05f), Quaternion.identity);
            wl.localScale = new Vector3(-1f, 1f, 1f);
            var wr = Piece(root, "Wing R", _gullWing, _wingMat, new Vector3(0.06f, 0.03f, 0.05f), Quaternion.identity);
            var g = new Gull
            {
                Root = root, WingL = wl, WingR = wr,
                Angle = Random.Range(0f, Mathf.PI * 2f), Radius = radius, Height = height,
                AngularSpeed = Random.Range(6f, 8.5f) / radius * (Random.value < 0.8f ? 1f : -1f),
                Phase = Random.Range(0f, 10f), FlapTimer = Random.Range(0f, 2f),
            };
            return g;
        }

        private static void FlyGull(Gull g, Vector3 center, float dt)
        {
            if (g.Root == null) return;
            g.Angle += g.AngularSpeed * dt;
            float wobble = Mathf.Sin(Time.time * 0.7f + g.Phase) * 2f;
            Vector3 p = new(center.x + Mathf.Cos(g.Angle) * (g.Radius + wobble),
                            g.Height + Mathf.Sin(Time.time * 0.9f + g.Phase) * 0.8f,
                            center.z + Mathf.Sin(g.Angle) * (g.Radius + wobble));
            Vector3 v = p - g.Prev;
            g.Prev = p;
            g.Root.position = p;
            if (v.sqrMagnitude > 1e-5f)
            {
                float bank = -Mathf.Sign(g.AngularSpeed) * 22f;
                g.Root.rotation = Quaternion.LookRotation(v.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, bank);
            }

            // 날갯짓(1.2초) ↔ 활공(1.5초)
            g.FlapTimer -= dt;
            if (g.FlapTimer <= 0f) { g.Flapping = !g.Flapping; g.FlapTimer = g.Flapping ? Random.Range(0.8f, 1.4f) : Random.Range(1.2f, 2.2f); }
            float wing = g.Flapping ? Mathf.Sin((Time.time + g.Phase) * Mathf.PI * 2f * 3.5f) * 32f + 6f : 8f;
            g.WingR.localRotation = Quaternion.Euler(0f, 0f, wing);
            g.WingL.localRotation = Quaternion.Euler(0f, 0f, -wing);
        }

        // ============================================================ 돌고래

        /// <summary>검증용: 지금 함선 옆에 돌고래 무리를 부른다.</summary>
        public void ForceDolphins() => _podTimer = 0f;

        private void UpdateDolphins(Game.Ship.ShipController ship, float dt)
        {
            float speed = ship.CurrentSpeed;
            if (_dolphins.Count == 0)
            {
                _podTimer -= dt;
                if (_podTimer > 0f || speed < 6f) return;
                _podTimer = Random.Range(18f, 35f);
                SpawnPod(ship.transform);
                return;
            }

            _podLeft -= dt;
            bool ending = _podLeft <= 0f || speed < 3f;
            var t = ship.transform;
            Vector3 fwd = Flat3(t.forward), right = Vector3.Cross(Vector3.up, fwd);
            bool anyVisible = false;
            foreach (var d in _dolphins)
            {
                d.Offset.z += 0.35f * dt;   // 함선보다 조금 빨리
                Vector3 basePos = t.position + right * d.Offset.x + fwd * d.Offset.z;

                // 뜀: 주기 중 45%는 공중(포물선), 나머지는 물속(보이지 않음)
                float cycle = Mathf.Repeat((Time.time + d.Phase) / d.Period, 1f);
                bool air = cycle < 0.45f && !ending && Islands.IsClear(basePos, 2f);
                float y, pitch;
                if (air)
                {
                    float p = cycle / 0.45f;
                    y = WaterY - 0.35f + 4f * d.Height * p * (1f - p);
                    float slope = 4f * d.Height * (1f - 2f * p) / (0.45f * d.Period * Mathf.Max(3f, speed));
                    pitch = -Mathf.Atan(slope) * Mathf.Rad2Deg;
                    DolphinMaxLeap = Mathf.Max(DolphinMaxLeap, y - WaterY);
                    anyVisible = true;
                }
                else { y = WaterY - 2.5f; pitch = 0f; }

                if (air != d.WasAirborne) Splash(new Vector3(basePos.x, WaterY + 0.1f, basePos.z), 0.7f);
                d.WasAirborne = air;
                d.Root.SetPositionAndRotation(new Vector3(basePos.x, y, basePos.z), Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(pitch, 0f, 0f));
            }

            if (ending && !anyVisible)
            {
                foreach (var d in _dolphins) if (d.Root != null) Destroy(d.Root.gameObject);
                _dolphins.Clear();
            }
        }

        private void SpawnPod(Transform ship)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            int n = Random.Range(3, 6);
            var box = ship.GetComponent<BoxCollider>();
            float halfBeam = box != null ? box.size.x * 0.5f : 2f;
            float halfLen = box != null ? box.size.z * 0.5f : 5f;
            for (int i = 0; i < n; i++)
            {
                var root = new GameObject("Dolphin").transform;
                root.SetParent(transform, false);
                Piece(root, "Body", _dolphinBody, _dolphinMat, Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
                Piece(root, "Fin", _dolphinFin, _dolphinMat, new Vector3(0f, 0.22f, -0.1f), Quaternion.identity);
                Piece(root, "Fluke", _dolphinFluke, _dolphinMat, new Vector3(0f, 0f, -1.2f), Quaternion.identity);
                root.position = new Vector3(0f, -10f, 0f);
                _dolphins.Add(new Dolphin
                {
                    Root = root,
                    Offset = new Vector3(side * (halfBeam + Random.Range(3f, 7f)), 0f, halfLen * Random.Range(0.2f, 1.2f) + Random.Range(-2f, 4f)),
                    Period = Random.Range(1.3f, 1.8f),
                    Phase = Random.Range(0f, 2f),
                    Height = Random.Range(0.9f, 1.4f),
                });
            }
            _podLeft = Random.Range(7f, 11f);
        }

        // ============================================================ 고래

        /// <summary>검증용: 지금 화면 가장자리에 고래를 부른다.</summary>
        public void ForceWhale() => _whaleTimer = 0f;

        private void UpdateWhale(Transform player, float dt)
        {
            if (_whale == null)
            {
                _whaleTimer -= dt;
                if (_whaleTimer > 0f) return;
                _whaleTimer = Random.Range(35f, 70f);
                // 화면 안(카메라는 북쪽을 본다) 옆쪽 멀리, 섬이 없는 곳
                for (int i = 0; i < 6; i++)
                {
                    float side = Random.value < 0.5f ? -1f : 1f;
                    Vector3 p = player.position + new Vector3(side * Random.Range(26f, 42f), 0f, Random.Range(8f, 32f));
                    if (!Islands.IsClear(p, 12f)) continue;
                    SpawnWhale(p, new Vector3(-side * Random.Range(0.2f, 0.6f), 0f, 1f).normalized);
                    break;
                }
                return;
            }

            _whaleAge += dt;
            float t = _whaleAge;
            // 0~1.6초 떠오름, 1.4~2.2초 물 뿜기, ~5초 헤엄, 5~8초 잠수(머리가 내려가고 꼬리가 들림)
            float rise = Mathf.SmoothStep(0f, 1f, t / 1.6f);
            float dive = Mathf.SmoothStep(0f, 1f, (t - 5f) / 3f);
            float y = Mathf.Lerp(WaterY - 4f, WaterY - 1.25f, rise) - 5f * dive * dive;   // 등만 낮게 드러난다(잠수함처럼 보이지 않게)
            float pitch = 38f * Mathf.SmoothStep(0f, 1f, (t - 4.8f) / 1.6f);
            Vector3 pos = _whaleStart + _whaleDir * (2f * t) + Vector3.up * (y + Mathf.Sin(t * 1.3f) * 0.08f);
            _whale.SetPositionAndRotation(pos, Quaternion.LookRotation(_whaleDir, Vector3.up) * Quaternion.Euler(pitch, 0f, 0f));
            // 꼬리를 위로 젖힌다
            if (_whaleFlukePivot != null) _whaleFlukePivot.localRotation = Quaternion.Euler(35f * Mathf.SmoothStep(0f, 1f, (t - 5.2f) / 1.2f), 0f, 0f);
            if (_whaleFlukePivot != null) WhaleFlukeMaxY = Mathf.Max(WhaleFlukeMaxY, _whaleFlukePivot.position.y);

            // 물 뿜기(분기공 = 머리 쪽 위)
            if (t > 1.4f && t < 2.2f)
            {
                _whaleSpoutDebt += 45f * dt;
                Vector3 blow = _whale.position + _whale.forward * 3.2f + Vector3.up * 0.9f;
                blow.y = Mathf.Max(blow.y, WaterY + 0.3f);
                while (_whaleSpoutDebt >= 1f)
                {
                    _whaleSpoutDebt -= 1f;
                    Vector2 r = Random.insideUnitCircle * 1.1f;   // 위로 갈수록 퍼지는 물안개
                    DecorFx.Emit(DecorFx.Spray, blow, new Vector3(r.x, Random.Range(7f, 10f), r.y) + DecorFx.Wind * 0.4f,
                        Random.Range(1.1f, 1.7f), Random.Range(1.2f, 2.2f), new Color(0.95f, 0.97f, 1f, Random.Range(0.45f, 0.75f)));
                    WhaleSpouts++;
                }
            }
            if (!_whaleSplashed && t > 7.2f)
            {
                _whaleSplashed = true;
                Splash(new Vector3(_whaleFlukePivot.position.x, WaterY + 0.1f, _whaleFlukePivot.position.z), 1.6f);
            }
            if (t > 9f)
            {
                Destroy(_whale.gameObject);
                _whale = null;
            }
        }

        private void SpawnWhale(Vector3 at, Vector3 dir)
        {
            _whale = new GameObject("Whale").transform;
            _whale.SetParent(transform, false);
            Piece(_whale, "Body", _whaleBody, _whaleMat, Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
            Piece(_whale, "Hump", _whaleHump, _whaleMat, new Vector3(0f, 1.35f, -2.6f), Quaternion.identity);
            _whaleFlukePivot = new GameObject("Fluke pivot").transform;
            _whaleFlukePivot.SetParent(_whale, false);
            _whaleFlukePivot.localPosition = new Vector3(0f, 0f, -6.4f);
            Piece(_whaleFlukePivot, "Fluke", _whaleFluke, _whaleMat, Vector3.zero, Quaternion.identity);
            _whaleStart = new Vector3(at.x, 0f, at.z);
            _whaleDir = dir;
            _whaleAge = 0f;
            _whaleSpoutDebt = 0f;
            _whaleSplashed = false;
            _whale.position = new Vector3(at.x, -10f, at.z);
        }

        // ============================================================ 도구

        private static void Splash(Vector3 at, float size)
        {
            int n = Mathf.RoundToInt(5 * size) + 3;
            for (int i = 0; i < n; i++)
            {
                Vector2 r = Random.insideUnitCircle;
                DecorFx.Emit(DecorFx.Spray, at + new Vector3(r.x, 0f, r.y) * 0.4f * size,
                    new Vector3(r.x * 1.5f, Random.Range(1.8f, 3.2f) * Mathf.Sqrt(size), r.y * 1.5f),
                    Random.Range(0.4f, 0.7f), Random.Range(0.4f, 0.8f) * size, new Color(0.94f, 0.97f, 1f, 0.7f));
            }
        }

        private static Transform Piece(Transform parent, string name, Mesh mesh, Material mat, Vector3 local, Quaternion rot)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = rot;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = false;
            return go.transform;
        }

        private void BuildMeshes()
        {
            var rng = new System.Random(20260927);
            // 몸통은 y축 회전체(앞 = +y)로 만들고 Piece에서 눕힌다
            _gullBody = IslandBuilder.Lathe(new[] { (-0.38f, 0.15f), (-0.12f, 0.95f), (0.14f, 0.85f), (0.28f, 0.45f), (0.36f, 0f) }, 0.13f, 6, 0f, rng, flatTop: false);
            _gullWing = FlatShape(new[] { new Vector3(0f, 0f, 0.14f), new Vector3(0.45f, 0.02f, 0.1f), new Vector3(0.9f, 0f, -0.12f), new Vector3(0.45f, 0f, -0.16f), new Vector3(0f, 0f, -0.14f) });
            _dolphinBody = IslandBuilder.Lathe(new[] { (-1.25f, 0.12f), (-0.95f, 0.4f), (-0.3f, 0.95f), (0.35f, 1f), (0.85f, 0.6f), (1.1f, 0.3f), (1.3f, 0f) }, 0.32f, 8, 0f, rng, flatTop: false);
            _dolphinFin = FlatShape(new[] { new Vector3(0f, 0f, 0.3f), new Vector3(0f, 0.38f, -0.2f), new Vector3(0f, 0f, -0.25f) });
            _dolphinFluke = FlatShape(new[] { new Vector3(0f, 0f, 0.1f), new Vector3(0.45f, 0f, -0.2f), new Vector3(0f, 0f, -0.05f), new Vector3(-0.45f, 0f, -0.2f) });
            _whaleBody = IslandBuilder.Lathe(new[] { (-6.6f, 0.08f), (-5.2f, 0.35f), (-2.5f, 0.85f), (1f, 1f), (4f, 0.9f), (6f, 0.55f), (6.8f, 0f) }, 1.5f, 10, 0f, rng, flatTop: false);
            _whaleFluke = FlatShape(new[] { new Vector3(0f, 0f, 0.3f), new Vector3(2.5f, 0f, -1.1f), new Vector3(0.35f, 0f, -0.45f), new Vector3(0f, 0f, -0.7f), new Vector3(-0.35f, 0f, -0.45f), new Vector3(-2.5f, 0f, -1.1f) });
            _whaleHump = FlatShape(new[] { new Vector3(0f, 0f, 0.6f), new Vector3(0f, 0.45f, -0.3f), new Vector3(0f, 0f, -0.7f) });
        }

        /// <summary>한 장짜리 평면 모양(부채꼴로 삼각형), 앞뒷면 모두 보이게.</summary>
        private static Mesh FlatShape(Vector3[] outline)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            Vector3 c = Vector3.zero;
            foreach (var p in outline) c += p;
            c /= outline.Length;
            for (int i = 0; i < outline.Length; i++)
            {
                Vector3 a = outline[i], b = outline[(i + 1) % outline.Length];
                int k = verts.Count;
                verts.Add(c); verts.Add(a); verts.Add(b);
                tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
                k = verts.Count;
                verts.Add(c); verts.Add(b); verts.Add(a);
                tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
            }
            var mesh = new Mesh { name = "Sea life part" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
        private static Vector3 Flat3(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }

        private static float Hash01(Vector3 p)
        {
            unchecked
            {
                uint h = (uint)(Mathf.RoundToInt(p.x * 10f) * 73856093 ^ Mathf.RoundToInt(p.z * 10f) * 19349663);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h & 0xFFFF) / 65535f;
            }
        }
    }
}

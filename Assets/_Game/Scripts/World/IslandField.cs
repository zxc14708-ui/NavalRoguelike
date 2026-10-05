using System.Collections.Generic;
using UnityEngine;
using Game.Core;

namespace Game.World
{
    /// <summary>
    /// 바다 곳곳의 섬. 바다는 끝이 없으므로 함선 주변 구역(칸 110m)마다 섬을 0~2개씩 정해 두고,
    /// 함선이 움직이면 앞쪽 구역을 새로 채우고 멀어진 구역은 치운다.
    /// 구역의 섬은 (판 시드, 구역 좌표)로 정해져 같은 판에서 되돌아가면 같은 자리에 같은 섬이 있다.
    /// 판마다 시드가 달라 배치는 매번 바뀐다. 출항 지점 둘레(clearRadius)는 비워 둔다.
    /// 씬을 다시 만들지 않아도 되게 씬이 열린 뒤 스스로 붙는다.
    /// </summary>
    public class IslandField : MonoBehaviour
    {
        public const float ChunkSize = 110f;
        private const int KeepRadius = 2;          // 5×5 구역
        private const int DropRadius = 3;
        private const float ClearRadius = 45f;      // 출항 지점 둘레는 비움
        private const float MinSpacing = 34f;       // 섬끼리 간격(수로)
        private const float CoastReach = 10f;       // 해안 소품이 해안선 밖으로 나가는 거리

        /// <summary>검증·재현용: 0이 아니면 이 시드로 배치한다(다음 판 시작 전에 설정).</summary>
        public static int SeedOverride;
        /// <summary>검증용: 섬을 만들지 않는다.</summary>
        public static bool Disabled;

        private readonly Dictionary<Vector2Int, List<IslandInfo>> _chunks = new();
        private readonly List<Vector2Int> _drop = new();
        private int _seed;
        private Vector3 _origin;
        private bool _originSet;
        private float _timer;

        public static IslandField Instance { get; private set; }
        public int Seed => _seed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<IslandField>() != null) return;
            if (GameObject.Find("Ocean") == null) return;   // 전투 씬에서만
            new GameObject("Islands").AddComponent<IslandField>();
        }

        private void Awake()
        {
            Instance = this;
            _seed = SeedOverride != 0 ? SeedOverride : Random.Range(1, int.MaxValue);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>검증용: 모든 섬을 지우고 새 시드로 다시 채운다(출항 지점 = 지금 함선 위치).</summary>
        public void Regenerate(int seed, bool keepClearAroundPlayer = true)
        {
            foreach (var list in _chunks.Values) foreach (var i in list) Remove(i);
            _chunks.Clear();
            _seed = seed != 0 ? seed : Random.Range(1, int.MaxValue);
            _originSet = false;
            if (!keepClearAroundPlayer) _origin = new Vector3(1e6f, 0f, 1e6f);
            _timer = 0f;
            Refresh(force: true);
        }

        /// <summary>검증용: 지정한 자리에 섬 하나를 놓는다.</summary>
        public IslandInfo PlaceAt(Vector3 center, float radius, bool rocky, int seed = 1)
        {
            var key = new Vector2Int(int.MinValue, int.MinValue);
            if (!_chunks.TryGetValue(key, out var list)) _chunks[key] = list = new List<IslandInfo>();
            var info = IslandBuilder.Build(center, radius, rocky, new System.Random(seed), transform);
            list.Add(info);
            Physics.SyncTransforms();
            return info;
        }

        /// <summary>검증용: 모든 섬을 지운다(자동 생성도 멈춘다).</summary>
        public void ClearAll()
        {
            foreach (var list in _chunks.Values) foreach (var i in list) Remove(i);
            _chunks.Clear();
        }

        private void LateUpdate()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = 0.5f;
            Refresh(force: false);
        }

        private void Refresh(bool force)
        {
            if (Disabled) return;
            var focus = Focus(out bool hasPlayer);
            if (!hasPlayer && !force) return;
            if (!_originSet) { _origin = focus; _originSet = true; }

            var c = new Vector2Int(Mathf.FloorToInt(focus.x / ChunkSize), Mathf.FloorToInt(focus.z / ChunkSize));
            bool added = false;
            for (int dx = -KeepRadius; dx <= KeepRadius; dx++)
                for (int dz = -KeepRadius; dz <= KeepRadius; dz++)
                {
                    var key = new Vector2Int(c.x + dx, c.y + dz);
                    if (_chunks.ContainsKey(key)) continue;
                    _chunks[key] = Generate(key);
                    added = true;
                }

            _drop.Clear();
            foreach (var key in _chunks.Keys)
            {
                if (key.x == int.MinValue) continue;   // 검증용 수동 배치
                if (Mathf.Abs(key.x - c.x) > DropRadius || Mathf.Abs(key.y - c.y) > DropRadius) _drop.Add(key);
            }
            foreach (var key in _drop)
            {
                foreach (var i in _chunks[key]) Remove(i);
                _chunks.Remove(key);
            }
            if (added) Physics.SyncTransforms();
        }

        private static Vector3 Focus(out bool hasPlayer)
        {
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            hasPlayer = player != null;
            if (player != null) return player.transform.position;
            var cam = Camera.main;
            return cam != null ? cam.transform.position : Vector3.zero;
        }

        private List<IslandInfo> Generate(Vector2Int key)
        {
            var list = new List<IslandInfo>(2);
            int hash = unchecked(_seed * 73856093 ^ key.x * 19349663 ^ key.y * 83492791);
            var rng = new System.Random(hash);

            double roll = rng.NextDouble();
            int count = roll < 0.3 ? 0 : roll < 0.78 ? 1 : 2;
            for (int n = 0; n < count; n++)
            {
                bool rocky = rng.NextDouble() < 0.55;
                float radius = rocky ? 4f + (float)rng.NextDouble() * 6f : 8f + (float)rng.NextDouble() * 8f;
                // Codex 섬(모델)이면 그 해안 반경으로 자리를 잡는다
                var art = IslandBuilder.PickArt(rocky, rng, out float artScale);
                if (art != null) radius = art.shoreRadius * artScale;
                float margin = radius + 8f;
                Vector3 center = Vector3.zero;
                bool ok = false;
                for (int tries = 0; tries < 6 && !ok; tries++)
                {
                    center = new Vector3((key.x + Mathf.Lerp(0f, 1f, (float)rng.NextDouble())) * ChunkSize, 0f,
                                         (key.y + Mathf.Lerp(0f, 1f, (float)rng.NextDouble())) * ChunkSize);
                    center.x = Mathf.Clamp(center.x, key.x * ChunkSize + margin, (key.x + 1) * ChunkSize - margin);
                    center.z = Mathf.Clamp(center.z, key.y * ChunkSize + margin, (key.y + 1) * ChunkSize - margin);
                    // 해안 소품(앞바다 최대 약 10m)까지 비워 둔다
                    ok = FarFromOthers(center, radius) && Horizontal(center - _origin) > ClearRadius + radius + CoastReach && !NearShips(center, radius + 12f + CoastReach);
                }
                if (!ok) continue;
                var islandRng = new System.Random(rng.Next());
                var info = art != null ? IslandBuilder.BuildArt(center, art, artScale, islandRng, transform)
                                       : IslandBuilder.Build(center, radius, rocky, islandRng, transform);
                IslandBuilder.DressCoast(info, new System.Random(rng.Next()));
                list.Add(info);
            }
            return list;
        }

        /// <summary>검증용: Codex 섬 모델 하나를 지정한 자리에 놓는다(해안 소품 포함 여부 선택).</summary>
        public IslandInfo PlaceArt(Vector3 center, string artName, int seed = 1, bool dress = false)
        {
            var art = IslandBuilder.FindArt(artName);
            if (art == null) return null;
            var key = new Vector2Int(int.MinValue, int.MinValue);
            if (!_chunks.TryGetValue(key, out var list)) _chunks[key] = list = new List<IslandInfo>();
            var rng = new System.Random(seed);
            var info = IslandBuilder.BuildArt(center, art, 1f, rng, transform);
            if (dress) IslandBuilder.DressCoast(info, rng);
            list.Add(info);
            Physics.SyncTransforms();
            return info;
        }

        private bool FarFromOthers(Vector3 center, float radius)
        {
            foreach (var other in Islands.All)
                if (Horizontal(other.Center - center) < other.Radius + radius + MinSpacing) return false;
            return true;
        }

        /// <summary>새 구역이 함선이나 적 위에 생기지 않게.</summary>
        private static bool NearShips(Vector3 center, float radius)
        {
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player != null && Horizontal(player.transform.position - center) < radius + 20f) return true;
            foreach (var kind in new[] { Game.Combat.TargetKind.Surface, Game.Combat.TargetKind.Submarine })
                foreach (var t in Game.Combat.TargetRegistry.Get(kind))
                    if (t != null && t.Transform != null && Horizontal(t.Transform.position - center) < radius) return true;
            return false;
        }

        private static float Horizontal(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        private static void Remove(IslandInfo info)
        {
            Islands.Unregister(info);
            if (info.Root == null) return;
            // 런타임에 만든 메시만 지운다(Codex 모델 메시는 자산이라 남긴다)
            foreach (var mf in info.Root.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null && mf.GetComponentInParent<EnvironmentArt>() == null) Destroy(mf.sharedMesh);
            Destroy(info.Root);
        }
    }
}

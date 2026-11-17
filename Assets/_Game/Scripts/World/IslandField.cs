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
    /// 해역(SeaRegion, 2026-10-11 점검 K04)이 바뀌면 새 시드로 다시 채운다: 섬 수·간격·바위섬 비율이 해역마다 다르고,
    /// 좁은 수로에서는 물길 안 작은 바위섬만 두고 양안 기슭(IslandField.Banks)을 물길을 따라 이어 붙인다.
    /// 섬 밀도(2026-10-31): 판 시드로 정한 큰 노이즈 무늬(FastNoiseLite, 약 400m)를 따라 구역의 섬 수를 늘리고 줄여
    /// 섬이 몰린 군도와 트인 바다가 번갈아 나온다(좁은 수로는 그대로).
    /// </summary>
    public partial class IslandField : MonoBehaviour
    {
        public const float ChunkSize = 110f;
        private const int KeepRadius = 2;          // 5×5 구역
        private const int DropRadius = 3;
        private const float ClearRadius = 45f;      // 출항 지점 둘레는 비움
        private const float MinSpacing = 34f;       // 섬끼리 간격(수로)
        private const float CoastReach = 10f;       // 해안 소품이 해안선 밖으로 나가는 거리
        private const float DensityScale = 400f;    // 섬 밀도 무늬 크기(m)
        private const float DensityShift = 0.28f;   // 밀도가 구역의 섬 수 굴림을 얼마나 미는가
        /// <summary>군도에서 구역마다 적 기지 섬이 나올 확률(2026-11-15). 규모 소 50% · 중 35% · 대 15%.</summary>
        public const double BaseChance = 0.1;

        /// <summary>검증·재현용: 0이 아니면 이 시드로 배치한다(다음 판 시작 전에 설정).</summary>
        public static int SeedOverride;
        /// <summary>검증용: 섬을 만들지 않는다.</summary>
        public static bool Disabled;

        private readonly Dictionary<Vector2Int, List<IslandInfo>> _chunks = new();
        private readonly List<Vector2Int> _drop = new();
        private int _seed;
        private FastNoiseLite _density;
        private int _densitySeed;
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
            SeaRegion.Changed += OnSeaChanged;
            SeaRegion.HarborChanged += OnHarborChanged;
        }

        private void OnDestroy()
        {
            SeaRegion.Changed -= OnSeaChanged;
            SeaRegion.HarborChanged -= OnHarborChanged;
            if (Instance == this) Instance = null;
        }

        /// <summary>해역이 바뀌었다(스테이지 시작): 기슭을 치우고 새 시드로 다시 채운다(함선 둘레는 비운다).</summary>
        private void OnSeaChanged(SeaKind kind)
        {
            ClearBanks();
            if (Disabled) return;
            Regenerate(SeedOverride != 0 ? SeedOverride + (int)kind * 7919 : Random.Range(1, int.MaxValue));
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

        /// <summary>검증용: 지정한 자리에 Codex 키트 섬(적 기지 · 자연 섬) 하나를 놓는다.</summary>
        public IslandInfo PlaceKit(Vector3 center, IslandBuilder.Kit kind, int seed = 1, float scale = 1f)
        {
            var key = new Vector2Int(int.MinValue, int.MinValue);
            if (!_chunks.TryGetValue(key, out var list)) _chunks[key] = list = new List<IslandInfo>();
            var info = IslandBuilder.BuildKit(center, kind, new System.Random(seed), transform, scale);
            if (info != null) list.Add(info);
            Physics.SyncTransforms();
            return info;
        }

        /// <summary>검증용: 지정한 자리에 절차형 섬을 놓고 자연 장식까지(DressNature).</summary>
        public IslandInfo PlaceDressed(Vector3 center, float radius, bool rocky, int seed = 1)
        {
            var info = PlaceAt(center, radius, rocky, seed);
            IslandBuilder.DressNature(info);
            return info;
        }

        /// <summary>검증용: 모든 섬을 지운다(자동 생성도 멈춘다).</summary>
        public void ClearAll()
        {
            foreach (var list in _chunks.Values) foreach (var i in list) Remove(i);
            _chunks.Clear();
            ClearBanks();
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
            RefreshBanks(focus);

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

            // 해역별 섬 수(점검 K04). 해역이 없으면(훈련·검사) 예전 그대로 0~2개
            var sea = SeaRegion.Active ? SeaRegion.CurrentProfile : null;
            bool strait = SeaRegion.IsStrait;
            double roll = rng.NextDouble();
            if (!strait) roll = System.Math.Clamp(roll + Density(key) * DensityShift, 0.0, 0.999);
            int count = sea == null ? (roll < 0.3 ? 0 : roll < 0.78 ? 1 : 2)
                      : roll < sea.P0 ? 0 : roll < sea.P1 ? 1 : roll < sea.P2 ? 2 : 3;

            // Codex 키트 섬(2026-11-15): 적 기지 섬(군도만) · 자연 섬. 난수는 따로 만들어 기존 섬 배치를 건드리지 않는다
            var kitRng = new System.Random(unchecked(hash ^ 0x5A17B));
            if (sea != null && sea.Kind == SeaKind.Archipelago && kitRng.NextDouble() < BaseChance)
            {
                double size = kitRng.NextDouble();
                var kind = size < 0.5 ? IslandBuilder.Kit.BaseS : size < 0.85 ? IslandBuilder.Kit.BaseM : IslandBuilder.Kit.BaseL;
                if (IslandBuilder.KitAvailable(kind))
                {
                    float radius = IslandBuilder.KitRadius(kind);
                    float margin = Mathf.Min(radius + 8f, ChunkSize * 0.5f - 1f);   // 대형(반경 약 66m)은 구역보다 커서 가운데 근처에
                    for (int tries = 0; tries < 6; tries++)
                    {
                        var center = new Vector3((key.x + (float)kitRng.NextDouble()) * ChunkSize, 0f, (key.y + (float)kitRng.NextDouble()) * ChunkSize);
                        center.x = Mathf.Clamp(center.x, key.x * ChunkSize + margin, (key.x + 1) * ChunkSize - margin);
                        center.z = Mathf.Clamp(center.z, key.y * ChunkSize + margin, (key.y + 1) * ChunkSize - margin);
                        if (!FarFromOthers(center, radius, sea.MinSpacing) || Horizontal(center - _origin) <= ClearRadius + radius + CoastReach || NearShips(center, radius + 12f + CoastReach)) continue;
                        var info = IslandBuilder.BuildKit(center, kind, new System.Random(kitRng.Next()), transform);
                        if (info != null) { list.Add(info); count = Mathf.Max(0, count - 1); }
                        break;
                    }
                }
            }

            // 내해(4스테이지, 2026-11-16): 섬 대신 유전 플랫폼 · 모래톱(난수 따로)
            if (sea != null && sea.Kind == SeaKind.InlandSea) GenerateInlandSea(key, kitRng, list, sea);

            for (int n = 0; n < count; n++)
            {
                bool rocky = rng.NextDouble() < (sea != null ? sea.RockyChance : 0.55);
                float radius = rocky ? 4f + (float)rng.NextDouble() * 6f : 8f + (float)rng.NextDouble() * 8f;
                if (strait) radius = 4f + (float)rng.NextDouble() * 3f;   // 물길 안에는 작은 바위섬만
                // Codex 섬(모델)이면 그 해안 반경으로 자리를 잡는다
                float artScale = 1f;
                EnvironmentArt art = strait ? null : IslandBuilder.PickArt(rocky, rng, out artScale);
                if (art != null) radius = art.shoreRadius * artScale;
                // Codex 자연 섬(절차형 대신): 바위섬 25% → NAT_RockIslet, 무인도 18% → 모래섬 NAT_IsletBase(야자수 · 텐트 · 난파 보트)
                IslandBuilder.Kit? kit = null;
                float kitScale = 1f;
                double kitRoll = kitRng.NextDouble(), kitSize = kitRng.NextDouble();
                if (art == null && !strait)
                {
                    if (rocky && kitRoll < 0.25 && IslandBuilder.KitAvailable(IslandBuilder.Kit.RockIslet)) { kit = IslandBuilder.Kit.RockIslet; kitScale = Mathf.Lerp(0.8f, 1.35f, (float)kitSize); }
                    else if (!rocky && kitRoll < 0.18 && IslandBuilder.KitAvailable(IslandBuilder.Kit.SandIslet)) { kit = IslandBuilder.Kit.SandIslet; kitScale = Mathf.Lerp(0.85f, 1.15f, (float)kitSize); }
                    if (kit.HasValue) radius = IslandBuilder.KitRadius(kit.Value) * kitScale;
                }
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
                    ok = FarFromOthers(center, radius, sea != null ? sea.MinSpacing : MinSpacing) && Horizontal(center - _origin) > ClearRadius + radius + CoastReach && !NearShips(center, radius + 12f + CoastReach)
                         && (!strait || Mathf.Abs(SeaRegion.Lateral(center)) < SeaRegion.ChannelHalfWidth - radius - 22f)
                         && (!SeaRegion.HarborActive || SeaRegion.HarborT(center) < HarborClearFromT - radius);   // 군항 만 안쪽은 비움
                }
                if (!ok) continue;
                var islandRng = new System.Random(rng.Next());
                var info = art != null ? IslandBuilder.BuildArt(center, art, artScale, islandRng, transform)
                         : kit.HasValue ? IslandBuilder.BuildKit(center, kit.Value, islandRng, transform, kitScale)
                                        : IslandBuilder.Build(center, radius, rocky, islandRng, transform);
                if (info == null) continue;
                IslandBuilder.DressCoast(info, new System.Random(rng.Next()));
                if (art == null && !kit.HasValue) IslandBuilder.DressNature(info);   // 절차형 섬: 야자수 · 사람 흔적 · 난파 보트 · 바위 관목
                list.Add(info);
            }
            return list;
        }

        /// <summary>내해 구역마다: 유전 플랫폼 30%(대 40% · 무장 대 55% · 소 40%), 모래톱 30%(반경 10~18m).</summary>
        public const double PlatformChance = 0.3, SandbarChance = 0.3;

        private void GenerateInlandSea(Vector2Int key, System.Random r, List<IslandInfo> list, SeaRegion.Profile sea)
        {
            double pRoll = r.NextDouble(), pSize = r.NextDouble(), pArmed = r.NextDouble(), sRoll = r.NextDouble(), sSize = r.NextDouble();
            if (pRoll < PlatformChance)
            {
                bool large = pSize < 0.4;
                bool armed = pArmed < (large ? 0.55 : 0.4);
                float radius = large ? 15.5f : 9f;
                if (TryPlace(key, r, radius, sea.MinSpacing, out var c))
                {
                    var info = IslandBuilder.BuildPlatform(c, large, armed, new System.Random(r.Next()), transform);
                    if (info != null) list.Add(info);
                }
            }
            if (sRoll < SandbarChance)
            {
                float radius = Mathf.Lerp(10f, 18f, (float)sSize);
                if (TryPlace(key, r, radius, 12f, out var c))
                    list.Add(IslandBuilder.BuildSandbar(c, radius, new System.Random(r.Next()), transform));
            }
        }

        private bool TryPlace(Vector2Int key, System.Random r, float radius, float spacing, out Vector3 center)
        {
            float margin = Mathf.Min(radius + 8f, ChunkSize * 0.5f - 1f);
            for (int tries = 0; tries < 6; tries++)
            {
                center = new Vector3((key.x + (float)r.NextDouble()) * ChunkSize, 0f, (key.y + (float)r.NextDouble()) * ChunkSize);
                center.x = Mathf.Clamp(center.x, key.x * ChunkSize + margin, (key.x + 1) * ChunkSize - margin);
                center.z = Mathf.Clamp(center.z, key.y * ChunkSize + margin, (key.y + 1) * ChunkSize - margin);
                if (FarFromOthers(center, radius, spacing) && Horizontal(center - _origin) > ClearRadius + radius && !NearShips(center, radius + 12f)) return true;
            }
            center = default;
            return false;
        }

        /// <summary>검증용: 유전 플랫폼 · 모래톱을 지정한 자리에 놓는다.</summary>
        public IslandInfo PlacePlatform(Vector3 center, bool large, bool armed, int seed = 1)
        {
            var key = new Vector2Int(int.MinValue, int.MinValue);
            if (!_chunks.TryGetValue(key, out var list)) _chunks[key] = list = new List<IslandInfo>();
            var info = IslandBuilder.BuildPlatform(center, large, armed, new System.Random(seed), transform);
            if (info != null) list.Add(info);
            Physics.SyncTransforms();
            return info;
        }

        public IslandInfo PlaceSandbar(Vector3 center, float radius, int seed = 1)
        {
            var key = new Vector2Int(int.MinValue, int.MinValue);
            if (!_chunks.TryGetValue(key, out var list)) _chunks[key] = list = new List<IslandInfo>();
            var info = IslandBuilder.BuildSandbar(center, radius, new System.Random(seed), transform);
            list.Add(info);
            return info;
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

        /// <summary>구역 가운데의 섬 밀도(-1 트인 바다 ~ +1 군도). 같은 판 시드면 같은 무늬.</summary>
        public float Density(Vector2Int key)
        {
            if (_density == null || _densitySeed != _seed)
            {
                _densitySeed = _seed;
                _density = new FastNoiseLite(_seed);
                _density.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
                _density.SetFractalType(FastNoiseLite.FractalType.FBm);
                _density.SetFractalOctaves(2);
                _density.SetFrequency(1f / DensityScale);
            }
            return _density.GetNoise((key.x + 0.5f) * ChunkSize, (key.y + 0.5f) * ChunkSize);
        }

        private bool FarFromOthers(Vector3 center, float radius, float spacing)
        {
            foreach (var other in Islands.All)
                if (Horizontal(other.Center - center) < other.Radius + radius + spacing) return false;
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
            // 런타임에 만든 메시만 지운다(Codex 모델 메시 · 기본 도형은 자산이라 남긴다 — EnvironmentArt · KeepSharedMeshes 아래)
            foreach (var mf in info.Root.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null && mf.GetComponentInParent<EnvironmentArt>() == null && mf.GetComponentInParent<KeepSharedMeshes>() == null) Destroy(mf.sharedMesh);
            Destroy(info.Root);
        }
    }
}

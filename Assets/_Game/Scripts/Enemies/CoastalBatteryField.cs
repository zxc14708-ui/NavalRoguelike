using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Data;
using Game.World;

namespace Game.Enemies
{
    /// <summary>
    /// 해안포대 배치(2026-10-11 점검 K04). 해역마다 자리를 정해 두고, 함선이 다가가면 그 자리에 포대를 세운다.
    ///   - 군도: 반경 7m 이상인 섬 중 35%(섬 자리로 정해짐)의 해안 한 곳
    ///   - 좁은 수로: 기슭 4구간(160m)마다 한 곳, 양안을 번갈아
    ///   - 개방해역: 없음
    /// 동시에 3개까지, 함선에서 50~115m 사이에서만 새로 세운다(눈앞에 갑자기 생기지 않게). 230m 넘게 멀어지면 치운다(다시 오면 다시 선다).
    /// 격침된 자리는 그 스테이지 동안 다시 생기지 않는다. 해역이 바뀌면 처음부터.
    /// 포대는 바다 쪽 물가에서 2m 밖에 둔다 — 섬 콜라이더 안이면 바다 쪽 사선도 막히기 때문(뒤쪽은 섬이 가린다).
    /// </summary>
    public sealed class CoastalBatteryField : MonoBehaviour
    {
        public const int MaxActive = 3;
        public const float SpawnRange = 115f, MinSpawnDistance = 50f, DespawnRange = 230f;
        private const int BankEvery = 4;
        private const string DefinitionPath = "Enemies/ene_coastal_battery";

        /// <summary>검증용: 자동 배치를 멈춘다.</summary>
        public static bool Disabled;
        public static CoastalBatteryField Instance { get; private set; }

        private EnemyDefinition _def;
        private readonly Dictionary<long, EnemyController> _placed = new();
        private readonly HashSet<long> _destroyed = new();
        private readonly List<(long key, Vector3 pos, Quaternion rot, float dist)> _candidates = new();
        private readonly List<long> _remove = new();
        private float _timer;

        public EnemyDefinition Definition => _def != null ? _def : _def = Resources.Load<EnemyDefinition>(DefinitionPath);
        public int ActiveCount => _placed.Count;
        public int DestroyedCount => _destroyed.Count;
        public IEnumerable<EnemyController> Placed => _placed.Values;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Disabled = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<CoastalBatteryField>() != null) return;
            if (GameObject.Find("Ocean") == null) return;   // 전투 씬에서만
            new GameObject("Coastal batteries").AddComponent<CoastalBatteryField>();
        }

        private void Awake()
        {
            Instance = this;
            SeaRegion.Changed += OnSeaChanged;
        }

        private void OnDestroy()
        {
            SeaRegion.Changed -= OnSeaChanged;
            if (Instance == this) Instance = null;
        }

        private void OnSeaChanged(SeaKind kind) => Reset();

        /// <summary>세운 포대를 모두 치우고 격침 기록을 지운다.</summary>
        public void Reset()
        {
            foreach (var e in _placed.Values) if (e != null && e.IsAlive) e.RemoveWithoutReward();
            _placed.Clear();
            _destroyed.Clear();
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = 0.5f;
            Prune();
            if (Disabled || !SeaRegion.Active) return;
            var gm = GameManager.Instance;
            var ship = gm != null ? gm.Player : null;
            if (ship == null || gm.State != GameState.Playing || EnemySpawner.Instance == null || Definition == null) return;
            Vector3 me = ship.transform.position;

            // 멀어진 포대 치우기
            _remove.Clear();
            foreach (var kv in _placed)
                if (Flat(kv.Value.transform.position - me) > DespawnRange) _remove.Add(kv.Key);
            foreach (var key in _remove) { _placed[key].RemoveWithoutReward(); _placed.Remove(key); }

            if (_placed.Count >= MaxActive) return;
            CollectCandidates(me);
            _candidates.Sort((a, b) => a.dist.CompareTo(b.dist));
            foreach (var c in _candidates)
            {
                if (_placed.Count >= MaxActive) break;
                if (c.dist < MinSpawnDistance || c.dist > SpawnRange) continue;
                if (_placed.ContainsKey(c.key) || _destroyed.Contains(c.key)) continue;
                var e = EnemySpawner.Instance.SpawnFixed(Definition, c.pos, c.rot);
                if (e != null) _placed[c.key] = e;
            }
        }

        /// <summary>격침(또는 스테이지 전환으로 치워진) 포대를 정리한다. 새로 세우기 전에 불러 풀에서 다시 쓰인 객체와 헷갈리지 않게.</summary>
        private void Prune()
        {
            _remove.Clear();
            foreach (var kv in _placed) if (kv.Value == null || !kv.Value.IsAlive) _remove.Add(kv.Key);
            foreach (var key in _remove) { _placed.Remove(key); _destroyed.Add(key); }
        }

        private void CollectCandidates(Vector3 me)
        {
            _candidates.Clear();
            var profile = SeaRegion.CurrentProfile;
            if (SeaRegion.IsStrait)
            {
                var field = IslandField.Instance;
                if (field == null) return;
                foreach (var (index, _, _) in field.Banks)
                {
                    if (((index % BankEvery) + BankEvery) % BankEvery != 0) continue;
                    int side = ((index / BankEvery) & 1) == 0 ? 1 : -1;
                    float along = index * IslandBuilder.BankSegmentLength + IslandBuilder.BankSegmentLength * 0.5f;
                    float shore = IslandBuilder.BankShore(along, side, field.Seed);
                    Vector3 pos = SeaRegion.ChannelPoint(along, side * (shore - 2.2f));
                    var rot = Quaternion.LookRotation(-SeaRegion.ChannelRight * side, Vector3.up);
                    _candidates.Add((Key(1, index, side), pos, rot, Flat(pos - me)));
                }
                return;
            }
            if (profile.BatteryChance <= 0f) return;
            foreach (var island in Islands.All)
            {
                if (island == null || island.Bank || island.Radius < 7f || island.Root == null) continue;
                int cx = Mathf.RoundToInt(island.Center.x), cz = Mathf.RoundToInt(island.Center.z);
                long key = Key(0, cx, cz);
                double roll = Hash01(cx, cz, 17);
                if (roll >= profile.BatteryChance) continue;
                float angle = (float)Hash01(cx, cz, 91) * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                if (!CoastPoint(island, dir, out Vector3 pos)) continue;
                _candidates.Add((key, pos, Quaternion.LookRotation(dir, Vector3.up), Flat(pos - me)));
            }
        }

        /// <summary>섬 바깥에서 가운데로 쏴 해안선을 찾고, 그 2m 바다 쪽을 포대 자리로 한다.</summary>
        private static bool CoastPoint(IslandInfo island, Vector3 dir, out Vector3 pos)
        {
            pos = default;
            float reach = island.Radius + 25f;
            var origin = new Vector3(island.Center.x, 0.5f, island.Center.z) + dir * reach;
            if (!Physics.Raycast(origin, -dir, out var hit, reach, Islands.Mask, QueryTriggerInteraction.Ignore)) return false;
            pos = new Vector3(hit.point.x, 0f, hit.point.z) + dir * 2f;
            return Islands.IsClear(pos + dir * 3f, 2f);
        }

        private static long Key(int kind, int a, int b) => ((long)kind << 60) ^ ((long)(a & 0x3FFFFFFF) << 30) ^ (b & 0x3FFFFFFF);

        private static double Hash01(int x, int z, int salt)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(z * 19349663) ^ (uint)(salt * 83492791);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (h & 0xFFFFFF) / (double)0x1000000;
            }
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}

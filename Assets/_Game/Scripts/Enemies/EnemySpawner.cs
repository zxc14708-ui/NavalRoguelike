using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Data;

namespace Game.Enemies
{
    /// <summary>
    /// 스테이지 구간 구성에 따라 적을 스폰한다.
    /// 구간이 바뀌어도 살아 있는 적은 그대로 두고 스폰 표만 바꾼다(전투가 끊기지 않게).
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        public static EnemySpawner Instance { get; private set; }

        [Header("Spawn Ring")]
        [SerializeField] private float spawnRadius = 45f;
        [SerializeField] private float spawnRadiusJitter = 8f;
        [Tooltip("보스 호위함이 보스 주변에 나타나는 거리")]
        [SerializeField] private float escortRadius = 7f;

        private RoundSet.Round _round;
        private Transform _player;
        private bool _active;
        private float _accumulator;

        private readonly List<EnemyController> _alive = new();

        private void Awake() => Instance = this;

        /// <summary>구간 시작. StageDirector만 호출한다. 보스가 있으면 이때 등장한다.</summary>
        public void SetPhase(RoundSet.Round phase, Transform player)
        {
            _round = phase;
            _player = player;
            _active = true;

            if (player == null) Debug.LogError("[EnemySpawner] player 미할당.", this);
            if (phase.Boss == null) return;

            var boss = Spawn(phase.Boss);
            if (boss == null || phase.Escort == null || phase.EscortCount <= 0) return;

            // 호위함: 보스 주변 원 위에 고르게, 플레이어 쪽을 향해
            for (int i = 0; i < phase.EscortCount; i++)
            {
                float a = (i + 0.5f) / phase.EscortCount * Mathf.PI * 2f;
                Vector3 pos = boss.transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * escortRadius;
                Vector3 to = player != null ? player.position - pos : Vector3.forward;
                to.y = 0f;
                SpawnAt(phase.Escort, pos, Quaternion.LookRotation(to.sqrMagnitude > 0.01f ? to.normalized : Vector3.forward, Vector3.up));
            }
        }

        private void Update()
        {
            if (!_active || _player == null) return;
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;
            if (_alive.Count >= _round.MaxAlive) return;

            _accumulator += _round.SpawnRate * Time.deltaTime;

            while (_accumulator >= 1f && _alive.Count < _round.MaxAlive)
            {
                _accumulator -= 1f;
                Spawn(RoundSet.PickWeighted(_round, UnderAliveCap));
            }
        }

        /// <summary>종류별 동시 최대 수(EnemyDefinition.MaxAlive)에 아직 여유가 있는가.</summary>
        private bool UnderAliveCap(EnemyDefinition def)
        {
            if (def == null || def.MaxAlive <= 0) return true;
            int n = 0;
            foreach (var e in _alive) if (e != null && e.Definition == def && e.IsAlive) n++;
            return n < def.MaxAlive;
        }

        private EnemyController Spawn(EnemyDefinition def)
        {
            if (def == null || def.Prefab == null) return null;

            var go = PoolManager.Instance?.Spawn(def.Prefab, GetRingPosition(), Quaternion.identity);
            if (go == null) return null;

            var enemy = go.GetComponent<EnemyController>();
            if (enemy == null)
            {
                Debug.LogError($"[EnemySpawner] {def.DisplayName} 프리팹에 EnemyController가 없습니다.", go);
                return null;
            }

            enemy.Setup(def, _player);
            _alive.Add(enemy);
            return enemy;
        }

        /// <summary>플레이어 주변 링 위의 임의 지점.</summary>
        private Vector3 GetRingPosition()
        {
            Vector3 p = _player.position;
            for (int tries = 0; tries < 10; tries++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float r = spawnRadius + Random.Range(-spawnRadiusJitter, spawnRadiusJitter);
                p = _player.position + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                if (Game.World.Islands.IsClear(p, 9f)) return p;   // 섬 위에 나오지 않게
            }
            return p;
        }

        /// <summary>
        /// 보스가 띄우는 함재기처럼 정해진 자리·방향으로 적을 내보낸다. 동시 최대 수 제한을 받지 않지만
        /// 살아 있는 적 목록에는 들어가 스테이지 전환 때 함께 치워진다.
        /// </summary>
        public EnemyController SpawnAt(EnemyDefinition def, Vector3 position, Quaternion rotation)
        {
            if (def == null || def.Prefab == null || _player == null) return null;

            var go = PoolManager.Instance?.Spawn(def.Prefab, position, rotation);
            if (go == null || !go.TryGetComponent<EnemyController>(out var enemy)) return null;

            enemy.Setup(def, _player);
            enemy.transform.SetPositionAndRotation(position, rotation);   // Setup이 플레이어 쪽으로 돌려 놓은 것을 되돌린다
            _alive.Add(enemy);
            return enemy;
        }

        /// <summary>스테이지 전환: 남은 적을 보상 없이 모두 치우고 스폰을 멈춘다(다음 SetPhase까지).</summary>
        public void ClearAll()
        {
            _active = false;
            _accumulator = 0f;
            var snapshot = _alive.ToArray();
            foreach (var enemy in snapshot)
                if (enemy != null && enemy.isActiveAndEnabled) enemy.RemoveWithoutReward();
            _alive.Clear();
        }

        /// <summary>적이 격침되거나 회수될 때 호출한다.</summary>
        public void NotifyEnemyRemoved(EnemyController enemy) => _alive.Remove(enemy);
    }
}

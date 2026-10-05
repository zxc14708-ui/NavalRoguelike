using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    /// <summary>
    /// 프리팹 단위 오브젝트 풀. 적/포탄/미사일/XP 픽업은 반드시 이 경로로만 생성한다.
    /// Instantiate/Destroy 직접 호출 금지.
    /// </summary>
    public class PoolManager : MonoBehaviour
    {
        public static PoolManager Instance { get; private set; }

        [SerializeField] private int defaultCapacity = 64;
        [SerializeField] private int maxSize = 1024;

        private readonly Dictionary<GameObject, ObjectPool<GameObject>> _pools = new();
        private readonly Dictionary<GameObject, GameObject> _instanceToPrefab = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>프리팹을 풀에서 꺼내 배치한다.</summary>
        public GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            if (prefab == null) { Debug.LogError("[PoolManager] prefab이 null입니다."); return null; }

            var pool = GetOrCreate(prefab);
            var go = pool.Get();
            go.transform.SetPositionAndRotation(pos, rot);
            _instanceToPrefab[go] = prefab;

            // IPoolable을 구현한 컴포넌트에 재사용 시점을 알린다.
            foreach (var p in go.GetComponentsInChildren<IPoolable>(true)) p.OnSpawned();
            return go;
        }

        /// <summary>풀로 반납. 어떤 풀 소속인지 모르면 그냥 비활성화한다.</summary>
        public void Despawn(GameObject go)
        {
            if (go == null) return;
            foreach (var p in go.GetComponentsInChildren<IPoolable>(true)) p.OnDespawned();

            if (_instanceToPrefab.TryGetValue(go, out var prefab) && _pools.TryGetValue(prefab, out var pool))
                pool.Release(go);
            else
                go.SetActive(false);
        }

        /// <summary>웨이브 시작 전 미리 채워 스파이크를 방지한다.</summary>
        public void Prewarm(GameObject prefab, int count) { /* TODO */ }

        private ObjectPool<GameObject> GetOrCreate(GameObject prefab)
        {
            if (_pools.TryGetValue(prefab, out var pool)) return pool;

            pool = new ObjectPool<GameObject>(
                createFunc: () => Instantiate(prefab, transform),
                actionOnGet: go => go.SetActive(true),
                actionOnRelease: go => go.SetActive(false),
                actionOnDestroy: Destroy,
                collectionCheck: false,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize);

            _pools[prefab] = pool;
            return pool;
        }
    }

    /// <summary>풀에서 꺼내지고 반납될 때 런타임 상태를 초기화하기 위한 훅.</summary>
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }
}

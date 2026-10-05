using System.Collections.Generic;
using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// 연돌 배기 연기(장식, 판정 없음). 모델에서 연돌 끝(FunnelTop·FunnelCap… 없으면 Funnel, Exhaust)을 찾아 그 위에서 연기를 뿜는다.
    ///   - 기관 부하(0~1)가 클수록 연기가 짙고 많다. 정지 중에도 옅게 조금 나온다.
    ///   - 부하를 올리는 순간(전진 기어 올림) 검은 연기가 한 번 크게 솟는다.
    ///   - 연기는 월드 공간에 남아 배가 지나간 자리로 흘러가고, 바람을 따라 기운다.
    /// 플레이어 함선은 함교 모듈을 설치·교체할 수 있으므로 연돌을 1초마다 다시 찾는다.
    /// 연돌이 없는 배(고속정 등)는 아무것도 하지 않는다.
    /// </summary>
    public class FunnelSmoke : MonoBehaviour
    {
        private static readonly string[][] StackNames =
        {
            new[] { "FunnelTop", "FunnelCap", "FunnelBlackCap" },
            new[] { "Funnel" },
            new[] { "Exhaust" },
        };

        /// <summary>기관 부하 0~1.</summary>
        public System.Func<float> Load;

        private readonly List<Transform> _stacks = new();
        private float _nextSearch, _debt, _puff, _lastLoad, _scale = 1f;

        /// <summary>검증용: 찾은 연돌 수, 검은 연기 세기(0~1).</summary>
        public int StackCount => _stacks.Count;
        public float Puff => _puff;

        public static FunnelSmoke Ensure(GameObject ship, System.Func<float> load)
        {
            if (!ship.TryGetComponent<FunnelSmoke>(out var smoke)) smoke = ship.AddComponent<FunnelSmoke>();
            smoke.Load = load;
            smoke._nextSearch = 0f;
            return smoke;
        }

        private void OnEnable()
        {
            _nextSearch = 0f;
            _puff = 0f;
            _lastLoad = -1f;
        }

        private void LateUpdate()
        {
            if (Time.time >= _nextSearch)
            {
                _nextSearch = Time.time + 1f;
                if (_stacks.Count == 0 || _stacks.Exists(s => s == null || !s.gameObject.activeInHierarchy)) FindStacks();
            }
            if (_stacks.Count == 0) return;

            float dt = Time.deltaTime;
            float load = Mathf.Clamp01(Load != null ? Load() : 0.3f);
            if (_lastLoad >= 0f && load > _lastLoad + 0.1f) _puff = 1f;   // 기어를 올리면 검은 연기
            _lastLoad = Mathf.MoveTowards(_lastLoad < 0f ? load : _lastLoad, load, dt * 0.5f);
            _puff = Mathf.MoveTowards(_puff, 0f, dt / 2.2f);

            float rate = (6f + 20f * load + 26f * _puff) * _stacks.Count;
            _debt += rate * dt;
            int n = Mathf.Min(8, (int)_debt);
            _debt -= n;

            float dark = Mathf.Clamp01(0.2f * load + 0.85f * _puff);
            for (int i = 0; i < n; i++)
            {
                var stack = _stacks[Random.Range(0, _stacks.Count)];
                if (stack == null) continue;
                Vector3 p = Top(stack) + Random.insideUnitSphere * 0.12f * _scale;
                Vector3 v = Vector3.up * (1.4f + 1.4f * load + 1.2f * _puff) + DecorFx.Wind * 0.7f + Random.insideUnitSphere * 0.3f;
                float g = Mathf.Lerp(0.5f, 0.13f, dark) + Random.Range(-0.04f, 0.04f);
                float a = Mathf.Lerp(0.3f, 0.62f, Mathf.Max(load * 0.6f, dark));
                DecorFx.Emit(DecorFx.Smoke, p, v, Random.Range(2.4f, 3.6f), Random.Range(1.6f, 2.4f) * _scale * (1f + 0.5f * _puff),
                    new Color(g, g, g * 1.04f, a));
            }
        }

        private static Vector3 Top(Transform stack)
        {
            var r = stack.GetComponentInChildren<Renderer>();
            if (r == null) return stack.position;
            var b = r.bounds;
            return new Vector3(b.center.x, b.max.y + 0.1f, b.center.z);
        }

        private void FindStacks()
        {
            _stacks.Clear();
            var all = GetComponentsInChildren<Transform>(false);
            foreach (var group in StackNames)
            {
                foreach (var t in all)
                {
                    if (t == transform || !StartsWithAny(t.name, group)) continue;
                    if (t.name.EndsWith("_Mesh") && t.parent != null && StartsWithAny(t.parent.name, group)) continue;
                    Vector3 top = Top(t);
                    if (_stacks.Exists(s => (Top(s) - top).sqrMagnitude < 0.36f)) continue;   // 같은 연돌의 다른 조각
                    _stacks.Add(t);
                    if (_stacks.Count >= 3) break;
                }
                if (_stacks.Count > 0) break;
            }

            // 연돌 굵기에 맞춰 연기 크기
            _scale = 1f;
            if (_stacks.Count > 0)
            {
                var r = _stacks[0].GetComponentInChildren<Renderer>();
                if (r != null) _scale = Mathf.Clamp(Mathf.Max(r.bounds.size.x, r.bounds.size.z) / 1.2f, 0.6f, 2f);
            }
        }

        private static bool StartsWithAny(string name, string[] prefixes)
        {
            foreach (var p in prefixes) if (name.StartsWith(p, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}

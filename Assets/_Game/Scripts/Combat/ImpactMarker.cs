using UnityEngine;
using Game.Core;

namespace Game.Combat
{
    /// <summary>착탄 경고 원. 착탄이 가까워질수록 안쪽 원이 바깥 원까지 차오른다.</summary>
    public class ImpactMarker : MonoBehaviour, IPoolable
    {
        [Tooltip("착탄 반경을 나타내는 바깥 원")]
        [SerializeField] private Transform outer;
        [Tooltip("시간이 지나며 커지는 안쪽 원")]
        [SerializeField] private Transform inner;

        private float _radius, _duration, _age;

        public void Show(float radius, float duration)
        {
            _radius = radius;
            _duration = Mathf.Max(0.1f, duration);
            _age = 0f;
            float d = radius * 2f;
            if (outer != null) outer.localScale = new Vector3(d, outer.localScale.y, d);
            if (inner != null) inner.localScale = new Vector3(0.01f, inner.localScale.y, 0.01f);
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float d = _radius * 2f * Mathf.Clamp01(_age / _duration);
            if (inner != null) inner.localScale = new Vector3(Mathf.Max(0.01f, d), inner.localScale.y, Mathf.Max(0.01f, d));
        }

        public void OnSpawned() => _age = 0f;
        public void OnDespawned() { }
    }
}

using UnityEngine;
using Game.Core;

namespace Game.Combat
{
    /// <summary>
    /// 전함 주포탄. 포구에서 착탄점까지 높은 포물선으로 날아가고, 착탄 전까지 수면에 경고 원을 띄운다.
    /// 착탄하면 물기둥과 함께 반경 안의 함체에 피해를 준다 — 원을 보고 조함으로 피하는 패턴이다.
    /// </summary>
    public class ArtilleryShell : MonoBehaviour, IPoolable
    {
        [SerializeField] private GameObject warningMarker;
        [SerializeField] private GameObject impactEffect;

        [Tooltip("탄도 최고점 높이(m)")]
        [SerializeField] private float apexHeight = 14f;

        private Vector3 _from, _to;
        private float _flightTime, _age, _damage, _radius;
        private Collider _target;
        private GameObject _marker;
        private bool _flying;

        /// <summary>발사. 경고 원도 함께 띄운다.</summary>
        public void Launch(Vector3 muzzle, Vector3 impactPoint, float flightTime, float damage, float radius, Collider target)
        {
            _from = muzzle;
            _to = impactPoint;
            _flightTime = Mathf.Max(0.3f, flightTime);
            _damage = damage;
            _radius = radius;
            _target = target;
            _age = 0f;
            _flying = true;
            transform.position = muzzle;

            if (warningMarker != null && PoolManager.Instance != null)
            {
                _marker = PoolManager.Instance.Spawn(warningMarker, new Vector3(_to.x, 0.05f, _to.z), Quaternion.identity);
                if (_marker != null && _marker.TryGetComponent<ImpactMarker>(out var m)) m.Show(radius, _flightTime);
            }
        }

        private void Update()
        {
            if (!_flying) return;

            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _flightTime);

            Vector3 p = Vector3.Lerp(_from, _to, t) + Vector3.up * (4f * apexHeight * t * (1f - t));
            Vector3 v = p - transform.position;
            transform.position = p;
            if (v.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(v.normalized, Vector3.up);

            if (t >= 1f) Impact();
        }

        private void Impact()
        {
            _flying = false;

            if (_target != null && _target.gameObject.activeInHierarchy)
            {
                Vector3 closest = _target.ClosestPoint(_to);
                Vector3 flat = closest - _to;
                flat.y = 0f;
                if (flat.sqrMagnitude <= _radius * _radius &&
                    _target.TryGetComponent<IDamageable>(out var dmg) && dmg.IsAlive)
                {
                    dmg.TakeDamage(new DamageInfo(_damage, closest, Vector3.down, DamageSource.Gun));
                }
            }

            PooledEffect.Spawn(impactEffect, new Vector3(_to.x, 0f, _to.z));
            AudioManager.Play(Game.Data.SfxId.Explosion, _to, 0.6f, 0.75f);
            Despawn();
        }

        private void Despawn()
        {
            if (_marker != null && PoolManager.Instance != null) PoolManager.Instance.Despawn(_marker);
            _marker = null;
            if (PoolManager.Instance != null) PoolManager.Instance.Despawn(gameObject);
            else gameObject.SetActive(false);
        }

        public void OnSpawned() { _flying = false; }

        public void OnDespawned()
        {
            _flying = false;
            if (_marker != null && PoolManager.Instance != null) PoolManager.Instance.Despawn(_marker);
            _marker = null;
        }
    }
}

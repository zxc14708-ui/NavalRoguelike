using UnityEngine;
using Game.Core;

namespace Game.Combat
{
    /// <summary>
    /// 전함 주포탄. 포구에서 착탄점까지 높은 포물선으로 날아가고, 착탄 전까지 수면에 경고 원을 띄운다.
    /// 착탄하면 물기둥과 함께 반경 안의 함체에 피해를 준다 — 원을 보고 조함으로 피하는 패턴이다.
    /// 아군 곡사포(LaunchArea)는 같은 탄도로 날아가 반경 안의 적 수상함 모두에 피해를 준다(경고 원 없음).
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

        // 아군 곡사포: 반경 안 적 전부, 가운데 100% → 가장자리 edgeRatio
        private bool _area;
        private float _apex, _edgeRatio;
        private TargetEfficiency _efficiency;
        private string _statsKey;
        private readonly System.Collections.Generic.List<ITargetable> _victims = new();

        /// <summary>마지막 착탄에서 피해를 준 적 수(검증용).</summary>
        public int LastHitCount { get; private set; }

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
            _area = false;
            _apex = apexHeight;
            transform.position = muzzle;

            if (warningMarker != null && PoolManager.Instance != null)
            {
                _marker = PoolManager.Instance.Spawn(warningMarker, new Vector3(_to.x, 0.05f, _to.z), Quaternion.identity);
                if (_marker != null && _marker.TryGetComponent<ImpactMarker>(out var m)) m.Show(radius, _flightTime);
            }
        }

        /// <summary>
        /// 아군 곡사포탄. 탄도 최고점은 거리에 비례(apex). 착탄 반경 안의 적 수상함(드러난 잠수함 포함) 모두에
        /// 피해 × 분류별 효율 × (가운데 1 → 가장자리 edgeRatio). 경고 원은 띄우지 않는다(적의 위험 표시와 헷갈리지 않게).
        /// </summary>
        public void LaunchArea(Vector3 muzzle, Vector3 impactPoint, float flightTime, float apex,
                               float damage, float radius, float edgeRatio, TargetEfficiency efficiency)
        {
            _from = muzzle;
            _to = impactPoint;
            _flightTime = Mathf.Max(0.3f, flightTime);
            _damage = damage;
            _radius = radius;
            _edgeRatio = Mathf.Clamp01(edgeRatio);
            _efficiency = efficiency;
            _target = null;
            _age = 0f;
            _flying = true;
            _area = true;
            _apex = Mathf.Max(2f, apex);
            transform.position = muzzle;
            _statsKey = "곡사포";
            CombatStats.RecordFire(_statsKey);
        }

        private void Update()
        {
            if (!_flying) return;

            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _flightTime);

            Vector3 p = Vector3.Lerp(_from, _to, t) + Vector3.up * (4f * _apex * t * (1f - t));
            Vector3 v = p - transform.position;
            transform.position = p;
            if (v.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(v.normalized, Vector3.up);

            if (t >= 1f) Impact();
        }

        private void Impact()
        {
            _flying = false;

            if (_area) AreaDamage();
            else if (_target != null && _target.gameObject.activeInHierarchy)
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

        private void AreaDamage()
        {
            _victims.Clear();
            float r2 = _radius * _radius;
            foreach (var kind in new[] { TargetKind.Surface, TargetKind.Submarine })
            {
                var list = TargetRegistry.HostileTo(CombatFaction.Player, kind);
                for (int i = 0; i < list.Count; i++)
                {
                    var t = list[i];
                    if (t == null || !t.IsAlive || t.Transform == null) continue;
                    if (kind == TargetKind.Submarine && !t.IsRevealed) continue;   // 잠항 중이면 맞지 않는다
                    Vector3 d = t.Transform.position - _to;
                    d.y = 0f;
                    if (d.sqrMagnitude > r2) continue;
                    _victims.Add(t);
                }
            }
            LastHitCount = 0;
            foreach (var t in _victims)
            {
                if (t is not IDamageable dmg || !dmg.IsAlive) continue;
                float eff = _efficiency.For(TargetInfo.Category(t));
                if (eff <= 0f) continue;
                Vector3 d = t.Transform.position - _to;
                d.y = 0f;
                float falloff = Mathf.Lerp(1f, _edgeRatio, Mathf.Clamp01(d.magnitude / Mathf.Max(0.01f, _radius)));
                dmg.TakeDamage(new DamageInfo(_damage * eff * falloff, t.Transform.position, Vector3.down, DamageSource.Gun, _statsKey));
                CombatStats.RecordHit(_statsKey, t.Transform.position);
                LastHitCount++;
            }
            _victims.Clear();
        }

        private void Despawn()
        {
            if (_marker != null && PoolManager.Instance != null) PoolManager.Instance.Despawn(_marker);
            _marker = null;
            if (PoolManager.Instance != null) PoolManager.Instance.Despawn(gameObject);
            else gameObject.SetActive(false);
        }

        public void OnSpawned() { _flying = false; _area = false; }

        public void OnDespawned()
        {
            _flying = false;
            if (_marker != null && PoolManager.Instance != null) PoolManager.Instance.Despawn(_marker);
            _marker = null;
        }
    }
}

using UnityEngine;
using Game.Core;
using Game.Enemies;

namespace Game.Combat
{
    /// <summary>
    /// 경어뢰. VLS 대잠탄이 마지막 접촉 지점에 투하하거나(Launch), 현측 발사관이 부채꼴로 쏜다(LaunchFromTube).
    /// 입수 후 단서 지점으로 달려가 그 둘레를 돌며 찾고, 자체 센서 반경 안에 잠수함이 들어오면 추적한다.
    /// </summary>
    public class AswTorpedo : MonoBehaviour, IPoolable
    {
        [SerializeField] private float sinkTime = 0.55f;
        [SerializeField] private float runDepth = -0.45f;
        [SerializeField] private float speed = 10f;
        [SerializeField] private float searchRadius = 8f;
        [SerializeField] private float hitRadius = 1.2f;
        [SerializeField] private float lifeTime = 7f;
        [SerializeField] private float turnRate = 220f;
        [SerializeField] private GameObject impactEffect;

        private Vector3 _cue;
        private SubmarineBase _target;
        private float _age, _damage, _searchAngle;
        private bool _fromTube;   // 발사관: 입수하는 동안에도 발사 방향으로 미끄러져 나간다
        private string _statsKey;

        private void Awake()
        {
            if (transform.childCount != 0) return;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Torpedo body";
            body.transform.SetParent(transform, false);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localScale = new Vector3(0.22f, 0.68f, 0.22f);
            if (body.TryGetComponent<Collider>(out var collider)) Destroy(collider);
            if (body.TryGetComponent<Renderer>(out var renderer)) renderer.material.color = new Color(0.64f, 0.74f, 0.78f);
        }

        public void Launch(Vector3 dropPoint, Vector3 lastKnownPosition, float damage)
        {
            transform.position = dropPoint;
            _cue = new Vector3(lastKnownPosition.x, runDepth, lastKnownPosition.z);
            _damage = damage;
            _age = _searchAngle = 0f;
            _target = null;
            _fromTube = false;
            _statsKey = CombatStats.KeyFor(gameObject);
            CombatStats.RecordFire(_statsKey);
        }

        /// <summary>현측 발사관: 발사 방향을 향해 물에 들어간 뒤 단서 지점(부채꼴의 한 갈래 끝)으로 달린다.</summary>
        public void LaunchFromTube(Vector3 muzzle, Vector3 heading, Vector3 cue, float damage)
        {
            Launch(muzzle, cue, damage);
            heading.y = 0f;
            if (heading.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(heading.normalized, Vector3.up);
            _fromTube = true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (_age >= lifeTime) { Despawn(); return; }

            if (_age < sinkTime)
            {
                Vector3 p = transform.position;
                p.y = Mathf.MoveTowards(p.y, runDepth, (Mathf.Max(0f, p.y - runDepth) + 0.2f) * dt / Mathf.Max(0.05f, sinkTime - _age));
                if (_fromTube) p += transform.forward * (speed * 0.6f * dt);
                transform.position = p;
                return;
            }
            var atDepth = transform.position;
            atDepth.y = runDepth;
            transform.position = atDepth;

            _target = FindLocalTarget();
            Vector3 goal;
            if (_target != null)
                goal = new Vector3(_target.transform.position.x, runDepth, _target.transform.position.z);
            else
            {
                _searchAngle += dt * 115f;
                goal = _cue + new Vector3(Mathf.Cos(_searchAngle * Mathf.Deg2Rad), 0f,
                                           Mathf.Sin(_searchAngle * Mathf.Deg2Rad)) * 4f;
            }

            Vector3 direction = goal - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction.normalized, Vector3.up), turnRate * dt);
            transform.position += transform.forward * (speed * dt);

            if (_target == null) return;
            Vector3 offset = _target.transform.position - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > hitRadius * hitRadius) return;

            _target.ConfirmContact(4f);
            _target.TakeDamage(new DamageInfo(_damage, transform.position, transform.forward, DamageSource.Torpedo, _statsKey));
            CombatStats.RecordHit(_statsKey, transform.position);
            PooledEffect.Spawn(impactEffect, new Vector3(transform.position.x, 0f, transform.position.z));
            AudioManager.Play(Game.Data.SfxId.Explosion, transform.position, 0.8f, 0.65f);
            Despawn();
        }

        private SubmarineBase FindLocalTarget()
        {
            SubmarineBase nearest = null;
            float best = searchRadius * searchRadius;
            foreach (var t in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Submarine))
            {
                if (t is not SubmarineBase sub || !sub.IsAlive) continue;
                Vector3 offset = sub.transform.position - transform.position;
                offset.y = 0f;
                float d = offset.sqrMagnitude;
                if (d >= best) continue;
                best = d;
                nearest = sub;
            }
            return nearest;
        }

        private void Despawn()
        {
            if (PoolManager.Instance != null) PoolManager.Instance.Despawn(gameObject);
            else gameObject.SetActive(false);
        }

        public void OnSpawned() { _age = 0f; _target = null; _fromTube = false; }
        public void OnDespawned() { _target = null; }
    }
}

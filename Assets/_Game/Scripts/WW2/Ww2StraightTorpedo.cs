using UnityEngine;
using Game.Core;
using Game.Combat;

namespace Game.WW2
{
    /// <summary>
    /// 플레이어 현측 어뢰(WWII 패치 B). 발사한 방향으로 수면 바로 아래를 곧게 달린다 — <b>유도하지 않는다</b>.
    ///   - 발사 후 <c>armDistance</c>를 달리기 전에는 신관이 작동하지 않아 맞아도 불발(물보라만).
    ///   - 적 수상함(항공기 제외)에 닿으면 피해. 섬에 닿으면 폭발. 사거리를 다 달리면 가라앉는다(빗나감).
    ///   - 흰 항적을 남겨 어디로 갔는지 보인다. 기록: 발사·명중·빗나감(CombatStats "현측 어뢰", Ww2BattleLog).
    /// 적의 어뢰(Combat.Torpedo)와 같은 직진 규칙이지만 진영·신관·사거리가 다르다.
    /// </summary>
    public class Ww2StraightTorpedo : MonoBehaviour, IPoolable
    {
        [SerializeField] private float hitRadius = 0.6f;
        [SerializeField] private float runDepth = -0.32f;
        [SerializeField] private LayerMask hitMask = 1 << 7;
        [SerializeField] private TrailRenderer wake;
        [SerializeField] private GameObject impactEffect;

        private Vector3 _dir;
        private float _speed, _range, _arm, _damage, _travelled;
        private bool _running;

        public bool Running => _running;
        public Vector3 Direction => _dir;
        public float Travelled => _travelled;
        /// <summary>발사 순간 방향과 지금 방향의 차이(유도하지 않음 검증용, 항상 0).</summary>
        public float HeadingDrift => _running ? Vector3.Angle(_dir, transform.forward) : 0f;

        public void Launch(Vector3 direction, float speed, float range, float armDistance, float damage)
        {
            direction.y = 0f;
            _dir = direction.sqrMagnitude > 1e-4f ? direction.normalized : transform.forward;
            _speed = Mathf.Max(1f, speed);
            _range = Mathf.Max(5f, range);
            _arm = Mathf.Max(0f, armDistance);
            _damage = damage;
            _travelled = 0f;
            _running = true;
            var p = transform.position;
            transform.SetPositionAndRotation(new Vector3(p.x, runDepth, p.z), Quaternion.LookRotation(_dir, Vector3.up));
            if (wake != null) wake.Clear();
            CombatStats.RecordFire(Ww2BattleLog.PlayerTorpedo);
            Ww2BattleLog.Released(Ww2BattleLog.PlayerTorpedo);
            PooledEffect.Spawn(impactEffect, new Vector3(p.x, 0f, p.z), 0.3f);
        }

        private void Update()
        {
            if (!_running) return;
            float step = _speed * Time.deltaTime;
            if (step <= 0f) return;

            if (Physics.SphereCast(transform.position, hitRadius, _dir, out var hit, step,
                                   hitMask | Game.World.Islands.Mask, QueryTriggerInteraction.Collide))
            {
                var target = hit.collider.GetComponentInParent<IDamageable>();
                bool enemyShip = target is ITargetable t && t.Kind != TargetKind.Aircraft && target.IsAlive &&
                                 Factions.CanDamage(CombatFaction.Player, target);
                if (enemyShip || target == null)
                {
                    Detonate(hit.point, enemyShip ? target : null);
                    return;
                }
            }

            transform.position += _dir * step;
            _travelled += step;
            if (_travelled >= _range)
            {
                Ww2BattleLog.Miss(Ww2BattleLog.PlayerTorpedo);
                PooledEffect.Spawn(impactEffect, new Vector3(transform.position.x, 0f, transform.position.z), 0.25f);
                Despawn();
            }
        }

        private void Detonate(Vector3 point, IDamageable target)
        {
            Vector3 at = new(point.x, 0f, point.z);
            if (target != null && _travelled < _arm)
            {
                // 신관이 아직 작동하지 않았다 — 맞고 튕겨 나간다
                CombatLog.Add("어뢰", "불발 — 너무 가까움");
                Ww2BattleLog.Miss(Ww2BattleLog.PlayerTorpedo);
                PooledEffect.Spawn(impactEffect, at, 0.3f);
                Despawn();
                return;
            }
            if (target != null)
            {
                target.TakeDamage(new DamageInfo(_damage, point, _dir, DamageSource.Torpedo, Ww2BattleLog.PlayerTorpedo));
                CombatStats.RecordHit(Ww2BattleLog.PlayerTorpedo, point);
                Ww2BattleLog.Hit(Ww2BattleLog.PlayerTorpedo);
                PooledEffect.Spawn(impactEffect, at, 1.3f);
                AudioManager.Play(Game.Data.SfxId.Explosion, point, 1f, 0.6f);
            }
            else
            {
                Ww2BattleLog.Miss(Ww2BattleLog.PlayerTorpedo);
                PooledEffect.Spawn(impactEffect, at, 0.8f);
                AudioManager.Play(Game.Data.SfxId.Explosion, point, 0.6f, 0.7f);
            }
            Despawn();
        }

        private void Despawn()
        {
            _running = false;
            if (PoolManager.Instance != null) PoolManager.Instance.Despawn(gameObject);
            else gameObject.SetActive(false);
        }

        public void OnSpawned() { _running = false; _travelled = 0f; }
        public void OnDespawned() { _running = false; }
    }
}

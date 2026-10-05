using System.Collections.Generic;
using UnityEngine;
using Game.Core;

namespace Game.Combat
{
    /// <summary>
    /// 어뢰. 발사 순간 겨눈 방향으로 수면 바로 아래를 곧게 달린다. 유도하지 않는다.
    ///
    /// CIWS나 기만체로는 막을 수 없으므로, 대응 수단은 조함이어야 한다.
    /// 유도하면 피할 방법이 없어 무조건 맞는다. 곧게 달리게 하고 하얀 항적을 남겨,
    /// 발사를 보고 진로에서 벗어나면 피할 수 있게 한다.
    ///
    /// 발사 신호: 발사 지점 물보라 + 경고음 + 화면 표시(MissileWarningUI가 Active 목록을 읽어 "어뢰"로 표시).
    /// 속력 16(약 31노트, 함선 최고 30노트보다 조금 빠름) → 30~42m에서 쏘아 도착까지 약 2.5~3초.
    /// 어뢰는 "지금 속력·침로 그대로 가면 만나는 점"을 노리므로 그 사이 전령기를 한 칸 바꾸거나 타를 쓰면 빗나간다.
    /// 전속으로 어뢰에서 멀어지면 따라잡히지 않는다.
    /// CIWS·함포의 표적은 아니다(TargetRegistry에 올리지 않는다). ITargetable은 화면 표시용이다.
    /// </summary>
    public class Torpedo : MonoBehaviour, IPoolable, ITargetable
    {
        private static readonly List<Torpedo> s_active = new();

        /// <summary>물속을 달리고 있는 어뢰(화면 경고·검증용).</summary>
        public static IReadOnlyList<Torpedo> Active => s_active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_active.Clear();

        [SerializeField] private float speed = 16f;
        [SerializeField] private float lifeTime = 9f;
        [SerializeField] private float hitRadius = 0.8f;
        [Tooltip("항주 심도. 바다 평면(-0.9)보다 위여야 보인다.")]
        [SerializeField] private float runDepth = -0.35f;
        [SerializeField] private LayerMask hitMask;
        [SerializeField] private TrailRenderer wake;
        [Tooltip("명중 시 물기둥")]
        [SerializeField] private GameObject impactEffect;
        [Tooltip("발사 순간 발사 지점에 띄우는 물보라 크기(명중 물기둥 대비)")]
        [SerializeField] private float launchSplashScale = 0.4f;

        private float _damage;
        private float _age;
        private float _countermeasureTimer;

        public float Speed => speed;
        public GameObject ImpactEffect => impactEffect;

        // --- ITargetable(화면 표시용)
        public Transform Transform => transform;
        public TargetKind Kind => TargetKind.Missile;
        /// <summary>적 잠수함의 어뢰.</summary>
        public CombatFaction Faction => CombatFaction.Hostile;
        public bool IsAlive => isActiveAndEnabled;
        public bool IsRevealed => true;

        /// <summary>aimPoint를 향해 수평으로 곧게 달린다. 이후 방향은 바뀌지 않는다.</summary>
        public void Launch(Vector3 aimPoint, float damage)
        {
            _damage = damage;
            _age = 0f;

            var p = transform.position;
            transform.position = new Vector3(p.x, runDepth, p.z);

            Vector3 to = aimPoint - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to, Vector3.up);

            if (wake != null) wake.Clear();
            if (!s_active.Contains(this)) s_active.Add(this);

            // 발사 신호: 물보라와 경고음(소나 핑을 높고 크게)
            PooledEffect.Spawn(impactEffect, new Vector3(p.x, 0f, p.z), launchSplashScale);
            AudioManager.Play(Game.Data.SfxId.MissileLaunch, transform.position, 0.3f, 0.6f);
            AudioManager.Play(Game.Data.SfxId.SonarPing, transform.position, 1f, 1.45f);
            CombatLog.Add("어뢰", $"발사 · 피해 {damage:0}");
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (_age >= lifeTime) { Despawn(); return; }

            // 어뢰 기만기(아직 없음)가 있으면 주기적으로 무력화를 시도한다
            if (TorpedoCountermeasures.Count > 0)
            {
                _countermeasureTimer -= dt;
                if (_countermeasureTimer <= 0f)
                {
                    _countermeasureTimer = 0.25f;
                    if (TorpedoCountermeasures.TryDefeat(this)) { Despawn(); return; }
                }
            }

            float step = speed * dt;
            if (Physics.SphereCast(transform.position, hitRadius, transform.forward, out var hit, step,
                                   hitMask | Game.World.Islands.Mask, QueryTriggerInteraction.Collide))
            {
                if (hit.collider.TryGetComponent<IDamageable>(out var target) && target.IsAlive && Factions.CanDamage(Faction, target))
                    target.TakeDamage(new DamageInfo(_damage, hit.point, transform.forward, DamageSource.Torpedo));

                PooledEffect.Spawn(impactEffect, new Vector3(hit.point.x, 0f, hit.point.z));
                AudioManager.Play(Game.Data.SfxId.Explosion, hit.point, 0.9f, 0.65f);
                Despawn();
                return;
            }

            transform.position += transform.forward * step;
        }

        private void Despawn()
        {
            if (PoolManager.Instance != null) PoolManager.Instance.Despawn(gameObject);
            else gameObject.SetActive(false);
        }

        public void OnSpawned() { _age = 0f; _countermeasureTimer = 0f; }
        public void OnDespawned() => s_active.Remove(this);
        private void OnDisable() => s_active.Remove(this);
    }
}

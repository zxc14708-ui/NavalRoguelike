using UnityEngine;
using Game.Core;

namespace Game.Combat
{
    /// <summary>
    /// 직선으로 날아가는 포탄. 기관포·CIWS·76mm·적 포탄이 공용으로 사용한다.
    /// 반드시 PoolManager를 통해서만 생성/반납한다.
    ///
    /// 전투 피드백
    ///   - 예광탄: tracerTime > 0이면 짧은 발광 꼬리를 단다(기관포·CIWS).
    ///   - 명중: impactEffect를 impactEffectScale 크기로(기관포는 작은 불꽃, 76mm는 폭발).
    ///   - 빗나감: waterEffect가 있으면 수면에 닿을 때 물기둥(76mm).
    /// </summary>
    public class Projectile : MonoBehaviour, IPoolable
    {
        [SerializeField] private float lifeTime = 3f;
        [SerializeField] private float hitRadius = 0.4f;
        [SerializeField] private LayerMask hitMask;

        [Header("High Explosive (선택)")]
        [Tooltip("0보다 크면 명중 지점 반경 안의 다른 표적에게도 파편 피해를 준다")]
        [SerializeField] private float splashRadius;
        [SerializeField, Range(0f, 1f)] private float splashDamageRatio = 0.5f;
        [SerializeField] private GameObject impactEffect;
        [SerializeField] private float impactEffectScale = 1f;

        [Header("Feedback (선택)")]
        [Tooltip("빗나가 수면에 닿으면 띄우는 물기둥")]
        [SerializeField] private GameObject waterEffect;
        [SerializeField] private float waterEffectScale = 0.35f;
        [Tooltip("예광탄 꼬리 길이(초). 0이면 없음")]
        [SerializeField] private float tracerTime;
        [SerializeField] private Color tracerColor = new(1f, 0.85f, 0.35f, 0.9f);
        [SerializeField] private float tracerWidth = 0.12f;

        private static readonly Collider[] SplashHits = new Collider[24];
        private static Material s_tracerMaterial;

        /// <summary>수면 높이. 바다 평면은 -0.9지만 함정 피벗·흘수선이 0이다.</summary>
        private const float SeaLevel = 0f;

        private float _speed;
        private float _damage;
        private float _age;
        private DamageSource _source;
        private string _statsKey;
        private TrailRenderer _trail;
        private float _maxTravel, _traveled;
        private float _splashMultiplier = 1f;

        public bool IsAlive => isActiveAndEnabled;

        /// <summary>쏜 쪽 진영(맞히는 레이어로 정한다). 같은 진영에는 피해를 주지 않는다. 알 수 없으면 Neutral(검사 안 함).</summary>
        public CombatFaction Owner { get; private set; }

        private void Awake()
        {
            Owner = Factions.OwnerFromHitMask(hitMask.value);
            if (tracerTime <= 0f) return;

            _trail = GetComponent<TrailRenderer>();
            if (_trail == null) _trail = gameObject.AddComponent<TrailRenderer>();
            if (s_tracerMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null) s_tracerMaterial = new Material(shader) { name = "Tracer (runtime)" };
            }

            _trail.sharedMaterial = s_tracerMaterial;
            _trail.time = tracerTime;
            _trail.minVertexDistance = 0.3f;
            _trail.widthCurve = AnimationCurve.Linear(0f, tracerWidth, 1f, 0f);
            _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _trail.receiveShadows = false;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(tracerColor, 0f), new GradientColorKey(tracerColor, 1f) },
                      new[] { new GradientAlphaKey(tracerColor.a, 0f), new GradientAlphaKey(0f, 1f) });
            _trail.colorGradient = g;
        }

        /// <summary>발사 직후 1회 설정한다. 방향은 포신 방향이어야 한다(탄 생성 위치 = Muzzle).</summary>
        public void Launch(Vector3 direction, float speed, float damage, DamageSource source)
        {
            transform.forward = direction.normalized;
            _speed = speed;
            _damage = damage;
            _source = source;
            _age = 0f;
            _splashMultiplier = 1f;

            if (_trail != null) _trail.Clear();
            _maxTravel = 0f;
            _traveled = 0f;
            _statsKey = CombatStats.KeyFor(gameObject);
            CombatStats.RecordFire(_statsKey);
        }

        /// <summary>
        /// 조준점 거리를 알려 준다(76mm). 이만큼 날아가도 맞지 않으면 빗나간 것으로 보고 그 아래 수면에 물기둥을 띄운다.
        /// </summary>
        public void SetMaxTravel(float distance)
        {
            _maxTravel = Mathf.Max(0f, distance);
            _traveled = 0f;
        }

        /// <summary>발사된 포탄 한 발의 파편 범위만 조정한다. 풀 재사용 시 Launch가 기본값으로 되돌린다.</summary>
        public void SetSplashMultiplier(float multiplier) => _splashMultiplier = Mathf.Max(0f, multiplier);

        private void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (_age >= lifeTime) { Despawn(); return; }

            float step = _speed * dt;
            // 빠른 탄이 얇은 대상을 통과하지 않도록 스윕 판정을 쓴다
            // 섬(Terrain)에도 막힌다 — 섬 뒤에 숨으면 함포를 피할 수 있다(양쪽 모두)
            if (Physics.SphereCast(transform.position, hitRadius, transform.forward,
                                   out var hit, step, hitMask | Game.World.Islands.Mask, QueryTriggerInteraction.Collide))
            {
                // 아군 몸체에 막히면 피해 없이 멈춘다(레이어로도 걸러지지만 진영으로 한 번 더 막는다)
                if (hit.collider.TryGetComponent<IDamageable>(out var target) && target.IsAlive && FactionAllows(target))
                {
                    target.TakeDamage(new DamageInfo(_damage, hit.point, transform.forward, _source, _statsKey));
                    // 미사일 요격은 Missile이 따로 센다(요격 = 명중 포함)
                    if (target is not Missile) CombatStats.RecordHit(_statsKey, hit.point);
                }

                if (splashRadius > 0f) ApplySplash(hit.point, hit.collider);
                PooledEffect.Spawn(impactEffect, hit.point, impactEffectScale);
                Despawn();
                return;
            }

            Vector3 next = transform.position + transform.forward * step;
            _traveled += step;

            if (waterEffect != null && _maxTravel > 0f && _traveled >= _maxTravel)
            {
                PooledEffect.Spawn(waterEffect, new Vector3(next.x, SeaLevel, next.z), waterEffectScale);
                Despawn();
                return;
            }

            // 빗나간 탄이 수면에 닿으면 물기둥을 띄우고 사라진다
            if (waterEffect != null && next.y <= SeaLevel && transform.forward.y < 0f)
            {
                float t = Mathf.InverseLerp(transform.position.y, next.y, SeaLevel);
                PooledEffect.Spawn(waterEffect, Vector3.Lerp(transform.position, next, t), waterEffectScale);
                Despawn();
                return;
            }

            transform.position = next;
        }

        /// <summary>직격한 표적을 뺀 주변 표적에게 파편 피해.</summary>
        private void ApplySplash(Vector3 center, Collider direct)
        {
            int n = Physics.OverlapSphereNonAlloc(center, splashRadius * _splashMultiplier, SplashHits, hitMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var col = SplashHits[i];
                if (col == null || col == direct) continue;
                if (col.TryGetComponent<IDamageable>(out var t) && t.IsAlive && FactionAllows(t))
                    t.TakeDamage(new DamageInfo(_damage * splashDamageRatio, center, transform.forward, _source, _statsKey));
            }
        }

        private bool FactionAllows(IDamageable target)
            => Owner == CombatFaction.Neutral || Factions.CanDamage(Owner, target);

        private void Despawn()
        {
            if (PoolManager.Instance != null) PoolManager.Instance.Despawn(gameObject);
            else gameObject.SetActive(false);
        }

        public void OnSpawned()
        {
            _age = 0f;
            if (_trail != null) _trail.Clear();
        }

        public void OnDespawned() { }
    }
}

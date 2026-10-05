using System.Collections.Generic;
using UnityEngine;
using Game.Core;

namespace Game.Combat
{
    /// <summary>
    /// 기만체. 수명 동안 주변 미사일의 목표를 자신으로 바꾸려 시도한다.
    /// 유도 미사일(TargetKind.Missile로 등록된 적 대함미사일)만 속는다 — 함포탄·보스 포탄·어뢰·드론에는 효과가 없다.
    ///
    /// 플레이어가 직접 타이밍을 잡아 쏘므로, 뿌린 뒤에 들어오는 미사일도 유인해야 한다.
    /// 그래서 짧은 간격으로 주변을 다시 살피되, 미사일마다 판정은 한 번만 한다
    /// (매번 굴리면 사실상 100%가 된다).
    ///
    /// 연출(2026-10-04, <see cref="Game.View.DecoyFx"/>): 발사 섬광·흰 연기 → 붉은 꼬리를 끄는 탄이 포물선으로 솟아
    /// burstAfter초 뒤 공중에서 터진다(하얀 섬광, 플레어 불똥이 사방으로 퍼져 떨어짐, 은박 반짝임) → flareSeconds초 동안 불타는 플레어가 떨어진다.
    /// 유인하는 자리는 탄 위치 그대로다(터진 뒤엔 바람에 흘러가며 천천히 내려온다). 프리팹의 구체 외형은 감춘다.
    /// </summary>
    public class Decoy : MonoBehaviour, IPoolable, ITargetable
    {
        [SerializeField] private float lifeTime = 7f;
        [SerializeField] private float lureRadius = 35f;
        [SerializeField, Range(0f, 1f)] private float lureChance = 0.9f;
        [SerializeField] private float driftSpeed = 3f;
        [SerializeField, Min(0.05f)] private float lureInterval = 0.25f;

        [Header("Launch & burst (연출)")]
        [Tooltip("발사 때 수평 속력(m/s)")]
        [SerializeField] private float launchSpeed = 15f;
        [Tooltip("발사 때 위로 솟는 속력(m/s). 중력으로 포물선을 그린다")]
        [SerializeField] private float launchClimb = 12f;
        [Tooltip("발사 뒤 공중에서 터지기까지(초)")]
        [SerializeField] private float burstAfter = 0.9f;
        [Tooltip("터진 뒤 플레어가 불타며 떨어지는 시간(초)")]
        [SerializeField] private float flareSeconds = 2.8f;
        [Tooltip("터진 뒤 내려오는 속력(m/s)")]
        [SerializeField] private float sinkSpeed = 0.6f;

        private float _age;
        private float _lureTimer;
        private Vector3 _drift;
        private Vector3 _velocity, _flat;
        private bool _burst;
        private float _burstAge, _fxDebt;
        private float _radiusMultiplier = 1f;
        private readonly HashSet<Missile> _tried = new();

        public Transform Transform => transform;
        public TargetKind Kind => TargetKind.Decoy;
        /// <summary>아군이 쏜 기만체(적 미사일이 노린다).</summary>
        public CombatFaction Faction => CombatFaction.Player;
        public bool IsAlive => isActiveAndEnabled;
        public bool IsRevealed => true;

        /// <summary>발사 방향으로 살짝 흘러가며 유인한다.</summary>
        public void Deploy(Vector3 direction, float radiusMultiplier = 1f)
        {
            _radiusMultiplier = Mathf.Max(0.1f, radiusMultiplier);
            _flat = new Vector3(direction.x, 0f, direction.z);
            _flat = _flat.sqrMagnitude > 1e-4f ? _flat.normalized : Vector3.forward;
            _drift = _flat * driftSpeed + Game.View.DecorFx.Wind * 0.3f + Vector3.down * sinkSpeed;
            _velocity = _flat * launchSpeed + Vector3.up * launchClimb;
            _burst = false;
            _fxDebt = 0f;
            _age = 0f;
            _lureTimer = 0f;
            Game.View.DecoyFx.Launch(transform.position, _velocity);
            TryLureNearbyMissiles();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (!_burst)
            {
                // 탄: 포물선으로 솟는다(붉은 꼬리)
                _velocity += Physics.gravity * dt;
                transform.position += _velocity * dt;
                Game.View.DecoyFx.Trail(transform.position, _velocity, dt, ref _fxDebt);
                if (_age >= burstAfter)
                {
                    _burst = true;
                    _burstAge = _age;
                    _fxDebt = 0f;
                    Game.View.DecoyFx.Burst(transform.position, _flat);
                }
            }
            else
            {
                // 터진 뒤: 바람에 흘러가며 천천히 내려온다(수면 위 1.5m에서 멈춤), 그동안 플레어가 불타며 떨어진다
                Vector3 p = transform.position + _drift * dt;
                p.y = Mathf.Max(1.5f, p.y);
                transform.position = p;
                float t = (_age - _burstAge) / Mathf.Max(0.1f, flareSeconds);
                if (t < 1f) Game.View.DecoyFx.Burning(p, t, dt, ref _fxDebt);
            }
            if (_age >= lifeTime) { Despawn(); return; }

            _lureTimer -= dt;
            if (_lureTimer > 0f) return;
            _lureTimer = lureInterval;
            TryLureNearbyMissiles();
        }

        private void TryLureNearbyMissiles()
        {
            var missiles = TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Missile);
            float sqr = lureRadius * lureRadius * _radiusMultiplier * _radiusMultiplier;

            for (int i = 0; i < missiles.Count; i++)
            {
                if (missiles[i] is not Missile m || !m.IsAlive || m.IsDecoyed) continue;
                if (_tried.Contains(m)) continue;
                if ((m.transform.position - transform.position).sqrMagnitude > sqr) continue;

                _tried.Add(m);
                if (Random.value <= lureChance) m.RedirectToDecoy(transform);
                else CombatLog.Add("기만체", $"{m.LogName} 유인 실패 (확률 {lureChance * 100f:0}%)");
            }
        }

        private void Despawn()
        {
            if (PoolManager.Instance != null) PoolManager.Instance.Despawn(gameObject);
            else gameObject.SetActive(false);
        }

        public void OnSpawned()
        {
            _age = 0f;
            _radiusMultiplier = 1f;
            _burst = false;
            _velocity = Vector3.zero;
            _tried.Clear();
            // 예전 프리팹의 흰 구체 외형은 감춘다 — 모습은 DecoyFx 파티클로 그린다
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            TargetRegistry.Register(this);
        }

        public void OnDespawned()
        {
            _tried.Clear();
            TargetRegistry.Unregister(this);
        }
    }
}

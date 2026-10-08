using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 곡사포(2026-10-08). 높은 포물선으로 쏘아 착탄 반경 안의 적 수상함 모두에 피해를 준다.
    ///   - 함교·레이더 같은 상부 구조물 너머로 쏜다: 사격 금지 구역(FireArc)이 없고 360° 돈다.
    ///     탄이 날아가는 길에 섬이 있어도 넘어간다(직사 포탄은 섬에 막힌다). 표적은 탐지된 것만.
    ///   - 착탄까지 1.4~3초 — 표적이 지금 진로를 유지하면 있을 곳(착탄 시각 기준)을 겨눈다. 급선회하면 빗나간다.
    ///   - 최소 사거리가 있다(높은 탄도). 항공기·잠항 잠수함은 노리지 않는다(드러난 잠수함은 맞는다).
    ///   - 주변에 적이 많이 몰린 표적을 먼저 친다 — 고속정 무리에 강하고 단일 표적 화력은 76mm보다 약하다.
    /// </summary>
    public class HowitzerModule : ModuleRuntime, IAmmoUser
    {
        [SerializeField] private GameObject shellPrefab;
        [SerializeField] private GameObject muzzleFlash;
        [Tooltip("포탑(좌우 선회)")]
        [SerializeField] private Transform turret;
        [Tooltip("포신(고각). 쏠 때 뒤로 밀린다.")]
        [SerializeField] private Transform barrel;
        [SerializeField] private Transform muzzle;

        [Header("Ballistics")]
        [Tooltip("착탄 반경(m)")]
        [SerializeField] private float blastRadius = 4f;
        [Tooltip("착탄 반경 가장자리 피해 비율")]
        [SerializeField, Range(0f, 1f)] private float edgeDamageRatio = 0.5f;
        [Tooltip("수평 탄속(m/s). 비행 시간 = 거리 / 이 값")]
        [SerializeField] private float horizontalSpeed = 15f;
        [SerializeField] private float minFlightTime = 1.4f;
        [Tooltip("탄도 최고점 = 거리 × 이 값")]
        [SerializeField] private float apexPerMeter = 0.32f;
        [Tooltip("이 각도 안으로 포탑이 돌면 쏜다")]
        [SerializeField] private float aimTolerance = 6f;

        [Header("Target Priority")]
        [SerializeField] private float clusterRadius = 6f;
        [SerializeField] private float clusterBonus = 0.8f;

        [Header("Feedback")]
        [SerializeField] private float recoilDistance = 0.22f;

        private readonly AmmoMagazine _ammo = new();
        private TargetingSystem _targeting;
        private System.Func<ITargetable, float, float> _score;
        private System.Predicate<Vector3> _inBand;
        private float _cooldown, _lastShotTime = -999f, _recoil;
        private Vector3 _barrelRest;

        public AmmoMagazine Ammo => _ammo;
        public bool IsEngaged => Time.time - _lastShotTime < 1.5f;
        public int Shots { get; private set; }
        public Vector3 LastImpact { get; private set; }
        public float BlastRadius => blastRadius;

        protected override void OnInitialized()
        {
            _targeting = GetComponentInParent<TargetingSystem>();
            if (shellPrefab == null) Debug.LogError("[Howitzer] shellPrefab 미할당.", this);
            _ammo.Configure(Stats);
            _ammo.Label = LogName;
            _score = Score;
            _inBand = InBand;
            if (barrel != null) _barrelRest = barrel.localPosition;
        }

        protected override void Tick(float dt)
        {
            _ammo.Tick(dt);
            if (_cooldown > 0f) _cooldown -= dt;
            Recoil(dt);
            if (_targeting == null || shellPrefab == null) return;

            var target = _targeting.GetBest(transform.position, Stats.Range, TargetClass.Surface | TargetClass.Submarine, _score, _inBand);
            if (target == null) return;

            Vector3 from = muzzle != null ? muzzle.position : transform.position;
            Vector3 impact = PredictImpact(target, from, out float flight);
            if (!InBand(impact)) return;

            if (!TurnTowards(impact, dt)) return;
            if (_cooldown > 0f || !_ammo.CanFire) return;

            _ammo.Consume();
            _cooldown = Mathf.Max(0.5f, Stats.ReloadTime);
            Fire(from, impact, flight);
        }

        /// <summary>표적이 진로를 유지하면 탄이 떨어질 때 있을 곳(두 번 고쳐 잡는다).</summary>
        private Vector3 PredictImpact(ITargetable target, Vector3 from, out float flight)
        {
            Vector3 pos = target.Transform.position;
            Vector3 vel = target is EnemyController e ? e.MoveVelocity : Vector3.zero;
            Vector3 aim = pos;
            flight = minFlightTime;
            for (int i = 0; i < 2; i++)
            {
                Vector3 d = aim - from;
                d.y = 0f;
                flight = Mathf.Max(minFlightTime, d.magnitude / Mathf.Max(1f, horizontalSpeed));
                aim = pos + vel * flight;
            }
            aim.y = 0f;
            return aim;
        }

        private void Fire(Vector3 from, Vector3 impact, float flight)
        {
            Vector3 flat = impact - from;
            flat.y = 0f;
            var go = PoolManager.Instance?.Spawn(shellPrefab, from, Quaternion.LookRotation(flat.sqrMagnitude > 0.01f ? flat.normalized : transform.forward));
            go?.GetComponent<ArtilleryShell>()?.LaunchArea(from, impact, flight, flat.magnitude * apexPerMeter,
                Stats.Damage, blastRadius, edgeDamageRatio,
                Definition != null ? Definition.TargetEfficiency : TargetEfficiency.Neutral);

            Shots++;
            LastImpact = impact;
            _lastShotTime = Time.time;
            _recoil = recoilDistance;
            if (muzzleFlash != null) PooledEffect.Spawn(muzzleFlash, from);
            AudioManager.Play(Game.Data.SfxId.NavalGunShot, from, 1f, 0.42f);   // 76mm보다 낮고 무거운 포성
            CombatLog.Add("곡사포", $"{LogName} 발사 — 착탄 {flight:0.0}초 뒤");
        }

        /// <summary>포탑을 착탄점 쪽으로 돌린다. 겨눠졌으면 true. 포탑이 없으면 언제나 true.</summary>
        private bool TurnTowards(Vector3 impact, float dt)
        {
            if (turret == null) return true;
            Vector3 d = impact - turret.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return true;
            var want = Quaternion.LookRotation(d.normalized, Vector3.up);
            turret.rotation = Quaternion.RotateTowards(turret.rotation, want, Mathf.Max(10f, Stats.TurretTurnRate) * dt);
            Vector3 f = turret.forward;
            f.y = 0f;
            return Vector3.Angle(f, d) <= aimTolerance;
        }

        private void Recoil(float dt)
        {
            if (barrel == null) return;
            _recoil = Mathf.MoveTowards(_recoil, 0f, recoilDistance * 2.5f * dt);
            barrel.localPosition = _barrelRest - Vector3.forward * _recoil;
        }

        /// <summary>최소~최대 사거리 안(수평 거리).</summary>
        private bool InBand(Vector3 p)
        {
            Vector3 d = p - transform.position;
            d.y = 0f;
            float min = Stats.MinRange, max = Stats.Range;
            return d.sqrMagnitude >= min * min && d.sqrMagnitude <= max * max;
        }

        /// <summary>가까울수록, 반경 안에 다른 수상 표적이 많을수록 낮은 점수(먼저 쏜다). 효율 0이면 쏘지 않는다.</summary>
        private float Score(ITargetable t, float sqrDistance)
        {
            float eff = EfficiencyAgainst(t);
            if (eff <= 0.001f) return float.MaxValue;
            int neighbours = 0;
            float r2 = clusterRadius * clusterRadius;
            var list = _targeting.DetectedSurface;
            for (int i = 0; i < list.Count; i++)
            {
                var o = list[i];
                if (o == null || o == t || !o.IsAlive || o.Transform == null) continue;
                if ((o.Transform.position - t.Transform.position).sqrMagnitude <= r2) neighbours++;
            }
            return Mathf.Sqrt(sqrDistance) / (1f + clusterBonus * neighbours) / eff;
        }
    }
}

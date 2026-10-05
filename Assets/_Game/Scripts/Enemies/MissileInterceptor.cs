using UnityEngine;
using Game.Combat;
using Game.Core;

namespace Game.Enemies
{
    /// <summary>
    /// 방공 프리깃의 근접방어포(2026-10-05, 스테이지 3). 같은 오브젝트의 적이 살아 있으면 interceptRange 안의
    /// 플레이어 유도탄(VLS·유도로켓·함대공 — <see cref="Missile.ActiveFriendly"/>)을 노려 예광탄을 점사한다.
    /// 예광탄(PRJ_EnemyAA)은 플레이어 미사일 레이어에만 맞으므로 실제로 맞혀야 떨어진다(항공전함 대공포와 같은 방식).
    /// 프리깃 근처의 다른 적을 노린 유도탄도 막는다 — 미사일 위주 함선이면 이 배를 함포로 먼저 잡는 편이 낫다.
    /// 포탑(RearCIWSPivot)은 쏠 쪽으로 돈다(장식).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MissileInterceptor : MonoBehaviour
    {
        [SerializeField] private GameObject tracerPrefab;
        [SerializeField] private Transform mount;
        [SerializeField] private Transform turret;
        [SerializeField] private float interceptRange = 28f;
        [SerializeField] private float projectileSpeed = 70f;
        [SerializeField] private float shotInterval = 0.08f;
        [SerializeField, Min(1)] private int burstRounds = 12;
        [SerializeField] private float cooldown = 1.2f;
        [SerializeField] private float spreadDegrees = 2.5f;
        [SerializeField] private float turretTurnRate = 360f;

        private EnemyController _owner;
        private float _timer;
        private int _rounds;
        private Quaternion _turretRest;
        private Vector3 _turretRestDir;
        private float _yaw;

        /// <summary>검증·표시용: 지금까지 쏜 예광탄 수.</summary>
        public int RoundsFired { get; private set; }

        private void Awake()
        {
            _owner = GetComponent<EnemyController>();
            _rounds = burstRounds;
            if (turret != null)
            {
                _turretRest = turret.localRotation;
                Vector3 d = mount != null ? transform.InverseTransformPoint(mount.position) - transform.InverseTransformPoint(turret.position) : Vector3.forward;
                d.y = 0f;
                _turretRestDir = d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;
            }
        }

        private void OnEnable()
        {
            _rounds = burstRounds;
            _timer = 0f;
            RoundsFired = 0;
        }

        private void Update()
        {
            if (_owner == null || !_owner.IsAlive || tracerPrefab == null) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;
            float dt = Time.deltaTime;
            var origin = mount != null ? mount : transform;

            Missile target = null;
            float best = interceptRange * interceptRange;
            var list = Missile.ActiveFriendly;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (m == null || !m.IsAlive) continue;
                float d = (m.transform.position - origin.position).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                target = m;
            }

            if (target != null) TurnTurret(target.transform.position, dt);

            _timer -= dt;
            if (_timer > 0f) return;
            if (target == null)
            {
                if (_rounds < burstRounds) { _rounds++; _timer = cooldown / burstRounds; }
                return;
            }
            if (_rounds <= 0)
            {
                _rounds = burstRounds;
                _timer = cooldown;
                return;
            }

            Vector3 aim = Ballistics.PredictIntercept(origin.position, target.transform.position, target.Velocity, projectileSpeed);
            Vector3 dir = (aim - origin.position).normalized;
            dir = Quaternion.Euler(Random.Range(-spreadDegrees, spreadDegrees), Random.Range(-spreadDegrees, spreadDegrees), 0f) * dir;
            var go = PoolManager.Instance?.Spawn(tracerPrefab, origin.position, Quaternion.LookRotation(dir));
            go?.GetComponent<Projectile>()?.Launch(dir, projectileSpeed, 1f, DamageSource.Gun);
            if (_rounds == burstRounds) AudioManager.Play(Game.Data.SfxId.CiwsBurst, origin.position, 0.45f, 1.1f);
            _rounds--;
            RoundsFired++;
            _timer = shotInterval;
        }

        private void TurnTurret(Vector3 worldPoint, float dt)
        {
            if (turret == null) return;
            Vector3 to = transform.InverseTransformPoint(worldPoint) - transform.InverseTransformPoint(turret.position);
            to.y = 0f;
            if (to.sqrMagnitude < 1e-4f) return;
            float want = Vector3.SignedAngle(_turretRestDir, to.normalized, Vector3.up);
            _yaw = Mathf.MoveTowardsAngle(_yaw, want, turretTurnRate * dt);
            Vector3 axis = turret.parent != null ? turret.parent.InverseTransformDirection(transform.up) : Vector3.up;
            turret.localRotation = Quaternion.AngleAxis(_yaw, axis) * _turretRest;
        }
    }
}

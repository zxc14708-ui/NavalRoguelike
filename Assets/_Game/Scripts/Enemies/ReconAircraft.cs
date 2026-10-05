using UnityEngine;
using Game.Core;

namespace Game.Enemies
{
    /// <summary>
    /// 정찰기. 직접 공격하지 않고, 높은 고도에서 넓게 선회하며 아군 함대에 표적을 지시한다.
    /// 한 대라도 살아 있으면 모든 적의 공격 재장전이 빨라진다(SpottedAttackRate).
    ///
    /// 기관포 사거리 밖을 돌기 때문에 76mm·함대공 미사일로 떨어뜨려야 한다 — 우선 제거 표적이다.
    /// </summary>
    public class ReconAircraft : AirEnemy
    {
        /// <summary>정찰기가 표적을 지시하는 동안 적 공격 재장전 속도 배수.</summary>
        public const float SpottedAttackRate = 1.35f;

        /// <summary>살아 있는 정찰기 수.</summary>
        public static int ActiveCount { get; private set; }

        [Tooltip("돌아가는 레이돔(선택)")]
        [SerializeField] private Transform radome;
        [SerializeField] private float radomeRpm = 12f;
        [Tooltip("쌍발 프로펠러(선택, 미니어처 적 v9)")]
        [SerializeField] private Transform[] propellers;
        [SerializeField] private float propellerRpm = 1500f;

        private float _side = 1f;
        private bool _counted;

        public override void Setup(Game.Data.EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            _side = Random.value < 0.5f ? -1f : 1f;
        }

        /// <summary>정찰기는 기함을 지시한다(호위함을 쫓지 않는다).</summary>
        protected override bool TargetsEscorts => false;

        protected override void UpdateBehaviour(float dt)
        {
            // PreferredRange = 선회 반경. 멀면 다가오고, 반경 근처에서는 접선으로 돈다.
            Vector3 toPlayer = Player.position - transform.position;
            toPlayer.y = 0f;
            float dist = Mathf.Max(toPlayer.magnitude, 0.01f);
            Vector3 radial = toPlayer / dist;

            float radius = Definition.PreferredRange;
            Vector3 tangent = Vector3.Cross(Vector3.up, radial) * _side;
            float correction = Mathf.Clamp((dist - radius) / radius * 2f, -1f, 1f);
            Vector3 heading = (tangent * (1f - Mathf.Abs(correction) * 0.5f) + radial * correction).normalized;

            Vector3 point = transform.position + heading * 12f;
            point.y = cruiseAltitude;
            Fly(point, Definition.MoveSpeed, Definition.TurnRateDegPerSec, false, dt);

            if (radome != null) radome.Rotate(Vector3.up, radomeRpm * 6f * dt, Space.World);
            if (propellers != null)
                foreach (var p in propellers)
                    if (p != null) p.Rotate(transform.forward, propellerRpm * 6f * dt, Space.World);
        }

        public override void OnSpawned()
        {
            base.OnSpawned();
            if (_counted) return;
            _counted = true;
            ActiveCount++;
            GameEvents.RaiseReconChanged(ActiveCount);
        }

        public override void OnDespawned()
        {
            base.OnDespawned();
            if (!_counted) return;
            _counted = false;
            ActiveCount = Mathf.Max(0, ActiveCount - 1);
            GameEvents.RaiseReconChanged(ActiveCount);
        }

        /// <summary>플레이 모드를 다시 시작하면 정적 카운트를 비운다(도메인 리로드를 끈 에디터 대비).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ActiveCount = 0;
    }
}

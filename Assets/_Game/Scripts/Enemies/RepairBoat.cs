using UnityEngine;
using Game.Combat;
using Game.Data;

namespace Game.Enemies
{
    /// <summary>
    /// 수리 지원정(일반 수상 적). 공격하지 않고, 다친 아군 수상함 곁으로 가서 크레인 빔으로 선체를 고친다.
    /// 놔두면 엘리트·포격정이 계속 살아나므로 먼저 잡아야 하는 표적이다.
    ///
    ///   - 대상: 반경 seekRange 안에서 체력 비율이 가장 낮은(maxHealFraction 미만) 아군 수상함. 보스·기뢰·다른 수리정은 고치지 않는다.
    ///   - 이동: 대상 곁(플레이어 반대편 escortDistance)에 붙는다. 플레이어가 keepAway보다 가까우면 먼저 물러난다.
    ///     대상이 없으면 가장 가까운 아군 뒤를 따르고, 아군도 없으면 플레이어를 멀리서 돈다.
    ///   - 수리: 빔 기점(RepairBeamOrigin)에서 beamRange 안이면 초당 repairPerSecond. 초록 빔과 크레인 회전으로 보인다.
    /// </summary>
    public class RepairBoat : EnemyController
    {
        [Header("Repair")]
        [SerializeField] private float repairPerSecond = 4f;
        [SerializeField] private float beamRange = 16f;
        [SerializeField] private float seekRange = 70f;
        [SerializeField, Range(0f, 1f)] private float maxHealFraction = 0.97f;
        [SerializeField] private float escortDistance = 7f;
        [SerializeField] private float keepAway = 20f;

        [Header("Visual")]
        [SerializeField] private Transform beamOrigin;
        [SerializeField] private Transform cranePivot;
        [SerializeField] private LineRenderer beam;
        [SerializeField] private float craneTurnRate = 90f;

        private EnemyController _patient;
        private float _retargetAt;
        private Quaternion _craneRest;   // 배 기준 크레인 쉬는 자세
        private Vector3 _craneRestDir;   // 배 기준 붐(빔 기점) 수평 방향
        private bool _craneReady;

        /// <summary>검증·사전용: 지금 고치는 중인 적(없으면 null)과 지금까지 고친 총량.</summary>
        public EnemyController Patient => _patient;
        public float TotalRepaired { get; private set; }

        public override void Setup(EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            _patient = null;
            _retargetAt = 0f;
            TotalRepaired = 0f;
            if (beam != null) beam.enabled = false;
            if (cranePivot != null && !_craneReady)
            {
                // 처음 한 번(쉬는 자세일 때) 배 기준 자세와 붐 방향을 기억한다 — 모델 축과 상관없이 배의 위축으로 돌리기 위해
                _craneRest = Quaternion.Inverse(transform.rotation) * cranePivot.rotation;
                Vector3 boom = beamOrigin != null ? transform.InverseTransformDirection(beamOrigin.position - cranePivot.position) : Vector3.forward;
                boom.y = 0f;
                _craneRestDir = boom.sqrMagnitude > 1e-4f ? boom.normalized : Vector3.forward;
                _craneReady = true;
            }
        }

        protected override void UpdateBehaviour(float dt)
        {
            if (Time.time >= _retargetAt || _patient == null || !_patient.IsAlive)
            {
                _patient = FindPatient();
                _retargetAt = Time.time + 1f;
            }

            Vector3 self = transform.position;
            Vector3 fromPlayer = self - Player.position; fromPlayer.y = 0f;
            if (fromPlayer.magnitude < keepAway)
                Steer(fromPlayer, 1f, dt);                       // 너무 가까우면 먼저 물러난다
            else if (_patient != null)
            {
                // 환자 곁, 플레이어 반대편에 붙는다
                Vector3 away = _patient.transform.position - Player.position; away.y = 0f;
                Vector3 spot = _patient.transform.position + (away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward) * escortDistance;
                Vector3 to = spot - self; to.y = 0f;
                Steer(to, Mathf.Clamp01(to.magnitude / 12f + 0.15f), dt);
            }
            else
                OrbitPlayer(Mathf.Max(Definition.PreferredRange, keepAway + 10f), 0.7f, dt);

            TickRepair(dt);
        }

        private EnemyController FindPatient()
        {
            EnemyController best = null;
            float bestFraction = maxHealFraction;
            var list = TargetRegistry.Of(CombatFaction.Hostile, TargetKind.Surface);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is not EnemyController e || e == this || !e.IsAlive || e.Definition == null) continue;
                if (e.Definition.Rank == EnemyRank.Boss || e is RepairBoat || e is SeaMine) continue;
                if ((e.transform.position - transform.position).sqrMagnitude > seekRange * seekRange) continue;
                float fraction = e.CurrentHp / Mathf.Max(1f, e.Definition.MaxHp);
                if (fraction < bestFraction) { bestFraction = fraction; best = e; }
            }
            return best;
        }

        private void TickRepair(float dt)
        {
            Vector3 origin = beamOrigin != null ? beamOrigin.position : transform.position + Vector3.up * 1.5f;
            bool active = _patient != null && _patient.IsAlive &&
                          (_patient.transform.position - origin).sqrMagnitude <= beamRange * beamRange &&
                          _patient.CurrentHp < _patient.Definition.MaxHp;
            if (active) TotalRepaired += _patient.Repair(repairPerSecond * dt);

            Vector3 hit = active ? _patient.transform.position + Vector3.up * 1f : origin;
            if (beam != null)
            {
                beam.enabled = active;
                if (active)
                {
                    beam.SetPosition(0, origin);
                    beam.SetPosition(1, hit);
                    float pulse = 0.12f + 0.05f * Mathf.Sin(Time.time * 12f);
                    beam.startWidth = pulse;
                    beam.endWidth = pulse * 0.6f;
                }
            }

            // 크레인은 환자 쪽으로 돈다(없으면 쉬는 자세로). 배의 위축 기준 회전.
            if (cranePivot != null && _craneReady)
            {
                float yaw = 0f;
                if (active)
                {
                    Vector3 to = transform.InverseTransformDirection(hit - cranePivot.position);
                    to.y = 0f;
                    if (to.sqrMagnitude > 0.01f) yaw = Vector3.SignedAngle(_craneRestDir, to, Vector3.up);
                }
                Quaternion want = transform.rotation * Quaternion.Euler(0f, yaw, 0f) * _craneRest;
                cranePivot.rotation = Quaternion.RotateTowards(cranePivot.rotation, want, craneTurnRate * Time.deltaTime);
            }
        }

        public override void OnDespawned()
        {
            base.OnDespawned();
            _patient = null;
            if (beam != null) beam.enabled = false;
        }
    }
}

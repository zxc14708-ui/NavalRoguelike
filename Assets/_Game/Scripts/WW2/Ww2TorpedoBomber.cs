using UnityEngine;
using Game.Core;
using Game.Combat;

namespace Game.WW2
{
    /// <summary>
    /// 뇌격기(WWII 패치 B, ww2_enemy_torpedo_bomber). 기함의 현측으로 돌아 들어가 낮게 깔려 직진 어뢰 한 발을 떨어뜨린다.
    ///   1 접근: 순항 고도로 빠르게 기함 한쪽 현측 바깥(진입 거리 = 진입 속력 × 진입 시간 + 투하 거리)으로 간다.
    ///   2 확정: 그 순간 "기함이 지금 침로·속력을 유지하면 어뢰와 만나는 점"을 향해 방향을 고정하고, 낮고 느리게(진입 속력) 곧게 난다.
    ///      수면에 어뢰가 떨어질 지점(원)과 그 뒤로 이어질 어뢰 경로(주황 선)를 띄운다 — <see cref="runInTime"/>초 뒤 투하.
    ///   3 투하: 고정한 방향으로 어뢰(Combat.Torpedo — 유도 없음)를 떨어뜨린다. 투하 전에 격추하면 어뢰는 없다.
    ///   4 이탈: 그대로 빠져나가 떠난다.
    /// 피하는 법: 예고가 뜨면 전령기를 바꾸거나(만나는 시각이 어긋남) 어뢰 경로와 나란히 되도록 돈다(맞는 면적이 줄어듦).
    /// </summary>
    public class Ww2TorpedoBomber : Ww2StrikeAircraft
    {
        [Header("Torpedo Run")]
        [SerializeField] private GameObject torpedoPrefab;
        [SerializeField] private float runInAltitude = 3f;
        [Tooltip("방향 확정부터 투하까지(예고 시간)")]
        [SerializeField] private float runInTime = 3.6f;
        [Tooltip("진입 속력(m/s). 어뢰를 떨어뜨리려면 낮고 느리게 곧게 날아야 한다 — 대공포가 잡을 기회. 접근은 데이터의 속력(빠름)")]
        [SerializeField] private float runInSpeed = 10f;
        [Tooltip("투하 지점에서 기함 예상 위치까지")]
        [SerializeField] private float dropRange = 16f;
        [Tooltip("이 시간이 지나도 진입 지점에 못 가면 그 자리에서 확정")]
        [SerializeField] private float approachTimeout = 16f;
        [SerializeField] private float torpedoSpeed = 16f;

        private float _side = 1f;
        private Vector3 _meet;
        private LineRenderer _path, _drop, _hit;

        private static readonly Color Warn = new(1f, 0.55f, 0.15f, 0.9f);

        public override string StrikeName => Ww2BattleLog.TorpedoBomber;
        /// <summary>어뢰가 기함 예상 침로와 만나는 곳(지금 침로·속력이면 맞는 곳).</summary>
        public override Vector3 DangerPoint => _meet;

        protected override void OnStrikeSetup()
        {
            _side = Random.value < 0.5f ? -1f : 1f;
            if (torpedoPrefab != null && torpedoPrefab.TryGetComponent<Torpedo>(out var t)) torpedoSpeed = t.Speed;
        }

        private float RunInDistance => RunInSpeed * runInTime + dropRange;
        /// <summary>진입 중 수평 속력(일정 — 예고한 투하 지점에 정확히 닿는다).</summary>
        private float RunInSpeed => Mathf.Max(4f, runInSpeed);

        protected override void UpdateApproach(float dt)
        {
            Vector3 beam = Player.right * _side;
            beam.y = 0f;
            Vector3 setup = Player.position + beam.normalized * RunInDistance + PlayerVelocity * runInTime;
            setup.y = cruiseAltitude;
            Fly(setup, Definition.MoveSpeed, Definition.TurnRateDegPerSec, false, dt);

            Vector3 flat = setup - transform.position;
            flat.y = 0f;
            if (PlayerConcealed) return;   // 연막: 조준할 수 없어 맴돈다
            if (flat.magnitude > 9f && PhaseTime < approachTimeout) return;

            // 확정: "진입해 투하 → 어뢰가 달려 만나는 시각"에 기함이 있을 곳을 향한다(몇 번 되풀이해 맞춘다)
            Vector3 pos = transform.position; pos.y = 0f;
            Vector3 p0 = Player.position; p0.y = 0f;
            Vector3 v = PlayerVelocity;
            float runDistance = RunInSpeed * runInTime;
            Vector3 meet = p0, heading = p0 - pos, drop = pos;
            for (int i = 0; i < 5; i++)
            {
                heading = meet - pos; heading.y = 0f;
                if (heading.sqrMagnitude < 1e-4f) heading = transform.forward;
                drop = pos + heading.normalized * runDistance;
                float run = Vector3.Distance(drop, meet) / Mathf.Max(1f, torpedoSpeed);
                meet = p0 + v * (runInTime + run);
            }
            _meet = meet;
            Commit(runInTime, drop, heading);
            ShowTelegraph();
        }

        protected override void UpdateCommitted(float dt)
        {
            if (dt <= 0f) return;   // 시간이 멈춤(일시정지·정비)
            // 방향은 고정 — 고도만 내린다. 수평 속력을 일정하게 두어 예고한 투하 지점에 정확히 닿는다
            Vector3 p = transform.position;
            float y = Mathf.MoveTowards(p.y, runInAltitude, 3f * dt);
            Vector3 next = p + CommittedHeading * RunInSpeed * dt;
            next.y = y;
            transform.rotation = Quaternion.LookRotation((next - p).normalized, Vector3.up);
            SetVelocity((next - p) / dt);
            transform.position = next;
            UpdateTelegraph();
            if (Time.time < ReleaseAt) return;

            p = new Vector3(StrikePt.x, transform.position.y, StrikePt.z);   // 예고한 투하 지점에서
            if (torpedoPrefab != null)
            {
                var go = PoolManager.Instance?.Spawn(torpedoPrefab, new Vector3(p.x, 0f, p.z), Quaternion.LookRotation(CommittedHeading));
                go?.GetComponent<Torpedo>()?.Launch(p + CommittedHeading * 60f, Definition.AttackDamage, Ww2BattleLog.TorpedoBomber);
            }
            AudioManager.Play(Game.Data.SfxId.MissileLaunch, p, 0.5f, 0.7f);
            MarkReleased();
        }

        // ------------------------------------------------------------ 예고(수면)

        private void ShowTelegraph()
        {
            if (_path == null) _path = Ww2Marks.Line(null, "WW2 torpedo bomber run", Warn, 0.55f);
            if (_drop == null) _drop = Ww2Marks.Line(null, "WW2 torpedo bomber drop", Warn, 0.35f, true);
            if (_hit == null) _hit = Ww2Marks.Line(null, "WW2 torpedo bomber meet", Warn, 0.45f, true);
            // 투하 지점부터 기함 예상 침로와 만나는 곳을 지나 더 멀리까지(어뢰 경로) + 만나는 곳 원
            float past = Vector3.Distance(StrikePt, _meet) + 30f;
            Ww2Marks.Segment(_path, StrikePt, StrikePt + CommittedHeading * Mathf.Max(past, dropRange * 2.6f));
            Ww2Marks.Ring(_drop, StrikePt, 1.6f, 24);
            Ww2Marks.Ring(_hit, _meet, 3.5f, 32);
            _path.enabled = _drop.enabled = _hit.enabled = true;
        }

        private void UpdateTelegraph()
        {
            if (_path == null) return;
            float left01 = Mathf.Clamp01(SecondsToRelease / Mathf.Max(0.1f, runInTime));
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * Mathf.Lerp(18f, 6f, left01));
            var c = new Color(Warn.r, Mathf.Lerp(0.2f, Warn.g, left01), Warn.b, Warn.a * pulse);
            Ww2Marks.SetColor(_path, c);
            Ww2Marks.SetColor(_drop, c);
            if (_hit != null) Ww2Marks.SetColor(_hit, c);
        }

        protected override void HideTelegraph()
        {
            if (_path != null) _path.enabled = false;
            if (_drop != null) _drop.enabled = false;
            if (_hit != null) _hit.enabled = false;
        }

        private void OnDestroy()
        {
            if (_path != null) Destroy(_path.gameObject);
            if (_drop != null) Destroy(_drop.gameObject);
            if (_hit != null) Destroy(_hit.gameObject);
        }
    }
}

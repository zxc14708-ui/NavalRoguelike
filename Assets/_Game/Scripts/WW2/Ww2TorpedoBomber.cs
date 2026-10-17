using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;

namespace Game.WW2
{
    /// <summary>
    /// 뇌격기(WWII 패치 B, ww2_enemy_torpedo_bomber). 기함의 현측으로 돌아 들어가 낮게 깔려 직진 어뢰 한 발을 떨어뜨린다.
    ///   1 접근: 순항 고도로 빠르게 기함 한쪽 현측 바깥(진입 거리 = 진입 속력 × 진입 시간 + 투하 거리)으로 간다.
    ///   2 확정: 그 순간 "기함이 지금 속력·<b>선회</b>를 유지하면 어뢰와 만나는 점"(Ww2Aim)을 향해 방향을 고정하고, 낮고 느리게(진입 속력) 곧게 난다.
    ///      수면에 어뢰가 떨어질 지점(원)·어뢰 경로(주황 선)·내 침로와 만나는 곳(원)을 띄운다 — <see cref="runInTime"/>초 뒤 투하.
    ///   3 투하: 고정한 방향으로 어뢰(Combat.Torpedo — 유도 없음)를 떨어뜨린다. 투하 전에 격추하면 어뢰는 없다.
    ///   4 이탈: 그대로 빠져나가 떠난다.
    /// <b>3기 편대</b>(2026-10-17 플레이 피드백, EnemyDefinition.GroupSize 3): 편대장 뒤 좌우로 비스듬히 따라붙어 함께 날고,
    /// 편대장이 확정하면 같은 순간 함께 확정해 <b>어뢰 부채</b>를 만든다 — 편대장은 예상 위치, 두 번째는 그 앞(속력을 올리면 있을 곳),
    /// 세 번째는 그 뒤(속력을 줄이면 있을 곳)를 노린다. 속력만 바꿔서는 다 피하기 어렵고 어뢰와 나란히 돌아야 한다.
    /// 편대장이 떨어지면 남은 기체는 각자 공격한다.
    /// </summary>
    public class Ww2TorpedoBomber : Ww2StrikeAircraft, IFormationMember
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

        [Header("Formation")]
        [Tooltip("편대 자리 간격(m) — 편대장 뒤 좌우로 비스듬히")]
        [SerializeField] private float formationSpacing = 7f;
        [Tooltip("편대 어뢰 부채: 기체마다 노리는 곳을 기함 예상 침로를 따라 이만큼씩 앞뒤로 벌린다(m)")]
        [SerializeField] private float spreadAlongTrack = 10f;

        private float _side = 1f;
        private Vector3 _meet;
        private LineRenderer _path, _drop, _hit;
        private Ww2TorpedoBomber _leader;
        private int _slot;
        private readonly List<Ww2TorpedoBomber> _wingmen = new();

        private static readonly Color Warn = new(1f, 0.55f, 0.15f, 0.9f);

        public override string StrikeName => Ww2BattleLog.TorpedoBomber;
        /// <summary>어뢰가 기함 예상 침로와 만나는 곳(지금 속력·선회를 유지하면 맞는 곳).</summary>
        public override Vector3 DangerPoint => _meet;
        /// <summary>편대장(없으면 null — 편대장이거나 혼자).</summary>
        public Ww2TorpedoBomber Leader => _leader;
        public int Slot => _slot;
        public IReadOnlyList<Ww2TorpedoBomber> Wingmen => _wingmen;

        protected override void OnStrikeSetup()
        {
            _side = Random.value < 0.5f ? -1f : 1f;
            _leader = null;
            _slot = 0;
            _wingmen.Clear();
            if (torpedoPrefab != null && torpedoPrefab.TryGetComponent<Torpedo>(out var t)) torpedoSpeed = t.Speed;
        }

        public void JoinFormation(EnemyController leader, int slot)
        {
            if (leader is not Ww2TorpedoBomber lead || lead == this) return;
            _leader = lead;
            _slot = slot;
            _side = lead._side;
            if (!lead._wingmen.Contains(this)) lead._wingmen.Add(this);
        }

        private float RunInDistance => RunInSpeed * runInTime + dropRange;
        /// <summary>진입 중 수평 속력(일정 — 예고한 투하 지점에 정확히 닿는다).</summary>
        private float RunInSpeed => Mathf.Max(4f, runInSpeed);

        private bool FollowingLeader => _leader != null && _leader.IsAlive && _leader.State == Phase.Approach;

        protected override void UpdateApproach(float dt)
        {
            if (FollowingLeader)
            {
                // 편대 자리: 편대장 뒤 좌우로 비스듬히(같은 고도) — 편대장이 확정하면 함께 확정한다
                var lt = _leader.transform;
                Vector3 lf = lt.forward; lf.y = 0f; lf = lf.sqrMagnitude > 1e-4f ? lf.normalized : Vector3.forward;
                Vector3 lr = Vector3.Cross(Vector3.up, lf);
                int rank = (_slot + 1) / 2;
                float side = _slot % 2 == 1 ? 1f : -1f;
                Vector3 spot = lt.position + lr * side * formationSpacing * rank - lf * formationSpacing * rank;
                spot.y = lt.position.y;
                float gap = Vector3.Distance(spot, transform.position);
                Fly(spot, Definition.MoveSpeed * Mathf.Lerp(0.95f, 1.3f, Mathf.Clamp01(gap / 15f)), Definition.TurnRateDegPerSec * 1.5f, gap < 6f, dt);
                return;
            }
            if (_leader != null) _leader = null;   // 편대장이 떨어졌거나 먼저 확정했다 — 이제 혼자

            Vector3 beam = Player.right * _side;
            beam.y = 0f;
            Vector3 setup = Ww2Aim.Predict(Player, PlayerShip, runInTime) + beam.normalized * RunInDistance;
            setup.y = cruiseAltitude;
            Fly(setup, Definition.MoveSpeed, Definition.TurnRateDegPerSec, false, dt);

            Vector3 flat = setup - transform.position;
            flat.y = 0f;
            if (PlayerConcealed) return;   // 연막: 조준할 수 없어 맴돈다
            if (flat.magnitude > 9f && PhaseTime < approachTimeout) return;

            // 확정: "진입해 투하 → 어뢰가 달려 만나는 시각"에 기함이 있을 곳(속력·선회 유지)을 향한다(몇 번 되풀이해 맞춘다)
            Vector3 meet = SolveMeet(transform.position, out float total);
            CommitRun(meet);

            // 편대: 같은 순간 함께 확정 — 예상 침로를 따라 앞뒤로 벌린 곳을 노려 어뢰 부채를 만든다
            Vector3 track = Ww2Aim.PredictHeading(Player, PlayerShip, total);
            foreach (var w in _wingmen)
            {
                if (w == null || !w.IsAlive || w.State != Phase.Approach || w._leader != this) continue;
                int rank = (w._slot + 1) / 2;
                float sign = w._slot % 2 == 1 ? 1f : -1f;
                w.CommitRun(meet + track * sign * spreadAlongTrack * rank);
            }
        }

        /// <summary>from에서 진입해 투하한 어뢰가 기함과 만나는 곳(되풀이로 맞춤). total = 확정부터 만날 때까지 초.</summary>
        private Vector3 SolveMeet(Vector3 from, out float total)
        {
            Vector3 pos = from; pos.y = 0f;
            Vector3 meet = Ww2Aim.Predict(Player, PlayerShip, 0f);
            float runDistance = RunInSpeed * runInTime;
            total = runInTime;
            for (int i = 0; i < 5; i++)
            {
                Vector3 heading = meet - pos; heading.y = 0f;
                if (heading.sqrMagnitude < 1e-4f) heading = transform.forward;
                Vector3 drop = pos + heading.normalized * runDistance;
                total = runInTime + Vector3.Distance(drop, meet) / Mathf.Max(1f, torpedoSpeed);
                meet = Ww2Aim.Predict(Player, PlayerShip, total);
            }
            return meet;
        }

        /// <summary>meet을 향해 진입 방향을 고정하고 예고를 띄운다.</summary>
        private void CommitRun(Vector3 meet)
        {
            Vector3 pos = transform.position; pos.y = 0f;
            Vector3 heading = meet - pos; heading.y = 0f;
            if (heading.sqrMagnitude < 1e-4f) heading = transform.forward;
            Vector3 drop = pos + heading.normalized * RunInSpeed * runInTime;
            _meet = new Vector3(meet.x, 0f, meet.z);
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

        public override void OnDespawned()
        {
            if (_leader != null) _leader._wingmen.Remove(this);
            foreach (var w in _wingmen) if (w != null && w._leader == this) w._leader = null;
            _wingmen.Clear();
            _leader = null;
            base.OnDespawned();
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

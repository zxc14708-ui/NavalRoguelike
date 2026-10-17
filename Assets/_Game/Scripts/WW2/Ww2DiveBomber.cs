using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;

namespace Game.WW2
{
    /// <summary>
    /// 급강하폭격기(WWII 패치 B, ww2_enemy_dive_bomber). 높이 와서 기함 위에서 급강하해 폭탄 한 발을 떨어뜨린다.
    ///   1 접근: 높은 고도로 기함에게 다가간다.
    ///   2 확정: 수평 거리 <see cref="commitDistance"/> 안에 들면 "기함이 지금 침로·속력을 유지하면 폭탄이 떨어질 때 있을 곳"을
    ///      착탄점으로 고정하고 수면에 붉은 원(반경 <see cref="blastRadius"/>)을 띄운 뒤 그 점을 향해 급강하한다.
    ///   3 투하: <see cref="diveTime"/>초 뒤 낮은 고도에서 폭탄을 놓는다(<see cref="bombFlightTime"/>초 뒤 착탄). 원 안의 선체만 맞는다.
    ///      강하 중에 격추하면 폭탄은 없다(원도 사라진다).
    ///   4 이탈: 기수를 들어 빠져나가 떠난다.
    /// 피하는 법: 원이 뜨면 전령기·타로 원 밖으로 빠진다. 대공포는 강하하는 동안 가까이 내려온 기체를 잡을 기회다.
    /// 예측은 지금 속력과 <b>선회</b>를 유지한다고 본다(Ww2Aim) — 돌고 있던 그대로 돌면 맞는다.
    /// <b>2기 편대</b>(GroupSize 2): 두 번째는 편대장 뒤를 따라오다 편대장이 확정하면 <see cref="wingmanDelay"/>초 늦게,
    /// 예상 침로를 따라 <see cref="wingmanLead"/>m 앞(속력을 올려 빠져나가면 있을 곳)에 떨어뜨린다.
    /// </summary>
    public class Ww2DiveBomber : Ww2StrikeAircraft, IFormationMember
    {
        [Header("Dive")]
        [SerializeField] private GameObject bombPrefab;
        [SerializeField] private GameObject warningMarker;
        [SerializeField] private float commitDistance = 26f;
        [SerializeField] private float diveTime = 2.3f;
        [SerializeField] private float releaseAltitude = 5f;
        [SerializeField] private float bombFlightTime = 0.6f;
        [SerializeField] private float blastRadius = 4f;

        [Header("Formation")]
        [SerializeField] private float wingmanDelay = 0.7f;
        [SerializeField] private float wingmanLead = 7f;

        private Ww2DiveBomber _leader, _wingman;
        private Vector3 _diveStart, _releasePos;
        private GameObject _marker;
        private Collider _playerHull;

        public override string StrikeName => Ww2BattleLog.DiveBomber;

        public Ww2DiveBomber Leader => _leader;

        protected override void OnStrikeSetup()
        {
            _playerHull = Player != null ? Player.GetComponent<Collider>() : null;
            _leader = _wingman = null;
        }

        public void JoinFormation(EnemyController leader, int slot)
        {
            if (leader is not Ww2DiveBomber lead || lead == this || lead._wingman != null) return;
            _leader = lead;
            lead._wingman = this;
        }

        protected override void UpdateApproach(float dt)
        {
            if (_leader != null && _leader.IsAlive && _leader.State == Phase.Approach)
            {
                // 편대장 뒤 오른쪽 비스듬히
                var lt = _leader.transform;
                Vector3 spot = lt.position + lt.right * 6f - lt.forward * 6f;
                float gap = Vector3.Distance(spot, transform.position);
                Fly(spot, Definition.MoveSpeed * Mathf.Lerp(0.95f, 1.3f, Mathf.Clamp01(gap / 15f)), Definition.TurnRateDegPerSec * 1.5f, gap < 6f, dt);
                return;
            }
            _leader = null;

            Vector3 over = Ww2Aim.Predict(Player, PlayerShip, 1.5f);
            over.y = cruiseAltitude;
            Fly(over, Definition.MoveSpeed, Definition.TurnRateDegPerSec, false, dt);

            Vector3 flat = Player.position - transform.position;
            flat.y = 0f;
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (PlayerConcealed || flat.magnitude > commitDistance || Vector3.Angle(fwd, flat) > 55f) return;

            float t = diveTime + bombFlightTime;
            BeginDive(Ww2Aim.Predict(Player, PlayerShip, t), diveTime);
            // 편대: 조금 늦게, 예상 침로를 따라 조금 앞에
            if (_wingman != null && _wingman.IsAlive && _wingman.State == Phase.Approach && _wingman._leader == this)
            {
                float t2 = t + wingmanDelay;
                Vector3 aim2 = Ww2Aim.Predict(Player, PlayerShip, t2) + Ww2Aim.PredictHeading(Player, PlayerShip, t2) * wingmanLead;
                _wingman.BeginDive(aim2, diveTime + wingmanDelay);
            }
        }

        /// <summary>착탄점 aim을 고정하고 seconds 뒤 투하하도록 강하를 시작한다(원 표시).</summary>
        private void BeginDive(Vector3 aim, float seconds)
        {
            aim.y = 0f;
            Vector3 approach = aim - transform.position;
            approach.y = 0f;
            if (approach.sqrMagnitude < 1e-4f) approach = transform.forward;
            _diveStart = transform.position;
            // 폭탄은 앞으로 조금 날아가므로 착탄점 조금 앞(뒤쪽)에서 놓는다
            _releasePos = aim - approach.normalized * 3f + Vector3.up * releaseAltitude;
            Commit(seconds, aim, approach);
            ShowMarker(seconds + bombFlightTime);
        }

        public override void OnDespawned()
        {
            if (_leader != null && _leader._wingman == this) _leader._wingman = null;
            if (_wingman != null && _wingman._leader == this) _wingman._leader = null;
            _leader = _wingman = null;
            base.OnDespawned();
        }

        protected override void UpdateCommitted(float dt)
        {
            float left = Mathf.Max(0.05f, ReleaseAt - Time.time);
            float dist = Vector3.Distance(transform.position, _releasePos);
            // 급강하 제동판: 정해진 시각에 투하 지점에 닿도록 속력을 맞춘다(접근보다 느릴 수 있다 — 대공포가 잡을 기회)
            Fly(_releasePos, Mathf.Clamp(dist / left, 6f, 40f), 900f, true, dt);
            if (Time.time < ReleaseAt) return;

            var p = transform.position;
            var go = bombPrefab != null ? PoolManager.Instance?.Spawn(bombPrefab, p, Quaternion.LookRotation(Vector3.down + CommittedHeading)) : null;
            if (go != null && go.TryGetComponent<ArtilleryShell>(out var bomb))
            {
                bomb.Launch(p, StrikePt, bombFlightTime, Definition.AttackDamage, blastRadius, _playerHull, Ww2BattleLog.DiveBomber);
                if (_marker != null) { bomb.AdoptMarker(_marker); _marker = null; }   // 원은 폭탄이 떨어질 때 함께 치운다
            }
            AudioManager.Play(Game.Data.SfxId.MissileLaunch, p, 0.4f, 1.3f);
            MarkReleased();
        }

        private void ShowMarker(float seconds)
        {
            if (warningMarker == null || PoolManager.Instance == null) return;
            _marker = PoolManager.Instance.Spawn(warningMarker, new Vector3(StrikePt.x, 0.05f, StrikePt.z), Quaternion.identity);
            if (_marker != null && _marker.TryGetComponent<ImpactMarker>(out var m)) m.Show(blastRadius, seconds);
        }

        protected override void HideTelegraph()
        {
            if (_marker == null) return;
            if (PoolManager.Instance != null) PoolManager.Instance.Despawn(_marker);
            else _marker.SetActive(false);
            _marker = null;
        }
    }
}

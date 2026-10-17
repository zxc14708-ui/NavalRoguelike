using UnityEngine;
using Game.Core;
using Game.Combat;

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
    /// </summary>
    public class Ww2DiveBomber : Ww2StrikeAircraft
    {
        [Header("Dive")]
        [SerializeField] private GameObject bombPrefab;
        [SerializeField] private GameObject warningMarker;
        [SerializeField] private float commitDistance = 26f;
        [SerializeField] private float diveTime = 2.3f;
        [SerializeField] private float releaseAltitude = 5f;
        [SerializeField] private float bombFlightTime = 0.6f;
        [SerializeField] private float blastRadius = 4f;

        private Vector3 _diveStart, _releasePos;
        private GameObject _marker;
        private Collider _playerHull;

        public override string StrikeName => Ww2BattleLog.DiveBomber;

        protected override void OnStrikeSetup()
        {
            _playerHull = Player != null ? Player.GetComponent<Collider>() : null;
        }

        protected override void UpdateApproach(float dt)
        {
            Vector3 over = Player.position + PlayerVelocity * 1.5f;
            over.y = cruiseAltitude;
            Fly(over, Definition.MoveSpeed, Definition.TurnRateDegPerSec, false, dt);

            Vector3 flat = Player.position - transform.position;
            flat.y = 0f;
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (PlayerConcealed || flat.magnitude > commitDistance || Vector3.Angle(fwd, flat) > 55f) return;

            float t = diveTime + bombFlightTime;
            Vector3 aim = Player.position + PlayerVelocity * t;
            aim.y = 0f;
            Vector3 approach = aim - transform.position;
            approach.y = 0f;
            _diveStart = transform.position;
            // 폭탄은 앞으로 조금 날아가므로 착탄점 조금 앞(뒤쪽)에서 놓는다
            _releasePos = aim - approach.normalized * 3f + Vector3.up * releaseAltitude;
            Commit(diveTime, aim, approach);
            ShowMarker(t);
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

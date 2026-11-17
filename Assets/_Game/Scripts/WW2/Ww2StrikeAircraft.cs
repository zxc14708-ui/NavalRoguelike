using UnityEngine;
using Game.Combat;
using Game.Enemies;
using Game.Ship;

namespace Game.WW2
{
    /// <summary>
    /// WWII 공격기 공통(패치 B): 접근 → 공격 방향 확정(예고) → 투하 → 이탈.
    ///   - 확정한 뒤에는 방향을 바꾸지 않는다. 예고(수면 표시·HUD 경보)를 보고 침로·속력을 바꾸면 피할 수 있다.
    ///   - 투하 전에 격추되면 공격이 취소된다(<see cref="Ww2BattleLog.Cancelled"/>). 이미 투하한 어뢰·폭탄은 그대로 간다.
    ///   - 투하하면 이탈해 <see cref="egressTime"/>초 뒤 전장을 떠난다(격침이 아니므로 경험치 없음 — 한 번 공격하는 기체).
    ///   - 기함만 노린다. 연막이 퍼져 있으면 확정하지 않고 맴돈다(조준 공격 불가).
    /// </summary>
    public abstract class Ww2StrikeAircraft : AirEnemy, IAirStrike
    {
        protected enum Phase { Approach, Committed, Egress }

        [Header("Strike")]
        [SerializeField] protected float egressTime = 8f;

        protected Phase State;
        protected float PhaseTime;
        protected float ReleaseAt;
        protected Vector3 StrikePt;
        protected ShipController PlayerShip;

        public abstract string StrikeName { get; }
        public bool IsCommitted => State == Phase.Committed;
        public bool HasReleased { get; protected set; }
        public float SecondsToRelease => State == Phase.Committed ? Mathf.Max(0f, ReleaseAt - Time.time) : -1f;
        public Vector3 StrikePoint => StrikePt;
        public virtual Vector3 DangerPoint => StrikePt;
        /// <summary>검증용: 확정한 방향(수평).</summary>
        public Vector3 CommittedHeading { get; protected set; }

        protected override bool TargetsEscorts => false;

        public override void Setup(Game.Data.EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            State = Phase.Approach;
            PhaseTime = 0f;
            HasReleased = false;
            PlayerShip = player != null ? player.GetComponent<ShipController>() : null;
            AirStrikes.Register(this);
            OnStrikeSetup();
        }

        protected virtual void OnStrikeSetup() { }

        /// <summary>기함의 지금 속도(수평).</summary>
        protected Vector3 PlayerVelocity
        {
            get
            {
                if (PlayerShip == null || Player == null) return Vector3.zero;
                Vector3 v = Player.forward * PlayerShip.CurrentSpeed;
                v.y = 0f;
                return v;
            }
        }

        protected sealed override void UpdateBehaviour(float dt)
        {
            PhaseTime += dt;
            switch (State)
            {
                case Phase.Approach: UpdateApproach(dt); break;
                case Phase.Committed: UpdateCommitted(dt); break;
                default: UpdateEgress(dt); break;
            }
        }

        protected abstract void UpdateApproach(float dt);
        protected abstract void UpdateCommitted(float dt);

        protected void Commit(float secondsToRelease, Vector3 strikePoint, Vector3 heading)
        {
            State = Phase.Committed;
            PhaseTime = 0f;
            ReleaseAt = Time.time + secondsToRelease;
            StrikePt = new Vector3(strikePoint.x, 0f, strikePoint.z);
            heading.y = 0f;
            CommittedHeading = heading.sqrMagnitude > 1e-4f ? heading.normalized : transform.forward;
            Ww2BattleLog.Committed(StrikeName);
        }

        protected void MarkReleased()
        {
            HasReleased = true;
            Ww2BattleLog.Released(StrikeName);
            State = Phase.Egress;
            PhaseTime = 0f;
            HideTelegraph();
        }

        private void UpdateEgress(float dt)
        {
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            Vector3 away = transform.position + fwd.normalized * 30f;
            away.y = cruiseAltitude + 4f;
            Fly(away, Definition.MoveSpeed * 1.15f, Definition.TurnRateDegPerSec * 0.5f, false, dt);
            if (PhaseTime >= egressTime) RemoveWithoutReward();   // 전장을 떠났다
        }

        protected override void Die()
        {
            if (State == Phase.Committed && !HasReleased)
            {
                Ww2BattleLog.Cancelled(StrikeName);
                Game.Combat.CombatLog.Add("항공", $"{StrikeName} — 투하 전에 격추");
            }
            HideTelegraph();
            base.Die();
        }

        public override void OnDespawned()
        {
            HideTelegraph();
            AirStrikes.Unregister(this);
            base.OnDespawned();
        }

        protected abstract void HideTelegraph();
    }
}

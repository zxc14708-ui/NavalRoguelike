using UnityEngine;
using Game.Core;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 자폭 보트. 고속정보다 작고 빠르며 체력이 낮다. 폭약을 싣고 함선에 곧장 돌진해 부딪히는 순간 터진다.
    ///
    ///   - 함선이 지금 속력·침로로 가면 만나는 점으로 전속 돌진한다. 멀리서는 좌우로 지그재그해 조준을 흐트러뜨리고,
    ///     가까워지면(zigzagStopRange) 곧게 들어온다.
    ///   - 함선 최고속력(30노트)보다 빨라(19 m/s ≈ 37노트) 도망만으로는 떨칠 수 없다 — 기관포·76mm로 쏴서 막거나 섬으로 가로막는다.
    ///     추격 중에는 지그재그 폭을 줄여 다가가는 속력을 지킨다.
    ///   - 함체 표면에서 detonateDistance 안이면 폭발: 맞은 자리 모듈과 선체가 피해(DamageResolver).
    ///   - 스스로 터지면 경험치 없음, 도중에 격침하면 경험치를 주고 제자리에서 폭발(피해 없음).
    ///   - 섬은 다른 수상함처럼 비켜 간다(EnemyController.Steer).
    /// </summary>
    public class SuicideBoat : EnemyController
    {
        [SerializeField] private GameObject blastEffect;
        [SerializeField] private float blastScale = 1.4f;

        [Tooltip("함체 표면에서 이 거리 안이면 폭발")]
        [SerializeField] private float detonateDistance = 1.3f;

        [Header("Approach")]
        [Tooltip("이보다 멀면 지그재그로 접근한다")]
        [SerializeField] private float zigzagStopRange = 22f;
        [SerializeField] private float zigzagAngle = 28f;
        [SerializeField] private float zigzagPeriod = 2.2f;

        private Collider _playerHull;
        private Game.Ship.ShipController _playerShip;
        private float _phase;

        /// <summary>개발·검증용: 함선에 부딪혀 터졌는가(스폰마다 초기화).</summary>
        public bool Detonated { get; private set; }

        public override void Setup(Game.Data.EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            _playerHull = player != null ? player.GetComponent<Collider>() : null;
            _playerShip = player != null ? player.GetComponent<Game.Ship.ShipController>() : null;
            _phase = Random.value * Mathf.PI * 2f;
            Detonated = false;
        }

        /// <summary>자폭 공격(한 방 큰 피해)은 호위함에 쓰지 않는다 — 위협은 기함에 모은다(대함미사일·어뢰와 같은 규칙).</summary>
        protected override bool TargetsEscorts => false;

        protected override void OnTargetChanged() => _playerHull = Player != null ? Player.GetComponent<Collider>() : null;

        protected override void UpdateBehaviour(float dt)
        {
            Vector3 self = transform.position;
            Vector3 target = Player.position;

            // 만나는 점으로(함선 속력이 더 느리므로 해가 있다)
            if (_playerShip != null)
            {
                Vector3 v = Player.forward * _playerShip.CurrentSpeed;
                target = Ballistics.PredictIntercept(self, Player.position, v, Mathf.Max(1f, CurrentSpeed));
            }

            Vector3 heading = target - self;
            heading.y = 0f;
            float dist = heading.magnitude;
            if (dist > zigzagStopRange && dist > 0.01f)
            {
                // 지그재그는 다가가는 속력을 깎는다. 함선이 달아나는 만큼은 남기고 흔든다(추격 중에는 거의 곧게).
                float fleeing = _playerShip != null ? Mathf.Max(0f, Vector3.Dot(Player.forward * _playerShip.CurrentSpeed, heading / dist)) : 0f;
                float need = Mathf.Clamp01((fleeing + 2f) / Mathf.Max(1f, Definition.MoveSpeed));
                float maxSwing = Mathf.Min(zigzagAngle, Mathf.Acos(need) * Mathf.Rad2Deg);
                float swing = Mathf.Sin(Time.time * Mathf.PI * 2f / zigzagPeriod + _phase) * maxSwing;
                heading = Quaternion.Euler(0f, swing, 0f) * heading;
            }
            Steer(heading, 1f, dt);

            Vector3 surface = _playerHull != null ? _playerHull.ClosestPoint(self) : Player.position;
            surface.y = self.y;
            if ((surface - self).sqrMagnitude <= detonateDistance * detonateDistance)
                Detonate(surface);
        }

        private void Detonate(Vector3 hitPoint)
        {
            Detonated = true;
            if (Player.TryGetComponent<IDamageable>(out var target) && target.IsAlive)
                target.TakeDamage(new DamageInfo(Definition.AttackDamage, hitPoint, transform.forward, DamageSource.Missile));

            PooledEffect.Spawn(blastEffect, transform.position, blastScale);
            Game.View.Explosions.Spawn(hitPoint + Vector3.up * 0.6f, 1.7f, Game.View.Explosions.Kind.Charge);
            AudioManager.Play(Game.Data.SfxId.Explosion, transform.position, 1f, 0.85f);
            CombatLog.Add("자폭 보트", $"함선 충돌 폭발 · 피해 {Definition.AttackDamage:0}");
            RemoveWithoutReward();
        }

        /// <summary>격침되면 싣고 있던 폭약이 제자리에서 터진다(함선 피해 없음). 산산조각 나므로 가라앉는 잔해는 없다.</summary>
        protected override void Die()
        {
            PooledEffect.Spawn(blastEffect, transform.position, blastScale * 0.8f);
            Game.View.Explosions.Spawn(transform.position + Vector3.up * 0.6f, 1.3f, Game.View.Explosions.Kind.Charge);
            base.Die();
        }

        protected override bool LeavesWreck => false;
    }
}

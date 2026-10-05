using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Ship;

namespace Game.Enemies
{
    /// <summary>
    /// 초반 기본 적. 빠르게 접근해 선회하면서 갑판 포탑으로 짧은 점사를 날린다.
    ///
    /// 예전에는 사거리 안에 들면 즉시 피해를 줘서, 소리는 나는데 누가 쏘는지 보이지 않았다.
    /// 포탑이 실제로 돌아가고 예광탄이 날아와야 플레이어가 위협을 읽고 피할 수 있다.
    /// </summary>
    public class FastAttackBoat : EnemyController
    {
        [Header("Turret")]
        [SerializeField] private WeaponController weapon = new();
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private float turretTurnRate = 240f;

        [Header("Burst")]
        [Tooltip("한 번에 쏘는 발 수. 한 점사의 총 피해가 EnemyDefinition.AttackDamage다.")]
        [SerializeField, Min(1)] private int burstCount = 3;
        [SerializeField] private float burstSpacing = 0.12f;
        [Tooltip("예광탄이 눈으로 따라갈 수 있을 만큼 느려야 한다")]
        [SerializeField] private float projectileSpeed = 45f;

        private ShipController _playerShip;
        private int _burstLeft;
        private float _burstTimer;

        public override void Setup(Game.Data.EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            _playerShip = player != null ? player.GetComponent<ShipController>() : null;
            _burstLeft = 0;
            _burstTimer = 0f;
        }

        protected override void UpdateBehaviour(float dt)
        {
            OrbitPlayer(Definition.PreferredRange, 1f, dt);

            weapon.Tick(dt);
            Vector3 aim = AimPoint();
            weapon.AimAt(aim, turretTurnRate, dt);

            // 점사 진행 중이면 남은 발을 이어서 쏜다
            if (_burstLeft > 0)
            {
                _burstTimer -= dt;
                if (_burstTimer > 0f) return;

                FireRound(aim);
                _burstLeft--;
                _burstTimer = burstSpacing;
                return;
            }

            if (AttackTimer > 0f || PlayerConcealed || PlayerBehindIsland) return;

            float dist = Vector3.Distance(transform.position, Player.position);
            if (dist > EngageRadius(Definition.PreferredRange) + 4f) return;
            if (!weapon.IsAimedAt(aim)) return;

            _burstLeft = burstCount;
            _burstTimer = 0f;
            AttackTimer = Definition.AttackCooldown;
        }

        /// <summary>플레이어 배가 움직이는 만큼 앞을 겨눈다. 선체 중앙 약간 위.</summary>
        private Vector3 AimPoint()
        {
            Vector3 target = Player.position + Vector3.up * 0.8f;
            if (_playerShip == null) return target;

            var muzzle = weapon.Muzzle != null ? weapon.Muzzle : transform;
            Vector3 velocity = Player.forward * _playerShip.CurrentSpeed;
            return Ballistics.PredictIntercept(muzzle.position, target, velocity, projectileSpeed);
        }

        private void FireRound(Vector3 aim)
        {
            var muzzle = weapon.Muzzle != null ? weapon.Muzzle : transform;
            float damage = Definition.AttackDamage / burstCount;

            AudioManager.Play(Game.Data.SfxId.AutocannonShot, muzzle.position, 0.35f, 1.25f);

            var go = projectilePrefab != null
                ? PoolManager.Instance?.Spawn(projectilePrefab, muzzle.position, muzzle.rotation)
                : null;

            if (go == null || !go.TryGetComponent<Projectile>(out var projectile))
            {
                // 방어 코드: 탄 프리팹이 없으면 예전처럼 즉시 피해를 준다
                if (Player.TryGetComponent<IDamageable>(out var target) && target.IsAlive)
                    target.TakeDamage(new DamageInfo(damage, Player.position, transform.forward, DamageSource.Gun));
                return;
            }

            projectile.Launch((aim - muzzle.position).normalized, projectileSpeed, damage, DamageSource.Gun);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 미사일 초계함(4스테이지 엘리트, 모티브 타란툴급). 교전 원을 돌며
    ///   - 모스킷 대함미사일 2발(좌 · 우 발사관에서 한 발씩, Definition.MissilePrefab · AttackDamage · AttackCooldown)
    ///   - 선수 함포 점사(gunRange 안, gunCooldown마다 gunBurst발)
    /// 미사일은 기함만 노린다. 연막 · 섬 뒤 표적에는 쏘지 않는다.
    /// </summary>
    public class MissileCorvette : EnemyController
    {
        [Header("Missiles")]
        [SerializeField] private Transform[] launchPoints;
        [SerializeField] private float missileRangeFactor = 1.6f;

        [Header("Bow gun")]
        [SerializeField] private WeaponController weapon = new();
        [SerializeField] private float turretTurnRate = 90f;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private float projectileSpeed = 55f;
        [SerializeField] private float gunRange = 30f;
        [SerializeField] private float gunCooldown = 3.2f;
        [SerializeField, Min(1)] private int gunBurst = 3;
        [SerializeField] private float gunShotInterval = 0.18f;
        [SerializeField] private float gunDamage = 4f;

        [Header("Model")]
        [SerializeField] private Transform radar;

        private float _gunTimer;
        private int _nextLauncher;
        private Game.Ship.ShipController _playerShip;

        /// <summary>검증용: 쏜 미사일 · 함포탄 수.</summary>
        public int DevMissiles { get; private set; }
        public int DevGunShots { get; private set; }

        protected override bool TargetsEscorts => false;

        public override void Setup(Game.Data.EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            StopAllCoroutines();
            _playerShip = player != null ? player.GetComponent<Game.Ship.ShipController>() : null;
            if (def != null) AttackTimer = def.AttackCooldown * 0.4f;
            _gunTimer = 2f;
            _nextLauncher = 0;
            DevMissiles = DevGunShots = 0;
        }

        protected override void UpdateBehaviour(float dt)
        {
            OrbitPlayer(Definition.PreferredRange, 0.75f, dt);
            if (radar != null) radar.Rotate(Vector3.up, 90f * dt, Space.World);

            Vector3 aim = Player.position + Vector3.up * 0.8f;
            if (_playerShip != null && weapon.Muzzle != null)
                aim = Ballistics.PredictIntercept(weapon.Muzzle.position, aim, Player.forward * _playerShip.CurrentSpeed, projectileSpeed);
            weapon.Tick(dt);
            weapon.AimAt(aim, turretTurnRate, dt);
            _gunTimer -= dt;

            if (PlayerConcealed || PlayerBehindIsland) return;
            float dist = Vector3.Distance(transform.position, Player.position);

            if (_gunTimer <= 0f && dist <= gunRange && projectilePrefab != null && weapon.IsAimedAt(aim))
            {
                _gunTimer = gunCooldown;
                StartCoroutine(GunBurst());
            }
            if (AttackTimer <= 0f && Definition.MissilePrefab != null && dist <= Definition.PreferredRange * missileRangeFactor)
            {
                AttackTimer = Definition.AttackCooldown;
                StartCoroutine(MissileSalvo());
            }
        }

        private IEnumerator GunBurst()
        {
            for (int i = 0; i < gunBurst; i++)
            {
                if (!IsAlive || Player == null) yield break;
                var muzzle = weapon.Muzzle != null ? weapon.Muzzle : transform;
                Vector3 dir = weapon.FireDirection;
                dir = Quaternion.Euler(Random.Range(-1.2f, 1.2f), Random.Range(-1.2f, 1.2f), 0f) * dir;
                var go = PoolManager.Instance?.Spawn(projectilePrefab, muzzle.position, Quaternion.LookRotation(dir));
                go?.GetComponent<Projectile>()?.Launch(dir, projectileSpeed, gunDamage, DamageSource.Gun);
                AudioManager.Play(Game.Data.SfxId.EnemyGunShot, muzzle.position, 0.5f, 1.15f);
                weapon.Kick(0.12f);
                DevGunShots++;
                yield return new WaitForSeconds(gunShotInterval);
            }
        }

        private IEnumerator MissileSalvo()
        {
            int count = Mathf.Max(1, Definition.SalvoSize);
            for (int i = 0; i < count; i++)
            {
                if (!IsAlive || Player == null || PlayerConcealed) yield break;
                Transform origin = transform;
                if (launchPoints != null && launchPoints.Length > 0)
                {
                    origin = launchPoints[_nextLauncher % launchPoints.Length] ?? transform;
                    _nextLauncher++;
                }
                var go = PoolManager.Instance?.Spawn(Definition.MissilePrefab, origin.position, origin.rotation);
                go?.GetComponent<Missile>()?.Launch(Player, Definition.AttackDamage);
                DevMissiles++;
                if (i < count - 1) yield return new WaitForSeconds(Mathf.Max(0.3f, Definition.SalvoSpacing));
            }
        }
    }
}

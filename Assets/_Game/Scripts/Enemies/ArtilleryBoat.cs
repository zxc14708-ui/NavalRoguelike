using System.Collections;
using UnityEngine;
using Game.Core;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 포격 지원정(일반 수상 적). 기관포·76mm 사거리 밖에서 천천히 돌며, 큰 선수 함포로 곡사 포격을 한다.
    ///
    /// 보스 주포와 같은 "보고 피하는 공격": 포탄(<see cref="ArtilleryShell"/>)이 날아가는 동안 착탄점 수면에
    /// 붉은 경고 원이 차오르고, 원 안의 함체만 맞는다. 착탄 시각의 함선 위치를 예측해 쏘므로 침로·속력을 바꾸면 빗나간다.
    /// 한 번에 shellsPerSalvo발(첫 발은 예측점, 나머지는 주변). 포탑이 표적을 향해야 쏜다. 연막·섬 뒤면 쏘지 않는다.
    /// 포격은 기함만 노린다(경고 원 판정이 기함 선체 기준).
    /// </summary>
    public class ArtilleryBoat : EnemyController
    {
        [Header("Gun")]
        [SerializeField] private WeaponController weapon = new();
        [SerializeField] private float turretTurnRate = 70f;
        [SerializeField] private GameObject shellPrefab;
        [SerializeField] private GameObject muzzleFlash;

        [Header("Salvo")]
        [SerializeField, Min(1)] private int shellsPerSalvo = 2;
        [SerializeField] private float shellFlightTime = 2.6f;
        [SerializeField] private float shellBlastRadius = 2.6f;
        [SerializeField] private float salvoSpread = 4f;
        [SerializeField] private float shellSpacing = 0.35f;
        [Tooltip("이보다 가까우면 쏘지 않는다(곡사포 최소 사거리)")]
        [SerializeField] private float minFireRange = 16f;
        [Tooltip("교전 거리보다 이만큼 멀어도 쏜다")]
        [SerializeField] private float extraRange = 14f;

        private Game.Ship.ShipController _playerShip;
        private Collider _playerHull;

        /// <summary>포격은 기함에만(착탄 판정이 기함 선체 기준이다).</summary>
        protected override bool TargetsEscorts => false;

        public override void Setup(Game.Data.EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            _playerShip = player != null ? player.GetComponent<Game.Ship.ShipController>() : null;
            _playerHull = player != null ? player.GetComponent<Collider>() : null;
            if (def != null) AttackTimer = def.AttackCooldown * 0.5f;
        }

        protected override void UpdateBehaviour(float dt)
        {
            OrbitPlayer(Definition.PreferredRange, 0.6f, dt);

            Vector3 predicted = PredictedImpact();
            weapon.Tick(dt);
            weapon.AimAt(predicted + Vector3.up * 4f, turretTurnRate, dt);   // 곡사: 조금 높이 겨눈다(포탑 선회가 핵심)

            if (AttackTimer > 0f || PlayerConcealed || PlayerBehindIsland) return;
            float dist = Vector3.Distance(transform.position, Player.position);
            if (dist < minFireRange || dist > Definition.PreferredRange + extraRange) return;
            if (!weapon.IsAimedAt(predicted + Vector3.up * 4f)) return;

            AttackTimer = Definition.AttackCooldown;
            StartCoroutine(FireSalvo(predicted));
        }

        /// <summary>착탄 시각의 함선 위치(지금 속력·침로 그대로).</summary>
        private Vector3 PredictedImpact()
        {
            Vector3 v = _playerShip != null ? Player.forward * _playerShip.CurrentSpeed : Vector3.zero;
            v.y = 0f;
            Vector3 p = Player.position + v * shellFlightTime;
            p.y = 0f;
            return p;
        }

        private IEnumerator FireSalvo(Vector3 predicted)
        {
            if (shellPrefab == null) yield break;
            var muzzle = weapon.Muzzle != null ? weapon.Muzzle : transform;
            for (int i = 0; i < shellsPerSalvo; i++)
            {
                if (!IsAlive || Player == null) yield break;
                Vector2 jitter = i == 0 ? Vector2.zero : Random.insideUnitCircle * salvoSpread;
                Vector3 impact = predicted + new Vector3(jitter.x, 0f, jitter.y);

                var go = PoolManager.Instance?.Spawn(shellPrefab, muzzle.position, muzzle.rotation);
                go?.GetComponent<ArtilleryShell>()?.Launch(muzzle.position, impact, shellFlightTime,
                                                           Definition.AttackDamage, shellBlastRadius, _playerHull);
                PooledEffect.Spawn(muzzleFlash, muzzle.position);
                AudioManager.Play(Game.Data.SfxId.NavalGunShot, muzzle.position, 0.8f, 0.75f);
                weapon.Kick(0.18f);
                yield return new WaitForSeconds(shellSpacing);
            }
        }
    }
}

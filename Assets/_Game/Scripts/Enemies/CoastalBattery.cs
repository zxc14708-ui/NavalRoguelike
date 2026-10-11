using System.Collections;
using UnityEngine;
using Game.Core;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 해안포대(2026-10-11 점검 K04, 고정 적). 군도의 큰 섬 해안·좁은 수로 양안에 박혀 움직이지 않는다.
    ///   - 사거리(PreferredRange, 45m — 기본 레이더 안) 안·최소 거리(14m) 밖의 표적에게 곡사포 2발(포격정과 같은 포탄 — 떨어질 자리가 미리 보여 피할 수 있다)
    ///   - 호위함·보급선도 노린다(가까운 쪽). 섬 뒤·연막 안의 표적에는 쏘지 않는다
    ///   - 체력이 많고 장갑이 두꺼운 고정 진지(대형 수상 표적 분류) — 곡사포·76mm가 잘 듣는다
    ///   - 격침되면 잔해 대신 큰 폭발과 불길. 같은 자리에는 그 스테이지 동안 다시 생기지 않는다(CoastalBatteryField)
    /// 포탑(weapon)만 돈다. 수상함 이동(Steer)·선회 궤도는 쓰지 않는다.
    /// </summary>
    public class CoastalBattery : EnemyController
    {
        [Header("Gun")]
        [SerializeField] private WeaponController weapon = new();
        [SerializeField] private float turretTurnRate = 45f;
        [SerializeField] private GameObject shellPrefab;
        [SerializeField] private GameObject muzzleFlash;

        [Header("Salvo")]
        [SerializeField, Min(1)] private int shellsPerSalvo = 2;
        [SerializeField] private float shellFlightTime = 2.4f;
        [SerializeField] private float shellBlastRadius = 2.8f;
        [SerializeField] private float salvoSpread = 3.5f;
        [SerializeField] private float shellSpacing = 0.45f;
        [SerializeField] private float minFireRange = 14f;

        private Vector3 _lastTargetPos;
        private Vector3 _targetVelocity;
        private Transform _lastTarget;

        /// <summary>검증용: 지금까지 쏜 포탄 수.</summary>
        public int ShellsFired { get; private set; }

        protected override bool LeavesWreck => false;

        public override void Setup(Game.Data.EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            if (def != null) AttackTimer = def.AttackCooldown * 0.6f;   // 나타나자마자 쏘지 않게
            _lastTarget = null;
            ShellsFired = 0;
        }

        protected override void UpdateBehaviour(float dt)
        {
            TrackTarget(dt);
            Vector3 predicted = Player.position + _targetVelocity * shellFlightTime;
            predicted.y = 0f;
            weapon.Tick(dt);
            weapon.AimAt(predicted + Vector3.up * 4f, turretTurnRate, dt);

            if (AttackTimer > 0f || PlayerConcealed || PlayerBehindIsland) return;
            Vector3 d = Player.position - transform.position; d.y = 0f;
            float dist = d.magnitude;
            if (dist < minFireRange || dist > Definition.PreferredRange) return;
            if (!weapon.IsAimedAt(predicted + Vector3.up * 4f)) return;

            AttackTimer = Definition.AttackCooldown;
            StartCoroutine(FireSalvo(predicted));
        }

        /// <summary>표적 속도는 위치 변화로 잰다(기함·호위함·보급선 모두).</summary>
        private void TrackTarget(float dt)
        {
            if (Player != _lastTarget || dt <= 0f)
            {
                _lastTarget = Player;
                _lastTargetPos = Player.position;
                _targetVelocity = Vector3.zero;
                return;
            }
            Vector3 v = (Player.position - _lastTargetPos) / dt;
            v.y = 0f;
            _targetVelocity = Vector3.Lerp(_targetVelocity, v, Mathf.Clamp01(dt * 3f));
            _lastTargetPos = Player.position;
        }

        private IEnumerator FireSalvo(Vector3 predicted)
        {
            if (shellPrefab == null) yield break;
            var muzzle = weapon.Muzzle != null ? weapon.Muzzle : transform;
            var hull = Player != null ? Player.GetComponent<Collider>() : null;
            for (int i = 0; i < shellsPerSalvo; i++)
            {
                if (!IsAlive || Player == null) yield break;
                Vector2 jitter = i == 0 ? Vector2.zero : Random.insideUnitCircle * salvoSpread;
                Vector3 impact = predicted + new Vector3(jitter.x, 0f, jitter.y);
                var go = PoolManager.Instance?.Spawn(shellPrefab, muzzle.position, muzzle.rotation);
                go?.GetComponent<ArtilleryShell>()?.Launch(muzzle.position, impact, shellFlightTime,
                                                           Definition.AttackDamage, shellBlastRadius, hull);
                PooledEffect.Spawn(muzzleFlash, muzzle.position);
                AudioManager.Play(Game.Data.SfxId.NavalGunShot, muzzle.position, 0.9f, 0.65f);
                weapon.Kick(0.2f);
                ShellsFired++;
                yield return new WaitForSeconds(shellSpacing);
            }
        }

        protected override void Die()
        {
            Game.View.Explosions.Spawn(transform.position + Vector3.up * 1.5f, 2.4f, Game.View.Explosions.Kind.Impact);
            Game.View.Explosions.Spawn(transform.position + Vector3.up * 0.8f + Random.insideUnitSphere, 1.6f, Game.View.Explosions.Kind.Impact);
            base.Die();
        }
    }
}

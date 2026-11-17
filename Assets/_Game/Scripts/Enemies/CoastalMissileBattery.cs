using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 해안 미사일 포대(2026-11-15, 고정 적). 군도의 적 기지 섬(IslandBuilder.BuildKit) 안에 서 있는 2연장 대함미사일 발사대.
    /// 모델은 Codex IslandBiomePack_Refined의 MIL_MissileBattery_2(TurretPivot → ElevationPivot → LaunchPoint_01·02).
    ///   - 사거리(PreferredRange) 안의 기함을 향해 발사대를 돌리고, 발사관을 들어 올리는 예고(raiseSeconds) 뒤 2발을 쏜다(미사일정과 같은 탄)
    ///   - 기함만 노린다(대함미사일은 호위함에 쓰지 않는다 — 미사일정과 같은 규칙). 섬 뒤·연막 안의 표적에는 쏘지 않는다
    ///   - 쏘고 나면 발사관을 내리고 재장전(AttackCooldown). 격침되면 잔해 대신 폭발
    /// 해안포대(CoastalBattery)처럼 CoastalBatteryField가 함선이 다가오면 세우고 멀어지면 치운다(격침한 자리는 그 스테이지 동안 다시 서지 않음).
    /// </summary>
    public class CoastalMissileBattery : EnemyController
    {
        [Header("Launcher")]
        [Tooltip("선회부(TurretPivot)와 포구만 — 고각은 아래 elevationPivot을 예고 연출로 직접 들어 올린다")]
        [SerializeField] private WeaponController weapon = new();
        [SerializeField] private Transform elevationPivot;
        [SerializeField] private Transform[] launchPoints;
        [SerializeField] private float turretTurnRate = 50f;
        [Tooltip("발사 전에 발사관을 이만큼 들어 올린다(예고)")]
        [SerializeField] private float raiseElevation = 24f;
        [SerializeField] private float raiseSeconds = 1.4f;
        [SerializeField] private float minFireRange = 12f;
        [SerializeField] private GameObject muzzleFlash;

        private bool _raising;
        private float _elevation;
        private Quaternion _elevationRest;
        private bool _restCaptured;

        /// <summary>검증용: 지금까지 쏜 미사일 수 · 발사 예고(발사관 들어 올림) 중인가.</summary>
        public int MissilesFired { get; private set; }
        public bool Raising => _raising;
        public float Elevation => _elevation;

        protected override bool LeavesWreck => false;
        protected override bool TargetsEscorts => false;

        public override void Setup(Game.Data.EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            if (def != null) AttackTimer = def.AttackCooldown * 0.5f;   // 나타나자마자 쏘지 않게
            MissilesFired = 0;
            _raising = false;
            _elevation = 0f;
            if (elevationPivot != null)
            {
                if (!_restCaptured) { _elevationRest = elevationPivot.localRotation; _restCaptured = true; }
                elevationPivot.localRotation = _elevationRest;
            }
        }

        protected override void UpdateBehaviour(float dt)
        {
            weapon.Tick(dt);
            // 발사대는 수평으로만 돌린다(고각은 예고 연출로 직접 올린다)
            Vector3 flat = Player.position; flat.y = transform.position.y + 1.5f;
            weapon.AimAt(flat, turretTurnRate, dt);
            float want = _raising ? raiseElevation : 0f;
            _elevation = Mathf.MoveTowards(_elevation, want, raiseElevation / Mathf.Max(0.1f, raiseSeconds * 0.8f) * dt);
            if (elevationPivot != null)
            {
                // 고각축 = 발사 방향의 오른쪽(월드). 모델 축 방향과 상관없이 위로 든다(Codex 검증과 같은 방식)
                elevationPivot.localRotation = _elevationRest;
                Vector3 fwd = LaunchForward(); fwd.y = 0f;
                if (fwd.sqrMagnitude > 1e-4f && _elevation > 0.01f)
                    elevationPivot.Rotate(Vector3.Cross(Vector3.up, fwd.normalized), -_elevation, Space.World);
            }

            if (_raising || AttackTimer > 0f || PlayerConcealed || PlayerBehindIsland) return;
            Vector3 d = Player.position - transform.position; d.y = 0f;
            float dist = d.magnitude;
            if (dist < minFireRange || dist > Definition.PreferredRange) return;
            if (Definition.MissilePrefab == null) { AttackTimer = Definition.AttackCooldown; return; }
            if (!weapon.IsAimedAt(flat)) return;

            AttackTimer = Definition.AttackCooldown;
            StartCoroutine(FireSalvo());
        }

        /// <summary>발사 방향: 첫 발사점(없으면 포구 · 선회부)의 앞.</summary>
        private Vector3 LaunchForward()
        {
            if (launchPoints != null && launchPoints.Length > 0 && launchPoints[0] != null) return launchPoints[0].forward;
            if (weapon.Muzzle != null) return weapon.Muzzle.forward;
            return transform.forward;
        }

        private IEnumerator FireSalvo()
        {
            _raising = true;
            yield return new WaitForSeconds(raiseSeconds);
            var points = new List<Transform>();
            if (launchPoints != null) foreach (var p in launchPoints) if (p != null) points.Add(p);
            if (points.Count == 0) points.Add(weapon.Muzzle != null ? weapon.Muzzle : transform);
            int count = Mathf.Max(1, Definition.SalvoSize);
            for (int i = 0; i < count; i++)
            {
                if (!IsAlive || Player == null || PlayerConcealed) break;
                var origin = points[i % points.Count];
                var go = PoolManager.Instance?.Spawn(Definition.MissilePrefab, origin.position, origin.rotation);
                go?.GetComponent<Missile>()?.Launch(Player, Definition.AttackDamage);
                PooledEffect.Spawn(muzzleFlash, origin.position);
                MissilesFired++;
                if (i < count - 1) yield return new WaitForSeconds(Mathf.Max(0.35f, Definition.SalvoSpacing));
            }
            yield return new WaitForSeconds(0.6f);
            _raising = false;
        }

        protected override void Die()
        {
            Game.View.Explosions.Spawn(transform.position + Vector3.up * 1.6f, 2.2f, Game.View.Explosions.Kind.Impact);
            Game.View.Explosions.Spawn(transform.position + Vector3.up * 0.8f + Random.insideUnitSphere, 1.4f, Game.View.Explosions.Kind.Impact);
            base.Die();
        }
    }
}

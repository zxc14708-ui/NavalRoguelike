using System.Collections;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Data;
using Game.Ship;
using Game.View;

namespace Game.Enemies
{
    /// <summary>1스테이지 연안 경비정: 원거리 미사일, 근거리 함수/함미 함포.</summary>
    public class BossShip : EnemyController
    {
        [Header("Missiles")]
        [SerializeField] private Transform[] launchPoints;
        [SerializeField, Min(1)] private int salvoCount = 4;
        [SerializeField, Min(0.1f)] private float salvoInterval = 9f;
        [SerializeField, Min(0.01f)] private float salvoSpacing = 0.24f;
        [SerializeField] private float missileMaxRange = 70f;

        [Header("Guns")]
        [SerializeField] private GameObject gunProjectilePrefab;
        [SerializeField] private GameObject gunFlashMaterialSource;
        [SerializeField] private float gunEnterRange = 25f;
        [SerializeField] private float gunExitRange = 30f;
        [SerializeField] private float gunShotInterval = 1.5f;
        [SerializeField] private float gunDamage = 9f;
        [SerializeField] private float gunProjectileSpeed = 48f;
        [SerializeField] private float gunTurnRate = 100f;

        private readonly WeaponController _bowGun = new();
        private readonly WeaponController _aftGun = new();
        private ShipController _playerShip;
        private MuzzleBurstVfx _muzzleFx;
        private Coroutine _salvo;
        private float _salvoTimer, _gunTimer, _smokeDebt, _fireDebt;
        private int _damageStage;
        private bool _closeCombat, _useAftNext;
        private bool _gunRestCaptured;
        private Quaternion _bowRestRotation, _aftRestRotation;

        public override void Setup(EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            _playerShip = player != null ? player.GetComponent<ShipController>() : null;
            _salvoTimer = 3f;
            _gunTimer = 0.8f;
            _smokeDebt = _fireDebt = 0f;
            _damageStage = 0;
            _closeCombat = _useAftNext = false;
            if (_salvo != null) StopCoroutine(_salvo);
            _salvo = null;

            // 구형 프리팹의 launchPoints는 함포 총구다. 항상 실제 발사관 소켓을 다시 찾는다.
            var all = GetComponentsInChildren<Transform>(true);
            var sockets = new System.Collections.Generic.List<Transform>(8);
            foreach (var t in all)
                if (t.name.StartsWith("MissileLaunchPoint_", System.StringComparison.Ordinal)) sockets.Add(t);
            sockets.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            launchPoints = sockets.ToArray();

            var bowTurret = Find(all, "BowGun_TurretPivot");
            var aftTurret = Find(all, "AftGun_TurretPivot");
            if (!_gunRestCaptured)
            {
                _bowRestRotation = bowTurret != null ? bowTurret.localRotation : Quaternion.identity;
                _aftRestRotation = aftTurret != null ? aftTurret.localRotation : Quaternion.identity;
                _gunRestCaptured = true;
            }
            else
            {
                if (bowTurret != null) bowTurret.localRotation = _bowRestRotation;
                if (aftTurret != null) aftTurret.localRotation = _aftRestRotation;
            }
            // 미니어처 적 v9 모델은 포신 고각부(…_ElevationPivot)가 따로 있다 — 없으면(예전 모델) 선회만
            _bowGun.BindArtTransforms(bowTurret, Find(all, "BowGun_ElevationPivot"), Find(all, "BowGun_Muzzle"));
            _aftGun.BindArtTransforms(aftTurret, Find(all, "AftGun_ElevationPivot"), Find(all, "AftGun_Muzzle"));
            var arc = LimitedGunArc(130f); // 함교/마스트를 가로질러 선회하지 않는다.
            _bowGun.SetFireArc(arc);
            _aftGun.SetFireArc(arc);
            _muzzleFx ??= GetComponent<MuzzleBurstVfx>() ?? gameObject.AddComponent<MuzzleBurstVfx>();
            GameEvents.RaiseBossPhaseChanged("연안 경비정 출현 — 원거리 미사일 / 근거리 함포");
        }

        /// <summary>보스는 기함과의 결투로 설계됐다 — 호위함으로 표적을 돌리지 않는다.</summary>
        protected override bool TargetsEscorts => false;

        protected override void UpdateBehaviour(float dt)
        {
            float distance = Vector3.Distance(transform.position, Player.position);
            if (_closeCombat ? distance >= gunExitRange : distance <= gunEnterRange)
                _closeCombat = !_closeCombat;

            OrbitPlayer(Definition.PreferredRange, _closeCombat ? 0.8f : 0.65f, dt);
            EmitDamageFx(dt);

            float tick = dt * (ReconAircraft.ActiveCount > 0 ? ReconAircraft.SpottedAttackRate : 1f);
            if (_closeCombat)
            {
                _bowGun.Tick(dt);
                _aftGun.Tick(dt);
                UpdateGuns(tick, dt);
            }
            else
            {
                _salvoTimer -= tick;
                if (_salvo == null && _salvoTimer <= 0f && distance <= missileMaxRange &&
                    !PlayerConcealed && !PlayerBehindIsland && Definition.MissilePrefab != null)
                {
                    _salvoTimer = salvoInterval;
                    _salvo = StartCoroutine(FireSalvo());
                }
            }
        }

        private void UpdateGuns(float tick, float dt)
        {
            Vector3 aim = Player.position + Vector3.up * 0.8f;
            if (_playerShip != null)
                aim = Ballistics.PredictIntercept(transform.position, aim,
                    Player.forward * _playerShip.CurrentSpeed, gunProjectileSpeed);
            _bowGun.AimAt(aim, gunTurnRate, dt);
            _aftGun.AimAt(aim, gunTurnRate, dt);
            _gunTimer -= tick;
            if (_gunTimer > 0f || PlayerConcealed || PlayerBehindIsland || gunProjectilePrefab == null) return;

            bool bowReady = _bowGun.Muzzle != null && _bowGun.IsInFireArc(aim) && _bowGun.IsAimedAt(aim);
            bool aftReady = _aftGun.Muzzle != null && _aftGun.IsInFireArc(aim) && _aftGun.IsAimedAt(aim);
            if (!bowReady && !aftReady) return;
            var gun = aftReady && (!bowReady || _useAftNext) ? _aftGun : _bowGun;
            _useAftNext = !_useAftNext;
            _gunTimer = gunShotInterval;
            var muzzle = gun.Muzzle;
            Vector3 dir = (aim - muzzle.position).normalized;
            var shot = PoolManager.Instance?.Spawn(gunProjectilePrefab, muzzle.position, Quaternion.LookRotation(dir));
            shot?.GetComponent<Projectile>()?.Launch(dir, gunProjectileSpeed, gunDamage, DamageSource.Gun);
            _muzzleFx.Emit(muzzle.position, dir, MuzzleBurstVfx.Style.NavalGun, gunFlashMaterialSource, true);
            AudioManager.Play(SfxId.NavalGunShot, muzzle.position, 0.7f, 1.05f);
        }

        private IEnumerator FireSalvo()
        {
            // FBX 축 보정에 따라 좌우가 뒤집힐 수 있으므로 소켓의 실제 월드 위치로 현측을 고른다.
            float targetX = transform.InverseTransformPoint(Player.position).x;
            int side = targetX >= 0f ? 4 : 0;
            if (launchPoints != null && launchPoints.Length >= 8)
            {
                float bank0X = transform.InverseTransformPoint(launchPoints[0].position).x;
                float bank4X = transform.InverseTransformPoint(launchPoints[4].position).x;
                side = Mathf.Abs(targetX - bank0X) <= Mathf.Abs(targetX - bank4X) ? 0 : 4;
            }
            for (int i = 0; i < salvoCount; i++)
            {
                if (!IsAlive || Player == null || _closeCombat || PlayerConcealed || PlayerBehindIsland) break;
                Vector3 position;
                Quaternion rotation;
                if (launchPoints != null && launchPoints.Length >= 8)
                {
                    var socket = launchPoints[side + i % 4];
                    position = socket.position;
                    rotation = socket.rotation;
                }
                else
                {
                    // 옛 프리팹을 쓰는 동안에도 함포 총구에서 미사일이 나오지 않게 한다.
                    float x = side == 4 ? 1.55f : -1.55f;
                    position = transform.TransformPoint(new Vector3(x, 1.65f + (i / 2) * 0.35f, -2.8f + (i % 2) * 0.65f));
                    rotation = Quaternion.LookRotation(transform.TransformDirection(new Vector3(x > 0f ? 1f : -1f, 0.2f, 0.1f)));
                }
                var missile = PoolManager.Instance?.Spawn(Definition.MissilePrefab, position, rotation);
                missile?.GetComponent<Missile>()?.Launch(Player, Definition.AttackDamage);
                if (i < salvoCount - 1) yield return new WaitForSeconds(salvoSpacing);
            }
            _salvo = null;
        }

        public override void TakeDamage(in DamageInfo info)
        {
            base.TakeDamage(info);
            if (!IsAlive) return;
            int stage = HpFraction <= 0.25f ? 3 : HpFraction <= 0.5f ? 2 : HpFraction <= 0.75f ? 1 : 0;
            if (stage <= _damageStage) return;
            _damageStage = stage;
            GameEvents.RaiseBossPhaseChanged(stage == 1 ? "연안 경비정 손상 — 연기 발생" :
                stage == 2 ? "연안 경비정 화재 — 함포·미사일 경계" : "연안 경비정 대파 — 화재 확산");
        }

        private void EmitDamageFx(float dt)
        {
            if (_damageStage == 0) return;
            _smokeDebt += dt * (_damageStage == 1 ? 8f : _damageStage == 2 ? 14f : 21f);
            while (_smokeDebt >= 1f)
            {
                _smokeDebt -= 1f;
                Vector3 p = transform.TransformPoint(new Vector3(1.25f, 2.1f, -1.3f)) + Random.insideUnitSphere * 0.25f;
                DecorFx.Emit(DecorFx.WreckSmoke, p, Vector3.up * Random.Range(1.4f, 2.2f) + DecorFx.Wind * 0.4f,
                    Random.Range(1.8f, 2.7f), Random.Range(0.9f, 1.4f), new Color(0.12f, 0.14f, 0.15f, 0.7f));
            }
            if (_damageStage < 2) return;
            _fireDebt += dt * (_damageStage == 2 ? 14f : 24f);
            while (_fireDebt >= 1f)
            {
                _fireDebt -= 1f;
                Vector3 local = _damageStage == 3 && Random.value < 0.4f
                    ? new Vector3(-1.2f, 1.55f, 1.5f) : new Vector3(1.25f, 1.55f, -1.3f);
                Vector3 p = transform.TransformPoint(local) + Random.insideUnitSphere * 0.2f;
                DecorFx.Emit(DecorFx.Fire, p, Vector3.up * Random.Range(0.8f, 1.5f) + DecorFx.Wind * 0.12f,
                    Random.Range(0.35f, 0.65f), Random.Range(0.55f, 0.95f),
                    new Color(1f, Random.Range(0.28f, 0.58f), 0.07f, 0.85f));
            }
        }

        private static Transform Find(Transform[] all, string name)
        {
            foreach (var t in all) if (t.name == name) return t;
            return null;
        }

        private static FireArc LimitedGunArc(float halfAngle)
        {
            var noTrain = new bool[FireArc.Steps];
            for (int i = 0; i < noTrain.Length; i++)
                noTrain[i] = Mathf.Abs(Mathf.DeltaAngle(0f, (i + 0.5f) * FireArc.StepDeg)) > halfAngle;
            return new FireArc(null, noTrain);
        }

        public override void OnDespawned()
        {
            if (_salvo != null) StopCoroutine(_salvo);
            _salvo = null;
            base.OnDespawned();
        }

        protected override void Die()
        {
            base.Die();
            GameEvents.RaiseBossDefeated();
        }
    }
}

using System.Collections;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Data;
using Game.Ship;
using Game.View;

namespace Game.Enemies
{
    /// <summary>
    /// 1스테이지 연안 경비정: 원거리 미사일, 근거리 함수/함미 함포.
    /// 부위 파괴(2026-11-01, <see cref="BossParts"/>): 함수·함미 함포(각 최대 체력 14%), 두 미사일 발사관 묶음(각 15%) —
    /// 부서진 포는 쏘지 않고, 부서진 쪽 발사관 묶음은 일제사격을 하지 않는다.
    ///
    /// 현측 일제사격(2026-11-26 Codex 제안 · "저쪽 발사관을 부수고 그쪽에 붙어서 이겼다"):
    ///   1 돌려 세우기  사격 3초 전, 살아 있는 발사관 현측을 기함 쪽으로 돌린다(최대 6초). 한쪽이 부서졌으면 반대 현측을 들이밀려 크게 돈다 —
    ///                  기함이 부서진 쪽에 붙어 따라가면 끝내 못 돌려 이번 사격을 거른다(DevBlockedSalvos).
    ///   2 예고 1.5초   그 현측 발사관에 붉은 불 + 수면에 붉은 부채꼴. 이때부터 그 발사관은 노출(우선 피격 · 부위 피해 1.5배) — 먼저 부수면 사격 취소.
    ///   3 일제사격     예고한 현측에서만 4발.
    ///   4 재장전 3초   달아오른 발사관 + 수면에 금색 부채꼴: 여기서 쏘면 우리 무장이 그 발사관을 겨눈다(IAimPointProvider), 부위 피해 1.5배.
    ///   발사관 둘 다 부서지면 함포만 남아 가까이 붙는다(돌격).
    /// 가까이(25m) 붙으면 지금처럼 함포로 바꾼다 — 파고들면 미사일은 막지만 함포를 맞는다.
    /// </summary>
    public class BossShip : EnemyController, IBossTelegraph, IBossParts, IAimPointProvider
    {
        // ---- 보스 학습(점검 K05): 원거리면 미사일 일제사격, 가까우면 함포. 손상 단계는 연출(75·50·25%).
        public string PhaseName => _gunsOnly ? "발사관 전멸 — 함포 돌격" : _closeCombat ? "근거리 — 함포 2문" : "원거리 — 현측 일제사격";
        public float[] PhaseThresholds => s_thresholds;
        private static readonly float[] s_thresholds = { 0.75f, 0.5f, 0.25f };
        public (string name, float seconds) NextAttack => _closeCombat || _gunsOnly ? ("함포", Mathf.Max(0f, _gunTimer))
            : _bs == Broadside.Telegraph ? ($"{BankSideName(_bsSide)} 일제사격", Mathf.Max(0f, TelegraphTime - _bsT))
            : ("미사일 일제사격", Mathf.Max(0f, _salvoTimer));

        // ---- 현측 일제사격(2026-11-26)
        public const float PresentLead = 3f, PresentGiveUp = 6f, TelegraphTime = 1.5f, ReloadWindow = 3f, ExposedPartBonus = 1.5f, AlignHalfAngle = 35f;
        public enum Broadside { None, Present, Telegraph, Firing, Window }
        private Broadside _bs;
        private float _bsT;
        private int _bsSide;              // 0 또는 4(발사관 소켓 묶음)
        private bool _bank0Starboard;
        private bool _gunsOnly;
        private int _announcedKills;
        private BossBroadsideFx _fx;
        /// <summary>검증용: 지금 단계 · 예고 횟수 · 쏜 일제사격 · 못 돌려 거른 사격 · 노출된 발사관에 들어간 피격 · 마지막 예고 길이(초) · 마지막 사격 현측.</summary>
        public Broadside DevBroadside => _bs;
        public int DevTelegraphs { get; private set; }
        public int DevSalvos { get; private set; }
        public int DevBlockedSalvos { get; private set; }
        public int DevExposedHits { get; private set; }
        public float DevLastTelegraph { get; private set; }
        public int DevSalvoSide { get; private set; }   // +1 우현 · −1 좌현
        public bool GunsOnly => _gunsOnly;
        /// <summary>지금 노출된(예고 · 사격 · 재장전 중) 발사관 부위. 없으면 null.</summary>
        public BossParts.Part ExposedBank => (_bs == Broadside.Telegraph || _bs == Broadside.Firing || _bs == Broadside.Window) ? BankPart(_bsSide) : null;
        /// <summary>현측(+1 우현 · −1 좌현)의 발사관 부위.</summary>
        public BossParts.Part BankOnSide(int sign) => (sign > 0) == _bank0Starboard ? _bank0Part : _bank4Part;

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
        private readonly BossParts _parts = new();
        private BossParts.Part _bowPart, _aftPart, _bank0Part, _bank4Part;
        private ShipController _playerShip;

        /// <summary>검증용: 부위 목록.</summary>
        public BossParts Parts => _parts;
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
            float maxHp = def != null ? def.MaxHp : 600f;
            _parts.Clear();
            _bowPart = _parts.Add("함수 함포", "함수 포 사격 중지", bowTurret, 2.4f, maxHp * 0.14f, bowTurret);
            _aftPart = _parts.Add("함미 함포", "함미 포 사격 중지", aftTurret, 2.4f, maxHp * 0.14f, aftTurret);
            if (launchPoints != null && launchPoints.Length >= 8)
            {
                bool bank0Starboard = transform.InverseTransformPoint(launchPoints[0].position).x >= 0f;
                _bank0Starboard = bank0Starboard;
                _bank0Part = _parts.Add(bank0Starboard ? "우현 미사일 발사관" : "좌현 미사일 발사관", "그쪽 일제사격 중지", launchPoints[0], 2.4f, maxHp * 0.15f, launchPoints[0].parent);
                _bank4Part = _parts.Add(bank0Starboard ? "좌현 미사일 발사관" : "우현 미사일 발사관", "그쪽 일제사격 중지", launchPoints[4], 2.4f, maxHp * 0.15f, launchPoints[4].parent);
            }
            _bs = Broadside.None; _bsT = 0f; _bsSide = 0; _gunsOnly = false; _announcedKills = 0;
            _loggedParts = 0; _loggedNames.Clear();
            DevTelegraphs = DevSalvos = DevBlockedSalvos = DevExposedHits = 0; DevLastTelegraph = 0f; DevSalvoSide = 0;
            _fx = BossBroadsideFx.Ensure(transform);
            _fx.Hide();
            if (Factions.GrantsReward(this)) BossDuelLog.Begin(def != null ? def.DisplayName : "연안 경비정");
            GameEvents.RaiseBossPhaseChanged("연안 경비정 출현 — 현측을 돌려 세우면 일제사격, 쏜 직후 그 발사관이 열린다");
        }

        /// <summary>보스는 기함과의 결투로 설계됐다 — 호위함으로 표적을 돌리지 않는다.</summary>
        protected override bool TargetsEscorts => false;

        protected override void UpdateBehaviour(float dt)
        {
            float distance = Vector3.Distance(transform.position, Player.position);
            bool hasBanks = launchPoints != null && launchPoints.Length >= 8;
            bool banksLeft = hasBanks && Definition.MissilePrefab != null &&
                             (!_parts.IsDestroyed(_bank0Part) || !_parts.IsDestroyed(_bank4Part));
            if (!banksLeft && !_gunsOnly && hasBanks)
            {
                _gunsOnly = true;
                EndBroadside();
                GameEvents.RaiseBossPhaseChanged("연안 경비정 발사관 전멸 — 함포로 붙어 온다");
            }
            if (_closeCombat ? distance >= gunExitRange : distance <= gunEnterRange)
                _closeCombat = !_closeCombat;
            // 돌려 세우거나 예고하는 중에 기함이 파고들면 사격을 접고 함포로(파고든 보람 — 대신 함포를 맞는다)
            if (_closeCombat && (_bs == Broadside.Present || _bs == Broadside.Telegraph)) { EndBroadside(); _salvoTimer = Mathf.Max(_salvoTimer, 2f); }

            bool holdBroadside = _bs == Broadside.Present || _bs == Broadside.Telegraph || _bs == Broadside.Firing;
            if (holdBroadside) SteerBroadside(_bsSide, dt);
            else OrbitPlayer(_gunsOnly ? gunEnterRange * 0.7f : Definition.PreferredRange, _closeCombat || _gunsOnly ? 0.85f : 0.65f, dt);
            EmitDamageFx(dt);
            _parts.Tick(dt);
            // 보스전 요약: 발사관 하나만 부서졌을 때 기함이 그 부서진 쪽에 있던 시간
            if (hasBanks && _parts.IsDestroyed(_bank0Part) != _parts.IsDestroyed(_bank4Part))
            {
                bool deadStar = _parts.IsDestroyed(_bank0Part) ? IsStarboard(0) : IsStarboard(4);
                if (deadStar == PlayerOnStarboard) BossDuelLog.DeadSideSeconds += dt;
            }

            float tick = dt * (ReconAircraft.ActiveCount > 0 ? ReconAircraft.SpottedAttackRate : 1f);
            if (_closeCombat || _gunsOnly)
            {
                _bowGun.Tick(dt);
                _aftGun.Tick(dt);
                UpdateGuns(tick, dt);
            }
            if (!_closeCombat && !_gunsOnly) UpdateBroadside(tick, dt, distance);
            else if (_bs == Broadside.Window) TickWindow(dt);
        }

        // ------------------------------------------------------------ 현측 일제사격

        private bool IsStarboard(int side) => side == 0 ? _bank0Starboard : !_bank0Starboard;
        private BossParts.Part BankPart(int side) => side == 0 ? _bank0Part : _bank4Part;
        private string BankSideName(int side) => IsStarboard(side) ? "우현" : "좌현";

        /// <summary>기함이 그 현측 정횡(±AlignHalfAngle) 안에 있는가.</summary>
        private bool PlayerAbeam(int side)
        {
            Vector3 local = transform.InverseTransformPoint(Player.position);
            float bearing = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            return Mathf.Abs(Mathf.DeltaAngle(bearing, IsStarboard(side) ? 90f : -90f)) <= AlignHalfAngle;
        }

        private bool PlayerOnStarboard => transform.InverseTransformPoint(Player.position).x >= 0f;

        private void UpdateBroadside(float tick, float dt, float distance)
        {
            bool canShoot = distance <= missileMaxRange && !PlayerConcealed && !PlayerBehindIsland;
            switch (_bs)
            {
                case Broadside.None:
                {
                    _salvoTimer -= tick;
                    if (_salvoTimer > PresentLead || !canShoot || _gunsOnly) break;
                    // 살아 있는 발사관 중 지금 기함 쪽을 향한 현측(둘 다 살아 있으면 덜 돌아도 되는 쪽)
                    bool a0 = !_parts.IsDestroyed(_bank0Part), a4 = !_parts.IsDestroyed(_bank4Part);
                    int side = a0 && a4 ? (IsStarboard(0) == PlayerOnStarboard ? 0 : 4) : a0 ? 0 : 4;
                    _bsSide = side; _bs = Broadside.Present; _bsT = 0f;
                    // 한쪽을 잃었고 기함이 그 부서진 쪽에 있다: 크게 돌아 살아 있는 현측을 들이민다(한 번 알림)
                    int kills = (a0 ? 0 : 1) + (a4 ? 0 : 1);
                    if (kills == 1 && IsStarboard(side) != PlayerOnStarboard && _announcedKills < 1)
                    {
                        _announcedKills = 1;
                        GameEvents.RaiseBossPhaseChanged($"연안 경비정 — 살아 있는 {BankSideName(side)}을 들이밀려 크게 선회. 부서진 쪽에 붙어 따라가십시오");
                    }
                    break;
                }
                case Broadside.Present:
                    _bsT += dt;
                    if (_parts.IsDestroyed(BankPart(_bsSide))) { EndBroadside(); break; }
                    if (!canShoot) { EndBroadside(); _salvoTimer = 1.5f; break; }
                    if (PlayerAbeam(_bsSide))
                    {
                        _bs = Broadside.Telegraph; _bsT = 0f;
                        DevTelegraphs++;
                        var lit = BankPart(_bsSide);
                        if (lit != null) lit.Priority = true;   // 불 들어온 발사관은 노출(먼저 부수면 사격 취소)
                        _fx.Show(BossBroadsideFx.Mode.Warn, IsStarboard(_bsSide) ? 1f : -1f, TelegraphTime, BankSockets(_bsSide));
                        AudioManager.Play(SfxId.SonarPing, transform.position, 0.6f, 0.7f);
                        break;
                    }
                    if (_bsT >= PresentGiveUp)
                    {
                        // 끝내 못 돌렸다(기함이 부서진 쪽 · 뒤쪽에 붙어 따라왔다): 이번 사격은 거른다
                        DevBlockedSalvos++;
                        BossDuelLog.Blocked++;
                        EndBroadside();
                        _salvoTimer = PresentLead + 2f;
                    }
                    break;
                case Broadside.Telegraph:
                    _bsT += dt;
                    if (_parts.IsDestroyed(BankPart(_bsSide)))
                    {
                        string name = BankSideName(_bsSide);
                        BossDuelLog.Cancelled++;
                        EndBroadside();
                        GameEvents.RaiseBossPhaseChanged($"예고 중인 {name} 발사관 파괴 — 일제사격 저지");
                        _salvoTimer = salvoInterval * 0.5f;
                        break;
                    }
                    if (!canShoot) { EndBroadside(); _salvoTimer = 1.5f; break; }
                    if (_bsT >= TelegraphTime)
                    {
                        DevLastTelegraph = _bsT;
                        _bs = Broadside.Firing;
                        _salvoTimer = salvoInterval;
                        if (_salvo != null) StopCoroutine(_salvo);
                        _salvo = StartCoroutine(FireSalvo(_bsSide));
                    }
                    break;
                case Broadside.Firing:
                    break;   // FireSalvo가 끝나면 재장전 창으로
                case Broadside.Window:
                    _salvoTimer -= tick;
                    TickWindow(dt);
                    break;
            }
        }

        private int _loggedParts;
        private void LogPartKills()
        {
            int n = 0;
            foreach (var p in _parts.Parts) if (p.Destroyed) n++;
            if (n <= _loggedParts) return;
            foreach (var p in _parts.Parts)
                if (p.Destroyed && !_loggedNames.Contains(p.Name)) { _loggedNames.Add(p.Name); BossDuelLog.PartKilled(p.Name); }
            _loggedParts = n;
        }
        private readonly System.Collections.Generic.HashSet<string> _loggedNames = new();

        private void TickWindow(float dt)
        {
            _bsT += dt;
            var bank = BankPart(_bsSide);
            if (_bsT >= ReloadWindow || bank == null || bank.Destroyed) EndBroadside();
        }

        private void EndBroadside()
        {
            var bank = BankPart(_bsSide);
            if (bank != null) bank.Priority = false;
            _bs = Broadside.None; _bsT = 0f;
            if (_fx != null) _fx.Hide();
        }

        private Transform[] BankSockets(int side)
        {
            if (launchPoints == null || launchPoints.Length < side + 4) return new Transform[0];
            return new[] { launchPoints[side], launchPoints[side + 1], launchPoints[side + 2], launchPoints[side + 3] };
        }

        /// <summary>그 현측을 기함 쪽으로 돌려 세운다: 기함 방향에 수직(현측이 기함을 봄) + 거리 유지 성분.</summary>
        private void SteerBroadside(int side, float dt)
        {
            Vector3 d = Player.position - transform.position; d.y = 0f;
            float dist = d.magnitude;
            if (dist < 0.01f) { Steer(transform.forward, 0.6f, dt); return; }
            d /= dist;
            Vector3 h = IsStarboard(side) ? Vector3.Cross(d, Vector3.up) : Vector3.Cross(Vector3.up, d);
            float radius = EngageRadius(Definition.PreferredRange);
            float corr = Mathf.Clamp((dist - radius) / radius * 2f, -1f, 1f);
            Steer((h + d * corr * 0.6f).normalized, _bs == Broadside.Present ? 0.75f : 0.55f, dt);
        }

        /// <summary>
        /// 우리 무장이 겨눌 점: 노출된 발사관(예고 · 사격 · 재장전 중)이 쏘는 쪽 현측(정횡 ±70°)에 있거나 위에서 꽂히는 유도탄이면 그 발사관,
        /// 아니면 배 중심(예전과 같음).
        /// </summary>
        public Vector3 AimPointFor(Vector3 shooter, bool topAttack)
        {
            var bank = ExposedBank;
            if (!IsAlive || bank == null || bank.Destroyed || bank.Anchor == null) return transform.position;
            Vector3 local = transform.InverseTransformPoint(shooter);
            float bearing = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            bool sameSide = Mathf.Abs(Mathf.DeltaAngle(bearing, IsStarboard(_bsSide) ? 90f : -90f)) <= 70f;
            return topAttack || sameSide ? bank.Anchor.position : transform.position;
        }

        private void UpdateGuns(float tick, float dt)
        {
            Vector3 aim = PlayerAimPoint(0.8f);   // 기함이면 이쪽을 향한 바깥 칸(2026-11-21)
            if (_playerShip != null)
                aim = Ballistics.PredictIntercept(transform.position, aim,
                    Player.forward * _playerShip.CurrentSpeed, gunProjectileSpeed);
            _bowGun.AimAt(aim, gunTurnRate, dt);
            _aftGun.AimAt(aim, gunTurnRate, dt);
            _gunTimer -= tick;
            if (_gunTimer > 0f || PlayerConcealed || PlayerBehindIsland || gunProjectilePrefab == null) return;

            bool bowReady = _bowGun.Muzzle != null && !_parts.IsDestroyed(_bowPart) && _bowGun.IsInFireArc(aim) && _bowGun.IsAimedAt(aim);
            bool aftReady = _aftGun.Muzzle != null && !_parts.IsDestroyed(_aftPart) && _aftGun.IsInFireArc(aim) && _aftGun.IsAimedAt(aim);
            if (!bowReady && !aftReady) return;
            var gun = aftReady && (!bowReady || _useAftNext) ? _aftGun : _bowGun;
            _useAftNext = !_useAftNext;
            _gunTimer = gunShotInterval;
            var muzzle = gun.Muzzle;
            Vector3 dir = (aim - muzzle.position).normalized;
            var shot = PoolManager.Instance?.Spawn(gunProjectilePrefab, muzzle.position, Quaternion.LookRotation(dir));
            shot?.GetComponent<Projectile>()?.Launch(dir, gunProjectileSpeed, gunDamage, DamageSource.Gun);
            _muzzleFx.Emit(muzzle.position, dir, MuzzleBurstVfx.Style.NavalGun, gunFlashMaterialSource, true);
            AudioManager.Play(SfxId.EnemyGunShot, muzzle.position, 0.7f, 1.05f);
        }

        /// <summary>예고한 현측(side = 0 · 4 소켓 묶음)에서 일제사격. 끝나면 그 발사관이 재장전 창으로 열린다.</summary>
        private IEnumerator FireSalvo(int side)
        {
            var bankPart = BankPart(side);
            if (_parts.IsDestroyed(bankPart)) { _salvo = null; EndBroadside(); yield break; }   // 그쪽 발사관 묶음이 부서졌다
            DevSalvos++;
            BossDuelLog.Salvos++;
            DevSalvoSide = IsStarboard(side) ? 1 : -1;
            if (bankPart != null) bankPart.Priority = true;   // 예고부터 쏘는 동안 · 재장전 창: 노출
            for (int i = 0; i < salvoCount; i++)
            {
                if (!IsAlive || Player == null || PlayerConcealed || PlayerBehindIsland || _parts.IsDestroyed(bankPart)) break;
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
            // 재장전 창: 달아오른 발사관 + 금색 부채꼴(여기서 쏘면 그 발사관을 노린다)
            if (IsAlive && bankPart != null && !bankPart.Destroyed && _bs == Broadside.Firing)
            {
                _bs = Broadside.Window; _bsT = 0f;
                bankPart.Priority = true;
                _fx.Show(BossBroadsideFx.Mode.Open, IsStarboard(side) ? 1f : -1f, ReloadWindow, BankSockets(side), 22f, 70f);   // 22m: 무장 사거리 원(노랑)과 겹치지 않게
            }
            else if (_bs == Broadside.Firing) EndBroadside();
        }

        public override void TakeDamage(in DamageInfo info)
        {
            base.TakeDamage(info);
            if (!IsAlive) return;
            // 노출된 발사관(예고 · 사격 · 재장전 중): 그 자리 피해는 부위에 1.5배(선체 피해는 그대로)
            var exposed = ExposedBank;
            bool bonus = false;
            if (exposed != null && !exposed.Destroyed && exposed.Anchor != null && info.HitPoint != Vector3.zero)
            {
                Vector3 d = info.HitPoint - exposed.Anchor.position; d.y = 0f;
                bonus = d.sqrMagnitude <= exposed.Radius * exposed.Radius;
            }
            if (bonus)
            {
                DevExposedHits++;
                BossDuelLog.ExposedHits++;
                _parts.Hit(new DamageInfo(info.Amount * ExposedPartBonus, info.HitPoint, info.Direction, info.Source, info.Tag), this);
            }
            else _parts.Hit(info, this);
            LogPartKills();
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
            EndBroadside();
            _parts.Clear();
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

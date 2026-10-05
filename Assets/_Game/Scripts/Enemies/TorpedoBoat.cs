using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 경어뢰정(일반 수상 적). 양현에 선체 고정식 어뢰관이 있어, 쏠 때는 뱃머리를 만나는 점으로 돌린다.
    ///
    /// 발사 절차는 어뢰 잠수함과 같은 "피할 수 있는 공격"이다:
    ///   1) 사격 제원 결정: 그 순간 함선의 위치·속력·침로를 기록하고 경고(화면 "어뢰 준비", 경고음, 뱃머리 물거품).
    ///   2) firingSolutionTime 동안 뱃머리를 만나는 점으로 돌린 뒤 발사 — 기록한 속력·침로 그대로 가면 맞는다.
    /// 잠수함과 달리 수면 위에서 보이므로 먼저 격침해 막을 수도 있다. 너무 가까우면 쏘지 않고 선회해 거리를 벌린다.
    /// 모든 어뢰(잠수함 포함)의 동시 수 제한을 함께 쓴다(<see cref="Submarine.TorpedoesInPlay"/>).
    /// 선수 소형 포탑은 표적을 따라 돌 뿐 쏘지 않는다(위협은 어뢰 하나로 읽히게).
    /// </summary>
    public class TorpedoBoat : EnemyController
    {
        private static readonly List<TorpedoBoat> s_preparing = new();

        /// <summary>사격 제원을 잡고 발사를 기다리는 어뢰정 수(어뢰 동시 수 제한용).</summary>
        public static int PreparingCount => s_preparing.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_preparing.Clear();

        [Header("Torpedo")]
        [SerializeField] private GameObject torpedoPrefab;
        [Tooltip("어뢰관 끝(TorpedoLaunchPoint_01~04). 번갈아 쓴다. 비면 뱃머리 앞에서 쏜다.")]
        [SerializeField] private Transform[] launchPoints;
        [SerializeField] private float maxFireRange = 58f;
        [Tooltip("이보다 가까우면 쏘지 않는다(경고 2초 + 비행 시간으로 피할 여유를 남긴다)")]
        [SerializeField] private float minFireRange = 26f;
        [SerializeField] private float firingSolutionTime = 2f;
        [Tooltip("모든 잠수함·어뢰정을 합쳐 동시에 물속에 있거나 준비 중인 어뢰 최대 수")]
        [SerializeField] private int maxTorpedoesAtOnce = 2;
        [SerializeField, Range(0f, 1f)] private float openingDelayRatio = 0.5f;
        [SerializeField] private float warningSplashScale = 0.22f;

        [Header("Bow Turret (표적 추적만)")]
        [SerializeField] private WeaponController weapon = new();
        [SerializeField] private float turretTurnRate = 160f;

        private Game.Ship.ShipController _playerShip;
        private bool _preparing;
        private float _solutionTime;
        private Vector3 _solutionPos, _solutionVel;
        private int _tube;

        public bool IsPreparingTorpedo => _preparing;

        /// <summary>어뢰는 기함에만 쓴다(호위함이 한 방에 무너지지 않게 — 잠수함과 같은 규칙).</summary>
        protected override bool TargetsEscorts => false;

        public override void Setup(Game.Data.EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            _playerShip = player != null ? player.GetComponent<Game.Ship.ShipController>() : null;
            if (def != null) AttackTimer = def.AttackCooldown * openingDelayRatio;
            CancelSolution();
        }

        protected override void UpdateBehaviour(float dt)
        {
            weapon.Tick(dt);
            weapon.AimAt(Player.position + Vector3.up * 0.8f, turretTurnRate, dt);

            if (_preparing)
            {
                if (PlayerConcealed) { CancelSolution(); return; }   // 연막: 표적을 놓친다
                // 뱃머리(고정 어뢰관)를 만나는 점으로 돌린다
                Vector3 to = InterceptPoint(transform.position) - transform.position;
                to.y = 0f;
                Steer(to, 0.6f, dt);
                if (Time.time - _solutionTime < firingSolutionTime) return;
                LaunchTorpedo();
                CancelSolution();
                return;
            }

            OrbitPlayer(Definition.PreferredRange, 0.85f, dt);

            if (AttackTimer > 0f || PlayerConcealed || PlayerBehindIsland) return;
            float dist = Vector3.Distance(transform.position, Player.position);
            if (dist > maxFireRange || dist < minFireRange) return;
            if (Submarine.TorpedoesInPlay >= maxTorpedoesAtOnce) return;

            BeginSolution();
            AttackTimer = Definition.AttackCooldown;
        }

        private void BeginSolution()
        {
            _preparing = true;
            _solutionTime = Time.time;
            _solutionPos = Player.position;
            Vector3 fwd = Player.forward; fwd.y = 0f;
            _solutionVel = _playerShip != null && fwd.sqrMagnitude > 1e-6f ? fwd.normalized * _playerShip.CurrentSpeed : Vector3.zero;
            if (!s_preparing.Contains(this)) s_preparing.Add(this);

            var torpedoData = torpedoPrefab != null ? torpedoPrefab.GetComponent<Torpedo>() : null;
            Vector3 bow = transform.position + transform.forward * 3f;
            if (torpedoData != null) PooledEffect.Spawn(torpedoData.ImpactEffect, new Vector3(bow.x, 0f, bow.z), warningSplashScale);
            AudioManager.Play(Game.Data.SfxId.SonarPing, transform.position, 0.8f, 1.9f);
            CombatLog.Add("어뢰", $"{Definition.DisplayName} 사격 제원 결정 · {firingSolutionTime:0.0}초 뒤 발사");
        }

        private void CancelSolution()
        {
            _preparing = false;
            s_preparing.Remove(this);
        }

        /// <summary>제원을 정한 순간의 속력·침로 그대로 갔을 때 from에서 쏜 어뢰와 만나는 점.</summary>
        private Vector3 InterceptPoint(Vector3 from)
        {
            float speed = 16f;
            if (torpedoPrefab != null && torpedoPrefab.TryGetComponent<Torpedo>(out var t)) speed = Mathf.Max(0.1f, t.Speed);
            float elapsed = Time.time - _solutionTime;
            Vector3 aim = _solutionPos;
            float travel = Vector3.Distance(from, aim) / speed;
            for (int i = 0; i < 4; i++)
            {
                aim = _solutionPos + _solutionVel * (elapsed + travel);
                travel = Vector3.Distance(from, aim) / speed;
            }
            return aim;
        }

        private void LaunchTorpedo()
        {
            Transform tube = launchPoints != null && launchPoints.Length > 0 ? launchPoints[_tube++ % launchPoints.Length] : null;
            Vector3 from = tube != null ? tube.position : transform.position + transform.forward * 3f;
            from.y = transform.position.y;

            var go = torpedoPrefab != null ? PoolManager.Instance?.Spawn(torpedoPrefab, from, transform.rotation) : null;
            if (go != null && go.TryGetComponent<Torpedo>(out var torpedo))
            {
                torpedo.Launch(InterceptPoint(go.transform.position), Definition.AttackDamage);
                return;
            }

            // 방어 코드: 어뢰 프리팹이 없으면 즉시 피해로 대신한다
            if (Player.TryGetComponent<IDamageable>(out var target) && target.IsAlive)
                target.TakeDamage(new DamageInfo(Definition.AttackDamage, Player.position, Vector3.up, DamageSource.Torpedo));
        }

        public override void OnSpawned()
        {
            base.OnSpawned();
            CancelSolution();
        }

        public override void OnDespawned()
        {
            base.OnDespawned();
            CancelSolution();
        }
    }
}

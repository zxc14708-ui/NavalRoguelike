using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 헬기데크. 갑판 위에 헬기가 실제로 서 있다가 출격하고, 임무를 마치면 돌아와 다시 앉는다.
    ///
    /// 소나가 없어도 헬기 자체가 수색을 나간다 — 목표 상공에 도달하면 그 자리에서
    /// 잠수함을 드러낸다. 소나가 있으면 이미 드러난 목표로 곧장 가므로 훨씬 빠르다.
    /// </summary>
    public class HelicopterDeckModule : ModuleRuntime
    {
        [SerializeField] private GameObject helicopterPrefab;
        [SerializeField] private Transform landingSpot;

        [Tooltip("갑판에 서 있는 헬기 모델. 출격 중에는 숨긴다.")]
        [SerializeField] private GameObject parkedHelicopter;

        [Tooltip("주기 중에도 천천히 도는 로터")]
        [SerializeField] private Transform parkedRotor;
        [SerializeField] private float parkedRotorRpm = 12f;

        [Tooltip("소나가 없을 때 헬기가 스스로 훑는 범위")]
        [SerializeField] private float searchRadius = 60f;

        private TargetingSystem _targeting;
        private float _cooldown;
        private bool _sortieActive;
        private AswHelicopter _activeHelicopter;
        private bool _pairedDeck;
        private float _staggerUntil;
        public HelicopterPolicy Policy { get; private set; } = HelicopterPolicy.Automatic;
        public float SearchPersistence => (_pairedDeck ? 12f : 8f) +
            (Policy == HelicopterPolicy.AntiSubPatrol ? 4f : 0f);
        public bool CanOperate => isActiveAndEnabled && Instance != null && Instance.IsOperational;
        public bool SortieActive => _sortieActive;
        public float CooldownRemaining => Mathf.Max(0f, _cooldown);

        public void SetPolicy(HelicopterPolicy policy) => Policy = policy;

        public bool IsTargetInRange(ITargetable target)
        {
            if (target is Object obj && obj == null) return false;
            if (target == null || target.Transform == null || !target.IsAlive) return false;
            if (!target.Transform.gameObject.activeInHierarchy) return false;
            if (target.Kind != TargetKind.Submarine && target.Kind != TargetKind.Surface) return false;
            if (Policy == HelicopterPolicy.AntiSubPatrol && target.Kind != TargetKind.Submarine) return false;
            if (Policy == HelicopterPolicy.SurfaceStrike && target.Kind != TargetKind.Surface) return false;
            Vector3 delta = target.Transform.position - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= searchRadius * searchRadius;
        }

        protected override void OnInitialized()
        {
            Policy = HelicopterPolicy.Automatic;
            _targeting = GetComponentInParent<TargetingSystem>();
            if (_targeting == null) Debug.LogError("[HeliDeck] TargetingSystem을 찾지 못했습니다.", this);
            if (helicopterPrefab == null) Debug.LogError("[HeliDeck] helicopterPrefab 미할당.", this);

            SetParkedVisible(true);
            OnShipLayoutChanged();
        }

        protected override void OnShipLayoutChanged()
        {
            bool paired = ModuleSynergy.Adjacent(Grid, Instance, ModuleType.HelicopterDeck);
            if (paired && !_pairedDeck && Instance != null && ((Instance.Origin.X + Instance.Origin.Z) & 1) != 0)
                _staggerUntil = Time.time + 1f;
            _pairedDeck = paired;
        }

        protected override void Tick(float dt)
        {
            // 서 있는 동안에도 로터가 돌면 "살아있는 장비"로 보인다
            if (!_sortieActive && parkedRotor != null)
                parkedRotor.Rotate(Vector3.up, parkedRotorRpm * 6f * dt, Space.Self);

            if (_sortieActive)
            {
                if (_activeHelicopter != null && _activeHelicopter.gameObject.activeInHierarchy) return;
                OnHelicopterReturned();
            }
            if (_cooldown > 0f) { _cooldown -= dt; return; }
            if (Time.time < _staggerUntil) return;

            var target = FindTarget();
            if (target != null) { Sortie(target, target.Transform.position); return; }
            if (TryGetSearchCue(out var cue)) Sortie(null, cue);
            else if (Policy == HelicopterPolicy.AntiSubPatrol)
                Sortie(null, PatrolPoint());
        }

        public Vector3 PatrolPoint()
        {
            var origin = Ship != null ? Ship.transform : transform;
            float side = Instance != null && (Instance.Origin.Z & 1) == 0 ? 1f : -1f;
            return origin.position + origin.forward * 20f + origin.right * side * 12f;
        }

        /// <summary>
        /// 대잠 헬기지만 잠수함만 기다리면 초반 라운드 내내 갑판에 서 있게 된다.
        /// 다른 헬기가 맡지 않은 표적을 우선하고, 동률이면 잠수함·거리 순으로 고른다.
        /// </summary>
        public ITargetable FindTarget()
        {
            ITargetable best = null;
            int assignments = int.MaxValue;
            int kindPriority = int.MaxValue;
            float distance = float.MaxValue;
            if (Policy != HelicopterPolicy.SurfaceStrike && _targeting != null)
                foreach (var target in _targeting.DetectedSubmarines)
                    Consider(target, 0, ref best, ref assignments, ref kindPriority, ref distance);

            if (Policy != HelicopterPolicy.AntiSubPatrol)
                foreach (var target in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Surface))
                    Consider(target, 1, ref best, ref assignments, ref kindPriority, ref distance);
            return best;
        }

        private void Consider(ITargetable target, int priority, ref ITargetable best,
                              ref int assignments, ref int kindPriority, ref float distance)
        {
            if (!IsTargetInRange(target)) return;
            int assigned = AswHelicopter.AssignedCount(target);
            Vector3 delta = target.Transform.position - transform.position;
            delta.y = 0f;
            float sqr = delta.sqrMagnitude;
            if (assigned > assignments || (assigned == assignments && priority > kindPriority) ||
                (assigned == assignments && priority == kindPriority && sqr >= distance)) return;
            best = target;
            assignments = assigned;
            kindPriority = priority;
            distance = sqr;
        }

        public bool TryGetSearchCue(out Vector3 cue)
        {
            cue = default;
            return Policy != HelicopterPolicy.SurfaceStrike && _targeting != null &&
                   _targeting.TryGetLaunchCue(transform.position, searchRadius, out cue);
        }

        /// <summary>헬기 바로 아래의 잠수함만 자체 센서로 확인한다.</summary>
        public ITargetable FindTargetFromHelicopter(Vector3 position)
        {
            if (Policy == HelicopterPolicy.SurfaceStrike) return null;
            float best = 10f * 10f;
            int assignments = int.MaxValue;
            SubmarineBase found = null;
            foreach (var t in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Submarine))
            {
                if (t is not SubmarineBase sub || !IsTargetInRange(sub)) continue;
                Vector3 delta = sub.transform.position - position;
                delta.y = 0f;
                float d = delta.sqrMagnitude;
                int assigned = AswHelicopter.AssignedCount(sub);
                if (assigned > assignments || (assigned == assignments && d >= best)) continue;
                best = d;
                assignments = assigned;
                found = sub;
            }
            if (found != null) found.ConfirmContact(2f);
            return found;
        }

        private void Sortie(ITargetable target, Vector3 searchPoint)
        {
            var spot = landingSpot != null ? landingSpot : transform;
            var go = PoolManager.Instance?.Spawn(helicopterPrefab, spot.position, spot.rotation);
            if (go == null) return;

            var heli = go.GetComponent<AswHelicopter>();
            if (heli == null)
            {
                Debug.LogError("[HeliDeck] 헬기 프리팹에 AswHelicopter가 없습니다.", go);
                PoolManager.Instance.Despawn(go);
                return;
            }

            _sortieActive = true;
            _activeHelicopter = heli;
            SetParkedVisible(false);

            heli.Sortie(spot, target, this, searchPoint);
        }

        /// <summary>헬기가 착함했을 때 AswHelicopter가 호출한다.</summary>
        public void OnHelicopterReturned()
        {
            _sortieActive = false;
            _activeHelicopter = null;
            _cooldown = Mathf.Max(3f, Stats.SortieCooldown * (_pairedDeck ? 0.8f : 1f));

            // 출격 중에 데크가 부서졌다면 내려앉을 곳이 없다
            if (Instance != null && Instance.IsOperational) SetParkedVisible(true);
        }

        private void SetParkedVisible(bool visible)
        {
            if (parkedHelicopter != null) parkedHelicopter.SetActive(visible);
        }

        public override void OnModuleDestroyed()
        {
            // 격납고가 부서지면 돌아올 곳이 없다
            _sortieActive = false;
            SetParkedVisible(false);
        }
    }
}

using UnityEngine;
using Game.Core;
using Game.Ship;

namespace Game.Modules
{
    /// <summary>
    /// 설치된 모듈 프리팹에 붙는 동작의 기반 클래스.
    /// 파생 클래스는 Tick()에서만 동작하며, 다른 모듈을 직접 참조하지 않는다.
    /// 필요한 함선 정보는 Ship / Systems 프로퍼티를 통해서만 읽는다.
    /// </summary>
    public abstract class ModuleRuntime : MonoBehaviour
    {
        public ModuleInstance Instance { get; private set; }
        public ModuleDefinition Definition => Instance?.Definition;
        /// <summary>최종 스탯(기본값 + 블록 강화). 탄약고·시너지 보너스는 각 무기가 이 위에 배율로 곱한다.</summary>
        public ModuleStats Stats => Instance != null ? Instance.EffectiveStats : default;

        protected ShipController Ship { get; private set; }
        protected ShipSystems Systems => Ship != null ? Ship.Systems : null;
        protected ShipGrid Grid => Ship != null ? Ship.Grid : null;

        /// <summary>ModuleInstance.BindRuntime에서 호출된다.</summary>
        public void Initialize(ModuleInstance instance)
        {
            Instance = instance;
            Ship = GetComponentInParent<ShipController>();

            if (Ship == null)
                Debug.LogError($"[{GetType().Name}] 부모에서 ShipController를 찾지 못했습니다.", this);

            OnInitialized();
        }

        private void OnEnable()
        {
            GameEvents.ModuleInstalled += HandleLayoutChanged;
            GameEvents.ModuleRemoved += HandleLayoutChanged;
            GameEvents.ModuleDestroyed += HandleLayoutChanged;
            GameEvents.ModuleRaisedChanged += HandleLayoutChanged;
            GameEvents.ModuleUpgraded += HandleLayoutChanged;   // 탄약고 강화 → 이웃 무기가 지원량을 다시 잡는다
            GameEvents.StateChanged += HandleStateChanged;
            RunUpgrades.Changed += HandleRunUpgradesChanged;
            if (Instance != null) HandleRunUpgradesChanged();   // 꺼져 있는 동안 성장 카드가 바뀌었을 수 있다
        }

        private void OnDisable()
        {
            GameEvents.ModuleInstalled -= HandleLayoutChanged;
            GameEvents.ModuleRemoved -= HandleLayoutChanged;
            GameEvents.ModuleDestroyed -= HandleLayoutChanged;
            GameEvents.ModuleRaisedChanged -= HandleLayoutChanged;
            GameEvents.ModuleUpgraded -= HandleLayoutChanged;
            GameEvents.StateChanged -= HandleStateChanged;
            RunUpgrades.Changed -= HandleRunUpgradesChanged;
        }

        /// <summary>성장 카드(공격력·연사력·장탄수·사거리·수리)가 바뀌면 최종 스탯을 다시 계산하고 탄창을 새 용량에 맞춘다.</summary>
        private void HandleRunUpgradesChanged()
        {
            if (Instance == null) return;
            Instance.RefreshStats();
            if (this is Game.Combat.IAmmoUser user) user.Ammo?.Reconfigure(Stats);
            OnShipLayoutChanged();   // 탄약고 보너스를 새 탄창 기준으로 다시 곱한다
        }

        /// <summary>정비 페이즈에 들어가면 탄약을 전량 보급한다(한 구간 안의 탄약 운용이 핵심).</summary>
        private void HandleStateChanged(GameState state)
        {
            if (state == GameState.Refit && this is Game.Combat.IAmmoUser user) user.Ammo?.Refill();
        }

        private void HandleLayoutChanged(ModuleInstance _)
        {
            if (Instance != null) OnShipLayoutChanged();
        }

        private void Update()
        {
            if (Instance == null || !Instance.IsOperational) return;
            Tick(Time.deltaTime);
        }

        /// <summary>전투 기록에 쓸 이름: "CIWS (2,-1)".</summary>
        protected string LogName => Instance != null && Instance.Definition != null
            ? $"{Instance.Definition.DisplayName} ({Instance.Origin.X},{Instance.Origin.Z})"
            : GetType().Name;

        /// <summary>이 무기의 표적 분류별 효율(데이터). 1 = 기준, 0 = 공격하지 않음.</summary>
        protected float EfficiencyAgainst(Game.Combat.ITargetable target)
            => Definition != null ? Definition.TargetEfficiency.For(Game.Combat.TargetInfo.Category(target)) : 1f;

        /// <summary>효율을 반영한 한 발 피해.</summary>
        protected float DamageAgainst(Game.Combat.ITargetable target) => Stats.Damage * EfficiencyAgainst(target);

        /// <summary>초기화 훅. 참조 캐싱은 여기서 한다.</summary>
        protected virtual void OnInitialized() { }

        /// <summary>
        /// 어떤 모듈이든 설치·철거·파괴되면 호출된다. 이웃에 따라 달라지는 값(사격각, 탄약고 보너스)을
        /// 여기서 다시 계산한다. 구독 해제는 기반 클래스가 책임진다.
        /// </summary>
        protected virtual void OnShipLayoutChanged() { }

        /// <summary>모듈이 작동 가능할 때만 호출된다.</summary>
        protected virtual void Tick(float dt) { }

        /// <summary>
        /// 함선 전체 집계값에 자기 몫을 더한다(탐지거리, 전력용량, 이동성 등).
        /// ShipSystems.Recalculate에서만 호출된다.
        /// </summary>
        public virtual void ContributeToShipSystems(ShipSystems systems) { }

        /// <summary>HP가 0이 되었을 때. 이펙트 재생과 기능 정지를 처리한다.</summary>
        public virtual void OnModuleDestroyed() { }

        /// <summary>
        /// ModuleInstance.ApplyUpgrade에서만 호출된다(스탯은 이미 새 단계). 탄약은 남은 비율을 유지한 채 새 탄창 용량으로 바꾼다 —
        /// 쿨타임·표적·HP는 건드리지 않는다.
        /// </summary>
        public void HandleUpgraded()
        {
            if (this is Game.Combat.IAmmoUser user) user.Ammo?.Reconfigure(Stats);
            OnUpgraded(Instance != null ? Instance.UpgradeLevel : 0);
            if (Instance != null) OnShipLayoutChanged();   // 탄약고 보너스를 새 탄창 기준으로 다시 곱한다
        }

        /// <summary>
        /// 자리로 정해지는 형태(ModuleVariant)가 바뀌었을 때(ModuleInstance.SetVariant · 런타임 연결 직후).
        /// 형태별 모델로 갈아 끼우고 파생 모듈에 알린다.
        /// </summary>
        public void HandleVariantChanged()
        {
            if (Instance == null) return;
            var visual = ModuleVariantVisual.Apply(this, Instance.Variant, Instance.Sides);
            OnVariantChanged(Instance.Variant, visual);
        }

        /// <summary>형태가 바뀐 뒤(visual = 형태별 모델, 없으면 null — 기본 모델 유지). 발사점 재연결 등.</summary>
        protected virtual void OnVariantChanged(ModuleVariant variant, Transform visual) { }

        /// <summary>강화 단계가 올랐을 때(0 = 기본, 1 = 강화 I, 2 = 강화 II). 파생 모듈의 추가 처리용.</summary>
        public virtual void OnUpgraded(int newLevel) { }

        /// <summary>
        /// 업그레이드 외형이 교체된 뒤 호출된다. 회전식 무기는 새 피벗과 포구를 다시 연결한다.
        /// 비무장 모듈은 아무 작업도 하지 않는다.
        /// </summary>
        public virtual void BindUpgradeVisual(Transform visualRoot) { }

        protected static Transform FindVisualPart(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindVisualPart(root.GetChild(i), objectName);
                if (found != null) return found;
            }
            return null;
        }
    }
}

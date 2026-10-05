using Game.Modules;

namespace Game.Refit
{
    public enum RefitCardKind
    {
        Install,        // 새 블록 설치(배치 공간 필요)
        WeaponUpgrade,  // 장비 강화: 설치된 블록 하나를 골라 한 단계 강화(Module = null이면 통합 카드, 공간 불필요)
        Growth,         // 성장 카드: 공격력·연사력 같은 함선 전체 능력(공간 불필요)
        FleetDeploy,    // 편대 배치: 역할 없는 고속정 1척을 고른 편대 슬롯(1~4)에 합류
        FleetUpgrade,   // 편대 강화: 고른 슬롯 호위함의 역할 지정(역할이 없으면) 또는 개량
    }

    /// <summary>정비 화면에 올라오는 카드 한 장. 종류에 따라 쓰는 필드가 다르다.</summary>
    public sealed class RefitCard
    {
        public RefitCardKind Kind;

        /// <summary>Install: 설치할 블록. WeaponUpgrade: null = 통합 장비 강화 카드(어느 블록이든), 값이 있으면 그 블록만.</summary>
        public ModuleDefinition Module;

        /// <summary>Growth: 올리는 능력과 희귀도.</summary>
        public RunStat Stat;
        public CardTier Tier;

        /// <summary>보스 격침 보상으로 보장된 상위 카드.</summary>
        public bool BossReward;

        /// <summary>FleetUpgrade: 편대 슬롯 화면에서 처음 가리킬 호위함 번호(-1 = 정하지 않음). 대상은 플레이어가 슬롯에서 고른다.</summary>
        public int EscortIndex = -1;

        public static RefitCard Install(ModuleDefinition def) => new() { Kind = RefitCardKind.Install, Module = def };
        public static RefitCard WeaponUpgrade(ModuleDefinition def) => new() { Kind = RefitCardKind.WeaponUpgrade, Module = def };
        /// <summary>통합 장비 강화 카드: 고르면 설치된 블록 중 강화할 것을 고른다(2026-10-03 — 블록·단계별 카드를 하나로).</summary>
        public static RefitCard EquipmentUpgrade() => WeaponUpgrade(null);
        public static RefitCard Growth(RunStat stat, CardTier tier, bool boss = false)
            => new() { Kind = RefitCardKind.Growth, Stat = stat, Tier = tier, BossReward = boss };
        public static RefitCard FleetDeploy() => new() { Kind = RefitCardKind.FleetDeploy };
        public static RefitCard FleetUpgrade(int escortIndex = -1) => new() { Kind = RefitCardKind.FleetUpgrade, EscortIndex = escortIndex };

        public string Label => Kind switch
        {
            RefitCardKind.Install => $"설치:{Module?.DisplayName}",
            RefitCardKind.WeaponUpgrade => Module != null ? $"강화:{Module.DisplayName}" : "강화:장비",
            RefitCardKind.FleetDeploy => "편대:배치",
            RefitCardKind.FleetUpgrade => "편대:강화",
            _ => $"성장:{RunUpgrades.Definition(Stat)?.Name}({RunUpgrades.TierName(Tier)})",
        };
    }
}

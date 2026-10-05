using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Modules
{
    /// <summary>
    /// 블록 강화 데이터(블록 종류 하나당 에셋 하나, Resources/Upgrades).
    /// 대상은 ModuleDefinition으로 정한다 — ModuleType은 예전 에셋(장갑·발전기)과 값이 겹쳐 안전하지 않다.
    ///
    /// 단계 목록의 0번 = 강화 I, 1번 = 강화 II. 각 단계의 배율은 **기본값 대비**다(강화 I 값에 다시 곱하지 않는다).
    /// 배율 0 이하는 "변경 없음(1)"으로 읽는다.
    /// 외형은 모듈 프리팹 안의 ModuleUpgradeVisuals(Visual_Level1~3)가 맡는다 — 여기에는 수치·문구·아이콘만 둔다.
    /// </summary>
    [CreateAssetMenu(menuName = "Naval/Module Upgrade Profile", fileName = "UPG_New")]
    public class ModuleUpgradeProfile : ScriptableObject
    {
        /// <summary>이번 구현의 최대 강화 단계(강화 II).</summary>
        public const int MaxSupportedLevel = 2;

        [SerializeField] private ModuleDefinition module;
        [SerializeField] private List<ModuleUpgradeStage> stages = new();

        public ModuleDefinition Module => module;
        public int MaxLevel => Mathf.Min(MaxSupportedLevel, stages != null ? stages.Count : 0);

        /// <summary>강화 단계(1부터). 없으면 null.</summary>
        public ModuleUpgradeStage GetStage(int upgradeLevel)
            => stages != null && upgradeLevel >= 1 && upgradeLevel <= MaxLevel ? stages[upgradeLevel - 1] : null;

#if UNITY_EDITOR
        /// <summary>에디터 빌더 전용.</summary>
        public void EditorSetup(ModuleDefinition def, List<ModuleUpgradeStage> newStages)
        {
            module = def;
            stages = newStages;
        }
#endif
    }

    [Serializable]
    public class ModuleUpgradeStage
    {
        [SerializeField] private string displayName;
        [SerializeField, TextArea(1, 3)] private string description;
        [SerializeField] private ModuleStatModifier modifier = new();
        [SerializeField] private Sprite icon;

        public string DisplayName => displayName;
        public string Description => description;
        public ModuleStatModifier Modifier => modifier;
        public Sprite Icon => icon;

        public ModuleUpgradeStage() { }

        public ModuleUpgradeStage(string name, string desc, ModuleStatModifier mod)
        {
            displayName = name;
            description = desc;
            modifier = mod;
        }
    }

    /// <summary>
    /// 단계별 스탯 배율(기본값 대비). 필요한 항목만 1이 아닌 값을 넣는다. 0 이하 = 변경 없음.
    ///   damage → 피해 · range → 사거리 · cooldown → 발사 간격(ReloadTime) · magazine → 탄창 용량
    ///   ammoReload → 탄약 보급·전량 재장전 시간(AmmoReloadTime) · turnSpeed → 포탑 선회·추적
    ///   supply → 탄약고 지원량(발사 간격 단축·용량·보급 보너스 자체) · explosionDamage → 유폭 피해
    ///   repair → 수리량 · detection → 탐지 거리
    /// </summary>
    [Serializable]
    public class ModuleStatModifier
    {
        public float damageMultiplier = 1f;
        public float rangeMultiplier = 1f;
        public float cooldownMultiplier = 1f;
        public float magazineMultiplier = 1f;
        public float ammoReloadMultiplier = 1f;
        public float turnSpeedMultiplier = 1f;
        public float supplyMultiplier = 1f;
        public float explosionDamageMultiplier = 1f;
        public float repairMultiplier = 1f;
        public float detectionMultiplier = 1f;
    }
}

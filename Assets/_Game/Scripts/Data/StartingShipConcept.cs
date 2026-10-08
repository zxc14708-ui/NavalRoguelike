using UnityEngine;
using Game.Modules;

namespace Game.Data
{
    /// <summary>시작 함선 디자인의 종류. 저장된 에셋 호환성을 위해 값을 바꾸지 않는다.</summary>
    public enum StartShipKind
    {
        Patrol = 0,
        Command = 1,
        Assault = 2,
        MissileDestroyer = 3,
    }

    /// <summary>
    /// 시작 함선의 함교, 배치, 미리보기와 의도한 성능을 한 에셋에 묶는다.
    /// 출항 선택은 StartLoadout과 InitialEscortCount를 적용한다.
    /// 함급별 성능 배율·무기 제한·최종 호위함 한도는 후속 구현용 명세다.
    /// </summary>
    [CreateAssetMenu(menuName = "Naval/Starting Ship Concept", fileName = "ShipConcept_")]
    public class StartingShipConcept : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id;
        [SerializeField] private string title;
        [SerializeField, TextArea(3, 8)] private string description;
        [SerializeField] private StartShipKind kind;
        [SerializeField] private Color color = UnityEngine.Color.cyan;

        [Header("Native Assets")]
        [SerializeField] private StartingLoadout startLoadout;
        [SerializeField] private StartingLoadout expandedLoadout;
        [SerializeField] private ModuleDefinition bridgeDefinition;
        [SerializeField] private GameObject startPreviewPrefab;
        [SerializeField] private GameObject expandedPreviewPrefab;

        [Header("Playable Start")]
        [SerializeField, TextArea(2, 5)] private string startSummary;
        [SerializeField, Range(0, 4)] private int initialEscortCount;
        public string StartSummary => startSummary;
        public int InitialEscortCount => initialEscortCount;

        [Header("Intended Hull Multipliers — Not Applied")]
        [SerializeField, Min(0f)] private float hpMultiplier = 1f;
        [Tooltip("후속 방어력 집계 설계에 쓰는 배율. 현재 DamageReduction에는 적용하지 않는다.")]
        [SerializeField, Min(0f)] private float armorMultiplier = 1f;
        [SerializeField, Min(0f)] private float speedMultiplier = 1f;
        [SerializeField, Min(0f)] private float turnMultiplier = 1f;

        [Header("Intended Projectile Multipliers — Not Applied")]
        [SerializeField, Min(0f)] private float projectileDamageMultiplier = 1f;
        [SerializeField, Min(0f)] private float projectileRangeMultiplier = 1f;
        [Tooltip("발사 횟수/초의 배율. 향후 실제 발사 간격에는 역수를 적용해야 한다.")]
        [SerializeField, Min(0.01f)] private float projectileFireRateMultiplier = 1f;

        [Header("Intended Missile Multipliers — Not Applied")]
        [SerializeField, Min(0f)] private float missileDamageMultiplier = 1f;
        [SerializeField, Min(0f)] private float missileRangeMultiplier = 1f;
        [Tooltip("재장전 시간의 배율. 1 미만이면 재장전 시간이 짧아진다.")]
        [SerializeField, Min(0.01f)] private float missileReloadMultiplier = 1f;

        [Header("Intended Aviation Multipliers — Not Applied")]
        [SerializeField, Min(0f)] private float helicopterDamageMultiplier = 1f;
        [Tooltip("출격 쿨타임의 배율. 1 미만이면 쿨타임이 짧아진다.")]
        [SerializeField, Min(0.01f)] private float helicopterCooldownMultiplier = 1f;

        [Header("Intended Loadout Rules — Not Applied")]
        [SerializeField, Min(0)] private int escortStart;
        [SerializeField, Min(0)] private int escortMax;
        [Tooltip("허용할 무기 타입. 보조/센서/함교 타입을 제한하는 목록이 아니다.")]
        [SerializeField] private ModuleType[] allowedWeapons = System.Array.Empty<ModuleType>();
        [Tooltip("컨셉상 필요한 후속 블록과 구현 작업. 현재 게임 기능을 보장하지 않는다.")]
        [SerializeField] private string[] futureBlocks = System.Array.Empty<string>();

        [Header("Implementation Status")]
        [Tooltip("함선 선택/기본 배치/초기 호위함은 적용됨. 고정 함급 배율·무기 제한·최종 편대 규칙까지 모두 구현한 뒤 true로 바꾼다.")]
        [SerializeField] private bool mechanicsImplemented = false;

        public string Id => id;
        public string Title => title;
        public string Description => description;
        public StartShipKind Kind => kind;
        public Color Color => color;
        public StartingLoadout StartLoadout => startLoadout;
        public StartingLoadout ExpandedLoadout => expandedLoadout;
        public ModuleDefinition BridgeDefinition => bridgeDefinition;
        public GameObject StartPreviewPrefab => startPreviewPrefab;
        public GameObject ExpandedPreviewPrefab => expandedPreviewPrefab;
        public float HpMultiplier => hpMultiplier;
        public float ArmorMultiplier => armorMultiplier;
        public float SpeedMultiplier => speedMultiplier;
        public float TurnMultiplier => turnMultiplier;
        public float ProjectileDamageMultiplier => projectileDamageMultiplier;
        public float ProjectileRangeMultiplier => projectileRangeMultiplier;
        public float ProjectileFireRateMultiplier => projectileFireRateMultiplier;
        public float MissileDamageMultiplier => missileDamageMultiplier;
        public float MissileRangeMultiplier => missileRangeMultiplier;
        public float MissileReloadMultiplier => missileReloadMultiplier;
        public float HelicopterDamageMultiplier => helicopterDamageMultiplier;
        public float HelicopterCooldownMultiplier => helicopterCooldownMultiplier;
        public int EscortStart => escortStart;
        public int EscortMax => escortMax;
        public System.Collections.Generic.IReadOnlyList<ModuleType> AllowedWeapons => allowedWeapons;
        public System.Collections.Generic.IReadOnlyList<string> FutureBlocks => futureBlocks;
        public bool MechanicsImplemented => mechanicsImplemented;
    }
}

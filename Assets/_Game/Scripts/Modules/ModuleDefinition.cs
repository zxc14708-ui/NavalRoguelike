using UnityEngine;

namespace Game.Modules
{
    /// <summary>
    /// 모듈의 정적 데이터. 런타임 상태(HP, 쿨다운 등)는 절대 여기에 두지 않는다.
    /// 밸런스 값은 전부 이 에셋에서 조절한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Naval/Module Definition", fileName = "MOD_")]
    public class ModuleDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = "mod_new";
        [SerializeField] private string displayName = "New Module";
        [SerializeField, TextArea(2, 4)] private string description;
        [SerializeField] private ModuleType type;
        [SerializeField] private Sprite icon;

        [Header("Footprint")]
        [SerializeField, Min(1)] private int width = 1;    // 선수-선미 방향 칸 수
        [SerializeField, Min(1)] private int height = 1;   // 좌현-우현 방향 칸 수
        [SerializeField] private bool canRotate = true;
        [SerializeField] private PlacementRule placement = PlacementRule.Anywhere;

        [Tooltip("갑판 위로 솟은 정도. 이웃 무기의 사격각을 가리는지 결정한다.")]
        [SerializeField] private ModuleHeight heightClass = ModuleHeight.Mid;

        [Header("Limits")]
        [Tooltip("함선에 설치할 수 있는 최대 개수. 0이면 제한 없음.")]
        [SerializeField, Min(0)] private int maxCount = 0;

        [Header("Durability")]
        [SerializeField, Min(1f)] private float maxHp = 30f;

        [Header("Draft")]
        [SerializeField] private Rarity rarity = Rarity.Common;
        [SerializeField, Min(0f)] private float weight = 1f;

        [Header("Visual")]
        [Tooltip("함선 위에 실제로 붙는 3D 프리팹. ModuleRuntime 파생 컴포넌트를 반드시 포함할 것.")]
        [SerializeField] private GameObject prefab;

        [Header("Type Payload")]
        [Tooltip("무기/센서/기관 등 타입별 수치. 해당 모듈만 참조한다.")]
        [SerializeField] private ModuleStats stats;

        [Tooltip("표적 분류별 효율. 1 = 기준, 0 = 공격하지 않음. 표적 선택과 피해 배율에 쓴다.")]
        [SerializeField] private Game.Combat.TargetEfficiency targetEfficiency = Game.Combat.TargetEfficiency.Neutral;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public ModuleType Type => type;
        public Sprite Icon => icon;

        public int Width => width;
        public int Height => height;
        public int CellCount => width * height;
        public bool CanRotate => canRotate;
        public PlacementRule Placement => placement;
        public ModuleHeight HeightClass => heightClass;

        public int MaxCount => maxCount;
        public float MaxHp => maxHp;

        public Rarity Rarity => rarity;
        public float Weight => weight;

        public GameObject Prefab => prefab;
        public ModuleStats Stats => stats;
        public Game.Combat.TargetEfficiency TargetEfficiency => targetEfficiency;

        private void OnValidate()
        {
            if (prefab == null)
                Debug.LogWarning($"[ModuleDefinition:{name}] Prefab이 비어 있습니다.", this);
        }
    }

    /// <summary>
    /// 타입별 수치를 한 구조체에 모은다. 사용하지 않는 필드는 해당 모듈이 무시한다.
    /// </summary>
    [System.Serializable]
    public struct ModuleStats
    {
        [Header("Weapon (Autocannon / VLS / CIWS)")]
        [Tooltip("최대 교전거리")]
        public float Range;
        [Tooltip("최소 교전거리. 이보다 가까운 표적은 다른 무기에 맡긴다(근·중·장거리 분담).")]
        public float MinRange;
        public float Damage;
        public float ReloadTime;
        public float ProjectileSpeed;
        [Tooltip("사격 가능 각도(도). 360이면 전방향.")]
        public float FireArcDegrees;
        public float TurretTurnRate;

        [Header("Ammo (0 = 무한 탄약)")]
        [Tooltip("탄약 계통. 탄약고는 Gun 계통만 지원한다.")]
        public AmmoFamily AmmoFamily;
        [Tooltip("탄창·셀 수. 0이면 탄약 제한 없음(예전 동작).")]
        public int MagazineCapacity;
        [Tooltip("한 발에 쓰는 탄약. 0이면 1.")]
        public int AmmoPerShot;
        [Tooltip("보급 한 번에 채우는 양. 0이면 '비면 전량 재장전' 방식(CIWS) — 재장전 중에는 쏠 수 없다.")]
        public int AmmoReloadAmount;
        [Tooltip("보급 간격(초). 전량 재장전 방식이면 재장전에 걸리는 시간.")]
        public float AmmoReloadTime;

        [Header("Detection (Radar / Sonar)")]
        public float DetectionRange;
        public int ExtraTrackedTargets;

        [Header("Support (Magazine)")]
        [Tooltip("지원 반경(맨해튼 거리)")]
        public int SupportRadiusCells;
        [Tooltip("재장전 단축 비율. 0.25 = 25% 단축")]
        public float ReloadBonus;
        [Tooltip("지원 반경 안 포탄 무기(기관포·76mm)의 탄약 용량 증가 비율. 0.5 = +50%")]
        public float AmmoCapacityBonus;
        [Tooltip("지원 반경 안 포탄 무기의 보급 속도 증가 비율. 0.25 = 25% 빨리")]
        public float ResupplyBonus;
        [Tooltip("탄약고 유폭 피해(파괴된 칸 기준, 거리에 따라 줄어든다)")]
        public float CookOffDamage;

        [Header("Repair (Repair Bay)")]
        [Tooltip("받는 피해 감소 비율. 0.1 = 10% 감소")]
        public float DamageReduction;
        public float HullRepairPerSecond;
        [Tooltip("초당 모듈 회복량. 파괴된 모듈은 복구하지 않는다.")]
        public float ModuleRepairPerSecond;

        [Header("Aviation (Helicopter Deck)")]
        public float SortieCooldown;
        public float HelicopterSpeed;
    }
}

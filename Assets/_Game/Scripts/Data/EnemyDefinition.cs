using UnityEngine;
using Game.Combat;

namespace Game.Data
{
    /// <summary>등급은 적의 역할 표시이며, 체력·피해 배율을 자동으로 바꾸지 않는다.</summary>
    public enum EnemyRank { Normal, Elite, Boss }

    /// <summary>적 한 종류의 정적 데이터. 실제 행동은 EnemyController 파생 클래스가 담당한다.</summary>
    [CreateAssetMenu(menuName = "Naval/Enemy Definition", fileName = "ENE_")]
    public class EnemyDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = "ene_new";
        [SerializeField] private string displayName = "Enemy";
        [SerializeField] private TargetKind kind = TargetKind.Surface;
        [SerializeField] private GameObject prefab;

        [Header("Classification")]
        [Tooltip("표적 분류. 무기별 효율(피해 배율·우선순위)이 이것으로 정해진다. Unspecified면 Kind로 추정.")]
        [SerializeField] private TargetCategory category = TargetCategory.Unspecified;
        [SerializeField] private EnemyRank rank = EnemyRank.Normal;
        [Tooltip("표적 가치. VLS처럼 탄이 한정된 무기가 낮은 가치의 표적에 쓰지 않게 한다. 0이면 분류 기본값.")]
        [SerializeField, Min(0)] private int targetValue;
        [Tooltip("웨이브에서 이 적이 동시에 살아 있을 수 있는 최대 수(0 = 제한 없음). 엘리트가 한꺼번에 몰려 급사하는 것을 막는다.")]
        [SerializeField, Min(0)] private int maxAlive;

        [Header("Stats")]
        [SerializeField] private float maxHp = 20f;
        [SerializeField] private float moveSpeed = 12f;
        [SerializeField] private float turnRateDegPerSec = 120f;

        [Header("Attack")]
        [Tooltip("이 거리까지 접근한다. 미사일정은 이 거리를 유지한다.")]
        [SerializeField] private float preferredRange = 6f;
        [SerializeField] private float attackDamage = 8f;
        [SerializeField] private float attackCooldown = 1.5f;
        [Tooltip("미사일을 쏘는 적만 사용")]
        [SerializeField] private GameObject missilePrefab;
        [Tooltip("한 번 공격에 쏘는 미사일 수(포화 공격). 1이면 한 발씩.")]
        [SerializeField, Min(1)] private int salvoSize = 1;
        [Tooltip("일제사격 발 사이 간격(초)")]
        [SerializeField, Min(0f)] private float salvoSpacing = 0.15f;

        [Header("Reward")]
        [Tooltip("격침 시 주는 경험치")]
        [SerializeField, Min(0)] private int xpReward = 1;

        [Header("Stealth (Submarine)")]
        [Tooltip("소나/헬기에 발각된 뒤 유지되는 노출 시간")]
        [SerializeField] private float revealDuration = 6f;

        public string Id => id;
        public string DisplayName => displayName;
        public TargetKind Kind => kind;
        public GameObject Prefab => prefab;

        public float MaxHp => maxHp;
        public float MoveSpeed => moveSpeed;
        public float TurnRateDegPerSec => turnRateDegPerSec;

        public float PreferredRange => preferredRange;
        public float AttackDamage => attackDamage;
        public float AttackCooldown => attackCooldown;
        public GameObject MissilePrefab => missilePrefab;
        public int SalvoSize => Mathf.Max(1, salvoSize);
        public float SalvoSpacing => salvoSpacing;
        public TargetCategory Category => category;
        public EnemyRank Rank => rank;
        public int TargetValue => targetValue;
        /// <summary>웨이브 동시 최대 수(0 = 제한 없음).</summary>
        public int MaxAlive => maxAlive;

        [Header("Carrier (항공전함 보스)")]
        [Tooltip("비행갑판에서 편대로 띄우는 적")]
        [SerializeField] private EnemyDefinition launchedDrone;
        [Tooltip("격추돼도 다시 띄우는 정찰기")]
        [SerializeField] private EnemyDefinition launchedRecon;

        [Tooltip("3단계에서 드론과 번갈아 띄우는 전투기")]
        [SerializeField] private EnemyDefinition launchedFighter;

        public EnemyDefinition LaunchedFighter => launchedFighter;
        public EnemyDefinition LaunchedDrone => launchedDrone;
        public EnemyDefinition LaunchedRecon => launchedRecon;

        public float RevealDuration => revealDuration;
        public int XpReward => xpReward;
    }
}

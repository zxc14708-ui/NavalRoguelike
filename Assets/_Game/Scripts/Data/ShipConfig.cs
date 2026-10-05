using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 플레이어 함선의 시작 수치. const로 박지 않고 전부 여기서 조절한다.
    /// 명세의 "Prototype 기본 밸런스" 값을 기본값으로 넣어 둔다.
    /// </summary>
    [CreateAssetMenu(menuName = "Naval/Ship Config", fileName = "ShipConfig")]
    public class ShipConfig : ScriptableObject
    {
        [Header("Hull")]
        [SerializeField] private float hullMaxHp = 100f;

        [Header("Mobility")]
        [SerializeField] private float baseMaxSpeed = 10f;
        [SerializeField] private float baseTurnRateDegPerSec = 90f;
        [SerializeField] private float acceleration = 12f;
        [SerializeField] private float deceleration = 8f;

        [Header("Handling")]
        [Tooltip("후진 최고속도 비율. 0.4면 전진 최고속도의 40%")]
        [Range(0f, 1f)][SerializeField] private float reverseSpeedRatio = 0.4f;

        [Tooltip("타가 완전히 듣기 시작하는 속력 비율. 이보다 느리면 선회가 둔해진다. " +
                 "정지 상태에서는 제자리 선회가 되지 않는다.")]
        [Range(0.05f, 1f)][SerializeField] private float rudderFullEffectSpeedRatio = 0.35f;

        [Tooltip("정지에 가까울 때도 남는 최소 선회 능력. 0이면 완전히 못 돈다.")]
        [Range(0f, 0.5f)][SerializeField] private float minRudderEffect = 0.12f;

        [Header("Helm (실제 배식 조함)")]
        [Tooltip("최대 타각(도). HUD 표시와 선회율 계산의 기준")]
        [Range(5f, 45f)][SerializeField] private float maxRudderAngle = 35f;
        [Tooltip("타를 중앙에서 최대 타각까지 돌리는 데 걸리는 시간(초)")]
        [Min(0.05f)][SerializeField] private float rudderShiftTime = 1.2f;
        [Tooltip("켜면 A/D를 떼도 타각이 그대로 남는다(실제 조타). 끄면 떼는 순간 중앙으로 돌아간다.")]
        [SerializeField] private bool rudderHoldsPosition = true;
        [Tooltip("선회율이 타각에 맞춰지는 시간(초). 클수록 늦게 돌기 시작하고 타를 풀어도 한동안 계속 돈다.")]
        [Min(0.01f)][SerializeField] private float turnResponseTime = 0.9f;
        [Tooltip("최대 타각으로 선회할 때 잃는 속력 비율")]
        [Range(0f, 0.6f)][SerializeField] private float turnSpeedLoss = 0.2f;
        [Tooltip("기관 전령기 단계(최고속력 비율). 음수는 후진(후진 최고속도 비율을 곱함). W/S로 한 칸씩 옮긴다.")]
        [SerializeField] private float[] engineOrders = { -1f, 0f, 0.25f, 0.5f, 0.75f, 1f };

        [Header("Detection")]
        [SerializeField] private float baseDetectionRange = 20f;
        [SerializeField] private int baseTrackedTargets = 3;

        [Header("Task Force Command")]
        [Tooltip("전투단 지원 전력을 편성할 때 사용할 수 있는 기본 지휘 점수")]
        [SerializeField, Min(0)] private int baseCommandCapacity = 6;

        public float HullMaxHp => hullMaxHp;
        public float BaseMaxSpeed => baseMaxSpeed;
        public float BaseTurnRateDegPerSec => baseTurnRateDegPerSec;
        public float Acceleration => acceleration;
        public float Deceleration => deceleration;
        public float ReverseSpeedRatio => reverseSpeedRatio;
        public float RudderFullEffectSpeedRatio => rudderFullEffectSpeedRatio;
        public float MinRudderEffect => minRudderEffect;
        public float MaxRudderAngle => maxRudderAngle;
        public float RudderShiftTime => rudderShiftTime;
        public bool RudderHoldsPosition => rudderHoldsPosition;
        public float TurnResponseTime => turnResponseTime;
        public float TurnSpeedLoss => turnSpeedLoss;
        public float[] EngineOrders => engineOrders != null && engineOrders.Length > 0 ? engineOrders : DefaultOrders;
        private static readonly float[] DefaultOrders = { -1f, 0f, 0.25f, 0.5f, 0.75f, 1f };
        public float BaseDetectionRange => baseDetectionRange;
        public int BaseTrackedTargets => baseTrackedTargets;
        public int BaseCommandCapacity => baseCommandCapacity;
    }
}

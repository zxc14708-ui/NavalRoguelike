using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 함종의 실제 수치(WWII 전환 패치 C1, 2026-10-18 — 패치 A 7장 "WWII 선체 정의: 실제 이동/방호 수치 · capacityBudget").
    /// 시작 함정(<see cref="StartingShipConcept.HullProfile"/>)이 이것을 가리키면 출항 전 선택 때 함선에 적용된다:
    /// 최고속력·선회·가감속·선회 관성 → ShipController, 선체 내구·장갑 → ShipController·DamageResolver,
    /// 격자 크기·탑재 예산 → ShipGrid(설치 판정). 비어 있으면(현대 함정) ShipConfig와 씬 격자 그대로 — 현대 균형은 바뀌지 않는다.
    /// 수치는 역사 제원이 아니라 게임 균형 가설이다.
    /// </summary>
    [CreateAssetMenu(menuName = "Naval/Hull Profile", fileName = "Hull_")]
    public sealed class HullProfile : ScriptableObject
    {
        [Header("Mobility")]
        [Tooltip("최고속력(m/s). 1 m/s = 1.944 kn")]
        [SerializeField, Min(1f)] private float maxSpeed = 15.4333f;
        [Tooltip("최대 타각일 때 선회율(°/초)")]
        [SerializeField, Min(1f)] private float turnRate = 40f;
        [SerializeField, Min(0.1f)] private float acceleration = 7.7f;
        [SerializeField, Min(0.1f)] private float deceleration = 5.4f;
        [Tooltip("선회율이 타각을 따라가는 시간(초). 클수록 무겁게 돈다")]
        [SerializeField, Min(0.01f)] private float turnResponseTime = 0.9f;

        [Header("Protection")]
        [SerializeField, Min(1f)] private float hullHp = 100f;
        [Tooltip("장갑: 선체가 받는 피해를 이 비율만큼 줄인다(블록 피해 감소와 곱한다)")]
        [SerializeField, Range(0f, 0.5f)] private float armor;

        [Header("Hull Size · Budget")]
        [Tooltip("함교 기준 선수/선미 방향 최대 칸 수(0이면 씬 격자 그대로)")]
        [SerializeField, Min(0)] private int halfLength;
        [Tooltip("함교 기준 좌현/우현 방향 최대 칸 수(0이면 씬 격자 그대로)")]
        [SerializeField, Min(0)] private int halfBeam;
        [Tooltip("탑재 예산(t). 설치된 블록 톤수의 합이 이것을 넘을 수 없다. 0이면 제한 없음")]
        [SerializeField, Min(0f)] private float tonnageBudget;
        [Tooltip("허용하는 가장 큰 포탑 마운트 등급(WWII 패치 C2). 구축함·순양함 = 중포탑, 전함 = 대구경 주포탑")]
        [SerializeField] private Game.Modules.MountClass maxMountClass = Game.Modules.MountClass.Heavy;

        public float MaxSpeed => maxSpeed;
        public float TurnRate => turnRate;
        public float Acceleration => acceleration;
        public float Deceleration => deceleration;
        public float TurnResponseTime => turnResponseTime;
        public float HullHp => hullHp;
        public float Armor => armor;
        public int HalfLength => halfLength;
        public int HalfBeam => halfBeam;
        public float TonnageBudget => tonnageBudget;
        public Game.Modules.MountClass MaxMountClass => maxMountClass;
    }
}

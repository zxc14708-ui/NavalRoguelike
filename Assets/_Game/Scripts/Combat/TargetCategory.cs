using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// 표적 분류(크기·종류). 무기가 "어떤 표적에 효율적인가"를 정하는 기준이다.
    /// 조회 범위를 고르는 TargetClass(수상·잠수·항공·미사일 플래그)와는 다르다.
    /// </summary>
    public enum TargetCategory
    {
        Unspecified,    // 데이터가 비어 있으면 TargetKind로 추정
        SmallSurface,   // 고속정
        MediumSurface,  // 미사일정
        LargeSurface,   // 대형함
        Boss,
        Submarine,
        Drone,
        Air,            // 전투기·정찰기
        Missile,
        Decoy,
    }

    /// <summary>
    /// 무기의 분류별 효율. 1 = 기준, 0 = 공격하지 않음.
    /// 표적 선택 점수(효율이 낮으면 뒤로)와 피해 배율에 함께 쓴다.
    /// </summary>
    [System.Serializable]
    public struct TargetEfficiency
    {
        public float SmallSurface;
        public float MediumSurface;
        public float LargeSurface;
        public float Boss;
        public float Submarine;
        public float Drone;
        public float Air;
        public float Missile;

        /// <summary>모든 분류 1.0(예전 동작).</summary>
        public static TargetEfficiency Neutral => new()
        {
            SmallSurface = 1f, MediumSurface = 1f, LargeSurface = 1f, Boss = 1f,
            Submarine = 1f, Drone = 1f, Air = 1f, Missile = 1f,
        };

        public float For(TargetCategory c) => c switch
        {
            TargetCategory.SmallSurface => SmallSurface,
            TargetCategory.MediumSurface => MediumSurface,
            TargetCategory.LargeSurface => LargeSurface,
            TargetCategory.Boss => Boss,
            TargetCategory.Submarine => Submarine,
            TargetCategory.Drone => Drone,
            TargetCategory.Air => Air,
            TargetCategory.Missile => Missile,
            _ => 1f,
        };
    }

    /// <summary>표적의 분류와 가치(VLS 같은 한정 탄약 무기가 낭비하지 않도록).</summary>
    public static class TargetInfo
    {
        public static TargetCategory Category(ITargetable t)
        {
            if (t == null) return TargetCategory.Unspecified;
            if (t is Game.Enemies.EnemyController e && e.Definition != null && e.Definition.Category != TargetCategory.Unspecified)
                return e.Definition.Category;

            return t.Kind switch
            {
                TargetKind.Missile => TargetCategory.Missile,
                TargetKind.Decoy => TargetCategory.Decoy,
                TargetKind.Submarine => TargetCategory.Submarine,
                TargetKind.Aircraft => TargetCategory.Air,
                _ => TargetCategory.MediumSurface,
            };
        }

        /// <summary>표적 가치. 데이터에 없으면 분류 기본값(소형 1 · 중형 3 · 대형 5 · 보스 10 · 잠수함 3 · 드론 1 · 항공 2).</summary>
        public static int Value(ITargetable t)
        {
            if (t is Game.Enemies.EnemyController e && e.Definition != null && e.Definition.TargetValue > 0)
                return e.Definition.TargetValue;
            return DefaultValue(Category(t));
        }

        public static int DefaultValue(TargetCategory c) => c switch
        {
            TargetCategory.SmallSurface => 1,
            TargetCategory.MediumSurface => 3,
            TargetCategory.LargeSurface => 5,
            TargetCategory.Boss => 10,
            TargetCategory.Submarine => 3,
            TargetCategory.Drone => 1,
            TargetCategory.Air => 2,
            _ => 0,
        };

        /// <summary>효율 표를 적용한 점수: 거리² ÷ 효율². 효율 0이면 제외(float.MaxValue).</summary>
        public static float Weighted(float sqrDistance, float efficiency)
            => efficiency <= 0.001f ? float.MaxValue : sqrDistance / (efficiency * efficiency);
    }
}

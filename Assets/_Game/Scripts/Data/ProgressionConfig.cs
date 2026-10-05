using System.Collections.Generic;
using UnityEngine;
using Game.Modules;

namespace Game.Data
{
    /// <summary>
    /// 레벨업 곡선과 카드로 뽑을 수 있는 모듈 목록.
    /// </summary>
    [CreateAssetMenu(menuName = "Naval/Progression Config", fileName = "ProgressionConfig")]
    public class ProgressionConfig : ScriptableObject
    {
        [Header("Draft Pool")]
        [SerializeField] private List<ModuleDefinition> pool = new();

        public IReadOnlyList<ModuleDefinition> Pool => pool;

        [Header("Level Curve")]
        [Tooltip("레벨 1→2에 필요한 경험치. 첫 레벨업이 20초 안팎에 오도록")]
        [SerializeField, Min(1)] private int xpBase = 5;
        [Tooltip("레벨마다 늘어나는 필요 경험치")]
        [SerializeField, Min(0)] private int xpStep = 4;

        [Header("Level-up Reward")]
        [Tooltip("레벨업마다 선체를 이만큼 수리한다")]
        [SerializeField, Min(0f)] private float hullRepairOnLevelUp = 8f;
        [Tooltip("레벨업마다 제시할 카드 수")]
        [SerializeField, Min(1)] private int cardCount = 3;

        public float HullRepairOnLevelUp => hullRepairOnLevelUp;
        public int CardCount => cardCount;

        /// <summary>현재 레벨에서 다음 레벨까지 필요한 경험치.</summary>
        public int XpRequired(int level) => xpBase + xpStep * Mathf.Max(0, level - 1);

    }
}

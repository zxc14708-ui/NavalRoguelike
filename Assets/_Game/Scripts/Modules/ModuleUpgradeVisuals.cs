using UnityEngine;

namespace Game.Modules
{
    /// <summary>
    /// 모듈 레벨에 맞는 완성형 외형 하나만 켠다.
    /// Level 1 = 기본형, Level 2 = U1, Level 3 이상 = U2.
    /// 무기 외형을 바꾼 뒤에는 새 조준 피벗과 포구를 런타임에 다시 연결한다.
    /// </summary>
    public sealed class ModuleUpgradeVisuals : MonoBehaviour
    {
        [SerializeField] private GameObject[] levels;
        private static readonly System.Collections.Generic.HashSet<string> s_warned = new();

        /// <summary>검증용: 지금 켜져 있는 외형 단계(0 = 기본형). 없으면 -1.</summary>
        public int ActiveIndex
        {
            get
            {
                if (levels == null) return -1;
                for (int i = 0; i < levels.Length; i++) if (levels[i] != null && levels[i].activeSelf) return i;
                return -1;
            }
        }

        public int VariantCount => levels != null ? levels.Length : 0;

        public void ApplyLevel(int level, ModuleRuntime runtime)
        {
            if (levels == null || levels.Length == 0) return;

            int activeIndex = Mathf.Clamp(level - 1, 0, levels.Length - 1);
            // 그 단계 외형이 없으면(모델 미제작) 가장 가까운 아래 단계, 없으면 기본형을 유지한다(경고는 한 번만)
            int wanted = activeIndex;
            while (activeIndex > 0 && levels[activeIndex] == null) activeIndex--;
            if (activeIndex != wanted && s_warned.Add($"{name}:{wanted}"))
                Debug.LogWarning($"[Upgrade] {name}: 강화 {wanted} 외형이 없어 {(activeIndex == 0 ? "기본" : $"강화 {activeIndex}")} 외형을 유지합니다.", this);
            GameObject active = null;
            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i] == null) continue;
                bool selected = i == activeIndex;
                levels[i].SetActive(selected);
                if (selected) active = levels[i];
            }

            if (active != null && runtime != null)
                runtime.BindUpgradeVisual(active.transform);
        }
    }
}

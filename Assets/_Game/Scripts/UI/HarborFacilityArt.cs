using UnityEngine;

namespace Game.UI
{
    /// <summary>시작 화면 군항의 세 기능 시설에 독립적인 정적 외형을 덧붙인다.</summary>
    public static class HarborFacilityArt
    {
        private const string ResourcePath = "Environment/HarborFacilityDetails";

        public static void Attach(Transform island)
        {
            if (island == null || island.Find("HarborFacilityDetails") != null) return;
            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning("[Harbor] 시설 외형을 찾을 수 없습니다: " + ResourcePath);
                return;
            }
            var instance = Object.Instantiate(prefab, island, false);
            instance.name = "HarborFacilityDetails";
        }
    }
}

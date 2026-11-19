using UnityEngine;
using Game.World;

namespace Game.View
{
    /// <summary>
    /// 낮과 밤 소품 모델(2026-11-19 Codex ArtSource/DayNight_v1 → Resources/Environment/DayNight).
    /// 항로 부표 빨강/초록 · 노란 등부표 · 조난 신호 부표 · 소노부이 · 낙하산 조명탄. 원점 = 수면선 중심, 등불 자리 = LampPoint.
    /// 모델이 없으면 null — 부르는 쪽이 예전 임시 외형으로 돌아간다.
    /// </summary>
    public static class DayNightProps
    {
        public const string Dir = "Environment/DayNight/";
        public const string ChannelRed = "ENV_ChannelBuoy_Red", ChannelGreen = "ENV_ChannelBuoy_Green", LightYellow = "ENV_LightBuoy_Yellow",
            Distress = "ENV_DistressBeacon", Sonobuoy = "PRJ_Sonobuoy", Flare = "FX_ParachuteFlare";

        /// <summary>parent 아래 local 자리에 모델을 놓는다(콜라이더 없음).</summary>
        public static GameObject Place(Transform parent, string name, Vector3 local, float yaw = 0f, float scale = 1f)
            => IslandBuilder.PlaceKitModel(parent, Dir + name, local, yaw, scale);

        /// <summary>이름 붙은 빈 노드 자리(go 자기 좌표). 없으면 fallback.</summary>
        public static Vector3 Socket(GameObject go, string socket, Vector3 fallback)
        {
            if (go == null) return fallback;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t.name == socket) return go.transform.InverseTransformPoint(t.position);
            return fallback;
        }

        /// <summary>모델의 LampPoint에 점멸 등불 하나.</summary>
        public static NightLampSet Lamp(GameObject go, Color color, float size, float period, float onFraction, float phase)
        {
            if (go == null) return null;
            var set = NightLampSet.On(go);
            set.Add(Socket(go, "LampPoint", Vector3.up * 2f / Mathf.Max(0.01f, go.transform.lossyScale.y)), color, size, period, onFraction, phase);
            return set;
        }
    }
}

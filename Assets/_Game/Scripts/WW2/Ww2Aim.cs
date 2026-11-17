using UnityEngine;
using Game.Ship;

namespace Game.WW2
{
    /// <summary>
    /// WWII 적의 조준 예측(2026-10-17 플레이 피드백 "플레이어를 더 노리게"). 예전에는 "지금 속력·침로 그대로"만 봤다 —
    /// 타를 쓴 채 돌고 있으면 늘 빗나갔다. 이제 지금 <b>선회율까지 유지</b>한다고 보고 원호를 따라 앞을 내다본다.
    /// 그래서 예고가 뜬 뒤에 <b>조함을 바꿔야</b>(속력을 바꾸거나 반대로 타를 쓰거나) 피한다. 같은 조함을 계속하면 맞는다.
    /// </summary>
    public static class Ww2Aim
    {
        /// <summary>seconds 뒤 기함이 있을 곳(수면, y = 0). 선회율은 이 시간 동안 그대로라고 본다.</summary>
        public static Vector3 Predict(Transform player, ShipController ship, float seconds)
        {
            if (player == null) return Vector3.zero;
            Vector3 p = player.position;
            p.y = 0f;
            if (ship == null || seconds <= 0f) return p;
            Vector3 fwd = player.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            float speed = ship.CurrentSpeed, yaw = ship.YawRate;
            const int steps = 12;
            float dt = seconds / steps;
            for (int i = 0; i < steps; i++)
            {
                fwd = Quaternion.Euler(0f, yaw * dt, 0f) * fwd;
                p += fwd * speed * dt;
            }
            return p;
        }

        /// <summary>seconds 뒤의 진행 방향(수평).</summary>
        public static Vector3 PredictHeading(Transform player, ShipController ship, float seconds)
        {
            Vector3 fwd = player != null ? player.forward : Vector3.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            return ship != null ? Quaternion.Euler(0f, ship.YawRate * seconds, 0f) * fwd : fwd;
        }
    }
}

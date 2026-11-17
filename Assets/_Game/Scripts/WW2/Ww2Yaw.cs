using UnityEngine;

namespace Game.WW2
{
    /// <summary>
    /// 모델 노드의 수평 선회(포탑·발사관). Codex 모델은 Model 노드가 Blender 축 변환(X 90°)을 품고 있어 안쪽 노드의 로컬 위(+Y)가
    /// 수평을 가리킨다 — 로컬 위 둘레로 돌리면 포탑이 옆으로 눕는다(2026-10-18 버그: 현측 어뢰 발사관·순양함 포탑).
    /// 그래서 방향·각도는 선체(hull) 공간의 수평면에서 재고, 선체 위 축을 노드 부모 공간으로 옮겨 그 둘레로 돌린다.
    /// </summary>
    public static class Ww2Yaw
    {
        /// <summary>월드 방향을 선체 공간 수평 방향으로(위 성분 제거, 정규화). 너무 짧으면 선체 앞.</summary>
        public static Vector3 Flat(Transform hull, Vector3 worldDir)
        {
            Vector3 d = hull.InverseTransformDirection(worldDir);
            d.y = 0f;
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
        }

        /// <summary>선체 공간 수평면에서 from → to 각도(위에서 보아 시계 방향 +).</summary>
        public static float Angle(Vector3 fromFlat, Vector3 toFlat) => Vector3.SignedAngle(fromFlat, toFlat, Vector3.up);

        /// <summary>쉬는 자세(restLocal)에서 선체 위 축 둘레로 yaw만큼 돈 로컬 회전.</summary>
        public static Quaternion Rotate(Transform node, Quaternion restLocal, float yaw, Transform hull)
        {
            Vector3 axis = node.parent != null ? node.parent.InverseTransformDirection(hull.up) : hull.up;
            return Quaternion.AngleAxis(yaw, axis) * restLocal;
        }
    }
}

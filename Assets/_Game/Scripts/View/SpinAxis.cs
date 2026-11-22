using System.Collections.Generic;
using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// 레이더 · 안테나 회전(2026-11-22). 예전에는 transform.Rotate(Vector3.up, …, Space.World) — 세계 수직축으로 돌렸는데,
    /// 배 모델이 파도에 흔들리면(부모가 기울면) 매 프레임 세계 축 회전이 부모 기준으로는 다른 축이 되어 안테나가 점점 비뚤어졌다
    /// (함교 레이더가 13° · 16°까지 기운 채 돌았다 — 레이더 검사 "안테나 회전 731° vs 스윕 707°").
    /// 처음 돌 때의 세계 위쪽을 부모 좌표로 한 번 기억해 그 축으로만 돈다 — 배가 흔들리면 안테나도 같이 흔들릴 뿐 비뚤어짐이 쌓이지 않는다.
    /// </summary>
    public static class SpinAxis
    {
        private static readonly Dictionary<Transform, Vector3> s_axis = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_axis.Clear();

        /// <summary>t를 (처음 돌 때의) 수직축 둘레로 degrees만큼 돌린다.</summary>
        public static void AboutUp(Transform t, float degrees)
        {
            if (t == null) return;
            if (!s_axis.TryGetValue(t, out var axis))
            {
                if (s_axis.Count > 512) s_axis.Clear();   // 사라진 물체가 쌓이지 않게(다시 처음부터 기억)
                axis = t.parent != null ? t.parent.InverseTransformDirection(Vector3.up).normalized : Vector3.up;
                s_axis[t] = axis;
            }
            t.localRotation = Quaternion.AngleAxis(degrees, axis) * t.localRotation;
        }
    }
}

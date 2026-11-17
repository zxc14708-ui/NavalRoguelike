using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// 수면선 맞추기(2026-10-13 점검 G06). 수면은 y −0.9, 기함의 흘수(수면 아래 깊이)는 약 0.37m인데 호위함·소형 적·정예 대부분은
    /// 모델 바닥이 수면 위 0.2~0.6m에 떠 있었다. 함정이 생길 때 모델만 내려 바닥이 수면 아래 Draft에 오게 한다.
    ///   - 내리기만 한다(이미 그만큼 잠긴 모델은 그대로) · 판정 상자·위치·로직은 그대로, 모델 묶음(ModelPivot 등)만 움직인다
    ///   - 기준 물체(reference)의 높이를 수면 기준선(y 0)으로 본다 — 출렁임(호위함 시각 묶음)이 있어도 같은 답
    ///   - 여러 번 불러도 같다(풀에서 다시 쓰는 적, 개량으로 모델을 바꾼 호위함)
    /// 프리팹은 고치지 않는다 — 빌더가 프리팹을 다시 만들어도, 새 모델이 들어와도 그대로 맞춰진다.
    /// </summary>
    public static class Waterline
    {
        public const float WaterY = -0.9f;
        public const float Draft = 0.35f;

        /// <summary>모델을 내려 바닥을 수면 아래 Draft로. 내린 거리(음수) — 그대로 두면 0.</summary>
        public static float Settle(Transform reference, Transform model)
        {
            if (reference == null || model == null) return 0f;
            bool any = false;
            float minY = float.MaxValue;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || !r.enabled) continue;
                any = true;
                minY = Mathf.Min(minY, r.bounds.min.y);
            }
            if (!any) return 0f;
            float bottom = minY - reference.position.y;          // 기준선(y 0)에서 본 바닥
            float shift = (WaterY - Draft) - bottom;
            if (shift > -0.05f) return 0f;                       // 이미 잠겨 있다(올리지 않는다)
            model.position += Vector3.up * shift;
            return shift;
        }
    }
}

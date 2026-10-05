using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 레이더 스윕. 로컬 위(+Y)가 빔 방향이고, 반시계 쪽(스윕이 지나온 쪽)으로 잔광 부채꼴이 흐려진다.
    /// 메시는 크기가 바뀔 때만 다시 만들고, 매 프레임에는 RectTransform 회전만 바꾼다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class RadarSweepGraphic : MaskableGraphic
    {
        [SerializeField] private float trailDegrees = 50f;
        [SerializeField, Range(0f, 1f)] private float trailAlpha = 0.38f;
        [SerializeField] private float beamWidth = 1.6f;
        [SerializeField, Min(4)] private int segments = 36;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            Vector2 center = r.center;
            float radius = Mathf.Min(r.width, r.height) * 0.5f;
            Color32 baseColor = color;

            // 잔광: 빔(수학 각 90°)에서 반시계로 trailDegrees만큼
            for (int i = 0; i < segments; i++)
            {
                float t0 = (float)i / segments, t1 = (float)(i + 1) / segments;
                float a0 = trailAlpha * (1f - t0) * (1f - t0);
                float a1 = trailAlpha * (1f - t1) * (1f - t1);
                Vector2 e0 = center + Dir(90f + trailDegrees * t0) * radius;
                Vector2 e1 = center + Dir(90f + trailDegrees * t1) * radius;

                int start = vh.currentVertCount;
                vh.AddVert(center, WithAlpha(baseColor, (a0 + a1) * 0.5f), Vector4.zero);
                vh.AddVert(e0, WithAlpha(baseColor, a0), Vector4.zero);
                vh.AddVert(e1, WithAlpha(baseColor, a1), Vector4.zero);
                vh.AddTriangle(start, start + 1, start + 2);
            }

            // 빔 선
            float hw = beamWidth * 0.5f;
            int b = vh.currentVertCount;
            Color32 beam = WithAlpha(baseColor, 1f);
            vh.AddVert(center + new Vector2(-hw, 0f), beam, Vector4.zero);
            vh.AddVert(center + new Vector2(-hw, radius), WithAlpha(baseColor, 0.85f), Vector4.zero);
            vh.AddVert(center + new Vector2(hw, radius), WithAlpha(baseColor, 0.85f), Vector4.zero);
            vh.AddVert(center + new Vector2(hw, 0f), beam, Vector4.zero);
            vh.AddTriangle(b, b + 1, b + 2);
            vh.AddTriangle(b, b + 2, b + 3);
        }

        private static Vector2 Dir(float mathDeg)
        {
            float rad = mathDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        private static Color32 WithAlpha(Color32 c, float a)
        {
            c.a = (byte)Mathf.Clamp(Mathf.RoundToInt(c.a * a), 0, 255);
            return c;
        }
    }
}

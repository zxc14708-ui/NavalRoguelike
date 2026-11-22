using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 레이더 화면의 섬(육지 반사, 2026-11-22 사용자: 섬이 둥근 점이 원형으로 늘어선 모양이라 하나의 섬으로 보이게).
    /// 예전에는 섬 중심과 해안선 표본점마다 흐린 점을 찍어 안이 빈 고리처럼 보였다. 이제 해안선(Islands.Rim)을 따라 채운 하나의 면으로 그린다.
    ///   - 섬 중심에서 해안선 점들로 부채꼴 삼각형(섬은 중심에서 본 별 모양이라 그대로 채워진다).
    ///   - 꼭짓점 밝기 = 스윕이 그 방위를 지난 뒤 흐른 시간: 막 지나간 쪽이 밝고(0.55) 한 바퀴 동안 옅어지되 바닥 0.16은 남는다 —
    ///     실제 레이더처럼 빔이 지나며 다시 칠하면서도 섬 모양은 늘 읽힌다.
    ///   - 표시 원(거리 눈금 끝) 밖으로 나간 꼭짓점은 원 둘레로 당겨 자른다. 모래톱(물속)은 그리지 않는다.
    /// </summary>
    public class RadarLandGraphic : MaskableGraphic
    {
        public const float Peak = 0.55f, Floor = 0.16f;

        private Vector3 _self;
        private float _scale, _sweep, _radius;
        private bool _online;

        /// <summary>검증용: 지금 그리는 섬 수 · 삼각형 수.</summary>
        public int Islands { get; private set; }
        public int Triangles { get; private set; }

        /// <summary>매 프레임: 자함 위치 · 픽셀/미터 · 스윕 방위(북 0° 시계방향) · 표시 반지름(px).</summary>
        public void Set(Vector3 self, float pixelsPerMeter, float sweepBearing, float displayRadius, bool online)
        {
            _self = self; _scale = pixelsPerMeter; _sweep = sweepBearing; _radius = displayRadius; _online = online;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Islands = 0; Triangles = 0;
            if (!_online || _scale <= 0f) return;
            var all = Game.World.Islands.All;
            Color32 baseColor = color;
            float rangeM = _radius / _scale;
            for (int i = 0; i < all.Count; i++)
            {
                var isl = all[i];
                if (isl == null || isl.Sandbar || isl.Rim == null || isl.Rim.Length < 3) continue;
                var c = new Vector2(isl.Center.x - _self.x, isl.Center.z - _self.z);
                if (c.magnitude - isl.Radius > rangeM) continue;
                Islands++;
                int start = vh.currentVertCount;
                vh.AddVert(Point(c), Shade(baseColor, c), Vector4.zero);
                for (int k = 0; k < isl.Rim.Length; k++)
                {
                    var r = new Vector2(isl.Rim[k].x - _self.x, isl.Rim[k].y - _self.z);
                    vh.AddVert(Point(r), Shade(baseColor, r), Vector4.zero);
                }
                int n = isl.Rim.Length;
                for (int k = 0; k < n; k++)
                {
                    vh.AddTriangle(start, start + 1 + k, start + 1 + (k + 1) % n);
                    Triangles++;
                }
            }
        }

        /// <summary>월드 오프셋(m) → 화면 px(북이 위). 표시 원 밖이면 둘레로 당긴다.</summary>
        private Vector3 Point(Vector2 offsetM)
        {
            Vector2 px = offsetM * _scale;
            if (px.sqrMagnitude > _radius * _radius) px = px.normalized * _radius;
            Vector2 o = rectTransform.rect.center;   // 사각형 가운데 = 자함(레이더 중심)
            return new Vector3(px.x + o.x, px.y + o.y, 0f);
        }

        /// <summary>스윕이 그 방위를 지난 뒤의 밝기(막 지나감 Peak → 한 바퀴 뒤 Floor).</summary>
        private Color32 Shade(Color32 baseColor, Vector2 offsetM)
        {
            float bearing = Mathf.Atan2(offsetM.x, offsetM.y) * Mathf.Rad2Deg;
            float age01 = Mathf.Repeat(_sweep - bearing, 360f) / 360f;   // 0 = 방금 빔이 지나감
            float a = Mathf.Lerp(Peak, Floor, Mathf.Sqrt(age01));
            baseColor.a = (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255);
            return baseColor;
        }
    }
}

using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// HUD가 코드로 만드는 단색 텍스처(흰색 + 알파). 색은 RawImage.color로 입힌다. 한 번 만들어 공유한다.
    /// </summary>
    public static class HudTextures
    {
        private static Texture2D s_brackets, s_triangle, s_dial;

        /// <summary>가운데가 빈 네 모서리 괄호 [ ]. 45° 돌리면 마름모 조준선.</summary>
        public static Texture2D CornerBrackets => s_brackets != null ? s_brackets : s_brackets = BuildBrackets();

        /// <summary>위(+Y)를 가리키는 채운 삼각형.</summary>
        public static Texture2D Triangle => s_triangle != null ? s_triangle : s_triangle = BuildTriangle();

        /// <summary>함 현황 나침반: 옅은 원판, 테두리, 30° 눈금, 북쪽 삼각 표식.</summary>
        public static Texture2D Dial => s_dial != null ? s_dial : s_dial = BuildDial();

        private static float Coverage(float distance, float width) => Mathf.Clamp01(width * 0.5f + 0.5f - distance);

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-5f));
            return Vector2.Distance(p, a + ab * t);
        }

        private static Texture2D BuildBrackets()
        {
            const int N = 64;
            const float margin = 4.5f, thickness = 5.5f, arm = 19f;
            float lo = margin, hi = N - margin;
            var segments = new (Vector2 a, Vector2 b)[]
            {
                (new(lo, hi), new(lo + arm, hi)), (new(lo, hi), new(lo, hi - arm)),
                (new(hi, hi), new(hi - arm, hi)), (new(hi, hi), new(hi, hi - arm)),
                (new(lo, lo), new(lo + arm, lo)), (new(lo, lo), new(lo, lo + arm)),
                (new(hi, lo), new(hi - arm, lo)), (new(hi, lo), new(hi, lo + arm)),
            };

            return Build(N, p =>
            {
                float a = 0f;
                foreach (var (sa, sb) in segments) a = Mathf.Max(a, Coverage(SegmentDistance(p, sa, sb), thickness));
                return a;
            });
        }

        private static Texture2D BuildTriangle()
        {
            const int N = 32;
            Vector2 top = new(N * 0.5f, N - 3f), left = new(3f, 5f), right = new(N - 3f, 5f);
            return Build(N, p =>
            {
                // 세 변 안쪽까지의 부호 거리 중 가장 작은 값
                float d = Mathf.Min(EdgeInside(p, left, top), Mathf.Min(EdgeInside(p, top, right), EdgeInside(p, right, left)));
                return Mathf.Clamp01(d + 0.5f);
            });
        }

        private static float EdgeInside(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 e = (b - a).normalized;
            Vector2 n = new(e.y, -e.x);   // 시계 방향 정점 순서에서 안쪽
            return Vector2.Dot(p - a, n);
        }

        private static Texture2D BuildDial()
        {
            const int N = 256;
            float half = N * 0.5f;
            float rim = half - 3f;
            return Build(N, p =>
            {
                Vector2 d = p - new Vector2(half, half);
                float r = d.magnitude;
                if (r > rim + 3f) return 0f;

                float a = 0.1f * Mathf.Clamp01(rim - r + 0.5f);                 // 원판
                a = Mathf.Max(a, Coverage(Mathf.Abs(r - rim), 2.2f) * 0.8f);        // 테두리

                float bearing = Mathf.Repeat(Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, 360f);
                float nearest = Mathf.Round(bearing / 30f) * 30f;
                float perp = r * Mathf.Abs(Mathf.Sin((bearing - nearest) * Mathf.Deg2Rad));
                if (r > rim - 14f && r < rim) a = Mathf.Max(a, Coverage(perp, 2.4f) * 0.75f);

                // 북쪽 삼각형(테두리 안쪽)
                if (d.y > rim - 26f && d.y < rim - 4f && Mathf.Abs(d.x) < (d.y - (rim - 26f)) * 0.45f) a = 1f;
                return a;
            });
        }

        // ------------------------------------------------------------ 스킬 아이콘

        public enum SkillIcon { Decoy, Jam, Repair, Smoke, Flank }

        private static readonly Sprite[] s_skillIcons = new Sprite[5];

        /// <summary>스킬 아이콘(흰색 + 알파, 96px). Image의 원형 채우기에 쓰도록 스프라이트로 준다.</summary>
        public static Sprite SkillIconSprite(SkillIcon icon)
        {
            int i = (int)icon;
            if (s_skillIcons[i] != null) return s_skillIcons[i];
            var tex = BuildSupersampled(96, InsideFor(icon));
            s_skillIcons[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            return s_skillIcons[i];
        }

        /// <summary>도형 안쪽 판정(좌표 -1~1, y 위).</summary>
        private static System.Func<Vector2, bool> InsideFor(SkillIcon icon) => icon switch
        {
            SkillIcon.Decoy => DecoyShape,
            SkillIcon.Jam => JamShape,
            SkillIcon.Repair => WrenchShape,
            SkillIcon.Smoke => SmokeShape,
            _ => FlankShape,
        };

        /// <summary>기만체: 발사 궤적(점 셋) 끝에서 터지는 채프 섬광.</summary>
        private static bool DecoyShape(Vector2 p)
        {
            Vector2 c = new(0.28f, 0.3f);
            Vector2 d = p - c;
            float r = d.magnitude;
            if (r < 0.15f) return true;
            if (r > 0.27f && r < 0.62f)
            {
                float a = Mathf.Repeat(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, 360f);
                float nearest = Mathf.Round(a / 45f) * 45f;
                if (r * Mathf.Abs(Mathf.Sin((a - nearest) * Mathf.Deg2Rad)) < 0.055f) return true;
            }
            for (int i = 0; i < 3; i++)
            {
                Vector2 dot = Vector2.Lerp(new Vector2(-0.72f, -0.72f), new Vector2(-0.12f, -0.12f), i / 2f);
                if ((p - dot).magnitude < 0.085f + i * 0.015f) return true;
            }
            return false;
        }

        /// <summary>재밍: 송신점에서 퍼지는 전파 호 셋.</summary>
        private static bool JamShape(Vector2 p)
        {
            Vector2 o = new(-0.62f, -0.05f);
            Vector2 d = p - o;
            float r = d.magnitude;
            if (r < 0.16f) return true;
            float ang = Mathf.Abs(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            if (ang > 48f) return false;
            foreach (float radius in new[] { 0.46f, 0.8f, 1.14f })
                if (Mathf.Abs(r - radius) < 0.075f) return true;
            return false;
        }

        /// <summary>응급 수리: 렌치.</summary>
        private static bool WrenchShape(Vector2 p)
        {
            if (SegmentDistance(p, new Vector2(-0.66f, -0.66f), new Vector2(0.18f, 0.18f)) < 0.13f) return true;
            Vector2 head = new(0.42f, 0.42f);
            Vector2 d = p - head;
            float r = d.magnitude;
            if (r > 0.38f) return false;
            // 턱: 바깥(오른쪽 위)으로 열린 홈
            Vector2 axis = new Vector2(1f, 1f).normalized;
            float along = Vector2.Dot(d, axis);
            float across = Mathf.Abs(Vector2.Dot(d, new Vector2(-axis.y, axis.x)));
            return !(along > -0.06f && across < 0.14f);
        }

        /// <summary>연막: 바닥이 평평한 구름.</summary>
        private static bool SmokeShape(Vector2 p)
        {
            if ((p - new Vector2(-0.45f, -0.12f)).magnitude < 0.34f) return true;
            if ((p - new Vector2(-0.02f, 0.16f)).magnitude < 0.44f) return true;
            if ((p - new Vector2(0.45f, -0.08f)).magnitude < 0.33f) return true;
            return p.x > -0.45f && p.x < 0.45f && p.y > -0.46f && p.y < -0.1f;
        }

        /// <summary>전속: 위를 가리키는 겹 갈매기표.</summary>
        private static bool FlankShape(Vector2 p)
        {
            if (Mathf.Abs(p.x) > 0.74f) return false;
            foreach (float top in new[] { 0.62f, 0.08f })
            {
                float v = top - Mathf.Abs(p.x) * 0.85f;
                if (p.y <= v && p.y >= v - 0.3f) return true;
            }
            return false;
        }

        /// <summary>안쪽 판정을 4×4로 표본 추출해 가장자리를 부드럽게 한다.</summary>
        private static Texture2D BuildSupersampled(int size, System.Func<Vector2, bool> inside)
        {
            const int S = 4;
            return Build(size, px =>
            {
                int hits = 0;
                for (int sy = 0; sy < S; sy++)
                    for (int sx = 0; sx < S; sx++)
                    {
                        float x = px.x - 0.5f + (sx + 0.5f) / S, y = px.y - 0.5f + (sy + 0.5f) / S;
                        var p = new Vector2(x / size * 2f - 1f, y / size * 2f - 1f) * 1.08f;   // 가장자리 여백
                        if (inside(p)) hits++;
                    }
                return hits / (float)(S * S);
            });
        }

        private static Texture2D Build(int size, System.Func<Vector2, float> alpha)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(new Vector2(x + 0.5f, y + 0.5f))) * 255f));

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }
    }
}

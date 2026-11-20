using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Enemies;

namespace Game.UI
{
    /// <summary>
    /// 위협 · 표적 아이콘(2026-11-20). 프리팹 · 이미지 파일 없이 코드로 그리는 흰색 + 알파 텍스처(색은 RawImage/SpriteRenderer가 입힌다).
    ///   - 종류 아이콘(<see cref="Kind"/>): 화면 밖 적 표식 배지 안에 넣어 적 종류를 한눈에 — 미사일 · 어뢰 · 고속정 · 전투함 · 전투기 · 드론 ·
    ///     항공기 · 잠수함 · 기뢰 · 보스 · 위그선. 모두 위가 앞(기수).
    ///   - 갈라진 방패(<see cref="ShieldHalf"/>): 범용 초계함 레이더망의 약점 표시 — 방패 문장이 지그재그 금으로 둘로 갈라진 모양(왼쪽 · 오른쪽 반쪽 따로).
    /// 좌표는 -1~1(y 위), 4×4 표본으로 가장자리를 부드럽게.
    /// </summary>
    public static class ThreatIcons
    {
        public enum Kind { Missile, Torpedo, Boat, Warship, Fighter, Drone, Aircraft, Submarine, Mine, Boss, Wig }

        private static readonly Dictionary<Kind, Texture2D> s_icons = new();
        private static Texture2D s_disc, s_ring;
        private static readonly Sprite[] s_shield = new Sprite[2];

        /// <summary>표식 배지 바탕(채운 원).</summary>
        public static Texture2D Disc => s_disc != null ? s_disc : s_disc = Build(64, p => p.magnitude <= 0.92f ? 1f : 0f);
        /// <summary>표식 배지 테두리(굵은 고리).</summary>
        public static Texture2D BadgeRing => s_ring != null ? s_ring : s_ring = Build(64, p => { float r = p.magnitude; return r <= 0.95f && r >= 0.76f ? 1f : 0f; });

        public static Texture2D Icon(Kind kind)
        {
            if (s_icons.TryGetValue(kind, out var tex) && tex != null) return tex;
            tex = Build(64, Shape(kind));
            s_icons[kind] = tex;
            return tex;
        }

        /// <summary>표적 → 아이콘 종류.</summary>
        public static Kind KindFor(ITargetable t, bool missile)
        {
            if (t is Torpedo) return Kind.Torpedo;
            if (missile || t is Missile) return Kind.Missile;
            return t switch
            {
                LunBoss => Kind.Boss,
                BossShip or HybridBattleshipBoss or ModernCorvetteBoss => Kind.Boss,
                IBossTelegraph => Kind.Boss,
                WigEnemy or LightWig => Kind.Wig,
                KamikazeDrone => Kind.Drone,
                FighterJet => Kind.Fighter,
                SeaMine => Kind.Mine,
                SubmarineBase => Kind.Submarine,
                SuicideBoat or FastAttackBoat { Definition: { Id: "ene_usv" } } => Kind.Boat,
                PccCorvette or MissileCorvette or FastAttackBoat { Definition: { Id: "ene_ew_corvette" } } or FastAttackBoat { Definition: { Id: "ene_aa_frigate" } } => Kind.Warship,
                _ when t.Kind == TargetKind.Aircraft => Kind.Aircraft,
                _ when t.Kind == TargetKind.Submarine => Kind.Submarine,
                _ => Kind.Boat,
            };
        }

        // ------------------------------------------------------------ 종류 도형

        private static System.Func<Vector2, float> Shape(Kind kind) => kind switch
        {
            Kind.Missile => p => Any(Capsule(p, new Vector2(0f, -0.55f), new Vector2(0f, 0.62f), 0.15f),
                                     Tri(p, new Vector2(0f, 0.95f), new Vector2(-0.15f, 0.6f), new Vector2(0.15f, 0.6f)),
                                     Tri(p, new Vector2(-0.15f, -0.25f), new Vector2(-0.45f, -0.75f), new Vector2(-0.15f, -0.6f)),
                                     Tri(p, new Vector2(0.15f, -0.25f), new Vector2(0.45f, -0.75f), new Vector2(0.15f, -0.6f))),
            Kind.Torpedo => p => Any(Capsule(p, new Vector2(0f, -0.55f), new Vector2(0f, 0.7f), 0.2f),
                                     Box(p, new Vector2(0f, -0.82f), new Vector2(0.38f, 0.07f)),
                                     Box(p, new Vector2(0f, -0.82f), new Vector2(0.07f, 0.18f))),
            Kind.Boat => p => Hull(p, 0.32f, 0.85f, 0.25f),
            Kind.Warship => p => Hull(p, 0.36f, 0.92f, 0.28f) * (Box(p, new Vector2(0f, -0.05f), new Vector2(0.17f, 0.28f)) > 0f ? 0f : 1f)
                                 + Box(p, new Vector2(0f, -0.05f), new Vector2(0.1f, 0.2f)),
            Kind.Fighter => p => Any(Capsule(p, new Vector2(0f, -0.7f), new Vector2(0f, 0.75f), 0.13f),
                                     Tri(p, new Vector2(0f, 0.35f), new Vector2(-0.85f, -0.3f), new Vector2(0.85f, -0.3f)),
                                     Tri(p, new Vector2(0f, -0.45f), new Vector2(-0.4f, -0.85f), new Vector2(0.4f, -0.85f))),
            Kind.Drone => p => Any(Capsule(p, new Vector2(-0.6f, -0.6f), new Vector2(0.6f, 0.6f), 0.11f),
                                   Capsule(p, new Vector2(-0.6f, 0.6f), new Vector2(0.6f, -0.6f), 0.11f),
                                   Ring(p, new Vector2(-0.6f, -0.6f), 0.28f, 0.08f), Ring(p, new Vector2(0.6f, 0.6f), 0.28f, 0.08f),
                                   Ring(p, new Vector2(-0.6f, 0.6f), 0.28f, 0.08f), Ring(p, new Vector2(0.6f, -0.6f), 0.28f, 0.08f),
                                   Disk(p, Vector2.zero, 0.22f)),
            Kind.Aircraft => p => Any(Capsule(p, new Vector2(0f, -0.75f), new Vector2(0f, 0.75f), 0.12f),
                                      Box(p, new Vector2(0f, 0.15f), new Vector2(0.9f, 0.11f)),
                                      Box(p, new Vector2(0f, -0.65f), new Vector2(0.38f, 0.08f))),
            Kind.Submarine => p => Any(Capsule(p, new Vector2(-0.68f, -0.15f), new Vector2(0.68f, -0.15f), 0.24f),
                                       Box(p, new Vector2(0.05f, 0.22f), new Vector2(0.17f, 0.2f)),
                                       Box(p, new Vector2(0.1f, 0.48f), new Vector2(0.03f, 0.12f))),
            Kind.Mine => p => Any(Disk(p, Vector2.zero, 0.5f), Spikes(p, 8, 0.85f, 0.09f)),
            Kind.Boss => p => Any(Hull(p, 0.38f, 0.6f, 0.2f, -0.35f),
                                  Tri(p, new Vector2(-0.55f, 0.45f), new Vector2(-0.55f, 0.95f), new Vector2(-0.25f, 0.6f)),
                                  Tri(p, new Vector2(0f, 0.45f), new Vector2(0f, 0.98f), new Vector2(0.18f, 0.62f)),
                                  Tri(p, new Vector2(0f, 0.45f), new Vector2(0f, 0.98f), new Vector2(-0.18f, 0.62f)),
                                  Tri(p, new Vector2(0.55f, 0.45f), new Vector2(0.55f, 0.95f), new Vector2(0.25f, 0.6f)),
                                  Box(p, new Vector2(0f, 0.48f), new Vector2(0.58f, 0.07f))),
            Kind.Wig => p => Any(Capsule(p, new Vector2(0f, -0.8f), new Vector2(0f, 0.8f), 0.14f),
                                 Box(p, new Vector2(0f, -0.1f), new Vector2(0.75f, 0.17f)),
                                 Box(p, new Vector2(0f, -0.72f), new Vector2(0.42f, 0.08f)),
                                 Box(p, new Vector2(-0.3f, 0.5f), new Vector2(0.18f, 0.05f)), Box(p, new Vector2(0.3f, 0.5f), new Vector2(0.18f, 0.05f))),
            _ => p => Disk(p, Vector2.zero, 0.5f),
        };

        /// <summary>위에서 본 선체: 뾰족한 선수 · 둥근 선미(폭 halfWidth, 길이 ±length, 선수 뾰족한 길이 bow). centerY로 위아래 이동.</summary>
        private static float Hull(Vector2 p, float halfWidth, float length, float bow, float centerY = 0f)
        {
            float y = p.y - centerY;
            if (y > length || y < -length) return 0f;
            float w = halfWidth;
            float bowStart = length - bow * 2.2f;
            if (y > bowStart) w = halfWidth * (1f - (y - bowStart) / (length - bowStart));
            if (y < -length + 0.15f) w = halfWidth * Mathf.Sqrt(Mathf.Clamp01((y + length) / 0.15f));
            return Mathf.Abs(p.x) <= w ? 1f : 0f;
        }

        private static float Any(params float[] v) { foreach (var x in v) if (x > 0f) return 1f; return 0f; }
        private static float Disk(Vector2 p, Vector2 c, float r) => (p - c).sqrMagnitude <= r * r ? 1f : 0f;
        private static float Ring(Vector2 p, Vector2 c, float r, float w) { float d = (p - c).magnitude; return Mathf.Abs(d - r) <= w ? 1f : 0f; }
        private static float Box(Vector2 p, Vector2 c, Vector2 half) => Mathf.Abs(p.x - c.x) <= half.x && Mathf.Abs(p.y - c.y) <= half.y ? 1f : 0f;

        private static float Capsule(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
            return (p - (a + ab * t)).sqrMagnitude <= r * r ? 1f : 0f;
        }

        private static float Tri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
            bool neg = d1 < 0f || d2 < 0f || d3 < 0f, pos = d1 > 0f || d2 > 0f || d3 > 0f;
            return neg && pos ? 0f : 1f;
        }

        private static float Cross(Vector2 p, Vector2 a, Vector2 b) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

        private static float Spikes(Vector2 p, int n, float len, float w)
        {
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                if (Capsule(p, Vector2.zero, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * len, w) > 0f) return 1f;
            }
            return 0f;
        }

        // ------------------------------------------------------------ 갈라진 방패(약점 표시)

        /// <summary>방패 반쪽 스프라이트(0 = 왼쪽, 1 = 오른쪽). 두 장을 같은 자리에 겹치면 온전한 방패, 벌리면 갈라진 방패.</summary>
        public static Sprite ShieldHalf(int side)
        {
            side = Mathf.Clamp(side, 0, 1);
            if (s_shield[side] != null) return s_shield[side];
            var tex = Build(128, p => ShieldAlpha(p, side));
            s_shield[side] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 128f);
            return s_shield[side];
        }

        /// <summary>방패 문장: 굵은 테두리(불투명) · 속(반투명) · 가운데 가로 띠, 지그재그 금으로 둘로 나뉜다.</summary>
        private static float ShieldAlpha(Vector2 p, int side)
        {
            float half = ShieldHalfWidth(p.y);
            if (half <= 0f || Mathf.Abs(p.x) > half) return 0f;
            // 금: 위에서 아래로 지그재그(가운데 근처), 틈 0.07
            float crack = Crack(p.y);
            float gap = 0.035f;
            if (side == 0 ? p.x > crack - gap : p.x < crack + gap) return 0f;
            // 테두리: 바깥 윤곽에서 0.13 안쪽까지
            float inset = Mathf.Min(half - Mathf.Abs(p.x), 0.86f - p.y, Mathf.Abs(crack - p.x) - gap + 0.02f);
            if (half - Mathf.Abs(p.x) < 0.13f || 0.86f - p.y < 0.13f || ShieldHalfWidth(p.y - 0.13f) < Mathf.Abs(p.x)) return 1f;
            // 가운데 가로 띠(문장 느낌)
            if (Mathf.Abs(p.y - 0.28f) < 0.07f) return 0.9f;
            return inset < 0.18f ? 0.6f : 0.38f;
        }

        /// <summary>높이 y에서 방패 반폭: 위는 평평(0.86), 0 아래로 둥글게 좁아져 -0.95에서 뾰족.</summary>
        private static float ShieldHalfWidth(float y)
        {
            if (y > 0.86f || y < -0.95f) return 0f;
            if (y >= 0.05f) return 0.78f;
            float t = (y + 0.95f) / 1.0f;   // 0(끝) ~ 1(어깨)
            return 0.78f * Mathf.Sqrt(Mathf.Clamp01(t)) * (0.35f + 0.65f * t);
        }

        /// <summary>지그재그 금(높이 y → x).</summary>
        private static float Crack(float y)
        {
            float[] ys = { 0.9f, 0.55f, 0.25f, -0.1f, -0.45f, -1f };
            float[] xs = { 0.06f, -0.1f, 0.09f, -0.08f, 0.05f, 0f };
            for (int i = 0; i < ys.Length - 1; i++)
                if (y <= ys[i] && y >= ys[i + 1]) return Mathf.Lerp(xs[i], xs[i + 1], (ys[i] - y) / (ys[i] - ys[i + 1]));
            return 0f;
        }

        // ------------------------------------------------------------ 텍스처

        private static Texture2D Build(int size, System.Func<Vector2, float> alpha)
        {
            const int S = 4;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float sum = 0f;
                    for (int sy = 0; sy < S; sy++)
                        for (int sx = 0; sx < S; sx++)
                        {
                            var p = new Vector2((x + (sx + 0.5f) / S) / size * 2f - 1f, (y + (sy + 0.5f) / S) / size * 2f - 1f) * 1.06f;
                            sum += Mathf.Clamp01(alpha(p));
                        }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(sum / (S * S) * 255f));
                }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }
    }
}

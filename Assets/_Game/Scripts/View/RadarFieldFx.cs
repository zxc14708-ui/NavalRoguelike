using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Game.Combat;

namespace Game.View
{
    /// <summary>
    /// 레이더망 장판 연출(2026-11-20, 범용 초계함 고유 스킬). 수면에 레이더 화면 문양을 눕혀 배를 따라다니게 한다(북쪽 = 화면 위, 배가 돌아도 문양은 그대로).
    ///   - 문양(코드로 그린 텍스처): 옅은 바탕 · 격자 · 거리 고리(굵게 1/3 · 2/3, 가늘게 1/6 · 1/2 · 5/6) · 바깥 방위 고리(5° · 10° · 30° 눈금) · 십자선.
    ///   - 방위 숫자 000 · 030 … 330, 거리 숫자(m) — HUD 글꼴을 빌린다(없으면 숫자 없이).
    ///   - 회전 스윕(잔광 꼬리) · 스윕이 적 위를 지나면 초록 에코(잠수함은 하늘색으로 크게).
    ///   - 펼침: 0.45초에 중심에서 퍼지며 번쩍 · 3초마다 바깥으로 퍼지는 고리 · 끝: 0.5초에 옅어지며 사라진다.
    /// </summary>
    public sealed class RadarFieldFx : MonoBehaviour
    {
        public static readonly Color Green = new(0.38f, 1f, 0.5f);
        private const float SweepSeconds = 2.4f, PulseEvery = 3f;
        private static Sprite s_disc, s_sweep;
        private static TMP_FontAsset s_font;

        private Transform _ship;
        private float _radius, _until, _age, _endAt = -1f, _sweep, _pulse;
        private SpriteRenderer _disc, _sweepRenderer;
        private Transform _scaled;   // 문양 · 스윕만 반경만큼 키운다(글자는 크기 그대로, 자리만 옮김)
        private readonly List<(TMP_Text text, Vector3 dir, float fraction)> _labels = new();
        private static readonly float[] ThinRings = { 1f / 6f, 0.5f, 5f / 6f }, ThickRings = { 1f / 3f, 2f / 3f };

        public static RadarFieldFx Active { get; private set; }
        /// <summary>검증용: 지금 펼친 정도(0~1) · 에코 수.</summary>
        public float Open { get; private set; }
        public int Echoes { get; private set; }

        public static RadarFieldFx Begin(Transform ship, float radius, float duration)
        {
            if (ship == null) return null;
            if (Active != null) Destroy(Active.gameObject);
            var go = new GameObject("Radar field");
            var fx = go.AddComponent<RadarFieldFx>();
            fx.Init(ship, radius, duration);
            Active = fx;
            return fx;
        }

        public void End() { if (_endAt < 0f) _endAt = Time.time; }

        /// <summary>그 자리에 에코 한 번(잠수함은 하늘색 · 크게).</summary>
        public void Echo(Vector3 at, bool submarine)
        {
            Echoes++;
            var c = submarine ? new Color(0.45f, 0.95f, 1f, 0.95f) : new Color(Green.r, Green.g, Green.b, 0.9f);
            DecorFx.Emit(DecorFx.Flash, new Vector3(at.x, 0.4f, at.z), Vector3.zero, submarine ? 0.5f : 0.3f, submarine ? 5f : 3.2f, c);
        }

        private void Init(Transform ship, float radius, float duration)
        {
            _ship = ship; _radius = radius; _until = Time.time + duration;
            _scaled = new GameObject("Scaled").transform;
            _scaled.SetParent(transform, false);
            _disc = Flat("Radar disc", Disc(), 30);
            _sweepRenderer = Flat("Radar sweep", Sweep(), 31);
            BuildLabels();
            Game.Ship.SignatureFx.Ring(ship.position, radius, 0.7f, new Color(Green.r, Green.g, Green.b, 0.95f), 1.2f, 2, 0.15f);
            Explosions.FlashLight(ship.position + Vector3.up * 2f, 0.3f, Green);
            LateUpdate();
        }

        private SpriteRenderer Flat(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_scaled, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 눕힌다: 텍스처 위쪽 = 북(+Z)
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
            return sr;
        }

        private void BuildLabels()
        {
            if (s_font == null)
            {
                var any = FindFirstObjectByType<TMP_Text>();
                s_font = any != null ? any.font : TMP_Settings.defaultFontAsset;
            }
            if (s_font == null) return;
            for (int b = 0; b < 360; b += 30)
                Label(b.ToString("000"), b, 0.93f, b % 90 == 0 ? 30f : 22f);   // TMP 3D: 글자 크기 10 ≈ 높이 약 0.7m
            // 거리 숫자(m): 동쪽 선을 따라 굵은 고리 · 바깥 고리
            foreach (float f in new[] { 1f / 3f, 2f / 3f })
                Label($"{_radius * f:0}m", 90f, f + 0.06f, 18f, 0.6f);
        }

        private void Label(string text, float bearing, float radiusFraction, float size, float alpha = 1f)
        {
            var go = new GameObject("Radar label " + text);
            go.transform.SetParent(transform, false);
            float a = bearing * Mathf.Deg2Rad;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var t = go.AddComponent<TextMeshPro>();
            t.font = s_font;
            t.text = text;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(12f, 4f);
            t.color = new Color(Green.r, Green.g, Green.b, alpha);
            var mr = t.GetComponent<MeshRenderer>();
            if (mr != null) { mr.sortingOrder = 32; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            _labels.Add((t, new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)), radiusFraction));   // 1 = 장판 반경(방위 고리)
        }

        private void LateUpdate()
        {
            if (_ship == null || !_ship.gameObject.activeInHierarchy) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            _age += dt;
            if (_endAt < 0f && Time.time >= _until) End();
            float fade = _endAt >= 0f ? Mathf.Clamp01(1f - (Time.time - _endAt) / 0.5f) : 1f;
            if (fade <= 0f) { Destroy(gameObject); return; }

            // 펼침: 0.45초에 퍼짐(살짝 넘쳤다 돌아옴)
            float u = Mathf.Clamp01(_age / 0.45f);
            Open = u < 1f ? 1f - Mathf.Pow(1f - u, 3f) * Mathf.Cos(u * 5f) * 0.9f - 0.1f * (1f - u) : 1f;
            Open = Mathf.Clamp(Open, 0f, 1.06f);
            transform.position = new Vector3(_ship.position.x, 0.14f, _ship.position.z);
            float scale = _radius / 0.92f * Mathf.Lerp(0.96f, 1f, fade) * Open;   // 문양 방위 고리(0.92) = 반경
            _scaled.localScale = Vector3.one * scale;

            float flash = _age < 0.6f ? 1f - _age / 0.6f : 0f;
            float breathe = 0.85f + 0.15f * Mathf.Sin(Time.time * 3f);
            var c = Color.Lerp(Green, Color.white, flash * 0.5f);
            c.a = (0.66f + 0.34f * flash) * breathe * fade;
            _disc.color = c;
            var sc = Green; sc.a = 0.85f * fade;
            _sweepRenderer.color = sc;
            foreach (var (t, dir, f) in _labels)
            {
                if (t == null) continue;
                t.transform.localPosition = dir * (f * _radius * Open) + Vector3.up * 0.02f;
                var lc = t.color; lc.a = Mathf.Min(1f, fade * Mathf.Clamp01(Open) * (t.fontSize < 20f ? 0.6f : 1f)); t.color = lc;
            }

            // 스윕(시계 방향 — 방위가 늘어나는 쪽) · 에코
            float prev = _sweep;
            _sweep = Mathf.Repeat(_sweep + 360f * dt / SweepSeconds, 360f);
            _sweepRenderer.transform.localRotation = Quaternion.Euler(90f, _sweep, 0f);
            if (_endAt < 0f) SweepEchoes(prev, _sweep);

            _pulse -= dt;
            if (_endAt < 0f && _age > 0.5f && _pulse <= 0f)
            {
                _pulse = PulseEvery;
                Game.Ship.SignatureFx.Ring(_ship.position, _radius, 1.1f, new Color(Green.r, Green.g, Green.b, 0.6f), 0.5f);
            }
        }

        private void SweepEchoes(float from, float to)
        {
            Vector3 c = _ship.position;
            float r2 = _radius * _radius;
            foreach (var kind in new[] { TargetKind.Surface, TargetKind.Aircraft })
            {
                var list = TargetRegistry.HostileTo(CombatFaction.Player, kind);
                for (int i = 0; i < list.Count; i++)
                {
                    var t = list[i];
                    if (t == null || !t.IsAlive || t.Transform == null) continue;
                    Vector3 d = t.Transform.position - c; d.y = 0f;
                    if (d.sqrMagnitude > r2 || d.sqrMagnitude < 1f) continue;
                    float bearing = Mathf.Repeat(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 360f);
                    bool crossed = from <= to ? bearing > from && bearing <= to : bearing > from || bearing <= to;
                    if (crossed) Echo(t.Transform.position, false);
                }
            }
        }

        private void OnDestroy() { if (Active == this) Active = null; }

        // ------------------------------------------------------------ 텍스처(흰색 + 알파, 색은 SpriteRenderer)

        private static Sprite Disc()
        {
            if (s_disc != null) return s_disc;
            const int N = 1024;
            float px = 2f / N;
            var pixels = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var p = new Vector2((x + 0.5f) / N * 2f - 1f, (y + 0.5f) / N * 2f - 1f);
                    pixels[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(DiscAlpha(p, px)) * 255f));
                }
            s_disc = MakeSprite(pixels, N);
            return s_disc;
        }

        private static float Line(float distance, float halfWidth, float px) => Mathf.Clamp01((halfWidth + px * 0.5f - Mathf.Abs(distance)) / px);

        /// <summary>레이더 화면 문양. 바깥 방위 고리 = 0.92, 맨 바깥 테 = 1.0.</summary>
        private static float DiscAlpha(Vector2 p, float px)
        {
            float r = p.magnitude;
            if (r > 1.01f) return 0f;
            const float R = 0.92f;
            float a = 0f;
            if (r < R)
            {
                a = 0.045f;                                                                   // 옅은 바탕
                float gx = Mathf.Abs(Mathf.Repeat(p.x + 0.0625f, 0.125f) - 0.0625f), gy = Mathf.Abs(Mathf.Repeat(p.y + 0.0625f, 0.125f) - 0.0625f);
                a = Mathf.Max(a, 0.14f * Mathf.Max(Line(gx, 0.6f * px, px), Line(gy, 0.6f * px, px)));   // 격자
                a = Mathf.Max(a, 0.3f * Mathf.Max(Line(p.x, 0.7f * px, px), Line(p.y, 0.7f * px, px)));    // 십자선
                foreach (float f in ThinRings) a = Mathf.Max(a, 0.45f * Line(r - f * R, 0.9f * px, px));   // 가는 고리
                foreach (float f in ThickRings) a = Mathf.Max(a, 0.95f * Line(r - f * R, 2.2f * px, px));        // 굵은 고리
                a = Mathf.Max(a, Line(r, 3f * px, px));                                        // 가운데 점
            }
            a = Mathf.Max(a, Line(r - R, 3f * px, px));                                        // 방위 고리(굵게)
            a = Mathf.Max(a, 0.75f * Line(r - 1f, 1.5f * px, px));                             // 맨 바깥 테
            if (r > R && r < 1f)
            {
                float bearing = Mathf.Repeat(Mathf.Atan2(p.x, p.y) * Mathf.Rad2Deg, 360f);
                float nearest5 = Mathf.Round(bearing / 5f) * 5f;
                float along = Mathf.Abs(Mathf.DeltaAngle(bearing, nearest5)) * Mathf.Deg2Rad * r;   // 눈금선까지 호 길이
                int step = Mathf.RoundToInt(nearest5) % 360;
                float len = step % 30 == 0 ? 1f : step % 10 == 0 ? 0.965f : 0.948f;
                float w = step % 30 == 0 ? 2.4f * px : step % 10 == 0 ? 1.4f * px : 0.9f * px;
                if (r <= len) a = Mathf.Max(a, (step % 30 == 0 ? 1f : 0.7f) * Line(along, w, px));
            }
            return a;
        }

        private static Sprite Sweep()
        {
            if (s_sweep != null) return s_sweep;
            const int N = 512;
            float px = 2f / N;
            var pixels = new Color32[N * N];
            const float tail = 70f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var p = new Vector2((x + 0.5f) / N * 2f - 1f, (y + 0.5f) / N * 2f - 1f);
                    float r = p.magnitude, a = 0f;
                    if (r < 0.92f && r > 0.02f)
                    {
                        // 앞선(북, 0°)에서 반시계 쪽으로 꼬리(돌면 시계 방향으로 나아간다)
                        float behind = Mathf.Repeat(-Mathf.Atan2(p.x, p.y) * Mathf.Rad2Deg, 360f);
                        if (behind < tail) a = 0.42f * Mathf.Pow(1f - behind / tail, 2f);
                        float lead = Mathf.Abs(p.x) * (p.y > 0f ? 1f : 99f);
                        a = Mathf.Max(a, 0.95f * Mathf.Clamp01((1.6f * px - lead) / px));
                    }
                    pixels[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            s_sweep = MakeSprite(pixels, N);
            return s_sweep;
        }

        private static Sprite MakeSprite(Color32[] pixels, int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n / 2f);   // 반경 1 = 1 유닛
        }
    }
}

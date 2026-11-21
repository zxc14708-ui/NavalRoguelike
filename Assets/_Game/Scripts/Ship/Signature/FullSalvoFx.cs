using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Game.Combat;
using Game.View;

namespace Game.Ship
{
    /// <summary>
    /// 전탄 발사 연출(2026-11-21 사용자 시안 승인).
    ///   - 발동: 함선 둘레 충격파 고리 두 겹(주황 · 흰빛) · 갑판 섬광 · 주황 조명.
    ///   - 발사: 셀마다 발사 섬광과 연기 뭉치 — 갑판에 하얀 연기 기둥이 쌓인다.
    /// 표적 위 마커는 SalvoMarker.
    /// </summary>
    public static class FullSalvoFx
    {
        public static readonly Color Orange = new(1f, 0.65f, 0.25f, 0.95f);

        public static void Activate(Transform ship)
        {
            if (ship == null) return;
            Vector3 p = ship.position;
            SignatureFx.Ring(p, 24f, 1.2f, Orange, 0.9f);
            SignatureFx.Ring(p, 13f, 0.7f, new Color(1f, 0.95f, 0.84f, 0.9f), 0.45f);
            DecorFx.Emit(DecorFx.Flash, p + Vector3.up * 1.5f, Vector3.zero, 0.25f, 9f, new Color(1f, 0.72f, 0.35f, 0.55f));
            Explosions.FlashLight(p + Vector3.up * 2.5f, 1.4f, new Color(1f, 0.66f, 0.3f));
        }

        /// <summary>셀 하나가 쏠 때: 짧은 섬광 · 불꽃 · 갑판에 쌓이는 연기 뭉치.</summary>
        public static void LaunchPlume(Vector3 hatch)
        {
            DecorFx.Emit(DecorFx.Flash, hatch + Vector3.up * 0.4f, Vector3.zero, 0.12f, 2.2f, new Color(1f, 0.85f, 0.55f, 0.9f));
            for (int i = 0; i < 3; i++)
                DecorFx.Emit(DecorFx.Fire, hatch + Vector3.up * 0.2f, Random.insideUnitSphere * 1.5f + Vector3.up * 2f, Random.Range(0.18f, 0.3f), Random.Range(0.6f, 0.9f),
                    new Color(1f, 0.8f, 0.45f, 1f));
            for (int i = 0; i < 5; i++)
            {
                Vector3 r = Random.insideUnitSphere; r.y = Mathf.Abs(r.y);
                float g = Random.Range(0.78f, 0.9f);
                DecorFx.Emit(DecorFx.Smoke, hatch + r * 0.5f, new Vector3(r.x * 1.6f, Random.Range(0.8f, 2.4f), r.z * 1.6f) + DecorFx.Wind * 0.3f,
                    Random.Range(2.6f, 3.6f), Random.Range(1.1f, 1.8f), new Color(g, g, g, 0.5f));
            }
        }
    }

    /// <summary>
    /// 전탄 발사 표적 마커(시안 그대로): 표적 바로 위에 기수를 아래로 향한 미사일 아이콘, 아이콘에서 표적까지 점선,
    /// 표적 둘레 수면에 네 눈금 조준 고리, 아이콘 옆 남은 미사일 수(×N)와 우선순위 번호.
    ///   - 락온(0.42초): 아이콘이 위에서 떨어져 꽂히며 살짝 튕기고, 넓던 고리가 조여든다.
    ///   - 비행 중: 아이콘이 천천히 오르내린다.
    ///   - 명중 임박(이 표적을 노리는 미사일이 내리꽂히는 중): 흰색 · 표적색으로 깜박이고 고리가 더 조여 맥박친다.
    ///   - 한 발 끝날 때마다(명중 · 격추 · 추락) ×N이 줄고, 0이 되거나 표적이 사라지면 오그라들며 없어진다.
    /// 색: 고가치(표적 가치 5 이상 — 대형함 · 보스) 빨강 · 일반 수상함 주황 · 항공기 하늘색.
    /// </summary>
    public sealed class SalvoMarker : MonoBehaviour
    {
        public static readonly Color HighValue = new(1f, 0.3f, 0.18f), Normal = new(1f, 0.69f, 0.18f), Air = new(0.5f, 0.83f, 1f);
        private const float DropSeconds = 0.42f, DropHeight = 12f, Lifetime = 14f;
        // 크기(m) — 높은 전투 카메라에서도 읽히게(2026-11-21 화면 확인 뒤 키움)
        private const float IconSize = 4f, AirIconSize = 3f, LabelScale = 1.5f;

        private static Sprite s_missile, s_disc, s_plate;
        private static Material s_dash;
        private static TMP_FontAsset s_font;
        private static readonly List<SalvoMarker> s_active = new();

        /// <summary>검증용: 지금 떠 있는 마커(우선순위 순).</summary>
        public static IReadOnlyList<SalvoMarker> Active => s_active;

        public Transform Target { get; private set; }
        public int Order { get; private set; }
        public int Remaining { get; private set; }
        public int Assigned { get; private set; }
        public bool Imminent { get; private set; }
        public Color Tint => _color;

        private Color _color;
        private bool _air;
        private float _t0, _topH, _radius, _endAt = -1f;
        private Transform _icon, _label, _reticle;
        private SpriteRenderer _iconSr, _plateSr, _badgeSr;
        private TextMeshPro _count, _orderText;
        private LineRenderer _line, _ring;
        private readonly List<LineRenderer> _ticks = new();

        public static SalvoMarker Begin(Transform target, int missiles, Color color, int order, bool air)
        {
            if (target == null || missiles <= 0) return null;
            var go = new GameObject("Salvo marker");
            var m = go.AddComponent<SalvoMarker>();
            m.Build(target, missiles, color, order, air);
            return m;
        }

        private void Build(Transform target, int missiles, Color color, int order, bool air)
        {
            Target = target; Remaining = Assigned = missiles; _color = color; Order = order; _air = air; _t0 = Time.time;
            EnsureAssets();
            // 표적 크기: 렌더러 범위(꼭대기 높이 · 반지름)
            var b = new Bounds(target.position, Vector3.one);
            bool any = false;
            foreach (var r in target.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || !r.enabled || r.name == "Night lamp") continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            _topH = Mathf.Clamp(b.max.y - target.position.y, 0.6f, 10f);
            _radius = air ? 1.6f : Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z) * 0.85f, 1.3f, 9f);

            _icon = new GameObject("Icon").transform; _icon.SetParent(transform, false);
            _iconSr = _icon.gameObject.AddComponent<SpriteRenderer>();
            _iconSr.sprite = s_missile; _iconSr.color = color;
            _icon.localScale = Vector3.one * (air ? AirIconSize : IconSize);

            _label = new GameObject("Label").transform; _label.SetParent(transform, false);
            _label.localScale = Vector3.one * LabelScale;
            var plate = new GameObject("Plate").transform; plate.SetParent(_label, false);
            plate.localPosition = new Vector3(1.55f, 0.55f, 0.01f); plate.localScale = new Vector3(0.9f, 0.85f, 1f);
            _plateSr = plate.gameObject.AddComponent<SpriteRenderer>(); _plateSr.sprite = s_plate; _plateSr.color = new Color(0.1f, 0.07f, 0.06f, 0.85f);
            _count = Text(_label, new Vector3(1.55f, 0.55f, 0f), 11f, new Color(1f, 0.9f, 0.86f));
            var badge = new GameObject("Badge").transform; badge.SetParent(_label, false);
            badge.localPosition = new Vector3(-1.2f, 0.7f, 0.01f); badge.localScale = Vector3.one * 0.95f;
            _badgeSr = badge.gameObject.AddComponent<SpriteRenderer>(); _badgeSr.sprite = s_disc; _badgeSr.color = color;
            _orderText = Text(_label, new Vector3(-1.2f, 0.7f, 0f), 10f, new Color(0.1f, 0.07f, 0.06f));
            if (_orderText != null) _orderText.text = order.ToString();

            _line = gameObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = true; _line.positionCount = 2; _line.widthMultiplier = 0.16f;
            _line.sharedMaterial = s_dash; _line.textureMode = LineTextureMode.Tile; _line.textureScale = new Vector2(0.9f, 1f);
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; _line.receiveShadows = false;

            _reticle = new GameObject("Reticle").transform; _reticle.SetParent(transform, false);
            _reticle.rotation = Quaternion.Euler(90f, 0f, 0f);
            _ring = SignatureFx.Circle(_reticle, 64, 0.2f, color);
            for (int i = 0; i < 4; i++)
            {
                var t = new GameObject("Tick").transform; t.SetParent(_reticle, false);
                t.localRotation = Quaternion.Euler(0f, 0f, i * 90f);
                var lr = t.gameObject.AddComponent<LineRenderer>();
                lr.useWorldSpace = false; lr.positionCount = 2; lr.widthMultiplier = 0.2f;
                lr.sharedMaterial = SignatureFx.Additive; lr.alignment = LineAlignment.TransformZ;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.SetPositions(new[] { new Vector3(1f, 0f, 0f), new Vector3(1.35f, 0f, 0f) });
                _ticks.Add(lr);
            }
            s_active.Add(this);
            Missile.SalvoEnded += OnMissileEnded;
            Refresh(0f, 1f);
        }

        private static TextMeshPro Text(Transform parent, Vector3 local, float size, Color color)
        {
            if (s_font == null) return null;
            var go = new GameObject("Text"); go.transform.SetParent(parent, false); go.transform.localPosition = local;
            var t = go.AddComponent<TextMeshPro>();
            t.font = s_font; t.fontSize = size; t.color = color;
            t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(3f, 1.2f);
            t.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return t;
        }

        private void OnMissileEnded(Transform target)
        {
            if (target != Target || Remaining <= 0) return;
            Remaining--;
            if (Remaining <= 0) _endAt = Time.time;
        }

        private void OnDestroy()
        {
            Missile.SalvoEnded -= OnMissileEnded;
            s_active.Remove(this);
        }

        private void LateUpdate()
        {
            bool gone = Target == null || !Target.gameObject.activeInHierarchy
                        || (Target.TryGetComponent<ITargetable>(out var tg) && !tg.IsAlive);
            if ((gone || Time.time - _t0 > Lifetime) && _endAt < 0f) _endAt = Time.time;
            float fade = 1f;
            if (_endAt >= 0f)
            {
                fade = 1f - (Time.time - _endAt) / 0.25f;
                if (fade <= 0f || Target == null) { Destroy(gameObject); return; }
            }
            Refresh(Time.time - _t0, fade);
        }

        private void Refresh(float age, float fade)
        {
            float e = Mathf.Clamp01(age / DropSeconds);
            float ease = 1f - (1f - e) * (1f - e) * (1f - e);
            Imminent = false;
            if (Target != null)
                foreach (var m in Missile.ActiveSalvo)
                    if (m != null && m.SalvoTarget == Target && (m.IsTopDiving || (_air && (m.transform.position - Target.position).sqrMagnitude < 100f))) { Imminent = true; break; }

            Color col = Imminent && Mathf.Repeat(Time.time * 8f, 1f) < 0.5f ? Color.white : _color;
            col.a = fade;
            Vector3 basePos = Target != null ? Target.position : transform.position;
            float y = basePos.y + _topH + 3.5f + DropHeight * (1f - ease);
            if (e >= 1f && !Imminent) y += Mathf.Sin(Time.time * 5f + Order) * 0.3f;
            float pop = age > DropSeconds && age < DropSeconds + 0.3f ? 1f + 0.25f * Mathf.Sin((age - DropSeconds) / 0.3f * Mathf.PI) : 1f;
            transform.position = basePos;
            _icon.position = new Vector3(basePos.x, y, basePos.z);
            _icon.localScale = Vector3.one * (_air ? AirIconSize : IconSize) * pop * Mathf.Lerp(0.6f, 1f, fade);
            _label.position = _icon.position;
            var cam = Camera.main;
            if (cam != null) { _icon.rotation = cam.transform.rotation; _label.rotation = cam.transform.rotation; }
            _iconSr.color = col;
            _badgeSr.color = new Color(_color.r, _color.g, _color.b, fade * ease);
            _plateSr.color = new Color(0.1f, 0.07f, 0.06f, 0.85f * fade * ease);
            if (_count != null) { _count.text = "×" + Remaining; var c = _count.color; c.a = fade * ease; _count.color = c; }
            if (_orderText != null) { var c = _orderText.color; c.a = fade * ease; _orderText.color = c; }

            // 점선: 아이콘 끝 → 표적 꼭대기
            var lc = col; lc.a *= 0.8f * ease;
            _line.startColor = _line.endColor = lc;
            _line.SetPosition(0, _icon.position + Vector3.down * ((_air ? AirIconSize : IconSize) * 0.5f));
            _line.SetPosition(1, basePos + Vector3.up * _topH);

            // 조준 고리: 수면(항공기는 그 높이)에, 락온 때 넓게서 조여들고 명중 임박이면 더 조여 맥박친다
            float ry = _air ? basePos.y : 0.3f;
            _reticle.position = new Vector3(basePos.x, ry, basePos.z);
            float r = _radius * (1f + 0.8f * (1f - ease)) * (Imminent ? 0.85f + 0.08f * Mathf.Sin(Time.time * 14f) : 1f);
            _reticle.localScale = Vector3.one * r;
            var rc = col; rc.a *= ease;
            _ring.widthMultiplier = 0.2f / Mathf.Max(0.1f, r);
            _ring.startColor = _ring.endColor = rc;
            foreach (var t in _ticks) { t.widthMultiplier = 0.2f / Mathf.Max(0.1f, r); t.startColor = t.endColor = rc; }
        }

        // ------------------------------------------------------------ 그림(런타임에 한 번 그린다)

        private static void EnsureAssets()
        {
            if (s_missile == null) s_missile = Draw(32, 96, MissilePixel, 0.5f, 0.5f, 96f);
            if (s_disc == null) s_disc = Draw(32, 32, (x, y) => (x - 15.5f) * (x - 15.5f) + (y - 15.5f) * (y - 15.5f) <= 15f * 15f ? 1f : 0f, 0.5f, 0.5f, 32f);
            if (s_plate == null) s_plate = Draw(48, 24, PlatePixel, 0.5f, 0.5f, 24f);
            if (s_dash == null)
            {
                var tex = new Texture2D(8, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, name = "Salvo dash" };
                for (int x = 0; x < 8; x++) for (int y = 0; y < 2; y++) tex.SetPixel(x, y, x < 5 ? Color.white : Color.clear);
                tex.Apply();
                s_dash = new Material(Shader.Find("Sprites/Default")) { name = "Salvo dash", mainTexture = tex };
            }
            if (s_font == null)
            {
                var any = Object.FindFirstObjectByType<TMP_Text>();
                s_font = any != null ? any.font : TMP_Settings.defaultFontAsset;
            }
        }

        /// <summary>기수를 아래로 향한 미사일(위 = 꼬리날개, 아래 = 뾰족한 기수). 가운데 줄은 조금 어둡게.</summary>
        private static float MissilePixel(int x, int y)
        {
            float cx = x + 0.5f - 16f;
            if (y < 22) return Mathf.Abs(cx) <= 6f * (y / 22f) ? 1f : 0f;                      // 기수(아래가 뾰족)
            if (y < 80 && Mathf.Abs(cx) <= 6f) return Mathf.Abs(cx) <= 1.5f && y > 30 && y < 70 ? 0.6f : 1f;   // 몸통 + 가운데 줄
            if (y >= 62 && y < 96)
            {
                float k = (y - 62f) / 34f;                                                        // 꼬리날개: 위로 갈수록 바깥으로
                if (Mathf.Abs(cx) > 6f && Mathf.Abs(cx) <= 6f + 10f * k) return 1f;
                if (Mathf.Abs(cx) <= 6f && y < 84) return 1f;
            }
            return 0f;
        }

        private static float PlatePixel(int x, int y)
        {
            const float r = 6f;
            float dx = Mathf.Max(0f, Mathf.Max(r - x - 0.5f, x + 0.5f - (48f - r)));
            float dy = Mathf.Max(0f, Mathf.Max(r - y - 0.5f, y + 0.5f - (24f - r)));
            return dx * dx + dy * dy <= r * r ? 1f : 0f;
        }

        private static Sprite Draw(int w, int h, System.Func<int, int, float> shade, float px, float py, float ppu)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Salvo marker" };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = shade(x, y);
                    tex.SetPixel(x, y, v > 0f ? new Color(v, v, v, 1f) : Color.clear);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(px, py), ppu);
        }
    }
}

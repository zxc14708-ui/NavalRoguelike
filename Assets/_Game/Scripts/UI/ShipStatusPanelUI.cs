using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using Game.Ship;

namespace Game.UI
{
    /// <summary>
    /// 함 현황(하단 HUD 가운데 칸, 월드 오브 워쉽 조함 패널 참고).
    ///   - 위: 선체 내구 수치와 막대(피격 순간 붉게 깜빡임, 30% 미만이면 붉은 막대)
    ///   - 그 아래: 타각 게이지(좌·우 눈금, 현재 타각 숫자) — 나침반 바로 위
    ///   - 왼쪽: 나침반(북 위) 안에 위에서 본 함선 도식. 함수 방향대로 돌고, 모듈 칸마다 상태 색·무장 점
    ///   - 오른쪽: 기관 전령기(전속/FULL·3/4·1/2·1/4·STOP·후진) — 지금 잡은 출력에 불, 실제 속력은 화살표로
    ///   - 맨 아래 한 줄: 가동 모듈·탐지·소나
    /// </summary>
    public class ShipStatusPanelUI : MonoBehaviour
    {
        public const float Width = 290f, Height = 252f;

        private static readonly Color Dim = new(0.62f, 0.72f, 0.76f, 0.75f);
        private static readonly Color Alert = new(1f, 0.32f, 0.24f, 1f);
        private static readonly string[] Gears = { "FULL", "3/4", "1/2", "1/4", "STOP", "후진" };

        private const float GearTopY = 172f, GearStep = 18f, GearX = 172f;
        private const float DialX = 82f, DialY = 102f, DialRadius = 56f;
        private const float RudderHalf = 58f, RudderY = 186f;

        private TMP_FontAsset _font;
        private Color _accent, _gold;

        private TMP_Text _hullValue;
        private RectTransform _hullFill;
        private RawImage _hullFillImage;
        private RectTransform _diagramPivot;
        private ShipDiagramGraphic _diagram;
        private readonly TMP_Text[] _gearLabels = new TMP_Text[Gears.Length];
        private RectTransform _gearHighlight;
        private RectTransform _speedArrow;
        private TMP_Text _speedText;
        private RectTransform _rudderDot;
        private TMP_Text _rudderText;
        private TMP_Text _systemsText;

        private ShipController _player;
        private float _lastHp = -1f, _flashUntil, _pollTimer;

        public static ShipStatusPanelUI Create(Transform parent, TMP_FontAsset font, Color panelColor, Color accent, Color gold, Vector2 position)
        {
            var panel = Rect("Ship status", parent, position, new Vector2(Width, Height));
            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = panelColor;
            bg.raycastTarget = false;
            panel.gameObject.AddComponent<Canvas>();   // 매 프레임 바뀌는 요소가 HUD 전체를 다시 그리지 않게

            var ui = panel.gameObject.AddComponent<ShipStatusPanelUI>();
            ui._font = font;
            ui._accent = accent;
            ui._gold = gold;
            ui.Build(panel);
            return ui;
        }

        private void Build(RectTransform panel)
        {
            // --- 선체
            Label(panel, "Hull label", "HULL", new Vector2(12, 226), new Vector2(50, 20), 14, _accent);
            _hullValue = Label(panel, "Hull value", "-- / --", new Vector2(56, 222), new Vector2(170, 26), 21, _gold);
            Label(panel, "Hint", "TAB 상세", new Vector2(Width - 84, 226), new Vector2(72, 20), 12, _accent, TextAlignmentOptions.Right);
            var track = Raw(panel, "Hull track", Texture2D.whiteTexture, new Vector2(12, 214), new Vector2(Width - 24, 5), new Color(0.15f, 0.22f, 0.26f));
            _hullFill = Raw(track.transform, "Fill", Texture2D.whiteTexture, Vector2.zero, new Vector2(Width - 24, 5), _gold).rectTransform;
            _hullFillImage = _hullFill.GetComponent<RawImage>();

            // --- 나침반 + 함선 도식
            var dial = Raw(panel, "Dial", HudTextures.Dial, new Vector2(DialX - DialRadius, DialY - DialRadius), new Vector2(DialRadius * 2, DialRadius * 2), _accent);
            dial.color = new Color(_accent.r, _accent.g, _accent.b, 0.9f);
            Label(panel, "North", "N", new Vector2(DialX - 10, DialY + DialRadius - 1), new Vector2(20, 16), 11, _accent, TextAlignmentOptions.Center);

            _diagramPivot = Rect("Heading", panel, Vector2.zero, Vector2.zero);
            _diagramPivot.anchoredPosition = new Vector2(DialX, DialY);
            var diagramRect = Rect("Ship diagram", _diagramPivot, Vector2.zero, new Vector2(62, 84));
            diagramRect.anchorMin = diagramRect.anchorMax = new Vector2(0.5f, 0.5f);
            diagramRect.pivot = new Vector2(0.5f, 0.5f);
            diagramRect.anchoredPosition = Vector2.zero;
            _diagram = diagramRect.gameObject.AddComponent<ShipDiagramGraphic>();
            _diagram.raycastTarget = false;

            // --- 타각 게이지(나침반 위): 가운데 0, 양 끝 최대 타각, 절반 눈금
            var rudderTrack = Raw(panel, "Rudder", Texture2D.whiteTexture, new Vector2(DialX - RudderHalf, RudderY), new Vector2(RudderHalf * 2, 2), new Color(0.45f, 0.55f, 0.6f, 0.7f));
            foreach (float f in new[] { 0f, 0.5f, 1f, 1.5f, 2f })
            {
                bool center = Mathf.Approximately(f, 1f);
                Raw(rudderTrack.transform, "Tick", Texture2D.whiteTexture, new Vector2(RudderHalf * f - 0.75f, center ? -4 : -2), new Vector2(1.5f, center ? 10 : 6), Dim);
            }
            Label(panel, "Port", "좌", new Vector2(DialX - RudderHalf - 16, RudderY - 7), new Vector2(14, 16), 11, Dim, TextAlignmentOptions.Right);
            Label(panel, "Starboard", "우", new Vector2(DialX + RudderHalf + 3, RudderY - 7), new Vector2(14, 16), 11, Dim);
            _rudderDot = Raw(panel, "Rudder position", HudTextures.Triangle, Vector2.zero, new Vector2(10, 9), Alert).rectTransform;
            _rudderDot.pivot = new Vector2(0.5f, 0f);
            _rudderText = Label(panel, "Rudder angle", "0°", new Vector2(DialX - 30, RudderY + 4), new Vector2(60, 16), 12, Dim, TextAlignmentOptions.Center);

            // --- 기관 전령기
            _gearHighlight = Raw(panel, "Gear highlight", Texture2D.whiteTexture, new Vector2(GearX - 4, 0), new Vector2(46, 16),
                                   new Color(_accent.r, _accent.g, _accent.b, 0.22f)).rectTransform;
            for (int i = 0; i < Gears.Length; i++)
                _gearLabels[i] = Label(panel, Gears[i], Gears[i], new Vector2(GearX, GearTopY - i * GearStep - 7), new Vector2(40, 16), 13, Dim);

            _speedArrow = Rect("Speed", panel, Vector2.zero, new Vector2(100, 22));
            var arrow = Raw(_speedArrow, "Arrow", HudTextures.Triangle, new Vector2(0, 6), new Vector2(10, 10), Color.white);
            arrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            arrow.rectTransform.anchoredPosition = new Vector2(5, 11);
            arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, 90);   // 왼쪽(전령기)을 가리킴
            _speedText = Label(_speedArrow, "Value", "0.0", new Vector2(12, 0), new Vector2(90, 22), 19, Color.white);

            // --- 시스템
            _systemsText = Label(panel, "Systems", "", new Vector2(12, 8), new Vector2(Width - 24, 18), 12, Dim);
        }

        private void Update()
        {
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player != _player)
            {
                _player = player;
                _lastHp = -1f;
                _diagram.Bind(player != null ? player.Grid : null);
            }
            if (_player == null) return;

            float heading = _player.transform.eulerAngles.y;
            _diagramPivot.localRotation = Quaternion.Euler(0, 0, -heading);

            UpdateHull();
            UpdateEngine();

            float rudder = Mathf.Clamp(_player.RudderInput, -1f, 1f);
            _rudderDot.anchoredPosition = new Vector2(DialX + rudder * RudderHalf, RudderY - 10);
            int angle = Mathf.RoundToInt(Mathf.Abs(_player.RudderAngle));
            _rudderText.text = angle == 0 ? "0°" : $"{(rudder < 0f ? "좌" : "우")}{angle}°";
            _rudderText.color = angle == 0 ? Dim : Color.white;

            _pollTimer -= Time.unscaledDeltaTime;
            if (_pollTimer > 0f) return;
            _pollTimer = 0.25f;
            _diagram.Refresh();
            UpdateSystems();
        }

        private void UpdateHull()
        {
            float cur = _player.HullHp, max = Mathf.Max(1f, _player.HullMaxHp);
            if (_lastHp >= 0f && cur < _lastHp - 0.01f) _flashUntil = Time.unscaledTime + 0.35f;
            _lastHp = cur;

            float f = Mathf.Clamp01(cur / max);
            _hullFill.sizeDelta = new Vector2((Width - 28) * f, 5);
            bool flash = Time.unscaledTime < _flashUntil;
            _hullFillImage.color = flash || f < 0.3f ? Alert : _gold;
            _hullValue.text = $"{Mathf.CeilToInt(cur)} / {Mathf.CeilToInt(max)}";
            _hullValue.color = flash ? Alert : _gold;
        }

        private void UpdateEngine()
        {
            float throttle = _player.ThrottleInput;
            int gear = throttle >= 0.875f ? 0 : throttle >= 0.625f ? 1 : throttle >= 0.375f ? 2 : throttle >= 0.125f ? 3 : throttle > -0.125f ? 4 : 5;
            bool flank = _player.IsFlanking;

            for (int i = 0; i < _gearLabels.Length; i++)
            {
                bool on = i == gear;
                _gearLabels[i].color = on ? (flank && i == 0 ? _gold : Color.white) : Dim;
                _gearLabels[i].fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
            }
            _gearLabels[0].text = flank ? "전속" : "FULL";
            _gearHighlight.anchoredPosition = new Vector2(GearX - 4, GearTopY - gear * GearStep - 7.5f);

            // 실제 속력 화살표: STOP 줄에서 FULL 줄까지(전속이면 위로 조금 넘침), 후진은 아래 줄
            float speed = _player.CurrentSpeed;
            float stopY = GearTopY - 4 * GearStep, fullY = GearTopY;
            float y = speed >= 0f
                ? stopY + Mathf.Min(speed / Mathf.Max(0.01f, _player.BaseMaxSpeed), 1.3f) * (fullY - stopY)
                : stopY - Mathf.Min(-speed / Mathf.Max(0.01f, _player.BaseMaxSpeed * _player.ReverseSpeedRatio), 1f) * GearStep;
            _speedArrow.anchoredPosition = new Vector2(GearX + 44, y - 11);
            _speedText.text = $"{ShipController.ToKnots(Mathf.Abs(speed)):0.0}<size=12> kn</size>";
        }

        private void UpdateSystems()
        {
            if (_player.Grid == null || _player.Systems == null) return;
            int active = 0, total = 0;
            foreach (var m in _player.Grid.Modules) { total++; if (m.IsOperational) active++; }
            string sonar = _player.Systems.SonarRange > 0f ? $"{_player.Systems.SonarRange:0}" : "—";
            string modules = active < total ? $"<color=#FF6A4D>{active}</color>/{total}" : $"{active}/{total}";
            _systemsText.text = $"모듈 {modules}   ·   탐지 {_player.Systems.DetectionRange:0}   ·   소나 {sonar}";
        }

        // ------------------------------------------------------------ 만들기

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static RawImage Raw(Transform parent, string name, Texture texture, Vector2 position, Vector2 size, Color color)
        {
            var rect = Rect(name, parent, position, size);
            var img = rect.gameObject.AddComponent<RawImage>();
            img.texture = texture;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private TMP_Text Label(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize, Color color,
                               TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var rect = Rect(name, parent, position, size);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (_font != null) text.font = _font;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }
    }
}

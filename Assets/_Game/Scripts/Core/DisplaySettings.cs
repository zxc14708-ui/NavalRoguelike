using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 화면·접근성 설정(2026-10-11 점검 U04·U05·U06). PlayerPrefs "Naval.Display.*"에 저장하고 실행할 때 다시 적용한다.
    ///   - 화면 모드·해상도: 바꾸면 바로 적용하고 15초 안에 [유지]를 누르지 않으면 전 설정으로 되돌린다(설정 화면).
    ///     유니티가 마지막 창 모드·해상도를 스스로 기억하므로 여기서는 따로 저장하지 않는다.
    ///   - 수직 동기화 · 프레임 제한(30/60/120/144/무제한 — 수직 동기화가 켜져 있으면 모니터 주사율을 따른다) · 그래픽 품질.
    ///   - UI 크기 100/115/130%: 모든 UI 캔버스의 기준 해상도를 1920×1080 ÷ 배율로. 화면 비율이 달라도 잘리지 않게 Expand 맞춤.
    ///   - 번쩍임 줄이기: 선체 위험 테두리의 맥박·피격 번쩍임, 폭발 점광, 섬광 입자를 약하게.
    ///   - 적아 표식: 화면 안 함선 머리 위 모양 표식(적 ▼▲◯, 아군 ◆) 켜기/끄기.
    ///   - 그래픽 탭(점검 G04·G05): 항구 화질(높음/보통/낮음 — 렌더 텍스처 크기·MSAA·그림자맵), 항구 배경 정지(멈춘 화면 한 장만 그림),
    ///     장식 효과(높음/보통/낮음 — 물보라·연기·불꽃 입자 양, 낮음이면 바다 생물 숨김), 연기 옅게(연돌·격침 연기 절반 투명).
    ///     위협·피격 표시(미사일·어뢰·폭발 섬광·경고)는 줄이지 않는다.
    /// </summary>
    public static class DisplaySettings
    {
        private const string Prefix = "Naval.Display.";
        public static readonly Vector2 ReferenceResolution = new(1920f, 1080f);
        public static readonly int[] FrameLimits = { 30, 60, 120, 144, -1 };
        public static readonly float[] UiScales = { 1f, 1.15f, 1.3f };

        private static bool _loaded;
        private static bool _vsync = true;
        private static int _frameLimit;   // FrameLimits 순번
        private static int _uiScale;
        private static bool _reduceFlashes;
        private static bool _identityMarkers = true;
        private static int _harborQuality, _effectsQuality;
        private static bool _harborStill, _thinSmoke;

        public static event System.Action Changed;

        public static bool VSync { get { Load(); return _vsync; } }
        public static int FrameLimitIndex { get { Load(); return _frameLimit; } }
        public static int UiScaleIndex { get { Load(); return _uiScale; } }
        public static float UiScale => UiScales[Mathf.Clamp(UiScaleIndex, 0, UiScales.Length - 1)];
        public static bool ReduceFlashes { get { Load(); return _reduceFlashes; } }
        /// <summary>적아 식별 표식(▼▲◯◆)을 단다(IdentityMarkersUI).</summary>
        public static bool IdentityMarkers { get { Load(); return _identityMarkers; } }
        public static readonly string[] QualityNames = { "높음", "보통", "낮음" };
        /// <summary>항구 화질 0 높음 · 1 보통 · 2 낮음.</summary>
        public static int HarborQuality { get { Load(); return _harborQuality; } }
        /// <summary>항구 배경을 멈추고(교통·회전) 멈춘 화면을 다시 그리지 않는다.</summary>
        public static bool HarborStill { get { Load(); return _harborStill; } }
        /// <summary>장식 효과 0 높음 · 1 보통 · 2 낮음.</summary>
        public static int EffectsQuality { get { Load(); return _effectsQuality; } }
        public static bool ThinSmoke { get { Load(); return _thinSmoke; } }
        /// <summary>장식 입자를 내는 비율(높음 1 · 보통 0.65 · 낮음 0.35).</summary>
        public static float DecorDensity => EffectsQuality switch { 0 => 1f, 1 => 0.65f, _ => 0.35f };

        /// <summary>번쩍이는 연출의 세기 배율(줄이기면 0.35).</summary>
        public static float FlashScale => ReduceFlashes ? 0.35f : 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyOnStart()
        {
            Load();
            ApplyFrameSettings();
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            // 저장한 적이 없으면 지금 동작 그대로(품질 단계의 수직 동기화, 프레임 제한 없음)
            _vsync = PlayerPrefs.GetInt(Prefix + "VSync", QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
            _frameLimit = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "FrameLimit", FrameLimits.Length - 1), 0, FrameLimits.Length - 1);
            _uiScale = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "UiScale", 0), 0, UiScales.Length - 1);
            _reduceFlashes = PlayerPrefs.GetInt(Prefix + "ReduceFlashes", 0) == 1;
            _identityMarkers = PlayerPrefs.GetInt(Prefix + "IdentityMarkers", 1) == 1;
            _harborQuality = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "HarborQuality", 0), 0, 2);
            _effectsQuality = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "EffectsQuality", 0), 0, 2);
            _harborStill = PlayerPrefs.GetInt(Prefix + "HarborStill", 0) == 1;
            _thinSmoke = PlayerPrefs.GetInt(Prefix + "ThinSmoke", 0) == 1;
            int quality = PlayerPrefs.GetInt(Prefix + "Quality", -1);
            if (quality >= 0 && quality < QualitySettings.names.Length && quality != QualitySettings.GetQualityLevel())
                QualitySettings.SetQualityLevel(quality, true);
        }

        private static void ApplyFrameSettings()
        {
            QualitySettings.vSyncCount = _vsync ? 1 : 0;
            Application.targetFrameRate = _vsync ? -1 : FrameLimits[_frameLimit];
        }

        private static void Save(string key, int value)
        {
            PlayerPrefs.SetInt(Prefix + key, value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void SetVSync(bool on) { Load(); _vsync = on; ApplyFrameSettings(); Save("VSync", on ? 1 : 0); }
        public static void SetFrameLimit(int index) { Load(); _frameLimit = Mathf.Clamp(index, 0, FrameLimits.Length - 1); ApplyFrameSettings(); Save("FrameLimit", _frameLimit); }
        public static void SetReduceFlashes(bool on) { Load(); _reduceFlashes = on; Save("ReduceFlashes", on ? 1 : 0); }
        public static void SetIdentityMarkers(bool on) { Load(); _identityMarkers = on; Save("IdentityMarkers", on ? 1 : 0); }
        public static void SetHarborQuality(int q) { Load(); _harborQuality = Mathf.Clamp(q, 0, 2); Save("HarborQuality", _harborQuality); }
        public static void SetHarborStill(bool on) { Load(); _harborStill = on; Save("HarborStill", on ? 1 : 0); }
        public static void SetEffectsQuality(int q) { Load(); _effectsQuality = Mathf.Clamp(q, 0, 2); Save("EffectsQuality", _effectsQuality); }
        public static void SetThinSmoke(bool on) { Load(); _thinSmoke = on; Save("ThinSmoke", on ? 1 : 0); }

        public static void SetQuality(int level)
        {
            Load();
            level = Mathf.Clamp(level, 0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(level, true);
            ApplyFrameSettings();   // 품질 단계가 수직 동기화를 덮어쓰므로 다시
            Save("Quality", level);
        }

        public static void SetUiScale(int index)
        {
            Load();
            _uiScale = Mathf.Clamp(index, 0, UiScales.Length - 1);
            ApplyUiScaleToAll();
            Save("UiScale", _uiScale);
        }

        /// <summary>캔버스 하나에 UI 크기를 적용한다(HUD가 만들 때 부른다).</summary>
        public static void ApplyUiScale(CanvasScaler scaler)
        {
            if (scaler == null) return;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referenceResolution = ReferenceResolution / UiScale;
        }

        public static void ApplyUiScaleToAll()
        {
            foreach (var scaler in Object.FindObjectsByType<CanvasScaler>(FindObjectsSortMode.None))
                if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize) ApplyUiScale(scaler);
        }

        // ------------------------------------------------------------ 화면 모드 · 해상도

        public static readonly (FullScreenMode mode, string name)[] Modes =
        {
            (FullScreenMode.FullScreenWindow, "전체 화면(테두리 없음)"),
            (FullScreenMode.ExclusiveFullScreen, "전용 전체 화면"),
            (FullScreenMode.Windowed, "창 모드"),
        };

        public static int ModeIndex()
        {
            for (int i = 0; i < Modes.Length; i++) if (Modes[i].mode == Screen.fullScreenMode) return i;
            return Screen.fullScreenMode == FullScreenMode.MaximizedWindow ? 2 : 0;
        }

        /// <summary>고를 수 있는 해상도(크기만, 겹치지 않게, 작은 것부터). 지금 창 크기도 넣는다.</summary>
        public static List<Vector2Int> Resolutions()
        {
            var list = new List<Vector2Int>();
            foreach (var r in Screen.resolutions)
            {
                var size = new Vector2Int(r.width, r.height);
                if (size.x >= 1024 && !list.Contains(size)) list.Add(size);
            }
            var current = new Vector2Int(Screen.width, Screen.height);
            if (!list.Contains(current)) list.Add(current);
            list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return list;
        }

        public static void ApplyScreen(Vector2Int size, FullScreenMode mode) => Screen.SetResolution(size.x, size.y, mode);

        public static string FrameLimitName(int index) => FrameLimits[index] < 0 ? "무제한" : $"{FrameLimits[index]} fps";
    }
}

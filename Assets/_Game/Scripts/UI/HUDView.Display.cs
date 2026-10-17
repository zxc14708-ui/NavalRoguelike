using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 설정 › 화면 탭(2026-10-11 점검 U04·U05·U06): 화면 모드 · 해상도 · 수직 동기화 · 프레임 제한 · 그래픽 품질 · UI 크기 · 번쩍임 줄이기.
    /// 값은 ◀ ▶로 바꾸고 바로 적용·저장한다(DisplaySettings). 화면 모드·해상도는 15초 안에 [유지]를 누르지 않으면 전 설정으로 되돌린다
    /// — 모니터가 받지 못하는 설정으로 화면이 사라져도 기다리면 돌아온다.
    /// </summary>
    public partial class HUDView
    {
        private const float RevertSeconds = 15f;

        private float _revertAt = -1f;
        private Vector2Int _revertSize;
        private FullScreenMode _revertMode;
        private string _displayNotice;

        private string DisplayMessage()
        {
            if (_revertAt > 0f) return $"새 화면 설정 — {Mathf.CeilToInt(_revertAt - Time.unscaledTime)}초 안에 [유지]를 누르지 않으면 되돌립니다.";
            return _displayNotice ?? "값은 즉시 적용되며 자동 저장됩니다. 화면 모드·해상도는 확인 후 유지됩니다.";
        }

        private void BuildDisplaySettings(Transform page)
        {
            var sizes = DisplaySettings.Resolutions();
            var current = new Vector2Int(Screen.width, Screen.height);
            int sizeIndex = Mathf.Max(0, sizes.IndexOf(current));
            int mode = DisplaySettings.ModeIndex();
            string[] quality = QualitySettings.names;
            int q = QualitySettings.GetQualityLevel();
            int frame = DisplaySettings.FrameLimitIndex;
            int ui = DisplaySettings.UiScaleIndex;

            float y = 348f;
            void Next() => y -= 42f;

            Cycler(page, y, "화면 모드", DisplaySettings.Modes[mode].name, d =>
                ChangeScreen(sizes[sizeIndex], DisplaySettings.Modes[(mode + d + DisplaySettings.Modes.Length) % DisplaySettings.Modes.Length].mode));
            Next();
            Cycler(page, y, "해상도", $"{sizes[sizeIndex].x} × {sizes[sizeIndex].y}", d =>
                ChangeScreen(sizes[(sizeIndex + d + sizes.Count) % sizes.Count], DisplaySettings.Modes[mode].mode));
            Next();
            Cycler(page, y, "수직 동기화", DisplaySettings.VSync ? "켜기" : "끄기", _ =>
            {
                DisplaySettings.SetVSync(!DisplaySettings.VSync);
                ShowSettingsTab(2);
            });
            Next();
            Cycler(page, y, "프레임 제한", DisplaySettings.VSync ? "모니터 주사율(수직 동기화)" : DisplaySettings.FrameLimitName(frame), d =>
            {
                if (DisplaySettings.VSync) { _settingsMessage.text = "수직 동기화를 끄면 프레임 제한을 고를 수 있습니다."; return; }
                DisplaySettings.SetFrameLimit((frame + d + DisplaySettings.FrameLimits.Length) % DisplaySettings.FrameLimits.Length);
                ShowSettingsTab(2);
            });
            Next();
            Cycler(page, y, "그래픽 품질", quality.Length > 0 ? quality[q] : "기본", d =>
            {
                if (quality.Length == 0) return;
                DisplaySettings.SetQuality((q + d + quality.Length) % quality.Length);
                ShowSettingsTab(2);
            });
            Next();
            Cycler(page, y, "UI 크기", $"{DisplaySettings.UiScales[ui] * 100f:0}%", d =>
            {
                DisplaySettings.SetUiScale((ui + d + DisplaySettings.UiScales.Length) % DisplaySettings.UiScales.Length);
                ShowSettingsTab(2);
            });
            Next();
            Cycler(page, y, "번쩍임 줄이기", DisplaySettings.ReduceFlashes ? "켜기 — 위험 테두리 맥박·폭발 섬광 약하게" : "끄기", _ =>
            {
                DisplaySettings.SetReduceFlashes(!DisplaySettings.ReduceFlashes);
                ShowSettingsTab(2);
            });
            Next();
            Cycler(page, y, "적아 표식", DisplaySettings.IdentityMarkers ? "켜기 — 적 ▼ 항공기 ▲ 잠수함 ◯ · 아군 ◆" : "끄기", _ =>
            {
                DisplaySettings.SetIdentityMarkers(!DisplaySettings.IdentityMarkers);
                ShowSettingsTab(2);
            });

            if (_revertAt > 0f)
            {
                var keep = MenuButton(page, "유지", new Vector2(790, 10), new Vector2(140, 36), ConsoleActive, 17);
                keep.onClick.AddListener(() =>
                {
                    _revertAt = -1f;
                    _displayNotice = "새 화면 설정을 유지합니다.";
                    ShowSettingsTab(2);
                });
            }
        }

        /// <summary>
        /// 설정 › 정보 탭(점검 O07): 버전 · 런 시드 · 크레딧(Resources/Credits.txt — 출처를 여기 적는다) ·
        /// [진단 내보내기](persistentDataPath/Diagnostics에 한 파일) · [폴더 열기].
        /// </summary>
        private void BuildAboutPage(Transform page)
        {
            Label(page, "Version", $"<b>NAVAL ROGUELIKE</b>  <color=#9fb3c0>{Diagnostics.Version} · Unity {Application.unityVersion}</color>", new Vector2(20, 350), new Vector2(900, 30), 19, Color.white);
            Label(page, "Seed", $"지금 판 런 시드 <color=#e0c05a>{RunSeed.Current}</color>  <size=85%><color=#9fb3c0>(실행 인자 -seed 값으로 같은 추첨을 다시 만들 수 있다)</color></size>", new Vector2(20, 318), new Vector2(900, 26), 16, ConsoleText);
            var credits = Resources.Load<TextAsset>("Credits");
            var body = Label(page, "Credits", credits != null ? credits.text : "크레딧 파일(Resources/Credits.txt)이 없습니다.", new Vector2(20, 64), new Vector2(908, 244), 14, Color.white);
            body.alignment = TextAlignmentOptions.TopLeft;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.overflowMode = TextOverflowModes.Ellipsis;
            var export = MenuButton(page, "진단 내보내기", new Vector2(20, 12), new Vector2(200, 40), ConsoleActive, 17);
            export.name = "Export diagnostics";
            export.onClick.AddListener(() =>
            {
                string path = Diagnostics.Export(out string error);
                _settingsMessage.text = path != null ? $"저장했습니다: {System.IO.Path.GetFileName(path)} — [폴더 열기]로 찾을 수 있습니다." : $"진단 파일을 만들지 못했습니다: {error}";
                LastDiagnosticsPath = path;
            });
            var open = MenuButton(page, "폴더 열기", new Vector2(230, 12), new Vector2(160, 40), ConsoleIdle, 17);
            open.onClick.AddListener(() =>
            {
                System.IO.Directory.CreateDirectory(Diagnostics.Folder);
                Application.OpenURL("file:///" + Diagnostics.Folder.Replace('\\', '/'));
            });
        }

        /// <summary>검증용: 마지막으로 내보낸 진단 파일.</summary>
        public string LastDiagnosticsPath { get; private set; }

        /// <summary>설정 › 그래픽 탭(점검 G04·G05): 항구 화질 · 항구 배경 정지 · 장식 효과 · 연기 옅게.</summary>
        private void BuildGraphicsSettings(Transform page)
        {
            var names = DisplaySettings.QualityNames;
            int harbor = DisplaySettings.HarborQuality, effects = DisplaySettings.EffectsQuality;
            float y = 348f;
            void Next() => y -= 42f;
            Cycler(page, y, "항구 화질", harbor switch
            {
                0 => "높음 — 1.5배 해상도 · MSAA 8 · 그림자 4096",
                1 => "보통 — 화면 해상도 · MSAA 4 · 그림자 2048",
                _ => "낮음 — 최대 1080 · MSAA 2 · 그림자 1024",
            }, d => { DisplaySettings.SetHarborQuality((harbor + d + 3) % 3); ShowSettingsTab(3); });
            Next();
            Cycler(page, y, "항구 배경 정지", DisplaySettings.HarborStill ? "켜기 — 교통·회전 멈춤, 멈춘 화면은 다시 그리지 않음" : "끄기", _ =>
            {
                DisplaySettings.SetHarborStill(!DisplaySettings.HarborStill);
                ShowSettingsTab(3);
            });
            Next();
            Cycler(page, y, "장식 효과", effects switch
            {
                0 => "높음 — 물보라·연기·바다 생물 모두",
                1 => "보통 — 물보라·연기 입자 65%",
                _ => "낮음 — 입자 35% · 바다 생물 숨김",
            }, d => { DisplaySettings.SetEffectsQuality((effects + d + 3) % 3); ShowSettingsTab(3); });
            Next();
            Cycler(page, y, "연기 옅게", DisplaySettings.ThinSmoke ? "켜기 — 연돌·격침 연기 절반 투명" : "끄기", _ =>
            {
                DisplaySettings.SetThinSmoke(!DisplaySettings.ThinSmoke);
                ShowSettingsTab(3);
            });
        }

        /// <summary>설정 한 줄: 이름 | ◀ 값 ▶. 값을 누르면 다음 값(▶와 같다).</summary>
        private void Cycler(Transform page, float y, string title, string value, System.Action<int> change)
        {
            var line = Panel(page, title, Vector2.zero, new Vector2(14, y), new Vector2(920, 38), ConsoleIdle);
            Label(line, "Title", title, new Vector2(14, 6), new Vector2(260, 27), 18, Color.white);
            var prev = MenuButton(line, "◀", new Vector2(300, 3), new Vector2(40, 32), ConsoleActive, 16);
            var label = MenuButton(line, value, new Vector2(346, 3), new Vector2(520, 32), new Color(0.02f, 0.11f, 0.075f, 1f), 16);
            label.name = $"{title} value";
            var next = MenuButton(line, "▶", new Vector2(872, 3), new Vector2(40, 32), ConsoleActive, 16);
            prev.onClick.AddListener(() => change(-1));
            next.onClick.AddListener(() => change(+1));
            label.onClick.AddListener(() => change(+1));
        }

        /// <summary>화면 모드·해상도 바꾸기: 지금 값을 기억해 두고 적용, 15초 뒤 [유지]가 없으면 되돌린다.</summary>
        private void ChangeScreen(Vector2Int size, FullScreenMode mode)
        {
            if (_revertAt < 0f)
            {
                _revertSize = new Vector2Int(Screen.width, Screen.height);
                _revertMode = Screen.fullScreenMode;
            }
            DisplaySettings.ApplyScreen(size, mode);
            _revertAt = Time.unscaledTime + RevertSeconds;
            _displayNotice = null;
            StartCoroutine(RefreshDisplayTabNextFrame());
        }

        private System.Collections.IEnumerator RefreshDisplayTabNextFrame()
        {
            yield return null;   // 해상도·모드는 다음 프레임에 바뀐다
            yield return null;
            if (_settingsOverlay != null && _settingsOverlay.activeInHierarchy && _settingsTab == 2) ShowSettingsTab(2);
        }

        private void UpdateDisplayRevert()
        {
            if (_revertAt < 0f) return;
            if (Time.unscaledTime >= _revertAt)
            {
                _revertAt = -1f;
                DisplaySettings.ApplyScreen(_revertSize, _revertMode);
                _displayNotice = "확인이 없어 이전 화면 설정으로 되돌렸습니다.";
                StartCoroutine(RefreshDisplayTabNextFrame());
                return;
            }
            if (_settingsMessage != null && _settingsTab == 2 && _settingsOverlay != null && _settingsOverlay.activeInHierarchy)
                _settingsMessage.text = DisplayMessage();
        }
    }
}

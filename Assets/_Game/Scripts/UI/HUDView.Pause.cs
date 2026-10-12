using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 전투 일시정지(2026-10-11 점검 U01). 전투 중 ESC로 열고 닫는다.
    ///   계속 · 설정 · 사전 · 조작 안내 | 재출격(같은 함정) · 항구로(함정 변경) · 게임 종료
    ///   - 열려 있는 동안 상태는 Paused(시간 정지), 소리도 멈춘다. 닫으면 전투로 돌아간다.
    ///   - 설정·사전은 시작 화면과 같은 화면을 이 위에 띄운다. 그 화면이 열려 있으면 ESC는 그 화면만 닫는다.
    ///   - 재출격·항구·종료는 이번 판을 버리므로 한 번 더 눌러야 실행된다.
    ///   - 정비(카드·배치)·항로·교리 화면에서는 열리지 않는다 — 그 화면들은 이미 시간이 멈춰 있고 ESC를 자체로 쓴다.
    /// </summary>
    public partial class HUDView
    {
        private static readonly Color PauseDanger = new Color(0.30f, 0.11f, 0.09f, 1f);

        private GameObject _pauseRoot;
        private TMP_Text _pauseInfo;
        private GameObject _pauseGuide;
        private TMP_Text _pauseGuideBody;
        private Button _pendingConfirm;
        private string _pendingConfirmLabel;
        private bool _overlayOpenLastFrame;

        public bool IsPauseMenuOpen => _pauseRoot != null && _pauseRoot.activeSelf;

        /// <summary>설정·사전 화면을 띄울 곳: 일시정지 중이면 일시정지 화면 위, 아니면 시작 화면 위.</summary>
        private Transform OverlayParent => IsPauseMenuOpen ? _pauseRoot.transform
            : _menu != null ? _menu.transform : GetComponentInParent<Canvas>().rootCanvas.transform;

        private bool SubOverlayOpen =>
            (_settingsOverlay != null && _settingsOverlay.activeInHierarchy) ||
            (_codex != null && _codex.gameObject.activeInHierarchy) ||
            (_records != null && _records.gameObject.activeInHierarchy);

        private void UpdatePause()
        {
            // 설정·사전이 이번 프레임이나 바로 전 프레임에 열려 있었으면 ESC는 그 화면 몫(닫는 순서가 컴포넌트마다 달라서)
            bool sub = SubOverlayOpen, subBefore = _overlayOpenLastFrame;
            _overlayOpenLastFrame = sub;
            if (subBefore && !sub && IsPauseMenuOpen) RefreshPauseGuide();   // 설정에서 키를 바꾸고 돌아왔을 수 있다
            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame || sub || subBefore) return;
            if (IsPauseMenuOpen) ResumeFromPause();
            else OpenPauseMenu();
        }

        /// <summary>전투 중일 때만 연다. 열었으면 true.</summary>
        public bool OpenPauseMenu()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing || RunRestart.InProgress) return false;
            if (_routeChoice != null || _doctrineChoice != null) return false;
            if (_pauseRoot == null) BuildPauseMenu();
            gm.SetState(GameState.Paused);
            _pauseRoot.transform.SetAsLastSibling();
            _pauseRoot.SetActive(true);
            ClearPendingConfirm();
            RefreshPauseInfo();
            RefreshPauseGuide();
            AudioListener.pause = true;
            return true;
        }

        /// <summary>
        /// 전투 중 사전 바로 열기(2026-10-11 점검 U08): 일시정지하고 사전을 그 항목(id)으로 연다.
        /// 사전을 닫으면 일시정지 화면이 남고, ESC/[계속]으로 전투로 돌아간다.
        /// </summary>
        public bool OpenCodexAt(string entryId)
        {
            if (!IsPauseMenuOpen && !OpenPauseMenu()) return false;
            ShowCodex();
            return _codex != null && _codex.JumpTo(entryId);
        }

        public void ResumeFromPause()
        {
            if (!IsPauseMenuOpen) return;
            if (_settingsOverlay != null && _settingsOverlay.activeSelf) CloseSettings();
            if (_codex != null) _codex.Close();
            ClearPendingConfirm();
            _pauseRoot.SetActive(false);
            AudioListener.pause = false;
            var gm = GameManager.Instance;
            if (gm != null && gm.State == GameState.Paused) gm.SetState(GameState.Playing);
        }

        private void OnDestroy()
        {
            if (IsPauseMenuOpen) AudioListener.pause = false;
        }

        private void BuildPauseMenu()
        {
            var canvas = GetComponentInParent<Canvas>().rootCanvas;
            var overlay = Panel(canvas.transform, "Pause menu", Vector2.zero, Vector2.zero, Vector2.zero,
                new Color(0.004f, 0.02f, 0.025f, 0.78f));
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().raycastTarget = true;
            _pauseRoot = overlay.gameObject;

            var content = Panel(overlay, "Pause console", new Vector2(0.5f, 0.5f), new Vector2(-500, -320),
                new Vector2(560, 640), ConsoleBackground);
            Panel(content, "Top signal", Vector2.zero, new Vector2(0, 636), new Vector2(560, 4), ConsoleActive);
            Label(content, "Eyebrow", "FLEET COMMAND  /  PAUSED", new Vector2(30, 594), new Vector2(500, 27), 16, ConsoleText);
            Label(content, "Title", "작전 일시정지", new Vector2(30, 540), new Vector2(500, 51), 35, Color.white);
            _pauseInfo = Label(content, "Info", "", new Vector2(30, 494), new Vector2(500, 44), 17, ConsoleText);

            PauseButton(content, "계속  /  ESC", 432, ConsoleActive).onClick.AddListener(ResumeFromPause);
            PauseButton(content, "설정  /  조작 · 음향", 372, ConsoleIdle).onClick.AddListener(() => { ClearPendingConfirm(); ShowSettings(); });
            PauseButton(content, "사전  /  무장 · 호위함 · 적", 312, ConsoleIdle).onClick.AddListener(() => { ClearPendingConfirm(); ShowCodex(); });
            PauseButton(content, "조작 안내  보기 / 숨기기", 252, ConsoleIdle).onClick.AddListener(() =>
            {
                ClearPendingConfirm();
                _pauseGuide.SetActive(!_pauseGuide.activeSelf);
            });
            Panel(content, "Divider", Vector2.zero, new Vector2(30, 226), new Vector2(500, 1), ConsoleActive);
            var concept = SelectedStartingShip();
            ConfirmButton(content, concept != null ? $"재출격  /  {concept.Title}" : "재출격  /  처음부터", 158,
                "한 번 더 누르면 처음부터 다시 출격", () => RunRestart.Retry(SelectedStartingShip()));
            ConfirmButton(content, "항구로  /  함정 다시 고르기", 98,
                "한 번 더 누르면 이번 판을 끝내고 항구로", RunRestart.ReturnToHarbor);
            ConfirmButton(content, "게임 종료", 38, "한 번 더 누르면 게임 종료", RunRestart.Quit);

            BuildPauseGuide(overlay);
            _pauseRoot.SetActive(false);
        }

        private void BuildPauseGuide(RectTransform overlay)
        {
            var guide = Panel(overlay, "Controls guide", new Vector2(0.5f, 0.5f), new Vector2(80, -320),
                new Vector2(420, 640), ConsoleBackground);
            _pauseGuide = guide.gameObject;
            Panel(guide, "Top signal", Vector2.zero, new Vector2(0, 636), new Vector2(420, 4), ConsoleIdle);
            Label(guide, "Header", "조작 안내  /  CONTROLS", new Vector2(24, 590), new Vector2(380, 30), 19, ConsoleText);
            _pauseGuideBody = Label(guide, "Bindings", "", new Vector2(24, 76), new Vector2(380, 500), 17, Color.white);
            _pauseGuideBody.alignment = TextAlignmentOptions.TopLeft;
            _pauseGuideBody.lineSpacing = 6f;
            Label(guide, "Hint", "키는 설정 › 조작키에서 바꿀 수 있습니다.", new Vector2(24, 22), new Vector2(380, 26), 14, ConsoleText);
            RefreshPauseGuide();
        }

        /// <summary>조작 안내를 지금 키 배치로 다시 쓴다(일시정지를 열 때·설정을 닫고 돌아올 때).</summary>
        private void RefreshPauseGuide()
        {
            if (_pauseGuideBody == null) return;
            var sb = new System.Text.StringBuilder();
            foreach (var (action, title) in ControlTitles)
                sb.Append(title).Append("<pos=66%><color=#A8FFC0>").Append(GameSettings.BindingLabel(action)).Append("</color>\n");
            sb.Append("일시정지 · 화면 닫기<pos=66%><color=#A8FFC0>ESC</color>");
            _pauseGuideBody.text = sb.ToString();
        }

        /// <summary>검증용: 일시정지 조작 안내 본문.</summary>
        public string PauseGuideText => _pauseGuideBody != null ? _pauseGuideBody.text : "";

        private Button PauseButton(Transform parent, string title, float y, Color color) =>
            MenuButton(parent, title, new Vector2(30, y), new Vector2(500, 52), color, 21);

        /// <summary>판을 버리는 버튼: 처음 누르면 문구가 확인으로 바뀌고, 한 번 더 누르면 실행한다.</summary>
        private void ConfirmButton(Transform parent, string title, float y, string confirm, System.Action action)
        {
            var button = PauseButton(parent, title, y, PauseDanger);
            button.name = title;
            var label = button.GetComponentInChildren<TMP_Text>();
            button.onClick.AddListener(() =>
            {
                if (_pendingConfirm == button) { ClearPendingConfirm(); UiSound.Play(UiSound.Cue.Confirm); action(); return; }
                ClearPendingConfirm();
                _pendingConfirm = button;
                _pendingConfirmLabel = label.text;
                label.text = confirm;
                label.color = Gold;
            });
        }

        private void ClearPendingConfirm()
        {
            if (_pendingConfirm != null)
            {
                var label = _pendingConfirm.GetComponentInChildren<TMP_Text>();
                label.text = _pendingConfirmLabel;
                label.color = Color.white;
            }
            _pendingConfirm = null;
        }

        private void RefreshPauseInfo()
        {
            if (_pauseInfo == null) return;
            var gm = GameManager.Instance;
            float t = gm != null ? gm.ElapsedTime : 0f;
            string sea = Game.World.SeaRegion.Active ? $" · {Game.World.SeaRegion.CurrentProfile.Name}" : "";
            _pauseInfo.text = $"스테이지 {_stage}{sea} · 구간 {_phase}/{_phaseCount} · 작전 {(int)t / 60}:{(int)t % 60:00} · 격침 {(gm != null ? gm.TotalKills : 0)}" +
                              $"\n<size=80%><color=#9fb3c0>시드 {RunSeed.Current} · {Diagnostics.Version}</color></size>";
        }

        private static Game.Data.StartingShipConcept SelectedStartingShip()
        {
            var init = FindFirstObjectByType<Game.Ship.ShipInitializer>();
            return init != null ? init.SelectedConcept : null;
        }

        /// <summary>
        /// 재출격으로 다시 연 씬: 시작 화면(항구) 없이 지난 판의 함정으로 바로 출항한다(2026-10-11 점검 U02).
        /// 함정을 고를 수 없으면 평소 시작 화면을 연다.
        /// </summary>
        private bool TryQuickLaunch(System.Action onLaunch)
        {
            if (!RunRestart.ConsumeQuickLaunch(out var concept)) return false;
            var initializer = FindFirstObjectByType<Game.Ship.ShipInitializer>();
            string reason = null;
            if (initializer != null && concept != null && !initializer.TrySelect(concept, out reason))
            {
                Debug.LogWarning($"[HUD] 재출격 함정 선택 실패 — 시작 화면을 엽니다: {reason}");
                return false;
            }
            if (initializer != null) initializer.LockForLaunch();
            _combatGroup.alpha = 1;
            _combatGroup.blocksRaycasts = true;
            onLaunch?.Invoke();
            return true;
        }
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Game.Modules;
using Game.Ship;

namespace Game.UI
{
    /// <summary>
    /// 정비 화면 — 같은 블록 설치 카드로 강화(2026-11-22 사용자). 예전 "장비 강화" 카드는 없앴다.
    ///   - 이미 가진 블록의 설치 카드를 고르면 배치 화면에서 같은 블록들에 강화 표시(초록 = 강화 가능 · 금색 = 최고 단계)가 뜬다.
    ///   - 들고 있는 블록을 같은 블록 위에 놓으면 "○○를 Lv N으로 강화하시겠습니까?" 확인 창 → [강화](Enter · Y) / [취소](Esc · N · 우클릭).
    ///     강화하면 카드를 쓴 것으로 치고 바로 전투로 돌아간다(결과는 안내 띠). 빈 칸에 놓으면 지금처럼 하나 더 설치.
    ///   - 지원 블록(ModuleUpgrades.IsSupport)은 가진 것이 최고 단계(Lv3)가 아니면 하나 더 놓을 수 없다 — 강화만(안내 문구).
    /// </summary>
    public partial class RefitUI
    {
        private RectTransform _confirmPanel;
        private TMP_Text _confirmText;
        private RectTransform _confirmYes, _confirmNo;
        private ModuleInstance _confirmTarget;

        /// <summary>검증용: 강화 확인 창이 떠 있는가 · 그 대상.</summary>
        public bool UpgradeConfirmOpen => _confirmTarget != null;
        public ModuleInstance UpgradeConfirmTarget => _confirmTarget;

        /// <summary>지금 들고 있는 보상 블록이 강화만 할 수 있는가(지원 블록 — 가진 것이 아직 최고 단계가 아님).</summary>
        private bool HeldUpgradeOnly => _held != null && _heldIsReward && ModuleUpgrades.MustMaxBeforeAnother(grid, _held);

        /// <summary>
        /// 들고 있는 보상 블록을 놓으려 할 때(Confirm): 커서 아래가 같은 블록이고 강화할 수 있으면 확인 창을 띄운다(true).
        /// 강화만 되는 지원 블록인데 빈 칸이면 막고 안내한다(true). 그 밖에는 false — 평소 설치로 넘어간다.
        /// </summary>
        private bool TryOfferInstallUpgrade()
        {
            if (_held == null || !_heldIsReward || grid == null) return false;
            var target = grid.Get(_cursor);
            if (target != null && target.Definition == _held)
            {
                if (ModuleUpgrades.CanUpgradeWith(target, _held)) { OpenUpgradeConfirm(target); return true; }
                Game.Core.UiSound.Play(Game.Core.UiSound.Cue.Fail);
                SetStatusNote($"{_held.DisplayName}: 이미 최고 단계(Lv{target.Level})입니다");
                return true;
            }
            if (HeldUpgradeOnly)
            {
                Game.Core.UiSound.Play(Game.Core.UiSound.Cue.Fail);
                SetStatusNote($"지원 블록은 가진 {_held.DisplayName}를 Lv{ModuleUpgrades.MaxLevel(_held) + 1}까지 강화해야 하나 더 놓을 수 있습니다 — 같은 블록 위에 놓아 강화하세요");
                return true;
            }
            return false;
        }

        private void OpenUpgradeConfirm(ModuleInstance target)
        {
            EnsureConfirmPanel();
            _confirmTarget = target;
            if (_confirmPanel == null) { ApplyInstallUpgrade(); return; }   // 창을 못 만들면 바로 강화
            var stage = ModuleUpgrades.ProfileFor(target.Definition)?.GetStage(target.UpgradeLevel + 1);
            _confirmText.text = $"<b>{target.Definition.DisplayName}</b> ({target.Origin.X}, {target.Origin.Z})\n" +
                                $"<size=125%>Lv{target.Level} → <color=#5be37a>Lv{target.Level + 1}</color></size>  으로 강화하시겠습니까?" +
                                (stage != null && !string.IsNullOrEmpty(stage.Description) ? $"\n<size=80%><color=#c8d4dc>{stage.Description}</color></size>" : "");
            _confirmPanel.gameObject.SetActive(true);
            _confirmPanel.SetAsLastSibling();
            Game.Core.UiSound.Play(Game.Core.UiSound.Cue.Confirm);
        }

        private void CloseUpgradeConfirm()
        {
            _confirmTarget = null;
            if (_confirmPanel != null) _confirmPanel.gameObject.SetActive(false);
        }

        /// <summary>확인 창 [강화]: 한 단계 올리고 카드를 쓴 것으로 친 뒤 전투로(안내 띠).</summary>
        private void ApplyInstallUpgrade()
        {
            var target = _confirmTarget;
            CloseUpgradeConfirm();
            if (target == null || !target.ApplyUpgrade()) { Game.Core.UiSound.Play(Game.Core.UiSound.Cue.Fail); return; }
            var stage = ModuleUpgrades.ProfileFor(target.Definition)?.GetStage(target.UpgradeLevel);
            _upgradeResult = $"강화: {target.Definition.DisplayName} Lv{target.Level} 완료" +
                             (stage != null && !string.IsNullOrEmpty(stage.Description) ? $" — {stage.Description}" : "");
            _rewardUsed = true;
            ClearHeld();
            ResumeWithBanner(_upgradeResult);
        }

        /// <summary>확인 창이 떠 있는 동안의 입력. true = 처리함(다른 입력은 막는다).</summary>
        private bool HandleUpgradeConfirmInput()
        {
            if (_confirmTarget == null) return false;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.yKey.wasPressedThisFrame) { ApplyInstallUpgrade(); return true; }
                if (kb.escapeKey.wasPressedThisFrame || kb.nKey.wasPressedThisFrame) { CloseUpgradeConfirm(); return true; }
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.rightButton.wasPressedThisFrame) { CloseUpgradeConfirm(); return true; }
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    Vector2 p = mouse.position.ReadValue();
                    if (Hit(_confirmYes, p)) ApplyInstallUpgrade();
                    else if (Hit(_confirmNo, p)) CloseUpgradeConfirm();
                }
            }
            return true;
        }

        /// <summary>검증용: 확인 창의 [강화] · [취소]를 누른 것과 같다.</summary>
        public void DevAnswerUpgradeConfirm(bool yes) { if (yes) ApplyInstallUpgrade(); else CloseUpgradeConfirm(); }

        /// <summary>검증용: 들고 있는 보상 블록을 coord에 놓으려 한다(마우스 클릭과 같은 길).</summary>
        public void DevTryPlaceHeldAt(GridCoord coord) { _cursor = coord; Confirm(); }

        private void EnsureConfirmPanel()
        {
            if (_confirmPanel != null) return;
            var parent = cardPanel != null ? cardPanel.transform.parent : root != null ? root.transform : null;
            if (parent == null) return;
            var font = titleText != null ? titleText.font : null;
            _confirmPanel = UiRect("Upgrade confirm", parent, new Vector2(0.5f, 0.5f), new Vector2(640, 250), new Color(0.02f, 0.07f, 0.09f, 0.97f));
            var head = UiText(_confirmPanel, font, new Vector2(0, 92), new Vector2(600, 36), 22);
            head.text = "<color=#e0c05a><b>블록 강화</b></color>";
            _confirmText = UiText(_confirmPanel, font, new Vector2(0, 18), new Vector2(600, 110), 21);
            _confirmYes = UiRect("Upgrade confirm yes", _confirmPanel, new Vector2(0.5f, 0.5f), new Vector2(220, 52), new Color(0.12f, 0.5f, 0.25f, 1f));
            _confirmYes.anchoredPosition = new Vector2(-125, -88);
            UiText(_confirmYes, font, Vector2.zero, new Vector2(210, 48), 21).text = "<b>강화</b>  <size=75%>Enter · Y</size>";
            _confirmNo = UiRect("Upgrade confirm no", _confirmPanel, new Vector2(0.5f, 0.5f), new Vector2(220, 52), new Color(0.22f, 0.12f, 0.1f, 1f));
            _confirmNo.anchoredPosition = new Vector2(125, -88);
            UiText(_confirmNo, font, Vector2.zero, new Vector2(210, 48), 21).text = "<b>취소</b>  <size=75%>Esc · N</size>";
            _confirmPanel.gameObject.SetActive(false);
        }

        private string _statusNote;
        private float _statusNoteUntil;

        /// <summary>배치 화면 아래 상태 줄에 잠깐 안내를 띄운다(4초).</summary>
        private void SetStatusNote(string note)
        {
            _statusNote = note;
            _statusNoteUntil = Time.unscaledTime + 4f;
        }

        /// <summary>강화 표시(초록/금색)를 보상 블록을 들고 있을 때도 띄우는가.</summary>
        private bool ShowInstallUpgradeMarkers => _held != null && _heldIsReward && ModuleUpgrades.MaxLevel(_held) > 0 && grid != null && grid.CountOf(_held) > 0;
    }
}

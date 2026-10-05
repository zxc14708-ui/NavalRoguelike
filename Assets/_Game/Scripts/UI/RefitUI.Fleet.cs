using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Game.Modules;
using Game.Modules.Runtime;
using Game.Refit;
using Game.Ship;
using Game.TaskForce;

namespace Game.UI
{
    /// <summary>정비 화면 — 편대 카드: 슬롯 1~4 선택 화면(배치·강화)과 역할 선택 패널.</summary>
    public partial class RefitUI
    {
        // --------------------------------------------------------------- 편대 카드

        private static readonly Color FleetAccent = new(0.4f, 0.9f, 0.95f);

        private void ShowFleetCard(int i, RefitCard card, TMP_Text name, Image icon, TMP_Text stats)
        {
            if (icon != null) { icon.sprite = null; icon.enabled = false; }
            int count = _fleet != null ? _fleet.EscortCount : 0;
            if (card.Kind == RefitCardKind.FleetDeploy)
            {
                if (name != null) name.text = $"<color=#e0c05a>[{i + 1}]</color> 고속정 합류";
                SetCardDecor(i, FleetAccent, $"<color=#66e6f2>편대 카드</color>  ·  편대 배치  ·  {count}/{TaskForceEscortFormation.MaxEscorts}척",
                             "<size=170%><b>PB</b></size>\n<color=#c8d4dc>편대 +1</color>");
                if (stats != null) stats.text = ModuleCardText.BuildFleetDeploy(count + 1);
                return;
            }

            // 편대 강화: 대상은 고른 뒤 편대 슬롯 화면에서 고른다 — 카드에는 슬롯마다 무엇이 되는지 보여 준다
            if (name != null) name.text = $"<color=#e0c05a>[{i + 1}]</color> 편대 강화";
            SetCardDecor(i, FleetAccent, "<color=#66e6f2>편대 강화</color>  ·  슬롯에서 호위함 선택  ·  역할 지정 또는 개량",
                         "<size=170%><b>TF</b></size>\n<color=#c8d4dc>편대 강화</color>");
            if (stats != null) stats.text = ModuleCardText.BuildFleetUpgradeAny(_fleet);
        }

        private void ApplyFleetDeploy(int slot)
        {
            _choosing = false;
            _reward = null;
            _rewardUsed = true;
            int index = _fleet != null ? _fleet.Deploy(slot) : -1;
            _fleetResult = index >= 0
                ? $"편대 합류: {_fleet.GetInfo(index).Name} → {slot + 1}번 슬롯 ({_fleet.EscortCount}/{TaskForceEscortFormation.MaxEscorts}척) — 편대 강화 카드로 역할을 고를 수 있습니다"
                : "그 슬롯에 배치하지 못했습니다.";
            ShowFleetResult();
        }

        private void ApplyFleetUpgrade(int index)
        {
            _rewardUsed = true;
            bool ok = _fleet != null && _fleet.Upgrade(index);
            var info = _fleet != null ? _fleet.GetInfo(index) : default;
            _fleetResult = ok
                ? $"편대 강화: {info.Name} 개량 {info.Tier} — {TaskForceEscortFormation.TierDescription(info.Role, info.Tier)}"
                : "편대 강화를 적용하지 못했습니다.";
            ShowFleetResult();
        }

        /// <summary>역할 없는 고속정의 역할을 고른다: 1~4 키 또는 클릭, 우클릭·Esc = 카드로 돌아가기.</summary>
        private void OpenRolePicker(int index)
        {
            _rolePickIndex = index;
            _rolePicking = true;
            EnsureRolePanel();
            if (_rolePanel != null) { _rolePanel.gameObject.SetActive(true); _rolePanel.SetAsLastSibling(); }   // 편대 슬롯 화면 위에
            Refresh();
        }

        private void ChooseRole(int option)
        {
            if (!_rolePicking || option < 0 || option >= TaskForceEscortFormation.Roles.Length) return;
            var role = TaskForceEscortFormation.Roles[option];
            string before = _fleet != null ? _fleet.GetInfo(_rolePickIndex).Name : "";
            bool ok = _fleet != null && _fleet.AssignRole(_rolePickIndex, role);
            _rolePicking = false;
            if (_rolePanel != null) _rolePanel.gameObject.SetActive(false);
            _rewardUsed = true;
            _fleetResult = ok
                ? $"편대 강화: {before} → {_fleet.GetInfo(_rolePickIndex).Name} — {TaskForceEscortFormation.RoleSummary(role)}"
                : "역할을 지정하지 못했습니다.";
            ShowFleetResult();
        }

        private void HandleRoleInput()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) { ChooseRole(0); return; }
                if (kb.digit2Key.wasPressedThisFrame) { ChooseRole(1); return; }
                if (kb.digit3Key.wasPressedThisFrame) { ChooseRole(2); return; }
                if (kb.digit4Key.wasPressedThisFrame) { ChooseRole(3); return; }
                if (kb.escapeKey.wasPressedThisFrame) { BackToCards(); return; }
            }
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (mouse.rightButton.wasPressedThisFrame) { BackToCards(); return; }
            if (!mouse.leftButton.wasPressedThisFrame) return;
            Vector2 p = mouse.position.ReadValue();
            for (int i = 0; i < _roleOptions.Count; i++)
                if (Hit(_roleOptions[i], p)) { ChooseRole(i); return; }
        }

        /// <summary>역할 선택 패널(코드로 한 번만 만든다). 카드 패널과 같은 캔버스, 화면 가운데.</summary>
        private void EnsureRolePanel()
        {
            if (_rolePanel != null) return;
            var parent = cardPanel != null ? cardPanel.transform.parent : root != null ? root.transform : null;
            if (parent == null) return;
            var font = titleText != null ? titleText.font : null;

            _rolePanel = UiRect("Escort role choice", parent, new Vector2(0.5f, 0.5f), new Vector2(1180, 300), new Color(0.02f, 0.07f, 0.09f, 0.96f));
            UiText(_rolePanel, font, new Vector2(0, 112), new Vector2(1100, 44), 26).text = "<b>호위함 역할 지정</b>  —  1 / 2 / 3 / 4 키 또는 클릭";
            var roles = TaskForceEscortFormation.Roles;
            for (int i = 0; i < roles.Length; i++)
            {
                var rc = TaskForceEscortFormation.RoleColor(roles[i]);
                var opt = UiRect($"Role {TaskForceEscortFormation.RoleCode(roles[i])}", _rolePanel, new Vector2(0.5f, 0.5f), new Vector2(270, 170),
                                 new Color(rc.r * 0.22f, rc.g * 0.22f, rc.b * 0.22f, 1f));
                opt.anchoredPosition = new Vector2(-435 + i * 290, -10);
                var t = UiText(opt, font, Vector2.zero, new Vector2(250, 160), 21);
                t.text = $"<color=#e0c05a>[{i + 1}]</color>\n<size=140%><b><color=#{ColorUtility.ToHtmlStringRGB(rc)}>{TaskForceEscortFormation.RoleCode(roles[i])}</color></b></size>\n" +
                         $"<b>{TaskForceEscortFormation.RoleName(roles[i])}</b>\n<size=80%><color=#c8d4dc>{TaskForceEscortFormation.RoleSummary(roles[i])}</color></size>";
                _roleOptions.Add(opt);
            }
            UiText(_rolePanel, font, new Vector2(0, -125), new Vector2(900, 30), 17).text = "<color=#8fa4b8>우클릭 / Esc — 카드로 돌아가기</color>";
            _rolePanel.gameObject.SetActive(false);
        }

        // --------------------------------------------------------------- 편대 슬롯 화면(2026-10-03)
        // 편대 카드(배치·강화)를 고르면 함선 블록 배치 화면 대신 이 화면이 뜬다. 슬롯 1~4 중
        // 배치 카드 = 고속정을 넣을 빈 슬롯, 강화 카드 = 강화할 호위함을 고른다. 적용 뒤 결과를 보여 주고
        // 전투 재개(Space·Enter) 또는 함선 배치 화면(Tab, 블록 재배치)으로 넘어간다.

        private bool _fleetPicking;      // 슬롯 고르는 중
        private bool _fleetDeployMode;   // true = 편대 배치(빈 슬롯), false = 편대 강화(호위함)
        private bool _fleetDone;         // 적용 끝 — 결과 화면
        private int _fleetHover = -1;
        private RectTransform _fleetPanel, _fleetResume, _fleetToShip;
        private TMP_Text _fleetTitle, _fleetDetail, _fleetFooter;
        private readonly List<RectTransform> _fleetSlotRects = new();
        private readonly List<Image> _fleetSlotBgs = new();
        private readonly List<TMP_Text> _fleetSlotTexts = new();

        /// <summary>편대 슬롯 화면을 연다. preselectIndex = 강화 카드에서 처음 가리킬 호위함 번호(-1 = 고를 수 있는 첫 슬롯).</summary>
        private void OpenFleetPanel(bool deploy, int preselectIndex)
        {
            _fleetDeployMode = deploy;
            _fleetPicking = true;
            _fleetDone = false;
            EnsureFleetPanel();
            _fleetHover = -1;
            if (_fleet != null)
                _fleetHover = deploy ? _fleet.FirstEmptyRosterSlot()
                            : preselectIndex >= 0 ? _fleet.GetInfo(preselectIndex).RosterSlot : -1;
            if (!FleetSlotSelectable(_fleetHover)) _fleetHover = FirstSelectableSlot();
            if (_fleetPanel != null) { _fleetPanel.gameObject.SetActive(true); _fleetPanel.SetAsLastSibling(); }
            RefreshFleetPanel();
        }

        private void CloseFleetPanel()
        {
            _fleetPicking = false;
            _fleetDone = false;
            if (_fleetPanel != null) _fleetPanel.gameObject.SetActive(false);
        }

        /// <summary>지금 고를 수 있는 슬롯인가: 배치 = 빈 슬롯, 강화 = 역할이 없거나 최대 개량이 아닌 호위함.</summary>
        private bool FleetSlotSelectable(int slot)
        {
            if (_fleet == null || slot < 0 || slot >= TaskForceEscortFormation.MaxEscorts) return false;
            int index = _fleet.IndexOfRosterSlot(slot);
            return _fleetDeployMode ? index < 0 && _fleet.CanDeploy : index >= 0 && _fleet.CanUpgrade(index);
        }

        private int FirstSelectableSlot()
        {
            for (int s = 0; s < TaskForceEscortFormation.MaxEscorts; s++) if (FleetSlotSelectable(s)) return s;
            return -1;
        }

        /// <summary>슬롯(0~3)을 고른다 — 1~4 키·클릭·Enter. 역할 없는 고속정을 강화하면 역할 선택 창이 이어서 뜬다.</summary>
        private void ChooseFleetSlot(int slot)
        {
            if (!_fleetPicking || !FleetSlotSelectable(slot)) return;
            _fleetHover = slot;
            if (_fleetDeployMode) { ApplyFleetDeploy(slot); return; }

            int index = _fleet.IndexOfRosterSlot(slot);
            if (_fleet.GetInfo(index).Role == EscortRole.None)
            {
                _fleetPicking = false;   // 슬롯 화면은 뒤에 그대로 두고 역할 선택 창을 위에 띄운다
                RefreshFleetPanel();
                OpenRolePicker(index);
                return;
            }
            ApplyFleetUpgrade(index);
        }

        /// <summary>적용 끝: 바뀐 슬롯과 결과를 보여 주고 전투 재개 / 함선 배치 화면을 기다린다.</summary>
        private void ShowFleetResult()
        {
            _fleetPicking = false;
            _fleetDone = true;
            EnsureFleetPanel();
            if (_fleetPanel != null) { _fleetPanel.gameObject.SetActive(true); _fleetPanel.SetAsLastSibling(); }
            RefreshFleetPanel();
        }

        private void HandleFleetInput()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            Vector2 p = mouse != null ? mouse.position.ReadValue() : default;
            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;

            if (_fleetDone)
            {
                if ((kb != null && kb.tabKey.wasPressedThisFrame) || (click && Hit(_fleetToShip, p)))
                {
                    CloseFleetPanel();   // 함선 배치 화면(블록 재배치 · Space로 재개)
                    Refresh();
                    return;
                }
                if ((kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) ||
                    (click && Hit(_fleetResume, p)))
                {
                    CloseFleetPanel();
                    TryLaunch();
                }
                return;
            }

            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) { ChooseFleetSlot(0); return; }
                if (kb.digit2Key.wasPressedThisFrame) { ChooseFleetSlot(1); return; }
                if (kb.digit3Key.wasPressedThisFrame) { ChooseFleetSlot(2); return; }
                if (kb.digit4Key.wasPressedThisFrame) { ChooseFleetSlot(3); return; }
                if (kb.escapeKey.wasPressedThisFrame) { BackToCards(); return; }
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) { ChooseFleetSlot(_fleetHover); return; }
                int dir = (kb.rightArrowKey.wasPressedThisFrame ? 1 : 0) - (kb.leftArrowKey.wasPressedThisFrame ? 1 : 0);
                if (dir != 0)
                {
                    int max = TaskForceEscortFormation.MaxEscorts;
                    int from = _fleetHover < 0 ? (dir > 0 ? -1 : max) : _fleetHover;
                    for (int k = 1; k <= max; k++)
                    {
                        int s = ((from + dir * k) % max + max) % max;
                        if (FleetSlotSelectable(s)) { _fleetHover = s; break; }
                    }
                    RefreshFleetPanel();
                }
            }

            if (mouse == null) return;
            if (mouse.rightButton.wasPressedThisFrame) { BackToCards(); return; }
            for (int s = 0; s < _fleetSlotRects.Count; s++)
            {
                if (!Hit(_fleetSlotRects[s], p)) continue;
                if (FleetSlotSelectable(s) && _fleetHover != s) { _fleetHover = s; RefreshFleetPanel(); }
                if (click) ChooseFleetSlot(s);
                return;
            }
        }

        /// <summary>편대 슬롯 화면(코드로 한 번만 만든다). 카드 화면처럼 화면 전체를 불투명하게 덮는다.</summary>
        private void EnsureFleetPanel()
        {
            if (_fleetPanel != null) return;
            var parent = cardPanel != null ? cardPanel.transform.parent : root != null ? root.transform : null;
            if (parent == null) return;
            var font = titleText != null ? titleText.font : null;

            _fleetPanel = UiRect("Fleet slots", parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Color(0.025f, 0.04f, 0.055f, 1f));
            _fleetPanel.anchorMin = Vector2.zero;
            _fleetPanel.anchorMax = Vector2.one;
            _fleetPanel.offsetMin = _fleetPanel.offsetMax = Vector2.zero;

            _fleetTitle = UiText(_fleetPanel, font, new Vector2(0, 330), new Vector2(1600, 56), 34);
            _fleetTitle.fontStyle = FontStyles.Bold;
            for (int s = 0; s < TaskForceEscortFormation.MaxEscorts; s++)
            {
                var slot = UiRect($"Fleet slot {s + 1}", _fleetPanel, new Vector2(0.5f, 0.5f), new Vector2(350, 420), FrameColor);
                slot.anchoredPosition = new Vector2(-570 + s * 380, 40);
                var text = UiText(slot, font, Vector2.zero, new Vector2(320, 390), 22);
                text.alignment = TextAlignmentOptions.Top;
                text.textWrappingMode = TextWrappingModes.Normal;
                text.lineSpacing = 6f;
                _fleetSlotRects.Add(slot);
                _fleetSlotBgs.Add(slot.GetComponent<Image>());
                _fleetSlotTexts.Add(text);
            }
            _fleetDetail = UiText(_fleetPanel, font, new Vector2(0, -225), new Vector2(1560, 70), 22);
            _fleetDetail.textWrappingMode = TextWrappingModes.Normal;
            _fleetFooter = UiText(_fleetPanel, font, new Vector2(0, -285), new Vector2(1560, 34), 19);
            _fleetResume = FleetButton("Resume", font, new Vector2(-200, -360), "<b>전투 재개</b>  <color=#8fa4b8>[Space]</color>");
            _fleetToShip = FleetButton("To ship", font, new Vector2(200, -360), "<b>함선 배치 화면</b>  <color=#8fa4b8>[Tab]</color>");
            _fleetPanel.gameObject.SetActive(false);
        }

        private RectTransform FleetButton(string name, TMP_FontAsset font, Vector2 position, string label)
        {
            var button = UiRect(name, _fleetPanel, new Vector2(0.5f, 0.5f), new Vector2(360, 58), new Color(0.09f, 0.22f, 0.26f, 1f));
            button.anchoredPosition = position;
            UiText(button, font, Vector2.zero, new Vector2(340, 52), 22).text = label;
            return button;
        }

        private void RefreshFleetPanel()
        {
            if (_fleetPanel == null) return;
            int max = TaskForceEscortFormation.MaxEscorts;
            int count = _fleet != null ? _fleet.EscortCount : 0;
            string formation = _fleet != null ? FleetFormations.Name(_fleet.Formation) : "";
            _fleetTitle.text = _fleetDone ? $"편대  <size=75%><color=#8fa4b8>{count}/{max}척 · {formation}</color></size>"
                : _fleetDeployMode ? "편대 배치 — 고속정을 넣을 슬롯을 고르세요"
                : "편대 강화 — 강화할 호위함을 고르세요";

            for (int s = 0; s < max; s++)
            {
                int index = _fleet != null ? _fleet.IndexOfRosterSlot(s) : -1;
                var info = index >= 0 ? _fleet.GetInfo(index) : default;
                bool selectable = _fleetPicking && FleetSlotSelectable(s);
                bool lit = s == _fleetHover && (selectable || _fleetDone);
                Color rc = index >= 0 ? TaskForceEscortFormation.RoleColor(info.Role) : FleetAccent;
                float k = lit ? 0.42f : selectable ? 0.2f : 0.09f;
                _fleetSlotBgs[s].color = new Color(rc.r * k, rc.g * k, rc.b * k, 1f);
                _fleetSlotTexts[s].alpha = !_fleetPicking || selectable ? 1f : 0.45f;
                _fleetSlotTexts[s].text = FleetSlotText(s, index, info);
            }

            _fleetDetail.text = _fleetDone ? $"<color=#68dede>{_fleetResult}</color>" : FleetHoverDetail();
            _fleetFooter.text = _fleetDone
                ? "<color=#8fa4b8>Space / Enter — 전투 재개    Tab — 함선 배치 화면(블록 재배치)</color>"
                : "<color=#8fa4b8>1 ~ 4 키 · 클릭 · ←→ + Enter 로 선택    우클릭 / Esc — 카드로 돌아가기</color>";
            _fleetResume.gameObject.SetActive(_fleetDone);
            _fleetToShip.gameObject.SetActive(_fleetDone);
        }

        /// <summary>그 슬롯의 진형 자리: "함대원형진 앞 · 단종진 뒤 1번째 · 자율 스스로 판단".</summary>
        private static string SlotPlaces(int slot)
        {
            var sb = new StringBuilder();
            foreach (var f in FleetFormations.All)
            {
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append($"{FleetFormations.Name(f)} {FleetFormations.SlotLabel(f, slot)}");
            }
            return sb.ToString();
        }

        private string FleetSlotText(int slot, int index, TaskForceEscortFormation.EscortInfo info)
        {
            _sb.Clear();
            _sb.Append($"<color=#e0c05a>[{slot + 1}]</color>  <b>{slot + 1}번 슬롯</b>\n");
            if (_fleet != null)
                _sb.Append($"<size=75%><color=#9fe0a0>{FleetFormations.Name(_fleet.Formation)} · {FleetFormations.SlotLabel(_fleet.Formation, slot)}</color></size>\n");
            if (index < 0)
            {
                _sb.Append("\n\n\n<size=125%><color=#5a6570>비어 있음</color></size>");
                if (_fleetPicking && _fleetDeployMode) _sb.Append("\n\n<color=#7fd08a>여기에 고속정 합류</color>");
                return _sb.ToString();
            }
            string hex = ColorUtility.ToHtmlStringRGB(TaskForceEscortFormation.RoleColor(info.Role));
            _sb.Append($"\n<size=200%><b><color=#{hex}>{TaskForceEscortFormation.RoleCode(info.Role)}</color></b></size>\n");
            _sb.Append($"<b>{info.Name}</b>\n");
            _sb.Append(info.Role == EscortRole.None ? "<color=#8fa4b8>역할 없음</color>\n" : $"개량 {info.Tier} / {TaskForceEscortFormation.MaxUpgradeLevel}\n");
            _sb.Append($"<size=85%><color=#8fa4b8>선체 {TaskForceEscortFormation.MaxHullFor(info.Tier):0} · {TaskForceEscortFormation.RoleShort(info.Role)}</color></size>");
            if (_fleetPicking && !_fleetDeployMode)
                _sb.Append(info.Role == EscortRole.None ? "\n\n<color=#7fd08a>역할 지정</color>"
                           : info.Tier >= TaskForceEscortFormation.MaxUpgradeLevel ? "\n\n<color=#e0c05a>최대 개량</color>"
                           : $"\n\n<color=#7fd08a>개량 {info.Tier} → {info.Tier + 1}</color>");
            else if (_fleetPicking) _sb.Append("\n\n<color=#5a6570>사용 중</color>");
            return _sb.ToString();
        }

        private string FleetHoverDetail()
        {
            if (_fleet == null || _fleetHover < 0)
                return _fleetDeployMode ? "빈 슬롯이 없습니다." : "강화할 수 있는 호위함이 없습니다.";
            if (_fleetDeployMode)
            {
                return $"<b>{_fleetHover + 1}번 슬롯</b>에 고속정 합류 — 진형 자리: {SlotPlaces(_fleetHover)}" +
                       "  <color=#8fa4b8>(슬롯마다 진형 자리가 정해져 있다)</color>";
            }
            var info = _fleet.GetInfo(_fleet.IndexOfRosterSlot(_fleetHover));
            if (info.Role == EscortRole.None)
                return $"<b>{info.Name}</b> — 역할 지정: 방공 · 대잠 · 전자전 · 미사일 중 하나(고르면 역할 선택 창)";
            int to = info.Tier + 1;
            return $"<b>{info.Name}</b> 개량 {info.Tier} → {to}: {TaskForceEscortFormation.TierDescription(info.Role, to)}" +
                   $"  <color=#8fa4b8>· 선체 {TaskForceEscortFormation.MaxHullFor(info.Tier):0} → {TaskForceEscortFormation.MaxHullFor(to):0}</color>";
        }
    }
}

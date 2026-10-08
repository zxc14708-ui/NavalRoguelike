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
    /// <summary>정비 화면 — 보상 카드 3장 화면(카드 배치·꾸밈·선택, 성장 카드 적용).</summary>
    public partial class RefitUI
    {
        private static readonly Color CardColor = new(0.085f, 0.115f, 0.15f, 1f);
        private static readonly Color FrameColor = new(0.05f, 0.075f, 0.1f, 1f);

        private void ShowCards()
        {
            if (cardPanel != null) cardPanel.SetActive(_choosing);
            PrepareCardPanel();
            if (cardRects == null) return;

            for (int i = 0; i < cardRects.Length; i++)
            {
                bool used = i < _cards.Count;
                if (cardRects[i] != null) cardRects[i].gameObject.SetActive(used);
                if (!used) continue;

                LayoutCard(i);
                var name = cardNames != null && i < cardNames.Length ? cardNames[i] : null;
                var icon = cardIcons != null && i < cardIcons.Length ? cardIcons[i] : null;
                var stats = cardStats != null && i < cardStats.Length ? cardStats[i] : null;

                var card = _cards[i];
                var def = card.Module;

                if (card.Kind is RefitCardKind.FleetDeploy or RefitCardKind.FleetUpgrade)
                {
                    ShowFleetCard(i, card, name, icon, stats);
                    continue;
                }

                if (card.Kind == RefitCardKind.Growth)
                {
                    var gdef = RunUpgrades.Definition(card.Stat);
                    var tc = RunUpgrades.TierColor(card.Tier);
                    string hex = ColorUtility.ToHtmlStringRGB(tc);
                    if (name != null)
                        name.text = $"<color=#e0c05a>[{i + 1}]</color> {gdef.Name}  <color=#{hex}><size=80%>{RunUpgrades.TierName(card.Tier)}</size></color>";
                    SetCardDecor(i, tc, ModuleCardText.BuildGrowthTag(gdef, card.Tier, card.BossReward), ModuleCardText.BuildGrowthIconLabel(gdef, card.Tier));
                    if (icon != null) { icon.sprite = null; icon.enabled = false; }
                    if (stats != null) stats.text = ModuleCardText.BuildGrowthStats(gdef, card.Tier);
                    continue;
                }

                if (card.Kind == RefitCardKind.WeaponUpgrade)
                {
                    if (def == null) { ShowEquipmentUpgradeCard(i, name, icon, stats); continue; }
                    int from = LowestUpgradeLevel(def);
                    var stage = ModuleUpgrades.ProfileFor(def)?.GetStage(from + 1);
                    if (name != null)
                        name.text = $"<color=#e0c05a>[{i + 1}]</color> {def.DisplayName} 강화 {ModuleUpgrades.Roman(from + 1)}";
                    SetCardDecor(i, UpgradeAccent, ModuleCardText.BuildTag(def), null);
                    if (icon != null)
                    {
                        // 강화 후 외형(아이콘이 있으면)을 보여 준다
                        var sprite = stage != null && stage.Icon != null ? stage.Icon : def.Icon;
                        icon.sprite = sprite;
                        icon.preserveAspect = true;
                        icon.color = Color.white;
                        icon.enabled = sprite != null;
                    }
                    if (stats != null) stats.text = ModuleCardText.BuildUpgradeChoice(def, from);
                    continue;
                }

                // 설치 카드
                if (name != null) name.text = $"<color=#e0c05a>[{i + 1}]</color> {def.DisplayName}";
                SetCardDecor(i, CategoryAccent(def.Type), ModuleCardText.BuildTag(def), null);
                if (icon != null)
                {
                    icon.sprite = def.Icon;
                    icon.preserveAspect = true;
                    icon.color = Color.white;
                    icon.enabled = def.Icon != null;
                }
                if (stats != null) stats.text = ModuleCardText.BuildStats(def);
            }
        }

        /// <summary>카드 화면은 불투명하게 덮고(뒤의 정비 문구가 비치지 않게) 제목에 레벨을 넣는다.</summary>
        private void PrepareCardPanel()
        {
            if (cardPanel == null) return;
            var bg = cardPanel.GetComponent<Image>();
            if (bg != null) bg.color = new Color(0.025f, 0.04f, 0.055f, 1f);
            var title = cardPanel.transform.Find("CardTitle")?.GetComponent<TMP_Text>();
            if (title != null)
            {
                title.text = _bossReward ? "보스 격침 — 보상 선택" : _openLevel > 0 ? $"레벨 {_openLevel} — 보상 선택" : "보상 선택";
                title.rectTransform.anchoredPosition = new Vector2(0f, -56f);
            }
        }

        private int _openLevel;

        private void SetCardDecor(int i, Color accent, string tag, string iconLabel)
        {
            if (_cardAccents != null && _cardAccents[i] != null) _cardAccents[i].color = accent;
            if (_cardTags != null && _cardTags[i] != null) _cardTags[i].text = tag;
            if (_iconFrames != null && _iconFrames[i] != null)
                _iconFrames[i].color = iconLabel != null ? new Color(accent.r * 0.25f, accent.g * 0.25f, accent.b * 0.25f, 1f) : FrameColor;
            if (_iconLabels != null && _iconLabels[i] != null)
            {
                _iconLabels[i].gameObject.SetActive(iconLabel != null);
                _iconLabels[i].text = iconLabel ?? "";
                _iconLabels[i].color = accent;
            }
        }

        private static Color CategoryAccent(ModuleType t) => t switch
        {
            ModuleType.Autocannon => new Color(0.88f, 0.58f, 0.42f),
            ModuleType.NavalGun or ModuleType.GuidedRocket => new Color(0.88f, 0.75f, 0.35f),
            ModuleType.Vls => new Color(0.5f, 0.72f, 0.88f),
            ModuleType.Ciws or ModuleType.SamLauncher => new Color(0.56f, 0.82f, 1f),
            ModuleType.AswLauncher or ModuleType.TorpedoTube or ModuleType.Sonar or ModuleType.HelicopterDeck => new Color(0.41f, 0.72f, 1f),
            ModuleType.DecoyLauncher or ModuleType.EwSuite => new Color(0.82f, 0.6f, 1f),
            ModuleType.RepairBay => new Color(0.5f, 0.82f, 0.54f),
            ModuleType.FleetRelay => new Color(0.38f, 0.79f, 0.86f),
            ModuleType.TurboIntake => new Color(0.88f, 0.58f, 0.42f),
            ModuleType.FireControlArray => new Color(0.56f, 0.82f, 1f),
            ModuleType.MissileLogistics => new Color(0.5f, 0.72f, 0.88f),
            _ => new Color(0.78f, 0.83f, 0.86f),
        };

        /// <summary>통합 장비 강화 카드(2026-10-03): 강화할 수 있는 장비 목록. 대상은 고른 뒤 함선에서 고른다.</summary>
        private void ShowEquipmentUpgradeCard(int i, TMP_Text name, Image icon, TMP_Text stats)
        {
            int count = 0;
            foreach (var m in grid.Modules) if (m != null && m.CanUpgrade) count++;
            if (name != null) name.text = $"<color=#e0c05a>[{i + 1}]</color> 장비 강화";
            SetCardDecor(i, UpgradeAccent, $"<color=#7fd08a>장비 강화</color>  ·  설치된 장비 하나를 한 단계  ·  대상 {count}개",
                         "<size=170%><b>UP</b></size>\n<color=#c8d4dc>장비 +1단계</color>");
            if (icon != null) { icon.sprite = null; icon.enabled = false; }
            if (stats != null) stats.text = ModuleCardText.BuildEquipmentUpgradeChoice(grid);
        }

        private static readonly Color UpgradeAccent = new(0.35f, 0.9f, 0.45f);

        /// <summary>이 카드로 강화할 수 있는 블록 중 가장 낮은 단계(카드에 "다음 단계"로 보여 준다).</summary>
        private int LowestUpgradeLevel(ModuleDefinition def)
        {
            int lowest = int.MaxValue;
            foreach (var m in grid.Modules)
                if (ModuleUpgrades.CanUpgradeWith(m, def)) lowest = Mathf.Min(lowest, m.UpgradeLevel);
            return lowest == int.MaxValue ? 0 : lowest;
        }

        /// <summary>
        /// 카드 한 장의 구역(위→아래): 색 띠 · 이름 · 분류 줄 · 그림(틀 안) · 표(항목 | 값).
        /// 글자는 모두 왼쪽 정렬, 값은 표의 같은 지점(44%)에 맞춘다.
        /// </summary>
        private void LayoutCard(int index)
        {
            var rect = cardRects[index];
            if (rect == null) return;
            rect.anchorMin = new Vector2(0.07f + index * 0.30f, 0.14f);
            rect.anchorMax = new Vector2(0.33f + index * 0.30f, 0.86f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            if (rect.GetComponent<RectMask2D>() == null) rect.gameObject.AddComponent<RectMask2D>();
            var cardImage = rect.GetComponent<Image>();
            if (cardImage != null) cardImage.color = CardColor;
            EnsureCardDecor(index, rect);

            if (cardNames != null && index < cardNames.Length && cardNames[index] != null)
            {
                var n = cardNames[index];
                FitCardRegion(n.rectTransform, new Vector2(0, 0.905f), new Vector2(1, 0.985f), 22, 4);
                n.enableAutoSizing = true;
                n.fontSizeMin = 20;
                n.fontSizeMax = 30;
                n.fontStyle = FontStyles.Bold;
                n.alignment = TextAlignmentOptions.MidlineLeft;
                n.overflowMode = TextOverflowModes.Ellipsis;
                n.textWrappingMode = TextWrappingModes.NoWrap;
            }
            if (_cardTags[index] != null) FitCardRegion(_cardTags[index].rectTransform, new Vector2(0, 0.855f), new Vector2(1, 0.905f), 22, 2);
            if (_iconFrames[index] != null) FitCardRegion(_iconFrames[index].rectTransform, new Vector2(0, 0.53f), new Vector2(1, 0.85f), 22, 6);
            if (cardIcons != null && index < cardIcons.Length && cardIcons[index] != null)
            {
                FitCardRegion(cardIcons[index].rectTransform, new Vector2(0, 0.53f), new Vector2(1, 0.85f), 30, 12);
                cardIcons[index].transform.SetAsLastSibling();
            }
            if (_iconLabels[index] != null)
            {
                FitCardRegion(_iconLabels[index].rectTransform, new Vector2(0, 0.53f), new Vector2(1, 0.85f), 30, 12);
                _iconLabels[index].transform.SetAsLastSibling();
            }
            if (cardStats != null && index < cardStats.Length && cardStats[index] != null)
            {
                var s = cardStats[index];
                FitCardRegion(s.rectTransform, new Vector2(0, 0.02f), new Vector2(1, 0.51f), 24, 8);
                s.enableAutoSizing = true;
                s.fontSizeMin = 16;
                s.fontSizeMax = 24;
                s.lineSpacing = 22f;
                s.alignment = TextAlignmentOptions.TopLeft;
                s.overflowMode = TextOverflowModes.Ellipsis;
                s.textWrappingMode = TextWrappingModes.Normal;
                s.color = new Color(0.9f, 0.94f, 0.96f);
            }
        }

        private void EnsureCardDecor(int index, RectTransform card)
        {
            int n = cardRects.Length;
            _cardAccents ??= new Image[n];
            _iconFrames ??= new Image[n];
            _cardTags ??= new TMP_Text[n];
            _iconLabels ??= new TMP_Text[n];
            if (_cardAccents[index] != null) return;

            var font = cardNames != null && index < cardNames.Length && cardNames[index] != null ? cardNames[index].font : null;
            _cardAccents[index] = UiRect("Accent", card, Vector2.zero, Vector2.zero, Color.white).GetComponent<Image>();
            var ar = _cardAccents[index].rectTransform;
            ar.anchorMin = new Vector2(0, 1); ar.anchorMax = Vector2.one; ar.pivot = new Vector2(0.5f, 1f);
            ar.sizeDelta = new Vector2(0, 6); ar.anchoredPosition = Vector2.zero;

            _iconFrames[index] = UiRect("Icon frame", card, Vector2.zero, Vector2.zero, FrameColor).GetComponent<Image>();

            var tag = UiText((RectTransform)card, font, Vector2.zero, Vector2.zero, 17);
            tag.name = "Tag";
            tag.alignment = TextAlignmentOptions.MidlineLeft;
            tag.color = new Color(0.72f, 0.8f, 0.86f);
            tag.enableAutoSizing = true;
            tag.fontSizeMin = 13; tag.fontSizeMax = 17;
            tag.textWrappingMode = TextWrappingModes.NoWrap;
            tag.overflowMode = TextOverflowModes.Ellipsis;
            _cardTags[index] = tag;

            var label = UiText((RectTransform)card, font, Vector2.zero, Vector2.zero, 34);
            label.name = "Icon label";
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            _iconLabels[index] = label;
            label.gameObject.SetActive(false);
        }

        private static void FitCardRegion(RectTransform rect, Vector2 min, Vector2 max, float padX = 18, float padY = 12)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(padX, padY);
            rect.offsetMax = new Vector2(-padX, -padY);
        }

        private void ChooseCard(int index)
        {
            if (!_choosing || index < 0 || index >= _cards.Count) return;

            var card = _cards[index];
            _upgradeResult = null;
            _growthResult = null;
            _fleetResult = null;
            if (cardPanel != null) cardPanel.SetActive(false);

            switch (card.Kind)
            {
                case RefitCardKind.Growth:
                    ApplyGrowth(card);
                    return;

                case RefitCardKind.WeaponUpgrade:
                    _reward = card.Module;
                    _rewardUsed = false;
                    _choosing = false;
                    BeginUpgradePick();
                    return;

                case RefitCardKind.FleetDeploy:   // 편대 슬롯 화면에서 빈 슬롯을 고른다
                    _choosing = false;
                    _reward = null;
                    OpenFleetPanel(true, -1);
                    return;

                case RefitCardKind.FleetUpgrade:  // 편대 슬롯 화면에서 강화할 호위함을 고른다
                    _choosing = false;
                    _reward = null;
                    OpenFleetPanel(false, card.EscortIndex);
                    return;

                default:   // 설치
                    _reward = card.Module;
                    _rewardUsed = false;
                    _choosing = false;
                    TakeReward();
                    return;
            }
        }

        /// <summary>성장 카드: 즉시 적용하고(함선 전체 능력) 배치 화면으로 넘어간다.</summary>
        private void ApplyGrowth(RefitCard card)
        {
            var def = RunUpgrades.Definition(card.Stat);
            float add = def != null ? def.ValueOf(card.Tier) : 0f;
            RunUpgrades.Add(card.Stat, card.Tier);
            _choosing = false;
            _reward = null;
            _rewardUsed = true;
            _growthResult = def != null
                ? $"성장 완료: {def.Name} +{add * 100f:0.#}% ({RunUpgrades.TierName(card.Tier)}) — 누적 {RunUpgrades.Describe(card.Stat, RunUpgrades.Get(card.Stat))}"
                : "성장 카드를 적용하지 못했습니다.";
            Refresh();
        }

        private void HandleCardInput()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) ChooseCard(0);
                if (kb.digit2Key.wasPressedThisFrame) ChooseCard(1);
                if (kb.digit3Key.wasPressedThisFrame) ChooseCard(2);
            }

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || cardRects == null) return;

            Vector2 screenPos = mouse.position.ReadValue();
            for (int i = 0; i < cardRects.Length; i++)
            {
                if (cardRects[i] == null || !cardRects[i].gameObject.activeSelf) continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(cardRects[i], screenPos, null)) continue;

                ChooseCard(i);
                return;
            }
        }

        private static string RewardLabel(ModuleDefinition def, bool used)
        {
            if (def == null) return "이번 보상: —";
            if (used) return $"<color=#5a6570>이번 보상: {def.DisplayName} (배치 완료)</color>";

            return $"이번 보상 [1] <b>{def.DisplayName}</b>  {def.Width}x{def.Height}";
        }
    }
}

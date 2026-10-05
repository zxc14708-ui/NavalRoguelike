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
    /// <summary>정비 화면 — 장비 업그레이드 카드: 함선 위에서 올릴 장비 고르기와 표시.</summary>
    public partial class RefitUI
    {
        // --------------------------------------------------------------- 장비 강화

        private static string UpgradeStepLabel(int from) => from <= 0 ? "기본 → 강화 I" : $"강화 {ModuleUpgrades.Roman(from)} → 강화 {ModuleUpgrades.Roman(from + 1)}";

        /// <summary>
        /// 강화할 블록을 고르게 한다. 대상이 하나뿐이면 고르지 않고 바로 적용한다.
        /// 통합 장비 강화 카드(_reward = null)는 강화 단계가 남은 모든 블록, 블록별 카드는 그 블록만.
        /// </summary>
        private void BeginUpgradePick()
        {
            _upgradePicking = true;
            ModuleInstance only = null;
            int count = 0;
            foreach (var m in grid.Modules)
            {
                if (!ModuleUpgrades.CanUpgradeWith(m, _reward)) continue;
                if (count == 0) { only = m; _cursor = m.Origin; }
                count++;
            }
            if (count == 0) { BackToCards(); return; }
            if (count == 1) TryUpgradeAt(only.Origin);
        }

        /// <summary>강화 선택을 그만두고 카드 선택 화면으로 돌아간다(우클릭·Esc·취소).</summary>
        private void BackToCards()
        {
            _upgradePicking = false;
            _rolePicking = false;
            if (_rolePanel != null) _rolePanel.gameObject.SetActive(false);
            CloseFleetPanel();
            _reward = null;
            _rewardUsed = true;
            _choosing = _cards.Count > 0;
            ShowCards();
        }

        /// <summary>커서 아래 블록을 한 단계 강화한다. 같은 블록이 아니거나 최대 단계면 아무것도 하지 않는다.</summary>
        private bool TryUpgradeAt(GridCoord coord)
        {
            if (!_upgradePicking) return false;
            var target = grid.Get(coord);
            if (!ModuleUpgrades.CanUpgradeWith(target, _reward) || !target.ApplyUpgrade()) return false;

            var stage = ModuleUpgrades.ProfileFor(target.Definition)?.GetStage(target.UpgradeLevel);
            _upgradeResult = $"{target.Definition.DisplayName} ({target.Origin.X},{target.Origin.Z}) 강화 {ModuleUpgrades.Roman(target.UpgradeLevel)} 완료" +
                             (stage != null && !string.IsNullOrEmpty(stage.Description) ? $" — {stage.Description}" : "");
            _upgradePicking = false;
            _rewardUsed = true;
            return true;
        }

        private void HandleUpgradePickInput()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                int dx = 0, dz = 0;
                if (kb.rightArrowKey.wasPressedThisFrame) dx += 1;
                if (kb.leftArrowKey.wasPressedThisFrame) dx -= 1;
                if (kb.downArrowKey.wasPressedThisFrame) dz += 1;
                if (kb.upArrowKey.wasPressedThisFrame) dz -= 1;
                if (dx != 0 || dz != 0) MoveCursor(dx, dz);
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) { TryUpgradeAt(_cursor); return; }
                if (kb.escapeKey.wasPressedThisFrame) { BackToCards(); return; }
            }

            var mouse = Mouse.current;
            if (mouse == null) return;
            if (mouse.rightButton.wasPressedThisFrame) { BackToCards(); return; }
            if (TryGetCellUnderMouse(mouse.position.ReadValue(), out var coord))
            {
                _cursor = coord;
                ClampCursor();
                if (mouse.leftButton.wasPressedThisFrame) TryUpgradeAt(_cursor);
            }
        }

        /// <summary>
        /// 강화 선택 중: 강화 대상이 될 수 있는 블록(통합 카드 = 강화 단계가 있는 모든 블록, 블록별 카드 = 그 블록) 중
        /// 강화 가능 = 초록, 최대 강화 = 금색. 다른 블록은 강조하지 않는다.
        /// </summary>
        private void UpdateUpgradeMarkers()
        {
            int used = 0;
            if (_upgradePicking && cellHighlightPrefab != null && moduleRoot != null)
            {
                float cell = grid.CellSize;
                foreach (var m in grid.Modules)
                {
                    if (!ShownForUpgrade(m)) continue;
                    if (used >= _upgradeMarkers.Count)
                    {
                        var go = Instantiate(cellHighlightPrefab, moduleRoot);
                        go.name = "UpgradeTarget";
                        SetLayerRecursive(go, ModuleFactory.LayerShip);
                        _upgradeMarkers.Add(go.transform);
                    }
                    int w = m.Definition.Width, h = m.Definition.Height;
                    if ((m.RotationSteps & 1) == 1) (w, h) = (h, w);
                    var marker = _upgradeMarkers[used++];
                    marker.gameObject.SetActive(true);
                    marker.localPosition = grid.CoordToLocal(m.Origin) + new Vector3((h - 1) * 0.5f * cell, 0.07f, (w - 1) * 0.5f * cell);
                    marker.localRotation = Quaternion.identity;
                    marker.localScale = new Vector3(h * cell * 0.9f, 1f, w * cell * 0.9f);
                    var r = marker.GetComponentInChildren<Renderer>();
                    if (r != null) r.material.color = m.CanUpgrade ? UpgradeableColor : MaxedColor;
                }
            }
            for (int i = used; i < _upgradeMarkers.Count; i++)
                if (_upgradeMarkers[i] != null) _upgradeMarkers[i].gameObject.SetActive(false);
        }

        /// <summary>강화 선택 중 표시할 블록: 강화 단계가 있는(강화 프로필이 있는) 블록. 블록별 카드면 그 블록만.</summary>
        private bool ShownForUpgrade(ModuleInstance m)
            => m != null && !m.IsDestroyed &&
               (_reward == null ? ModuleUpgrades.MaxLevel(m.Definition) > 0 : m.Definition == _reward);
    }
}

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
    /// <summary>정비 화면 — 격자 배치: 커서·회전·설치·되돌리기·승강·철거, 추천 자리, 유령 미리보기.</summary>
    public partial class RefitUI
    {
        /// <summary>화면 좌표 -> 격자 칸. 렌더 텍스처라 뷰 안의 비율로 바꿔 광선을 쏜다.</summary>
        private bool TryGetCellUnderMouse(Vector2 screenPos, out GridCoord coord)
        {
            coord = default;
            if (shipView == null || shipCamera == null || grid == null) return false;

            var rect = shipView.rectTransform;
            if (!RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, null)) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPos, null, out var local))
                return false;

            var r = rect.rect;
            var viewport = new Vector3((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height, 0f);

            var ray = shipCamera.ViewportPointToRay(viewport);
            var plane = new Plane(grid.transform.up, grid.CoordToWorld(default));
            if (!plane.Raycast(ray, out float enter)) return false;

            coord = grid.WorldToNearestCoord(ray.GetPoint(enter));
            return grid.InBounds(coord);
        }

        // --------------------------------------------------------------- 조작

        private void TakeReward()
        {
            if (_held != null || _rewardUsed || _reward == null) return;

            _held = _reward;
            _heldIsReward = true;
            _heldWasInstalled = false;
            _heldRotation = 0;

            // 빠른 배치: 바로 놓을 수 있는 좋은 자리에서 시작한다. 클릭 한 번이면 설치된다.
            if (TryFindSuggestedPlacement(_held, out var cell, out int rotation))
            {
                _cursor = cell;
                _heldRotation = rotation;
            }

            ClampCursor();
            SpawnGhost();
        }

        /// <summary>
        /// 설치 가능한 자리 중 함교에 가까운 곳을 고른다.
        /// 사격각이 주변 블록에 따라 달라지는 무기는 사격각이 넓은 자리를 먼저 본다.
        /// </summary>
        private bool TryFindSuggestedPlacement(ModuleDefinition def, out GridCoord best, out int bestRotation)
        {
            best = default;
            bestRotation = 0;
            if (def == null || grid == null) return false;

            bool arcWeapon = Game.Combat.FireArcCalculator.UsesNeighborArc(def.Type);
            int rotations = def.CanRotate ? 4 : 1;
            float bestScore = float.MaxValue;
            bool found = false;

            for (int rot = 0; rot < rotations; rot++)
            {
                for (int x = -grid.MaxHalfLength; x <= grid.MaxHalfLength; x++)
                {
                    for (int z = -grid.MaxHalfBeam; z <= grid.MaxHalfBeam; z++)
                    {
                        var c = new GridCoord(x, z);
                        if (!grid.CanPlace(def, c, rot, out _)) continue;

                        // 가까울수록, 좌우 중앙일수록, (무기라면) 사격각이 넓을수록 좋다
                        float score = Mathf.Abs(x) + Mathf.Abs(z) * 1.5f + rot * 0.01f;
                        if (arcWeapon)
                            score -= Game.Combat.FireArcCalculator.Compute(grid, c, rot, def.HeightClass).Width / 30f;

                        if (score >= bestScore) continue;
                        bestScore = score;
                        best = c;
                        bestRotation = rot;
                        found = true;
                    }
                }
            }
            return found;
        }

        private void Rotate()
        {
            if (_held == null || !_held.CanRotate) return;

            _heldRotation = (_heldRotation + 1) & 3;
            ClampCursor();

            if (_ghost != null)
                _ghost.transform.localRotation = Quaternion.Euler(0f, _heldRotation * 90f, 0f);
        }

        private void MoveCursor(int dx, int dz)
        {
            _cursor = new GridCoord(_cursor.X + dx, _cursor.Z + dz);
            ClampCursor();
        }

        private void ClampCursor()
        {
            GetFootprintSize(out int w, out int h);

            _cursor = new GridCoord(
                Mathf.Clamp(_cursor.X, -grid.MaxHalfLength, grid.MaxHalfLength - (w - 1)),
                Mathf.Clamp(_cursor.Z, -grid.MaxHalfBeam, grid.MaxHalfBeam - (h - 1)));
        }

        private void GetFootprintSize(out int w, out int h)
        {
            w = h = 1;
            if (_held == null) return;

            w = _held.Width; h = _held.Height;
            if ((_heldRotation & 1) == 1) (w, h) = (h, w);
        }

        /// <summary>들고 있으면 놓고, 아니면 커서 아래 모듈을 집는다(재배치).</summary>
        private void Confirm()
        {
            if (_held != null) { PlaceHeld(); return; }

            var instance = grid.Get(_cursor);
            if (instance == null) return;

            _held = instance.Definition;
            _heldRotation = instance.RotationSteps;
            _heldIsReward = false;

            _heldWasInstalled = true;
            _heldOrigin = instance.Origin;
            _heldOriginRotation = instance.RotationSteps;
            _heldWasRaised = instance.IsRaised;
            _heldUpgrade = instance.Upgrade;
            _cursor = instance.Origin;

            factory.Uninstall(instance);
            _originSynergy = ModuleSynergy.Preview(grid, _held, _heldOrigin, _heldOriginRotation);
            ClampCursor();
            SpawnGhost();
        }

        private void PlaceHeld()
        {
            if (!CanPlaceHere()) return;
            var placed = factory.Install(_held, _cursor, _heldRotation, _heldWasInstalled ? _heldUpgrade : null);
            if (placed == null) return;
            RestoreRaise(placed);

            if (_heldIsReward) _rewardUsed = true;
            ClearHeld();
        }

        private bool CanPlaceHere()
            => _held != null && grid.CanPlace(_held, _cursor, _heldRotation, out _);

        /// <summary>집어든 기존 모듈을 원래 자리로 되돌린다.</summary>
        private void ReturnHeld()
        {
            if (_held == null || !_heldWasInstalled) return;

            RestoreRaise(factory.Install(_held, _heldOrigin, _heldOriginRotation, _heldUpgrade));
            ClearHeld();
        }

        /// <summary>올려져 있던 무기를 옮겼으면, 새 자리도 조건을 만족할 때 다시 올린다.</summary>
        private void RestoreRaise(ModuleInstance placed)
        {
            if (!_heldWasRaised || placed == null) return;
            if (Game.Combat.FireArcCalculator.CanRaise(grid, placed)) factory.SetRaised(placed, true);
        }

        private void ClearHeld()
        {
            _originSynergy = null;
            _heldWasRaised = false;
            _heldUpgrade = null;
            _held = null;
            _heldIsReward = false;
            _heldWasInstalled = false;
            _heldRotation = 0;
            ClearGhost();
        }

        /// <summary>커서 아래 무기를 승강 거치대에 올리거나 내린다. 올리기는 3면 이상 막혔을 때만.</summary>
        private void ToggleRaiseUnderCursor()
        {
            if (_held != null || factory == null) return;

            var instance = grid.Get(_cursor);
            if (instance == null) return;

            if (instance.IsRaised) factory.SetRaised(instance, false);
            else if (Game.Combat.FireArcCalculator.CanRaise(grid, instance)) factory.SetRaised(instance, true);
        }

        /// <summary>커서 아래 모듈을 영구 철거한다. 함교는 배의 뿌리라 지울 수 없다.</summary>
        private void ScrapUnderCursor()
        {
            if (_held != null) return;

            var instance = grid.Get(_cursor);
            if (instance == null || instance.Definition.Type == ModuleType.Bridge) return;

            factory.Uninstall(instance);
        }

        private void SpawnGhost()
        {
            ClearGhost();
            if (_held == null || _held.Prefab == null || moduleRoot == null) return;

            _ghost = Instantiate(_held.Prefab, moduleRoot);
            _ghost.name = "RefitGhost";
            _ghost.transform.localRotation = Quaternion.Euler(0f, _heldRotation * 90f, 0f);

            foreach (var mb in _ghost.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
            foreach (var col in _ghost.GetComponentsInChildren<Collider>(true)) col.enabled = false;

            SetLayerRecursive(_ghost, ModuleFactory.LayerShip);
        }

        private void ClearGhost()
        {
            if (_ghost != null) Destroy(_ghost);
            _ghost = null;
        }

        private void EnsureHighlight()
        {
            if (_highlight != null || cellHighlightPrefab == null || moduleRoot == null) return;

            var go = Instantiate(cellHighlightPrefab, moduleRoot);
            go.name = "RefitCursor";
            _highlight = go.transform;
            _highlightRenderer = go.GetComponentInChildren<Renderer>();

            SetLayerRecursive(go, ModuleFactory.LayerShip);
        }
    }
}

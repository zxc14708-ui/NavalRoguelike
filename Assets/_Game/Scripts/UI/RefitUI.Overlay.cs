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
    /// <summary>정비 화면 — 격자 위 표시: 배지·시너지 강조·사격각 메시·정보 문구.</summary>
    public partial class RefitUI
    {
        /// <summary>설치된 블록 위의 강화 단계 표시(강화 I = "I", 강화 II = "II"). 기본은 표시 없음.</summary>
        private void UpdateBadges()
        {
            int used = 0;
            if (moduleRoot != null && shipCamera != null)
            {
                float cell = grid.CellSize;
                foreach (var m in grid.Modules)
                {
                    if (m == null || m.UpgradeLevel <= 0) continue;
                    if (used >= _badges.Count)
                    {
                        var go = new GameObject("UpgradeBadge");
                        go.transform.SetParent(moduleRoot, false);
                        go.layer = ModuleFactory.LayerShip;
                        var t = go.AddComponent<TextMeshPro>();
                        if (titleText != null) t.font = titleText.font;
                        t.fontSize = 13f;
                        t.fontStyle = FontStyles.Bold;
                        t.alignment = TextAlignmentOptions.Center;
                        t.rectTransform.sizeDelta = new Vector2(6f, 3f);
                        t.outlineWidth = 0.25f;
                        t.outlineColor = new Color32(0, 0, 0, 255);
                        _badges.Add(t);
                    }
                    var badge = _badges[used++];
                    badge.gameObject.SetActive(true);
                    badge.text = $"<mark=#081216D9> {ModuleUpgrades.Roman(m.UpgradeLevel)} </mark>";   // 어두운 바탕으로 갑판 위에서도 읽히게
                    badge.color = m.CanUpgrade ? new Color(0.55f, 1f, 0.6f) : new Color(1f, 0.82f, 0.3f);
                    int w = m.Definition.Width, h = m.Definition.Height;
                    if ((m.RotationSteps & 1) == 1) (w, h) = (h, w);
                    badge.transform.localPosition = grid.CoordToLocal(m.Origin) + new Vector3((h - 1) * 0.5f * cell, 3.2f, (w - 1) * 0.5f * cell);
                    badge.transform.rotation = shipCamera.transform.rotation;
                }
            }
            for (int i = used; i < _badges.Count; i++)
                if (_badges[i] != null) _badges[i].gameObject.SetActive(false);
        }

        private void UpdateSynergyHighlights()
        {
            int used = 0;
            if (_held != null && grid != null && grid.CanPlace(_held, _cursor, _heldRotation, out _) &&
                cellHighlightPrefab != null && moduleRoot != null &&
                !string.IsNullOrEmpty(ModuleSynergy.Preview(grid, _held, _cursor, _heldRotation)))
            {
                ShipGrid.GetFootprint(_held, _cursor, _heldRotation, _previewCells);
                foreach (var module in grid.Modules)
                {
                    if (module == null || !module.IsOperational ||
                        !RelevantSynergyNeighbour(_held.Type, module.Definition.Type) ||
                        !ModuleSynergy.Touches(_previewCells, module.OccupiedCoords)) continue;
                    if (used >= _synergyHighlights.Count)
                    {
                        var go = Instantiate(cellHighlightPrefab, moduleRoot);
                        go.name = "SynergyNeighbour";
                        SetLayerRecursive(go, ModuleFactory.LayerShip);
                        var renderer = go.GetComponentInChildren<Renderer>();
                        if (renderer != null) renderer.material.color = new Color(0.25f, 0.9f, 1f, 0.35f);
                        _synergyHighlights.Add(go.transform);
                    }
                    var marker = _synergyHighlights[used++];
                    marker.gameObject.SetActive(true);
                    marker.localPosition = grid.CoordToLocal(module.Origin) + Vector3.up * 0.07f;
                    marker.localRotation = Quaternion.identity;
                    marker.localScale = new Vector3(grid.CellSize * 0.94f, 1f, grid.CellSize * 0.94f);
                }
            }
            for (int i = used; i < _synergyHighlights.Count; i++)
                _synergyHighlights[i].gameObject.SetActive(false);
        }

        private static bool RelevantSynergyNeighbour(ModuleType held, ModuleType other)
        {
            if (held == ModuleType.RepairBay || other == ModuleType.RepairBay) return true;
            return held switch
            {
                ModuleType.HelicopterDeck => other == ModuleType.HelicopterDeck || other == ModuleType.Sonar,
                ModuleType.Sonar => other == ModuleType.HelicopterDeck || other == ModuleType.AswLauncher,
                ModuleType.AswLauncher => other == ModuleType.Sonar,
                ModuleType.Radar => other == ModuleType.SamLauncher || other == ModuleType.Ciws,
                ModuleType.SamLauncher or ModuleType.Ciws => other == ModuleType.Radar,
                ModuleType.EwSuite => other == ModuleType.DecoyLauncher,
                ModuleType.DecoyLauncher => other == ModuleType.EwSuite,
                ModuleType.NavalGun => other == ModuleType.Magazine,
                ModuleType.Magazine => other == ModuleType.NavalGun,
                _ => false,
            };
        }

        // ------------------------------------------------------------ 사격각 표시

        /// <summary>
        /// 설치된 무기와 들고 있는 무기의 사격각을 갑판 위 부채꼴로 그린다.
        /// 들고 있는 무기와 커서 아래 무기는 사격 금지 구역도 붉게 그린다.
        /// </summary>
        private void UpdateArcViews()
        {
            int used = 0;
            var hovered = _held == null ? grid.Get(_cursor) : null;

            foreach (var m in grid.Modules)
            {
                if (m?.Definition == null || !Game.Combat.FireArcCalculator.HasCutout(m.Definition.Type)) continue;
                var arc = Game.Combat.FireArcCalculator.ComputeFor(m.Definition.Type, grid, m.Origin, m.RotationSteps, m.EffectiveHeight);
                DrawArc(ref used, m.Origin, m.RotationSteps, arc, InstalledArcColor, m == hovered);
            }

            if (_held != null && Game.Combat.FireArcCalculator.HasCutout(_held.Type))
            {
                var arc = Game.Combat.FireArcCalculator.ComputeFor(_held.Type, grid, _cursor, _heldRotation, _held.HeightClass);
                DrawArc(ref used, _cursor, _heldRotation, arc, HeldArcColor, true);
            }

            for (int i = used; i < _arcViews.Count; i++)
                if (_arcViews[i] != null) _arcViews[i].gameObject.SetActive(false);
        }

        private void DrawArc(ref int used, GridCoord origin, int rotationSteps, Game.Combat.FireArc arc, Color color, bool showCutout)
        {
            arc.GetRuns(_arcRuns, cutOut: false);
            DrawRuns(used++, origin, rotationSteps, color, arcInnerRadiusCells, arcOuterRadiusCells);

            if (!showCutout) return;
            arc.GetRuns(_arcRuns, cutOut: true);
            if (_arcRuns.Count > 0)
                DrawRuns(used++, origin, rotationSteps, CutoutArcColor, arcInnerRadiusCells, arcOuterRadiusCells * 0.8f);
        }

        private void DrawRuns(int index, GridCoord origin, int rotationSteps, Color color, float innerCells, float outerCells)
        {
            var view = GetArcView(index);
            if (view == null) return;

            float cell = grid.CellSize;
            view.gameObject.SetActive(true);
            view.transform.localPosition = grid.CoordToLocal(origin) + Vector3.up * (color == CutoutArcColor ? 0.09f : 0.08f);
            view.transform.localRotation = Quaternion.identity;

            BuildArcMesh(view.sharedMesh, Game.Combat.FireArcCalculator.FacingYaw(rotationSteps), _arcRuns,
                         innerCells * cell, outerCells * cell);

            _arcBlock ??= new MaterialPropertyBlock();
            _arcBlock.SetColor("_BaseColor", color);
            _arcBlock.SetColor("_Color", color);
            view.GetComponent<MeshRenderer>().SetPropertyBlock(_arcBlock);
        }

        private MeshFilter GetArcView(int index)
        {
            if (moduleRoot == null || arcMaterial == null) return null;

            while (_arcViews.Count <= index)
            {
                var go = new GameObject($"FireArc_{_arcViews.Count}", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(moduleRoot, false);
                go.layer = ModuleFactory.LayerShip;

                var mf = go.GetComponent<MeshFilter>();
                mf.sharedMesh = new Mesh { name = "FireArc" };
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = arcMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                _arcViews.Add(mf);
            }
            return _arcViews[index];
        }

        private readonly List<(float start, float width)> _arcRuns = new();

        /// <summary>열린 조각마다 고리 모양 부채꼴을 그려 한 메시로 합친다. 0도 = 선수(+Z 로컬), 조각 각도는 facingYaw 기준.</summary>
        private static void BuildArcMesh(Mesh mesh, float facingYaw, List<(float start, float width)> runs, float inner, float outer)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();

            foreach (var (runStart, width) in runs)
            {
                int segments = Mathf.Max(2, Mathf.CeilToInt(width / 5f));
                int baseIndex = verts.Count;
                float start = facingYaw + runStart;

                for (int i = 0; i <= segments; i++)
                {
                    float yaw = (start + width * i / segments) * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                    verts.Add(dir * inner);
                    verts.Add(dir * outer);
                }

                for (int i = 0; i < segments; i++)
                {
                    int v = baseIndex + i * 2;
                    tris.Add(v); tris.Add(v + 1); tris.Add(v + 3);
                    tris.Add(v); tris.Add(v + 3); tris.Add(v + 2);
                }
            }

            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        private void ClearArcViews()
        {
            foreach (var view in _arcViews)
            {
                if (view == null) continue;
                if (view.sharedMesh != null) Destroy(view.sharedMesh);
                Destroy(view.gameObject);
            }
            _arcViews.Clear();
        }

        private void AppendRaiseHint(ModuleInstance m)
        {
            if (!Game.Combat.FireArcCalculator.UsesNeighborArc(m.Definition.Type)) return;

            if (m.IsRaised)
            {
                _sb.AppendLine("<color=#7fb8e0>거치대에 올림</color> — 중간 블록 너머로 사격, 내구 −25%  (E로 내리기)");
                return;
            }

            if (Game.Combat.FireArcCalculator.RaisesAnywhere(m.Definition.Type))
            {
                _sb.AppendLine("<color=#e0c05a>E: 거치대 올리기</color> — 중간 블록 너머로 사격 (내구 −25%)");
                return;
            }

            int blocked = Game.Combat.FireArcCalculator.CountBlockingNeighbors(grid, m);
            int need = Game.Combat.FireArcCalculator.RaiseRequiredBlockedSides;
            _sb.AppendLine(blocked >= need
                ? "<color=#e0c05a>E: 거치대 올리기</color> — 막힌 면이 많아 높여 쏠 수 있다 (내구 −25%)"
                : $"<color=#8fa4b8>거치대: 막힌 면 {blocked}/{need} — {need}면 이상 막히면 올릴 수 있다</color>");
        }

        private string BuildInfo()
        {
            _sb.Clear();

            if (_upgradePicking)
            {
                _sb.AppendLine(_reward != null
                    ? $"<b>강화할 {_reward.DisplayName}을(를) 고르세요</b> — 클릭 또는 방향키+Enter"
                    : "<b>강화할 장비를 고르세요</b> — 클릭 또는 방향키+Enter");
                _sb.AppendLine("<color=#7fd08a>초록 = 강화 가능</color>   <color=#e0c05a>금색 = 최대 강화</color>   우클릭·Esc = 카드로 돌아가기");
                var hovered = grid.Get(_cursor);
                if (ShownForUpgrade(hovered))
                {
                    if (hovered.CanUpgrade)
                        _sb.AppendLine($"{hovered.Definition.DisplayName} ({hovered.Origin.X},{hovered.Origin.Z}) {UpgradeStepLabel(hovered.UpgradeLevel)}\n" +
                                       ModuleCardText.UpgradeDiff(hovered.Definition, hovered.UpgradeLevel, hovered.UpgradeLevel + 1, 4));
                    else _sb.AppendLine($"{hovered.Definition.DisplayName} ({hovered.Origin.X},{hovered.Origin.Z}) <color=#e0c05a>이미 최대 강화</color>");
                }
                return _sb.ToString();
            }

            if (_held != null)
            {
                _sb.AppendLine($"들고 있음: <b>{_held.DisplayName}</b>  {_held.Width}x{_held.Height}" +
                               (_heldWasInstalled ? $"   (원래 자리 {_heldOrigin} · 우클릭/Esc로 복귀)" : ""));

                if (!grid.CanPlace(_held, _cursor, _heldRotation, out string reason) && reason != null)
                    _sb.AppendLine($"<color=#e06a5a>{reason}</color>");
                else
                {
                    string synergy = ModuleSynergy.Preview(grid, _held, _cursor, _heldRotation);
                    if (!string.IsNullOrEmpty(synergy)) _sb.AppendLine($"<color=#68dede>연결 시너지: {synergy}</color>");
                    if (!string.IsNullOrEmpty(_originSynergy) && _originSynergy != synergy)
                        _sb.AppendLine($"<color=#e0a16a>이전 자리 연결 해제: {_originSynergy}</color>");
                    // 자리로 형태가 정해지는 블록: 여기 놓으면 무엇이 되는가
                    if (ModuleVariants.HasVariants(_held.Type))
                    {
                        var foot = new List<GridCoord>();
                        ShipGrid.GetFootprint(_held, _cursor, _heldRotation, foot);
                        var v = ModuleVariants.Resolve(_held.Type, grid, foot, out _);
                        if (v != ModuleVariant.None)
                            _sb.AppendLine($"<color=#9fe0a0>형태: <b>{ModuleVariants.Name(v)}</b></color> — {ModuleVariants.Summary(v)}");
                    }
                }

                if (Game.Combat.FireArcCalculator.HasCutout(_held.Type))
                {
                    var arc = Game.Combat.FireArcCalculator.ComputeFor(_held.Type, grid, _cursor, _heldRotation, _held.HeightClass);
                    _sb.AppendLine($"<color=#e0c05a>사격각 {arc.Width:0}°</color>  <color=#e0705a>붉은 곳 = 사격 금지 구역</color>  (R로 포신 방향 전환)");
                }
            }
            else
            {
                var m = grid.Get(_cursor);
                if (m != null)
                {
                    _sb.AppendLine($"{m.Definition.DisplayName}" +
                                   (m.UpgradeLevel > 0 ? $" <color=#7fd08a>강화 {ModuleUpgrades.Roman(m.UpgradeLevel)}</color>" : "") +
                                   $"  HP {m.Hp:0}/{m.MaxHp:0}" +
                                   (m.IsDestroyed ? "  <color=#e06a5a>파괴됨</color>" : ""));
                    _sb.AppendLine(m.Definition.Description);
                    if (m.Variant != ModuleVariant.None)
                        _sb.AppendLine($"<color=#9fe0a0>형태: <b>{ModuleVariants.Name(m.Variant)}</b></color> — {ModuleVariants.Summary(m.Variant)}");
                    if (m.UpgradeLevel > 0)
                        _sb.AppendLine("<color=#7fd08a>적용 중:</color> " + ModuleCardText.UpgradeDiff(m.Definition, 0, m.UpgradeLevel, 4).Replace("\n", " · "));
                    AppendRaiseHint(m);
                }
                else
                {
                    _sb.AppendLine($"{_cursor} — 빈 자리 (기존 모듈과 맞닿아야 설치 가능)");
                }
            }

            string growth = ModuleCardText.BuildGrowthSummary();
            if (!string.IsNullOrEmpty(growth)) _sb.AppendLine($"<color=#ffd060>성장</color> {growth}");
            _sb.AppendLine();
            _sb.AppendLine("마우스: 클릭 집기·놓기 · 우클릭 되돌리기 · 휠 회전");
            _sb.Append("키보드: 방향키 이동 · Enter 집기·놓기 · R 회전 · E 거치대 · X 철거 · Space 전투 재개");

            if (_held != null) _sb.Append("   <color=#e0c05a>(내려놓아야 재개할 수 있습니다)</color>");

            return _sb.ToString();
        }
    }
}

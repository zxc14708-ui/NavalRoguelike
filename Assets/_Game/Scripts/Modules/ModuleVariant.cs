using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Ship;

namespace Game.Modules
{
    /// <summary>
    /// 놓인 자리에 따라 형태가 바뀌는 블록의 형태(2026-10-03). 순서를 바꾸지 말고 뒤에만 덧붙인다.
    /// </summary>
    public enum ModuleVariant
    {
        None,
        BowSonar,             // 소나 · 앞이 트인 자리: 선수 소나 — 앞쪽 ±70°만, 기본 반경
        TowedSonar,           // 소나 · 뒤가 트인 자리: 예인 소나(TAS) — 사방, 반경 1.5배, 빠르면 절반
        HullSonar,            // 소나 · 그 밖(함내): 함내 소나 — 사방, 짧은 반경
        DepthChargeRack,      // 폭뢰 · 뒤가 트인 자리: 선미 투하대 — 배 바로 뒤에 많이 떨어뜨림
        DepthChargeProjector, // 폭뢰 · 옆이 트인 자리: 측면 발사대 — 트인 현측으로 던짐
        TorpedoTubeSide,      // 경어뢰 발사관 · 형태는 하나 — 트인 현측(Sides)만 기록한다
    }

    /// <summary>측면 발사대가 던질 수 있는 현측.</summary>
    [Flags]
    public enum ModuleSides { None = 0, Port = 1, Starboard = 2 }

    /// <summary>
    /// 자리 → 형태 판정과 형태별 수치. 형태는 놓을 때 고정하지 않고, 격자가 바뀔 때마다(설치·철거·재배치)
    /// ShipGrid가 다시 판정한다 — 선수 소나 앞에 블록을 붙이면 함내 소나가 되는 식.
    ///   소나: 앞이 트임 → 선수 소나, 아니면 뒤가 트임 → 예인 소나, 아니면 함내 소나.
    ///   폭뢰: 뒤가 트임 → 선미 투하대(모서리처럼 옆도 트였어도 투하대), 아니면 옆이 트임 → 측면 발사대.
    ///         사방이 막힌 자리는 설치할 수 없고(PlacementZone.SideOrStern), 이미 놓인 폭뢰의 옆·뒤를 모두 막는 설치도 거부한다.
    /// "트임" = 그 방향 바로 옆 칸이 비어 있음(함체는 점유 칸을 따라 생기므로 빈 칸은 바다).
    /// </summary>
    public static class ModuleVariants
    {
        // ---- 소나
        public const float BowSonarHalfArc = 70f;      // 선수 소나: 함수 기준 ±70°
        public const float TowedRangeMultiplier = 1.5f;
        public const float TowedFastSpeedRatio = 0.7f; // 최고 속력의 70% 이상이면
        public const float TowedFastMultiplier = 0.5f; // 예인 소나 반경 절반
        public const float HullRangeMultiplier = 0.65f;

        // ---- 폭뢰
        public const int RackSalvo = 4;
        public const float RackDamageMultiplier = 1.25f;
        public const float RackReloadMultiplier = 0.8f;
        public const float RackReach = 8f;             // 투하 지점에서 이 반경 안의 잠수함만 노린다
        public const float RackDropBehind = 2.5f;      // 투하 지점: 블록에서 선미 쪽으로
        public const float ProjectorHalfArc = 75f;     // 측면 발사대: 현측 정횡 기준 ±75°

        public static bool HasVariants(ModuleType type) => type == ModuleType.Sonar || type == ModuleType.AswLauncher
                                                           || type == ModuleType.TorpedoTube;

        // ---- 경어뢰 발사관
        public const float TorpedoTubeHalfArc = 75f;   // 트인 현측 정횡 기준 ±75°

        /// <summary>그 자리에서의 형태. 폭뢰가 사방이 막힌 자리면 None.</summary>
        public static ModuleVariant Resolve(ModuleType type, ShipGrid grid, IReadOnlyList<GridCoord> coords, out ModuleSides sides)
        {
            sides = ModuleSides.None;
            if (grid == null || coords == null || coords.Count == 0) return ModuleVariant.None;
            switch (type)
            {
                case ModuleType.Sonar:
                    if (PlacementRuleEvaluator.IsOpen(grid, coords, +1, 0)) return ModuleVariant.BowSonar;
                    if (PlacementRuleEvaluator.IsOpen(grid, coords, -1, 0)) return ModuleVariant.TowedSonar;
                    return ModuleVariant.HullSonar;
                case ModuleType.AswLauncher:
                    if (PlacementRuleEvaluator.IsOpen(grid, coords, 0, -1)) sides |= ModuleSides.Port;
                    if (PlacementRuleEvaluator.IsOpen(grid, coords, 0, +1)) sides |= ModuleSides.Starboard;
                    if (PlacementRuleEvaluator.IsOpen(grid, coords, -1, 0)) return ModuleVariant.DepthChargeRack;
                    return sides != ModuleSides.None ? ModuleVariant.DepthChargeProjector : ModuleVariant.None;
                case ModuleType.TorpedoTube:
                    if (PlacementRuleEvaluator.IsOpen(grid, coords, 0, -1)) sides |= ModuleSides.Port;
                    if (PlacementRuleEvaluator.IsOpen(grid, coords, 0, +1)) sides |= ModuleSides.Starboard;
                    return sides != ModuleSides.None ? ModuleVariant.TorpedoTubeSide : ModuleVariant.None;
                default:
                    return ModuleVariant.None;
            }
        }

        public static string Name(ModuleVariant v) => v switch
        {
            ModuleVariant.BowSonar => "선수 소나",
            ModuleVariant.TowedSonar => "예인 소나",
            ModuleVariant.HullSonar => "함내 소나",
            ModuleVariant.DepthChargeRack => "폭뢰 투하대",
            ModuleVariant.DepthChargeProjector => "폭뢰 발사대",
            ModuleVariant.TorpedoTubeSide => "현측 어뢰 발사관",
            _ => "",
        };

        /// <summary>한 줄 설명(정비 화면·카드).</summary>
        public static string Summary(ModuleVariant v) => v switch
        {
            ModuleVariant.BowSonar => $"앞쪽 ±{BowSonarHalfArc:0}°만 탐지, 언제나 작동",
            ModuleVariant.TowedSonar => $"사방 탐지 · 반경 {TowedRangeMultiplier:0.#}배 · 최고 속력 {TowedFastSpeedRatio * 100f:0}% 이상이면 반경 절반",
            ModuleVariant.HullSonar => $"사방 탐지 · 반경 {HullRangeMultiplier * 100f:0}%",
            ModuleVariant.DepthChargeRack => $"배 바로 뒤에 {RackSalvo}발 투하 · 피해 {RackDamageMultiplier * 100f:0}% · 재장전 {RackReloadMultiplier * 100f:0}% — 잠수함 위를 지나가야 맞음",
            ModuleVariant.DepthChargeProjector => $"트인 현측으로 던짐(정횡 ±{ProjectorHalfArc:0}°) · 앞뒤로는 못 던짐",
            ModuleVariant.TorpedoTubeSide => $"트인 현측(정횡 ±{TorpedoTubeHalfArc:0}°)으로 부채꼴 3발 · 앞뒤로는 못 쏨",
            _ => "",
        };

        /// <summary>자리 규칙 안내(카드의 배치 줄).</summary>
        public static string PlacementHint(ModuleType type) => type switch
        {
            ModuleType.Sonar => "앞이 트임 → 선수 소나 · 뒤가 트임 → 예인 소나 · 그 밖 → 함내 소나",
            ModuleType.AswLauncher => "뒤가 트임 → 폭뢰 투하대 · 옆이 트임 → 폭뢰 발사대 · 사방이 막힌 자리 불가",
            ModuleType.TorpedoTube => "좌현이나 우현이 트인 자리에만 · 트인 현측으로만 발사",
            _ => "",
        };

        /// <summary>형태별 모델 프리팹(Resources/Modules/Variants/…). 없으면 기본 블록 모델을 그대로 쓴다.</summary>
        public static string ModelName(ModuleVariant v) => v switch
        {
            ModuleVariant.BowSonar => "MOD_Sonar_Bow",
            ModuleVariant.TowedSonar => "MOD_Sonar_TAS",
            ModuleVariant.HullSonar => "MOD_Sonar_Internal",
            ModuleVariant.DepthChargeRack => "MOD_DepthChargeRack",
            ModuleVariant.DepthChargeProjector => "MOD_DepthChargeProjector",
            _ => null,
        };

        /// <summary>소나 형태별 실효 반경(속력 반영). baseRange는 성장 카드까지 반영한 값.</summary>
        public static float SonarRange(ModuleVariant v, float baseRange, float speedRatio) => v switch
        {
            ModuleVariant.BowSonar => baseRange,
            ModuleVariant.TowedSonar => baseRange * TowedRangeMultiplier * (speedRatio >= TowedFastSpeedRatio ? TowedFastMultiplier : 1f),
            ModuleVariant.HullSonar => baseRange * HullRangeMultiplier,
            _ => baseRange,
        };

        /// <summary>소나 형태가 그 방향(함수 기준 각도, 0 = 정면)을 보는가.</summary>
        public static bool SonarSees(ModuleVariant v, float bearingFromBow)
            => v != ModuleVariant.BowSonar || Mathf.Abs(Mathf.DeltaAngle(0f, bearingFromBow)) <= BowSonarHalfArc;
    }
}

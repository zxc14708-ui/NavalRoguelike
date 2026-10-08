using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Game.Modules;

namespace Game.UI
{
    /// <summary>
    /// 모듈 정의를 카드에 보여줄 문구로 바꾼다.
    /// 표시 규칙을 한곳에 모아 카드·툴팁·현황판이 같은 표현을 쓰게 한다.
    ///
    /// 카드 본문은 "항목 | 값" 두 열 표로 쓴다(값은 카드 폭의 ValueColumn 지점에 맞춰 정렬 — TMP pos 태그).
    /// 한 줄 요약(역할)을 맨 위에, 그다음 핵심 수치, 상성, 배치 제약 순. 7줄 안팎.
    /// </summary>
    public static class ModuleCardText
    {
        private const string ValueColumn = "<pos=44%>";
        private const string Muted = "#8fa4b8";
        private const string Good = "#7fd08a";
        private const string Warn = "#e0956a";
        private const string Key = "#e0c05a";

        /// <summary>카드 위쪽 분류 줄: "근거리 무장 · 1×1 · 내구 40 · 높이 중간".</summary>
        public static string BuildTag(ModuleDefinition def)
        {
            if (def == null) return string.Empty;
            string max = def.MaxCount > 0 ? $"  ·  최대 {def.MaxCount}개" : "";
            return $"<color={CategoryColor(def.Type)}>{Category(def.Type)}</color>  ·  {def.Width}×{def.Height}  ·  내구 {def.MaxHp:0}  ·  높이 {HeightLabel(def.HeightClass)}{max}";
        }

        /// <summary>카드 본문(새 블록): 역할 요약 한 줄 + 두 열 표.</summary>
        public static string BuildStats(ModuleDefinition def)
        {
            if (def == null) return string.Empty;
            var sb = new StringBuilder();
            var s = def.Stats;
            sb.AppendLine($"<color={Muted}>{Summary(def.Type)}</color>");

            switch (def.Type)
            {
                case ModuleType.Autocannon:
                case ModuleType.NavalGun:
                case ModuleType.GuidedRocket:
                case ModuleType.Vls:
                case ModuleType.SamLauncher:
                    Row(sb, "교전 거리", $"{s.MinRange:0} ~ {s.Range:0} m");
                    Row(sb, "피해", $"{s.Damage:0.#}");
                    Row(sb, "발사 간격", $"{s.ReloadTime:0.0#} 초");
                    Ammo(sb, s);
                    break;
                case ModuleType.Ciws:
                    Row(sb, "요격 반경", $"{s.Range:0} m");
                    Row(sb, "연사", $"초당 {1f / Mathf.Max(0.01f, s.ReloadTime):0}발 · 피해 {s.Damage:0.##}");
                    Ammo(sb, s);
                    break;
                case ModuleType.AswLauncher:
                    Row(sb, "대잠 사거리", $"{s.Range:0} m");
                    Row(sb, "피해", $"{s.Damage:0} × 2");
                    Row(sb, "투하 간격", $"{s.ReloadTime:0.0} 초");
                    Ammo(sb, s);
                    break;
                case ModuleType.Radar:
                    Row(sb, "탐지 거리", $"{s.DetectionRange:0} m");
                    Row(sb, "동시 추적", $"+{s.ExtraTrackedTargets}");
                    break;
                case ModuleType.Sonar:
                    Row(sb, "잠수함 탐지", $"{s.DetectionRange:0} m");
                    Row(sb, "능동 핑", "재사용마다 자동");
                    break;
                case ModuleType.DecoyLauncher:
                    Row(sb, "<color=" + Key + ">[Q]</color> 기만체", $"재장전 {s.ReloadTime:0} 초");
                    Row(sb, "<color=" + Key + ">[F]</color> 연막", "8초 · 재장전 28 초");
                    break;
                case ModuleType.EwSuite:
                    Row(sb, "<color=" + Key + ">[E]</color> 재밍", $"반경 {s.Range:0} m");
                    Row(sb, "재장전", $"{s.ReloadTime:0} 초");
                    Row(sb, "효과", "미사일 유도 75% 차단");
                    break;
                case ModuleType.Magazine:
                    Row(sb, "발사 간격", $"−{s.ReloadBonus * 100f:0}%");
                    Row(sb, "탄약 용량", $"+{s.AmmoCapacityBonus * 100f:0}%");
                    Row(sb, "보급 속도", $"+{s.ResupplyBonus * 100f:0}%");
                    Row(sb, "지원 거리", $"{s.SupportRadiusCells}칸 (기관포·76mm)");
                    Row(sb, $"<color={Warn}>유폭</color>", $"<color={Warn}>{s.CookOffDamage:0}</color>");
                    break;
                case ModuleType.RepairBay:
                    Row(sb, "피해 감소", $"{s.DamageReduction * 100f:0}%");
                    Row(sb, "선체 회복", $"{s.HullRepairPerSecond:0.0} /초");
                    Row(sb, "모듈 수리", $"{s.ModuleRepairPerSecond:0.0} /초");
                    Row(sb, "<color=" + Key + ">[R]</color> 응급 수리", "재장전 35 초");
                    break;
                case ModuleType.HelicopterDeck:
                    Row(sb, "출격 간격", $"{s.SortieCooldown:0} 초");
                    Row(sb, "우선 표적", "잠수함 → 수상함");
                    break;
                case ModuleType.Bridge:
                    Row(sb, "탐지 거리", $"{s.DetectionRange:0} m");
                    Row(sb, "선체 회복", $"{s.HullRepairPerSecond:0.0} /초");
                    break;
                case ModuleType.FleetRelay:
                    Row(sb, "편대 재사용 속도", $"+{s.EscortFireRateBonus * 100f:0}%");
                    Row(sb, "대상", "호위함 자율 무장");
                    Row(sb, "지휘 점수·슬롯", "증가 없음");
                    Row(sb, "중복", "가장 강한 1개만 적용");
                    break;
                case ModuleType.TurboIntake:
                    Row(sb, "최고 속력", $"+{s.SpeedBonus * 100f:0}%");
                    Row(sb, "가속", $"+{s.AccelerationBonus * 100f:0}%");
                    Row(sb, "항해 중 포 피해", $"+{s.MovingGunDamageBonus * 100f:0}%");
                    Row(sb, "조건", "최고 속력의 50% 이상");
                    Row(sb, "대상", "기관포·76mm 함포");
                    Row(sb, "중복", "가장 강한 1개만 적용");
                    break;
                case ModuleType.FireControlArray:
                    Row(sb, "동시 추적", $"+{s.ExtraTrackedTargets}");
                    Row(sb, "유도 무장 사거리", $"+{s.GuidedRangeBonus * 100f:0}%");
                    Row(sb, "대상", "VLS·유도로켓·함대공");
                    Row(sb, "중복", "가장 강한 1개만 적용");
                    break;
                case ModuleType.MissileLogistics:
                    Row(sb, "발사 간격", $"−{s.MissileReloadReduction * 100f:0}%");
                    Row(sb, "셀 보급 시간", $"−{s.MissileReloadReduction * 100f:0}%");
                    Row(sb, "대상", "VLS·유도로켓 (함대공 제외)");
                    Row(sb, "중복", "가장 강한 1개만 적용");
                    break;
            }

            Efficiency(sb, def);
            string zone = def.Placement.Zone switch
            {
                PlacementZone.BowOnly => "뱃머리(앞이 트인 자리)만",
                PlacementZone.SternOnly => "선미(뒤가 트인 자리)만",
                PlacementZone.SideOrStern => "옆이나 뒤가 트인 자리(사방이 막힌 자리 불가)",
                _ => null,
            };
            // 자리로 형태가 바뀌는 블록(소나·폭뢰)
            string variants = ModuleVariants.PlacementHint(def.Type);
            if (!string.IsNullOrEmpty(variants)) Row(sb, $"<color={Key}>형태</color>", $"<color={Key}>{variants}</color>");
            if (zone != null) Row(sb, $"<color={Key}>배치</color>", $"<color={Key}>{zone}</color>");
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// 무장 강화 카드(독립 카드): 단계 · 전후 수치(3줄) · 적용 · 공간.
        /// </summary>
        public static string BuildUpgradeChoice(ModuleDefinition def, int fromLevel)
        {
            if (def == null) return string.Empty;
            var sb = new StringBuilder();
            string step = fromLevel <= 0 ? "기본 → 강화 I" : $"강화 {ModuleUpgrades.Roman(fromLevel)} → {ModuleUpgrades.Roman(fromLevel + 1)}";
            sb.AppendLine($"<color={Good}><b>무장 강화</b></color>{ValueColumn}<b>{step}</b>");
            foreach (var line in UpgradeDiff(def, fromLevel, fromLevel + 1, 3, aligned: true).Split('\n'))
                if (!string.IsNullOrEmpty(line)) sb.AppendLine(line);
            Row(sb, "적용", "즉시 · 대상 선택");
            Row(sb, "공간", $"<color={Good}>추가 사용 없음</color>");
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// 통합 장비 강화 카드 본문(2026-10-03): 강화할 수 있는 장비를 종류별로(수 · 가장 낮은 단계 → 다음) 보여 준다.
        /// 고르면 함선에서 강화할 장비 하나를 고른다.
        /// </summary>
        public static string BuildEquipmentUpgradeChoice(Game.Ship.ShipGrid grid)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<color={Muted}>고르면 함선에서 강화할 장비를 하나 고른다</color>");
            var defs = new List<ModuleDefinition>();
            var counts = new Dictionary<ModuleDefinition, int>();
            var lowest = new Dictionary<ModuleDefinition, int>();
            if (grid != null)
            {
                foreach (var m in grid.Modules)
                {
                    if (m == null || !m.CanUpgrade) continue;
                    var d = m.Definition;
                    if (!counts.ContainsKey(d)) { defs.Add(d); counts[d] = 0; lowest[d] = m.UpgradeLevel; }
                    counts[d]++;
                    lowest[d] = Mathf.Min(lowest[d], m.UpgradeLevel);
                }
            }
            const int MaxRows = 4;
            for (int i = 0; i < defs.Count; i++)
            {
                if (i == MaxRows) { Row(sb, "…", $"외 {defs.Count - MaxRows}종"); break; }
                var d = defs[i];
                int lo = lowest[d];
                string step = lo <= 0 ? "기본 → 강화 I" : $"강화 {ModuleUpgrades.Roman(lo)} → {ModuleUpgrades.Roman(lo + 1)}";
                Row(sb, counts[d] > 1 ? $"{d.DisplayName} ×{counts[d]}" : d.DisplayName, $"<color={Good}>{step}</color>");
            }
            Row(sb, "적용", "즉시 · 대상 선택");
            Row(sb, "공간", $"<color={Good}>추가 사용 없음</color>");
            return sb.ToString().TrimEnd();
        }

        /// <summary>두 강화 단계 사이에서 바뀌는 핵심 수치("피해 2 → 2.5"), 최대 maxLines줄.</summary>
        public static string UpgradeDiff(ModuleDefinition def, int fromLevel, int toLevel, int maxLines, bool aligned = false)
        {
            var a = ModuleUpgrades.Compute(def, fromLevel);
            var b = ModuleUpgrades.Compute(def, toLevel);
            var sb = new StringBuilder();
            int lines = 0;
            void Line(string label, float x, float y, string fmt, string unit = "")
            {
                if (lines >= maxLines || Mathf.Approximately(x, y)) return;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append($"{label}{(aligned ? ValueColumn : " ")}{x.ToString(fmt)}{unit} → <color={Good}>{y.ToString(fmt)}{unit}</color>");
                lines++;
            }
            Line("피해", a.Damage, b.Damage, "0.##");
            Line("탄창", a.MagazineCapacity, b.MagazineCapacity, "0");
            Line("발사 간격", a.ReloadTime, b.ReloadTime, "0.###", "초");
            Line("선회", a.TurretTurnRate, b.TurretTurnRate, "0", "°/초");
            Line("재장전", a.AmmoReloadTime, b.AmmoReloadTime, "0.#", "초");
            Line("사거리", a.Range, b.Range, "0.#");
            Line("지원 발사 간격", a.ReloadBonus * 100f, b.ReloadBonus * 100f, "0.#", "%");
            Line("지원 탄약 용량", a.AmmoCapacityBonus * 100f, b.AmmoCapacityBonus * 100f, "0.#", "%");
            Line("지원 보급 속도", a.ResupplyBonus * 100f, b.ResupplyBonus * 100f, "0.#", "%");
            Line("유폭 피해", a.CookOffDamage, b.CookOffDamage, "0");
            return sb.ToString();
        }

        // ------------------------------------------------------------ 도구

        private static void Row(StringBuilder sb, string label, string value)
            => sb.AppendLine($"<color={Muted}>{label}</color>{ValueColumn}{value}");

        /// <summary>탄약: 보유 탄과 보급(또는 전량 재장전). 데이터가 없으면(0) 표시하지 않는다.</summary>
        private static void Ammo(StringBuilder sb, ModuleStats s)
        {
            if (s.MagazineCapacity <= 0) return;
            string unit = s.AmmoFamily == AmmoFamily.Gun ? "발" : "기";
            string resupply = s.AmmoReloadAmount > 0
                ? $"{s.AmmoReloadTime:0.#}초마다 +{s.AmmoReloadAmount}"
                : $"비면 {s.AmmoReloadTime:0.#}초 재장전";
            Row(sb, "탄약", $"{s.MagazineCapacity}{unit}  <color={Muted}>{resupply}</color>");
        }

        /// <summary>표적 상성: 효율이 높은(1 이상)·낮은(0.6 미만) 분류만. 모두 1이면 생략.</summary>
        private static void Efficiency(StringBuilder sb, ModuleDefinition def)
        {
            var e = def.TargetEfficiency;
            var entries = new (string name, float v)[]
            {
                ("소형함", e.SmallSurface), ("중형함", e.MediumSurface), ("대형함", e.LargeSurface), ("보스", e.Boss),
                ("잠수함", e.Submarine), ("드론", e.Drone), ("항공기", e.Air), ("미사일", e.Missile),
            };
            var strong = new StringBuilder();
            var weak = new StringBuilder();
            bool allNeutral = true;
            foreach (var (name, v) in entries)
            {
                if (!Mathf.Approximately(v, 1f)) allNeutral = false;
                if (v >= 1f) strong.Append(strong.Length > 0 ? " · " : "").Append(name);
                else if (v > 0f && v < 0.6f) weak.Append(weak.Length > 0 ? " · " : "").Append(name);
            }
            if (allNeutral) return;
            if (strong.Length > 0) Row(sb, $"<color={Good}>강함</color>", strong.ToString());
            if (weak.Length > 0) Row(sb, $"<color={Warn}>약함</color>", weak.ToString());
        }

        /// <summary>높이는 이웃 무기의 사격각을 가리는지를 뜻한다.</summary>
        private static string HeightLabel(ModuleHeight h) => h switch
        {
            ModuleHeight.Low => $"<color={Good}>낮음</color>",
            ModuleHeight.High => $"<color={Warn}>높음</color>",
            _ => "중간",
        };

        public static string Category(ModuleType t) => t switch
        {
            ModuleType.Autocannon => "근거리 무장",
            ModuleType.NavalGun or ModuleType.GuidedRocket => "중거리 무장",
            ModuleType.Vls => "장거리 무장",
            ModuleType.Ciws or ModuleType.SamLauncher => "방공",
            ModuleType.AswLauncher or ModuleType.Sonar or ModuleType.HelicopterDeck => "대잠",
            ModuleType.Radar => "센서",
            ModuleType.DecoyLauncher or ModuleType.EwSuite => "기만·전자전",
            ModuleType.Magazine => "보급",
            ModuleType.RepairBay => "손상 통제",
            ModuleType.FleetRelay => "편대 지원",
            ModuleType.TurboIntake => "기관 보조",
            ModuleType.FireControlArray => "사격 통제",
            ModuleType.MissileLogistics => "미사일 보급",
            _ => "함체",
        };

        private static string CategoryColor(ModuleType t) => t switch
        {
            ModuleType.Autocannon => "#e0956a",
            ModuleType.NavalGun or ModuleType.GuidedRocket => "#e0c05a",
            ModuleType.Vls => "#7fb8e0",
            ModuleType.Ciws or ModuleType.SamLauncher => "#8fd0ff",
            ModuleType.AswLauncher or ModuleType.Sonar or ModuleType.HelicopterDeck => "#68b8ff",
            ModuleType.DecoyLauncher or ModuleType.EwSuite => "#d09aff",
            ModuleType.RepairBay => "#7fd08a",
            ModuleType.FleetRelay => "#62cadc",
            ModuleType.TurboIntake => "#e0956a",
            ModuleType.FireControlArray => "#8fd0ff",
            ModuleType.MissileLogistics => "#7fb8e0",
            _ => "#c8d4dc",
        };

        // ------------------------------------------------------------ 성장 카드

        /// <summary>성장 카드 위쪽 분류 줄: "성장 카드 · 희귀 · 모든 무장".</summary>
        public static string BuildGrowthTag(RunStatDefinition def, CardTier tier, bool boss)
        {
            if (def == null) return string.Empty;
            string color = ColorUtility.ToHtmlStringRGB(RunUpgrades.TierColor(tier));
            string bossTag = boss ? "  ·  <color=#ffd060>보스 격침 보상</color>" : "";
            return $"성장 카드  ·  <color=#{color}>{RunUpgrades.TierName(tier)}</color>  ·  {def.Scope}{bossTag}";
        }

        /// <summary>성장 카드 그림 자리의 큰 글자: "+10%" 와 능력 이름.</summary>
        public static string BuildGrowthIconLabel(RunStatDefinition def, CardTier tier)
        {
            if (def == null) return string.Empty;
            string color = ColorUtility.ToHtmlStringRGB(RunUpgrades.TierColor(tier));
            return $"<size=190%><b>+{def.ValueOf(tier) * 100f:0.#}%</b></size>\n<color=#{color}><b>{def.Name}</b></color>";
        }

        /// <summary>성장 카드 본문: 효과 한 줄 + 이번 카드 · 누적 변화 · 대상 · 공간.</summary>
        public static string BuildGrowthStats(RunStatDefinition def, CardTier tier)
        {
            if (def == null) return string.Empty;
            var sb = new StringBuilder();
            float add = def.ValueOf(tier);
            float cur = RunUpgrades.Get(def.Stat);
            string tierColor = ColorUtility.ToHtmlStringRGB(RunUpgrades.TierColor(tier));
            sb.AppendLine($"<color={Muted}>{def.Effect}</color>");
            Row(sb, "이번 카드", $"<color=#{tierColor}>+{add * 100f:0.#}%</color>  ({RunUpgrades.TierName(tier)})");
            Row(sb, "누적", cur > 0f
                ? $"{RunUpgrades.Describe(def.Stat, cur, true)} → <color={Good}>{RunUpgrades.Describe(def.Stat, cur + add, true)}</color>"
                : $"없음 → <color={Good}>{RunUpgrades.Describe(def.Stat, add, true)}</color>");
            Row(sb, "대상", def.Scope);
            Row(sb, "적용", "즉시 · 이번 출격 내내");
            Row(sb, "공간", $"<color={Good}>배치 공간 불필요</color>");
            return sb.ToString().TrimEnd();
        }

        /// <summary>지금까지 쌓은 성장 카드 요약(현황판·정비 화면). 없으면 빈 문자열.</summary>
        public static string BuildGrowthSummary(string separator = "  ·  ")
        {
            var sb = new StringBuilder();
            foreach (var def in RunUpgrades.Catalog)
            {
                float v = RunUpgrades.Get(def.Stat);
                if (v <= 0f) continue;
                if (sb.Length > 0) sb.Append(separator);
                sb.Append($"{def.Name} {RunUpgrades.Describe(def.Stat, v)}");
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------ 편대 카드

        /// <summary>편대 배치 카드 본문: 고속정 1척 합류.</summary>
        public static string BuildFleetDeploy(int countAfter)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<color={Muted}>고른 편대 슬롯(1~4)에 고속정 1척이 합류한다</color>");
            Row(sb, "편대", $"{countAfter - 1} → <color={Good}>{countAfter}</color>척 (최대 {Game.TaskForce.TaskForceEscortFormation.MaxEscorts})");
            Row(sb, "무장", $"소형 함포 {Game.TaskForce.EscortDefense.GunRange:0}m · 자동");
            Row(sb, "선체", $"{Game.TaskForce.TaskForceEscortFormation.MaxHullFor(0):0} · 손실 {Game.TaskForce.TaskForceEscortFormation.RecoverSeconds:0}초 뒤 복귀");
            Row(sb, "슬롯", "빈 슬롯 중 선택 · 슬롯마다 진형 자리 고정");
            Row(sb, "역할", "편대 강화 카드로");
            Row(sb, "공간", $"<color={Good}>배치 공간 불필요</color>");
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// 편대 강화 카드 본문(2026-10-03 — 대상은 고른 뒤 슬롯 화면에서): 슬롯마다 지금 함과 강화하면 무엇이 되는지.
        /// </summary>
        public static string BuildFleetUpgradeAny(Game.TaskForce.TaskForceEscortFormation fleet)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<color={Muted}>고르면 편대 슬롯에서 강화할 호위함을 고른다</color>");
            int max = Game.TaskForce.TaskForceEscortFormation.MaxEscorts;
            for (int s = 0; s < max; s++)
            {
                int index = fleet != null ? fleet.IndexOfRosterSlot(s) : -1;
                if (index < 0) { Row(sb, $"{s + 1}번", $"<color={Muted}>비어 있음</color>"); continue; }
                var info = fleet.GetInfo(index);
                string hex = ColorUtility.ToHtmlStringRGB(Game.TaskForce.TaskForceEscortFormation.RoleColor(info.Role));
                string what = info.Role == Game.TaskForce.EscortRole.None ? $"<color={Good}>역할 지정</color>"
                    : info.Tier >= Game.TaskForce.TaskForceEscortFormation.MaxUpgradeLevel ? $"<color={Muted}>최대 개량</color>"
                    : $"개량 {info.Tier} → <color={Good}>{info.Tier + 1}</color>";
                Row(sb, $"{s + 1}번 <color=#{hex}>{Game.TaskForce.TaskForceEscortFormation.RoleCode(info.Role)}</color>", what);
            }
            Row(sb, "공간", $"<color={Good}>배치 공간 불필요</color>");
            return sb.ToString().TrimEnd();
        }

        /// <summary>편대 강화(역할 지정) 카드 본문: 고를 수 있는 역할 넷.</summary>
        public static string BuildFleetRoleChoice()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<color={Muted}>고르면 역할 넷 중 하나를 정한다</color>");
            foreach (var role in Game.TaskForce.TaskForceEscortFormation.Roles)
            {
                string hex = ColorUtility.ToHtmlStringRGB(Game.TaskForce.TaskForceEscortFormation.RoleColor(role));
                Row(sb, $"<color=#{hex}>{Game.TaskForce.TaskForceEscortFormation.RoleCode(role)}</color>", Game.TaskForce.TaskForceEscortFormation.RoleSummary(role));
            }
            Row(sb, "공간", $"<color={Good}>배치 공간 불필요</color>");
            return sb.ToString().TrimEnd();
        }

        /// <summary>편대 강화(개량) 카드 본문: 단계 · 효과 · 선체 · 공간.</summary>
        public static string BuildFleetUpgrade(Game.TaskForce.EscortRole role, int fromTier)
        {
            var sb = new StringBuilder();
            int to = fromTier + 1;
            sb.AppendLine($"<color={Muted}>{Game.TaskForce.TaskForceEscortFormation.TierDescription(role, to)}</color>");
            Row(sb, "개량 단계", $"{fromTier} → <color={Good}>{to}</color> (최대 {Game.TaskForce.TaskForceEscortFormation.MaxUpgradeLevel})");
            Row(sb, "선체", $"{Game.TaskForce.TaskForceEscortFormation.MaxHullFor(fromTier):0} → <color={Good}>{Game.TaskForce.TaskForceEscortFormation.MaxHullFor(to):0}</color>");
            Row(sb, "적용", "즉시 · 함체 확대");
            Row(sb, "공간", $"<color={Good}>배치 공간 불필요</color>");
            return sb.ToString().TrimEnd();
        }

        /// <summary>한 줄 역할 요약.</summary>
        private static string Summary(ModuleType t) => t switch
        {
            ModuleType.Autocannon => "대함·대공 속사 — 가까운 고속정·드론을 먼저",
            ModuleType.NavalGun => "고폭 파편 · 예측 사격 · 대공 겸용",
            ModuleType.GuidedRocket => "유도 타격 — 중거리 수상 표적",
            ModuleType.Vls => "대함·대공·대잠 탄종을 콘솔에서 선택",
            ModuleType.SamLauncher => "적 미사일·항공기를 멀리서 요격",
            ModuleType.Ciws => "최후 방어 — 미사일 우선, 다음 항공기",
            ModuleType.AswLauncher => "소나 접촉·어뢰 흔적에 폭뢰 투하",
            ModuleType.Radar => "탐지 거리와 동시 추적 수 증가",
            ModuleType.Sonar => "잠항 잠수함 탐지",
            ModuleType.DecoyLauncher => "유도탄을 끌어내는 기만체와 연막",
            ModuleType.EwSuite => "반경 안 미사일 유도를 교란",
            ModuleType.Magazine => "가까운 포의 발사·보급을 빠르게 (파괴 시 유폭)",
            ModuleType.RepairBay => "선체·모듈을 서서히 수리",
            ModuleType.HelicopterDeck => "대잠 헬기 자동 출격",
            ModuleType.FleetRelay => "편대 통신을 중계해 호위함 자율 무장을 빠르게",
            ModuleType.TurboIntake => "기관 보조 — 빠르게 항해할 때 실탄 화력 증가",
            ModuleType.FireControlArray => "사격 통제 — 동시 추적과 유도 무장 사거리 증가",
            ModuleType.MissileLogistics => "미사일 발사와 셀 보급 대기 시간을 단축",
            _ => "",
        };

        /// <summary>전투 중 현황판의 지원 장비 효과. 파괴되면 표시하지 않는다.</summary>
        public static string BuildSupportStatus(ModuleInstance module)
        {
            if (module == null || !module.IsOperational || module.Definition == null) return "";
            var s = module.Runtime != null ? module.Runtime.Stats : module.EffectiveStats;
            return module.Definition.Type switch
            {
                ModuleType.FleetRelay => $"편대 +{s.EscortFireRateBonus * 100f:0}%",
                ModuleType.TurboIntake => $"속력 +{s.SpeedBonus * 100f:0}%",
                ModuleType.FireControlArray => $"추적 +{s.ExtraTrackedTargets}",
                ModuleType.MissileLogistics => $"발사 −{s.MissileReloadReduction * 100f:0}%",
                _ => "",
            };
        }
    }
}

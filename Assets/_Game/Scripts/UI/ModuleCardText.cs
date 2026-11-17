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
                case ModuleType.Howitzer:
                    Row(sb, "교전 거리", $"{s.MinRange:0} ~ {s.Range:0} m");
                    Row(sb, "피해", $"{s.Damage:0.#} · 착탄 반경 4 m (가장자리 50%)");
                    Row(sb, "발사 간격", $"{s.ReloadTime:0.0#} 초 · 착탄까지 1.4~3 초");
                    Ammo(sb, s);
                    break;
                case ModuleType.Nobong:
                    Row(sb, "교전 거리", $"수상 {s.MinRange:0} ~ {s.Range:0} m · 대공 {s.Range * 0.7f:0} m");
                    Row(sb, "피해", $"{s.Damage:0.#} × 2 (쌍열) · 근접신관 파편 1.2 m");
                    Row(sb, "발사 간격", $"{s.ReloadTime:0.0#} 초 · 4회 쏘고 0.5 초 쉼");
                    Ammo(sb, s);
                    break;
                case ModuleType.RamBow:
                    Row(sb, "충돌 피해", $"{s.Damage:0} × 속력 (최고 속력 20% 이상)");
                    Row(sb, "충돌 구역", "블록 앞 2.4 m · 같은 적은 1초에 한 번");
                    break;
                case ModuleType.MineRail:
                    Row(sb, "기뢰 피해", $"{s.Damage:0} · 폭발 반경 3 m (가장자리 50%)");
                    Row(sb, "투하", $"뒤쪽 30 m 안 추격하는 적 · {s.ReloadTime:0.#} 초 간격 · 35 초 뒤 가라앉음");
                    Ammo(sb, s);
                    break;
                case ModuleType.TorpedoTube:
                    Row(sb, "대잠 사거리", $"{s.MinRange:0} ~ {s.Range:0} m");
                    Row(sb, "피해", $"{s.Damage:0} × 3 (부채꼴)");
                    Row(sb, "발사 간격", $"{s.ReloadTime:0.0} 초");
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
                case ModuleType.RepairBay:   // 2026-10-11: 선체 회복·응급 수리는 함교가 — 이 블록은 함교를 키운다
                    Row(sb, "선체 자동 회복", $"<color={Good}>상한 +17%p · 속도 +50%</color>");
                    Row(sb, "<color=" + Key + ">[R]</color> 응급 수리", $"<color={Good}>+1회</color>");
                    Row(sb, "모듈 수리", $"{s.ModuleRepairPerSecond:0.0} /초 (3칸)");
                    Row(sb, "피해 감소", $"{s.DamageReduction * 100f:0}%");
                    break;
                case ModuleType.HelicopterDeck:
                    Row(sb, "출격 간격", $"{s.SortieCooldown:0} 초");
                    Row(sb, "우선 표적", "잠수함 → 수상함");
                    break;
                case ModuleType.Bridge:
                    Row(sb, "탐지 거리", $"{s.DetectionRange:0} m");
                    Row(sb, "선체 회복", $"{s.HullRepairPerSecond:0.0} /초 (50%까지)");
                    Row(sb, "<color=" + Key + ">[R]</color> 응급 수리", "1회 · 스테이지마다 다시");
                    if (def.Id != null && def.Id.StartsWith(Game.Core.Ruleset.Ww2Prefix))
                        Row(sb, "<color=" + Key + ">[F]</color> 연막", "함교 연막 · 기만체 없음(WWII)");
                    break;
                // WWII(패치 B 2026-10-17)
                case ModuleType.Ww2DualPurposeGun:
                    Row(sb, "교전 거리", $"수상 {s.MinRange:0} ~ {s.Range:0} m · 대공 {s.Range * Game.WW2.Ww2DualPurposeGunModule.AirRangeRatio:0} m");
                    Row(sb, "피해", $"{s.Damage:0.#} (항공기에는 {def.TargetEfficiency.Air * 100f:0}%)");
                    Row(sb, "발사 간격", $"{s.ReloadTime:0.0#} 초 · 선회 {s.TurretTurnRate:0}°/초 · 탄속 {s.ProjectileSpeed:0} m/초");
                    Ammo(sb, s);
                    break;
                case ModuleType.Ww2HeavyGun:
                    Row(sb, "교전 거리", $"수상 {s.MinRange:0} ~ {s.Range:0} m · 항공기는 쏘지 않음");
                    Row(sb, "피해", $"{s.Damage:0.#} × 연장 동시 발사 · 큰 배에 강하고 작은 배에 약함");
                    Row(sb, "발사 간격", $"{s.ReloadTime:0.0#} 초 · 선회 {s.TurretTurnRate:0}°/초 · 탄속 {s.ProjectileSpeed:0} m/초");
                    Row(sb, "<color=" + Key + $">[{Game.Core.Ww2Controls.BindingLabel(Game.Core.Ww2Control.MainSalvo)}]</color> 주포 일제", "누르고 겨눔 · 떼면 준비된 주포가 함께(탄착 좁게)");
                    Ammo(sb, s);
                    break;
                case ModuleType.Ww2QuadAa:
                    Row(sb, "교전 거리", $"대공 {s.Range:0} m · 경수상 {s.Range * Game.WW2.Ww2QuadAaModule.SurfaceRangeRatio:0} m");
                    Row(sb, "피해", $"{s.Damage:0.#} × 4연장 번갈아 · 근접신관 없음(직격)");
                    Row(sb, "발사 간격", $"{s.ReloadTime:0.0#} 초 · {Game.WW2.Ww2QuadAaModule.BurstRounds}발 쏘고 {Game.WW2.Ww2QuadAaModule.BurstPause:0.#} 초 쉼");
                    Ammo(sb, s);
                    break;
                case ModuleType.Ww2BroadsideTorpedo:
                    Row(sb, "<color=" + Key + ">[수동]</color> 일제 발사", $"{s.MagazineCapacity}발 · 좌/우 현측 정횡 ±{s.FireArcDegrees * 0.5f:0}°");
                    Row(sb, "어뢰", $"피해 {s.Damage:0} · 직진 {s.Range:0} m · {s.ProjectileSpeed:0} m/초 · 유도 없음");
                    Row(sb, "재장전", $"{s.AmmoReloadTime:0} 초 (전량) · 발사 후 {s.MinRange:0} m부터 작동");
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
                PlacementZone.SideOnly => "좌현이나 우현이 트인 자리",
                _ => null,
            };
            // 자리로 형태가 바뀌는 블록(소나·폭뢰)
            string variants = ModuleVariants.PlacementHint(def.Type);
            if (!string.IsNullOrEmpty(variants)) Row(sb, $"<color={Key}>형태</color>", $"<color={Key}>{variants}</color>");
            if (zone != null) Row(sb, $"<color={Key}>배치</color>", $"<color={Key}>{zone}</color>");
            return sb.ToString().TrimEnd();
        }

        // ------------------------------------------------------------ 간단한 무장 카드(2026-11-17)

        private const string PipOn = "#7fd08a", PipOff = "#2c3a46";

        /// <summary>정비 카드에서 간단한 무장 카드(등급 · 공격력 · 탄약 · 사거리)로 보여 주는 블록.</summary>
        public static bool IsSimpleWeapon(ModuleType t) => t is ModuleType.Autocannon or ModuleType.Nobong or ModuleType.NavalGun
            or ModuleType.Howitzer or ModuleType.GuidedRocket or ModuleType.Vls or ModuleType.SamLauncher or ModuleType.Ciws
            or ModuleType.AswLauncher or ModuleType.TorpedoTube
            or ModuleType.Ww2DualPurposeGun or ModuleType.Ww2HeavyGun or ModuleType.Ww2QuadAa or ModuleType.Ww2BroadsideTorpedo;

        /// <summary>정비 카드 분류 줄(간단): "근거리 무장 · 1×1".</summary>
        public static string BuildSimpleTag(ModuleDefinition def)
            => def == null ? string.Empty : $"<color={CategoryColor(def.Type)}>{Category(def.Type)}</color>  ·  {def.Width}×{def.Height}";

        /// <summary>
        /// 무장 카드 본문(2026-11-17 — 설명을 줄였다): 대공 · 대함 · 대잠 등급(초록 칸 5개) · 공격력 · 탄약 · 사거리(근 · 중 · 장).
        /// 등급은 표적 효율 평균 × 5(대공 = 드론·항공기·미사일, 대함 = 소형·중형·대형함, 대잠 = 잠수함). 0.6 아래인 분야는 줄을 뺀다.
        /// 대잠 줄은 잠항한 잠수함을 잡는 무장에만 — 부상한 잠수함은 대함으로 맞는다(TargetEfficiency.Against).
        /// 자세한 수치(발사 간격 · 보급 · 상성)는 도감(BuildStats)에 남긴다.
        /// </summary>
        public static string BuildWeaponCard(ModuleDefinition def)
        {
            if (def == null) return string.Empty;
            var sb = new StringBuilder();
            foreach (var (name, value) in Ratings(def))
                sb.AppendLine($"<b>{name}</b><pos=26%>{Pips(value)}");
            var s = def.Stats;
            sb.AppendLine();
            sb.AppendLine($"<size=78%><color={Muted}>공격력<pos=36%>탄약<pos=66%>사거리</color></size>");
            string ammo = s.MagazineCapacity > 0 ? $"{s.MagazineCapacity}" : "—";
            sb.AppendLine($"<size=120%><b>{DamageText(def)}<pos=36%>{ammo}<pos=66%></b>{RangeBand(s.Range)}</size>");

            string zone = def.Placement.Zone switch
            {
                PlacementZone.BowOnly => "뱃머리만",
                PlacementZone.SternOnly => "선미만",
                PlacementZone.SideOrStern => "옆이나 뒤가 트인 자리",
                PlacementZone.SideOnly => "좌현이나 우현이 트인 자리",
                _ => null,
            };
            if (zone != null) sb.AppendLine($"<size=80%><color={Key}>배치  {zone}</color></size>");
            return sb.ToString().TrimEnd();
        }

        /// <summary>카드에 보일 등급 줄(이름, 효율 0~). 검증에서도 쓴다.</summary>
        public static List<(string name, float value)> Ratings(ModuleDefinition def)
        {
            var list = new List<(string, float)>();
            if (def == null) return list;
            var e = def.TargetEfficiency;
            float air = (e.Drone + e.Air + e.Missile) / 3f;
            if (def.Type == ModuleType.Vls) air = Game.Modules.Runtime.VlsModule.AirPayloadDamageRatio;   // 대공 탄은 효율 대신 피해 45%
            float surface = (e.SmallSurface + e.MediumSurface + e.LargeSurface) / 3f;
            bool hunter = def.Type is ModuleType.AswLauncher or ModuleType.TorpedoTube or ModuleType.Vls or ModuleType.HelicopterDeck;
            if (air >= 0.6f || (def.Type == ModuleType.Vls && air > 0f)) list.Add(("대공", air));
            if (surface >= 0.6f) list.Add(("대함", surface));
            if (hunter && e.Submarine >= 0.6f) list.Add(("대잠", e.Submarine));
            return list;
        }

        /// <summary>효율 → 초록 칸 수(1~5).</summary>
        public static int PipCount(float value) => Mathf.Clamp(Mathf.RoundToInt(value * 5f), 1, 5);

        private static string Pips(float value)
        {
            int n = PipCount(value);
            return $"<color={PipOn}>{new string('■', n)}</color><color={PipOff}>{new string('■', 5 - n)}</color>";
        }

        /// <summary>사거리 단계: 30m 미만 근 · 40m까지 중 · 그 너머 장.</summary>
        public static string RangeName(float range) => range < 30f ? "근" : range <= 40f ? "중" : "장";

        private static string RangeBand(float range)
        {
            string on = RangeName(range);
            var sb = new StringBuilder();
            foreach (var band in new[] { "근", "중", "장" })
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(band == on ? $"<color={PipOn}><b>{band}</b></color>" : $"<color={PipOff}>{band}</color>");
            }
            return sb.ToString();
        }

        /// <summary>한 번 쏠 때의 피해(쌍열 · 부채꼴처럼 한꺼번에 나가면 ×수).</summary>
        private static string DamageText(ModuleDefinition def)
        {
            float d = def.Stats.Damage;
            string v = d >= 10f ? $"{d:0}" : $"{d:0.##}";
            return def.Type switch
            {
                ModuleType.AswLauncher or ModuleType.Nobong => $"{v}×2",
                ModuleType.TorpedoTube => $"{v}×3",
                _ => v,
            };
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
            // 곡사포: 한 번에 쏘는 포탄 수(강화의 핵심이라 맨 앞)
            var howitzer = def.Prefab != null ? def.Prefab.GetComponent<Game.Modules.Runtime.HowitzerModule>() : null;
            if (howitzer != null) Line("한 번에", howitzer.ShellsAt(fromLevel), howitzer.ShellsAt(toLevel), "0", "발");
            // 헬기데크(2026-10-11): 출격 간격 · 헬기 무장 피해 · 수색 반경(배율은 데크가 헬기에 넘긴다)
            if (def.Type == ModuleType.HelicopterDeck)
            {
                float Mod(int level, System.Func<ModuleStatModifier, float> pick)
                {
                    var stage = level > 0 ? ModuleUpgrades.ProfileFor(def)?.GetStage(level) : null;
                    float v = stage != null ? pick(stage.Modifier) : 1f;
                    return v > 0f ? v : 1f;
                }
                Line("출격 간격", a.SortieCooldown, b.SortieCooldown, "0.#", "초");
                Line("헬기 피해", 100f * Mod(fromLevel, m => m.damageMultiplier), 100f * Mod(toLevel, m => m.damageMultiplier), "0", "%");
                Line("수색 반경", 100f * Mod(fromLevel, m => m.rangeMultiplier), 100f * Mod(toLevel, m => m.rangeMultiplier), "0", "%");
            }
            Line("피해", a.Damage, b.Damage, "0.##");
            Line("내구", def.MaxHp * ModuleUpgrades.MaxHpMultiplier(def, fromLevel), def.MaxHp * ModuleUpgrades.MaxHpMultiplier(def, toLevel), "0");
            if (def.Type == ModuleType.RamBow)
                Line("반동 피해", Game.Modules.Runtime.RamBowModule.BaseSelfDamageRatio * 100f * ModuleUpgrades.SelfDamageMultiplier(def, fromLevel),
                     Game.Modules.Runtime.RamBowModule.BaseSelfDamageRatio * 100f * ModuleUpgrades.SelfDamageMultiplier(def, toLevel), "0", "%");
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
            ModuleType.Autocannon or ModuleType.RamBow or ModuleType.Nobong => "근거리 무장",
            ModuleType.MineRail => "후방 무장",
            ModuleType.NavalGun or ModuleType.GuidedRocket or ModuleType.Howitzer => "중거리 무장",
            ModuleType.Vls => "장거리 무장",
            ModuleType.Ciws or ModuleType.SamLauncher => "방공",
            ModuleType.AswLauncher or ModuleType.TorpedoTube or ModuleType.Sonar or ModuleType.HelicopterDeck => "대잠",
            ModuleType.Radar => "센서",
            ModuleType.DecoyLauncher or ModuleType.EwSuite => "기만·전자전",
            ModuleType.Magazine => "보급",
            ModuleType.RepairBay => "손상 통제",
            ModuleType.FleetRelay => "편대 지원",
            ModuleType.TurboIntake => "기관 보조",
            ModuleType.FireControlArray => "사격 통제",
            ModuleType.MissileLogistics => "미사일 보급",
            ModuleType.Ww2DualPurposeGun => "중거리 무장",
            ModuleType.Ww2HeavyGun => "주포",
            ModuleType.Ww2QuadAa => "방공",
            ModuleType.Ww2BroadsideTorpedo => "대수상 어뢰",
            _ => "함체",
        };

        private static string CategoryColor(ModuleType t) => t switch
        {
            ModuleType.Autocannon or ModuleType.RamBow or ModuleType.MineRail or ModuleType.Nobong => "#e0956a",
            ModuleType.NavalGun or ModuleType.GuidedRocket or ModuleType.Howitzer => "#e0c05a",
            ModuleType.Vls => "#7fb8e0",
            ModuleType.Ciws or ModuleType.SamLauncher => "#8fd0ff",
            ModuleType.AswLauncher or ModuleType.TorpedoTube or ModuleType.Sonar or ModuleType.HelicopterDeck => "#68b8ff",
            ModuleType.DecoyLauncher or ModuleType.EwSuite => "#d09aff",
            ModuleType.RepairBay => "#7fd08a",
            ModuleType.FleetRelay => "#62cadc",
            ModuleType.TurboIntake => "#e0956a",
            ModuleType.FireControlArray => "#8fd0ff",
            ModuleType.MissileLogistics => "#7fb8e0",
            ModuleType.Ww2DualPurposeGun => "#e0c05a",
            ModuleType.Ww2HeavyGun => "#e0c05a",
            ModuleType.Ww2QuadAa => "#8fd0ff",
            ModuleType.Ww2BroadsideTorpedo => "#e0956a",
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

        /// <summary>
        /// 성장 카드 본문(2026-11-14 — 실제 수치): 효과 한 줄 · 이번 카드 · 누적 · <b>함선 변화</b>(지금 함선의 실제 값 → 고른 뒤 값 —
        /// 무장마다 한 발 피해·발사 간격·장탄·사거리, 스킬마다 재사용 시간, 선체 최대·회복, 속력, 받는 피해, 수리·탐지) · 적용.
        /// 고른 뒤 값은 RunUpgrades.Preview 범위 안에서 같은 계산식(ModuleUpgrades.Compute)으로 구한다. ship이 없으면 비율만.
        /// </summary>
        public static string BuildGrowthStats(RunStatDefinition def, CardTier tier, Game.Ship.ShipController ship = null)
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

            var rows = GrowthChanges(def.Stat, add, ship, out string what, out int more);
            if (rows.Count > 0)
            {
                sb.AppendLine($"<color={Key}>함선 변화</color><color={Muted}> · {what}</color>");
                foreach (var (label, before, after) in rows)
                    sb.AppendLine($"  {label}{ChangeColumn}{before} → <color={Good}>{after}</color>");
                if (more > 0) sb.AppendLine($"<color={Muted}>  외 {more}종</color>");
            }
            else
            {
                Row(sb, "대상", def.Scope);
                if (ship != null && def.Stat is RunStat.Damage or RunStat.GunDamage or RunStat.MissileDamage or RunStat.FireRate
                        or RunStat.AirDefenseRate or RunStat.Magazine or RunStat.Range or RunStat.SkillRate or RunStat.Repair)
                    sb.AppendLine($"<color={Warn}>지금 함선에는 바뀌는 장비 없음 — 나중에 설치하면 적용</color>");
            }
            return sb.ToString().TrimEnd();
        }

        // 함선 변화 줄은 이름이 길어(무장 이름 · 강화 단계 · 수량) 값 열을 조금 오른쪽에
        private const string ChangeColumn = "<pos=54%>";
        private const int MaxChangeRows = 4;

        /// <summary>성장 카드를 고르면 지금 함선에서 바뀌는 실제 값들(이름, 지금, 고른 뒤). what = 무슨 값인지("한 발 피해"), more = 줄 수를 넘어 숨긴 종류 수.</summary>
        public static List<(string label, string before, string after)> GrowthChanges(RunStat stat, float add, Game.Ship.ShipController ship, out string what, out int more)
        {
            var rows = new List<(string, string, string)>();
            what = "";
            more = 0;
            if (ship == null || add <= 0f) return rows;
            float cur = 1f + RunUpgrades.Get(stat), next = cur + add;   // 선형 배율(1 + 누적) — 함선 쪽 값은 비율로
            var grid = ship.Grid;
            var systems = ship.Systems;
            switch (stat)
            {
                case RunStat.Damage: case RunStat.GunDamage: case RunStat.MissileDamage:
                    what = "한 발 피해";
                    WeaponChanges(stat, add, grid, rows, ref more);
                    break;
                case RunStat.FireRate: case RunStat.AirDefenseRate:
                    what = "발사 간격";
                    WeaponChanges(stat, add, grid, rows, ref more);
                    break;
                case RunStat.Magazine:
                    what = "장탄";
                    WeaponChanges(stat, add, grid, rows, ref more);
                    break;
                case RunStat.Range:
                    what = "사거리";
                    WeaponChanges(stat, add, grid, rows, ref more);
                    break;
                case RunStat.SkillRate:
                    what = "재사용 시간";
                    SkillChanges(ship, cur, next, rows, ref more);
                    break;
                case RunStat.Speed:
                {
                    what = "최고 속력";
                    float v = Game.Ship.ShipController.ToKnots(ship.BaseMaxSpeed);
                    rows.Add(("최고 속력", $"{v:0.0}kn", $"{v * next / cur:0.0}kn"));
                    break;
                }
                case RunStat.HullMax:
                {
                    what = "선체";
                    float max = ship.HullMaxHp, after = max * next / cur;
                    rows.Add(("선체 최대", $"{max:0}", $"{after:0}"));
                    rows.Add(("지금 선체(바로 회복)", $"{ship.HullHp:0}", $"{Mathf.Min(after, ship.HullHp + after - max):0}"));
                    break;
                }
                case RunStat.Defense:
                    what = "받는 피해";
                    rows.Add(("적 공격 100당", $"{100f / cur:0.#}", $"{100f / next:0.#}"));
                    break;
                case RunStat.Repair:
                    what = "수리 속도";
                    RepairChanges(grid, cur, next, rows);
                    break;
                case RunStat.Detection:
                    what = "탐지 거리";
                    if (systems != null)
                    {
                        rows.Add(("레이더", $"{systems.DetectionRange:0}m", $"{systems.DetectionRange * next / cur:0}m"));
                        if (systems.SonarCount > 0) rows.Add(("소나", $"{systems.SonarRange:0}m", $"{systems.SonarRange * next / cur:0}m"));
                    }
                    break;
            }
            return rows;
        }

        /// <summary>무장 줄: 같은 블록·같은 강화 단계는 한 줄(×수량). 지금 값은 실제 무장 수치(함선 지원·탄약고 보너스 포함), 고른 뒤는 같은 비율·차이로.</summary>
        private static void WeaponChanges(RunStat stat, float add, Game.Ship.ShipGrid grid, List<(string, string, string)> rows, ref int more)
        {
            if (grid == null) return;
            var keys = new List<(ModuleDefinition def, int level)>();
            var counts = new List<int>();
            var lines = new List<(string before, string after)>();
            foreach (var m in grid.Modules)
            {
                var def = m?.Definition;
                if (def == null || m.Runtime == null || !RunUpgrades.IsWeapon(def.Type)) continue;
                int k = keys.IndexOf((def, m.UpgradeLevel));
                if (k >= 0) { counts[k]++; continue; }

                var before = ModuleUpgrades.Compute(def, m.UpgradeLevel);
                ModuleStats after;
                using (RunUpgrades.Preview(stat, add)) after = ModuleUpgrades.Compute(def, m.UpgradeLevel);
                var live = m.Runtime.Stats;
                (string, string)? line = null;
                switch (stat)
                {
                    case RunStat.Damage: case RunStat.GunDamage: case RunStat.MissileDamage:
                        if (before.Damage > 0f && after.Damage > before.Damage + 1e-4f)
                            line = (Num(live.Damage), Num(live.Damage * after.Damage / before.Damage));
                        break;
                    case RunStat.FireRate: case RunStat.AirDefenseRate:
                        if (before.ReloadTime > 0f && after.ReloadTime < before.ReloadTime - 1e-5f)
                            line = (Seconds(live.ReloadTime), Seconds(live.ReloadTime * after.ReloadTime / before.ReloadTime));
                        break;
                    case RunStat.Magazine:
                        if (before.MagazineCapacity > 0 && after.MagazineCapacity != before.MagazineCapacity)
                            line = ($"{live.MagazineCapacity}발", $"{live.MagazineCapacity + after.MagazineCapacity - before.MagazineCapacity}발");
                        break;
                    case RunStat.Range:
                        if (before.Range > 0f && after.Range > before.Range + 1e-4f)
                            line = ($"{live.Range:0}m", $"{live.Range * after.Range / before.Range:0}m");
                        break;
                }
                if (line == null) continue;
                keys.Add((def, m.UpgradeLevel));
                counts.Add(1);
                lines.Add(line.Value);
            }
            for (int i = 0; i < keys.Count; i++)
            {
                if (rows.Count >= MaxChangeRows) { more++; continue; }
                string level = keys[i].level > 0 ? " " + ModuleUpgrades.Roman(keys[i].level) : "";
                string count = counts[i] > 1 ? $" ×{counts[i]}" : "";
                rows.Add(($"{keys[i].def.DisplayName}{level}{count}", lines[i].before, lines[i].after));
            }
        }

        /// <summary>스킬 줄: 기만체·연막·재밍·전속·응급 수리 충전·고유 스킬의 실제 재사용 시간(타이머가 1+누적 배로 흐른다).</summary>
        private static void SkillChanges(Game.Ship.ShipController ship, float cur, float next, List<(string, string, string)> rows, ref int more)
        {
            var seen = new HashSet<string>();
            var all = new List<(string name, float seconds)>();
            void Add(string name, float seconds)
            {
                if (seconds > 0f && seen.Add(name)) all.Add((name, seconds));
            }
            var sig = ship.GetComponent<Game.Ship.SignatureSkill>();
            if (sig != null && sig.Ability != null) Add(sig.Ability.Title, sig.Ability.Cooldown);
            if (ship.Grid != null)
                foreach (var m in ship.Grid.Modules)
                {
                    if (m?.Runtime == null) continue;
                    if (m.Runtime is Game.Modules.Runtime.DecoyLauncherModule d)
                    {
                        if (d.DecoyEnabled) Add("기만체", Mathf.Max(0.5f, d.Stats.ReloadTime));
                        if (d.CarriesSmoke) Add("연막", d.SmokeCooldownSeconds);
                    }
                    if (m.Runtime is Game.Modules.Runtime.BridgeModule b)
                    {
                        Add("전속", b.FlankCooldownSeconds);
                        Add("응급 수리 1회 충전", b.ChargeRechargeSeconds);
                    }
                    if (m.Runtime is Game.Modules.Runtime.EwSuiteModule e) Add("재밍", Mathf.Max(1f, e.Stats.ReloadTime));
                }
            foreach (var (name, seconds) in all)
            {
                if (rows.Count >= MaxChangeRows) { more++; continue; }
                rows.Add((name, $"{seconds / cur:0.#}초", $"{seconds / next:0.#}초"));
            }
        }

        /// <summary>수리 줄: 함교 선체 자동 회복(손상통제반 수에 따라) · 손상통제반 모듈 수리.</summary>
        private static void RepairChanges(Game.Ship.ShipGrid grid, float cur, float next, List<(string, string, string)> rows)
        {
            if (grid == null) return;
            float moduleRepair = 0f;
            foreach (var m in grid.Modules)
            {
                if (m?.Runtime is Game.Modules.Runtime.BridgeModule b && b.HullRepairRate > 0f && rows.Count == 0)
                    rows.Add(("선체 자동 회복", $"{b.HullRepairRate:0.##}/초", $"{b.HullRepairRate * next / cur:0.##}/초"));
                if (m?.Runtime is Game.Modules.Runtime.RepairBayModule r) moduleRepair += r.Stats.ModuleRepairPerSecond;
            }
            if (moduleRepair > 0f) rows.Add(("모듈 수리(통제반 합)", $"{moduleRepair:0.##}/초", $"{moduleRepair * next / cur:0.##}/초"));
        }

        private static string Num(float v) => v >= 100f ? $"{v:0}" : $"{v:0.#}";
        /// <summary>발사 간격: 1초 아래는 소수 셋째 자리까지(CIWS 0.040 → 0.034처럼 변화가 보이게).</summary>
        private static string Seconds(float v) => v < 1f ? $"{v:0.000}초" : $"{v:0.00}초";

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
            ModuleType.TorpedoTube => "트인 현측으로 경어뢰 3발 부채꼴 — 중거리 대잠",
            ModuleType.Howitzer => "상부 구조물·섬 너머로 쏘는 범위 포격 — 몰린 수상함",
            ModuleType.RamBow => "맨 앞 칸 강화 함수 — 빠르게 나아가며 들이받는다",
            ModuleType.Nobong => "40mm 쌍열 · 근접신관 공중 폭발 — 드론 떼와 고속정",
            ModuleType.MineRail => "선미에서 추격하는 적 앞에 기뢰를 떨어뜨린다",
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
            ModuleType.Ww2DualPurposeGun => "수상함 우선 · 공격해 오는 항공기 보조 — 한 번에 한 표적",
            ModuleType.Ww2HeavyGun => "큰 수상함을 멀리서 — 느리게 돌고 오래 장전, 주포 일제로 한꺼번에",
            ModuleType.Ww2QuadAa => "항공기 우선 · 가까운 경수상함 보조 — 구조물에 가리면 못 쏜다",
            ModuleType.Ww2BroadsideTorpedo => "현측으로 직진 어뢰 일제 발사 — 예측해서 쏘고 오래 재장전",
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

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Game.Core;
using Game.Data;
using Game.Modules;
using Game.Ship;

namespace Game.UI
{
    /// <summary>
    /// 사전 보강(2026-10-11 점검 C01~C04): 역할 필터 분류, 강화 단계 요약, 사거리 막대, 함께 쓰기·대응 무장 링크,
    /// "함정·용어" 탭(시작 함정 4종과 고유 스킬, 탐지·잠수함·사격각·탄약·손상 통제·교리·항로·다층 방어 용어).
    /// 링크는 본문의 &lt;link="항목 id"&gt; — 누르면 그 항목으로 이동한다(CodexUI).
    /// </summary>
    public static partial class CodexCatalog
    {
        private const string LinkColor = "#8fd8ff";

        /// <summary>본문 안 링크(누르면 그 항목으로).</summary>
        public static string Link(string id, string text) => $"<link=\"{id}\"><color={LinkColor}><u>{text}</u></color></link>";

        private static List<ModuleDefinition> s_modules;
        private static List<ModuleDefinition> Modules => s_modules ??= CollectModules();

        /// <summary>그 종류의 블록(사전에 있는 것) — 없으면 null.</summary>
        private static ModuleDefinition DefOf(ModuleType type)
        {
            foreach (var d in Modules) if (d.Type == type) return d;
            return null;
        }

        private static string LinkTo(ModuleType type)
        {
            var d = DefOf(type);
            return d != null ? Link(d.Id, d.DisplayName) : null;
        }

        // ------------------------------------------------------------ 역할 분류

        /// <summary>무장이 맡는 위협.</summary>
        public static CodexRole RolesOf(ModuleType t) => t switch
        {
            ModuleType.Autocannon or ModuleType.Nobong or ModuleType.NavalGun => CodexRole.Surface | CodexRole.Air,
            ModuleType.Howitzer or ModuleType.RamBow or ModuleType.MineRail or ModuleType.GuidedRocket => CodexRole.Surface,
            ModuleType.Vls => CodexRole.Surface | CodexRole.Air,
            ModuleType.SamLauncher or ModuleType.Ciws => CodexRole.Air,
            ModuleType.AswLauncher or ModuleType.TorpedoTube or ModuleType.Sonar => CodexRole.Sub,
            ModuleType.HelicopterDeck => CodexRole.Sub | CodexRole.Surface,
            ModuleType.Radar => CodexRole.Air | CodexRole.Surface | CodexRole.Support,
            ModuleType.DecoyLauncher or ModuleType.EwSuite => CodexRole.Air | CodexRole.Support,
            _ => CodexRole.Support,
        };

        /// <summary>적을 상대하는 데 필요한 역할(미사일을 쏘는 적은 대공도).</summary>
        private static CodexRole CounterRoles(EnemyDefinition def)
        {
            var r = def.Kind switch
            {
                Game.Combat.TargetKind.Submarine => CodexRole.Sub,
                Game.Combat.TargetKind.Aircraft => CodexRole.Air,
                _ => CodexRole.Surface,
            };
            if (def.MissilePrefab != null) r |= CodexRole.Air;
            return r;
        }

        // ------------------------------------------------------------ 사거리 막대

        private static void AddRanges(CodexEntry e, ModuleDefinition def)
        {
            var s = def.Stats;
            switch (def.Type)
            {
                case ModuleType.Autocannon:
                case ModuleType.NavalGun:
                case ModuleType.GuidedRocket:
                case ModuleType.Vls:
                case ModuleType.SamLauncher:
                case ModuleType.Howitzer:
                case ModuleType.TorpedoTube:
                    e.Ranges.Add(new CodexRange("교전", s.MinRange, s.Range));
                    break;
                case ModuleType.Nobong:
                    e.Ranges.Add(new CodexRange("수상", s.MinRange, s.Range));
                    e.Ranges.Add(new CodexRange("대공", 0f, s.Range * 0.7f));
                    break;
                case ModuleType.Ciws: e.Ranges.Add(new CodexRange("요격", 0f, s.Range)); break;
                case ModuleType.AswLauncher: e.Ranges.Add(new CodexRange("대잠", 0f, s.Range)); break;
                case ModuleType.MineRail: e.Ranges.Add(new CodexRange("뒤쪽", 0f, 30f)); break;
                case ModuleType.RamBow: e.Ranges.Add(new CodexRange("앞", 0f, 2.4f)); break;
                case ModuleType.EwSuite: e.Ranges.Add(new CodexRange("재밍", 0f, s.Range)); break;
                case ModuleType.Radar:
                case ModuleType.Bridge: e.Ranges.Add(new CodexRange("탐지", 0f, s.DetectionRange)); break;
                case ModuleType.Sonar: e.Ranges.Add(new CodexRange("잠수함", 0f, s.DetectionRange)); break;
            }
        }

        // ------------------------------------------------------------ 강화 단계 요약

        /// <summary>외형 visual(0 = 기본)을 고를 때 본문 위 요약: 기본이면 안내, 아니면 바로 앞 단계 대비 변화(마지막 외형이 남은 단계를 맡으면 그 단계들까지).</summary>
        private static string StageNote(ModuleDefinition def, int visual, int toLevel, string label)
        {
            if (visual <= 0)
                return $"<color={Key}><b>선택: 기본형</b></color>  <color={Muted}>아래 표가 기본 수치 · 장비 강화 카드로 단계가 오른다</color>";
            var sb = new StringBuilder($"<color={Good}><b>선택: {label}</b></color>  <color={Muted}>(바로 앞 단계 대비)</color>");
            int from = visual - 1;
            for (int l = from; l < Mathf.Max(toLevel, visual); l++)
            {
                if (toLevel - from > 1) sb.Append($"\n<b>{(l == 0 ? "기본" : ModuleUpgrades.Roman(l))} → {ModuleUpgrades.Roman(l + 1)}</b>");
                foreach (var line in ModuleCardText.UpgradeDiff(def, l, l + 1, 6).Split('\n'))
                    if (!string.IsNullOrWhiteSpace(line)) sb.Append("\n   " + line.Trim());
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------ 함께 쓰기 · 대응 무장

        private static readonly Dictionary<ModuleType, (ModuleType with, string why)[]> Synergies = new()
        {
            [ModuleType.Autocannon] = new[] { (ModuleType.Magazine, "2칸 안이면 발사 간격 −25% · 탄 +50%"), (ModuleType.TurboIntake, "항해 중 포 피해 증가"), (ModuleType.Ciws, "근접 방어를 겹친다") },
            [ModuleType.NavalGun] = new[] { (ModuleType.Magazine, "2칸 안이면 발사 간격 −25% · 탄 +50%"), (ModuleType.Radar, "34 m 끝까지 일찍 잡는다"), (ModuleType.TurboIntake, "항해 중 포 피해 증가") },
            [ModuleType.Nobong] = new[] { (ModuleType.Radar, "자체 센서가 없어 레이더 표적만 쏜다"), (ModuleType.Ciws, "드론·항공기 근접 방어") },
            [ModuleType.Howitzer] = new[] { (ModuleType.Radar, "44 m 사거리는 전용 레이더(52 m)가 있어야 다 쓴다"), (ModuleType.NavalGun, "가까운 적은 함포로") },
            [ModuleType.GuidedRocket] = new[] { (ModuleType.FireControlArray, "유도 무장 사거리 증가"), (ModuleType.MissileLogistics, "발사 간격 단축"), (ModuleType.Magazine, "발사 간격만 지원") },
            [ModuleType.Vls] = new[] { (ModuleType.Radar, "48 m 끝까지 쓰려면 전용 레이더"), (ModuleType.FireControlArray, "유도 무장 사거리 증가"), (ModuleType.MissileLogistics, "셀 보급 단축") },
            [ModuleType.SamLauncher] = new[] { (ModuleType.Radar, "멀리서 먼저 잡는다"), (ModuleType.Ciws, "뚫고 온 미사일 최종 방어"), (ModuleType.FireControlArray, "유도 무장 사거리 증가") },
            [ModuleType.Ciws] = new[] { (ModuleType.SamLauncher, "바깥에서 먼저 요격"), (ModuleType.DecoyLauncher, "기만체로 들어오는 수를 줄인다"), (ModuleType.EwSuite, "재밍으로 빗나가게") },
            [ModuleType.AswLauncher] = new[] { (ModuleType.Sonar, "잠수함을 드러내야 쏜다"), (ModuleType.HelicopterDeck, "헬기가 접촉을 확정한다") },
            [ModuleType.TorpedoTube] = new[] { (ModuleType.Sonar, "소나 접촉 쪽으로 쏜다"), (ModuleType.HelicopterDeck, "헬기 접촉 확정") },
            [ModuleType.Sonar] = new[] { (ModuleType.AswLauncher, "드러난 잠수함 타격"), (ModuleType.TorpedoTube, "먼 잠수함 추적 어뢰"), (ModuleType.HelicopterDeck, "넓게 찾기") },
            [ModuleType.HelicopterDeck] = new[] { (ModuleType.Sonar, "함 주변 탐지"), (ModuleType.AswLauncher, "가까운 잠수함 마무리") },
            [ModuleType.Radar] = new[] { (ModuleType.Vls, "사거리 끝까지"), (ModuleType.SamLauncher, "먼 미사일 요격"), (ModuleType.NavalGun, "먼 수상함") },
            [ModuleType.DecoyLauncher] = new[] { (ModuleType.Ciws, "남은 미사일 최종 방어"), (ModuleType.EwSuite, "유인과 교란을 함께") },
            [ModuleType.EwSuite] = new[] { (ModuleType.DecoyLauncher, "유인과 교란을 함께"), (ModuleType.Ciws, "남은 미사일 최종 방어") },
            [ModuleType.Magazine] = new[] { (ModuleType.Autocannon, "붙이면 빠르지만 유폭 때 함께 잃는다"), (ModuleType.NavalGun, "붙이면 빠르지만 유폭 때 함께 잃는다"), (ModuleType.RepairBay, "옆 손상통제반이 유폭 피해를 줄인다") },
            [ModuleType.RepairBay] = new[] { (ModuleType.Magazine, "옆 탄약고 유폭 피해 감소"), (ModuleType.Bridge, "함교의 자동 회복·응급 수리를 키운다") },
            [ModuleType.TurboIntake] = new[] { (ModuleType.RamBow, "속력이 곧 충돌 피해"), (ModuleType.Autocannon, "항해 중 포 피해 증가"), (ModuleType.NavalGun, "항해 중 포 피해 증가") },
            [ModuleType.FireControlArray] = new[] { (ModuleType.Vls, "사거리 증가"), (ModuleType.GuidedRocket, "사거리 증가"), (ModuleType.SamLauncher, "사거리 증가") },
            [ModuleType.MissileLogistics] = new[] { (ModuleType.Vls, "발사·셀 보급 단축"), (ModuleType.GuidedRocket, "발사 간격 단축") },
            [ModuleType.RamBow] = new[] { (ModuleType.TurboIntake, "속력이 곧 충돌 피해") },
            [ModuleType.MineRail] = new[] { (ModuleType.TurboIntake, "빠르게 달리며 쫓는 적 앞에 뿌린다") },
            [ModuleType.Bridge] = new[] { (ModuleType.RepairBay, "손상통제반마다 회복 상한·속도·응급 수리 +1") },
        };

        private static void Synergy(StringBuilder sb, ModuleType type)
        {
            if (!Synergies.TryGetValue(type, out var list)) return;
            var lines = new List<string>();
            foreach (var (with, why) in list)
                if (LinkTo(with) is { } link) lines.Add($"{link}  <color={Muted}>{why}</color>");
            if (lines.Count == 0) return;
            sb.AppendLine().AppendLine($"<color={Key}><b>함께 쓰기</b></color>");
            foreach (var l in lines) sb.AppendLine(l);
        }

        /// <summary>적 본문 끝: 대응에 쓰는 무장(누르면 그 무장으로).</summary>
        private static void CounterLinks(StringBuilder sb, EnemyDefinition def)
        {
            var types = new List<ModuleType>();
            void Add(params ModuleType[] ts) { foreach (var t in ts) if (!types.Contains(t)) types.Add(t); }
            if (def.Kind == Game.Combat.TargetKind.Submarine) Add(ModuleType.Sonar, ModuleType.AswLauncher, ModuleType.TorpedoTube, ModuleType.HelicopterDeck);
            else if (def.Kind == Game.Combat.TargetKind.Aircraft) Add(ModuleType.Ciws, ModuleType.SamLauncher, ModuleType.Nobong, ModuleType.Autocannon);
            else if (def.Category is Game.Combat.TargetCategory.LargeSurface or Game.Combat.TargetCategory.Boss or Game.Combat.TargetCategory.MediumSurface)
                Add(ModuleType.Vls, ModuleType.NavalGun, ModuleType.Howitzer);
            else Add(ModuleType.Autocannon, ModuleType.NavalGun, ModuleType.GuidedRocket);
            if (def.MissilePrefab != null) Add(ModuleType.Ciws, ModuleType.SamLauncher, ModuleType.DecoyLauncher, ModuleType.EwSuite);
            var links = new List<string>();
            foreach (var t in types) if (LinkTo(t) is { } l) links.Add(l);
            if (links.Count > 0) sb.AppendLine().AppendLine($"<color={Key}><b>관련 무장</b></color>").AppendLine(string.Join("  ·  ", links));
        }

        // ------------------------------------------------------------ 함정·용어

        private static List<CodexEntry> Guide()
        {
            var entries = new List<CodexEntry>();
            foreach (var concept in StartingShipCatalog.All)
                if (concept != null) entries.Add(ShipEntry(concept));
            entries.AddRange(Terms());
            return entries;
        }

        private static CodexEntry ShipEntry(StartingShipConcept c)
        {
            var (skill, summary, cooldown) = SignatureSkill.Describe(c.Kind);
            string key = GameSettings.BindingLabel(NavalControl.Signature);
            var e = new CodexEntry
            {
                Id = $"ship_{c.Id}",
                Title = c.Title,
                Tag = $"시작 함정  ·  고유 스킬 [{key}] {skill}",
                Accent = c.Color,
                Roles = c.Kind switch
                {
                    StartShipKind.Command => CodexRole.Air | CodexRole.Support,
                    StartShipKind.MissileDestroyer => CodexRole.Surface | CodexRole.Air,
                    StartShipKind.Assault => CodexRole.Surface,
                    _ => CodexRole.Sub | CodexRole.Surface,
                },
            };
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(c.Description)) sb.AppendLine($"<color={Muted}>{c.Description}</color>").AppendLine();
            if (!string.IsNullOrEmpty(c.StartSummary)) Row(sb, "시작 배치", c.StartSummary);
            Row(sb, $"고유 스킬 [{key}]", $"<b>{skill}</b> — {summary} · 재사용 {cooldown:0}초");
            if (c.InitialEscortCount > 0) Row(sb, "시작 편대", $"고속정 {c.InitialEscortCount}척");
            if (c.StartLoadout != null)
            {
                var counts = new Dictionary<ModuleDefinition, int>();
                var order = new List<ModuleDefinition>();
                foreach (var entry in c.StartLoadout.Entries)
                {
                    if (entry.Module == null) continue;
                    if (!counts.ContainsKey(entry.Module)) { counts[entry.Module] = 0; order.Add(entry.Module); }
                    counts[entry.Module]++;
                }
                var parts = new List<string>();
                foreach (var m in order) parts.Add(Link(m.Id, m.DisplayName) + (counts[m] > 1 ? $" ×{counts[m]}" : ""));
                if (parts.Count > 0) Row(sb, "시작 블록", string.Join(" · ", parts));
            }
            Row(sb, "모든 함정 공통", "함교 기관총(지붕 · 18 m) · 응급 수리 1회 · 기만체 · 전속");
            e.Body = sb.ToString().TrimEnd();
            if (c.StartPreviewPrefab != null) e.Models.Add(new CodexModel("시작 배치", c.StartPreviewPrefab));
            return e;
        }

        private static CodexEntry Term(string id, string title, string tag, CodexRole roles, string body) => new()
        {
            Id = id,
            Title = title,
            Tag = $"용어  ·  {tag}",
            Accent = new Color(0.66f, 1f, 0.75f),
            Roles = roles,
            Body = body,
        };

        private static IEnumerable<CodexEntry> Terms()
        {
            string K(NavalControl c) => $"<color={Key}>[{GameSettings.BindingLabel(c)}]</color>";
            var sb = new StringBuilder();

            sb.Clear();
            Row(sb, "<b>탐지됨</b>", $"레이더(함교 32 m · 전용 {LinkTo(ModuleType.Radar) ?? "레이더"} 52 m)나 근접 센서(24 m, 항상)에 잡힌 적. 화면 표식·레이더 화면에 나온다.");
            Row(sb, "<b>추적 중</b>", "무장이 표적으로 고른 적. 한꺼번에 추적하는 수는 레이더·사격통제 블록이 늘린다(동시 추적 +N).");
            Row(sb, "<b>발사 가능</b>", "넷 다 맞아야 쏜다 — ① 사거리 안(최소~최대) ② 사격각 안(선체 구조물을 맞히는 방향 제외) ③ 포신이 표적을 향함(조준 허용 오차) ④ 탄약 있음.");
            Row(sb, "예외", "CIWS는 자체 센서로 접근 미사일·항공기를 직접 잡는다. VLS(48 m)는 전용 레이더가 있어야 사거리 끝까지 쓴다.");
            yield return Term("term_detect", "탐지 · 추적 · 발사 가능", "센서", CodexRole.Air | CodexRole.Surface | CodexRole.Support, sb.ToString().TrimEnd());

            sb.Clear();
            Row(sb, "<b>잠항</b>", "보이지 않고 일반 무장으로 조준할 수 없다. 어뢰를 준비할 때 물거품·경고음이 난다.");
            Row(sb, "<b>드러남</b>", $"소나 핑·폭뢰·조기경보기·광역 탐색 등에 잡히면 잠시 수면 표식이 생기고 모든 무장이 조준할 수 있다.");
            Row(sb, "<b>접촉 확정</b>", $"{LinkTo(ModuleType.Sonar) ?? "소나"}·{LinkTo(ModuleType.HelicopterDeck) ?? "헬기"} 디핑 소나가 위치를 잡은 상태 — 폭뢰·경어뢰·헬기가 잠항 중에도 그 위치를 공격한다.");
            Row(sb, "대응", $"{LinkTo(ModuleType.AswLauncher) ?? "폭뢰"} · {LinkTo(ModuleType.TorpedoTube) ?? "경어뢰"} · {Link("escort_asw", "대잠 호위함")} · 교리 '대잠'");
            yield return Term("term_sub", "잠수함: 드러남 · 접촉 확정", "대잠", CodexRole.Sub, sb.ToString().TrimEnd());

            sb.Clear();
            Row(sb, "<b>사격각</b>", "무기는 선체 구조물(함교·다른 블록)을 맞히는 방향으로 쏘지 않는다. 정비 화면에서 부채꼴로 보인다(설치 초록, 들고 있는 것 노랑).");
            Row(sb, "<b>사격 금지 구역</b>", "붉게 보이는 방향. 낮은 블록(탄약고·손상통제반·소나·기만체·폭뢰)은 가리지 않는다. 곡사포는 높은 포물선이라 금지 구역이 없다.");
            Row(sb, "<b>승강 거치대</b>", "정비 화면에서 E — 기관포·76mm(유도로켓은 3면 이상 막혔을 때)를 1.2 m 올려 가리지 않게 한다. 대신 내구 −25%.");
            yield return Term("term_firearc", "사격각 · 사격 금지 구역", "배치", CodexRole.Surface | CodexRole.Air, sb.ToString().TrimEnd());

            sb.Clear();
            Row(sb, "<b>조금씩 보급</b>", "탄창이 가득 차지 않았으면 간격마다 몇 발씩 채운다 — 남은 탄으로 계속 쏜다.");
            Row(sb, "<b>전량 재장전</b>", "CIWS는 비면 8초 동안 쏘지 못한다(3초 교전이 없고 절반 아래면 미리 재장전).");
            Row(sb, "<b>정비</b>", "레벨업 정비 화면에 들어가면 모든 무장이 가득 보급된다.");
            Row(sb, "<b>탄약고</b>", $"{LinkTo(ModuleType.Magazine) ?? "탄약고"} 2칸 안 기관포·76mm 발사 간격 −25% · 탄 +50%. 부서지면 유폭 — 붙은 블록과 선체가 함께 다친다.");
            yield return Term("term_ammo", "탄약 · 재장전 · 보급", "운용", CodexRole.Support, sb.ToString().TrimEnd());

            sb.Clear();
            Row(sb, "<b>자동 회복</b>", "함교가 선체를 0.5/초씩 50%까지 고친다. 맞은 뒤 3초 동안은 25%로 줄어든다.");
            Row(sb, $"<b>응급 수리 {K(NavalControl.Repair)}</b>", "선체 +30(상한 무시) · 부서지지 않은 모든 블록 최대 내구 40% 수리. 1회 + 손상통제반 수, 보스를 격침할 때마다 가득.");
            Row(sb, "<b>손상통제반</b>", $"{LinkTo(ModuleType.RepairBay) ?? "손상통제반"} 하나마다 회복 상한 +17%p(3개면 100%) · 속도 +50% · 응급 수리 +1회.");
            Row(sb, "그 밖", "레벨업마다 선체 8, 보스 뒤 보급 항로를 고르면 선체 40을 고친다.");
            yield return Term("term_damage", "손상 통제 · 응급 수리", "생존", CodexRole.Support, sb.ToString().TrimEnd());

            sb.Clear();
            sb.AppendLine($"<color={Muted}>첫 스테이지 보스를 격침한 뒤 한 번 고르고 그 판 동안 유지된다.</color>").AppendLine();
            Row(sb, "<b>방공</b>", "SAM 재장전 −15% · CIWS 포착 −30%");
            Row(sb, "<b>대잠</b>", "소나 확정 시간 −30% · 자동 핑 간격 −25%");
            Row(sb, "<b>포격</b>", "76mm 파편 범위 +25% · 기관포 점사 간격 −20%");
            yield return Term("term_doctrine", "작전 교리", "진행", CodexRole.Air | CodexRole.Sub | CodexRole.Surface, sb.ToString().TrimEnd());

            sb.Clear();
            sb.AppendLine($"<color={Muted}>보스를 격침하면 보상 카드 → 다음 해역으로 갈 항로를 고른다.</color>").AppendLine();
            Row(sb, "<b>보급 항로</b>", "선체 40 수리 · 적 편성 기본");
            Row(sb, "<b>위험 항로</b>", "적 등장·동시 수 +25% · 격침 경험치 +25%");
            Row(sb, "<b>임무 항로</b>", $"보급선 {Game.TaskForce.SupplyEscortMission.Duration:0}초 호위 — 성공: 선체 완전 수리 · 다시 뽑기 +1 · 정비 1회 / 실패: 보상 없음");
            yield return Term("term_route", "항로 선택", "진행", CodexRole.Support, sb.ToString().TrimEnd());

            // 해역 차별화(점검 K04): 스테이지마다 해역이 정해져 있다
            sb.Clear();
            sb.AppendLine($"<color={Muted}>스테이지마다 바다가 다르다 — 섬·레이더·적 구성·정비 카드가 해역에 따라 바뀐다.</color>").AppendLine();
            foreach (var kind in new[] { Game.World.SeaKind.Archipelago, Game.World.SeaKind.Strait, Game.World.SeaKind.Open })
            {
                var sea = Game.World.SeaRegion.For(kind);
                Row(sb, $"<b>{sea.Title}</b>", sea.Tactics);
            }
            Row(sb, "<b>해안포대</b>", "섬·기슭 물가에 박힌 고정 포대. 45m 안에 곡사포 2발(떨어질 자리가 보인다), 체력 110. 곡사포·76mm로 먼저 부수거나 섬 뒤로 피한다. 격침한 자리에는 다시 서지 않는다.");
            Row(sb, "<b>좁은 수로</b>", $"물길 폭 {Game.World.SeaRegion.ChannelHalfWidth * 2f:0}m. 양안 너머로는 나갈 수 없다 — 회피 공간이 좁으니 대잠 수단과 곡사포를 챙긴다.");
            yield return Term("term_sea", "해역 · 해안포대", "진행", CodexRole.Surface | CodexRole.Sub | CodexRole.Air, sb.ToString().TrimEnd());

            sb.Clear();
            sb.AppendLine($"<color={Muted}>바깥에서 안쪽으로 겹겹이 막는다. 한 가지로 모든 위협을 막을 수는 없다.</color>").AppendLine();
            Row(sb, "① 접근 경보", "화면 밖 미사일은 가장자리 괄호와 거리로 표시된다.");
            Row(sb, "② 바깥 요격", $"{LinkTo(ModuleType.SamLauncher) ?? "함대공"}(8~42 m) · {Link("escort_cap", "방공 호위함")}");
            Row(sb, "③ 유인 · 교란", $"기만체 {K(NavalControl.Decoy)} · 재밍 {K(NavalControl.Jam)} — 드론은 속지 않는다");
            Row(sb, "④ 최종 방어", $"{LinkTo(ModuleType.Ciws) ?? "CIWS"}(0~20 m)");
            Row(sb, "⑤ 피격 뒤", $"손상 통제 · 응급 수리 {K(NavalControl.Repair)} · 연막 {K(NavalControl.Smoke)}(적이 조준 사격을 멈춘다)");
            yield return Term("term_defense", "다층 방어 순서", "대공", CodexRole.Air, sb.ToString().TrimEnd());
        }
    }
}

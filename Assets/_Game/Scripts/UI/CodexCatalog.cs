using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Game.Core;
using Game.Data;
using Game.Modules;
using Game.Progression;
using Game.Ship;
using Game.TaskForce;
using Game.Refit;

namespace Game.UI
{
    public enum CodexSection { Weapons, Escorts, Enemies }

    /// <summary>사전 한 항목: 이름·분류 줄·본문(리치 텍스트)과 미리보기 모델(형태·단계별).</summary>
    public sealed class CodexEntry
    {
        public string Id;
        public string Title;
        public string Tag;
        public string Body;
        public Color Accent = Color.white;
        public readonly List<CodexModel> Models = new();
    }

    /// <summary>미리보기 모델 하나. OnlyChild가 있으면 그 자식(Visual_LevelN)만 보인다.</summary>
    public readonly struct CodexModel
    {
        public readonly string Label;
        public readonly GameObject Prefab;
        public readonly string OnlyChild;

        public CodexModel(string label, GameObject prefab, string onlyChild = null)
        {
            Label = label;
            Prefab = prefab;
            OnlyChild = onlyChild;
        }
    }

    /// <summary>
    /// 메인 화면 사전의 내용. 목록은 손으로 적지 않고 지금 게임에 연결된 데이터에서 모은다 —
    /// 무장 = 카드 풀(ExperienceSystem) + 시작 배치(ShipInitializer), 적 = 스테이지 웨이브(StageDirector)와 보스 함재기,
    /// 호위함 = 편대 역할. 수치도 데이터·편대 공식에서 읽으므로 밸런스를 바꾸면 사전이 따라 바뀐다.
    /// 적의 행동 설명·대응만 여기 글로 둔다(없으면 분류로 대신 설명).
    /// </summary>
    public static class CodexCatalog
    {
        private const string Muted = "#8fa4b8";
        private const string Key = "#e0c05a";
        private const string Good = "#7fd08a";
        private const string Warn = "#e0956a";
        private const string Elite = "#f2a33a";
        private const string Boss = "#ff6b5a";
        private const string Column = "<pos=34%>";
        private const float Knots = 1.944f;

        public static List<CodexEntry> Build(CodexSection section) => section switch
        {
            CodexSection.Weapons => Weapons(),
            CodexSection.Escorts => Escorts(),
            _ => Enemies(),
        };

        public static string SectionName(CodexSection section) => section switch
        {
            CodexSection.Weapons => "무장",
            CodexSection.Escorts => "호위함",
            _ => "적",
        };

        // ------------------------------------------------------------ 무장

        /// <summary>카드 풀과 시작 배치에 있는 블록(중복 없이) — 분류 순, 같은 분류는 이름 순.</summary>
        public static List<ModuleDefinition> CollectModules()
        {
            var list = new List<ModuleDefinition>();
            var init = Object.FindFirstObjectByType<ShipInitializer>(FindObjectsInactive.Include);
            if (init != null && init.Loadout != null)
                foreach (var e in init.Loadout.Entries)
                    if (e.Module != null && !list.Contains(e.Module)) list.Add(e.Module);
            var xp = Object.FindFirstObjectByType<ExperienceSystem>(FindObjectsInactive.Include);
            foreach (var m in RefitDraft.CollectInstallDefinitions(xp != null ? xp.Config : null))
                if (!list.Contains(m)) list.Add(m);
            foreach (var concept in StartingShipCatalog.All)
                if (concept != null && concept.StartLoadout != null)
                    foreach (var entry in concept.StartLoadout.Entries)
                        if (entry.Module != null && !list.Contains(entry.Module)) list.Add(entry.Module);
            list.Sort((a, b) =>
            {
                int c = TypeOrder(a.Type).CompareTo(TypeOrder(b.Type));
                return c != 0 ? c : string.CompareOrdinal(a.DisplayName, b.DisplayName);
            });
            return list;
        }

        private static int TypeOrder(ModuleType t) => t switch
        {
            ModuleType.Bridge => 0,
            ModuleType.Autocannon => 1,
            ModuleType.NavalGun => 2,
            ModuleType.GuidedRocket => 3,
            ModuleType.Vls => 4,
            ModuleType.Ciws => 5,
            ModuleType.SamLauncher => 6,
            ModuleType.Sonar => 7,
            ModuleType.AswLauncher => 8,
            ModuleType.HelicopterDeck => 9,
            ModuleType.Radar => 10,
            ModuleType.DecoyLauncher => 11,
            ModuleType.EwSuite => 12,
            ModuleType.Magazine => 13,
            ModuleType.RepairBay => 14,
            ModuleType.FleetRelay => 15,
            ModuleType.TurboIntake => 16,
            ModuleType.FireControlArray => 17,
            ModuleType.MissileLogistics => 18,
            _ => 20,
        };

        private static List<CodexEntry> Weapons()
        {
            var entries = new List<CodexEntry>();
            foreach (var def in CollectModules())
            {
                var e = new CodexEntry
                {
                    Id = def.Id,
                    Title = def.DisplayName,
                    Tag = $"{ModuleCardText.BuildTag(def)}  ·  {RarityName(def.Rarity)}",
                    Accent = new Color(0.66f, 1f, 0.75f),
                };
                var sb = new StringBuilder();
                if (!string.IsNullOrEmpty(def.Description)) sb.AppendLine(def.Description).AppendLine();
                // 카드 표(<pos=44%>)는 값이 길면 왼쪽 끝으로 줄이 넘어간다 — 사전에서는 넘어간 줄도 값 칸에 맞춘다
                foreach (var raw in ModuleCardText.BuildStats(def).Split('\n'))
                    if (raw.TrimEnd('\r') is var line)
                        sb.AppendLine(line.Contains("<pos=44%>") ? line.Replace("<pos=44%>", "<pos=44%><indent=44%>") + "</indent>" : line);

                // 위치별 형태(소나·폭뢰)
                if (ModuleVariants.HasVariants(def.Type))
                {
                    sb.AppendLine().AppendLine($"<color={Key}><b>자리에 따른 형태</b></color>");
                    foreach (var v in VariantsOf(def.Type))
                        sb.AppendLine($"<b>{ModuleVariants.Name(v)}</b>  <color={Muted}>{ModuleVariants.Summary(v)}</color>");
                }

                // 강화 단계
                int max = ModuleUpgrades.MaxLevel(def);
                if (max > 0)
                {
                    sb.AppendLine().AppendLine($"<color={Good}><b>무장 강화</b></color>  <color={Muted}>(장비 강화 카드 · 최대 {ModuleUpgrades.Roman(max)})</color>");
                    for (int l = 0; l < max; l++)
                    {
                        sb.AppendLine($"<b>{(l == 0 ? "기본" : ModuleUpgrades.Roman(l))} → {ModuleUpgrades.Roman(l + 1)}</b>");
                        foreach (var line in ModuleCardText.UpgradeDiff(def, l, l + 1, 4).Split('\n'))
                            if (!string.IsNullOrWhiteSpace(line)) sb.AppendLine("   " + line.Trim());
                    }
                }
                e.Body = sb.ToString().TrimEnd();

                // 미리보기: 형태가 있으면 형태별 모델, 강화 외형이 있으면 단계별, 아니면 블록 모델
                if (ModuleVariants.HasVariants(def.Type))
                {
                    foreach (var v in VariantsOf(def.Type))
                    {
                        var prefab = Resources.Load<GameObject>($"Modules/Variants/{ModuleVariants.ModelName(v)}");
                        e.Models.Add(new CodexModel(ModuleVariants.Name(v), prefab != null ? prefab : def.Prefab));
                    }
                }
                else if (def.Prefab != null && def.Prefab.GetComponent<ModuleUpgradeVisuals>() is { } visuals && visuals.VariantCount > 1)
                {
                    // 외형 단계: Visual_Level1 = 기본, Level2 = 강화 I, Level3 = 강화 II 이상(마지막 외형이 남은 단계를 함께 맡는다)
                    for (int i = 0; i < visuals.VariantCount; i++)
                    {
                        string label = i == 0 ? "기본"
                            : i == visuals.VariantCount - 1 && max > i ? $"강화 {ModuleUpgrades.Roman(i)}~{ModuleUpgrades.Roman(max)}"
                            : $"강화 {ModuleUpgrades.Roman(i)}";
                        e.Models.Add(new CodexModel(label, def.Prefab, $"Visual_Level{i + 1}"));
                    }
                }
                else e.Models.Add(new CodexModel("모델", def.Prefab));
                entries.Add(e);
            }
            return entries;
        }

        private static IEnumerable<ModuleVariant> VariantsOf(ModuleType type)
        {
            if (type == ModuleType.Sonar)
            {
                yield return ModuleVariant.BowSonar;
                yield return ModuleVariant.TowedSonar;
                yield return ModuleVariant.HullSonar;
            }
            else if (type == ModuleType.AswLauncher)
            {
                yield return ModuleVariant.DepthChargeRack;
                yield return ModuleVariant.DepthChargeProjector;
            }
        }

        private static string RarityName(Rarity r) => r switch
        {
            Rarity.Common => "일반",
            Rarity.Uncommon => $"<color={Good}>고급</color>",
            Rarity.Rare => "<color=#7fb8e0>희귀</color>",
            _ => "<color=#d09aff>영웅</color>",
        };

        // ------------------------------------------------------------ 호위함

        private static List<CodexEntry> Escorts()
        {
            var entries = new List<CodexEntry>();
            var roles = new List<EscortRole> { EscortRole.None };
            roles.AddRange(TaskForceEscortFormation.Roles);
            int maxTier = TaskForceEscortFormation.MaxUpgradeLevel;
            foreach (var role in roles)
            {
                string code = TaskForceEscortFormation.RoleCode(role);
                var e = new CodexEntry
                {
                    Id = $"escort_{code.ToLowerInvariant()}",
                    Title = TaskForceEscortFormation.RoleName(role),
                    Tag = $"<color=#{ColorUtility.ToHtmlStringRGB(TaskForceEscortFormation.RoleColor(role))}>{code}</color>  ·  {TaskForceEscortFormation.RoleShort(role)}  ·  아군 수상함",
                    Accent = TaskForceEscortFormation.RoleColor(role),
                };
                var sb = new StringBuilder();
                sb.AppendLine($"<color={Muted}>{TaskForceEscortFormation.RoleSummary(role)}.</color>").AppendLine();
                Row(sb, "얻는 법", role == EscortRole.None
                    ? $"편대 배치 카드 — 1척 합류(최대 {TaskForceEscortFormation.MaxEscorts}척)"
                    : "고속정에 편대 강화 카드 → 역할 지정(T0), 다시 강화하면 T1~T3");
                Row(sb, "함포(모든 함)", $"반경 {EscortDefense.GunRange:0} m · 피해 {EscortDefense.GunDamageFor(0):0.#}~{EscortDefense.GunDamageFor(maxTier):0.#} · {EscortDefense.GunInterval:0.0#}초 간격");
                string ability = role switch
                {
                    EscortRole.AirDefense => $"함대공 요격 반경 {EscortDefense.SamRange:0} m — 기함에 가장 가까운 적 미사일부터",
                    EscortRole.AntiSubmarine => $"대잠 탐색 반경 {EscortDefense.AswRange:0} m — 잠항 중인 잠수함도 찾아 접촉 확정 + 경어뢰",
                    EscortRole.ElectronicWarfare => $"근접 교란 반경 {EscortDefense.JamRange:0} m — 교란되지 않은 적 미사일 하나",
                    EscortRole.SurfaceStrike => $"대함 타격 반경 {EscortDefense.StrikeRange:0} m — 드러난 수상 표적 중 가치가 가장 높은 것",
                    _ => null,
                };
                if (ability != null) Row(sb, "역할 능력", ability);
                sb.AppendLine().AppendLine($"<color={Good}><b>개량 단계</b></color>");
                int tiers = role == EscortRole.None ? 0 : maxTier;
                for (int t = 0; t <= tiers; t++)
                    Row(sb, $"T{t}", $"{TaskForceEscortFormation.TierDescription(role, t)} · 선체 {TaskForceEscortFormation.MaxHullFor(t):0}");
                sb.AppendLine().AppendLine($"<color={Key}><b>생존</b></color>");
                Row(sb, "적의 표적", $"기함보다 덜 노림(우선도 {TaskForceEscortFormation.EscortTargetPriority:0.#}) · 한 척에 최대 {TaskForceEscortFormation.EscortMaxAttackers}척");
                Row(sb, "전투 불능", $"뒤로 이탈 → {TaskForceEscortFormation.RecoverSeconds:0}초 뒤 선체 {TaskForceEscortFormation.RecoverHull * 100f:0}%로 복귀 · 정비 때 전부 수리");
                Row(sb, "진형", "G키로 복렬진·종렬진·단열진·사열진·능형진 전환");
                e.Body = sb.ToString().TrimEnd();

                if (role == EscortRole.None)
                {
                    var prefab = Resources.Load<GameObject>("TaskForce/Escorts/ESC_PB_T0") ?? Resources.Load<GameObject>("TaskForce/Escorts/ESC_STK_T0");
                    e.Models.Add(new CodexModel("T0", prefab));
                }
                else
                    for (int t = 0; t <= maxTier; t++)
                        e.Models.Add(new CodexModel($"T{t}", Resources.Load<GameObject>($"TaskForce/Escorts/ESC_{code}_T{t}")));
                entries.Add(e);
            }
            return entries;
        }

        // ------------------------------------------------------------ 적

        /// <summary>스테이지 웨이브(등장 순서)와 보스 함재기에서 모은 적과, 적마다 등장한 스테이지.</summary>
        public static List<(EnemyDefinition def, string where)> CollectEnemies()
        {
            var order = new List<EnemyDefinition>();
            var where = new Dictionary<EnemyDefinition, SortedSet<int>>();
            var notes = new Dictionary<EnemyDefinition, string>();
            void Add(EnemyDefinition d, int stage, string note = null)
            {
                if (d == null) return;
                if (!where.TryGetValue(d, out var set)) { set = new SortedSet<int>(); where[d] = set; order.Add(d); }
                set.Add(stage);
                if (note != null && !notes.ContainsKey(d)) notes[d] = note;
            }
            var director = Object.FindFirstObjectByType<StageDirector>(FindObjectsInactive.Include);
            if (director != null && director.Stages != null)
                for (int s = 0; s < director.Stages.Count; s++)
                {
                    var set = director.Stages[s];
                    if (set == null) continue;
                    foreach (var round in set.Rounds)
                    {
                        if (round.Entries != null) foreach (var entry in round.Entries) Add(entry.Enemy, s + 1);
                        Add(round.Escort, s + 1, "보스 호위");
                        if (round.Boss == null) continue;
                        Add(round.Boss, s + 1, "보스");
                        Add(round.Boss.LaunchedDrone, s + 1, "보스 함재기");
                        Add(round.Boss.LaunchedRecon, s + 1, "보스 함재기");
                        Add(round.Boss.LaunchedFighter, s + 1, "보스 함재기");
                    }
                }
            var result = new List<(EnemyDefinition, string)>();
            foreach (var d in order)
            {
                string stages = "스테이지 " + string.Join(", ", where[d]);
                result.Add((d, notes.TryGetValue(d, out var n) ? $"{stages} ({n})" : stages));
            }
            return result;
        }

        private static List<CodexEntry> Enemies()
        {
            var entries = new List<CodexEntry>();
            foreach (var (def, where) in CollectEnemies())
            {
                string rank = def.Rank switch
                {
                    EnemyRank.Elite => $"<color={Elite}>엘리트</color>",
                    EnemyRank.Boss => $"<color={Boss}>보스</color>",
                    _ => "일반",
                };
                var e = new CodexEntry
                {
                    Id = def.Id,
                    Title = def.DisplayName,
                    Tag = $"{rank}  ·  {CategoryName(def)}  ·  {where}",
                    Accent = def.Rank == EnemyRank.Boss ? new Color(1f, 0.42f, 0.35f)
                        : def.Rank == EnemyRank.Elite ? new Color(0.95f, 0.64f, 0.23f) : new Color(0.85f, 0.9f, 0.92f),
                };
                var (about, counter) = Lore(def);
                var sb = new StringBuilder();
                sb.AppendLine($"<color={Muted}>{about}</color>").AppendLine();
                Row(sb, "체력", $"{def.MaxHp:0}");
                Row(sb, "속력", $"{def.MoveSpeed:0.#} m/s ({def.MoveSpeed * Knots:0}노트) · 선회 {def.TurnRateDegPerSec:0}°/초");
                if (def.PreferredRange > 0f) Row(sb, "교전 거리", $"{def.PreferredRange:0} m");
                if (def.AttackDamage > 0f)
                {
                    string salvo = def.SalvoSize > 1 ? $" × {def.SalvoSize}발" : "";
                    Row(sb, "공격", def.AttackCooldown > 0f
                        ? $"피해 {def.AttackDamage:0.#}{salvo} · {def.AttackCooldown:0.#}초마다"
                        : $"피해 {def.AttackDamage:0.#} (충돌)");
                }
                else Row(sb, "공격", "없음");
                if (def.MissilePrefab != null) Row(sb, "무장", "대함미사일 — CIWS·함대공 요격, 기만체·재밍에 속음");
                if (def.Kind == Game.Combat.TargetKind.Submarine)
                    Row(sb, "은밀", $"잠항 중엔 소나·헬기·폭뢰로 드러나야 조준 가능 · 드러나면 {def.RevealDuration:0}초 노출");
                Row(sb, "경험치", def.XpReward > 0 ? $"{def.XpReward}" : "없음");
                if (def.MaxAlive > 0) Row(sb, "동시 최대", $"{def.MaxAlive}척");
                if (!string.IsNullOrEmpty(counter))
                    sb.AppendLine().AppendLine($"<color={Key}><b>대응</b></color>").AppendLine(counter);
                e.Body = sb.ToString().TrimEnd();
                e.Models.Add(new CodexModel("모델", def.Prefab));
                entries.Add(e);
            }
            return entries;
        }

        private static string CategoryName(EnemyDefinition def) => def.Category switch
        {
            Game.Combat.TargetCategory.SmallSurface => "소형 수상함",
            Game.Combat.TargetCategory.MediumSurface => "중형 수상함",
            Game.Combat.TargetCategory.LargeSurface => "대형 수상함",
            Game.Combat.TargetCategory.Boss => "대형함",
            Game.Combat.TargetCategory.Submarine => "잠수함",
            Game.Combat.TargetCategory.Drone => "드론",
            Game.Combat.TargetCategory.Air => "항공기",
            _ => def.Kind switch
            {
                Game.Combat.TargetKind.Submarine => "잠수함",
                Game.Combat.TargetKind.Aircraft => "항공기",
                _ => "수상함",
            },
        };

        /// <summary>적 행동 설명과 대응(수치는 데이터에서 따로 보여 주므로 여기엔 행동만 적는다).</summary>
        private static (string about, string counter) Lore(EnemyDefinition def) => def.Id switch
        {
            "ene_fastboat" => ("가장 흔한 적. 무리 지어 다가와 선회하며 포탑으로 점사한다. 하나하나는 약하지만 수로 밀어붙인다.",
                "기관포 · 76mm · 호위함 함포로 다가오기 전에 정리한다."),
            "ene_suicide_boat" => ("폭약을 실은 작고 매우 빠른 무인 보트. 멀리서는 지그재그로, 가까워지면 만나는 점을 향해 돌진해 함체에 닿는 순간 폭발한다(맞은 칸 모듈·선체 피해). 스스로 터지면 경험치가 없다.",
                "기관포 · 76mm로 접근 전에 격침하거나, 전령기·타로 진로를 비틀어 빗나가게 한다."),
            "ene_armored_boat" => ("선수와 옆면을 장갑으로 두른 돌격정. 고속정처럼 다가와 선회하며 쌍열 기관포로 점사하지만 훨씬 단단하고 느리다.",
                "76mm 같은 한 방이 큰 무기로 잡고, 기관포만으로 상대하면 오래 붙어 있게 된다."),
            "ene_torpedo_boat" => ("양현에 고정식 어뢰관을 단 소형정. 거리를 두고 돌다가 사격 제원을 정하면(\"어뢰 준비\" · 경고음) 뱃머리를 만나는 점으로 돌려 어뢰를 쏜다. 너무 가까우면 쏘지 않는다.",
                "경고가 뜨면 전령기를 한 칸 바꾸거나 타를 써서 빗나가게 한다. 수면 위에 보이므로 경고 전에 격침하면 막을 수 있다. 연막은 제원을 취소시킨다."),
            "ene_artillery_boat" => ("큰 선수 함포를 단 지원정. 기관포 · 76mm 사거리 밖에서 천천히 돌며 곡사 포격을 한다. 포탄이 날아가는 동안 착탄점 수면에 붉은 경고 원이 차오른다.",
                "경고 원을 보고 침로·속력을 바꾸면 빗나간다. 유도로켓 · VLS로 먼 거리에서 잡거나, 다가가면 느려서 쉽게 따라잡힌다."),
            "ene_repair_boat" => ("크레인을 단 수리 지원정. 공격하지 않고 다친 아군 곁에 붙어 초록 빔으로 선체를 고친다(보스는 못 고친다). 가까이 오면 물러난다.",
                "놔두면 엘리트와 포격정이 계속 살아난다 — 초록 빔이 보이면 먼저 격침한다."),
            "ene_minelayer" => ("레일과 투하 슈트에 기뢰를 실은 부설정. 함선 진로 앞쪽 옆을 나란히 달리며 부유 기뢰를 뿌린다. 기뢰는 잠시 뒤 붉은 불이 빠르게 깜빡이며 작동하고, 함체가 닿으면 터진다.",
                "진로를 바꾸거나, 기관포 · 76mm로 기뢰를 쏘아 없앤다(기뢰는 경험치가 없다). 부설정을 먼저 잡으면 더 깔리지 않는다."),
            "ene_missileboat" => ("거리를 두고 선회하며 팝업 궤적 대함미사일을 연발로 쏜다. 엘리트라 단단하고 동시에 나오는 수가 제한된다.",
                "미사일은 CIWS · 함대공 · 기만체(Q) · 재밍(E)으로, 본체는 유도로켓 · VLS로 먼 거리에서."),
            "ene_submarine" => ("잠항한 채 선회하다 사격 제원을 정하면(물거품 · 경고음 · \"어뢰 준비\") 잠시 뒤 그 순간의 침로·속력으로 만나는 점에 직선 어뢰를 쏜다. 너무 가까우면 쏘지 않는다.",
                "경고가 뜨면 전령기를 한 칸 바꾸거나 타를 써서 빗나가게 한다. 연막은 제원을 취소시킨다. 소나로 찾고 폭뢰 · 헬기 · 대잠 호위함으로 잡는다."),
            "ene_cruise_submarine" => ("먼 거리에서 천천히 선회하며 주기적으로 잠망경을 올려 부상, 대함 순항미사일을 한 발 쏘고 잠시 드러난 뒤 다시 잠항한다. 어뢰는 없다.",
                "드러난 순간 VLS · 유도로켓으로 노린다. 미사일은 CIWS · 기만체 · 재밍으로 막는다."),
            "ene_pcc_corvette" => ("함포 4문(함수 2 · 함미 2)이 각자 선회해 넷 다 향할 때만 일제사격한다. 함수 · 함미 정면은 포가 돌지 못하는 사각. 체력이 절반이 되면 한 번 전속 회피한다.",
                "함수나 함미 쪽에서 교전하면 일제사격이 줄어든다. 연막 중에는 사격을 멈춘다."),
            "ene_drone" => ("낮게 날아와 가까워지면 급강하해 들이받는다(맞은 자리 모듈 피해). 기만체 · 재밍에 속지 않는다.",
                "기관포 · CIWS · 함대공. 편대로 오면 대공 무기를 나눠 맡게 배치한다."),
            "ene_fighter" => ("높이 접근한 뒤 낮게 내려와 기총 소사, 머리 위를 지나쳐 급상승 이탈했다가 다시 진입하기를 되풀이한다.",
                "저공 진입 때 기관포 · CIWS, 이탈해 멀어질 때 함대공 · 76mm."),
            "ene_recon" => ("공격하지 않고 기관포 사거리 밖을 높이 돈다. 살아 있는 동안 모든 적의 재장전이 빨라진다.",
                "76mm · 함대공으로 먼저 떨어뜨린다."),
            "ene_boss" => ("스테이지 1의 적 기함. 선회하며 대함미사일을 일제사격한다.",
                "기만체 · CIWS로 일제사격을 막고 그 사이 화력을 모은다."),
            "ene_boss_corvette" => ("스테이지 2의 적 기함. 선수·선미 76mm 함포, 중앙 경사 미사일 발사대, 좌우 어뢰 발사관을 갖춘 현대화 초계함. 거리를 오가며 공격이 바뀐다: 멀면 마주 보는 현측 발사대에서 대함미사일 2발이 두 방위로 돌아 들어오고, 중간 거리에서는 두 함포가 번갈아 예고 사격(수면의 붉은 경고 원), 가까우면 현측 발사관 붉은 등이 깜빡인 뒤 어뢰 2발. 체력 절반 아래부터 더 빨라지고 어뢰 뒤에 함포가 이어진다.",
                "미사일은 여러 방향을 덮는 CIWS · 기만체로. 경고 원은 조함으로 비키고, 어뢰는 등이 깜빡이는 동안 침로나 속력을 바꾸면 빗나간다 — 소나 없이도 수면 항적이 보인다. 현측 정면에 오래 머물지 않는다."),
            "ene_usv" => ("스테이지 3. 사람이 타지 않는 공격정. 고속정보다 빠르고 조금 단단하며 떼로 몰려와 선수 기관포를 점사한다.",
                "기관포 · CIWS로 쓸어 낸다. 무리로 오므로 여러 방향을 덮는 배치가 유리하다."),
            "ene_ew_corvette" => ("스테이지 3 엘리트. 방해 안테나로 전파를 교란해, 60m 안에 있는 동안 레이더 탐지 거리를 25% 줄인다(육안 거리는 그대로). 선수포로 견제한다.",
                "가장 먼저 잡을 표적. 격침하면 바로 풀린다. 탐지가 줄면 멀리서 오는 미사일·잠수함 대응이 늦어진다."),
            "ene_aa_frigate" => ("스테이지 3 엘리트. 함미 근접방어포가 28m 안의 유도탄(VLS · 유도로켓 · 함대공)을 예광탄으로 쏘아 떨어뜨린다 — 근처 다른 적을 노린 유도탄도 막는다.",
                "미사일 위주라면 함포로 먼저 잡는다. 근접방어포는 점사 뒤 1.2초 쉬므로 여러 발을 몰아 쏘면 뚫린다."),
            "ene_attack_submarine" => ("스테이지 3 엘리트 잠수함. 어뢰 잠수함보다 단단하고 자주 쏜다(예고 뒤 직선 어뢰).",
                "소나 · 대잠 헬기로 찾고 폭뢰 · 대잠 호위함으로. 어뢰는 예고 동안 침로 · 속력을 바꿔 피한다."),
            "ene_boss2" => ("스테이지 3의 적 기함(현대화 이세급 항공전함). 앞은 전함, 뒤는 비행갑판. 체력에 따라 세 단계로 바뀐다: 주포 일제사격(수면의 붉은 경고 원) → 드론 편대 · 정찰기 운용 → 드론 · 전투기 편대와 대함미사일 연발. 선미 쪽은 주포가 쏘지 못하는 안전 지대.",
                "경고 원은 조함 · 전속(C) · 연막(F)으로 피한다. 함재기는 대공 무기로, 미사일은 기만체 · 재밍으로. 선미 쪽으로 돌아 들어가면 주포를 피할 수 있다."),
            _ => (def.Kind switch
            {
                Game.Combat.TargetKind.Submarine => "잠항해 접근하는 잠수함.",
                Game.Combat.TargetKind.Aircraft => "하늘에서 접근하는 항공기.",
                _ => "적 수상함.",
            }, null),
        };

        /// <summary>표 한 줄: 항목 | 값(값이 길어 줄이 넘어가도 값 칸에 맞춘다).</summary>
        private static void Row(StringBuilder sb, string label, string value) => sb.AppendLine($"{label}{Column}<indent=34%>{value}</indent>");
    }
}

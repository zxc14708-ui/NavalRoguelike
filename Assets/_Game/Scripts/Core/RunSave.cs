using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Game.Data;
using Game.Modules;
using Game.Progression;
using Game.Refit;
using Game.Ship;
using Game.TaskForce;

namespace Game.Core
{
    /// <summary>
    /// 중단·재개 저장(2026-10-11 점검 O01). <b>스테이지 경계</b>에서만 저장한다 — 보스 보상·항로·교리를 다 고르고 다음 스테이지가
    /// 시작되는 순간(StageDirector.FinishStageTransition). 시작 화면 [이어하기]는 그 스테이지 처음부터 다시 연다.
    ///   - 담는 것: 시작 함정, 블록 배치(위치·회전·강화 단계·승강), 성장 카드, 편대(슬롯·역할·단계)·진형, 교리, 레벨·경험치,
    ///     선체 비율, 위험 항로, 남은 다시 뽑기, 작전 시간·격침 수, 결과 분석 기록(무장 기여 · 적 미사일 처리 — 2026-11-23 Codex 점검,
    ///     예전 저장은 기록이 없어 결과 화면에 "이어한 시점부터 집계"로 알린다). 블록 내구는 저장하지 않는다(모두 수리된 상태로 시작).
    ///   - 파일: persistentDataPath/naval_run.json. 저장 때마다 직전 파일을 .bak으로 남기고(임시 파일에 쓴 뒤 바꿔 넣음),
    ///     읽을 때 본 파일이 깨졌으면 .bak을, 둘 다 <b>깨졌으면(형식 오류)</b>만 지우고 LastProblem에 이유를 남긴다(시작 화면이 알린다).
    ///   - 형식 버전 Version. 더 새 버전 파일 · 이 빌드에 없는 함정의 파일은 읽지 않지만 <b>지우지 않는다</b>(2026-11-23 Codex 점검 —
    ///     업데이트 뒤 구버전을 열기만 해도 진행 기록을 잃었다). 파일을 열지 못한 일시적 오류(잠김 · 권한)도 지우지 않는다.
    ///     열거형은 이름(문자열)으로 저장해 순서가 바뀌어도 안전하다.
    ///   - 승패가 나면 지운다(훈련은 제외). 새로 출항하면(이어하기가 아닌) 지운다.
    /// </summary>
    public static class RunSave
    {
        public const int Version = 1;
        private const string FileName = "naval_run.json";

        [Serializable] public class ModuleData { public string id; public int x, z, rot, upgrade; public bool raised; }
        [Serializable] public class GrowthData { public string stat, tier; }
        [Serializable] public class EscortData { public int slot; public string role; public int tier; }
        [Serializable] public class ReportData { public string key; public float damage; public int kills; }
        [Serializable] public class ThreatData { public string outcome; public int count, seen; public float sum, min, max; }

        [Serializable]
        public class Data
        {
            public int version;
            public string savedAt;
            public string concept;
            public int stage;
            public bool danger;
            public bool dangerStart;   // 고난도 출항으로 시작한 판(점검 K06, 예전 파일은 false)
            public bool mission;       // 이 스테이지를 임무 항로로 왔는지(점검 K03, 예전 파일은 false)
            public int level, xp;
            public float hull;
            public float elapsed;
            public int kills;
            public string doctrine;
            public int rerolls;
            public int seed;
            public List<int> routePath = new();   // 항로 지도에서 고른 길(2026-10-31, 예전 파일은 비어 있음 — 출항 지점부터)
            public string formation;
            public List<ModuleData> modules = new();
            public List<GrowthData> growth = new();
            public List<EscortData> escorts = new();
            public bool hasStats;                          // 결과 분석 기록이 들어 있다(2026-11-23, 예전 파일은 false)
            public List<ReportData> report = new();        // 무장별 실제 피해 · 격침
            public List<ThreatData> threats = new();       // 적 미사일 처리(요격 · 피격 · 빗나감)
        }

        /// <summary>마지막으로 읽기에 실패한 이유(시작 화면이 한 번 보여 준다). 없으면 null.</summary>
        public static string LastProblem { get; private set; }

        /// <summary>읽기 결과. Corrupt만 지울 수 있다 — 나머지는 다른 빌드 · 잠깐의 오류라 파일을 남긴다.</summary>
        public enum ReadResult { Ok, Missing, Corrupt, Incompatible, IoError }

        /// <summary>검증용: 지난 TryLoad가 파일을 남겼는가(버전 불일치 · 읽기 오류).</summary>
        public static bool LastKept { get; private set; }
        /// <summary>검증용: 지난 불러오기에서 찾지 못해 건너뛴 블록 수.</summary>
        public static int LastSkippedModules { get; private set; }

        /// <summary>자동 검증 실행(-combatVerify)은 따로 된 파일을 쓴다 — 실제 이어하기 기록을 지우거나 덮지 않게.</summary>
        private static readonly bool s_verify = Array.IndexOf(Environment.GetCommandLineArgs(), "-combatVerify") >= 0;
        public static string FilePath => Path.Combine(Application.persistentDataPath, s_verify ? "naval_run_verify.json" : FileName);
        private static string BackupPath => FilePath + ".bak";
        private static string TempPath => FilePath + ".tmp";

        // ------------------------------------------------------------ 저장

        public static bool Save(StageDirector director)
        {
            var gm = GameManager.Instance;
            var ship = gm != null ? gm.Player : null;
            if (ship == null || director == null || director.Training || director.Ww2Test) return false;   // WWII 시험 전투는 저장하지 않는다
            var init = UnityEngine.Object.FindFirstObjectByType<ShipInitializer>();
            var xp = UnityEngine.Object.FindFirstObjectByType<ExperienceSystem>();
            var fleet = ship.GetComponent<TaskForceEscortFormation>();
            var data = new Data
            {
                version = Version,
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                concept = init != null && init.SelectedConcept != null ? init.SelectedConcept.Id : "",
                stage = director.StageIndex,
                danger = director.DangerRoute,
                dangerStart = director.DangerStartRun,
                mission = director.MissionRoute,
                level = xp != null ? xp.Level : 1,
                xp = xp != null ? xp.Xp : 0,
                hull = ship.HullMaxHp > 0f ? ship.HullHp / ship.HullMaxHp : 1f,
                elapsed = gm.ElapsedTime,
                kills = gm.TotalKills,
                doctrine = CombatPolicies.Doctrine.ToString(),
                rerolls = RefitDraft.RerollsLeft,
                seed = RunSeed.Current,
                routePath = new List<int>(RouteMap.Path),
                formation = fleet != null ? fleet.Formation.ToString() : "",
            };
            foreach (var m in ship.Grid.Modules)
                if (m?.Definition != null)
                    data.modules.Add(new ModuleData { id = m.Definition.Id, x = m.Origin.X, z = m.Origin.Z, rot = m.RotationSteps, upgrade = m.UpgradeLevel, raised = m.IsRaised });
            foreach (var (stat, tier) in RunUpgrades.Cards)
                data.growth.Add(new GrowthData { stat = stat.ToString(), tier = tier.ToString() });
            // 결과 분석 기록(이어한 판도 처음부터 집계되게)
            data.hasStats = !Game.Combat.RunReport.PartialSinceResume;
            foreach (var e in Game.Combat.RunReport.Snapshot()) data.report.Add(new ReportData { key = e.Key, damage = e.Damage, kills = e.Kills });
            foreach (var kv in Game.Combat.CombatStats.Threats)
                data.threats.Add(new ThreatData { outcome = kv.Key.ToString(), count = kv.Value.Count, seen = kv.Value.Seen, sum = kv.Value.SumSeconds,
                                                  min = kv.Value.MinSeconds, max = kv.Value.MaxSeconds });
            if (fleet != null)
                for (int i = 0; i < fleet.EscortCount; i++)
                {
                    var info = fleet.GetInfo(i);
                    data.escorts.Add(new EscortData { slot = info.RosterSlot, role = info.Role.ToString(), tier = info.Tier });
                }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(TempPath, JsonUtility.ToJson(data, true));
                if (File.Exists(FilePath)) File.Copy(FilePath, BackupPath, true);
                File.Copy(TempPath, FilePath, true);
                File.Delete(TempPath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunSave] 저장 실패: {e.Message}");
                return false;
            }
        }

        public static void Delete()
        {
            foreach (var p in new[] { FilePath, BackupPath, TempPath })
                try { if (File.Exists(p)) File.Delete(p); } catch (Exception e) { Debug.LogWarning($"[RunSave] 지우기 실패: {e.Message}"); }
        }

        /// <summary>승패가 났다(GameManager가 부른다). 훈련이 아니면 이어하기 기록을 지운다.</summary>
        public static void OnRunEnded()
        {
            var director = UnityEngine.Object.FindFirstObjectByType<StageDirector>();
            if (director != null && (director.Training || director.Ww2Test)) return;   // 훈련·WWII 시험 전투는 현대 이어하기 기록을 지우지 않는다
            Delete();
        }

        // ------------------------------------------------------------ 읽기

        /// <summary>이어할 기록이 있으면 읽어 준다(본 파일 → 백업 순). 둘 다 깨졌으면 지우고 LastProblem을 남긴다.</summary>
        public static bool TryLoad(out Data data)
        {
            data = null;
            LastKept = false;
            bool hadFile = File.Exists(FilePath) || File.Exists(BackupPath);
            if (!hadFile) return false;
            var r1 = TryRead(FilePath, out data, out string why1);
            if (r1 == ReadResult.Ok) return true;
            // 본 파일이 다른 빌드의 것이거나 지금 열 수 없으면: 백업(더 오래된 기록)으로 이어 하지도, 지우지도 않는다
            if (r1 == ReadResult.Incompatible || r1 == ReadResult.IoError) return Keep(r1, why1);
            var r2 = TryRead(BackupPath, out data, out string why2);
            if (r2 == ReadResult.Ok)
            {
                LastProblem = $"저장 파일이 손상되어 직전 백업으로 이어 합니다({why1}).";
                return true;
            }
            if (r2 == ReadResult.Incompatible || r2 == ReadResult.IoError) return Keep(r2, why2);
            LastProblem = $"저장 파일이 손상되어 지웠습니다({why1 ?? why2}).";
            Delete();
            return false;
        }

        private static bool Keep(ReadResult r, string why)
        {
            LastKept = true;
            LastProblem = r == ReadResult.Incompatible
                ? $"다른 버전의 저장이라 이어할 수 없습니다({why}) — 파일은 그대로 둡니다. 새로 출항하면 덮어씁니다."
                : $"저장 파일을 지금 읽지 못했습니다({why}) — 파일은 그대로 둡니다. 게임을 다시 열어 보세요.";
            return false;
        }

        public static void ClearProblem() => LastProblem = null;

        private static ReadResult TryRead(string path, out Data data, out string why)
        {
            data = null;
            why = null;
            if (!File.Exists(path)) { why = "파일 없음"; return ReadResult.Missing; }
            string text;
            try { text = File.ReadAllText(path); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                why = e is UnauthorizedAccessException ? "권한 없음" : "파일 잠김";   // 다른 프로그램(백신 · 동기화)이 잡고 있음 등
                return ReadResult.IoError;
            }
            try
            {
                data = JsonUtility.FromJson<Data>(text);
                if (data == null) { why = "내용 없음"; return ReadResult.Corrupt; }
                if (data.version > Version) { why = $"더 새 버전 {data.version}(이 빌드 {Version})"; data = null; return ReadResult.Incompatible; }
                if (data.version <= 0) { why = $"버전 {data.version}"; data = null; return ReadResult.Corrupt; }
                if (StartingShipCatalog.Find(data.concept) == null) { why = $"이 빌드에 없는 함정 {data.concept}"; data = null; return ReadResult.Incompatible; }
                if (data.modules == null || data.modules.Count == 0) { why = "블록 정보 없음"; data = null; return ReadResult.Corrupt; }
                if (data.stage < 0) { why = "스테이지 정보 오류"; data = null; return ReadResult.Corrupt; }
                return ReadResult.Ok;
            }
            catch (Exception e)
            {
                why = e is ArgumentException ? "형식 오류" : e.GetType().Name;
                data = null;
                return ReadResult.Corrupt;
            }
        }

        /// <summary>결과 분석 기록을 되살린다. 기록이 없는 예전 저장이면 비우고 "이어한 시점부터"로 표시한다.</summary>
        public static void RestoreStats(Data d)
        {
            Game.Combat.CombatStats.Reset();
            if (d == null || !d.hasStats) { Game.Combat.RunReport.BeginRun(partial: true); return; }
            var list = new List<Game.Combat.RunReport.Entry>();
            if (d.report != null) foreach (var r in d.report) if (r != null) list.Add(new Game.Combat.RunReport.Entry { Key = r.key, Damage = r.damage, Kills = r.kills });
            Game.Combat.RunReport.Restore(list);
            if (d.threats != null)
                foreach (var t in d.threats)
                    if (t != null && Enum.TryParse(t.outcome, out Game.Combat.ThreatOutcome o))
                        Game.Combat.CombatStats.RestoreThreat(o, t.count, t.seen, t.sum, t.min, t.max);
        }

        /// <summary>시작 화면에 보일 한 줄: "스테이지 2 · 지휘함 · Lv 6 · 10-11 21:40".</summary>
        public static string Describe(Data d)
        {
            var concept = StartingShipCatalog.Find(d.concept);
            return $"스테이지 {d.stage + 1} · {(concept != null ? concept.Title : "?")} · Lv {d.level} · {d.savedAt}";
        }

        // ------------------------------------------------------------ 되살리기

        /// <summary>
        /// 시작 함정은 이미 골라 출항 고정된 상태(HUD)에서 부른다: 블록을 저장대로 다시 깔고 성장·편대·교리·레벨을 맞춘 뒤
        /// 저장된 스테이지 처음부터 시작한다.
        /// </summary>
        public static void Apply(Data d, StageDirector director)
        {
            var gm = GameManager.Instance;
            var ship = gm.Player;
            var grid = ship.Grid;
            var factory = UnityEngine.Object.FindFirstObjectByType<ModuleFactory>();

            // 블록: 시작 배치를 치우고 저장대로
            var defs = new Dictionary<string, ModuleDefinition>();
            foreach (var def in Game.UI.CodexCatalog.CollectModules()) if (def != null && !defs.ContainsKey(def.Id)) defs[def.Id] = def;
            foreach (var def in Resources.LoadAll<ModuleDefinition>("Modules")) if (def != null && !defs.ContainsKey(def.Id)) defs[def.Id] = def;
            foreach (var m in new List<ModuleInstance>(grid.Modules))
            {
                if (m.Runtime != null) m.Runtime.gameObject.SetActive(false);
                factory.Uninstall(m);
            }
            // 저장 순서는 정비에서 집었다 놓은 순서라 연결부보다 바깥 블록이 앞설 수 있다.
            // 한 바퀴에 붙일 수 있는 것만 붙이고, 더 붙는 게 없을 때까지 남은 것을 다시 돈다.
            LastSkippedModules = 0;
            var pending = new List<(ModuleData md, ModuleDefinition def)>();
            foreach (var md in d.modules)
            {
                if (defs.TryGetValue(md.id, out var def)) pending.Add((md, def));
                else { LastSkippedModules++; Debug.LogWarning($"[RunSave] 블록 {md.id} 없음 — 건너뜀"); }
            }
            bool progress = true;
            while (pending.Count > 0 && progress)
            {
                progress = false;
                for (int p = 0; p < pending.Count; p++)
                {
                    var (md, def) = pending[p];
                    if (!grid.CanPlace(def, new GridCoord(md.x, md.z), md.rot, out _)) continue;
                    var inst = factory.Install(def, new GridCoord(md.x, md.z), md.rot);
                    if (inst == null) continue;
                    for (int i = 0; i < md.upgrade; i++) if (!inst.ApplyUpgrade()) break;
                    if (md.raised) factory.SetRaised(inst, true);
                    pending.RemoveAt(p--);
                    progress = true;
                }
            }
            foreach (var (md, _) in pending) { LastSkippedModules++; Debug.LogWarning($"[RunSave] 블록 {md.id} ({md.x},{md.z}) 놓을 수 없음 — 건너뜀"); }
            ship.Systems?.Recalculate();

            // 성장 · 교리 · 다시 뽑기 · 레벨
            RunUpgrades.Reset();
            foreach (var g in d.growth)
                if (Enum.TryParse(g.stat, out RunStat stat) && Enum.TryParse(g.tier, out CardTier tier)) RunUpgrades.Add(stat, tier);
            if (Enum.TryParse(d.doctrine, out NavalDoctrine doctrine)) CombatPolicies.ChooseDoctrine(doctrine);
            RefitDraft.SetRerolls(d.rerolls);
            UnityEngine.Object.FindFirstObjectByType<ExperienceSystem>()?.Restore(d.level, d.xp);

            // 편대(시작 함정이 준 고속정은 치우고 저장대로)
            var fleet = ship.GetComponent<TaskForceEscortFormation>();
            if (fleet != null)
            {
                fleet.ClearAll();
                foreach (var e in d.escorts)
                {
                    int index = fleet.Deploy(e.slot);
                    if (index < 0) continue;
                    if (Enum.TryParse(e.role, out EscortRole role) && role != EscortRole.None)
                    {
                        fleet.AssignRole(index, role);
                        for (int t = 0; t < e.tier; t++) if (!fleet.Upgrade(index)) break;
                    }
                }
                if (Enum.TryParse(d.formation, out FleetFormation formation)) fleet.SetFormation(formation);
            }

            gm.RestoreRun(d.elapsed, d.kills);
            RestoreStats(d);
            RunSeed.Resume(d.seed, d.stage);
            RouteMap.Restore(d.seed, director.StageCount, d.routePath);
            director.DangerStartRun = d.dangerStart;
            director.MissionRoute = d.mission;
            director.ResumeAt(d.stage, d.danger);
            ship.RestoreHull(d.hull);
            Game.Combat.CombatLog.Add("저장", $"이어하기: {Describe(d)} · 블록 {d.modules.Count - LastSkippedModules}/{d.modules.Count}");
        }
    }
}

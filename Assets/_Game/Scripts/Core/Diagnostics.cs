using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 배포 기본(2026-10-11 점검 O07): 버전 표기와 오류 제보용 진단 내보내기.
    ///   - Version: "v0.1.0 · 빌드 1a2b3c4d · 2026-10-11" — 앱 버전(PlayerSettings) · 빌드 고유값(buildGUID 앞 8자) · 빌드 날짜(데이터 파일 시각).
    ///   - Export(): persistentDataPath/Diagnostics/diag_날짜.txt 한 파일에 버전·기기·화면/그래픽/음량/조작 설정·런 시드·지금 판 상태
    ///     (스테이지·레벨·함정·블록·편대·교리)·최근 전투 기록(개발 빌드)·로그 파일 끝부분을 모은다. 개인 정보(이름·경로 밖 파일)는 넣지 않는다.
    /// </summary>
    public static class Diagnostics
    {
        public static string Version
        {
            get
            {
                string guid = string.IsNullOrEmpty(Application.buildGUID) ? "에디터" : Application.buildGUID.Substring(0, Math.Min(8, Application.buildGUID.Length));
                string date = "";
                try
                {
                    var data = Path.Combine(Application.dataPath, "globalgamemanagers");
                    if (!Application.isEditor && File.Exists(data)) date = $" · {File.GetLastWriteTime(data):yyyy-MM-dd}";
                }
                catch { /* 날짜는 없어도 된다 */ }
                return $"v{Application.version} · 빌드 {guid}{date}";
            }
        }

        public static string Folder => Path.Combine(Application.persistentDataPath, "Diagnostics");

        /// <summary>진단 파일을 만들고 경로를 돌려준다. 실패하면 null과 이유.</summary>
        public static string Export(out string error)
        {
            error = null;
            try
            {
                Directory.CreateDirectory(Folder);
                string path = Path.Combine(Folder, $"diag_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(path, Build(), new UTF8Encoding(false));
                return path;
            }
            catch (Exception e)
            {
                error = e.Message;
                return null;
            }
        }

        private static string Build()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# NavalRoguelike 진단 — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"버전: {Version} · Unity {Application.unityVersion} · {Application.platform}");
            sb.AppendLine($"기기: {SystemInfo.operatingSystem} · CPU {SystemInfo.processorType} ({SystemInfo.processorCount}) · RAM {SystemInfo.systemMemorySize} MB");
            sb.AppendLine($"그래픽: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}, {SystemInfo.graphicsMemorySize} MB)");
            sb.AppendLine($"화면: {Screen.width}×{Screen.height} {Screen.fullScreenMode} · 주사율 {Screen.currentResolution.refreshRateRatio.value:0} Hz · 품질 {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
            sb.AppendLine($"설정: 수직 동기화 {DisplaySettings.VSync} · 프레임 제한 {DisplaySettings.FrameLimitName(DisplaySettings.FrameLimitIndex)} · UI {DisplaySettings.UiScale * 100f:0}% · " +
                          $"번쩍임 줄이기 {DisplaySettings.ReduceFlashes} · 적아 표식 {DisplaySettings.IdentityMarkers} · 항구 화질 {DisplaySettings.QualityNames[DisplaySettings.HarborQuality]} · " +
                          $"장식 효과 {DisplaySettings.QualityNames[DisplaySettings.EffectsQuality]} · 연기 옅게 {DisplaySettings.ThinSmoke}");
            sb.Append("음량:");
            foreach (SoundChannel c in Enum.GetValues(typeof(SoundChannel))) sb.Append($" {c} {GameSettings.Volume(c):0.##}");
            sb.AppendLine();
            sb.Append("조작:");
            foreach (NavalControl c in Enum.GetValues(typeof(NavalControl))) sb.Append($" {c}={GameSettings.BindingLabel(c)}");
            sb.AppendLine().AppendLine();

            sb.AppendLine($"## 지금 판 (런 시드 {RunSeed.Current})");
            var gm = GameManager.Instance;
            if (gm == null) sb.AppendLine("게임 관리자 없음");
            else
            {
                var ship = gm.Player;
                var director = UnityEngine.Object.FindFirstObjectByType<StageDirector>();
                var xp = UnityEngine.Object.FindFirstObjectByType<Game.Progression.ExperienceSystem>();
                var init = UnityEngine.Object.FindFirstObjectByType<Game.Ship.ShipInitializer>();
                sb.AppendLine($"상태 {gm.State} · 작전 {gm.ElapsedTime:0}초 · 격침 {gm.TotalKills} · 스테이지 {director?.StageNumber} · 위험 항로 {director?.DangerRoute} · 훈련 {director?.Training} · " +
                              $"레벨 {xp?.Level} ({xp?.Xp}/{xp?.XpToNext}) · 교리 {CombatPolicies.Doctrine} · 함정 {init?.SelectedConcept?.Id ?? "기본"}");
                if (ship != null)
                {
                    sb.AppendLine($"선체 {ship.HullHp:0}/{ship.HullMaxHp:0} · 속력 {ship.CurrentSpeed:0.0} · 위치 {ship.transform.position}");
                    sb.Append("블록:");
                    foreach (var m in ship.Grid.Modules)
                        if (m?.Definition != null) sb.Append($" {m.Definition.Id}@{m.Origin.X},{m.Origin.Z}r{m.RotationSteps}u{m.UpgradeLevel}{(m.IsDestroyed ? "(파괴)" : "")}");
                    sb.AppendLine();
                    var fleet = ship.GetComponent<Game.TaskForce.TaskForceEscortFormation>();
                    if (fleet != null)
                    {
                        sb.Append($"편대 {fleet.Formation}:");
                        for (int i = 0; i < fleet.EscortCount; i++) { var e = fleet.GetInfo(i); sb.Append($" {e.RosterSlot}:{e.Role}T{e.Tier}({e.Hull01 * 100f:0}%)"); }
                        sb.AppendLine();
                    }
                }
                sb.Append("성장 카드:");
                foreach (var (stat, tier) in Game.Modules.RunUpgrades.Cards) sb.Append($" {stat}/{tier}");
                sb.AppendLine();
            }

            sb.AppendLine().AppendLine("## 최근 전투 기록(개발 빌드만)");
            int n = 0;
            foreach (var e in Game.Combat.CombatLog.Entries) { sb.AppendLine(e.ToString()); n++; }
            if (n == 0) sb.AppendLine("(없음)");

            sb.AppendLine().AppendLine("## 로그 끝부분");
            try
            {
                string log = Application.consoleLogPath;
                if (!string.IsNullOrEmpty(log) && File.Exists(log))
                {
                    using var fs = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    long start = Math.Max(0, fs.Length - 64 * 1024);
                    fs.Seek(start, SeekOrigin.Begin);
                    using var reader = new StreamReader(fs, Encoding.UTF8);
                    // 사용자 폴더 이름은 지운다(경로에 계정 이름이 들어 있다)
                    string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    string text = reader.ReadToEnd();
                    if (!string.IsNullOrEmpty(home)) text = text.Replace(home, "~").Replace(home.Replace('\\', '/'), "~");
                    sb.AppendLine(text);
                }
                else sb.AppendLine("(로그 파일 없음)");
            }
            catch (Exception e) { sb.AppendLine($"(로그를 읽지 못함: {e.Message})"); }
            return sb.ToString();
        }
    }
}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using Game.Core;

namespace Game.Dev
{
    /// <summary>
    /// QA 결과표(2026-10-12 점검 O03). 보고서 머리에 검사 이름·빌드 출처(커밋·미커밋 변경)·환경·시드를 적고,
    /// -qaTable &lt;파일&gt;이 있으면 끝날 때 그 마크다운 표에 이 검사의 최신 결과를 고쳐 쓴다.
    ///   검사별 최신 결과: 검사마다 한 줄(같은 검사를 다시 돌리면 그 줄을 바꾼다). "기준" 칸은 이번 실행과 같은 커밋이면 최신,
    ///                    아니면 "예전 커밋" — 지금 코드로 다시 확인하지 않은 결과를 구별한다.
    ///   최근 실행 기록: 최근 40회를 새것부터.
    /// </summary>
    public partial class CombatVerificationRunner
    {
        [Tooltip("결과를 고쳐 쓸 QA 결과표(마크다운). -qaTable <파일>")]
        public string QaTablePath;

        private const string LatestHeader = "## 검사별 최신 결과";
        private const string HistoryHeader = "## 최근 실행 기록";
        private const int HistoryKeep = 40;

        private DateTime _startedAt;
        private string _firstFailure;
        private int _seedAtStart, _seedAtEnd;

        /// <summary>켜진 "~Only" 검사 이름(-bundleCOnly 등). 아무것도 없으면 "전체".</summary>
        public string ScenarioLabel
        {
            get
            {
                var names = new List<string>();
                foreach (var f in GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                    if (f.FieldType == typeof(bool) && f.Name != nameof(QuitWhenDone) && (bool)f.GetValue(this))
                        names.Add("-" + char.ToLowerInvariant(f.Name[0]) + f.Name.Substring(1));
                // 성능 측정은 길이·창 모드에 따라 다른 결과라 따로 줄을 둔다
                if (PerfOnly)
                {
                    if (!Mathf.Approximately(PerfMinutes, 3f)) names.Add($"-perfMinutes {PerfMinutes:0.#}");
                    names.Add(Application.isBatchMode ? "(배치 모드)" : "(창 모드)");
                }
                return names.Count > 0 ? string.Join(" ", names) : "전체";
            }
        }

        private static string RunEnvironment =>
            $"{SystemInfo.operatingSystem} · {SystemInfo.graphicsDeviceName} · {Screen.width}×{Screen.height} · Unity {Application.unityVersion}";

        private void WriteReportHeader()
        {
            _startedAt = DateTime.Now;
            _report.AppendLine($"- 검사: {ScenarioLabel}");
            _report.AppendLine($"- 빌드: {BuildStamp.Summary} · {Diagnostics.Version}");
            if (BuildStamp.DataSync != "") _report.AppendLine($"- 빌드 직전 데이터 동기화 검사: {BuildStamp.DataSync}");
            _report.AppendLine($"- 환경: {RunEnvironment}");
        }

        /// <summary>시드는 스테이지가 시작된 뒤에 정해진다 — 처음 대기 뒤에 적는다.</summary>
        private void WriteSeed()
        {
            _seedAtStart = RunSeed.Current;
            _report.AppendLine($"- 런 시드(시작): {(_seedAtStart != 0 ? _seedAtStart.ToString() : "출항 전")}");
        }

        private void NoteFailure(string message)
        {
            if (_firstFailure == null) _firstFailure = message;
        }

        private void WriteQaFooterAndTable()
        {
            var took = DateTime.Now - _startedAt;
            _seedAtEnd = RunSeed.Current;   // 검사가 출항·재출격·고정 시드를 거치므로 끝의 시드를 표에 남긴다
            _report.AppendLine($"- 런 시드(끝): {_seedAtEnd} · 소요: {(int)took.TotalMinutes}분 {took.Seconds}초");
            if (string.IsNullOrEmpty(QaTablePath)) return;
            try
            {
                UpdateQaTable(QaTablePath, took);
                _report.AppendLine($"- QA 결과표 갱신: {QaTablePath}");
            }
            catch (Exception e)
            {
                _report.AppendLine($"- QA 결과표를 고치지 못함: {e.Message}");
            }
        }

        private static string Cell(string s) => (s ?? "").Replace("|", "/").Replace("\r", " ").Replace("\n", " ").Trim();

        private void UpdateQaTable(string path, TimeSpan took)
        {
            var latest = new List<string[]>();   // 검사 | 결과 | 기준 | 실행 시각 | 커밋 | 미커밋 | 시드 | 소요 | 환경 | 첫 실패
            var history = new List<string>();
            if (File.Exists(path))
            {
                string section = null;
                foreach (var raw in File.ReadAllLines(path))
                {
                    string line = raw.TrimEnd();
                    if (line.StartsWith("## ")) { section = line; continue; }
                    if (!line.StartsWith("| ") || line.StartsWith("| 검사 |") || line.StartsWith("| 실행 시각 |")) continue;
                    if (section == LatestHeader)
                    {
                        var cells = line.Trim('|').Split('|');
                        if (cells.Length >= 10)
                        {
                            for (int i = 0; i < cells.Length; i++) cells[i] = cells[i].Trim();
                            latest.Add(cells);
                        }
                    }
                    else if (section == HistoryHeader) history.Add(line);
                }
            }

            string commit = BuildStamp.Commit;
            string dirty = BuildStamp.Dirty < 0 ? "?" : BuildStamp.Dirty.ToString();
            string scenario = ScenarioLabel;
            string result = _failures == 0 ? "통과" : $"실패 {_failures}";
            string when = _startedAt.ToString("yyyy-MM-dd HH:mm");
            string duration = $"{(int)took.TotalMinutes}:{took.Seconds:00}";
            var row = new[]
            {
                Cell(scenario), result, "최신", when, Cell(commit), dirty, _seedAtEnd != 0 ? _seedAtEnd.ToString() : "—", duration, Cell(RunEnvironment),
                Cell(_firstFailure ?? "—"),
            };
            latest.RemoveAll(r => r[0] == row[0]);
            latest.Add(row);
            // 기준: 이번 실행과 같은 커밋이면 최신, 아니면 예전 커밋에서 확인한 결과
            foreach (var r in latest) r[2] = r[4] == commit ? "최신" : "예전 커밋";
            latest.Sort((a, b) => string.CompareOrdinal(a[0], b[0]));

            history.Insert(0, $"| {when} | {Cell(scenario)} | {result} | {Cell(commit)}{(dirty != "0" ? $" +{dirty}" : "")} | {duration} |");
            if (history.Count > HistoryKeep) history.RemoveRange(HistoryKeep, history.Count - HistoryKeep);

            var sb = new StringBuilder();
            sb.AppendLine("# QA 결과표");
            sb.AppendLine();
            sb.AppendLine("자동 전투 검증(`CombatVerificationRunner`)을 `-qaTable <이 파일>`로 돌릴 때마다 고쳐 쓴다(점검 O03). 손으로 고치지 않는다.");
            sb.AppendLine("- **기준**: 가장 최근 실행과 같은 커밋이면 `최신`, 아니면 `예전 커밋` — 지금 코드로 다시 확인하지 않은 결과다.");
            sb.AppendLine("- **미커밋**: 빌드할 때 커밋 안 한 변경 파일 수(0이면 그 커밋 그대로). 커밋·미커밋은 빌드 옆 `build_stamp.txt`에서 읽는다.");
            sb.AppendLine("- 자세한 내용은 각 실행의 `report.md`(출력 폴더).");
            sb.AppendLine();
            sb.AppendLine(LatestHeader);
            sb.AppendLine();
            sb.AppendLine("| 검사 | 결과 | 기준 | 실행 시각 | 커밋 | 미커밋 | 시드(끝) | 소요 | 환경 | 첫 실패 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            foreach (var r in latest) sb.AppendLine($"| {string.Join(" | ", r)} |");
            sb.AppendLine();
            sb.AppendLine(HistoryHeader);
            sb.AppendLine();
            sb.AppendLine("| 실행 시각 | 검사 | 결과 | 커밋 | 소요 |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var h in history) sb.AppendLine(h);

            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, sb.ToString().Replace("\r\n", "\n"), new UTF8Encoding(false));
        }
    }
}
#endif

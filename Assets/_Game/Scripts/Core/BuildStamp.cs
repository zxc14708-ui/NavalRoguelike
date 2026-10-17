using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 빌드 출처(2026-10-12 점검 O03): 이 빌드가 어느 커밋에서, 커밋 안 한 변경이 몇 개 있는 상태로, 언제 만들어졌는지.
    /// 빌드 후처리(Editor/BuildStampWriter)가 실행 파일 옆에 build_stamp.txt(키=값 줄)를 쓰고, 게임은 그것을 읽는다.
    /// 에디터에서는 파일 대신 프로젝트 폴더의 git을 직접 읽는다. 검증 보고서·QA 결과표·진단 파일이 이 값을 쓴다.
    /// </summary>
    public static class BuildStamp
    {
        public const string FileName = "build_stamp.txt";

        private static Dictionary<string, string> s_values;

        public static string Get(string key)
        {
            Load();
            return s_values.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : null;
        }

        public static string Commit => Get("commit") ?? "알 수 없음";
        public static string Branch => Get("branch") ?? "?";
        /// <summary>커밋 안 한 변경 파일 수(모르면 -1).</summary>
        public static int Dirty => int.TryParse(Get("dirty"), out var n) ? n : -1;
        public static string BuiltAt => Get("built") ?? "";
        /// <summary>빌드 직전 데이터 동기화 검사 결과 요약(O05). 없으면 빈 문자열.</summary>
        public static string DataSync => Get("datasync") ?? "";

        /// <summary>"커밋 1a2b3c4 +미커밋 3 · naval-base-scene · 빌드 2026-10-12 21:30"</summary>
        public static string Summary =>
            $"커밋 {Commit}{(Dirty > 0 ? $" +미커밋 {Dirty}" : Dirty < 0 ? " (변경 여부 모름)" : "")} · {Branch}" +
            (BuiltAt != "" ? $" · 빌드 {BuiltAt}" : "");

        private static void Load()
        {
            if (s_values != null) return;
            s_values = new Dictionary<string, string>();
            try
            {
                string path = Path.Combine(Application.dataPath, "..", FileName);
                if (File.Exists(path))
                {
                    foreach (var line in File.ReadAllLines(path))
                    {
                        int eq = line.IndexOf('=');
                        if (eq > 0) s_values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                    }
                    return;
                }
#if UNITY_EDITOR
                foreach (var kv in Collect(Path.GetFullPath(Path.Combine(Application.dataPath, "..")))) s_values[kv.Key] = kv.Value;
                s_values["built"] = "에디터";
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BuildStamp] 읽지 못함: {e.Message}");
            }
        }

#if UNITY_EDITOR
        /// <summary>git 저장소에서 커밋·브랜치·미커밋 파일 수를 모은다. 저장소가 아니거나 git이 없으면 빈 값.</summary>
        public static Dictionary<string, string> Collect(string repo)
        {
            var values = new Dictionary<string, string>();
            string commit = Git(repo, "rev-parse --short HEAD");
            if (string.IsNullOrEmpty(commit)) return values;
            values["commit"] = commit;
            values["branch"] = Git(repo, "rev-parse --abbrev-ref HEAD") ?? "";
            string status = Git(repo, "status --porcelain");
            values["dirty"] = status == null ? "-1"
                : status.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Length.ToString();
            return values;
        }

        private static string Git(string repo, string args)
        {
            try
            {
                var info = new System.Diagnostics.ProcessStartInfo("git", $"-C \"{repo}\" {args}")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                };
                using var p = System.Diagnostics.Process.Start(info);
                if (p == null) return null;
                string output = p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(10000) || p.ExitCode != 0) return null;
                return output.Trim();
            }
            catch
            {
                return null;   // git이 없는 PC
            }
        }
#endif
    }
}

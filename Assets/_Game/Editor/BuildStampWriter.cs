using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Game.Core;

namespace Game.EditorTools
{
    /// <summary>
    /// 빌드가 끝나면 실행 파일 옆에 build_stamp.txt를 쓴다(2026-10-12 점검 O03): 커밋·브랜치·미커밋 파일 수·빌드 시각·
    /// Unity 버전·빌드 직전 데이터 동기화 검사 결과. 게임은 Game.Core.BuildStamp로 읽는다.
    /// 프로젝트 사본(검증용 미러)처럼 git 저장소가 아닌 곳에서 빌드하면 -gitRepo &lt;원본 프로젝트 폴더&gt;로 출처를 알려 준다.
    /// </summary>
    internal sealed class BuildStampWriter : IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
            try
            {
                string exe = report.summary.outputPath;
                string dir = Directory.Exists(exe) ? exe : Path.GetDirectoryName(exe);
                if (string.IsNullOrEmpty(dir)) return;

                string repo = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                    if (args[i] == "-gitRepo") repo = args[i + 1];

                var values = BuildStamp.Collect(repo);
                var sb = new StringBuilder();
                sb.AppendLine($"commit={(values.TryGetValue("commit", out var c) ? c : "")}");
                sb.AppendLine($"branch={(values.TryGetValue("branch", out var b) ? b : "")}");
                sb.AppendLine($"dirty={(values.TryGetValue("dirty", out var d) ? d : "-1")}");
                sb.AppendLine($"built={DateTime.Now:yyyy-MM-dd HH:mm}");
                sb.AppendLine($"unity={Application.unityVersion}");
                sb.AppendLine($"datasync={DataSyncCheck.LastSummary}");
                File.WriteAllText(Path.Combine(dir, BuildStamp.FileName), sb.ToString(), new UTF8Encoding(false));
                Debug.Log($"[BuildStamp] {Path.Combine(dir, BuildStamp.FileName)} · 커밋 {c} · 미커밋 {d}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BuildStamp] 쓰지 못함: {e.Message}");
            }
        }
    }
}

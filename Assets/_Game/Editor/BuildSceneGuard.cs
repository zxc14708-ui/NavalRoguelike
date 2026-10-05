using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace Game.EditorTools
{
    /// <summary>
    /// 빌드 씬 목록 지킴이(2026-10-05). 예전에는 URP 템플릿의 빈 SampleScene 하나만 목록에 있어서
    /// File > Build로 만들면 게임이 아니라 빈 씬이 나왔다(검증 빌드는 씬을 코드에서 직접 지정해 드러나지 않음).
    /// 에디터가 스크립트를 불러올 때마다 Prototype_Main이 첫 번째로 들어 있는지 보고, 없어진 씬 항목은 뺀다.
    /// 나중에 타이틀 씬 등을 추가해도 그대로 두고 메인 씬 순서만 맞춘다.
    /// </summary>
    [InitializeOnLoad]
    internal static class BuildSceneGuard
    {
        private const string MainScene = NavalEditorUtil.Root + "/Scenes/Prototype_Main.unity";

        static BuildSceneGuard() => EditorApplication.delayCall += Apply;

        private static void Apply()
        {
            if (!File.Exists(MainScene)) return;   // 셋업 전 빈 프로젝트
            var current = EditorBuildSettings.scenes;
            var scenes = new List<EditorBuildSettingsScene> { new(MainScene, true) };
            foreach (var s in current)
                if (s.path != MainScene && File.Exists(s.path)) scenes.Add(s);

            bool same = current.Length == scenes.Count;
            for (int i = 0; same && i < scenes.Count; i++)
                same = current[i].path == scenes[i].path && current[i].enabled == scenes[i].enabled;
            if (same) return;

            EditorBuildSettings.scenes = scenes.ToArray();
            UnityEngine.Debug.Log($"[빌드 설정] 빌드 씬 목록을 정리했습니다: {string.Join(", ", scenes.ConvertAll(s => s.path))}");
        }
    }
}

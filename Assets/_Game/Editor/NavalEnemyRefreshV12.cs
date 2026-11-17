using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Codex 적 리프레시 v12 적용(2026-10-13): 미사일정(EnemyMissileBoat)·엘리트 초계함(EnemyPccCorvette) 새 모델.
    /// 원본 `ArtSource/EnemyRefresh_v12/*.fbx`를 `Art/Models`의 같은 이름 FBX에 덮어쓰고(.meta는 그대로 — GUID·임포트 설정 유지),
    /// 두 적 프리팹만 NavalPrefabBuilder로 다시 만든다(발사대·포탑·포구 이름이 같아 그대로 연결된다).
    /// 다른 적·적 데이터·씬은 건드리지 않는다. 이후 셋업(Setup Prototype Scene)을 다시 돌려도 Art/Models의 v12로 만든다.
    /// 배치 실행: -executeMethod Game.EditorTools.NavalEnemyRefreshV12.Run
    /// </summary>
    public static class NavalEnemyRefreshV12
    {
        private const string Source = "ArtSource/EnemyRefresh_v12";
        private static readonly string[] Models = { "EnemyMissileBoat", "EnemyPccCorvette" };

        [MenuItem("Naval/Art/Apply Enemy Refresh v12 (Missile Boat, Elite Corvette)")]
        public static void Run()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            int copied = 0;
            foreach (var name in Models)
            {
                string from = Path.Combine(project, Source, name + ".fbx");
                string to = $"{NavalEditorUtil.Root}/Art/Models/{name}.fbx";
                if (!File.Exists(from)) { Debug.LogError($"[EnemyRefresh v12] 원본 없음: {from}"); return; }
                string target = Path.Combine(project, to);
                if (File.Exists(target) && FilesEqual(from, target)) continue;
                File.Copy(from, target, true);
                AssetDatabase.ImportAsset(to, ImportAssetOptions.ForceUpdate);
                copied++;
            }
            var (boat, pcc) = NavalPrefabBuilder.BuildMissileBoatAndPccOnly();
            Debug.Log($"[EnemyRefresh v12] 모델 {copied}개 바꿈 · 미사일정 {(boat != null ? "O" : "X")} · 엘리트 초계함 {(pcc != null ? "O" : "X")}");
        }

        private static bool FilesEqual(string a, string b)
        {
            var fa = new FileInfo(a);
            var fb = new FileInfo(b);
            if (fa.Length != fb.Length) return false;
            return System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(a), File.ReadAllBytes(b));
        }
    }
}

using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using static Game.EditorTools.NavalEditorUtil;

namespace Game.EditorTools
{
    /// <summary>
    /// 항공대 호출 기체 모델(2026-10-21) — Blender 스크립트로 만든 저폴리 모형을 가져온다.
    ///   - 전투기 F/A-18F 슈퍼호넷: ArtSource/SuperHornet_v1(build.py) → Resources/Aviation/AIR_SuperHornet
    ///   - 조기경보기 E-2D 호크아이: ArtSource/HawkeyeE2_v1(build.py) → Resources/Aviation/AIR_Hawkeye
    /// 순서:
    ///   1. 새 재질(Hornet Light Grey · Hornet Grey · Hornet Canopy · Hornet Dark · Hawkeye Light Grey)을 Art/Models/Materials에 만든다(있으면 색만 맞춘다).
    ///      나머지(Gunmetal · Medical White)는 기존 팔레트.
    ///   2. FBX를 Art/Models/Aviation으로 복사·임포트(축 변환 굽기, 재질은 이름으로 연결 — NavalPlayerAviationArt.ImportAndRemap).
    ///   3. Resources/Aviation 프리팹: 기수가 +Z, 기수·꼬리 가운데가 원점이 되게 놓는다(Nose/Tail 소켓으로 판정).
    /// 크기는 모델 그대로 — 두 기체 모두 실제 치수 × 3.4/18.3(전투기 길이 3.4 = 적 전투기·헬기와 같은 급, 시작 함선 15의 약 1/4).
    /// 런타임(FriendlyAircraft)이 프리팹을 불러 소켓에 비행운·항법등·노즐 불꽃·기총 위치를 붙이고, 호크아이의 로토돔·프로펠러를 돌린다.
    /// 배치 실행: -executeMethod Game.EditorTools.NavalAirWingArt.Run
    /// </summary>
    public static class NavalAirWingArt
    {
        private const string MaterialDir = Root + "/Art/Models/Materials";
        private static readonly (string source, string name)[] Models =
        {
            ("ArtSource/SuperHornet_v1/Models/AIR_SuperHornet.fbx", "AIR_SuperHornet"),
            ("ArtSource/HawkeyeE2_v1/Models/AIR_Hawkeye.fbx", "AIR_Hawkeye"),
        };

        private static readonly (string name, Color color, float smoothness)[] Palette =
        {
            ("Hornet Light Grey", new Color(0.60f, 0.64f, 0.68f), 0.35f),
            ("Hornet Grey", new Color(0.44f, 0.48f, 0.53f), 0.35f),
            ("Hornet Canopy", new Color(0.10f, 0.13f, 0.15f), 0.85f),
            ("Hornet Dark", new Color(0.16f, 0.18f, 0.20f), 0.30f),
            ("Hawkeye Light Grey", new Color(0.74f, 0.76f, 0.78f), 0.35f),
        };

        [MenuItem("Naval/Art/Apply Air Wing Models (Super Hornet · Hawkeye)", priority = 45)]
        public static void Run()
        {
            EnsureMaterials();
            foreach (var (source, name) in Models)
            {
                string from = Path.GetFullPath(Path.Combine(Application.dataPath, "..", source));
                if (!File.Exists(from)) { Debug.LogError($"[Air Wing] 원본 없음: {from}"); continue; }
                string modelPath = $"{Root}/Art/Models/Aviation/{name}.fbx";
                Directory.CreateDirectory(Path.GetDirectoryName(modelPath));
                File.Copy(from, modelPath, true);
                AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
                NavalPlayerAviationArt.ImportAndRemap(modelPath);
                BuildPrefab(modelPath, $"{Root}/Resources/Aviation/{name}.prefab", name);
            }
            AssetDatabase.SaveAssets();
        }

        private static void EnsureMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var (name, color, smoothness) in Palette)
            {
                string path = $"{MaterialDir}/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(lit) { name = name };
                    AssetDatabase.CreateAsset(m, path);
                }
                m.SetColor("_BaseColor", color);
                m.SetFloat("_Smoothness", smoothness);
                m.SetFloat("_Metallic", 0.1f);
                EditorUtility.SetDirty(m);
            }
        }

        private static void BuildPrefab(string modelPath, string prefabPath, string name)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) { Debug.LogError($"[Air Wing] 모델을 불러오지 못함: {modelPath}"); return; }
            var root = new GameObject(name);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            Transform nose = Find(inst.transform, "Nose"), tail = Find(inst.transform, "Tail");
            if (nose == null || tail == null) { Debug.LogError($"[Air Wing] {name}: Nose/Tail 소켓 없음"); Object.DestroyImmediate(root); return; }

            // 앞: 꼬리 → 기수가 +Z(수평). 가운데: 기수·꼬리 중점이 원점
            Vector3 forward = nose.position - tail.position;
            inst.transform.rotation = Quaternion.FromToRotation(forward.normalized, Vector3.forward) * inst.transform.rotation;
            inst.transform.position -= (nose.position + tail.position) * 0.5f;

            var sb = new StringBuilder($"[Air Wing] {name}: 길이 {(nose.position - tail.position).magnitude:0.00}");
            Transform l = Find(inst.transform, "WingtipL"), r = Find(inst.transform, "WingtipR");
            if (l != null && r != null) sb.Append($" · 날개끝 간격 {(l.position - r.position).magnitude:0.00} (L x {l.position.x:0.00}, R x {r.position.x:0.00})");
            var gun = Find(inst.transform, "GunPort");
            if (gun != null) sb.Append($" · 기총 {gun.position}");
            // 도는 부품의 축: 로토돔은 자기 위가 세계 위, 프로펠러는 자기 앞이 세계 앞이어야 런타임이 Space.Self로 돌린다
            var dome = Find(inst.transform, "Rotodome");
            if (dome != null) sb.Append($" · 로토돔 위축·세계 위 {Vector3.Dot(dome.up, Vector3.up):0.00} 지름 {dome.GetComponent<Renderer>().bounds.size.x:0.00}");
            var prop = Find(inst.transform, "PropL");
            if (prop != null) sb.Append($" · 프로펠러 앞축·세계 앞 {Vector3.Dot(prop.forward, Vector3.forward):0.00}");

            Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            Debug.Log(sb.ToString());
        }

        private static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = Find(c, name); if (f != null) return f; }
            return null;
        }
    }
}

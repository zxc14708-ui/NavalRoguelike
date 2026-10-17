using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Game.Combat;
using Game.Enemies;
using Game.Modules.Runtime;
using Game.WW2;
using static Game.EditorTools.NavalEditorUtil;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>
    /// Codex WWII 모델 적용(2026-10-17, `ArtSource/WWII_ScaleReview_v30` — v29 전체 14종에 플레이어 블록 비율 조정).
    /// 원본 FBX가 있으면 `Art/Models/WW2`로 복사·임포트(축 변환 굽기, 재질은 팔레트 이름 `WW2 Hull` 등으로 연결)하고,
    /// 각 프리팹을 회색 상자 대신 모델로 만든다 — 포탑·고각·포구·발사관·프로펠러·투하점·레이더·기관총 자리는 모델 소켓에 연결.
    /// 모델이 없으면 예전 회색 상자 그대로. 충돌 상자·레이어·수치·탄·이펙트는 예전과 같다.
    ///   - 방향: 앞 소켓(포구·뱃머리 포탑·프로펠러·ImpactPoint)이 뒤 소켓보다 +Z가 되게 90° 단위로 돌린다(위는 축 변환 굽기 그대로).
    ///   - 적 함정 모델은 원점이 설계 수면선이라 ModelPivot을 게임 수면(y −0.9)에 둔다.
    ///   - 고속정·포격함은 현대 프리팹(ENE_FastBoat·ENE_ArtilleryBoat)을 복제해 겉모습만 바꾼 WWII 전용 프리팹(ENE_Ww2FastGunboat·ENE_Ww2Gunboat)을 쓴다.
    ///     어뢰정은 아직 WWII 모델이 없어 현대 프리팹 그대로.
    /// </summary>
    public static partial class NavalWw2Builder
    {
        private const string ArtSourceDir = "ArtSource/WWII_ScaleReview_v30/Models";
        private const string ArtDir = Root + "/Art/Models/WW2";
        private const string ArtMaterialDir = Root + "/Art/Models/Materials/WW2";
        private const float WaterY = -0.9f;

        /// <summary>Codex 팔레트(ArtSource/WWII_ModePack_v29/common.py PALETTE와 같은 이름·색).</summary>
        private static readonly (string name, Color color, float metallic, float smooth)[] Palette =
        {
            ("WW2 Hull", new Color(.46f, .50f, .53f), .1f, .3f), ("WW2 Deck", new Color(.36f, .33f, .28f), 0f, .15f),
            ("WW2 Gun", new Color(.33f, .36f, .38f), .3f, .35f), ("WW2 Dark", new Color(.10f, .125f, .14f), .2f, .3f),
            ("WW2 Enemy", new Color(.40f, .41f, .44f), .1f, .3f), ("WW2 Enemy Deck", new Color(.30f, .28f, .25f), 0f, .15f),
            ("WW2 Olive", new Color(.34f, .38f, .26f), .1f, .3f), ("WW2 Air Dark", new Color(.18f, .20f, .16f), .1f, .25f),
            ("WW2 Air Belly", new Color(.51f, .56f, .53f), .1f, .3f), ("WW2 Glass", new Color(.035f, .13f, .17f), .1f, .8f),
            ("WW2 Brass", new Color(.62f, .45f, .19f), .7f, .5f), ("WW2 Orange", new Color(.73f, .39f, .13f), .1f, .35f),
            ("WW2 Enemy Red", new Color(.56f, .17f, .13f), .1f, .3f), ("WW2 White", new Color(.74f, .76f, .70f), 0f, .3f),
            ("WW2 Waterline", new Color(.17f, .12f, .12f), 0f, .2f), ("WW2 Red", new Color(.56f, .17f, .13f), .1f, .3f),
        };

        /// <summary>원본 FBX를 프로젝트로 복사·임포트한다. 원본 폴더가 없으면 아무것도 하지 않는다(회색 상자).</summary>
        private static void ImportArt()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string src = Path.Combine(project, ArtSourceDir);
            if (!Directory.Exists(src)) { Debug.Log($"[WW2] {ArtSourceDir} 없음 — 회색 상자 모델로 만듭니다"); return; }
            EnsureFolder(ArtMaterialDir);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            foreach (var (name, color, metallic, smooth) in Palette)
            {
                string path = $"{ArtMaterialDir}/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(shader) { name = name }; AssetDatabase.CreateAsset(mat, path); }
                mat.color = color;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
            EnsureFolder(ArtDir);
            foreach (var file in Directory.GetFiles(src, "*.fbx"))
            {
                string to = $"{ArtDir}/{Path.GetFileName(file)}";
                File.Copy(file, Path.Combine(project, to), true);
                AssetDatabase.ImportAsset(to, ImportAssetOptions.ForceUpdate);
                NavalPlayerAviationArt.ImportAndRemap(to);
            }
            Debug.Log($"[WW2] Codex 모델 {Directory.GetFiles(src, "*.fbx").Length}종 임포트 — {ArtSourceDir}");
        }

        private static GameObject Art(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{ArtDir}/{name}.fbx");

        private static Transform Deep(Transform root, string name) => NavalPlayerAviationArt.Deep(root, name);

        /// <summary>모델을 pivot 아래에 붙이고 front 소켓이 origin 소켓보다 +Z가 되게 돌린다(위는 그대로). ground면 바닥을 0에.</summary>
        private static Transform MountArt(GameObject art, Transform parent, string pivot, string front, string origin, bool ground)
            => NavalPlayerAviationArt.Mount(art, parent, pivot, 1f, "#", "#", front, origin, ground);

        /// <summary>복제한 프리팹 안의 모델 프리팹을 풀고 모든 메시 렌더러를 걷어 낸다(파티클·항적·선은 남김).</summary>
        private static void StripMeshes(GameObject root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root.transform && PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))
                    PrefabUtility.UnpackPrefabInstance(t.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>();
                Object.DestroyImmediate(r);
                if (mf != null) Object.DestroyImmediate(mf);
            }
        }

        /// <summary>탄·어뢰·폭탄: 원래 메시 대신 모델(앞 = ImpactPoint). 모델이 없으면 그대로.</summary>
        private static void SkinProjectile(GameObject root, string artName)
        {
            var art = Art(artName);
            if (art == null) return;
            StripMeshes(root);
            var model = MountArt(art, root.transform, "ArtPivot", "ImpactPoint", "Exhaust", false);
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            SetLayerRecursive(model.gameObject, root.layer);
        }

        // ------------------------------------------------------------ 블록

        private static GameObject BuildDualGunArt(GameObject art, GameObject shell, GameObject flash)
        {
            var root = new GameObject("MOD_Ww2DualGun");
            var model = MountArt(art, root.transform, "ModelPivot", "Muzzle", "ElevationPivot", true);
            var comp = root.AddComponent<Ww2DualPurposeGunModule>();
            Configure(comp, so =>
            {
                Set(so, "weapon.turret", Deep(model, "TurretPivot"));
                Set(so, "weapon.elevationPivot", Deep(model, "ElevationPivot"));
                Set(so, "weapon.muzzle", Deep(model, "Muzzle"));
                Set(so, "weapon.minElevation", -5f);
                Set(so, "weapon.maxElevation", 70f);
                Set(so, "weapon.aimTolerance", 4f);
                Set(so, "projectilePrefab", shell);
                Set(so, "muzzleFlash", flash);
                Set(so, "muzzleA", Deep(model, "Muzzle_01"));
                Set(so, "muzzleB", Deep(model, "Muzzle_02"));
            });
            return Save(root);
        }

        private static GameObject BuildQuadAaArt(GameObject art, GameObject round)
        {
            var root = new GameObject("MOD_Ww2QuadAa");
            var model = MountArt(art, root.transform, "ModelPivot", "Muzzle", "ElevationPivot", true);
            var muzzles = new List<Transform>();
            for (int i = 1; i <= 4; i++) { var m = Deep(model, $"Muzzle_{i:00}"); if (m != null) muzzles.Add(m); }
            var comp = root.AddComponent<Ww2QuadAaModule>();
            Configure(comp, so =>
            {
                Set(so, "weapon.turret", Deep(model, "TurretPivot"));
                Set(so, "weapon.elevationPivot", Deep(model, "ElevationPivot"));
                Set(so, "weapon.muzzle", Deep(model, "Muzzle"));
                Set(so, "weapon.minElevation", -5f);
                Set(so, "weapon.maxElevation", 85f);
                Set(so, "weapon.aimTolerance", 6f);
                Set(so, "projectilePrefab", round);
                SetArray(so, "muzzles", muzzles.ToArray());
            });
            return Save(root);
        }

        private static GameObject BuildTorpedoMountArt(GameObject art, GameObject torpedo)
        {
            var root = new GameObject("MOD_Ww2TorpedoMount");
            var model = MountArt(art, root.transform, "ModelPivot", "LaunchPoint_01", "TubeMount", true);
            var comp = root.AddComponent<Ww2BroadsideTorpedoModule>();
            Configure(comp, so =>
            {
                Set(so, "torpedoPrefab", torpedo);
                Set(so, "mount", Deep(model, "TubeMount"));
            });
            return Save(root);
        }

        /// <summary>함교 모델을 복제한 초계함 함교 안에 붙인다(레이더·기관총 자리 소켓).</summary>
        private static void MountBridgeArt(GameObject art, GameObject root)
        {
            var model = MountArt(art, root.transform, "Ww2Visual", "BridgeGunMount", "LaunchPoint_01", true);
            var bridge = root.GetComponent<BridgeModule>();
            Configure(bridge, so =>
            {
                Set(so, "radarAntenna", Deep(model, "RadarPivot"));
                Set(so, "launchPoint", Deep(model, "LaunchPoint_01"));
            });
        }

        // ------------------------------------------------------------ 적

        private static GameObject BuildTorpedoBomberArt(GameObject art, GameObject torpedo)
        {
            var root = Plane("ENE_Ww2TorpedoBomber", out var R, new Vector3(3.6f, 1f, 2.8f));
            var model = MountArt(art, R, "ModelPivot", "Propeller", "TorpedoDropPoint", false);
            var spin = root.AddComponent<Ww2PropellerSpin>();
            Configure(spin, so => Set(so, "propeller", Deep(model, "Propeller")));
            var comp = root.AddComponent<Ww2TorpedoBomber>();
            Configure(comp, so =>
            {
                Set(so, "torpedoPrefab", torpedo);
                Set(so, "cruiseAltitude", 9f);
            });
            SetLayerRecursive(root, LayerEnemy);
            return Save(root);
        }

        private static GameObject BuildDiveBomberArt(GameObject art, GameObject bomb, GameObject marker)
        {
            var root = Plane("ENE_Ww2DiveBomber", out var R, new Vector3(3.2f, 1.1f, 2.6f));
            var model = MountArt(art, R, "ModelPivot", "Propeller", "BombDropPoint", false);
            var spin = root.AddComponent<Ww2PropellerSpin>();
            Configure(spin, so => Set(so, "propeller", Deep(model, "Propeller")));
            var comp = root.AddComponent<Ww2DiveBomber>();
            Configure(comp, so =>
            {
                Set(so, "bombPrefab", bomb);
                Set(so, "warningMarker", marker);
                Set(so, "cruiseAltitude", 14f);
                Set(so, "dropPoint", Deep(model, "BombDropPoint"));
            });
            SetLayerRecursive(root, LayerEnemy);
            return Save(root);
        }

        private static GameObject BuildCruiserArt(GameObject art, GameObject shell, GameObject secondaryRound, GameObject flash)
        {
            var root = new GameObject("ENE_Ww2GunneryCruiser");
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(3.8f, 3f, 18f);
            col.center = new Vector3(0f, 0.6f, 0f);
            col.isTrigger = true;
            var model = MountArt(art, root.transform, "ModelPivot", "A_TurretPivot", "X_TurretPivot", false);
            model.parent.localPosition = new Vector3(0f, WaterY, 0f);   // 모델 원점 = 설계 수면선
            var turrets = new List<Transform>();
            var muzzles = new List<Transform>();
            foreach (var p in new[] { "A_", "B_", "X_" })
            {
                var t = Deep(model, p + "TurretPivot");
                if (t != null) turrets.Add(t);
                for (int i = 1; i <= 2; i++) { var m = Deep(model, $"{p}Muzzle_{i:00}"); if (m != null) muzzles.Add(m); }
            }
            var secondary = new List<Transform>();
            foreach (var n in new[] { "Secondary_P_Muzzle", "Secondary_S_Muzzle" }) { var m = Deep(model, n); if (m != null) secondary.Add(m); }
            var comp = root.AddComponent<Ww2GunneryBoss>();
            Configure(comp, so =>
            {
                SetArray(so, "turrets", turrets.ToArray());
                SetArray(so, "muzzles", muzzles.ToArray());
                Set(so, "shellPrefab", shell);
                Set(so, "muzzleFlash", flash);
                SetArray(so, "secondaryMuzzles", secondary.ToArray());
                Set(so, "secondaryProjectile", secondaryRound);
                Set(so, "shellsPhase1", 6);
            });
            SetLayerRecursive(root, LayerEnemy);
            return Save(root);
        }

        /// <summary>WWII 소형 포함·포격함: 현대 프리팹(행동·탄·충돌)을 복제해 겉모습만 Codex 모델로. 모델이 없으면 null(현대 프리팹을 쓴다).</summary>
        private static GameObject BuildGunboat(bool fast)
        {
            var art = Art(fast ? "ENE_Ww2FastGunboat" : "ENE_Ww2Gunboat");
            if (art == null) return null;
            string src = fast ? "Prefabs/Enemies/ENE_FastBoat.prefab" : "Prefabs/Enemies/ENE_ArtilleryBoat.prefab";
            return Clone(src, fast ? "ENE_Ww2FastGunboat" : "ENE_Ww2Gunboat", root =>
            {
                StripMeshes(root);
                var old = root.transform.Find("ModelPivot");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var model = MountArt(art, root.transform, "ModelPivot", "TurretPivot", "Aft_TurretPivot", false);
                model.parent.localPosition = new Vector3(0f, WaterY, 0f);
                var box = root.GetComponent<BoxCollider>();
                if (box != null) box.size = fast ? new Vector3(1.7f, 1.6f, 5.6f) : new Vector3(2.2f, 1.7f, 8.4f);
                var enemy = root.GetComponent<EnemyController>();
                Configure(enemy, so =>
                {
                    Set(so, "weapon.turret", Deep(model, "TurretPivot"));
                    Set(so, "weapon.elevationPivot", Deep(model, "ElevationPivot"));
                    Set(so, "weapon.muzzle", Deep(model, "Muzzle"));
                });
                SetLayerRecursive(root, LayerEnemy);
            });
        }
    }
}

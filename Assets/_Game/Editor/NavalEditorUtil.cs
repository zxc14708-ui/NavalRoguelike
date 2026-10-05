using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Game.EditorTools
{
    /// <summary>
    /// 셋업 스크립트용 공용 헬퍼.
    /// 게임플레이 코드의 필드는 전부 private이므로 SerializedObject로 값을 넣는다.
    /// </summary>
    public static class NavalEditorUtil
    {
        public const string Root = "Assets/_Game";

        // --- 레이어 인덱스 (Projectile 마스크와 충돌 분리에 사용)
        public const int LayerPlayerShip = 6;
        public const int LayerEnemy = 7;
        public const int LayerEnemyMissile = 8;
        public const int LayerPlayerMissile = 9;
        public const int LayerDecoy = 10;

        // 정비 화면 카메라가 함선만 비추기 위한 레이어
        public const int LayerShip = 11;

        /// <summary>프로젝트 설정에 필요한 레이어를 만든다. 이미 있으면 건드리지 않는다.</summary>
        public static void EnsureLayers()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[Setup] TagManager.asset을 열 수 없습니다.");
                return;
            }

            var tm = new SerializedObject(assets[0]);
            var layers = tm.FindProperty("layers");

            SetLayer(layers, LayerPlayerShip, "PlayerShip");
            SetLayer(layers, LayerEnemy, "Enemy");
            SetLayer(layers, LayerEnemyMissile, "EnemyMissile");
            SetLayer(layers, LayerPlayerMissile, "PlayerMissile");
            SetLayer(layers, LayerDecoy, "Decoy");
            SetLayer(layers, LayerShip, "Ship");

            tm.ApplyModifiedProperties();
        }

        private static void SetLayer(SerializedProperty layers, int index, string name)
        {
            if (index >= layers.arraySize) return;
            var sp = layers.GetArrayElementAtIndex(index);
            if (sp.stringValue != name) sp.stringValue = name;
        }

        /// <summary>"Assets/_Game/Data/Modules" 같은 경로를 통째로 만든다.</summary>
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>ScriptableObject 에셋을 만들거나 기존 것을 덮어쓴다.</summary>
        public static T CreateSO<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var so = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(so, path);
            return so;
        }

        /// <summary>private 직렬화 필드에 값을 넣는다. 타입은 프로퍼티 종류를 보고 자동 변환한다.</summary>
        public static void Set(SerializedObject so, string path, object value)
        {
            var p = so.FindProperty(path);
            if (p == null)
            {
                Debug.LogError($"[Setup] 필드를 찾지 못했습니다: {path} ({so.targetObject.name})");
                return;
            }

            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer:   p.intValue = Convert.ToInt32(value); break;
                case SerializedPropertyType.Float:     p.floatValue = Convert.ToSingle(value); break;
                case SerializedPropertyType.Boolean:   p.boolValue = Convert.ToBoolean(value); break;
                case SerializedPropertyType.String:    p.stringValue = value as string; break;
                case SerializedPropertyType.Enum:      p.enumValueIndex = Convert.ToInt32(value); break;
                case SerializedPropertyType.LayerMask: p.intValue = Convert.ToInt32(value); break;
                case SerializedPropertyType.Color:     p.colorValue = (Color)value; break;
                case SerializedPropertyType.Vector3:   p.vector3Value = (Vector3)value; break;
                case SerializedPropertyType.ObjectReference:
                    p.objectReferenceValue = value as UnityEngine.Object; break;
                default:
                    Debug.LogError($"[Setup] 지원하지 않는 필드 타입: {path} ({p.propertyType})");
                    break;
            }
        }

        /// <summary>오브젝트 참조 배열/리스트를 채운다.</summary>
        public static void SetArray(SerializedObject so, string path, params UnityEngine.Object[] items)
        {
            var p = so.FindProperty(path);
            if (p == null) { Debug.LogError($"[Setup] 배열 필드 없음: {path}"); return; }

            p.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        /// <summary>컴포넌트의 private 필드들을 한 번에 설정한다.</summary>
        public static void Configure(Component c, Action<SerializedObject> body)
        {
            var so = new SerializedObject(c);
            body(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Configure(ScriptableObject o, Action<SerializedObject> body)
        {
            var so = new SerializedObject(o);
            body(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(o);
        }

        // --- 그레이박스용 머티리얼

        private static Shader _litShader;

        /// <summary>URP Lit 머티리얼을 만들어 에셋으로 저장한다.</summary>
        public static Material CreateMaterial(string name, Color color, float metallic = 0f, float smoothness = 0.2f)
        {
            string path = $"{Root}/Art/MAT_{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            if (_litShader == null)
                _litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            var mat = new Material(_litShader) { name = name };
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>
        /// 가장자리가 부드럽게 사라지는 원형 텍스처를 쓴 파티클 머티리얼.
        /// 기본 파티클은 네모로 보여 물보라처럼 보이지 않는다.
        /// </summary>
        public static Material CreateSoftParticleMaterial(string name)
        {
            string path = $"{Root}/Art/MAT_{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            string texPath = $"{Root}/Art/TEX_soft_circle.asset";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null)
            {
                const int size = 64;
                tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "soft_circle",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };

                float half = (size - 1) * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                        float a = Mathf.Clamp01(1f - d);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                    }
                }
                tex.Apply();
                AssetDatabase.CreateAsset(tex, texPath);
            }

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var mat = new Material(shader) { name = name, mainTexture = tex };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>
        /// 정점 색과 알파를 그대로 쓰는 머티리얼. TrailRenderer의 색 그라데이션이 적용되려면 필요하다.
        /// </summary>
        public static Material CreateVertexColorMaterial(string name)
        {
            string path = $"{Root}/Art/MAT_{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>
        /// 반투명·양면·그림자 없는 URP Unlit 머티리얼. 정비 화면 표시용.
        /// Lit 기본 머티리얼은 알파를 무시해 불투명하게 칠해진다.
        /// </summary>
        public static Material CreateTransparentMaterial(string name, Color color)
        {
            string path = $"{Root}/Art/MAT_{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader) { name = name };

            mat.SetFloat("_Surface", 1f);                       // Transparent
            mat.SetFloat("_Blend", 0f);                         // Alpha
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 0f);                          // 양면
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>
        /// 모델 인스턴스의 크기를 원하는 배율로 고정한다.
        ///
        /// 메시 정점은 규격상 이미 미터 단위다(§MODELING_SPEC 3장).
        /// 그런데 Blender에서 "Apply Scalings: FBX All"로 내보내면 루트 노드에
        /// 100배 스케일이 남아 그대로 쓰면 100배 커진다.
        /// 임포트된 스케일이 얼마든 무시하고 원하는 배율로 덮어써서 크기를 확정한다.
        /// </summary>
        public static void NormalizeScale(GameObject modelAsset, Transform instance, float gameplayScale)
        {
            if (instance == null) return;

            if (modelAsset != null)
            {
                float imported = modelAsset.transform.localScale.x;
                if (Mathf.Abs(imported - 1f) > 0.01f)
                    Debug.Log($"[Setup] {modelAsset.name}: 루트 스케일 {imported}를 {gameplayScale}로 교정했습니다. " +
                              "내보내기 설정을 'Apply Scalings: All Local'로 바꾸면 이 보정이 필요 없습니다.");
            }

            instance.localScale = Vector3.one * gameplayScale;
        }

        /// <summary>정비 화면용 렌더 텍스처를 만든다.</summary>
        public static RenderTexture CreateRenderTexture(string name, int width, int height)
        {
            string path = $"{Root}/Art/RT_{name}.renderTexture";
            var existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
            if (existing != null) return existing;

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = name,
                antiAliasing = 2,
                filterMode = FilterMode.Bilinear,
            };

            AssetDatabase.CreateAsset(rt, path);
            return rt;
        }

        /// <summary>
        /// FBX 임포트 설정을 고정하고, 모델이 쓰는 머티리얼을 URP 머티리얼로 연결한다.
        /// 이렇게 하지 않으면 빌트인 셰이더로 임포트돼 URP에서 분홍색으로 나온다.
        /// </summary>
        public static GameObject ImportModel(string assetPath, (string name, Material mat)[] materials)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[Setup] 모델을 찾지 못했습니다: {assetPath}");
                return null;
            }

            importer.useFileScale = true;
            importer.globalScale = 1f;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.isReadable = false;
            importer.addCollider = false;

            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.External;
            // 모델과 함께 들어온 .mat 을 이름으로 찾아 연결한다
            importer.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName,
                                             ModelImporterMaterialSearch.Everywhere);

            // 이전 실행에서 남은 불필요한 이름 매핑을 제거한 뒤 현재 팔레트만 기록한다.
            var oldRemaps = new List<AssetImporter.SourceAssetIdentifier>(importer.GetExternalObjectMap().Keys);
            foreach (var id in oldRemaps) importer.RemoveRemap(id);

            foreach (var (name, mat) in materials)
            {
                if (mat == null) continue;
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), mat);
            }

            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        }

        /// <summary>
        /// 한글이 나오는 TMP 폰트 에셋을 준비한다.
        /// 기본 LiberationSans에는 한글 글리프가 없어 UI가 전부 네모로 보인다.
        /// 시스템 맑은 고딕을 프로젝트로 복사해 동적 아틀라스 폰트로 만든다.
        /// </summary>
        public static TMP_FontAsset EnsureKoreanFont()
        {
            string dir = $"{Root}/Art/Fonts";
            EnsureFolder(dir);

            string ttfPath = $"{dir}/malgun.ttf";
            if (!File.Exists(ttfPath))
            {
                string system = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", "malgun.ttf");

                if (!File.Exists(system))
                {
                    Debug.LogWarning("[Setup] 맑은 고딕을 찾지 못했습니다. UI 한글이 네모로 보일 수 있습니다.");
                    return null;
                }

                File.Copy(system, ttfPath, true);
                AssetDatabase.ImportAsset(ttfPath, ImportAssetOptions.ForceSynchronousImport);
            }

            string assetPath = $"{dir}/KoreanFont SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null)
            {
                Debug.LogWarning($"[Setup] 폰트를 임포트하지 못했습니다: {ttfPath}");
                return null;
            }

            // 동적 모드라 필요한 글자를 실행 중에 아틀라스로 굽는다. 한글 전체를 미리 넣을 필요가 없다.
            var fontAsset = TMP_FontAsset.CreateFontAsset(
                font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);

            if (fontAsset == null)
            {
                Debug.LogWarning("[Setup] TMP 폰트 에셋 생성에 실패했습니다.");
                return null;
            }

            fontAsset.name = "KoreanFont SDF";
            AssetDatabase.CreateAsset(fontAsset, assetPath);

            // 아틀라스 텍스처와 머티리얼은 폰트 에셋의 하위 에셋으로 넣어야 저장된다
            if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0)
            {
                fontAsset.atlasTextures[0].name = "KoreanFont Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            }
            if (fontAsset.material != null)
            {
                fontAsset.material.name = "KoreanFont Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            AssetDatabase.SaveAssets();
            return fontAsset;
        }

        /// <summary>
        /// 반복 가능한 체커 텍스처를 만든다.
        /// 단색 바다 위에서는 함선이 움직여도 화면상 변화가 없어 이동을 확인할 수 없다.
        /// </summary>
        public static Texture2D CreateCheckerTexture(string name, Color a, Color b, int size = 64)
        {
            string path = $"{Root}/Art/TEX_{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
            };

            int half = size / 2;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool onLine = x < 2 || y < 2;                       // 격자선
                    bool checker = (x < half) ^ (y < half);             // 체커 무늬
                    tex.SetPixel(x, y, onLine ? Color.Lerp(a, Color.white, 0.25f)
                                              : (checker ? a : b));
                }
            }
            tex.Apply();

            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }

        /// <summary>기본 도형 하나를 만든다. Collider는 기본적으로 제거한다.</summary>
        public static GameObject Primitive(string name, PrimitiveType shape, Vector3 scale,
                                           Material mat, Transform parent = null, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.localScale = scale;

            if (parent != null)
            {
                go.transform.SetParent(parent, false);
                go.transform.localPosition = Vector3.zero;
            }

            if (!keepCollider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);
            }

            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>임시 GameObject를 프리팹으로 저장하고 씬에서 지운다.</summary>
        public static GameObject SavePrefab(GameObject go, string folder)
        {
            string path = $"{folder}/{go.name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>자기 자신과 모든 자식의 레이어를 바꾼다.</summary>
        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
        }
    }
}

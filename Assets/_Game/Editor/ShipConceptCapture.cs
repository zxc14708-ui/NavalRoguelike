using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>
    /// Real Unity render captures for four starting-ship concepts.
    /// Lists are ordered PATROL, COMMAND, ASSAULT, MISSILE; originals are never modified.
    /// Call after models and prefab meshes exist, from a graphics-enabled editor batch:
    /// ShipConceptCapture.CaptureAll(expandedShips, startShips, bridgeVisuals, outputDir).
    /// Do not pass -nographics. Capture files are ordinary PNGs, outside AssetDatabase.
    /// </summary>
    public static class ShipConceptCapture
    {
        private const int CaptureLayer = 31;
        private const int Width = 1920;
        private const int Height = 1280;
        private static readonly Vector3 Stage = new(5000f, 100f, 5000f);
        private static readonly string[] Names = { "PATROL", "COMMAND", "ASSAULT", "MISSILE" };
        private static readonly string[] Roles =
        {
            "COASTAL MULTIROLE / BALANCED START",
            "ESCORT CONTROL / DEFENSIVE FLAGSHIP",
            "FAST BALLISTIC STRIKE / CLOSE ENGAGEMENT",
            "GUIDED SALVO / LONG-RANGE FIRE CONTROL"
        };
        private static readonly Color[] Accents =
        {
            new(0.16f, 0.72f, 0.84f), new(0.35f, 0.88f, 0.67f),
            new(0.98f, 0.49f, 0.24f), new(0.51f, 0.66f, 1f)
        };

        public static void CaptureAll(List<GameObject> expandedShips, List<GameObject> startShips,
                                      List<GameObject> bridgeVisuals, string outputDir)
        {
            Validate(expandedShips, nameof(expandedShips));
            Validate(startShips, nameof(startShips));
            Validate(bridgeVisuals, nameof(bridgeVisuals));
            if (string.IsNullOrWhiteSpace(outputDir)) throw new ArgumentException("Capture output directory is empty.");
            Directory.CreateDirectory(outputDir);

            using var stage = new CaptureStage();
            for (int i = 0; i < 4; i++)
            {
                stage.Single(startShips[i], i, "STARTING LOADOUT / BASE PERFORMANCE", false, false,
                    Path.Combine(outputDir, $"start_{i + 1:00}_{Names[i].ToLowerInvariant()}.png"));
                stage.Single(expandedShips[i], i, "EXPANDED LOADOUT", false, false,
                    Path.Combine(outputDir, $"ship_{i + 1:00}_{Names[i].ToLowerInvariant()}.png"));
                stage.Single(bridgeVisuals[i], i, "BRIDGE DESIGN", true, false,
                    Path.Combine(outputDir, $"bridge_{i + 1:00}_{Names[i].ToLowerInvariant()}.png"));
                stage.Single(bridgeVisuals[i], i, "SIDE / ATTACHMENT CHECK", true, false,
                    Path.Combine(outputDir, $"bridge_side_{i + 1:00}_{Names[i].ToLowerInvariant()}.png"), true);
                stage.Single(expandedShips[i], i, "DECK LAYOUT / BOW UP", false, true,
                    Path.Combine(outputDir, $"layout_{i + 1:00}_{Names[i].ToLowerInvariant()}.png"));
            }
            stage.Lineup(startShips, "STARTING FLEET", "MINIMAL LOADOUT / ROOM TO GROW", false,
                Path.Combine(outputDir, "fleet_01_starting.png"));
            stage.Lineup(expandedShips, "EXPANDED FLEET", "ROLE-FOCUSED LOADOUT / UPGRADE TARGET", false,
                Path.Combine(outputDir, "fleet_02_expanded.png"));
            stage.Lineup(bridgeVisuals, "FOUR BRIDGES", "DISTINCT SILHOUETTES / SHARED MODULAR FOOTPRINT", true,
                Path.Combine(outputDir, "fleet_03_bridges.png"));
            Debug.Log($"[ShipConceptCapture] Wrote 19 native Unity captures to {outputDir}");
        }

        /// <summary>
        /// Captures the current live ship visual root after ShipInitializer/ModuleFactory have installed its loadout.
        /// Pass PlayerShip/Model, rather than PlayerShip, to avoid cloning the ship controller and game systems.
        /// </summary>
        public static void CaptureLiveStartingShip(GameObject shipVisual, int kind, string outputDir)
        {
            if (shipVisual == null) throw new ArgumentNullException(nameof(shipVisual));
            if (kind < 0 || kind >= 4) throw new ArgumentOutOfRangeException(nameof(kind));
            if (string.IsNullOrWhiteSpace(outputDir)) throw new ArgumentException("Capture output directory is empty.");
            Directory.CreateDirectory(outputDir);
            using var stage = new CaptureStage();
            stage.Single(shipVisual, kind, "LIVE START / INSTALLED LOADOUT", false, false,
                Path.Combine(outputDir, $"live_start_{kind + 1:00}_{Names[kind].ToLowerInvariant()}.png"));
        }

        /// <summary>
        /// Renders an actual live UI canvas, including its existing native preview texture.
        /// Temporarily uses an isolated camera and restores the canvas, scaler and child layers.
        /// This captures the UI that was exercised by verification, rather than recreating a mockup.
        /// </summary>
        public static void CaptureCanvas(Canvas canvas, string path)
        {
            if (canvas == null) throw new ArgumentNullException(nameof(canvas));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Capture path is empty.");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            const int uiWidth = 1920, uiHeight = 1080, uiLayer = 29;
            var originalMode = canvas.renderMode;
            var originalCamera = canvas.worldCamera;
            float originalDistance = canvas.planeDistance;
            float originalScale = canvas.scaleFactor;
            var scaler = canvas.GetComponent<CanvasScaler>();
            bool scalerEnabled = scaler != null && scaler.enabled;
            var transforms = canvas.GetComponentsInChildren<Transform>(true);
            var layers = new int[transforms.Length];
            for (int i = 0; i < transforms.Length; i++) layers[i] = transforms[i].gameObject.layer;
            var cameraObject = new GameObject("Live UI verification capture camera") { hideFlags = HideFlags.DontSave };
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.008f, .025f, .035f);
            camera.cullingMask = 1 << uiLayer;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 10f;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.transform.position = new Vector3(7000f, 100f, 7000f);
            var target = new RenderTexture(new RenderTextureDescriptor(uiWidth, uiHeight, RenderTextureFormat.ARGB32, 24)
            { msaaSamples = 4, sRGB = true });
            target.Create();
            camera.targetTexture = target;
            camera.aspect = uiWidth / (float)uiHeight;
            var originalActive = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                // Match the HUD's 1920x1080 reference layout independently of batch GameView size.
                if (scaler != null) scaler.enabled = false;
                canvas.scaleFactor = 1f;
                for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = uiLayer;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(uiWidth, uiHeight, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0f, 0f, uiWidth, uiHeight), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                Debug.Log($"[ShipConceptCapture] {Path.GetFileName(path)} — actual live canvas");
            }
            finally
            {
                RenderTexture.active = originalActive;
                canvas.renderMode = originalMode;
                canvas.worldCamera = originalCamera;
                canvas.planeDistance = originalDistance;
                canvas.scaleFactor = originalScale;
                for (int i = 0; i < transforms.Length; i++)
                    if (transforms[i] != null) transforms[i].gameObject.layer = layers[i];
                if (scaler != null) scaler.enabled = scalerEnabled;
                Canvas.ForceUpdateCanvases();
                if (pixels != null) Object.DestroyImmediate(pixels);
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
            }
        }

        /// <summary>Mark effectsImplemented only after the live block behaviors have passed verification.</summary>
        public static void CaptureBlockBoard(List<GameObject> blocks, string outputDir, bool effectsImplemented = false)
        {
            Validate(blocks, nameof(blocks));
            if (string.IsNullOrWhiteSpace(outputDir)) throw new ArgumentException("Capture output directory is empty.");
            Directory.CreateDirectory(outputDir);
            using var stage = new CaptureStage();
            stage.ContactBoard(blocks, effectsImplemented ? "SUPPORT BLOCKS" : "CLASS BLOCK CONCEPTS",
                effectsImplemented ? "OPERATIONAL SUPPORT / UPGRADE CARDS" : "ART PROTOTYPE / EFFECTS PENDING", true,
                new[] { "FLEET RELAY", "TURBO INTAKE", "FIRE CONTROL", "MISSILE LOGISTICS" },
                new[] { "COMMAND LINK / ESCORT COORDINATION", "INTAKE + EXHAUST / FAST HULL",
                        "TRACKING ARRAY / GUIDED SALVO", "CANISTER SERVICE / RELOAD SYSTEM" },
                Path.Combine(outputDir, "blocks_01_new.png"));
        }

        private static void Validate(List<GameObject> list, string name)
        {
            if (list == null || list.Count != 4) throw new ArgumentException($"{name} must contain four objects.");
            foreach (var item in list) if (item == null) throw new ArgumentException($"{name} contains a null object.");
        }

        private sealed class CaptureStage : IDisposable
        {
            private readonly GameObject _root;
            private readonly Camera _camera;
            private readonly Canvas _canvas;
            private readonly GameObject _floor;
            private readonly List<GameObject> _subjects = new();
            private readonly List<Material> _materials = new();
            private readonly List<Texture2D> _tiles = new();
            private readonly TMP_FontAsset _font;
            private readonly RenderTexture _rt;
            private readonly bool _oldFog;
            private readonly AmbientMode _oldAmbientMode;
            private readonly Color _oldAmbientLight;
            private readonly Color _oldAmbientSky;
            private readonly Color _oldAmbientEquator;
            private readonly Color _oldAmbientGround;
            private readonly float _oldAmbientIntensity;
            private readonly List<Light> _disabledLights = new();

            public CaptureStage()
            {
                _oldFog = RenderSettings.fog;
                _oldAmbientMode = RenderSettings.ambientMode;
                _oldAmbientLight = RenderSettings.ambientLight;
                _oldAmbientSky = RenderSettings.ambientSkyColor;
                _oldAmbientEquator = RenderSettings.ambientEquatorColor;
                _oldAmbientGround = RenderSettings.ambientGroundColor;
                _oldAmbientIntensity = RenderSettings.ambientIntensity;
                RenderSettings.fog = false;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.47f, 0.61f, 0.72f);
                RenderSettings.ambientEquatorColor = new Color(0.22f, 0.34f, 0.42f);
                RenderSettings.ambientGroundColor = new Color(0.10f, 0.16f, 0.20f);
                RenderSettings.ambientIntensity = 1f;
                foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (light.enabled) { light.enabled = false; _disabledLights.Add(light); }
                }

                _root = new GameObject("ShipConceptCaptureStage") { hideFlags = HideFlags.DontSave };
                _root.transform.position = Stage;
                _camera = NewChild("CaptureCamera", _root.transform).AddComponent<Camera>();
                _camera.enabled = false;
                _camera.orthographic = true;
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(0.018f, 0.043f, 0.061f);
                _camera.cullingMask = 1 << CaptureLayer;
                _camera.nearClipPlane = 0.1f;
                _camera.farClipPlane = 500f;
                _camera.allowHDR = false;
                _camera.allowMSAA = true;
                _rt = new RenderTexture(new RenderTextureDescriptor(Width, Height, RenderTextureFormat.ARGB32, 24)
                { msaaSamples = 4, sRGB = true });
                _rt.Create();
                _camera.targetTexture = _rt;

                AddLight("Key", new Vector3(35f, -28f, 0f), 1.65f, new Color(0.86f, 0.93f, 1f), true);
                AddLight("Fill", new Vector3(58f, 155f, 0f), 0.65f, new Color(0.33f, 0.67f, 0.84f), false);
                AddLight("Rim", new Vector3(18f, 215f, 0f), 0.80f, new Color(1f, 0.87f, 0.67f), false);

                _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _floor.name = "SeaStudio";
                _floor.transform.SetParent(_root.transform, false);
                _floor.transform.localScale = new Vector3(300f, 0.04f, 300f);
                _floor.layer = CaptureLayer;
                Object.DestroyImmediate(_floor.GetComponent<Collider>());
                _floor.GetComponent<Renderer>().sharedMaterial = Material("SeaStudio", new Color(0.026f, 0.089f, 0.112f), 0.2f, 0.34f);

                _font = TMP_Settings.defaultFontAsset;
                if (_font == null) _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Game/Art/Fonts/KoreanFont SDF.asset");
                if (_font == null) throw new InvalidOperationException("No TMP font is available for capture captions.");
                var canvasGo = NewChild("CaptureCaptions", _root.transform);
                _canvas = canvasGo.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceCamera;
                _canvas.worldCamera = _camera;
                _canvas.planeDistance = 1f;
                _canvas.sortingOrder = 100;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = 1f;
            }

            private void AddLight(string name, Vector3 rotation, float intensity, Color color, bool shadows)
            {
                var light = NewChild(name, _root.transform).AddComponent<Light>();
                light.type = LightType.Directional;
                light.cullingMask = 1 << CaptureLayer;
                light.color = color;
                light.intensity = intensity;
                light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
                light.shadowStrength = 0.65f;
                light.transform.rotation = Quaternion.Euler(rotation);
            }

            public void Single(GameObject source, int index, string view, bool bridge, bool topDown, string path, bool sideView = false)
            {
                Clear();
                var clone = Clone(source);
                var b = BoundsOf(clone);
                clone.transform.position += new Vector3(Stage.x - b.center.x, 0f, Stage.z - b.center.z);
                b = BoundsOf(clone);
                SetFloor(bridge ? b.min.y - 0.06f : Stage.y - 0.9f);
                var rotation = sideView ? Quaternion.Euler(8f, 90f, 0f) : topDown ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.Euler(35f, 145f, 0f);
                Frame(new[] { clone }, rotation, 0.75f, 0.70f, bridge ? 1.10f : 1.06f);
                Board($"0{index + 1}  /  {Names[index]}", view, Roles[index], Accents[index]);
                if (topDown)
                    Label("BOW  ↑", 29f, Accents[index], new Vector2(0.5f, 1f), new Vector2(0f, -166f),
                        new Vector2(260f, 42f), TextAlignmentOptions.Center);
                Label(bridge ? "BRIDGE + ENGINE HOUSE / 3 × 1 BLOCKS" : "MODULAR DECK / 2 m CELL SYSTEM", 24f,
                    new Color(0.65f, 0.79f, 0.84f), new Vector2(0f, 0f), new Vector2(62f, 83f),
                    new Vector2(1200f, 45f));
                Label(bridge ? "SILHOUETTE STUDY" : "LOADOUT CONCEPT", 21f, Accents[index],
                    new Vector2(1f, 0f), new Vector2(-62f, 83f), new Vector2(520f, 45f), TextAlignmentOptions.Right);
                Write(path);
            }

            public void Lineup(List<GameObject> sources, string title, string subtitle, bool bridges, string path)
            {
                ContactBoard(sources, title, subtitle, bridges, Names,
                    bridges ? new[] { "COMPACT / MULTIROLE", "TALL / FLEET COMMAND", "LOW / FAST STRIKE", "ANGULAR / FIRE CONTROL" } : Roles,
                    path);
            }

            /// <summary>
            /// Each subject has its own camera fit. Large escorts in one concept cannot shrink another.
            /// Native camera images are arranged by Unity UI, in stable patrol-to-missile reading order.
            /// </summary>
            public void ContactBoard(List<GameObject> sources, string title, string subtitle, bool onStudioFloor,
                                     string[] names, string[] captions, string path)
            {
                Clear();
                const int tileWidth = 846, tileHeight = 350;
                for (int i = 0; i < 4; i++)
                    _tiles.Add(RenderTile(sources[i], onStudioFloor, tileWidth, tileHeight));

                _floor.SetActive(false);
                _camera.aspect = Width / (float)Height;
                Board(title, "FOUR CONCEPTS / NATIVE 3D", subtitle, new Color(0.28f, 0.83f, 0.81f));
                for (int i = 0; i < 4; i++)
                {
                    float x = i % 2 == 0 ? 62f : 976f;
                    float y = i < 2 ? -187f : -697f;
                    var anchor = new Vector2(0f, 1f);
                    Bar($"Card_{i}", anchor, new Vector2(x, y), new Vector2(882f, 482f), new Color(0.032f, 0.064f, 0.083f));
                    Bar($"CardAccent_{i}", anchor, new Vector2(x, y), new Vector2(882f, 3f), Accents[i]);
                    Label($"0{i + 1}  {names[i]}", 29f, Accents[i], anchor, new Vector2(x + 18f, y - 15f),
                        new Vector2(846f, 42f));
                    var imageGo = new GameObject($"NativeRender_{i}", typeof(RectTransform), typeof(RawImage));
                    imageGo.layer = CaptureLayer;
                    imageGo.transform.SetParent(_canvas.transform, false);
                    var rect = imageGo.GetComponent<RectTransform>();
                    rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
                    rect.anchoredPosition = new Vector2(x + 18f, y - 65f);
                    rect.sizeDelta = new Vector2(tileWidth, tileHeight);
                    var image = imageGo.GetComponent<RawImage>();
                    image.texture = _tiles[i];
                    image.color = Color.white;
                    image.raycastTarget = false;
                    Label(captions[i], 21f, new Color(0.65f, 0.80f, 0.85f), anchor,
                        new Vector2(x + 18f, y - 436f), new Vector2(846f, 34f));
                }
                Write(path);
            }

            private Texture2D RenderTile(GameObject source, bool onStudioFloor, int width, int height)
            {
                ClearSubjects();
                var clone = Clone(source);
                var b = BoundsOf(clone);
                clone.transform.position += new Vector3(Stage.x - b.center.x, 0f, Stage.z - b.center.z);
                b = BoundsOf(clone);
                SetFloor(onStudioFloor ? b.min.y - 0.06f : Stage.y - 0.9f);
                _floor.SetActive(true);
                var tile = new RenderTexture(new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 24)
                { msaaSamples = 4, sRGB = true });
                tile.Create();
                var oldActive = RenderTexture.active;
                var oldTarget = _camera.targetTexture;
                Texture2D result = null;
                _canvas.enabled = false;
                try
                {
                    _camera.targetTexture = tile;
                    Frame(new[] { clone }, Quaternion.Euler(35f, 145f, 0f), 0.92f, 0.91f, 1.03f, width / (float)height);
                    _camera.Render();
                    _camera.Render();
                    RenderTexture.active = tile;
                    result = new Texture2D(width, height, TextureFormat.RGB24, false);
                    result.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                    result.Apply();
                    return result;
                }
                catch
                {
                    if (result != null) Object.DestroyImmediate(result);
                    throw;
                }
                finally
                {
                    RenderTexture.active = oldActive;
                    _camera.targetTexture = oldTarget;
                    _camera.aspect = Width / (float)Height;
                    _canvas.enabled = true;
                    tile.Release();
                    Object.DestroyImmediate(tile);
                    ClearSubjects();
                }
            }

            private GameObject Clone(GameObject source)
            {
                var clone = Object.Instantiate(source, _root.transform);
                clone.name = source.name + "_CAPTURE";
                clone.transform.localPosition = Vector3.zero;
                clone.transform.localRotation = Quaternion.identity;
                clone.transform.localScale = source.transform.localScale;
                SetLayer(clone);
                foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                foreach (var camera in clone.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                foreach (var light in clone.GetComponentsInChildren<Light>(true)) light.enabled = false;
                foreach (var canvas in clone.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
                foreach (var collider in clone.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                _subjects.Add(clone);
                return clone;
            }

            private static Bounds BoundsOf(GameObject go)
            {
                Bounds result = default;
                bool has = false;
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer || r is LineRenderer) continue;
                    if (!has) { result = r.bounds; has = true; }
                    else result.Encapsulate(r.bounds);
                }
                if (!has) throw new InvalidOperationException($"{go.name} has no active visual renderers.");
                return result;
            }

            private void Frame(IEnumerable<GameObject> objects, Quaternion rotation,
                               float widthFraction, float heightFraction, float margin, float? targetAspect = null)
            {
                var bounds = new List<Bounds>();
                foreach (var go in objects) bounds.Add(BoundsOf(go));
                var combined = bounds[0];
                foreach (var b in bounds) combined.Encapsulate(b);
                var inv = Quaternion.Inverse(rotation);
                Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
                foreach (var b in bounds)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        var corner = b.center + Vector3.Scale(b.extents, new Vector3(
                            (i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                        var p = inv * (corner - combined.center);
                        min = Vector2.Min(min, new Vector2(p.x, p.y));
                        max = Vector2.Max(max, new Vector2(p.x, p.y));
                    }
                }
                float aspect = targetAspect ?? Width / (float)Height;
                _camera.aspect = aspect;
                _camera.orthographicSize = Mathf.Max((max.y - min.y) / heightFraction,
                    (max.x - min.x) / (aspect * widthFraction)) * 0.5f * margin;
                var mid = (min + max) * 0.5f;
                var focus = combined.center + rotation * new Vector3(mid.x, mid.y, 0f);
                // Place subjects slightly below center to clear the title band.
                focus += rotation * Vector3.up * (_camera.orthographicSize * 0.025f);
                _camera.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * 130f, rotation);
            }

            private void SetFloor(float worldY)
                => _floor.transform.position = new Vector3(Stage.x, worldY - 0.02f, Stage.z);

            private void Board(string title, string view, string subtitle, Color accent)
            {
                Bar("TopRule", new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(1796f, 2f), new Color(0.2f, 0.4f, 0.47f));
                Bar("BottomRule", new Vector2(0.5f, 0f), new Vector2(0f, 67f), new Vector2(1796f, 2f), new Color(0.2f, 0.4f, 0.47f));
                Bar("Accent", new Vector2(0f, 1f), new Vector2(62f, -56f), new Vector2(7f, 82f), accent);
                Label(title, 50f, new Color(0.86f, 0.94f, 0.96f), new Vector2(0f, 1f), new Vector2(86f, -52f), new Vector2(1450f, 65f));
                Label(subtitle, 25f, accent, new Vector2(0f, 1f), new Vector2(87f, -119f), new Vector2(1450f, 42f));
                Label("NAVAL ROGUELIKE", 21f, new Color(0.71f, 0.84f, 0.88f), new Vector2(1f, 1f),
                    new Vector2(-62f, -61f), new Vector2(520f, 34f), TextAlignmentOptions.Right);
                Label(view, 19f, new Color(0.43f, 0.64f, 0.73f), new Vector2(1f, 1f),
                    new Vector2(-62f, -101f), new Vector2(520f, 32f), TextAlignmentOptions.Right);
                Label("STARTING SHIP SERIES    /    DESIGN PREVIEW", 18f, new Color(0.44f, 0.64f, 0.71f),
                    new Vector2(0f, 0f), new Vector2(62f, 24f), new Vector2(1250f, 34f));
                Label("2026.10", 18f, new Color(0.44f, 0.64f, 0.71f), new Vector2(1f, 0f),
                    new Vector2(-62f, 24f), new Vector2(300f, 34f), TextAlignmentOptions.Right);
            }

            private void Label(string text, float size, Color color, Vector2 anchor, Vector2 position,
                               Vector2 box, TextAlignmentOptions alignment = TextAlignmentOptions.Left, Vector2? pivot = null)
            {
                var go = new GameObject("Caption", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.layer = CaptureLayer;
                go.transform.SetParent(_canvas.transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = anchor;
                rt.pivot = pivot ?? anchor;
                rt.anchoredPosition = position;
                rt.sizeDelta = box;
                var label = go.GetComponent<TextMeshProUGUI>();
                label.font = _font;
                label.fontSize = size;
                label.text = text;
                label.color = color;
                label.alignment = alignment;
                label.raycastTarget = false;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;
            }

            private void Bar(string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.layer = CaptureLayer;
                go.transform.SetParent(_canvas.transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
                rt.anchoredPosition = pos;
                rt.sizeDelta = size;
                var image = go.GetComponent<Image>();
                image.color = color;
                image.raycastTarget = false;
            }

            private void Write(string path)
            {
                Canvas.ForceUpdateCanvases();
                _camera.Render();
                Canvas.ForceUpdateCanvases();
                _camera.Render();
                var old = RenderTexture.active;
                Texture2D texture = null;
                try
                {
                    RenderTexture.active = _rt;
                    texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                    texture.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                    texture.Apply();
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = old;
                    if (texture != null) Object.DestroyImmediate(texture);
                }
                Debug.Log($"[ShipConceptCapture] {Path.GetFileName(path)}");
            }

            private void Clear()
            {
                ClearSubjects();
                _floor.SetActive(true);
                for (int i = _canvas.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(_canvas.transform.GetChild(i).gameObject);
                foreach (var texture in _tiles) if (texture != null) Object.DestroyImmediate(texture);
                _tiles.Clear();
            }

            private void ClearSubjects()
            {
                foreach (var subject in _subjects) if (subject != null) Object.DestroyImmediate(subject);
                _subjects.Clear();
            }

            private Material Material(string name, Color color, float metallic, float smoothness)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                var mat = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                else mat.color = color;
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                _materials.Add(mat);
                return mat;
            }

            public void Dispose()
            {
                if (_camera != null) _camera.targetTexture = null;
                if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); }
                if (_root != null) Object.DestroyImmediate(_root);
                foreach (var texture in _tiles) if (texture != null) Object.DestroyImmediate(texture);
                foreach (var mat in _materials) if (mat != null) Object.DestroyImmediate(mat);
                foreach (var light in _disabledLights) if (light != null) light.enabled = true;
                RenderSettings.fog = _oldFog;
                RenderSettings.ambientMode = _oldAmbientMode;
                RenderSettings.ambientLight = _oldAmbientLight;
                RenderSettings.ambientSkyColor = _oldAmbientSky;
                RenderSettings.ambientEquatorColor = _oldAmbientEquator;
                RenderSettings.ambientGroundColor = _oldAmbientGround;
                RenderSettings.ambientIntensity = _oldAmbientIntensity;
            }

            private static GameObject NewChild(string name, Transform parent)
            {
                var go = new GameObject(name) { layer = CaptureLayer };
                go.transform.SetParent(parent, false);
                return go;
            }

            private static void SetLayer(GameObject go)
            {
                go.layer = CaptureLayer;
                foreach (Transform child in go.transform) SetLayer(child.gameObject);
            }
        }
    }
}

using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Game.Refit;
using Game.Combat;
using Game.Core;
using Game.Enemies;
using Game.Modules;
using Game.Ship;
using Game.UI;
using Game.View;
using static Game.EditorTools.NavalEditorUtil;

namespace Game.EditorTools
{
    /// <summary>Prototype_Main 씬을 통째로 조립한다.</summary>
    public static class NavalSceneBuilder
    {
        private static TMP_FontAsset _uiFont;

        private const float CellSize = 2f;
        private const int GridWidth = 10;
        private const int GridDepth = 3;

        public static void Build(NavalPrefabBuilder.Result p, NavalDataBuilder.Result d)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,
                                                    NewSceneMode.Single);

            // 씬을 새로 만들면 아직 아무도 참조하지 않는 에셋 인스턴스가 언로드된다.
            // 그 죽은 참조를 그대로 넣으면 모든 필드가 조용히 null이 되므로 여기서 다시 읽는다.
            p = NavalPrefabBuilder.Load();
            d = NavalDataBuilder.Load();

            if (d.Ship == null || d.Balance == null || d.Progression == null ||
                d.Loadout == null || d.Rounds == null || p.FastBoat == null)
            {
                Debug.LogError("[Setup] 에셋을 다시 읽지 못했습니다. 씬 조립을 중단합니다.");
                return;
            }

            // ------------------------------------------------------------ 바다
            // 파도·잔물결·하늘 반사·해 반짝임·흰 파도를 그리는 수면 셰이더(Naval/Ocean). 무늬가 월드 좌표에 붙어 있어
            // 카메라가 함선을 따라가도 이동이 보인다. 실행 중에는 OceanSurface가 큰 수면으로 바꿔 카메라를 따라간다.
            var oceanMat = CreateMaterial("ocean", Color.white, 0f, 0.55f);
            var oceanShader = Shader.Find("Naval/Ocean");
            if (oceanShader != null) oceanMat.shader = oceanShader;
            else Debug.LogWarning("[Setup] Naval/Ocean 셰이더를 찾지 못해 기본 머티리얼로 둡니다.");
            EditorUtility.SetDirty(oceanMat);

            // 바다는 보여주기만 한다. Collider를 남기면 함선이 수면과 겹쳐 이동이 막힌다.
            var ocean = Primitive("Ocean", PrimitiveType.Plane, Vector3.one * 60f, oceanMat, null, false);
            ocean.transform.position = new Vector3(0f, -0.9f, 0f);
            ocean.isStatic = true;

            // -------------------------------------------------------- 플레이어
            var ship = BuildPlayerShip(p, d, out var grid, out var systems, out var factory,
                                       out var targeting, out var moduleRoot);

            // ------------------------------------------------------------ 카메라
            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                cam.tag = "MainCamera";
            }
            // 효과음은 카메라에서 듣는다. 새로 만든 카메라에는 리스너가 없다.
            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            cam.transform.position = new Vector3(0f, 28f, -22f);
            cam.farClipPlane = 400f;
            var camCtrl = cam.gameObject.AddComponent<QuarterViewCamera>();
            Configure(camCtrl, so => Set(so, "target", ship.transform));

            var sun = Object.FindFirstObjectByType<Light>();
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(50f, 40f, 0f);
                sun.intensity = 1.2f;
            }

            // ------------------------------------------------- 정비용 3D 카메라
            // 함선과 모듈만 비춰 정비 화면에 띄운다.
            var refitRt = CreateRenderTexture("refit_ship", 1280, 640);
            var refitCam = BuildRefitCamera("RefitCamera", refitRt,
                (1 << LayerShip) | (1 << LayerPlayerShip),
                new Color(0.05f, 0.09f, 0.13f));

            // ------------------------------------------------------------ UI
            var ui = BuildUiCanvas(refitRt, cam, out var refitUi, out var hud, out var statusUi);

            // ---------------------------------------------------------- 시스템
            var systemsRoot = new GameObject("Systems");

            systemsRoot.AddComponent<PoolManager>();

            // 새 씬을 열면 메모리의 에셋 참조가 끊기므로 디스크에서 다시 읽는다
            var sfxLibrary = NavalAudioBuilder.Load();
            if (sfxLibrary == null) Debug.LogError("[Setup] SfxLibrary가 없습니다. 효과음이 재생되지 않습니다.");
            var audioManager = systemsRoot.AddComponent<AudioManager>();
            Configure(audioManager, so => Set(so, "library", sfxLibrary));

            var shipController = ship.GetComponent<ShipController>();

            var gm = systemsRoot.AddComponent<GameManager>();
            Configure(gm, so => Set(so, "player", shipController));

            var spawner = new GameObject("EnemySpawner").AddComponent<EnemySpawner>();
            Configure(spawner, so =>
            {
                Set(so, "spawnRadius", 45f);
                Set(so, "spawnRadiusJitter", 8f);
            });

            var draft = systemsRoot.AddComponent<RefitDraft>();
            Configure(draft, so =>
            {
                Set(so, "config", d.Progression);
                Set(so, "grid", grid);
            });

            var refit = systemsRoot.AddComponent<RefitController>();
            Configure(refit, so =>
            {
                Set(so, "draft", draft);
                Set(so, "ui", refitUi);
                Set(so, "ship", shipController);
                Set(so, "progression", d.Progression);
            });

            var experience = systemsRoot.AddComponent<Game.Progression.ExperienceSystem>();
            Configure(experience, so => Set(so, "config", d.Progression));

            var stage = systemsRoot.AddComponent<StageDirector>();
            Configure(stage, so =>
            {
                SetArray(so, "stages", d.Rounds, d.Rounds2);
                Set(so, "spawner", spawner);
                Set(so, "experience", experience);
                Set(so, "refit", refit);
                Set(so, "player", ship.transform);
            });

            Configure(refitUi, so =>
            {
                Set(so, "grid", grid);
                Set(so, "systems", systems);
                Set(so, "factory", factory);
                Set(so, "shipCamera", refitCam);
                Set(so, "moduleRoot", moduleRoot);
                Set(so, "cellHighlightPrefab", p.CellHighlight);
                Set(so, "arcMaterial", AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Art/MAT_fire_arc.mat"));
            });

            Configure(statusUi, so =>
            {
                Set(so, "grid", grid);
                Set(so, "systems", systems);
            });

            // ------------------------------------------------------------ 저장
            EnsureFolder($"{Root}/Scenes");
            string scenePath = $"{Root}/Scenes/Prototype_Main.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            Debug.Log($"[Setup] 씬 생성 완료: {scenePath}");
        }

        private const string ModelDir = Root + "/Art/Models";

        /// <summary>
        /// 초계함 모델에서 선체와 갑판, 난간만 남기고 상부구조를 모두 제거한다.
        /// 함교·포탑·마스트는 플레이어가 배치하는 모듈이므로 갑판이 비어 있어야 한다.
        /// </summary>
        private static void StripSuperstructure(GameObject hullGo)
        {
            var keep = new System.Collections.Generic.HashSet<string>
            {
                "Hull_fixed_10x3_grid_envelope",
                "Deck_surface",
                "Railing_stanchion",
                "Safety_rail",
                "Mooring_bollard",
                "Lifebuoy",
            };

            for (int i = hullGo.transform.childCount - 1; i >= 0; i--)
            {
                var child = hullGo.transform.GetChild(i);

                // "Railing_stanchion.017" 처럼 뒤에 번호가 붙는다
                string baseName = child.name.Split('.')[0];
                if (!keep.Contains(baseName)) Object.DestroyImmediate(child.gameObject);
            }
        }

        /// <summary>정비 화면에 띄울 전용 카메라. 평소에는 꺼져 있다.</summary>
        private static Camera BuildRefitCamera(string name, RenderTexture target,
                                               int cullingMask, Color background)
        {
            var go = new GameObject(name, typeof(Camera));
            var cam = go.GetComponent<Camera>();

            cam.orthographic = true;
            cam.orthographicSize = 6.5f;
            cam.cullingMask = cullingMask;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.targetTexture = target;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.enabled = false;      // RefitUI가 필요할 때만 켠다

            return cam;
        }

        // ------------------------------------------------------------- 플레이어

        private static GameObject BuildPlayerShip(NavalPrefabBuilder.Result p, NavalDataBuilder.Result d,
            out ShipGrid grid, out ShipSystems systems, out ModuleFactory factory,
            out TargetingSystem targeting, out Transform moduleRootOut)
        {
            var root = new GameObject("PlayerShip");
            root.layer = LayerPlayerShip;

            var rb = root.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.linearDamping = 0f;

            // 피격 판정용 볼륨이다. 물리적으로 밀고 밀리면 이동이 방해되므로 트리거로 둔다.
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(CellSize * 2f, 2f, CellSize * 4f);   // 시작 크기. 자라면 갱신된다
            col.isTrigger = true;

            // 시각 전용 루트. 흔들림은 여기에만 적용해 물리에 영향을 주지 않는다.
            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            model.AddComponent<ShipVisualSway>();

            // 선체는 점유된 칸을 따라 ShipHullBuilder가 만들어낸다.
            // 고정된 선체 모델을 두면 모듈을 아무리 붙여도 배 모양이 그대로다.
            var hullRoot = new GameObject("HullRoot") { layer = LayerShip };
            var moduleRoot = new GameObject("ModuleRoot") { layer = LayerShip };
            hullRoot.transform.SetParent(model.transform, false);
            moduleRoot.transform.SetParent(model.transform, false);

            var gridComp = root.AddComponent<ShipGrid>();
            Configure(gridComp, so =>
            {
                Set(so, "maxHalfLength", 10);
                Set(so, "maxHalfBeam", 3);
                Set(so, "cellSize", CellSize);
                Set(so, "deckHeight", 0.4f);
            });

            var systemsComp = root.AddComponent<ShipSystems>();
            Configure(systemsComp, so =>
            {
                Set(so, "grid", gridComp);
                Set(so, "config", d.Ship);
            });

            var ship = root.AddComponent<ShipController>();
            var resolver = root.AddComponent<DamageResolver>();
            Configure(resolver, so =>
            {
                Set(so, "grid", gridComp);
                Set(so, "ship", ship);
                Set(so, "systems", systemsComp);
                Set(so, "balance", d.Balance);
            });

            Configure(ship, so =>
            {
                Set(so, "config", d.Ship);
                Set(so, "grid", gridComp);
                Set(so, "systems", systemsComp);
                Set(so, "damageResolver", resolver);
                Set(so, "moveAction", FindMoveAction());
            });

            var factoryComp = root.AddComponent<ModuleFactory>();
            Configure(factoryComp, so =>
            {
                Set(so, "grid", gridComp);
                Set(so, "moduleRoot", moduleRoot.transform);
                Set(so, "pedestalPrefab", p.MountPedestal);
                Set(so, "raiseHeight", 1.2f);
            });

            var hullBuilder = root.AddComponent<ShipHullBuilder>();
            Configure(hullBuilder, so =>
            {
                Set(so, "grid", gridComp);
                Set(so, "hullRoot", hullRoot.transform);
                Set(so, "deckPlatePrefab", p.DeckPlate);
                Set(so, "sideSkirtPrefab", p.SideSkirt);
                Set(so, "bowDeckMaterial", AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Art/MAT_hull_deck.mat"));
                Set(so, "bowSideMaterial", AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Art/MAT_hull_side.mat"));
                if (p.BowFittings != null) Set(so, "bowFittingsPrefab", p.BowFittings);
                Set(so, "bowAnchorPrefab", p.BowAnchor);
            });

            var targetingComp = root.AddComponent<TargetingSystem>();
            Configure(targetingComp, so =>
            {
                Set(so, "systems", systemsComp);
                Set(so, "scanInterval", 0.2f);
            });

            // 기만체는 플레이어가 직접 쏘는 스킬이다(Q / HUD 버튼)
            var decoySkill = root.AddComponent<DecoySkill>();
            Configure(decoySkill, so =>
            {
                Set(so, "grid", gridComp);
                Set(so, "targeting", targetingComp);
            });

            // 재밍은 전자전 장비가 있을 때 쓰는 두 번째 스킬이다(E / HUD 버튼)
            var jammerSkill = root.AddComponent<JammerSkill>();
            Configure(jammerSkill, so => Set(so, "grid", gridComp));

            // 응급 수리(R, 손상통제반) · 연막(F, 기만체 발사기) · 전속(Shift, 함교)
            Configure(root.AddComponent<RepairBurstSkill>(), so => Set(so, "grid", gridComp));
            Configure(root.AddComponent<SmokeSkill>(), so => Set(so, "grid", gridComp));
            Configure(root.AddComponent<FlankSkill>(), so => Set(so, "grid", gridComp));

            // 연막: 함선에 붙은 연기 발생기
            var smokeFx = (GameObject)PrefabUtility.InstantiatePrefab(p.FxSmokeScreen);
            smokeFx.transform.SetParent(root.transform, false);
            var smokeScreen = root.AddComponent<Game.Combat.SmokeScreen>();
            Configure(smokeScreen, so => Set(so, "smoke", smokeFx.GetComponentInChildren<ParticleSystem>()));

            var init = root.AddComponent<ShipInitializer>();
            Configure(init, so =>
            {
                Set(so, "loadout", d.Loadout);
                Set(so, "factory", factoryComp);
                Set(so, "systems", systemsComp);
            });

            moduleRootOut = moduleRoot.transform;
            grid = gridComp;
            systems = systemsComp;
            factory = factoryComp;
            targeting = targetingComp;
            return root;
        }

        /// <summary>템플릿에 들어있는 InputSystem_Actions에서 Player/Move 액션 참조를 찾는다.</summary>
        private static InputActionReference FindMoveAction()
        {
            const string path = "Assets/InputSystem_Actions.inputactions";
            var all = AssetDatabase.LoadAllAssetsAtPath(path);

            if (all == null || all.Length == 0)
            {
                Debug.LogWarning("[Setup] InputSystem_Actions.inputactions를 찾지 못했습니다. " +
                                 "ShipController의 moveAction을 직접 연결하세요.");
                return null;
            }

            var reference = all.OfType<InputActionReference>()
                               .FirstOrDefault(r => r.action != null &&
                                                    r.action.name == "Move" &&
                                                    r.action.actionMap != null &&
                                                    r.action.actionMap.name == "Player");

            if (reference == null)
                Debug.LogWarning("[Setup] Player/Move 액션을 찾지 못했습니다.");

            return reference;
        }

        // -------------------------------------------------------------------- UI

        /// <summary>화면 아래 액티브 스킬 버튼 하나(이름 + 재장전 막대).</summary>
        private static GameObject MakeSkillButton(Transform parent, string name, string label, float x, Color barColor,
                                                  out TMP_Text text, out Slider bar)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(x, 40f);
            rt.sizeDelta = new Vector2(250f, 64f);
            go.GetComponent<Image>().color = new Color(0.08f, 0.12f, 0.16f, 0.85f);
            text = MakeText(go.transform, "Label", label, 22,
                            new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(240f, 36f),
                            TextAlignmentOptions.Center);
            bar = MakeBar(go.transform, "Cooldown",
                          new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(220f, 8f), barColor);
            return go;
        }

        private static GameObject BuildUiCanvas(RenderTexture refitRt,
                                                Camera cam, out RefitUI refitUi, out HUDView hud,
                                                out ModuleStatusUI statusUi)
        {
            _uiFont = EnsureKoreanFont();

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem", typeof(EventSystem));
                var module = esGo.AddComponent<InputSystemUIInputModule>();

                // 스크립트로 붙이면 액션 에셋이 비어 있어 클릭이 전혀 처리되지 않는다.
                // 에디터 메뉴로 추가할 때 Unity가 해주는 기본 액션 할당을 직접 해준다.
                module.AssignDefaultActions();
            }

            // --- HUD
            var hudRoot = MakePanel(canvasGo.transform, "HUD", Vector2.zero, Vector2.one, Color.clear);

            var hullText  = MakeText(hudRoot.transform, "HullText", "HULL 100 / 100", 28,
                                     new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(340f, 40f),
                                     TextAlignmentOptions.Left);
            var hullBar   = MakeBar(hudRoot.transform, "HullBar",
                                    new Vector2(0f, 1f), new Vector2(30f, -74f), new Vector2(340f, 18f),
                                    new Color(0.85f, 0.3f, 0.25f));
            var speedText = MakeText(hudRoot.transform, "SpeedText", "SPD 0.0", 22,
                                     new Vector2(0f, 1f), new Vector2(30f, -104f), new Vector2(340f, 32f),
                                     TextAlignmentOptions.Left);

            var roundText = MakeText(hudRoot.transform, "StageText", "STAGE 1", 28,
                                     new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(520f, 40f),
                                     TextAlignmentOptions.Center);
            var roundBar  = MakeBar(hudRoot.transform, "RoundBar",
                                    new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(520f, 16f),
                                    new Color(0.35f, 0.7f, 0.95f));
            var killsText = MakeText(hudRoot.transform, "KillsText", "격침 0", 22,
                                     new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(400f, 32f),
                                     TextAlignmentOptions.Center);

            // 레벨과 경험치: 화면 맨 아래 가로로 길게. 기만체 버튼 위에 둔다.
            var levelText = MakeText(hudRoot.transform, "LevelText", "Lv 1", 24,
                                     new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(400f, 32f),
                                     TextAlignmentOptions.Center);
            var xpBar = MakeBar(hudRoot.transform, "XpBar",
                                new Vector2(0.5f, 0f), new Vector2(0f, 116f), new Vector2(760f, 12f),
                                new Color(0.95f, 0.8f, 0.3f));
            xpBar.value = 0f;

            // 액티브 스킬: 화면 아래 가운데에 다섯 개를 나란히. 클릭해도 되고 키를 눌러도 된다.
            var decoyGo = MakeSkillButton(hudRoot.transform, "DecoyButton", "기만체 [Q]", -520f, new Color(0.55f, 1f, 0.6f), out var decoyText, out var decoyBar);
            var jamGo = MakeSkillButton(hudRoot.transform, "JamButton", "재밍 [E]", -260f, new Color(0.45f, 0.9f, 1f), out var jamText, out var jamBar);
            var repairGo = MakeSkillButton(hudRoot.transform, "RepairButton", "응급수리 [R]", 0f, new Color(0.5f, 1f, 0.5f), out var repairText, out var repairBar);
            var smokeGo = MakeSkillButton(hudRoot.transform, "SmokeButton", "연막 [F]", 260f, new Color(0.85f, 0.85f, 0.9f), out var smokeText, out var smokeBar);
            var flankGo = MakeSkillButton(hudRoot.transform, "FlankButton", "전속 [Shift]", 520f, new Color(1f, 0.75f, 0.35f), out var flankText, out var flankBar);

            // 라운드 전환 배너
            var bannerHolder = MakeHolder(hudRoot.transform, "Banner");
            var bannerText = MakeText(bannerHolder.transform, "Text", "", 54,
                                      new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1200f, 90f),
                                      TextAlignmentOptions.Center);

            hud = hudRoot.AddComponent<HUDView>();
            Configure(hud, so =>
            {
                Set(so, "hullBar", hullBar);
                Set(so, "hullText", hullText);
                Set(so, "speedText", speedText);
                Set(so, "roundText", roundText);
                Set(so, "roundBar", roundBar);
                Set(so, "killsText", killsText);
                Set(so, "levelText", levelText);
                Set(so, "xpBar", xpBar);
                Set(so, "decoyButton", decoyGo.GetComponent<Button>());
                Set(so, "decoyText", decoyText);
                Set(so, "decoyBar", decoyBar);
                Set(so, "jamButton", jamGo.GetComponent<Button>());
                Set(so, "jamText", jamText);
                Set(so, "jamBar", jamBar);
                Set(so, "repairButton", repairGo.GetComponent<Button>());
                Set(so, "repairText", repairText);
                Set(so, "repairBar", repairBar);
                Set(so, "smokeButton", smokeGo.GetComponent<Button>());
                Set(so, "smokeText", smokeText);
                Set(so, "smokeBar", smokeBar);
                Set(so, "flankButton", flankGo.GetComponent<Button>());
                Set(so, "flankText", flankText);
                Set(so, "flankBar", flankBar);
                Set(so, "bannerRoot", bannerHolder);
                Set(so, "bannerText", bannerText);
            });

            // --- 정비 화면 (라운드 클리어 후)
            var refitHolder = MakeHolder(canvasGo.transform, "RefitPanel");
            var refitPanel = MakePanel(refitHolder.transform, "Panel", Vector2.zero, Vector2.one,
                                       new Color(0.04f, 0.06f, 0.08f, 0.96f));

            var refitTitle = MakeText(refitPanel.transform, "Title", "정비", 42,
                                      new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(900f, 56f),
                                      TextAlignmentOptions.Center);
            var refitStatus = MakeText(refitPanel.transform, "Status", "", 26,
                                      new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(520f, 40f),
                                      TextAlignmentOptions.Right);

            var rewardLabel = MakeText(refitPanel.transform, "Reward", "", 28,
                                       new Vector2(0f, 1f), new Vector2(60f, -120f), new Vector2(1200f, 40f),
                                       TextAlignmentOptions.Left);

            var shipView = MakeView(refitPanel.transform, "ShipView", refitRt,
                                   new Vector2(0.5f, 1f), new Vector2(0f, -190f));

            var refitInfo = MakeText(refitPanel.transform, "Info", "", 22,
                                     new Vector2(0f, 0f), new Vector2(60f, 60f), new Vector2(1500f, 160f),
                                     TextAlignmentOptions.TopLeft);

            // --- 카드 선택 (정비 진입 시 먼저 뜬다)
            var cardPanel = MakePanel(refitPanel.transform, "CardPanel", Vector2.zero, Vector2.one,
                                      new Color(0.03f, 0.05f, 0.07f, 0.97f));
            MakeText(cardPanel.transform, "CardTitle", "시스템 선택", 44,
                     new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(900f, 60f),
                     TextAlignmentOptions.Center);
            MakeText(cardPanel.transform, "CardHint", "카드를 클릭하거나 1 / 2 / 3 키로 선택", 24,
                     new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(900f, 36f),
                     TextAlignmentOptions.Center);

            var cardRects = new RectTransform[3];
            var cardNames = new TMP_Text[3];
            var cardIcons = new Image[3];
            var cardStats = new TMP_Text[3];

            for (int i = 0; i < 3; i++)
                BuildCard(cardPanel.transform, i, out cardRects[i], out cardNames[i],
                          out cardIcons[i], out cardStats[i]);

            refitUi = refitHolder.AddComponent<RefitUI>();
            Configure(refitUi, so =>
            {
                Set(so, "root", refitPanel);
                Set(so, "titleText", refitTitle);
                Set(so, "rewardText", rewardLabel);
                Set(so, "shipView", shipView);
                Set(so, "cardPanel", cardPanel);
                SetArray(so, "cardRects", cardRects);
                SetArray(so, "cardNames", cardNames);
                SetArray(so, "cardIcons", cardIcons);
                SetArray(so, "cardStats", cardStats);
                Set(so, "infoText", refitInfo);
                Set(so, "statusText", refitStatus);
            });
            refitPanel.SetActive(false);

            // --- 모듈 현황 (전투 중 Tab)
            var statusHolder = MakeHolder(canvasGo.transform, "ModuleStatusPanel");
            var statusPanel = MakePanel(statusHolder.transform, "Panel",
                                        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                        new Color(0.05f, 0.07f, 0.09f, 0.94f));
            var statusRt = statusPanel.GetComponent<RectTransform>();
            statusRt.sizeDelta = new Vector2(1000f, 620f);
            statusRt.anchoredPosition = Vector2.zero;

            var statusHeader = MakeText(statusPanel.transform, "Header", "", 26,
                                        new Vector2(0f, 1f), new Vector2(40f, -30f),
                                        new Vector2(920f, 40f), TextAlignmentOptions.Left);
            var statusBody = MakeText(statusPanel.transform, "Body", "", 22,
                                      new Vector2(0f, 1f), new Vector2(40f, -90f),
                                      new Vector2(920f, 460f), TextAlignmentOptions.TopLeft);
            MakeText(statusPanel.transform, "Close", "Tab 을 다시 눌러 닫기", 20,
                     new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(600f, 32f),
                     TextAlignmentOptions.Center);

            statusUi = statusHolder.AddComponent<ModuleStatusUI>();
            Configure(statusUi, so =>
            {
                Set(so, "root", statusPanel);
                Set(so, "headerText", statusHeader);
                Set(so, "bodyText", statusBody);
            });
            statusPanel.SetActive(false);

            // --- 결과 화면
            var resultHolder = MakeHolder(canvasGo.transform, "ResultPanel");
            var resultPanel = MakePanel(resultHolder.transform, "Panel", Vector2.zero, Vector2.one,
                                        new Color(0f, 0f, 0f, 0.85f));
            var title  = MakeText(resultPanel.transform, "Title", "VICTORY", 72,
                                  new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(900f, 100f),
                                  TextAlignmentOptions.Center);
            var detail = MakeText(resultPanel.transform, "Detail", "", 30,
                                  new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(900f, 50f),
                                  TextAlignmentOptions.Center);

            var result = resultHolder.AddComponent<ResultScreenUI>();
            Configure(result, so =>
            {
                Set(so, "root", resultPanel);
                Set(so, "titleText", title);
                Set(so, "detailText", detail);
            });
            resultPanel.SetActive(false);

            return canvasGo;
        }

        private static RectTransform MakeWarningIndicatorPrefab()
        {
            EnsureFolder($"{Root}/Prefabs/UI");

            string path = $"{Root}/Prefabs/UI/UI_MissileWarning.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<RectTransform>();

            var go = new GameObject("UI_MissileWarning", typeof(RectTransform), typeof(Image));
            var img = go.GetComponent<Image>();
            img.color = new Color(1f, 0.25f, 0.2f, 0.9f);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(28f, 28f);

            var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved.GetComponent<RectTransform>();
        }

        // ------------------------------------------------------------ UI 헬퍼

        /// <summary>
        /// 카드 한 장. 위에 이름, 가운데 모듈 그림, 아래 스펙과 설명.
        /// 그림은 셋업할 때 구워둔 아이콘 스프라이트를 쓴다.
        /// </summary>
        private static void BuildCard(Transform parent, int index, out RectTransform rect,
                                      out TMP_Text nameText, out Image icon, out TMP_Text stats)
        {
            var card = MakePanel(parent, $"Card_{index}",
                                 new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                 new Color(0.11f, 0.14f, 0.18f, 1f));

            rect = card.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(420f, 620f);
            rect.anchoredPosition = new Vector2((index - 1) * 460f, 0f);

            nameText = MakeText(card.transform, "Name", "", 34,
                                new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(380f, 48f),
                                TextAlignmentOptions.Center);

            // 가운데 모듈 그림
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(card.transform, false);

            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 1f);
            iconRt.pivot = new Vector2(0.5f, 1f);
            iconRt.anchoredPosition = new Vector2(0f, -84f);
            iconRt.sizeDelta = new Vector2(260f, 260f);

            icon = iconGo.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            stats = MakeText(card.transform, "Stats", "", 20,
                             new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(380f, 240f),
                             TextAlignmentOptions.TopLeft);
        }

        /// <summary>카메라가 그린 3D 화면을 띄우는 패널.</summary>
        private static RawImage MakeView(Transform parent, string name, RenderTexture texture,
                                         Vector2 anchor, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(1280f, 640f);

            var raw = go.GetComponent<RawImage>();
            raw.texture = texture;
            raw.raycastTarget = false;
            return raw;
        }

        /// <summary>항상 켜져 있는 빈 컨테이너. UI 컴포넌트를 여기에 붙인다.</summary>
        private static GameObject MakeHolder(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go;
        }

        private static GameObject MakePanel(Transform parent, string name,
                                            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = color.a > 0.01f;
            return go;
        }

        private static TMP_Text MakeText(Transform parent, string name, string text, float size,
                                         Vector2 anchor, Vector2 pos, Vector2 sizeDelta,
                                         TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;

            var t = go.AddComponent<TextMeshProUGUI>();
            if (_uiFont != null) t.font = _uiFont;   // 기본 폰트에는 한글 글리프가 없다
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            return t;
        }

        private static Slider MakeBar(Transform parent, string name, Vector2 anchor,
                                      Vector2 pos, Vector2 size, Color fillColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f, 0.85f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(go.transform, false);
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = fillColor;

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillRt;
            slider.transition = Selectable.Transition.None;
            slider.interactable = false;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            return slider;
        }

    }
}

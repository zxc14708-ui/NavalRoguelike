using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.Core;
using Game.Data;
using Game.Modules;
using Game.Refit;
using Game.Ship;
using Game.TaskForce;
using Game.UI;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>
    /// Exercises the actual main-scene initializer, factory, support effects and draft pool.
    /// Batch entry: Game.EditorTools.StartingShipLiveVerification.RunBatch.
    /// Optional -shipConceptOutput selects the report/capture directory. Graphics are required.
    /// </summary>
    [InitializeOnLoad]
    public static class StartingShipLiveVerification
    {
        private const string RunningKey = "Naval.StartingShipLiveVerification.Running";
        private const string OutputKey = "Naval.StartingShipLiveVerification.Output";
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static readonly string[] Codes = { "Patrol", "Command", "Assault", "Missile" };
        private static readonly string[] SupportProperties = { "EscortFireRateMultiplier", "SpeedMultiplier",
            "AccelerationMultiplier", "MovingGunDamageMultiplier", "GuidedWeaponRangeMultiplier",
            "MissileReloadMultiplier", "MissileResupplyMultiplier" };
        private static readonly List<string> Report = new();
        private static double s_started, s_next;
        private static int s_kind;
        private static bool s_pendingLayout;
        private static StartingShipConcept s_concept;
        private static int s_cancelPhase, s_departurePhase;
        private static StartingLoadout s_beforeCancelLoadout;
        private static StartingShipConcept s_beforeCancelConcept;
        private static ModuleInstance[] s_beforeCancelModules;

        static StartingShipLiveVerification()
        {
            if (!SessionState.GetBool(RunningKey, false)) return;
            Hook();
            if (!EditorApplication.isPlayingOrWillChangePlaymode) RequestPlayMode();
        }

        public static void RunBatch()
        {
            SessionState.SetBool(RunningKey, true);
            SessionState.SetString(OutputKey, Argument("-shipConceptOutput") ??
                Path.Combine(Directory.GetCurrentDirectory(), "Logs", "StartingShips"));
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Prototype_Main.unity", OpenSceneMode.Single);
            Hook();
            RequestPlayMode();
        }

        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        private static void RequestPlayMode()
        {
            EditorApplication.delayCall += () =>
            {
                if (!SessionState.GetBool(RunningKey, false)) return;
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) { RequestPlayMode(); return; }
                if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = true;
            };
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;
            Report.Clear();
            s_kind = 0;
            s_pendingLayout = false;
            s_cancelPhase = s_departurePhase = 0;
            s_started = EditorApplication.timeSinceStartup;
            s_next = s_started + 0.8;
        }

        private static void Poll()
        {
            if (!SessionState.GetBool(RunningKey, false) || !EditorApplication.isPlaying) return;
            double now = EditorApplication.timeSinceStartup;
            if (now - s_started > 90) { Finish(false, "Live verification timed out."); return; }
            if (now < s_next) return;
            try
            {
                var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
                Require(player != null, "Main scene player did not initialize.");
                var initializer = player.GetComponent<ShipInitializer>();
                Require(initializer != null, "Main scene has no ShipInitializer.");
                if (s_cancelPhase < 4)
                {
                    ValidateSelectorCancel(player, initializer, now);
                    return;
                }
                if (s_kind < 4)
                {
                    if (!s_pendingLayout)
                    {
                        s_concept = AssetDatabase.LoadAssetAtPath<StartingShipConcept>(
                            $"Assets/_Game/Data/StartingShips/ShipConcept_{Codes[s_kind]}.asset");
                        Require(s_concept != null, $"Missing {Codes[s_kind]} concept asset.");
                        GameManager.Instance.SetState(GameState.Boot);
                        Require(Select(initializer, s_concept), $"{Codes[s_kind]} could not be selected before departure.");
                        s_pendingLayout = true;
                        s_next = now + 0.30;
                        return;
                    }
                    ValidateLoadout(player, initializer, s_concept);
                    var visual = player.transform.Find("Model");
                    Require(visual != null, "Live player Model visual root is missing.");
                    ShipConceptCapture.CaptureLiveStartingShip(visual.gameObject, s_kind,
                        SessionState.GetString(OutputKey, ""));
                    Report.Add($"PASS {Codes[s_kind]}: {player.Grid.Modules.Count} modules / {player.Grid.OccupiedCells.Count} cells / live capture.");
                    s_kind++;
                    s_pendingLayout = false;
                    s_next = now + 0.15;
                    return;
                }

                var command = AssetDatabase.LoadAssetAtPath<StartingShipConcept>(
                    "Assets/_Game/Data/StartingShips/ShipConcept_Command.asset");
                if (s_departurePhase == 0)
                {
                    ValidateSupportBlocks(player);
                    ValidateDraftPool(player.Grid);
                    OpenSelector();
                    s_departurePhase = 1;
                    s_next = now + .20;
                    return;
                }
                var selector = Object.FindFirstObjectByType<StartingShipSelectorUI>();
                Require(selector != null, "Main-menu launch did not open the starting-ship selector.");
                if (s_departurePhase == 1)
                {
                    FindButton(selector.transform, command.Title).onClick.Invoke();
                    s_departurePhase = 2;
                    s_next = now + .20;
                    return;
                }
                ValidatePreview(selector, "departure command preview");
                var formation = player.GetComponent<TaskForceEscortFormation>();
                Require(formation != null && formation.EscortCount == 0, "Escorts were created before departure.");
                FindButton(selector.transform, "선택 확정 · 출항").onClick.Invoke();
                Require(initializer.SelectedConcept == command && initializer.Loadout == command.StartLoadout,
                    "Actual selector confirmation did not install the chosen command loadout.");
                Require(initializer.SelectionLocked, "Actual selector confirmation did not lock ship selection.");
                Require(GameManager.Instance.State == GameState.Playing,
                    "Actual menu/selector confirmation did not enter gameplay.");
                var lockMethod = typeof(ShipInitializer).GetMethod("LockForLaunch", BindingFlags.Public | BindingFlags.Instance);
                Require(lockMethod != null, "ShipInitializer.LockForLaunch is missing.");
                Require(formation.EscortCount == 2, "Command ship did not receive two basic escorts on departure.");
                Require(formation.GetInfo(0).Role == EscortRole.None && formation.GetInfo(1).Role == EscortRole.None,
                    "Command starting escorts are not basic unassigned boats.");
                lockMethod.Invoke(initializer, null);
                Require(formation.EscortCount == 2, "Repeated launch locking spawned extra escorts.");
                Report.Add("PASS actual menu → command choice → confirm enters gameplay with two basic unassigned escorts; locking is idempotent.");
                var before = initializer.Loadout;
                var patrol = AssetDatabase.LoadAssetAtPath<StartingShipConcept>(
                    "Assets/_Game/Data/StartingShips/ShipConcept_Patrol.asset");
                Require(!Select(initializer, patrol), "Starting ship can still change after departure.");
                Require(initializer.Loadout == before, "Rejected selection changed the active loadout.");
                Report.Add("PASS departure locks starting-ship selection.");
                var blocks = Resources.LoadAll<ModuleDefinition>("Modules")
                    .Where(d => (int)d.Type >= 17 && (int)d.Type <= 20 && d.Weight > 0f)
                    .OrderBy(d => (int)d.Type).Select(d => d.Prefab).ToList();
                ShipConceptCapture.CaptureBlockBoard(blocks, SessionState.GetString(OutputKey, ""), true);
                Finish(true, null);
            }
            catch (Exception ex)
            {
                Finish(false, ex.InnerException != null ? ex.InnerException.ToString() : ex.ToString());
            }
        }

        private static void ValidateSelectorCancel(ShipController player, ShipInitializer initializer, double now)
        {
            if (s_cancelPhase == 0)
            {
                Require(!initializer.SelectionLocked, "Ship was locked before the launch menu was used.");
                s_beforeCancelLoadout = initializer.Loadout;
                s_beforeCancelConcept = initializer.SelectedConcept;
                s_beforeCancelModules = player.Grid.Modules.ToArray();
                OpenSelector();
                s_cancelPhase = 1;
            }
            else if (s_cancelPhase == 1)
            {
                var selector = Object.FindFirstObjectByType<StartingShipSelectorUI>();
                Require(selector != null, "Launch menu did not show the actual starting-ship selector.");
                var choices = new HashSet<Button>();
                foreach (var concept in StartingShipCatalog.All)
                    if (concept != null && concept.StartLoadout != null)
                        choices.Add(FindButton(selector.transform, concept.Title));
                Require(choices.Count == 4, $"Actual selector has {choices.Count} ship choices instead of four.");
                Require(selector.GetComponentsInChildren<Button>().Length == (NavalBaseMenu.Active!=null?8:6),
                    "Actual selector does not have four ship choices plus back/confirm controls.");
                var command = AssetDatabase.LoadAssetAtPath<StartingShipConcept>(
                    "Assets/_Game/Data/StartingShips/ShipConcept_Command.asset");
                int initialIndex=selector.SelectedIndex;
                selector.Navigate(1);
                Require(selector.SelectedIndex==(initialIndex+1)%4,"Right navigation did not advance the berth.");
                selector.Navigate(-1);
                Require(selector.SelectedIndex==initialIndex,"Left navigation did not restore the berth.");
                FindButton(selector.transform, command.Title).onClick.Invoke();
                s_cancelPhase = 2;
            }
            else if (s_cancelPhase == 2)
            {
                var selector = Object.FindFirstObjectByType<StartingShipSelectorUI>();
                Require(selector != null, "Selector disappeared while inspecting its command preview.");
                ValidatePreview(selector, "cancel-path command preview");
                Require(initializer.Loadout == s_beforeCancelLoadout && initializer.SelectedConcept == s_beforeCancelConcept,
                    "Browsing a ship choice installed a loadout before confirmation.");
                var canvas = selector.GetComponentInParent<Canvas>();
                Require(canvas != null, "Actual selector is not hosted by the main UI Canvas.");
                ShipConceptCapture.CaptureCanvas(canvas.rootCanvas,
                    Path.Combine(SessionState.GetString(OutputKey, ""), "ui_01_starting_ship_selector.png"));
                FindButton(selector.transform, "돌아가기").onClick.Invoke();
                s_cancelPhase = 3;
            }
            else
            {
                Require(Object.FindFirstObjectByType<StartingShipSelectorUI>() == null,
                    "Back button did not close the actual selector.");
                Require(initializer.Loadout == s_beforeCancelLoadout && initializer.SelectedConcept == s_beforeCancelConcept,
                    "Cancelling the ship selector changed the active ship/loadout.");
                Require(!initializer.SelectionLocked && player.Grid.Modules.SequenceEqual(s_beforeCancelModules),
                    "Cancelling the selector changed live modules or locked departure.");
                Require(FindButton(null, "출항  /  작전 시작").interactable,
                    "Back button did not restore main-menu launch interaction.");
                Report.Add("PASS actual Canvas selector: four ship choices; command preview renders; browsing/cancel preserve the ship; back restores launch.");
                s_cancelPhase = 4;
            }
            s_next = now + (NavalBaseMenu.Active!=null?2.5:.20);
        }

        private static void OpenSelector()
        {
            var launch = FindButton(null, "출항  /  작전 시작");
            Require(launch.interactable, "Main-menu launch button is not interactable.");
            launch.onClick.Invoke();
        }

        private static Button FindButton(Transform parent, string name)
        {
            var buttons = parent != null ? parent.GetComponentsInChildren<Button>()
                : Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            var button = buttons.FirstOrDefault(b => b.gameObject.activeInHierarchy && b.gameObject.name == name);
            Require(button != null, $"Actual UI button '{name}' is missing.");
            return button;
        }

        private static void ValidatePreview(StartingShipSelectorUI selector, string description)
        {
            var harbor=NavalBaseMenu.Active;
            if(harbor!=null)
            {
                Require(harbor.ShipCount==4 && harbor.SelectedIndex==selector.SelectedIndex,description+": selected berth differs from UI.");
                Require(harbor.TrafficCount>=2,description+": missing aircraft/ship traffic.");
                harbor.RenderNow();
                Report.Add($"PASS {description}: four moored ships, left/right navigation, aircraft and harbor traffic.");
                return;
            }
            var preview = selector.GetComponentInChildren<CodexPreview>();
            Require(preview != null && preview.RendererCount > 0, description + ": no native model renderers.");
            float coverage = preview.RenderAndMeasure();
            Require(coverage > 0f, description + ": native model preview is blank.");
            Report.Add($"PASS {description}: {preview.RendererCount} native renderers, {coverage:P2} non-background pixels.");
        }

        private static bool Select(ShipInitializer initializer, StartingShipConcept concept)
        {
            var methods = typeof(ShipInitializer).GetMethods(BindingFlags.Public | BindingFlags.Instance);
            var method = methods.FirstOrDefault(m => m.ReturnType == typeof(bool) &&
                m.Name == "TrySelect" && m.GetParameters().Length == 2 &&
                m.GetParameters()[0].ParameterType == typeof(StartingShipConcept));
            Require(method != null, "ShipInitializer.TrySelect(StartingShipConcept, out string) is missing.");
            var args = new object[] { concept, null };
            bool result = (bool)method.Invoke(initializer, args);
            if (!result) Report.Add("Selection rejected: " + args[1]);
            return result;
        }

        public static void ValidateLoadout(ShipController player, ShipInitializer initializer, StartingShipConcept concept)
        {
            Require(concept.StartLoadout != null, $"{concept.name}: start loadout is missing.");
            Require(initializer.Loadout == concept.StartLoadout, $"{concept.name}: initializer uses another loadout.");
            Require(initializer.SelectedConcept == concept && !initializer.SelectionLocked,
                $"{concept.name}: selected class/launch-lock state disagrees with the pre-launch loadout.");
            var entries = concept.StartLoadout.Entries.Where(e => e.Module != null).ToList();
            Require(player.Grid.Modules.Count == entries.Count,
                $"{concept.name}: {player.Grid.Modules.Count} installed modules, expected {entries.Count}.");
            var occupied = new HashSet<GridCoord>();
            var footprint = new List<GridCoord>();
            foreach (var e in entries)
            {
                var instance = player.Grid.Modules.FirstOrDefault(m => m.Definition == e.Module && m.Origin.Equals(e.Origin));
                Require(instance != null, $"{concept.name}: {e.Module.Id} is missing at {e.Origin}.");
                Require(instance.RotationSteps == e.RotationSteps, $"{e.Module.Id}: incorrect starting rotation.");
                Require(instance.Runtime != null && instance.Runtime.Instance == instance,
                    $"{e.Module.Id}: live ModuleFactory binding is missing.");
                Require(instance.IsOperational && Mathf.Approximately(instance.Hp, instance.MaxHp),
                    $"{e.Module.Id}: starting module is damaged or inactive.");
                var expected = player.Grid.CoordToLocal(e.Origin) +
                    ModuleFactory.GetFootprintOffset(e.Module, e.RotationSteps, player.Grid.CellSize);
                Require(Vector3.Distance(instance.Runtime.transform.localPosition, expected) < 0.001f,
                    $"{e.Module.Id}: runtime position disagrees with the native grid.");
                Require(e.Module.Prefab.GetComponent<ModuleRuntime>() != null, $"{e.Module.Id}: prefab lacks ModuleRuntime.");
                ShipGrid.GetFootprint(e.Module, e.Origin, e.RotationSteps, footprint);
                foreach (var cell in footprint)
                    Require(occupied.Add(cell), $"{concept.name}: overlapping starting cells at {cell}.");
            }
            Require(occupied.SetEquals(player.Grid.OccupiedCells), $"{concept.name}: live cell coverage disagrees with the loadout.");
            Require(occupied.Count >= 6 && occupied.Count <= 8,
                $"{concept.name}: starting hull has {occupied.Count} cells; expected small 6–8 cell design.");
            Require(entries.Any(e => e.Module.Type == ModuleType.Bridge), $"{concept.name}: no starting bridge.");
            Require(concept.Kind == StartShipKind.Patrol || entries.Any(e => (int)e.Module.Type >= 17 && (int)e.Module.Type <= 20),
                $"{concept.name}: no new support block in starting loadout.");
        }

        private static void ValidateSupportBlocks(ShipController player)
        {
            var grid = player.Grid;
            var systems = player.Systems;
            var factory = player.GetComponent<ModuleFactory>();
            Require(factory != null && systems != null, "Live module factory/systems are missing.");
            foreach (var m in grid.Modules.ToArray())
                if ((int)m.Definition.Type >= 17 && (int)m.Definition.Type <= 20) factory.Uninstall(m);
            typeof(ShipController).GetField("_currentSpeed", Fields).SetValue(player, 0f);
            systems.Recalculate();
            foreach (var property in SupportProperties) Close(Read(systems, property), 1f, $"baseline {property}");
            int baseTracks = systems.MaxTrackedTargets;
            float baseSpeed = player.BaseMaxSpeed;

            var vlsDef = AssetDatabase.LoadAssetAtPath<ModuleDefinition>("Assets/_Game/Data/Modules/mod_vls.asset");
            var autoDef = AssetDatabase.LoadAssetAtPath<ModuleDefinition>("Assets/_Game/Data/Modules/mod_autocannon.asset");
            var vls = grid.Modules.FirstOrDefault(m => m.Definition == vlsDef) ?? InstallAtFree(factory, grid, vlsDef);
            var auto = grid.Modules.FirstOrDefault(m => m.Definition == autoDef) ?? InstallAtFree(factory, grid, autoDef);
            var samDef = AssetDatabase.LoadAssetAtPath<ModuleDefinition>("Assets/_Game/Data/Modules/mod_sam.asset");
            var sam = grid.Modules.FirstOrDefault(m => m.Definition == samDef) ?? InstallAtFree(factory, grid, samDef);
            var vlsBase = vls.Runtime.Stats;
            var autoBase = auto.Runtime.Stats;
            var samBase = sam.Runtime.Stats;
            var originalDefinitionStats = vlsDef.Stats;
            var vlsAmmo = ((IAmmoUser)vls.Runtime).Ammo;
            float baseAmmoInterval = vlsAmmo.Interval;
            Require(vlsAmmo.Consume(), "VLS ammo-progress test could not consume one round.");
            vlsAmmo.Tick(baseAmmoInterval * 0.20f);
            float ammoProgress = vlsAmmo.SecondsToNext / baseAmmoInterval;
            int ammoCount = vlsAmmo.Current;
            var definitions = Resources.LoadAll<ModuleDefinition>("Modules");
            var installed = new List<ModuleInstance>();
            for (int type = 17; type <= 20; type++)
            {
                var def = definitions.FirstOrDefault(d => (int)d.Type == type && d.Weight > 0f);
                Require(def != null, $"Operational support ModuleType {type} is absent from Resources/Modules.");
                Require(def.Prefab != null && def.Prefab.GetComponent<ModuleRuntime>() != null,
                    $"{def.Id}: no live runtime prefab.");
                Require(def.Prefab.GetComponent<ModuleRuntime>().GetType().Name != "ConceptBlockModule",
                    $"{def.Id}: prototype runtime remains in the live block.");
                installed.Add(InstallAtFree(factory, grid, def));
                if (def.MaxCount == 1) Require(!grid.HasAnyValidPlacement(def), $"{def.Id}: max-count restriction is ignored.");
            }
            systems.Recalculate();
            Close(Read(systems, "EscortFireRateMultiplier"), 1.15f, "fleet relay fire-rate support");
            Close(Read(systems, "SpeedMultiplier"), 1.10f, "turbo speed");
            Close(Read(systems, "AccelerationMultiplier"), 1.15f, "turbo acceleration");
            Close(Read(systems, "GuidedWeaponRangeMultiplier"), 1.10f, "fire control guided range");
            Close(Read(systems, "MissileReloadMultiplier"), 0.85f, "logistics missile reload");
            Close(Read(systems, "MissileResupplyMultiplier"), 0.85f, "logistics missile resupply");
            Require(systems.MaxTrackedTargets == baseTracks + 2, "Fire control did not add two tracks.");
            Close(player.BaseMaxSpeed, baseSpeed * 1.10f, "ship controller consumes turbo speed");
            Close(vls.Runtime.Stats.Range, vlsBase.Range * 1.10f, "VLS consumes guided-range support");
            Close(vls.Runtime.Stats.ReloadTime, vlsBase.ReloadTime * 0.85f, "VLS consumes reload support");
            Close(vls.Runtime.Stats.AmmoReloadTime, vlsBase.AmmoReloadTime * 0.85f, "VLS consumes resupply support");
            Close(vlsAmmo.Interval, baseAmmoInterval * 0.85f, "actual VLS ammo resupply interval");
            Close(vlsAmmo.SecondsToNext / vlsAmmo.Interval, ammoProgress, "VLS resupply progress survives support installation");
            Require(vlsAmmo.Current == ammoCount, "Installing logistics generated free VLS ammunition.");
            Close(sam.Runtime.Stats.Range, samBase.Range * 1.10f, "SAM consumes guided-range support");
            Close(sam.Runtime.Stats.ReloadTime, samBase.ReloadTime, "missile logistics does not accelerate SAM");
            Close(auto.Runtime.Stats.Range, autoBase.Range, "guided range does not alter autocannon range");
            Close(auto.Runtime.Stats.Damage, autoBase.Damage, "turbo damage remains baseline while stopped");
            var escortObject = new GameObject("Verification relay cooldown probe");
            var escortDefense = escortObject.AddComponent<EscortDefense>();
            escortDefense.enabled = false;
            string[] timers = { "_gunAt", "_samAt", "_jamAt", "_aswAt", "_strikeAt" };
            float now = Time.time;
            typeof(EscortDefense).GetField("_relayRate", Fields).SetValue(escortDefense, 1f);
            foreach (var timer in timers) typeof(EscortDefense).GetField(timer, Fields).SetValue(escortDefense, now + 10f);
            Close(escortDefense.CommandRateMultiplier, 1.15f, "escort consumes fleet-relay rate");
            var syncRelay = typeof(EscortDefense).GetMethod("SyncRelayRate", Fields);
            Require(syncRelay != null, "EscortDefense relay-rate synchronization is missing.");
            syncRelay.Invoke(escortDefense, null);
            foreach (var timer in timers)
                Close((float)typeof(EscortDefense).GetField(timer, Fields).GetValue(escortDefense) - now,
                    10f / 1.15f, "fleet relay accelerates pending escort cooldown " + timer);
            typeof(ShipController).GetField("_currentSpeed", Fields).SetValue(player, player.BaseMaxSpeed * 0.65f);
            Close(Read(systems, "MovingGunDamageMultiplier"), 1.10f, "turbo moving firepower");
            Close(auto.Runtime.Stats.Damage, autoBase.Damage * 1.10f, "autocannon consumes moving-firepower support");
            Close(vlsDef.Stats.ReloadTime, originalDefinitionStats.ReloadTime, "support does not mutate the shared VLS definition");
            installed[0].TakeDamage(1f);
            Close(Read(systems, "EscortFireRateMultiplier"), 1.15f, "operational partially damaged relay retains effect");
            var weapon = typeof(Game.Modules.Runtime.VlsModule).GetField("weapon", Fields)?.GetValue(vls.Runtime) as WeaponController;
            Require(weapon != null, "VLS weapon controller is missing.");
            typeof(WeaponController).GetField("_cooldown", Fields).SetValue(weapon, 0f);
            Require(weapon.TryFire(vls.Runtime.Stats.ReloadTime), "VLS cooldown test could not fire.");
            Close((float)typeof(WeaponController).GetField("_cooldown", Fields).GetValue(weapon),
                vlsBase.ReloadTime * 0.85f, "actual VLS shot cooldown");
            Report.Add("PASS four operational support blocks, ship mobility, moving gun damage, guided range, missile reload/resupply and actual VLS cooldown.");

            foreach (var block in installed) block.TakeDamage(block.MaxHp + 1f);
            typeof(ShipController).GetField("_currentSpeed", Fields).SetValue(player, 0f);
            systems.Recalculate();
            foreach (var property in SupportProperties) Close(Read(systems, property), 1f, $"destroyed {property}");
            Require(systems.MaxTrackedTargets == baseTracks, "Destroyed fire control still contributes tracks.");
            Close(vls.Runtime.Stats.Range, vlsBase.Range, "destroyed range support reverted");
            Close(vls.Runtime.Stats.ReloadTime, vlsBase.ReloadTime, "destroyed reload support reverted");
            Close(vls.Runtime.Stats.AmmoReloadTime, vlsBase.AmmoReloadTime, "destroyed resupply support reverted");
            Close(vlsAmmo.Interval, baseAmmoInterval, "destroyed logistics reverts actual ammo interval");
            Close(vlsAmmo.SecondsToNext / vlsAmmo.Interval, ammoProgress, "VLS resupply progress survives logistics destruction");
            syncRelay.Invoke(escortDefense, null);
            foreach (var timer in timers)
                Close((float)typeof(EscortDefense).GetField(timer, Fields).GetValue(escortDefense) - now,
                    10f, "destroyed relay restores pending escort cooldown " + timer);
            Object.DestroyImmediate(escortObject);
            foreach (var block in installed) factory.Uninstall(block);
            systems.Recalculate();
            foreach (var property in SupportProperties) Close(Read(systems, property), 1f, $"removed {property}");
            var replacement = new List<ModuleInstance>();
            foreach (var block in installed) replacement.Add(InstallAtFree(factory, grid, block.Definition));
            systems.Recalculate();
            Close(Read(systems, "MissileReloadMultiplier"), 0.85f, "replacement logistics works");
            foreach (var block in replacement) factory.Uninstall(block);
            systems.Recalculate();
            foreach (var property in SupportProperties) Close(Read(systems, property), 1f, $"living removal {property}");
            Close(vlsAmmo.Interval, baseAmmoInterval, "live logistics removal reverts actual ammo interval");
            Require(vlsAmmo.Current == ammoCount, "Support destruction/removal changed ammunition count.");
            Report.Add("PASS max-count rules, ammo count/progress preservation and support reversion after destruction and live removal.");
        }

        private static ModuleInstance InstallAtFree(ModuleFactory factory, ShipGrid grid, ModuleDefinition def)
        {
            Require(def != null, "Requested test module is missing.");
            for (int x = -grid.MaxHalfLength; x <= grid.MaxHalfLength; x++)
                for (int z = -grid.MaxHalfBeam; z <= grid.MaxHalfBeam; z++)
                    for (int rotation = 0; rotation < (def.CanRotate ? 4 : 1); rotation++)
                    {
                        var origin = new GridCoord(x, z);
                        if (!grid.CanPlace(def, origin, rotation, out _)) continue;
                        var module = factory.Install(def, origin, rotation);
                        Require(module != null, $"Factory rejected validated placement of {def.Id}.");
                        return module;
                    }
            throw new InvalidOperationException($"No valid test placement exists for {def.Id}.");
        }

        private static void ValidateDraftPool(ShipGrid grid)
        {
            var config = AssetDatabase.LoadAssetAtPath<ProgressionConfig>("Assets/_Game/Data/Config/ProgressionConfig.asset");
            var method = typeof(RefitDraft).GetMethod("CollectInstallDefinitions", BindingFlags.Public | BindingFlags.Static);
            Require(method != null, "RefitDraft has no public merged install-pool collector.");
            var defs = (List<ModuleDefinition>)method.Invoke(null, new object[] { config });
            Require(defs.Count == defs.Distinct().Count(), "Merged install definitions contain duplicates.");
            Require(defs.All(d => d != null && d.Weight > 0f && d.Type != ModuleType.Bridge),
                "Weight-zero prototype or bridge entered the install pool.");
            for (int type = 17; type <= 20; type++) Require(defs.Any(d => (int)d.Type == type), $"Support type {type} cannot appear in upgrade cards.");
            var draft = Object.FindFirstObjectByType<RefitDraft>();
            Require(draft != null, "Main scene draft is missing.");
            var build = typeof(RefitDraft).GetMethod("BuildInstallPool", Fields);
            var poolField = typeof(RefitDraft).GetField("_installPool", Fields);
            Require(build != null && poolField != null, "Draft install-pool test entry points are missing.");
            build.Invoke(draft, new object[] { new HashSet<ModuleDefinition>() });
            var pool = (List<ModuleDefinition>)poolField.GetValue(draft);
            for (int type = 17; type <= 20; type++) Require(pool.Any(d => (int)d.Type == type), $"Live card draft excludes support type {type}.");
            var player = GameManager.Instance.Player;
            var limited = defs.First(d => (int)d.Type == 17);
            var probe = InstallAtFree(player.GetComponent<ModuleFactory>(), grid, limited);
            build.Invoke(draft, new object[] { new HashSet<ModuleDefinition>() });
            Require(!pool.Contains(limited), "Live card draft ignores an installed support block's max-count limit.");
            player.GetComponent<ModuleFactory>().Uninstall(probe);
            build.Invoke(draft, new object[] { new HashSet<ModuleDefinition>() });
            Require(pool.Contains(limited), "Removing a support block does not restore its installation card.");
            Report.Add("PASS draft merges all four support blocks, excludes zero-weight bridges/prototypes and respects live max-count/removal.");
        }

        private static float Read(ShipSystems systems, string name)
        {
            var property = typeof(ShipSystems).GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Require(property != null, $"ShipSystems.{name} is missing.");
            return Convert.ToSingle(property.GetValue(systems));
        }

        private static void Close(float actual, float expected, string description)
            => Require(Mathf.Abs(actual - expected) <= Mathf.Max(0.001f, Mathf.Abs(expected) * 0.0001f),
                $"{description}: expected {expected:F4}, got {actual:F4}.");

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }

        private static void Finish(bool success, string failure)
        {
            string output = SessionState.GetString(OutputKey, "");
            Directory.CreateDirectory(output);
            if (!success) Report.Add("FAIL " + failure);
            File.WriteAllText(Path.Combine(output, "live_validation.txt"), string.Join("\n", Report));
            Debug.Log(success ? "[StartingShipLiveVerification] PASS — actual selector/cancel/confirm, four live starting ships, support blocks, draft pool, departure lock."
                : "[StartingShipLiveVerification] FAIL — " + failure);
            SessionState.SetBool(RunningKey, false);
            EditorApplication.update -= Poll;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 2);
            else EditorApplication.ExitPlaymode();
        }
    }
}

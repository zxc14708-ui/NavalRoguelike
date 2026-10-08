using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Game.Data;
using Game.Modules;
using Game.Modules.Runtime;
using Game.Ship;
using static Game.EditorTools.NavalEditorUtil;

namespace Game.EditorTools
{
    /// <summary>Independent native art/data prototypes. Never rewrites Prototype_Main or its starting loadout.</summary>
    public static class NavalStartingShipBuilder
    {
        public const string Art = "Assets/_Game/Art/StartingShips";
        public const string Prefabs = "Assets/_Game/Prefabs/StartingShips";
        public const string Data = "Assets/_Game/Data/StartingShips";
        public const string LiveModules = "Assets/_Game/Resources/Modules";
        public const string CatalogPath = "Assets/_Game/Resources/StartingShips/Catalog.asset";
        private static readonly string[] Codes = { "Patrol", "Command", "Assault", "Missile" };
        private static readonly string[] Names = { "범용 초계함", "지휘함", "고속 강습함", "미사일 구축함" };
        private static readonly Color[] Colors = {
            new Color(.16f,.72f,.84f), new Color(.35f,.88f,.67f),
            new Color(.98f,.49f,.24f), new Color(.51f,.66f,1f) };
        private static Material Hull, Deck, Dark, Glass, White, Warning;
        private static Material[] Role;
        private static string _meshPrefix;
        private static int _meshIndex;
        private static readonly List<string> Report = new();

        public static void RunBatch()
        {
            try
            {
                BuildAndCapture();
                Debug.Log("[StartingShips] PASS — 4 bridges, 4 operational blocks, 8 native loadouts/prefabs, playable starter catalog, scene and captures.");
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("Naval/Art/Build Starting Ship Concepts and Capture", priority = 45)]
        public static void BuildAndCapture()
        {
            Report.Clear();
            foreach (string p in new[] { Art, Art+"/Meshes", Art+"/Materials", Prefabs,
                Prefabs+"/Bridges", Prefabs+"/Blocks", Prefabs+"/Assemblies", Data, Data+"/Modules", LiveModules,
                "Assets/_Game/Resources/StartingShips" }) EnsureFolder(p);
            Palette();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bridges = new List<GameObject>();
            var starts = new List<GameObject>();
            var grown = new List<GameObject>();
            ModuleDefinition[] blocks = BuildBlocks();
            for (int i = 0; i < 4; i++)
            {
                var bridge = BuildBridge(i);
                bridges.Add(bridge);
                var bridgeDef = CloneBridgeDefinition(i, bridge);
                var start = MakeLoadout(i, false, bridgeDef, blocks);
                var expanded = MakeLoadout(i, true, bridgeDef, blocks);
                var first = Assemble(i, false, start);
                var final = Assemble(i, true, expanded);
                starts.Add(first); grown.Add(final);
                MakeConcept(i, bridgeDef, start, expanded, first, final);
            }
            var catalog=CreateSO<StartingShipCatalog>(CatalogPath);
            Configure(catalog,so=>SetArray(so,"ships",Codes.Select(c=>(UnityEngine.Object)AssetDatabase.LoadAssetAtPath<StartingShipConcept>(Data+"/ShipConcept_"+c+".asset")).ToArray()));
            AssetDatabase.SaveAssets();
            string outDir = Argument("-shipConceptOutput") ?? Path.Combine(Directory.GetCurrentDirectory(), "Logs", "StartingShips");
            Directory.CreateDirectory(outDir);
            ShipConceptCapture.CaptureAll(grown, starts, bridges, outDir);
            ShipConceptCapture.CaptureBlockBoard(blocks.Select(b=>b.Prefab).ToList(),outDir,true);
            BuildPreviewScene(starts, grown, bridges);
            File.WriteAllText(Path.Combine(outDir, "asset_validation.txt"), string.Join("\n", Report));
            AssetDatabase.SaveAssets();
            AssetDatabase.ExportPackage(new[]{Art,Prefabs,Data,CatalogPath,
                LiveModules+"/mod_fleetrelay.asset",LiveModules+"/mod_turbointake.asset",
                LiveModules+"/mod_firecontrolarray.asset",LiveModules+"/mod_missilelogistics.asset",
                "Assets/_Game/Art/Icons/ICON_mod_fleetrelay.png","Assets/_Game/Art/Icons/ICON_mod_turbointake.png",
                "Assets/_Game/Art/Icons/ICON_mod_firecontrolarray.png","Assets/_Game/Art/Icons/ICON_mod_missilelogistics.png",
                "Assets/_Game/Scripts/Data/StartingShipConcept.cs",
                "Assets/_Game/Scripts/Data/StartingShipCatalog.cs",
                "Assets/_Game/Scripts/UI/StartingShipSelectorUI.cs",
                "Assets/_Game/Scripts/Modules/ModuleType.cs","Assets/_Game/Scripts/Modules/ModuleDefinition.cs",
                "Assets/_Game/Scripts/Modules/ModuleRuntime.cs","Assets/_Game/Scripts/Ship/ShipSystems.cs",
                "Assets/_Game/Scripts/Ship/ShipController.cs","Assets/_Game/Scripts/Ship/ShipInitializer.cs",
                "Assets/_Game/Scripts/TaskForce/EscortDefense.cs",
                "Assets/_Game/Scripts/Combat/AmmoMagazine.cs",
                "Assets/_Game/Scripts/Refit/RefitDraft.cs","Assets/_Game/Scripts/UI/ModuleCardText.cs",
                "Assets/_Game/Scripts/UI/ModuleStatusUI.cs","Assets/_Game/Scripts/UI/RefitUI.Cards.cs",
                "Assets/_Game/Scripts/UI/CodexCatalog.cs","Assets/_Game/Scripts/UI/HUDView.cs",
                "Assets/_Game/Scripts/UI/CodexPreview.cs",
                "Assets/_Game/Scripts/Modules/Runtime/FleetRelayModule.cs",
                "Assets/_Game/Scripts/Modules/Runtime/TurboIntakeModule.cs",
                "Assets/_Game/Scripts/Modules/Runtime/FireControlArrayModule.cs",
                "Assets/_Game/Scripts/Modules/Runtime/MissileLogisticsModule.cs",
                "Assets/_Game/Scripts/Modules/Runtime/ConceptBlockModule.cs",
                "Assets/_Game/Editor/NavalStartingShipBuilder.cs",
                "Assets/_Game/Editor/ShipConceptCapture.cs",
                "Assets/_Game/Editor/StartingShipLiveVerification.cs",
                "Assets/_Game/Scenes/StartingShipConcepts.unity"},
                Path.Combine(outDir,"StartingShipConcepts.unitypackage"),ExportPackageOptions.Recurse);
        }

        private static string Argument(string key)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i=0;i<args.Length-1;i++) if (args[i]==key) return args[i+1];
            return null;
        }

        private static void Palette()
        {
            Hull = Existing("MAT_art_navy_grey", new Color(.38f,.44f,.48f));
            Deck = Existing("MAT_art_deck_grey", new Color(.27f,.32f,.34f));
            Dark = Existing("MAT_art_gunmetal", new Color(.11f,.15f,.18f));
            Glass = Existing("MAT_art_glass", new Color(.04f,.17f,.21f));
            White = Existing("MAT_art_medical_white", new Color(.8f,.86f,.86f));
            Warning = Existing("MAT_art_warning", new Color(.95f,.73f,.25f));
            Role = new Material[4];
            for(int i=0;i<4;i++) Role[i]=OwnMaterial("Role_"+Codes[i],Colors[i]);
        }

        private static Material Existing(string name, Color fallback)
            => AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/"+name+".mat") ?? OwnMaterial(name,fallback);

        private static Material OwnMaterial(string name, Color color)
        {
            string path=Art+"/Materials/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat!=null)return mat;
            mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){ name=name };
            mat.SetColor("_BaseColor",color); mat.SetFloat("_Metallic",.2f); mat.SetFloat("_Smoothness",.35f);
            AssetDatabase.CreateAsset(mat,path); return mat;
        }

        private static GameObject BuildBridge(int kind)
        {
            _meshPrefix="Bridge_"+Codes[kind]; _meshIndex=0;
            var go=new GameObject("MOD_Bridge_"+Codes[kind]);
            var runtime=go.AddComponent<BridgeModule>();
            var original=LoadModule("bridge").Prefab.GetComponent<BridgeModule>();
            EditorUtility.CopySerialized(original,runtime);
            var model=new GameObject("Model").transform; model.SetParent(go.transform,false);
            // A 3x1 block centered on the middle cell, bottom at y=0; +Z is bow.
            Facet("Base",model,new Vector3(0,.18f,0),new Vector3(1.78f,.36f,5.78f),.94f,0,Deck);
            for(int side=-1;side<=1;side+=2)
                SidePanel("RoleStripe",model,new Vector3(0,.18f,0),new Vector3(1.78f,.36f,5.78f),.94f,side,.29f,.4f,.08f,3.1f,Role[kind]);
            Facet("EngineHouse",model,new Vector3(0,.59f,-.52f),new Vector3(1.61f,.46f,1.6f),.95f,0,Hull);
            for(int side=-1;side<=1;side+=2)
            {
                for(int n=0;n<5;n++) SidePanel("EngineVent",model,new Vector3(0,.59f,-.52f),new Vector3(1.61f,.46f,1.6f),.95f,side,.62f,-.9f+n*.18f,.12f,.085f,Dark);
            }
            Transform radar;
            float funnelY;
            if(kind==0)
            {
                Facet("BridgeCabin",model,new Vector3(0,1.16f,1.61f),new Vector3(1.7f,1.54f,2.16f),.92f,.06f,Hull);
                FacetWindows(model,new Vector3(0,1.16f,1.61f),new Vector3(1.7f,1.54f,2.16f),.92f,.06f,1.72f,1.65f);
                Facet("Roof",model,new Vector3(0,2.01f,1.61f),new Vector3(1.76f,.17f,2.18f),1,0,Deck);
                Pipe("Mast",model,new Vector3(0,2.42f,1.35f),.065f,.72f,Dark);
                radar=Socket(model,"RadarPivot",new Vector3(0,2.78f,1.35f));
                Box("NavigationArray",radar,Vector3.zero,new Vector3(1.16f,.12f,.18f),Glass);
                Pipe("Radome",model,new Vector3(.5f,2.25f,1.8f),.19f,.34f,White);
                funnelY=1.48f;
                Facet("Funnel",model,new Vector3(0,1.08f,-2.04f),new Vector3(.94f,1.38f,1.04f),.85f,-.08f,Hull);
                Box("FunnelCap",model,new Vector3(0,1.8f,-2.1f),new Vector3(.88f,.12f,.96f),Dark);
                Doors(model,new Vector3(0,1.16f,1.61f),new Vector3(1.7f,1.54f,2.16f),.92f,.85f,1.02f);
                Pipe("SearchlightFoot",model,new Vector3(-.46f,2.16f,2.12f),.12f,.16f,Dark);
                Box("SearchlightHousing",model,new Vector3(-.46f,2.3f,2.12f),new Vector3(.32f,.23f,.3f),Hull);
                Box("SearchlightLens",model,new Vector3(-.46f,2.3f,2.274f),new Vector3(.24f,.16f,.02f),White);
                for(int side=-1;side<=1;side+=2)
                    SidePanel("RescueLocker",model,new Vector3(0,1.16f,1.61f),new Vector3(1.7f,1.54f,2.16f),.92f,side,.92f,1.8f,.38f,.48f,White);
            }
            else if(kind==1)
            {
                Facet("ArmoredCIC",model,new Vector3(0,1.06f,1.45f),new Vector3(1.78f,1.36f,2.5f),.88f,0,Hull);
                Facet("UpperBridge",model,new Vector3(0,1.95f,1.75f),new Vector3(1.66f,.71f,1.79f),.87f,.08f,Hull);
                FacetWindows(model,new Vector3(0,1.95f,1.75f),new Vector3(1.66f,.71f,1.79f),.87f,.08f,2.09f,1.96f);
                Facet("CommandRoof",model,new Vector3(0,2.39f,1.8f),new Vector3(1.76f,.16f,1.87f),1,0,Deck);
                Facet("CommsTrunk",model,new Vector3(0,1.5f,-.42f),new Vector3(.72f,1.4f,.66f),.8f,0,Hull);
                Box("AntennaPlatform",model,new Vector3(0,2.2f,-.42f),new Vector3(1.36f,.12f,.48f),Deck);
                Pipe("CommsMast",model,new Vector3(0,2.61f,-.42f),.11f,.82f,Hull);
                radar=Socket(model,"RadarPivot",new Vector3(0,3.02f,-.42f));
                Box("CommandSweep",radar,Vector3.zero,new Vector3(1.24f,.16f,.24f),Glass);
                for(int side=-1;side<=1;side+=2)
                {
                    Pipe("SATCOM",model,new Vector3(side*.53f,2.58f,1.26f),.22f,.3f,White);
                    Pipe("WhipBase",model,new Vector3(side*.57f,2.29f,-.42f),.07f,.12f,Hull);
                    Pipe("Whip",model,new Vector3(side*.57f,2.7f,-.42f),.021f,.8f,Dark);
                }
                FrontPanel("CommandStatus",model,new Vector3(0,1.06f,1.45f),new Vector3(1.78f,1.36f,2.5f),.88f,0,0,1.33f,.34f,.08f,Role[kind]);
                funnelY=1.85f;
                Facet("BroadFunnel",model,new Vector3(0,1.26f,-2.03f),new Vector3(1.2f,1.64f,1.18f),.9f,0,Hull);
                Box("TwinExhaustA",model,new Vector3(-.28f,2.11f,-2.03f),new Vector3(.35f,.13f,.78f),Dark);
                Box("TwinExhaustB",model,new Vector3(.28f,2.11f,-2.03f),new Vector3(.35f,.13f,.78f),Dark);
                Doors(model,new Vector3(0,1.06f,1.45f),new Vector3(1.78f,1.36f,2.5f),.88f,.82f,.82f);
                for(int side=-1;side<=1;side+=2)
                {
                    SidePanel("CICEquipmentPanel",model,new Vector3(0,1.06f,1.45f),new Vector3(1.78f,1.36f,2.5f),.88f,side,1.12f,1.72f,.48f,.62f,Deck);
                    Pipe("SATCOMCollar",model,new Vector3(side*.53f,2.48f,1.26f),.25f,.1f,Deck);
                }
            }
            else if(kind==2)
            {
                Facet("LowRakedBridge",model,new Vector3(0,.98f,1.48f),new Vector3(1.76f,1.19f,2.52f),.77f,-.3f,Hull);
                FacetWindows(model,new Vector3(0,.98f,1.48f),new Vector3(1.76f,1.19f,2.52f),.77f,-.3f,1.36f,1.58f);
                Facet("ArmoredRoof",model,new Vector3(0,1.69f,1.15f),new Vector3(1.45f,.19f,1.98f),.93f,0,Dark);
                for(int side=-1;side<=1;side+=2)
                {
                    Facet("ArmorCheek",model,new Vector3(side*.69f,.95f,1.63f),new Vector3(.29f,.56f,1.72f),.8f,-.13f,Deck);
                    Facet("TurboDuct",model,new Vector3(side*.49f,.98f,-1.78f),new Vector3(.65f,1.12f,1.8f),.84f,-.17f,Hull);
                    Box("BlackExhaust",model,new Vector3(side*.49f,1.6f,-2.05f),new Vector3(.49f,.12f,.73f),Dark);
                    FrontPanel("IntakeMouth",model,new Vector3(side*.49f,.98f,-1.78f),new Vector3(.65f,1.12f,1.8f),.84f,-.17f,side*.49f,1.12f,.38f,.32f,Dark);
                    FrontPanel("IntakeWarning",model,new Vector3(side*.49f,.98f,-1.78f),new Vector3(.65f,1.12f,1.8f),.84f,-.17f,side*.49f,1.38f,.38f,.045f,Role[kind]);
                    for(int n=0;n<3;n++) FrontPanel("IntakeGrille",model,new Vector3(side*.49f,.98f,-1.78f),new Vector3(.65f,1.12f,1.8f),.84f,-.17f,side*.49f,1.03f+n*.09f,.36f,.025f,Hull,.027f);
                }
                radar=Socket(model,"RadarPivot",new Vector3(0,2.12f,.55f));
                Pipe("ShortMastFoot",model,new Vector3(0,1.8f,.55f),.13f,.1f,Hull);
                Pipe("ShortMast",model,new Vector3(0,1.96f,.55f),.055f,.34f,Dark);
                Box("CompactArray",radar,Vector3.zero,new Vector3(.77f,.13f,.19f),Glass);
                Doors(model,new Vector3(0,.98f,1.48f),new Vector3(1.76f,1.19f,2.52f),.77f,.82f,.55f);
                Box("EOArmoredFoot",model,new Vector3(0,1.8f,1.8f),new Vector3(.38f,.12f,.32f),Hull);
                Box("EOHousing",model,new Vector3(0,1.94f,1.8f),new Vector3(.34f,.22f,.28f),Deck);
                Box("EOLens",model,new Vector3(0,1.94f,1.945f),new Vector3(.21f,.12f,.025f),Glass);
                funnelY=1.68f;
            }
            else
            {
                var cabin=Facet("StealthCabin",model,new Vector3(0,1.16f,1.6f),new Vector3(1.78f,1.56f,2.26f),.76f,-.12f,Hull);
                FacetWindows(model,new Vector3(0,1.16f,1.6f),new Vector3(1.78f,1.56f,2.26f),.76f,-.12f,1.67f,1.68f);
                // Keep the entire sensor cluster aft of the cabin, without changing the 3x1 footprint.
                var sensors=Socket(model,"SensorAssembly",new Vector3(0,0,-.33f));
                var pedestal=Facet("SensorPedestal",sensors,new Vector3(0,1.15f,0),new Vector3(1.4f,.68f,1.45f),.96f,0,Hull);
                var tower=Facet("IntegratedSensorTower",sensors,new Vector3(0,2.13f,0),new Vector3(1.47f,1.29f,1.53f),.59f,0,Hull);
                for(int side=-1;side<=1;side+=2)
                {
                    SidePanel("AESA_Side",sensors,new Vector3(0,2.13f,0),new Vector3(1.47f,1.29f,1.53f),.59f,side,2.14f,0,.72f,.77f,Glass);
                    for(int n=0;n<3;n++) SidePanel("ArrayGrid",sensors,new Vector3(0,2.13f,0),new Vector3(1.47f,1.29f,1.53f),.59f,side,1.98f+n*.16f,0,.014f,.7f,Deck,.027f);
                }
                FrontPanel("AESA_Front",sensors,new Vector3(0,2.13f,0),new Vector3(1.47f,1.29f,1.53f),.59f,0,0,2.14f,.71f,.72f,Glass);
                radar=Socket(sensors,"RadarPivot",new Vector3(0,3.08f,0));
                Pipe("SensorSpine",sensors,new Vector3(0,2.93f,0),.06f,.51f,Dark);
                Box("NavigationArray",radar,Vector3.zero,new Vector3(.83f,.12f,.16f),White);
                var funnel=Facet("SuppressedFunnel",model,new Vector3(0,1.15f,-2.08f),new Vector3(1.06f,1.4f,1.1f),.82f,0,Hull);
                Box("FunnelCap",model,new Vector3(0,1.9f,-2.08f),new Vector3(.83f,.14f,.93f),Dark);
                Doors(model,new Vector3(0,1.16f,1.6f),new Vector3(1.78f,1.56f,2.26f),.76f,.85f,1.02f);
                for(int side=-1;side<=1;side+=2)
                    for(int n=0;n<4;n++) SidePanel("SensorCoolingLouvre",sensors,new Vector3(0,1.15f,0),new Vector3(1.4f,.68f,1.45f),.96f,side,.96f+n*.1f,.32f,.035f,.28f,Dark);
                Pipe("ESMFoot",model,new Vector3(0,1.96f,1.48f),.16f,.18f,Deck);
                Pipe("ESMReceiver",model,new Vector3(0,2.13f,1.48f),.13f,.2f,White);
                ValidateMissileSensorGeometry(cabin,sensors.gameObject,pedestal,tower,funnel);
                funnelY=1.95f;
            }
            // Small handrails, service fittings and engine access panels remain legible at refit zoom.
            for(int side=-1;side<=1;side+=2)
            {
                for(int n=0;n<4;n++) Box("ServiceRailPost",model,new Vector3(side*.8f,.58f,-2.65f+n*.55f),new Vector3(.024f,.48f,.024f),White);
                Box("ServiceRail",model,new Vector3(side*.8f,.82f,-1.82f),new Vector3(.022f,.022f,1.67f),White);
                if(kind==3)
                {
                    // The pedestal covers the old deck hatches; put its service access on the sides.
                    SidePanel("SensorServiceHatch",model,new Vector3(0,1.15f,-.33f),new Vector3(1.4f,.68f,1.45f),.96f,side,1.12f,-.5f,.28f,.38f,Deck);
                }
                else Box("DeckHatch",model,new Vector3(side*.41f,.836f,-.4f),new Vector3(.53f,.04f,.63f),Deck);
            }
            var launch=Socket(model,"DecoyLaunchPoint",new Vector3(kind==3?.77f:.63f,1f,-.76f));
            Socket(model,"FunnelTop",new Vector3(0,funnelY+.28f,-2.06f));
            Configure(runtime,so=>{Set(so,"radarAntenna",radar);Set(so,"launchPoint",launch);});
            Combine(model,radar);
            PersistMeshes(go,_meshPrefix);
            int tris=TriangleCount(go);
            if(tris>2400) throw new Exception($"{go.name}: excessive bridge triangles {tris}");
            Report.Add($"BRIDGE {Codes[kind]}: 3x1, +Z bow, runtime=BridgeModule, triangles={tris}, radar/decoy/funnel sockets present");
            return SavePrefab(go,Prefabs+"/Bridges");
        }

        // Mount fittings on the actual tapered wall, not on its full-width bounding box.
        // Thickness extends into the wall slightly so there is no daylight behind the fitting.
        private static void FacetWindows(Transform parent,Vector3 center,Vector3 size,float taper,float shift,float y,float sideZ)
        {
            for(int n=-1;n<=1;n++)
                FrontPanel("ForwardWindow",parent,center,size,taper,shift,n*.36f,y,.29f,.26f,Glass);
            for(int side=-1;side<=1;side+=2)
                for(int n=-1;n<=1;n++)
                    SidePanel("SideWindow",parent,center,size,taper,side,y,sideZ+n*.32f,.26f,.25f,Glass);
        }

        private static Transform SidePanel(string name,Transform parent,Vector3 center,Vector3 size,float taper,int side,float y,float z,float height,float width,Material material,float offset=.009f)
        {
            float fraction=(y-center.y+size.y*.5f)/size.y;
            float slope=size.x*.5f*(1-taper)/size.y;
            var rotation=Quaternion.Euler(0,0,side*Mathf.Atan(slope)*Mathf.Rad2Deg);
            var normal=rotation*new Vector3(side,0,0);
            var mount=Socket(parent,name+"Mount",new Vector3(center.x+side*size.x*.5f*Mathf.Lerp(1,taper,fraction),y,z)+normal*offset);
            mount.localRotation=rotation;
            Box(name,mount,Vector3.zero,new Vector3(.024f,height,width),material);
            return mount;
        }

        private static void FrontPanel(string name,Transform parent,Vector3 center,Vector3 size,float taper,float shift,float x,float y,float width,float height,Material material,float offset=.009f)
        {
            float fraction=(y-center.y+size.y*.5f)/size.y;
            float slope=(size.z*.5f*(taper-1)+shift)/size.y;
            var rotation=Quaternion.Euler(Mathf.Atan(slope)*Mathf.Rad2Deg,0,0);
            var mount=Socket(parent,name+"Mount",new Vector3(x,y,center.z+size.z*.5f*Mathf.Lerp(1,taper,fraction)+shift*fraction)+(rotation*Vector3.forward)*offset);
            mount.localRotation=rotation;
            Box(name,mount,Vector3.zero,new Vector3(width,height,.024f),material);
        }

        private static void Doors(Transform parent,Vector3 center,Vector3 size,float taper,float y,float z)
        {
            for(int side=-1;side<=1;side+=2)
            {
                var mount=SidePanel("AccessDoorFrame",parent,center,size,taper,side,y,z,.58f,.34f,Dark);
                Box("AccessDoor",mount,new Vector3(side*.014f,0,0),new Vector3(.025f,.52f,.28f),Deck);
                Box("DoorHandle",mount,new Vector3(side*.033f,0,.085f),new Vector3(.035f,.075f,.035f),White);
                Box("DoorSill",mount,new Vector3(side*.03f,-.29f,0),new Vector3(.09f,.04f,.38f),Hull);
            }
        }

        private static void ValidateMissileSensorGeometry(GameObject cabin,GameObject sensors,GameObject pedestal,GameObject tower,GameObject funnel)
        {
            // Conservative renderer AABBs prove separation even before meshes are merged.
            var sensorBounds=GeometryBounds(sensors);
            float cabinGap=GeometryBounds(cabin).min.z-sensorBounds.max.z;
            float funnelGap=sensorBounds.min.z-GeometryBounds(funnel).max.z;
            if(cabinGap<.02f || funnelGap<.1f)
                throw new Exception($"Missile sensor cluster clearance failed: cabin={cabinGap:F4}, funnel={funnelGap:F4}");
            var support=GeometryBounds(pedestal);
            const float engineRoofY=.82f;
            if(support.min.y>engineRoofY || support.max.y<GeometryBounds(tower).min.y)
                throw new Exception("Missile sensor pedestal does not connect the engine roof to the tower.");
            Report.Add($"GEOMETRY Missile: sensor cluster clear of cabin/funnel, conservative Z gaps={cabinGap:F4}/{funnelGap:F4}, pedestal connected, footprint unchanged");
        }

        private static Bounds GeometryBounds(GameObject go)
        {
            var renderers=go.GetComponentsInChildren<Renderer>();
            if(renderers.Length==0)throw new Exception($"{go.name}: no geometry to validate");
            var bounds=renderers[0].bounds;
            for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void WindowBand(Transform t,float y,float frontZ,float halfWidth,float sideZ,float rake,float sideRake)
        {
            for(int n=-1;n<=1;n++)
            {
                var pane=Box("ForwardWindow",t,new Vector3(n*.39f,y,frontZ),new Vector3(.32f,.29f,.026f),Glass);
                pane.transform.localRotation=Quaternion.Euler(rake,0,0);
            }
            for(int side=-1;side<=1;side+=2)
                for(int n=0;n<3;n++)
                {
                    var pane=Box("SideWindow",t,new Vector3(side*halfWidth,y,sideZ-.48f+n*.39f),new Vector3(.025f,.29f,.31f),Glass);
                    pane.transform.localRotation=Quaternion.Euler(0,0,side*sideRake);
                }
        }

        private static ModuleDefinition CloneBridgeDefinition(int kind,GameObject prefab)
        {
            string path=Data+"/Modules/mod_bridge_"+Codes[kind].ToLowerInvariant()+".asset";
            var def=CreateSO<ModuleDefinition>(path);
            EditorUtility.CopySerialized(LoadModule("bridge"),def);
            Configure(def,so=>{
                Set(so,"id","mod_bridge_"+Codes[kind].ToLowerInvariant()); Set(so,"displayName",Names[kind]+" 함교");
                Set(so,"description","3칸 함교 아트 시제품. 기존 함교 기능을 유지하며 함급별 성능 보너스는 StartingShipConcept 설계값으로만 저장됨.");
                Set(so,"prefab",prefab); Set(so,"weight",0f); Set(so,"maxCount",1);
            });
            return def;
        }

        private static ModuleDefinition[] BuildBlocks()
        {
            string[] codes={"FleetRelay","TurboIntake","FireControlArray","MissileLogistics"};
            string[] titles={"전투단 통신 중계", "강습 추진 흡기", "다중 표적 사격통제", "미사일 재장전 구획"};
            var result=new ModuleDefinition[4];
            for(int i=0;i<4;i++)
            {
                _meshPrefix="Block_"+codes[i];_meshIndex=0;
                var go=new GameObject("BLK_"+codes[i]);
                if(i==0)go.AddComponent<FleetRelayModule>();
                else if(i==1)go.AddComponent<TurboIntakeModule>();
                else if(i==2)go.AddComponent<FireControlArrayModule>();
                else go.AddComponent<MissileLogisticsModule>();
                var t=new GameObject("Model").transform;t.SetParent(go.transform,false);
                Facet("DeckBase",t,new Vector3(0,.12f,0),new Vector3(1.78f,.24f,1.78f),.97f,0,Deck);
                if(i==0)
                {
                    Facet("RadioCabinet",t,new Vector3(0,.52f,0),new Vector3(1.25f,.8f,1.3f),.88f,0,Hull);
                    Pipe("RelayMast",t,new Vector3(0,1.18f,0),.07f,.56f,Dark);
                    var dish=Pipe("CommsDish",t,new Vector3(0,1.58f,0),.43f,.12f,White);dish.transform.localRotation=Quaternion.Euler(55,0,0);
                    for(int s=-1;s<=1;s+=2)Pipe("Whip",t,new Vector3(s*.63f,.93f,0),.022f,1.3f,Dark);
                }
                else if(i==1)
                {
                    for(int s=-1;s<=1;s+=2)
                    {
                        Facet("IntakeHousing",t,new Vector3(s*.4f,.55f,0),new Vector3(.65f,.88f,1.57f),.88f,-.15f,Hull);
                        Box("Intake",t,new Vector3(s*.4f,.56f,.74f),new Vector3(.5f,.46f,.036f),Dark);
                        for(int n=0;n<4;n++)Box("Grille",t,new Vector3(s*.4f,.4f+n*.1f,.77f),new Vector3(.5f,.025f,.026f),Deck);
                        Box("Exhaust",t,new Vector3(s*.4f,.62f,-.8f),new Vector3(.48f,.37f,.03f),Dark);
                    }
                }
                else if(i==2)
                {
                    Facet("ElectronicsBase",t,new Vector3(0,.42f,0),new Vector3(1.36f,.6f,1.42f),.92f,0,Hull);
                    Pipe("ArraySupport",t,new Vector3(0,.91f,0),.14f,.39f,Hull);
                    Facet("TrackingArray",t,new Vector3(0,1.21f,0),new Vector3(1.29f,.44f,.74f),.96f,0,Hull);
                    var panel=Box("ArrayFace",t,new Vector3(0,1.25f,.4f),new Vector3(1.13f,.31f,.03f),Glass);panel.transform.localRotation=Quaternion.Euler(-12,0,0);
                    Socket(t,"FireControlOrigin",new Vector3(0,1.25f,.43f));
                }
                else
                {
                    Facet("ArmoredLocker",t,new Vector3(0,.51f,0),new Vector3(1.68f,.78f,1.7f),.94f,0,Hull);
                    for(int n=-1;n<=1;n++)
                    {
                        Box("CellLid",t,new Vector3(n*.47f,.922f,0),new Vector3(.38f,.045f,1.45f),Deck);
                        Box("Hinge",t,new Vector3(n*.47f,.96f,-.6f),new Vector3(.19f,.045f,.09f),Dark);
                    }
                    Box("Caution",t,new Vector3(0,.6f,.854f),new Vector3(1.23f,.09f,.019f),Warning);
                }
                Box("RoleMark",t,new Vector3(.89f,.22f,0),new Vector3(.019f,.07f,1.15f),Role[i]);
                Combine(t,null);PersistMeshes(go,_meshPrefix);
                var prefab=SavePrefab(go,Prefabs+"/Blocks");
                string liveId="mod_"+codes[i].ToLowerInvariant();
                string path=LiveModules+"/"+liveId+".asset";
                string oldPath=Data+"/Modules/concept_"+codes[i]+".asset";
                // Preserve existing references/GUIDs while moving the prototype definition into Resources.
                if(AssetDatabase.LoadAssetAtPath<ModuleDefinition>(path)==null && AssetDatabase.LoadAssetAtPath<ModuleDefinition>(oldPath)!=null)
                {
                    string error=AssetDatabase.MoveAsset(oldPath,path);
                    if(!string.IsNullOrEmpty(error))throw new Exception(error);
                }
                var def=CreateSO<ModuleDefinition>(path);
                var icon=NavalIconBaker.Bake(prefab,liveId);
                if(icon==null)throw new Exception("Missing live block icon: "+liveId);
                Configure(def,so=>{
                    Set(so,"id",liveId);Set(so,"displayName",titles[i]);
                    Set(so,"description",new[]{"호위함 자율 무장의 재사용 속도를 15% 높인다. 슬롯/CP를 추가하지 않는다.",
                        "최고속력 +10%, 가속 +15%. 기준 최고속력 50% 이상 항해 중 기관포·76mm 피해 +10%.",
                        "동시 추적 +2, VLS·유도로켓·SAM 사거리 +10%.",
                        "VLS·유도로켓 발사 간격과 셀 보급 시간 -15%. 기존 보급을 개선하며 추가 탄약을 공짜로 만들지 않는다."}[i]);
                    Set(so,"type",(int)new[]{ModuleType.FleetRelay,ModuleType.TurboIntake,ModuleType.FireControlArray,ModuleType.MissileLogistics}[i]);
                    Set(so,"prefab",prefab);Set(so,"icon",icon);Set(so,"weight",.65f);Set(so,"maxHp",40f);Set(so,"maxCount",1);Set(so,"heightClass",i==0?2:1);
                    Set(so,"stats.EscortFireRateBonus",i==0?.15f:0f);
                    Set(so,"stats.SpeedBonus",i==1?.1f:0f);Set(so,"stats.AccelerationBonus",i==1?.15f:0f);
                    Set(so,"stats.MovingGunDamageBonus",i==1?.1f:0f);
                    Set(so,"stats.ExtraTrackedTargets",i==2?2:0);Set(so,"stats.GuidedRangeBonus",i==2?.1f:0f);
                    Set(so,"stats.MissileReloadReduction",i==3?.15f:0f);
                });
                result[i]=def;
                Report.Add($"BLOCK {codes[i]}: 1x1, operational runtime, Resources draft pool, maxCount=1, icon present");
            }
            return result;
        }

        private static ModuleDefinition LoadModule(string id)
            => AssetDatabase.LoadAssetAtPath<ModuleDefinition>("Assets/_Game/Data/Modules/mod_"+id+".asset")
                ?? throw new Exception("Missing module: "+id);

        private static StartingLoadout MakeLoadout(int kind,bool expanded,ModuleDefinition bridge,ModuleDefinition[] blocks)
        {
            var entries=new List<StartingLoadout.Entry>();
            void Add(ModuleDefinition d,int x,int z,int rot=0)=>entries.Add(new StartingLoadout.Entry {Module=d,Origin=new GridCoord(x,z),RotationSteps=rot});
            void M(string id,int x,int z,int rot=0)=>Add(LoadModule(id),x,z,rot);
            Add(bridge,-1,0);
            if(!expanded)
            {
                if(kind==3){M("vls",2,0);M("autocannon",-2,0,2);}
                else {M("autocannon",2,0);M(kind==2?"autocannon":"ciws",-2,0,2);}
                if(kind==0)M("radar",0,1);
                else if(kind==1){Add(blocks[0],0,1);M("helideck",-3,0);}
                else if(kind==2)Add(blocks[1],0,1);
                else {Add(blocks[2],0,1);Add(blocks[3],1,1);}
            }
            else if(kind==0)
            {
                M("autocannon",2,0);M("gun76",3,0);M("ciws",-2,0);M("helideck",-3,0);
                M("radar",0,1);M("repairbay",-1,1);M("decoy",-1,-1);M("sonar",-3,1);M("asw",-3,-1);M("sam",1,-1);
            }
            else if(kind==1)
            {
                M("ciws",2,0);M("autocannon",3,0);M("repairbay",-2,0);M("helideck",-3,0);
                M("ew",0,-1);M("radar",0,1);M("sam",1,-1);M("sam",1,1);
                M("decoy",-1,-1);M("repairbay",-1,1);M("autocannon",2,-1);M("autocannon",2,1);
                Add(blocks[0],-2,-1);
            }
            else if(kind==2)
            {
                M("autocannon",2,0);M("gun76",3,0);M("autocannon",-2,0,2);
                M("magazine",2,1);M("gun76",3,1);M("autocannon",2,-1);M("magazine",1,-1);
                M("repairbay",0,-1);M("decoy",-1,-1);M("autocannon",-2,-1,2);
                Add(blocks[1],-3,0);
            }
            else
            {
                M("vls",2,0);M("vls",3,0);M("autocannon",-2,0,2);
                M("radar",0,1);M("sam",1,1);M("ciws",-1,1);M("vls",2,1);M("vls",3,1);
                M("ew",0,-1);M("decoy",-1,-1);M("repairbay",-2,-1);M("vls",2,-1);M("vls",3,-1);
                Add(blocks[2],1,-1);Add(blocks[3],-2,1);
            }
            var loadout=CreateSO<StartingLoadout>(Data+"/Loadout_"+Codes[kind]+(expanded?"_Expanded":"_Start")+".asset");
            var so=new SerializedObject(loadout);var p=so.FindProperty("entries");p.arraySize=entries.Count;
            for(int i=0;i<entries.Count;i++)
            {
                var e=p.GetArrayElementAtIndex(i);e.FindPropertyRelative("Module").objectReferenceValue=entries[i].Module;
                e.FindPropertyRelative("Origin").FindPropertyRelative("X").intValue=entries[i].Origin.X;
                e.FindPropertyRelative("Origin").FindPropertyRelative("Z").intValue=entries[i].Origin.Z;
                e.FindPropertyRelative("RotationSteps").intValue=entries[i].RotationSteps;
            }
            so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(loadout);return loadout;
        }

        private static GameObject Assemble(int kind,bool expanded,StartingLoadout loadout)
        {
            var go=new GameObject("SHIP_"+Codes[kind]+(expanded?"_Expanded":"_Start"));
            var grid=go.AddComponent<ShipGrid>();Configure(grid,so=>Set(so,"deckHeight",.4f));
            var moduleRoot=new GameObject("ModuleRoot").transform;moduleRoot.SetParent(go.transform,false);
            foreach(var e in loadout.Entries)
            {
                if(!grid.CanPlace(e.Module,e.Origin,e.RotationSteps,out string why))
                    throw new Exception($"{go.name}/{e.Module.Id}@{e.Origin}: {why}");
                if(!grid.Place(new ModuleInstance(e.Module),e.Origin,e.RotationSteps))throw new Exception("Place failed");
                var visual=UnityEngine.Object.Instantiate(e.Module.Prefab,moduleRoot);
                visual.name=e.Module.DisplayName+"_"+e.Origin;
                visual.transform.localPosition=grid.CoordToLocal(e.Origin)+ModuleFactory.GetFootprintOffset(e.Module,e.RotationSteps,grid.CellSize);
                visual.transform.localRotation=Quaternion.Euler(0,e.RotationSteps*90,0);
                StripBehavior(visual);
                if(e.Module.Type==ModuleType.HelicopterDeck)
                {
                    var heli=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Projectiles/HEL_Asw.prefab");
                    if(heli!=null){var parked=UnityEngine.Object.Instantiate(heli,visual.transform);parked.name="ParkedHelicopter_VisualOnly";parked.transform.localPosition=new Vector3(0,.32f,0);StripBehavior(parked);}
                }
            }
            var footprint=new List<GridCoord>();
            foreach(var e in loadout.Entries)
            {
                ShipGrid.GetFootprint(e.Module,e.Origin,e.RotationSteps,footprint);
                if(!PlacementRuleEvaluator.Evaluate(e.Module.Placement,grid,footprint,out string why))
                    throw new Exception($"{go.name}/{e.Module.Id}: final layout rule failed: {why}");
            }
            var hullRoot=new GameObject("HullRoot").transform;hullRoot.SetParent(go.transform,false);
            var hull=go.AddComponent<ShipHullBuilder>();Configure(hull,so=>{
                Set(so,"grid",grid);Set(so,"hullRoot",hullRoot);
                Set(so,"deckPlatePrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/VFX/HULL_DeckPlate.prefab"));
                Set(so,"sideSkirtPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/VFX/HULL_SideSkirt.prefab"));
                Set(so,"bowDeckMaterial",AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/MAT_hull_deck.mat"));
                Set(so,"bowSideMaterial",AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/MAT_hull_side.mat"));
                Set(so,"bowFittingsPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/VFX/HULL_BowFittings.prefab"));
                Set(so,"bowAnchorPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/VFX/HULL_Anchor.prefab"));
            });
            hull.Rebuild();PersistMeshes(go,go.name);
            Report.Add($"LOADOUT {go.name}: {loadout.Entries.Count} modules / {grid.OccupiedCells.Count} cells — native CanPlace+Place PASS");
            // The procedural bow is now a saved asset; don't let the runtime owner's OnDestroy destroy it.
            typeof(ShipHullBuilder).GetField("_bowMesh",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.SetValue(hull,null);
            UnityEngine.Object.DestroyImmediate(hull);UnityEngine.Object.DestroyImmediate(grid);
            if(kind==1) AddEscortVisuals(go.transform,expanded?6:2);
            StripBehavior(go);
            return SavePrefab(go,Prefabs+"/Assemblies");
        }

        private static void AddEscortVisuals(Transform parent,int count)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Resources/TaskForce/Escorts/ESC_PB_T0.prefab");
            if(asset==null) throw new Exception("Missing basic escort prefab");
            var root=new GameObject("EscortFormation_VisualOnly").transform;root.SetParent(parent,false);
            for(int i=0;i<count;i++)
            {
                var escort=UnityEngine.Object.Instantiate(asset,root);escort.name="BasicEscort_"+(i+1);
                int rank=i/2;int side=(i%2==0?-1:1);
                escort.transform.localPosition=new Vector3(side*(count==2?5f:7f+rank*1.4f),-.35f,3f-rank*6f);
                escort.transform.localScale=Vector3.one*(count==2?.6f:.76f);StripBehavior(escort);
            }
        }

        private static void MakeConcept(int i,ModuleDefinition bridge,StartingLoadout start,StartingLoadout grown,GameObject first,GameObject final)
        {
            var concept=CreateSO<StartingShipConcept>(Data+"/ShipConcept_"+Codes[i]+".asset");
            Configure(concept,so=>{
                Set(so,"id","ship_"+Codes[i].ToLowerInvariant());Set(so,"title",Names[i]);Set(so,"kind",i);Set(so,"color",Colors[i]);
                Set(so,"description",new[]{"균형 잡힌 센서·함포·대잠 확장.","CIC·통신 중계·헬기데크. 기본 고속정 2척으로 출항(현행 편대 최대 4척).","낮은 장갑 함교·쌍흡기. 실탄과 속도로 근접 전선을 돌파.","위상배열 센서·VLS. 사격통제와 보급으로 장거리 타격."}[i]);
                Set(so,"bridgeDefinition",bridge);Set(so,"startLoadout",start);Set(so,"expandedLoadout",grown);Set(so,"startPreviewPrefab",first);Set(so,"expandedPreviewPrefab",final);
                Set(so,"initialEscortCount",i==1?2:0);
                Set(so,"startSummary",new[]{"기관포 · CIWS · 레이더\n작은 범용 기본형. 탐지와 근거리 방어를 갖추고 카드로 확장.",
                    "기관포 · CIWS · 헬기데크 · 통신 중계\n기본 고속정 2척. 호위함 능력 재사용 속도 +15%.",
                    "전방/후방 기관포 · 강습 추진 흡기\n속력 +10% · 가속 +15% · 항해 중 실탄 피해 +10%.",
                    "VLS · 기관포 · 사격통제 · 미사일 보급\n추적 +2 · 유도 사거리 +10% · 미사일 발사/보급 시간 -15%."}[i]);
                Set(so,"mechanicsImplemented",false);
                Set(so,"hpMultiplier",new[]{1f,1.5f,.8f,1f}[i]);Set(so,"armorMultiplier",new[]{1f,1.3f,.75f,1f}[i]);
                Set(so,"speedMultiplier",new[]{1f,.75f,1.3f,.9f}[i]);Set(so,"turnMultiplier",new[]{1f,.8f,1.25f,.9f}[i]);
                Set(so,"projectileDamageMultiplier",i==2?1.2f:1f);Set(so,"projectileRangeMultiplier",i==2?1.15f:1f);Set(so,"projectileFireRateMultiplier",i==2?1.2f:1f);
                Set(so,"missileDamageMultiplier",i==3?1.25f:1f);Set(so,"missileRangeMultiplier",i==3?1.2f:1f);Set(so,"missileReloadMultiplier",i==3?1.1f:1f);
                Set(so,"helicopterDamageMultiplier",i==1?1.25f:1f);Set(so,"helicopterCooldownMultiplier",i==1?.8f:1f);
                Set(so,"escortStart",i==1?2:0);Set(so,"escortMax",i==1?6:4);
                ModuleType[] allowed=i==1?new[]{ModuleType.Autocannon,ModuleType.Ciws,ModuleType.SamLauncher}:
                    i==2?new[]{ModuleType.Autocannon,ModuleType.Ciws,ModuleType.NavalGun,ModuleType.AswLauncher}:
                    new[]{ModuleType.Autocannon,ModuleType.Ciws,ModuleType.NavalGun,ModuleType.Vls,ModuleType.GuidedRocket,ModuleType.SamLauncher,ModuleType.AswLauncher};
                var p=so.FindProperty("allowedWeapons");p.arraySize=allowed.Length;
                for(int n=0;n<allowed.Length;n++)p.GetArrayElementAtIndex(n).enumValueIndex=(int)allowed[n];
                string[] future=i==0?Array.Empty<string>():i==1?new[]{"BLK_FleetRelay — 편대 통제/지원 재사용 대기시간"}:i==2?new[]{"BLK_TurboIntake — 최고속력/가속/실탄 기동 보너스"}:new[]{"BLK_FireControlArray — 다중 사격통제","BLK_MissileLogistics — VLS 셀 보급"};
                var f=so.FindProperty("futureBlocks");f.arraySize=future.Length;
                for(int n=0;n<future.Length;n++)f.GetArrayElementAtIndex(n).stringValue=future[n];
            });
        }

        private static void BuildPreviewScene(List<GameObject> starts,List<GameObject> expanded,List<GameObject> bridges)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            for(int i=0;i<4;i++)
            {
                var root=new GameObject(Codes[i]+"_Display").transform;root.position=new Vector3((i-1.5f)*31f,0,0);
                var grown=(GameObject)PrefabUtility.InstantiatePrefab(expanded[i],scene);grown.transform.SetParent(root,false);
                var first=(GameObject)PrefabUtility.InstantiatePrefab(starts[i],scene);first.name="StartingConfiguration";first.transform.SetParent(root,false);first.transform.localPosition=new Vector3(0,0,-25);
                var bridge=(GameObject)PrefabUtility.InstantiatePrefab(bridges[i],scene);bridge.name="BridgeCloseup";bridge.transform.SetParent(root,false);bridge.transform.localPosition=new Vector3(0,0,25);StripBehavior(bridge);
            }
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.57f,.65f,.74f);
            var sea=GameObject.CreatePrimitive(PrimitiveType.Plane);sea.name="Ocean";sea.transform.position=new Vector3(0,-.9f,0);sea.transform.localScale=new Vector3(24,1,18);
            sea.GetComponent<Renderer>().sharedMaterial=OwnMaterial("ShowcaseWater",new Color(.035f,.14f,.19f));UnityEngine.Object.DestroyImmediate(sea.GetComponent<Collider>());
            var sun=new GameObject("Sun",typeof(Light)).GetComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.8f;sun.transform.rotation=Quaternion.Euler(48,-35,0);sun.shadows=LightShadows.Soft;
            var cam=new GameObject("Main Camera",typeof(Camera)).GetComponent<Camera>();cam.tag="MainCamera";cam.transform.position=new Vector3(-57,79,83);cam.transform.LookAt(Vector3.zero);cam.orthographic=true;cam.orthographicSize=46;cam.backgroundColor=new Color(.025f,.055f,.08f);cam.clearFlags=CameraClearFlags.SolidColor;
            EditorSceneManager.SaveScene(scene,"Assets/_Game/Scenes/StartingShipConcepts.unity");
        }

        private static void StripBehavior(GameObject go)
        {
            foreach(var c in go.GetComponentsInChildren<MonoBehaviour>(true))UnityEngine.Object.DestroyImmediate(c);
            foreach(var c in go.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
            foreach(var c in go.GetComponentsInChildren<Rigidbody>(true))UnityEngine.Object.DestroyImmediate(c);
            foreach(var c in go.GetComponentsInChildren<ParticleSystem>(true))if(c!=null)UnityEngine.Object.DestroyImmediate(c.gameObject);
        }

        private static Transform Socket(Transform parent,string name,Vector3 p)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=p;return t;}
        private static GameObject Box(string name,Transform t,Vector3 p,Vector3 s,Material m)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(t,false);go.transform.localPosition=p;go.transform.localScale=s;go.GetComponent<Renderer>().sharedMaterial=m;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;}

        private static GameObject Pipe(string name,Transform t,Vector3 p,float radius,float height,Material mat)
        {
            const int n=8;var v=new List<Vector3>();var ix=new List<int>();
            for(int i=0;i<n;i++)
            {
                float a=i*Mathf.PI*2/n,b=(i+1)*Mathf.PI*2/n;
                Vector3 a0=new Vector3(Mathf.Cos(a)*radius,-height*.5f,Mathf.Sin(a)*radius),b0=new Vector3(Mathf.Cos(b)*radius,-height*.5f,Mathf.Sin(b)*radius);
                Vector3 a1=a0+Vector3.up*height,b1=b0+Vector3.up*height;
                Quad(v,ix,b0,a0,a1,b1);Tri(v,ix,Vector3.up*height*.5f,b1,a1);Tri(v,ix,Vector3.down*height*.5f,a0,b0);
            }
            return MeshObject(name,t,p,v,ix,mat);
        }

        private static GameObject Facet(string name,Transform t,Vector3 p,Vector3 s,float taper,float shiftZ,Material mat)
        {
            float w=s.x/2,d=s.z/2,c=Mathf.Min(w,d)*.21f;
            Vector2[] ring={new Vector2(-w+c,-d),new Vector2(w-c,-d),new Vector2(w,-d+c),new Vector2(w,d-c),new Vector2(w-c,d),new Vector2(-w+c,d),new Vector2(-w,d-c),new Vector2(-w,-d+c)};
            var v=new List<Vector3>();var ix=new List<int>();
            for(int i=0;i<8;i++)
            {
                Vector2 a=ring[i],b=ring[(i+1)%8];
                var a0=new Vector3(a.x,-s.y*.5f,a.y);var b0=new Vector3(b.x,-s.y*.5f,b.y);
                var a1=new Vector3(a.x*taper,s.y*.5f,a.y*taper+shiftZ);var b1=new Vector3(b.x*taper,s.y*.5f,b.y*taper+shiftZ);
                Quad(v,ix,b0,a0,a1,b1);Tri(v,ix,new Vector3(0,s.y*.5f,shiftZ),b1,a1);Tri(v,ix,new Vector3(0,-s.y*.5f,0),a0,b0);
            }
            return MeshObject(name,t,p,v,ix,mat);
        }
        private static void Tri(List<Vector3> v,List<int> ix,Vector3 a,Vector3 b,Vector3 c){int i=v.Count;v.Add(a);v.Add(b);v.Add(c);ix.Add(i);ix.Add(i+1);ix.Add(i+2);}
        private static void Quad(List<Vector3> v,List<int> ix,Vector3 a,Vector3 b,Vector3 c,Vector3 d){Tri(v,ix,a,b,c);Tri(v,ix,a,c,d);}
        private static GameObject MeshObject(string name,Transform t,Vector3 p,List<Vector3> v,List<int> ix,Material mat)
        {
            var mesh=new Mesh{name=_meshPrefix+"_"+name+"_"+(_meshIndex++)};mesh.SetVertices(v);mesh.SetTriangles(ix,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(t,false);go.transform.localPosition=p;go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;return go;
        }

        private static void Combine(Transform root,Transform moving)
        {
            foreach(bool movable in moving==null?new[]{false}:new[]{false,true})
            {
                var target=movable?moving:root;
                var filters=root.GetComponentsInChildren<MeshFilter>().Where(f=>movable?f.transform.IsChildOf(moving):moving==null||!f.transform.IsChildOf(moving)).ToArray();
                foreach(var group in filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial))
                {
                    var mesh=new Mesh{name=_meshPrefix+"_"+group.Key.name+(movable?"_Radar":"_Fixed")};
                    mesh.CombineMeshes(group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=target.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);
                    var go=new GameObject("Merged_"+group.Key.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(target,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=group.Key;
                }
                foreach(var f in filters){UnityEngine.Object.DestroyImmediate(f.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(f);}
            }
        }

        private static void PersistMeshes(GameObject go,string prefix)
        {
            int n=0;
            foreach(var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=mf.sharedMesh;if(mesh==null||!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mesh)))continue;
                string path=Art+"/Meshes/"+prefix+"_"+(n++)+".asset";
                var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing==null){AssetDatabase.CreateAsset(mesh,path);mf.sharedMesh=mesh;}
                else
                {
                    // CopySerialized alone can leave the old GPU vertex buffer alive in this editor session.
                    // Rewrite through Mesh setters so a rebuild is reflected in the captures immediately.
                    existing.Clear();existing.vertices=mesh.vertices;existing.normals=mesh.normals;existing.uv=mesh.uv;
                    existing.subMeshCount=mesh.subMeshCount;
                    for(int s=0;s<mesh.subMeshCount;s++)existing.SetTriangles(mesh.GetTriangles(s),s);
                    existing.bounds=mesh.bounds;existing.UploadMeshData(false);
                    EditorUtility.SetDirty(existing);mf.sharedMesh=existing;
                }
            }
        }
        private static int TriangleCount(GameObject go)=>go.GetComponentsInChildren<MeshFilter>(true).Sum(f=>f.sharedMesh!=null?f.sharedMesh.triangles.Length/3:0);
    }
}

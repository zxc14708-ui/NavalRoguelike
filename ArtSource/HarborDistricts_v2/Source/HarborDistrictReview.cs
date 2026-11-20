using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    public static class HarborDistrictReview
    {
        private static string _output;
        private static Transform _island;
        private static NavalBaseMenu _menu;
        private static HarborOperations _ops;

        public static void Run()
        {
            string[] args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,"-districtOut");
            if (index<0 || index+1>=args.Length) throw new ArgumentException("Missing -districtOut.");
            _output=Path.GetFullPath(args[index+1]);
            Directory.CreateDirectory(Path.Combine(_output,"Screenshots"));
            var berthPoses=ReadBerths();
            NavalBaseBuilder.BuildMenuBackdrop();
            HarborOperationsBuilder.Build();
            var rebuilt=ReadBerths();
            Check(berthPoses.Count>0 && berthPoses.Count==rebuilt.Count &&
                berthPoses.All(pair=>rebuilt.TryGetValue(pair.Key,out var pose) &&
                    Vector3.Distance(pose.position,pair.Value.position)<.02f &&
                    Quaternion.Angle(pose.rotation,pair.Value.rotation)<.06f),"A stored selection berth changed.");

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var canvas = new GameObject("Harbor review canvas",typeof(RectTransform),typeof(Canvas));
            _menu=NavalBaseMenu.Create(canvas.transform);
            Check(_menu != null && _menu.ShipCount == 4,"Starting menu did not create all four selectable ships.");
            _menu.Paused=true;
            _ops=_menu.Operations;
            Check(_ops != null && _ops.Ready && _ops.SternTug != null,"The real menu did not bind the harbor overlay.");
            _island=_ops.transform.parent;
            var report=new List<string> { "Unity " + Application.unityVersion + " | copied starting menu, current resource prefabs" };
            report.Add($"PASS {berthPoses.Count} stored berth transforms and all four starting ships.");
            CheckTerrain(report);
            CheckShips(report);
            Screenshots();
            Export();
            File.WriteAllLines(Path.Combine(_output,"UnityValidation.txt"),report);
            Debug.Log("[Harbor Districts] PASS\n"+string.Join("\n",report));
        }

        private static Dictionary<string,Pose> ReadBerths()
        {
            var source=Resources.Load<GameObject>("Environment/NavalBaseMenu");
            var berths=source.transform.Find("Harbor/Berths");
            return berths.Cast<Transform>().Where(t=>t.name.StartsWith("Berth_",StringComparison.Ordinal))
                .ToDictionary(t=>t.name,t=>new Pose(t.localPosition,t.localRotation));
        }

        private static void CheckTerrain(List<string> report)
        {
            var patches=new Dictionary<string,List<MeshCollider>>();
            foreach (string key in new[]{"road","runway","grass_dark"}) patches.Add(key,new());
            foreach (var mesh in _island.GetComponentsInChildren<MeshFilter>(true))
            {
                var material=mesh.GetComponent<MeshRenderer>()?.sharedMaterial;
                if (material == null || !material.name.StartsWith("MAT_base_",StringComparison.Ordinal) ||
                    !patches.TryGetValue(material.name.Substring(9),out var list)) continue;
                var collider=mesh.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh=mesh.sharedMesh;
                list.Add(collider);
            }
            Physics.SyncTransforms();

            bool Surface(string material,Vector2 point,out float height)
            {
                bool hit=false;
                height=float.NegativeInfinity;
                var ray=new Ray(_island.TransformPoint(new Vector3(point.x,90f,point.y)),Vector3.down);
                foreach (var mesh in patches[material])
                {
                    if (!mesh.Raycast(ray,out var contact,12f)) continue;
                    hit=true;
                    height=Mathf.Max(height,_island.InverseTransformPoint(contact.point).y);
                }
                return hit;
            }

            void VerifyStrip(string material,params Vector2[] vertices)
            {
                for (int j=1;j<vertices.Length;j++)
                {
                    Vector2 a=vertices[j-1], b=vertices[j];
                    int count=Mathf.CeilToInt(Vector2.Distance(a,b));
                    for (int i=0;i<=count;i++)
                    {
                        var p=Vector2.Lerp(a,b,(float)i/count);
                        Check(Surface(material,p,out var y) && y>=4.19f,material+" paving gap at "+p);
                    }
                }
            }

            VerifyStrip("road",new(110,119),new(110,166));
            VerifyStrip("road",new(270,446),new(270,320),new(213,320));
            VerifyStrip("road",new(-10,446),new(-10,409));
            VerifyStrip("road",new(368,119),new(368,142));
            VerifyStrip("road",new(-850,119),new(-850,146));
            VerifyStrip("road",new(-380,-57),new(-380,-89));
            VerifyStrip("road",new(-400,284),new(-354,284));
            VerifyStrip("road",new(110,447),new(110,392));
            foreach (float x in new[]{-760f,-380f,0f,330f,660f})
                VerifyStrip("runway",new(x,-330),new(x,-262));
            for (int z=-40;z<=80;z+=3)
            for (int x=754;x<=766;x+=3)
            {
                var p=new Vector2(x,z);
                Check(Surface("road",p,out var road), "Missing roadway beside radar.");
                Check(!Surface("grass_dark",p,out var turf) || turf<road-.05f,"Radar embankment covers the road at "+p);
            }
            foreach (var mesh in patches.Values.SelectMany(p=>p)) Object.DestroyImmediate(mesh);
            report.Add("PASS Asphalt traced from main roads into all six lots, freight yard and command access.");
            report.Add("PASS All five taxi connections reach the apron/runway. Radar slope clears the east road.");
        }

        private static void CheckShips(List<string> report)
        {
            var ship=_ops.CargoShip;
            var bow=_ops.Tug;
            var stern=_ops.SternTug;
            Vector3 shipHome=ship.localPosition,bowHome=bow.localPosition,sternHome=stern.localPosition;
            Vector2 shipSize=new(14.8f,74f), tugSize=new(4.1f,10.7f);
            var pier=new Vector2(0,90);
            var servicePier=new Vector2(-60.1f,64.5f);
            var slots=new[]{272f,492f,712f,932f}.Select(x=>new Vector2(x,82)).ToArray();
            var route=NavalBaseMenu.PatrolRoute.Select(p=>p+new Vector2(850,-550)).ToArray();
            var patrol=new List<Vector2>();
            for (int i=1;i<route.Length;i++)
            {
                int segments=Mathf.CeilToInt(Vector2.Distance(route[i-1],route[i])/2);
                for (int j=0;j<=segments;j++)
                    patrol.Add(Vector2.Lerp(route[i-1],route[i],j/(float)segments));
            }
            float patrolGap=float.PositiveInfinity;
            for (int i=0;i<=3400;i++)
            {
                float t=i*.05f;
                _ops.SetTime(t);
                foreach (var boat in new[]{ship,bow,stern})
                {
                    var size=boat==ship?shipSize:tugSize;
                    Vector2 at=Flat(boat.localPosition);
                    float yaw=boat.localEulerAngles.y;
                    Check(!Overlap(at,yaw,size,pier,0,new(14,90)),boat.name+" overlaps cargo pier at "+t);
                    Check(!Overlap(at,yaw,size,servicePier,0,new(6.5f,76f)),boat.name+" overlaps tug pier at "+t);
                    Check(slots.All(slot=>!Overlap(at,yaw,size,slot,0,new(32,63))),boat.name+" enters selection berths at "+t);
                    Axes(yaw,out var right,out var forward);
                    foreach (var point in patrol)
                    {
                        Vector2 rel=point-at;
                        float dx=Mathf.Max(0f,Mathf.Abs(Vector2.Dot(rel,right))-size.x);
                        float dz=Mathf.Max(0f,Mathf.Abs(Vector2.Dot(rel,forward))-size.y);
                        patrolGap=Mathf.Min(patrolGap,dx*dx+dz*dz);
                    }
                }
                ValidatePair(bow,tugSize,stern,tugSize,t);
                ValidatePair(ship,shipSize,bow,tugSize,t);
                ValidatePair(ship,shipSize,stern,tugSize,t);
                if (_ops.ActiveTowlines==2)
                    Check(!CablesCross(_ops.transform.Find("Tow cable"),_ops.transform.Find("Stern tow cable")),"Tow cables cross at "+t);
            }
            Check(Mathf.Sqrt(patrolGap)>37f,"Boat sweep approaches the patrol corridor.");
            foreach (var sample in new[]{ (35f,2),(54f,1),(60f,0),(91f,0),(122f,2),(169.5f,0) })
            {
                _ops.SetTime(sample.Item1);
                Check(_ops.ActiveTowlines==sample.Item2,"Unexpected towline count at "+sample.Item1);
            }
            foreach (float t in new[]{91f,169.5f})
            {
                _ops.SetTime(t);
                Check(Vector3.Distance(bow.localPosition,bowHome)<.025f &&
                    Vector3.Distance(stern.localPosition,sternHome)<.025f,"Both tugs failed to return to their own berths at "+t);
            }
            Check(Vector3.Distance(ship.localPosition,shipHome)<.025f,"Cargo loop does not re-berth.");
            _ops.SetTime(40);
            var paused=ship.localPosition;
            _ops.Tick(0f);
            Check(ship.localPosition==paused && _ops.ActiveTowlines==2,"Paused animation moved.");
            report.Add("PASS 3,401 time samples: no hull/pier/selection-berth overlap, no crossing tow cables.");
            report.Add($"PASS Minimum patrol-center to swept hull distance {Mathf.Sqrt(patrolGap):F1} m in harbor-model units.");
            report.Add("PASS Two lines depart, stern releases first, both tugs return to their berths, two lines assist docking; pause works.");
        }

        private static void ValidatePair(Transform a,Vector2 aSize,Transform b,Vector2 bSize,float t)
        {
            Check(!Overlap(Flat(a.localPosition),a.localEulerAngles.y,aSize,Flat(b.localPosition),b.localEulerAngles.y,bSize),
                a.name+" overlaps "+b.name+" at "+t);
        }

        private static bool CablesCross(Transform first,Transform second)
        {
            for (int i=0;i<2;i++)
            for (int j=0;j<2;j++)
            {
                Transform a=first.GetChild(i),b=second.GetChild(j);
                Vector2 u=Flat(_island.InverseTransformPoint(a.TransformPoint(Vector3.down*.5f)));
                Vector2 v=Flat(_island.InverseTransformPoint(a.TransformPoint(Vector3.up*.5f)));
                Vector2 p=Flat(_island.InverseTransformPoint(b.TransformPoint(Vector3.down*.5f)));
                Vector2 q=Flat(_island.InverseTransformPoint(b.TransformPoint(Vector3.up*.5f)));
                if (Side(u,v,p)*Side(u,v,q)<0f && Side(p,q,u)*Side(p,q,v)<0f) return true;
            }
            return false;
        }

        private static float Side(Vector2 a,Vector2 b,Vector2 p)
            => (b.x-a.x)*(p.y-a.y)-(b.y-a.y)*(p.x-a.x);
        private static Vector2 Flat(Vector3 p) => new(p.x,p.z);
        private static void Axes(float yaw,out Vector2 right,out Vector2 forward)
        {
            float s=Mathf.Sin(yaw*Mathf.Deg2Rad),c=Mathf.Cos(yaw*Mathf.Deg2Rad);
            right=new(c,-s);
            forward=new(s,c);
        }
        private static bool Overlap(Vector2 a,float yawA,Vector2 sizeA,Vector2 b,float yawB,Vector2 sizeB)
        {
            Axes(yawA,out var ar,out var af);
            Axes(yawB,out var br,out var bf);
            Vector2 d=b-a;
            bool Separated(Vector2 axis) => Mathf.Abs(Vector2.Dot(d,axis)) >=
                sizeA.x*Mathf.Abs(Vector2.Dot(ar,axis))+sizeA.y*Mathf.Abs(Vector2.Dot(af,axis))+
                sizeB.x*Mathf.Abs(Vector2.Dot(br,axis))+sizeB.y*Mathf.Abs(Vector2.Dot(bf,axis));
            return !Separated(ar)&&!Separated(af)&&!Separated(br)&&!Separated(bf);
        }

        private static void Screenshots()
        {
            Shot("01_base_overview",32,new(0,3,75),new(210,1360,-1050),775);
            Shot("02_living_district",92,new(340,4,118),new(180,328,-270),167);
            Shot("03_fleet_command",30,new(55,3,436),new(216,354,260),250);
            Shot("04_radar_road",62,new(802,5,29),new(-126,223,222),111);
            Shot("05_airfield_west",6,new(-722,4,-294),new(163,261,-168),120);
            Shot("06_airfield_east",20,new(625,3,-297),new(-185,232,-196),126);
            Shot("07_freight_yard",62,new(-380,4,286),new(-144,196,156),93);
            Shot("08_ship_repair",40,new(617,3,549),new(177,268,220),189);
            ShipShot("09_twin_tug_departure",37,new(245,205,164),122);
            Shot("10_tug_service_berths",91,new(-904,3,632),new(-196,212,150),111);
            ShipShot("11_twin_tug_inbound",126,new(203,218,164),119);
            Shot("12_airfield_parking",42,new(-380,3,-70),new(-103,170,-128),73);
        }

        private static void ShipShot(string name,float time,Vector3 eye,float size)
        {
            _ops.SetTime(time);
            Vector3 focus=_island.InverseTransformPoint(_ops.CargoShip.position);
            Shot(name,time,focus+new Vector3(5f,5f,4f),eye,size);
        }

        private static void Shot(string name,float time,Vector3 at,Vector3 eye,float size)
        {
            _ops.SetTime(time);
            var camera=new GameObject("District review camera",typeof(Camera)).GetComponent<Camera>();
            camera.CopyFrom(_menu.ViewCamera);
            camera.enabled=false;
            camera.orthographic=true;
            camera.orthographicSize=size*_island.lossyScale.x;
            camera.transform.position=_island.TransformPoint(at+eye);
            camera.transform.LookAt(_island.TransformPoint(at));
            camera.nearClipPlane=.1f;
            camera.farClipPlane=1200f;
            camera.aspect=16f/9f;
            const int width=1600,height=900;
            var target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);
            var tex=new Texture2D(width,height,TextureFormat.RGB24,false);
            var prior=RenderTexture.active;
            try
            {
                camera.targetTexture=target;
                camera.Render();
                RenderTexture.active=target;
                tex.ReadPixels(new Rect(0,0,width,height),0,0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(_output,"Screenshots",name+".png"),tex.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=null;
                RenderTexture.active=prior;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(camera.gameObject);
            }
        }

        private static void Export()
        {
            const string root="Assets/_Game/";
            string[] files=
            {
                "Art/NavalBase/MenuLayout","Art/NavalBase/HarborOperations",
                "Resources/Environment/NavalBaseMenu.prefab","Resources/Environment/HarborOperations.prefab",
                "Editor/NavalBaseBuilder.cs","Editor/NavalBaseBuilder.Districts.cs","Editor/NavalBaseBuilder.Layout.cs",
                "Editor/NavalBaseBuilder.Kit.cs","Editor/NavalBaseMenuBuilder.cs",
                "Editor/HarborOperationsBuilder.cs","Editor/HarborOperationsBuilder.Layout.cs",
                "Scripts/UI/NavalBaseMenu.cs","Scripts/UI/HarborOperations.cs","Scripts/UI/HarborOperations.Motion.cs",
                "Scripts/UI/HarborOperations.Tugs.cs",
            };
            AssetDatabase.ExportPackage(files.Select(x=>root+x).ToArray(),
                Path.Combine(_output,"HarborDistricts_v2.unitypackage"),ExportPackageOptions.Recurse);
        }

        private static void Check(bool condition,string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

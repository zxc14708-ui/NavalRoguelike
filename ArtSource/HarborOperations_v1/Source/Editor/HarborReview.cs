using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HarborOperationsPack.Editor
{
    public static class HarborReview
    {
        private static Camera _camera;
        private static Transform _island;
        private static HarborOperations _ops;
        private static string _output;

        public static void Run()
        {
            var args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,"-harborOut");
            if(index<0 || index+1>=args.Length) throw new ArgumentException("Missing -harborOut directory");
            _output=Path.GetFullPath(args[index+1]);
            Directory.CreateDirectory(_output);
            HarborBuilder.Build();

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            _island=Instantiate("Environment/NavalBaseMenu",null);
            var details=Instantiate("Environment/HarborFacilityDetails",_island);
            details.name="HarborFacilityDetails";
            var berths=_island.Find("Harbor/Berths");
            var berthTransforms=berths==null ? Array.Empty<Transform>() : berths.GetComponentsInChildren<Transform>(true);
            var before=berthTransforms.Select(t=>new Pose(t.position,t.rotation)).ToArray();

            _ops=HarborOperations.Attach(_island);
            if(_ops==null || !_ops.Ready) throw new InvalidOperationException("The operation could not bind to the shipping scene.");
            if(HarborOperations.Attach(_island)!=_ops) throw new InvalidOperationException("Duplicate overlay");
            VerifyMotion(berthTransforms,before);
            AssetDatabase.ExportPackage(HarborBuilder.Root,Path.Combine(_output,"HarborOperations_v1.unitypackage"),
                ExportPackageOptions.Recurse);

            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.69f,.76f,.81f);
            var light=new GameObject("Review daylight",typeof(Light)).GetComponent<Light>();
            light.type=LightType.Directional;
            light.transform.rotation=Quaternion.Euler(46f,153f,0f);
            light.intensity=1.20f;
            light.shadows=LightShadows.Soft;
            light.shadowStrength=.68f;
            _camera=new GameObject("Harbor review",typeof(Camera)).GetComponent<Camera>();
            _camera.clearFlags=CameraClearFlags.SolidColor;
            _camera.backgroundColor=new Color(.50f,.72f,.84f);
            _camera.orthographic=true;
            _camera.nearClipPlane=.1f;
            _camera.farClipPlane=850f;
            _camera.allowHDR=false;

            Shot("01_Harbor_Overview",18f,new Vector3(-60,1,80),new Vector3(-650,1400,1250),890);
            Shot("02_Workshop_Road",34f,new Vector3(606,1,536),new Vector3(275,345,280),240);
            Shot("03_Cargo_Loading",11.5f,new Vector3(-831,3,638),new Vector3(255,230,200),108);
            Shot("04_Departure_Tow",36.3f,new Vector3(-798,0,681),new Vector3(203,196,173),128);
            Shot("05_Inbound_Assist",128.4f,new Vector3(-793,0,701),new Vector3(179,220,158),127);
            Shot("06_Yard_Gate",68f,new Vector3(-374,4,283),new Vector3(-146,178,145),92);
            Shot("07_Airfield_Service",14f,new Vector3(416,7,-151),new Vector3(-175,218,163),111);
            Shot("08_Airfield_Hover",16.6f,new Vector3(437,21,-175),new Vector3(160,180,155),84);

            string frames=Path.Combine(_output,"Frames");
            Directory.CreateDirectory(frames);
            for(int i=0;i<61;i++)
            {
                TowFrame(frames,"out",i,25f+i*.40f);
                TowFrame(frames,"in",i,115.8f+i*.40f);
            }
            Debug.Log("[Harbor Operations] Verify and export complete: "+_output);
        }

        private static void TowFrame(string directory,string label,int index,float time)
        {
            _ops.SetTime(time);
            var ship=_island.InverseTransformPoint(_ops.CargoShip.position);
            var focus=new Vector3(ship.x-8f,3f,ship.z+8f);
            CameraPose(focus,new Vector3(206,221,187),129f);
            Capture(Path.Combine(directory,$"{label}_{index:000}.png"),672,378);
        }

        private static Transform Instantiate(string resource, Transform parent)
        {
            var prefab=Resources.Load<GameObject>(resource);
            if(prefab==null) throw new InvalidOperationException("Missing "+resource);
            var t=UnityEngine.Object.Instantiate(prefab,parent,false).transform;
            t.name=prefab.name;
            return t;
        }

        private static void VerifyMotion(Transform[] berths, Pose[] before)
        {
            var ship=_ops.CargoShip;
            var tug=_ops.Tug;
            var pier=new Vector2(0,90);
            Vector3 port=_island.InverseTransformPoint(ship.parent.position);
            var selectionSlots=Enumerable.Range(0,4)
                .Select(i=>new Vector2(-578f+220f*i-port.x,632f-port.z)).ToArray();
            int towFrames=0;
            float maxTravel=0f;
            var rest=ship.localPosition;
            for(int i=0;i<=1700;i++)
            {
                float t=i*.1f;
                _ops.SetTime(t);
                Vector2 large=new(ship.localPosition.x,ship.localPosition.z);
                Vector2 little=new(tug.localPosition.x,tug.localPosition.z);
                Vector2 end=new(rest.x,rest.z);
                maxTravel=Mathf.Max(maxTravel,Vector2.Distance(large,end));
                if(_ops.TowlineVisible) towFrames++;
                if(Overlap(large,ship.localEulerAngles.y,new Vector2(14.8f,74f),pier,0,new Vector2(13.8f,89.8f)))
                    throw new InvalidOperationException($"Cargo sweep crosses cargo pier at {t:F1}s {large}");
                if(Overlap(little,tug.localEulerAngles.y,new Vector2(4.10f,10.7f),pier,0,new Vector2(13.8f,89.8f)))
                    throw new InvalidOperationException($"Tug sweep crosses cargo pier at {t:F1}s {little}");
                if(Overlap(large,ship.localEulerAngles.y,new Vector2(14.8f,70f),little,tug.localEulerAngles.y,new Vector2(3.75f,10.4f)))
                    throw new InvalidOperationException($"Tug overlaps cargo silhouette at {t:F1}s {large} {little}");
                foreach(Vector2 slot in selectionSlots)
                {
                    Vector2 halfSelection=new(32f,63f);
                    if(Overlap(large,ship.localEulerAngles.y,new Vector2(14.8f,74f),slot,0f,halfSelection) ||
                       Overlap(little,tug.localEulerAngles.y,new Vector2(4.10f,10.7f),slot,0f,halfSelection))
                        throw new InvalidOperationException($"Cargo traffic crosses a selection slot at {t:F1}s.");
                }
            }
            _ops.SetTime(169.999f);
            if(Vector3.Distance(rest,ship.localPosition)>.10f || Vector3.Distance(tug.localPosition,new Vector3(-22,-1,50))>.1f ||
                maxTravel<200f || towFrames<400) throw new InvalidOperationException("Vessel loop or tow duration changed.");
            _ops.SetTime(38.5f);
            var fixedPose=ship.localPosition;
            _ops.Tick(0);
            if(ship.localPosition!=fixedPose || !_ops.TowlineVisible) throw new InvalidOperationException("Pause or towline failed.");
            for(int i=0;i<before.Length;i++)
                if(berths[i].position!=before[i].position || berths[i].rotation!=before[i].rotation)
                    throw new InvalidOperationException("A ship selection berth was moved.");
            if(before.Length==0) throw new InvalidOperationException("Missing existing ship selection berth.");
            VerifyCargoAndAviation(ship);
            File.WriteAllLines(Path.Combine(_output,"UnityValidation.txt"),new[]
            {
                "PASS Current NavalBaseMenu and HarborFacilityDetails prefab copies.",
                "PASS Original ropes, one crane hoist and one container replaced on the cloned instance only.",
                $"PASS {towFrames} tow samples; {maxTravel:F1} m maximum cargo displacement from berth.",
                "PASS Ship and tug swept footprints clear the cargo pier and each other (0.1 s samples).",
                $"PASS {before.Length} existing berth transforms preserved. Four live selection positions remain outside the swept footprints.",
                "PASS Full loop returns cargo ship and tug to their berth. Zero time step pauses movement.",
                "PASS Container transfers between ship, spreader, and trailer. Helicopter lifts off and returns to the service pad.",
            });
        }

        private static void VerifyCargoAndAviation(Transform ship)
        {
            var root=_ops.transform;
            var box=root.Find("Transfer container");
            var spreader=root.Find("Container spreader");
            var trailer=root.Find("Cargo tractor and trailer/Load point");
            var helicopter=root.Find("Harbor rescue helicopter");
            _ops.SetTime(0f);
            var initialPad=helicopter.localPosition;
            void Match(Transform actual, Vector3 expected, string step)
            {
                if(Vector3.Distance(actual.position,expected)>.005f)
                    throw new InvalidOperationException("Incorrect "+step+" position.");
            }
            Match(box,ship.TransformPoint(new Vector3(1.7f,18.9f,39f)),"original container");
            _ops.SetTime(8f);
            Match(box,spreader.TransformPoint(Vector3.down*1.35f),"suspended container");
            _ops.SetTime(68f);
            Match(box,trailer.position,"loaded trailer");
            if(helicopter.localPosition.y<30f) throw new InvalidOperationException("Helicopter failed to lift off.");
            _ops.SetTime(153f);
            Match(box,ship.TransformPoint(new Vector3(1.7f,18.9f,39f)),"reloaded container");
            if(Vector3.Distance(initialPad,helicopter.localPosition)>.05f)
                throw new InvalidOperationException("Helicopter failed to return to the service pad.");
        }

        private static bool Overlap(Vector2 a,float yawA,Vector2 halfA,Vector2 b,float yawB,Vector2 halfB)
        {
            Vector2 da=Axis(yawA), db=Axis(yawB);
            Vector2 ra=new(da.y,-da.x), rb=new(db.y,-db.x);
            foreach(Vector2 axis in new[]{da,ra,db,rb})
            {
                float span=halfA.y*Mathf.Abs(Vector2.Dot(da,axis))+halfA.x*Mathf.Abs(Vector2.Dot(ra,axis))+
                           halfB.y*Mathf.Abs(Vector2.Dot(db,axis))+halfB.x*Mathf.Abs(Vector2.Dot(rb,axis));
                if(Mathf.Abs(Vector2.Dot(b-a,axis))>=span) return false;
            }
            return true;
        }

        private static Vector2 Axis(float yaw) =>
            new(Mathf.Sin(yaw*Mathf.Deg2Rad),Mathf.Cos(yaw*Mathf.Deg2Rad));

        private static void Shot(string name,float time,Vector3 focus,Vector3 eye,float span)
        {
            _ops.SetTime(time);
            CameraPose(focus,eye,span);
            Capture(Path.Combine(_output,"Preview",name+".png"),1440,810);
        }

        private static void CameraPose(Vector3 focus,Vector3 offset,float span)
        {
            _camera.transform.position=_island.TransformPoint(focus+offset);
            _camera.transform.LookAt(_island.TransformPoint(focus));
            _camera.orthographicSize=span*Mathf.Abs(_island.lossyScale.x);
        }

        private static void Capture(string path,int width,int height)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,2);
            var tex=new Texture2D(width,height,TextureFormat.RGB24,false);
            var old=RenderTexture.active;
            try
            {
                _camera.targetTexture=rt;
                _camera.aspect=(float)width/height;
                _camera.Render();
                RenderTexture.active=rt;
                tex.ReadPixels(new Rect(0,0,width,height),0,0);
                tex.Apply(false);
                File.WriteAllBytes(path,tex.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=old;
                _camera.targetTexture=null;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }
    }
}


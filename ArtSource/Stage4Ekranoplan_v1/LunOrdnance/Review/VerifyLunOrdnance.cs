using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class VerifyLunOrdnance
{
    private const string Palette = "Assets/_Game/Art/Models/Materials/";

    public static void Run()
    {
        string[] args=Environment.GetCommandLineArgs();
        int index=Array.IndexOf(args,"-lunArtOut");
        if (index<0 || index+1>=args.Length) throw new ArgumentException("Missing -lunArtOut");
        string output=args[index+1];
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var bomb=CheckModel("PRJ_LunBomb","Fuze",.90f,Vector3.forward,new(.60f,.60f,1.80f),.38f,172,
            new[]{"Enemy Dark","Warning Yellow","Enemy hull grey"},out string bombReport);
        var rocket=CheckModel("PRJ_LunRocket","Exhaust",-.80f,Vector3.back,new(.40f,.40f,1.60f),.16f,124,
            new[]{"Enemy hull grey","Emergency Red","Enemy Dark"},out string rocketReport);
        bomb.transform.SetPositionAndRotation(new Vector3(0,0,.35f),Quaternion.Euler(0,90,0));
        rocket.transform.SetPositionAndRotation(new Vector3(0,0,-.41f),Quaternion.Euler(0,90,0));
        Capture(Path.Combine(output,"Preview","Lun_Ordnance_Unity.png"));
        File.WriteAllLines(Path.Combine(output,"UnityValidation.txt"),new[]
        {
            $"Unity {Application.unityVersion} / isolated importer using current game materials",
            bombReport,rocketReport,
            "PASS FBX root scale 100; game-space origin and axes checked using the existing Stage 4 Y=180 model wrapper.",
            "PASS All requested material slots remap to the game's existing palette. No gameplay components on the art assets.",
        });
        Debug.Log("[Lun Ordnance] PASS");
    }

    private static GameObject CheckModel(string name,string socket,float socketZ,Vector3 axis,Vector3 size,
        float diameter,int triangles,string[] materials,out string report)
    {
        string path="Assets/Models/"+name+".fbx";
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);
        var originalNames=model.GetComponentsInChildren<MeshRenderer>()
            .SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Select(m=>m.name).Distinct().OrderBy(n=>n).ToArray();
        Require(originalNames.SequenceEqual(materials.OrderBy(n=>n)),name+" material slot names differ.");
        foreach (var materialName in materials)
        {
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Palette+materialName+".mat");
            Require(mat!=null,name+" palette material missing: "+materialName);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),materialName),mat);
        }
        importer.SaveAndReimport();
        model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(model);
        var wrapper=new GameObject(name+" review");
        instance.transform.SetParent(wrapper.transform,false);
        wrapper.transform.rotation=Quaternion.Euler(0,180,0);
        Require(Vector3.Distance(instance.transform.localScale,Vector3.one*100)<.02f,name+" root scale.");
        Require(Vector3.Distance(instance.transform.position,Vector3.zero)<.001f,name+" root is offset.");
        var locator=instance.transform.Find(socket);
        Require(locator!=null && locator.GetComponent<Renderer>()==null &&
            Vector3.Distance(locator.position,new Vector3(0,0,socketZ))<.002f &&
            Vector3.Angle(axis,locator.forward)<.1f,name+" missing or displaced "+socket);
        Require(instance.transform.childCount==3,name+" unexpected hierarchy.");
        Require(instance.GetComponentsInChildren<Collider>(true).Length==0,name+" contains a collider.");
        var renderers=instance.GetComponentsInChildren<MeshRenderer>();
        Require(renderers.Length==2,name+" needs separate body and fins meshes.");
        Bounds bounds=renderers[0].bounds;
        int count=0;
        foreach (var renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
            var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
            Require(renderer.sharedMaterials.All(m=>m!=null &&
                AssetDatabase.GetAssetPath(m).StartsWith(Palette,StringComparison.Ordinal)),name+" material remap failed.");
            if (renderer.name=="Body")
                Require(Mathf.Abs(renderer.bounds.size.x-diameter)<.002f &&
                    Mathf.Abs(renderer.bounds.size.y-diameter)<.002f,name+" body diameter.");
            for (int i=0;i<mesh.subMeshCount;i++) count+=(int)mesh.GetIndexCount(i)/3;
        }
        Require(Vector3.Distance(bounds.size,size)<.003f && Mathf.Abs(bounds.center.z)<.002f,name+" bounds.");
        Require(count==triangles,name+" triangle count.");
        report=$"PASS {name}: {bounds.size.z:F2} m long / {diameter:F2} m body / {size.x:F2} m fins / {count} tris / {socket} at z={socketZ:F2}.";
        return wrapper;
    }

    private static void Capture(string file)
    {
        RenderSettings.ambientMode=AmbientMode.Flat;
        RenderSettings.ambientLight=new Color(.66f,.73f,.78f);
        var light=new GameObject("Review key",typeof(Light)).GetComponent<Light>();
        light.type=LightType.Directional;
        light.intensity=1f;
        light.transform.rotation=Quaternion.Euler(47,145,0);
        var camera=new GameObject("Review camera",typeof(Camera)).GetComponent<Camera>();
        camera.backgroundColor=new Color(.21f,.34f,.39f);
        camera.clearFlags=CameraClearFlags.SolidColor;
        camera.transform.position=new Vector3(0f,4.8f,-2.05f);
        camera.transform.LookAt(Vector3.zero,Vector3.forward);
        camera.orthographic=true;
        camera.orthographicSize=.82f;
        camera.aspect=16f/9f;
        camera.allowHDR=false;
        const int width=1440,height=810;
        var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);
        var tex=new Texture2D(width,height,TextureFormat.RGB24,false);
        var old=RenderTexture.active;
        try
        {
            camera.targetTexture=rt;
            camera.Render();
            RenderTexture.active=rt;
            tex.ReadPixels(new Rect(0,0,width,height),0,0);
            tex.Apply();
            File.WriteAllBytes(file,tex.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=null;
            RenderTexture.active=old;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(tex);
        }
    }

    private static void Require(bool ok,string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}

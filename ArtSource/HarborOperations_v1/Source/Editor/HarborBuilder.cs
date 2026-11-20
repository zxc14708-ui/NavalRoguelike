using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HarborOperationsPack.Editor
{
    public static partial class HarborBuilder
    {
        public const string Root = "Assets/HarborOperations";
        private const string Data = Root + "/Generated";
        private const string PrefabPath = Root + "/Resources/HarborOperations/Operations.prefab";
        private const string ExistingPalette = "Assets/_Game/Art/NavalBase/MAT_base_";
        private static readonly Dictionary<string, Material> Materials = new();
        private static Mesh _cube, _cylinder, _sphere;
        private static Transform _root;

        [MenuItem("Tools/Harbor Operations/Build Overlay")]
        public static void Build()
        {
            Directory.CreateDirectory(Data);
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            AssetDatabase.Refresh();
            Materials.Clear();
            _cube=Primitive(PrimitiveType.Cube);
            _cylinder=Primitive(PrimitiveType.Cylinder);
            _sphere=Primitive(PrimitiveType.Sphere);
            _root=new GameObject("HarborOperations").transform;
            try
            {
                Transform details=Group(_root,"Static facility details");
                Roads(details);
                FacilityDetails(details);
                AviationApron(details);
                Batch(details);

                var component=_root.gameObject.AddComponent<HarborOperations>();
                var serialized=new SerializedObject(component);
                void Bind(string key,Transform part) => serialized.FindProperty(key).objectReferenceValue=part;

                Transform crane=Group(_root,"Crane trolley");
                Box(crane,"machinery",Vector3.zero,new(6.3f,2.7f,7.7f),"steel_dark");
                Box(crane,"cab",new(-1.85f,-1.30f,2.6f),new(2.1f,2.0f,2.4f),"wall_white");
                Box(crane,"cab window",new(-1.85f,-1.15f,3.83f),new(1.7f,1.1f,.12f),"glass_blue");
                Box(crane,"guide rail",new(0f,1.2f,0),new(6.7f,.6f,8.2f),"crane_red");
                Bind("trolley",crane);
                Transform spreader=Group(_root,"Container spreader");
                Box(spreader,"spreader",Vector3.zero,new(3.6f,.50f,12.35f),"safety_yellow");
                foreach(float z in new[]{-5.6f,5.6f})
                {
                    Box(spreader,"lock",new(0f,-.30f,z),new(3.65f,.65f,.72f),"steel_dark");
                    Box(spreader,"guide",new(0f,.38f,z*.48f),new(1.45f,.47f,1.45f),"crane_red");
                }
                Bind("spreader",spreader);
                Bind("craneCableA",UnitLine(_root,"Hoist cable L","steel_dark"));
                Bind("craneCableB",UnitLine(_root,"Hoist cable R","steel_dark"));
                Transform container=Group(_root,"Transfer container");
                Container(container);
                Bind("transferBox",container);
                Transform trailer=Truck(_root,false,out Transform wheels);
                Bind("truck",trailer);
                Bind("truckWheels",wheels);
                Bind("truckSocket",Group(trailer,"Load point",new(0,4.26f,0)));
                Bind("serviceTruck",Truck(_root,true,out _));
                Transform helicopter=HelicopterModel(_root,out Transform rotor,out Transform tailRotor);
                Bind("helicopter",helicopter);
                Bind("rotor",rotor);
                Bind("tailRotor",tailRotor);

                Transform lines=Group(_root,"Berth lines");
                Transform shipLines=Group(lines,"Cargo lines");
                Transform tugLines=Group(lines,"Tug lines");
                for (int i=0;i<4;i++) UnitLine(shipLines,"Mooring "+i,"rope");
                for (int i=0;i<2;i++) UnitLine(tugLines,"Tug berth "+i,"rope");
                Bind("dockTugLines",lines);
                Transform tow=Group(_root,"Tow cable");
                UnitLine(tow,"Bow segment","rope");
                UnitLine(tow,"Tug segment","rope");
                Bind("towLines",tow);
                Bind("shipWake",Wake(_root,"Cargo wake"));
                Bind("tugWake",Wake(_root,"Tug wake"));
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(_root.gameObject,PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[Harbor Operations] Road/facility overlay and moving assets built.");
            }
            finally { Object.DestroyImmediate(_root.gameObject); }
        }

        private static Material M(string name)
        {
            if (Materials.TryGetValue(name,out var found)) return found;
            var mat=AssetDatabase.LoadAssetAtPath<Material>(ExistingPalette+name+".mat");
            if (mat==null) throw new InvalidOperationException("Missing harbor palette material "+name);
            return Materials[name]=mat;
        }

        private static Mesh Primitive(PrimitiveType type)
        {
            var go=GameObject.CreatePrimitive(type);
            var source=go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go);
            return source;
        }

        private static Transform Group(Transform p,string name,Vector3 at=default)
        {
            var go=new GameObject(name).transform;
            go.SetParent(p,false);
            go.localPosition=at;
            return go;
        }

        private static Transform Part(Transform parent,string name,Mesh mesh,string material,Vector3 at,Vector3 size,Quaternion rotation)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)).transform;
            go.SetParent(parent,false);
            go.localPosition=at;
            go.localScale=size;
            go.localRotation=rotation;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial=M(material);
            if (size.magnitude<8f) renderer.shadowCastingMode=ShadowCastingMode.Off;
            return go;
        }

        private static Transform Box(Transform p,string name,Vector3 at,Vector3 size,string mat,float yaw=0f)
            => Part(p,name,_cube,mat,at,size,Quaternion.Euler(0f,yaw,0f));

        private static Transform Tube(Transform p,string name,Vector3 at,float radius,float height,string mat, Quaternion? rotation=null)
            => Part(p,name,_cylinder,mat,at,new Vector3(radius*2f,height*.5f,radius*2f),rotation??Quaternion.identity);

        private static Transform Beam(Transform p,string name,Vector3 a,Vector3 b,float width,string mat)
            => Part(p,name,_cube,mat,(a+b)*.5f,new(width,width,(b-a).magnitude),Quaternion.LookRotation(b-a));

        private static Transform UnitLine(Transform p,string name,string mat)
            => Part(p,name,_cube,mat,Vector3.zero,Vector3.one,Quaternion.identity);

        private static Mesh SaveMesh(string key,Mesh mesh)
        {
            mesh.name=key;
            string path=Data+"/"+key+".asset";
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old==null) { AssetDatabase.CreateAsset(mesh,path); return mesh; }
            EditorUtility.CopySerialized(mesh,old);
            Object.DestroyImmediate(mesh);
            return old;
        }

        private static Mesh Surface(string name,IList<Vector3> verts,IList<int> tris)
        {
            var mesh=new Mesh { name=name,indexFormat=IndexFormat.UInt32 };
            mesh.SetVertices(verts.ToList());
            mesh.SetTriangles(tris.ToList(),0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void Batch(Transform root)
        {
            var source=root.GetComponentsInChildren<MeshFilter>();
            var materials=source.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial);
            foreach(var group in materials)
            {
                Material material=group.Key;
                var blocks=group.Select(f=>new CombineInstance
                {
                    mesh=f.sharedMesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix
                }).ToArray();
                var mesh=new Mesh { indexFormat=IndexFormat.UInt32 };
                mesh.CombineMeshes(blocks,true,true);
                var merged=new GameObject(material.name,typeof(MeshFilter),typeof(MeshRenderer)).transform;
                merged.SetParent(root,false);
                merged.GetComponent<MeshFilter>().sharedMesh=SaveMesh("Harbor_" + material.name,mesh);
                merged.GetComponent<MeshRenderer>().sharedMaterial=material;
            }
            foreach(var filter in source) Object.DestroyImmediate(filter.gameObject);
        }

        private static Transform Wake(Transform root,string name)
        {
            const string key="WakeFoam";
            var material=AssetDatabase.LoadAssetAtPath<Material>(Data+"/"+key+".mat");
            if (material==null)
            {
                material=new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name=key };
                material.SetColor("_BaseColor",new Color(.79f,.90f,.94f,1f));
                AssetDatabase.CreateAsset(material,Data+"/"+key+".mat");
            }
            var verts=new List<Vector3>();
            var tris=new List<int>();
            for (int s=-1;s<=1;s+=2)
                for (int i=0;i<8;i++)
                {
                    float z=-.08f-i*.121f;
                    float x=s*(.09f+i*.046f);
                    float width=.09f*(1f-.09f*i);
                    int v=verts.Count;
                    verts.Add(new Vector3(x-width,0,z));
                    verts.Add(new Vector3(x+width,0,z+.018f));
                    verts.Add(new Vector3(x+s*.16f,0,z-.084f));
                    tris.AddRange(new[]{v,v+1,v+2});
                }
            var t=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)).transform;
            t.SetParent(root,false);
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Data+"/Wake.asset") ?? SaveMesh("Wake",Surface("Wake",verts,tris));
            t.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=t.GetComponent<MeshRenderer>();
            renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            return t;
        }
    }
}


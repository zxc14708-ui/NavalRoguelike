using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>Art-only proposals. Never writes module definitions or operational prefabs.</summary>
    public static class WeaponUpgradeConceptBuilder
    {
        const string Folder="Assets/_Game/Art/WeaponConcepts";
        static Material armor, edge, metal, dark, glass, accent;
        static string prefix;
        static int serial;
        static Transform Group(Transform p,string n,Vector3 pos=default)
        {var t=new GameObject(n).transform;t.SetParent(p,false);t.localPosition=pos;return t;}
        static Material Mat(string n,Color c)
        {string path=Folder+"/"+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.color=c;m.SetFloat("_Smoothness",.23f);EditorUtility.SetDirty(m);return m;}
        static Transform Box(Transform p,string n,Vector3 pos,Vector3 size,Material m)
        {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;Object.DestroyImmediate(g.GetComponent<Collider>());return g.transform;}
        static Transform Cylinder(Transform p,string n,Vector3 pos,float diameter,float length,Material m,bool alongZ=false)
        {var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=new Vector3(diameter,length/2,diameter);if(alongZ)g.transform.localRotation=Quaternion.Euler(90,0,0);g.GetComponent<Renderer>().sharedMaterial=m;Object.DestroyImmediate(g.GetComponent<Collider>());return g.transform;}
        static void Facet(Transform p,string n,Vector3 pos,Vector3 size,float taper,Material material)
        {
            // Chamfered eight-sided housing with independent face normals.
            Vector2[] outline={new(-.35f,-.5f),new(.35f,-.5f),new(.5f,-.35f),new(.5f,.35f),new(.35f,.5f),new(-.35f,.5f),new(-.5f,.35f),new(-.5f,-.35f)};
            var points=new Vector3[16];for(int i=0;i<8;i++){points[i]=new Vector3(outline[i].x*size.x,-size.y/2,outline[i].y*size.z);points[i+8]=new Vector3(outline[i].x*size.x*taper,size.y/2,outline[i].y*size.z*taper);}
            var verts=new List<Vector3>();var tris=new List<int>();
            void Tri(int a,int b,int c){int k=verts.Count;verts.Add(points[a]);verts.Add(points[b]);verts.Add(points[c]);tris.AddRange(new[]{k,k+1,k+2});}
            for(int i=0;i<8;i++){int j=(i+1)%8;Tri(i,j+8,j);Tri(i,i+8,j+8);}for(int i=1;i<7;i++){Tri(8,8+i+1,8+i);Tri(0,i,i+1);}
            string path=Folder+"/"+prefix+"_"+(serial++)+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}mesh.Clear();mesh.SetVertices(verts);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            var t=Group(p,n,pos);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
        static void Base(Transform t)
        {Facet(t,"Deck adapter",new Vector3(0,.1f,0),new Vector3(1.7f,.2f,1.7f),.94f,armor);Cylinder(t,"Traverse ring",new Vector3(0,.26f,0),1.18f,.15f,dark);Cylinder(t,"Ring shoulder",new Vector3(0,.35f,0),1.04f,.08f,edge);foreach(float x in new[]{-.65f,.65f})foreach(float z in new[]{-.65f,.65f})Cylinder(t,"Anchor bolt",new Vector3(x,.22f,z),.07f,.045f,metal);}
        static void Sensor(Transform t,Vector3 pos,int level)
        {Facet(t,"Optical sight",pos,new Vector3(.22f,.22f,.3f),.9f,edge);Box(t,"Lens",pos+Vector3.forward*.153f,new Vector3(.14f,.09f,.018f),glass);if(level==2){Cylinder(t,"Antenna",pos+new Vector3(0,.34f,-.07f),.025f,.5f,metal);}}
        static void Gun(Transform t,int level,bool howitzer)
        {
            Base(t);var turret=Group(t,"TurretPivot",new Vector3(0,.38f,0));
            float height=howitzer?.65f:.48f;
            Facet(turret,"Sloped turret",new Vector3(0,height/2,-.08f),new Vector3(1.13f+level*.1f,height,1.12f),.72f,armor);
            Facet(turret,"Roof hatch",new Vector3(-.23f,height+.025f,-.22f),new Vector3(.31f,.06f,.36f),.85f,edge);
            Box(turret,"Hatch handle",new Vector3(-.23f,height+.08f,-.22f),new Vector3(.14f,.04f,.045f),metal);
            var elev=Group(turret,"ElevationPivot",new Vector3(0,.34f,.4f));elev.localRotation=Quaternion.Euler(howitzer?-38:-9,0,0);
            Facet(elev,"Gun cradle",Vector3.zero,new Vector3(howitzer?.5f:.7f,.32f,.4f),.85f,edge);
            float length=howitzer?1.55f+level*.08f:1.25f;
            foreach(float x in howitzer?new[]{0f}:new[]{-.18f,.18f})
            {
                Cylinder(elev,"Barrel jacket",new Vector3(x,0,.25f),howitzer?.22f:.14f,.55f,metal,true);
                Cylinder(elev,"Barrel",new Vector3(x,0,length/2),howitzer?.13f:.075f,length,metal,true);
                Cylinder(elev,"Muzzle collar",new Vector3(x,0,length),howitzer?.22f:.12f,.12f,edge,true);
                Cylinder(elev,"Dark bore",new Vector3(x,0,length+.065f),howitzer?.12f:.07f,.008f,dark,true);
                if(level>0)for(int j=0;j<3;j++)Cylinder(elev,"Cooling collar",new Vector3(x,0,.4f+j*.12f),howitzer?.25f:.17f,.05f,edge,true);
            }
            if(howitzer){foreach(float x in new[]{-.2f,.2f})Cylinder(elev,"Recoil cylinder",new Vector3(x,-.15f,.4f),.085f,.65f,edge,true);}
            for(int i=0;i<4;i++)Box(turret,"Rear ventilation",new Vector3(-.3f+i*.2f,.13f,-.595f),new Vector3(.09f,.10f,.025f),dark);
            Sensor(turret,new Vector3(.35f,height+.11f,.04f),level);
            if(level>0)foreach(float x in new[]{-.67f,.67f})Facet(turret,howitzer?"Autoloader side housing":"Ammunition feed housing",new Vector3(x,.2f,-.17f),new Vector3(.28f,.4f,.65f),.82f,edge);
            if(level==2){Facet(turret,"Rear service pack",new Vector3(0,.29f,-.73f),new Vector3(.8f,.4f,.3f),.86f,armor);foreach(float x in new[]{-.55f,.55f})Box(turret,"Identification stripe",new Vector3(x,.4f,.3f),new Vector3(.12f,.07f,.02f),accent);}
        }
        static void Ram(Transform t,int level)
        {
            Facet(t,"Bow attachment",new Vector3(0,.12f,-.3f),new Vector3(1.7f,.24f,1.1f),.94f,armor);
            foreach(float x in new[]{-.5f,.5f}){Box(t,"Structural spine",new Vector3(x,.22f,.15f),new Vector3(.23f,.32f,1.65f),metal);for(int z=0;z<3;z++)Cylinder(t,"Mounting bolt",new Vector3(x,.4f,-.6f+z*.35f),.09f,.045f,edge);}
            var nose=Group(t,"Tapered ram",new Vector3(0,.28f,.65f));nose.localRotation=Quaternion.Euler(90,0,0);
            Facet(nose,"Forged wedge",Vector3.zero,new Vector3(1.65f,.95f,.48f),.15f,armor);
            if(level>0)foreach(float x in new[]{-.68f,.68f}){var wing=Box(t,"Reinforced cheek",new Vector3(x,.28f,.56f),new Vector3(.17f,.42f,1.1f),edge);wing.localRotation=Quaternion.Euler(0,x<0?24:-24,0);}
            if(level==2){foreach(float x in new[]{-.45f,.45f}){Box(t,"Absorber saddle",new Vector3(x,.44f,-.3f),new Vector3(.22f,.18f,.45f),metal);Cylinder(t,"Shock absorber",new Vector3(x,.55f,-.3f),.16f,.75f,edge,true);Cylinder(t,"Piston",new Vector3(x,.55f,.17f),.08f,.35f,metal,true);}Box(t,"Replaceable impact edge",new Vector3(0,.28f,1.16f),new Vector3(.42f,.34f,.13f),metal);}
            for(int i=0;i<=level;i++)Box(t,"Warning marker",new Vector3(-.18f+i*.16f,.255f,-.76f),new Vector3(.08f,.02f,.12f),accent);
        }
        static void Torpedo(Transform t,int level)
        {
            Base(t);var mount=Group(t,"TubeMount",new Vector3(0,.54f,0));
            Facet(mount,"Cradle",new Vector3(0,-.06f,0),new Vector3(1.0f,.18f,1.0f),.9f,metal);
            Vector3[] positions={new(-.23f,.13f,0),new(.23f,.13f,0),new(0,.53f,0)};
            foreach(var p in positions)
            {Cylinder(mount,"Launch tube",p,.4f,1.5f,armor,true);foreach(float z in new[]{-.5f,.5f})Cylinder(mount,"Tube clamp",p+Vector3.forward*z,.44f,.09f,edge,true);Cylinder(mount,"End collar",p+Vector3.forward*.77f,.46f,.10f,metal,true);Cylinder(mount,"Closed launch cap",p+Vector3.forward*.83f,.36f,.02f,dark,true);Box(mount,"Cap hinge",p+new Vector3(0,.23f,.76f),new Vector3(.12f,.08f,.14f),edge);}
            if(level>0){Facet(mount,"Valve enclosure",new Vector3(.63f,.16f,-.22f),new Vector3(.28f,.4f,.7f),.88f,edge);foreach(float x in new[]{-.53f,.53f})Cylinder(mount,"Air reservoir",new Vector3(x,.13f,0),.13f,1.1f,metal,true);}
            if(level==2){foreach(float x in new[]{-.65f,.65f})Facet(mount,"Fragment shield",new Vector3(x,.33f,.1f),new Vector3(.12f,.65f,1.5f),.8f,armor);Facet(mount,"Service cover",new Vector3(0,.55f,-.88f),new Vector3(.8f,.6f,.18f),.9f,edge);Sensor(mount,new Vector3(0,.86f,-.55f),0);}
        }
        [MenuItem("Naval/Art/Preview Weapon Upgrade Concepts")]
        public static void RunBatch()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);NavalEditorUtil.EnsureFolder(Folder);
            armor=Mat("Naval grey",new Color(.38f,.46f,.51f));edge=Mat("Edge grey",new Color(.52f,.59f,.61f));metal=Mat("Gunmetal",new Color(.19f,.25f,.29f));dark=Mat("Recess",new Color(.035f,.055f,.065f));glass=Mat("Sensor teal",new Color(.13f,.49f,.53f));accent=Mat("Safety amber",new Color(.82f,.53f,.19f));
            var light=new GameObject("Studio key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;light.transform.rotation=Quaternion.Euler(40,-35,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.6f,.67f,.72f);
            var cam=new GameObject("Concept camera").AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=1.75f;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.035f,.065f,.09f);
            string output=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-29/referenced-chatgpt-conversation-this-is-an/outputs/weapon_upgrade_concepts");Directory.CreateDirectory(output);
            string[] names={"Nobong","Howitzer","RamBow","TorpedoTube"};
            foreach(string name in names)
            {
                var board=new Texture2D(2100,700,TextureFormat.RGB24,false);
                for(int level=0;level<3;level++)
                {
                    prefix=name+"_T"+level;serial=0;var root=new GameObject(prefix);
                    if(name=="Nobong"||name=="Howitzer")Gun(root.transform,level,name=="Howitzer");else if(name=="RamBow")Ram(root.transform,level);else Torpedo(root.transform,level);
                    PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+prefix+".prefab");
                    var focus=new Vector3(0,.7f,.25f);cam.transform.position=focus+new Vector3(3.5f,2.8f,4.5f);cam.transform.LookAt(focus);
                    var label=new GameObject("Stage label");var tm=label.AddComponent<TextMesh>();tm.text=level==0?"BASE":"UPGRADE "+level;tm.fontSize=48;tm.characterSize=.07f;tm.anchor=TextAnchor.MiddleCenter;tm.color=new Color(.7f,.83f,.87f);label.transform.rotation=cam.transform.rotation;label.transform.position=focus-cam.transform.up*1.45f;
                    var rt=new RenderTexture(700,700,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;board.ReadPixels(new Rect(0,0,700,700),level*700,0);RenderTexture.active=null;cam.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(root);Object.DestroyImmediate(label);
                }
                board.Apply();File.WriteAllBytes(Path.Combine(output,name+"_stages.png"),board.EncodeToPNG());Object.DestroyImmediate(board);
            }
            AssetDatabase.SaveAssets();Debug.Log("[WeaponConcepts] PASS: 12 art-only prefabs and four stage boards. No gameplay assets changed.");
        }
    }
}

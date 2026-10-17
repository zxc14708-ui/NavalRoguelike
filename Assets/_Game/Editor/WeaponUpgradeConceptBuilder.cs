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
            float height=howitzer?.78f:.31f;
            if(howitzer)
            {
                Facet(turret,"Tall angular turret",new Vector3(0,height/2,-.09f),new Vector3(1.05f,height,1.06f),.77f,armor);
                Facet(turret,"Roof hatch",new Vector3(-.22f,height+.015f,-.23f),new Vector3(.32f,.05f,.35f),.85f,edge);
                Box(turret,"Hatch handle",new Vector3(-.22f,height+.06f,-.23f),new Vector3(.13f,.035f,.04f),metal);
                Facet(turret,"Heavy mantlet",new Vector3(0,.42f,.48f),new Vector3(.5f,.55f,.21f),.78f,edge);
            }
            else
            {
                Facet(turret,"Low rear mechanism",new Vector3(0,.15f,-.27f),new Vector3(1.05f,.3f,.66f),.86f,armor);
                foreach(float x in new[]{-.52f,.52f})
                {var plate=Box(turret,"Open shield cheek",new Vector3(x,.36f,.13f),new Vector3(.13f,.53f,.86f),edge);plate.localRotation=Quaternion.Euler(0,0,x<0?-13:13);}
                Box(turret,"Shield front lip",new Vector3(0,.2f,.52f),new Vector3(1.08f,.16f,.12f),edge);
            }
            var elev=Group(turret,"ElevationPivot",new Vector3(0,howitzer?.48f:.3f,.42f));
            Facet(elev,"Gun cradle",Vector3.zero,new Vector3(howitzer?.43f:.7f,.27f,.38f),.83f,edge);
            var barrel=Group(elev,"Barrel");float length=howitzer?1.19f:1.16f;
            foreach(float x in howitzer?new[]{0f}:new[]{-.14f,.14f})
            {
                Cylinder(barrel,"Barrel jacket",new Vector3(x,0,.22f),howitzer?.27f:.13f,.45f,metal,true);
                Cylinder(barrel,"Tube",new Vector3(x,0,length/2),howitzer?.19f:.075f,length,metal,true);
                Cylinder(barrel,"Muzzle collar",new Vector3(x,0,length),howitzer?.34f:.12f,.13f,edge,true);
                Cylinder(barrel,"Dark bore",new Vector3(x,0,length+.07f),howitzer?.15f:.07f,.008f,dark,true);
                if(level>0)for(int j=0;j<3;j++)Cylinder(barrel,"Cooling collar",new Vector3(x,0,.36f+j*.11f),howitzer?.3f:.17f,.045f,edge,true);
            }
            if(howitzer)
            {
                foreach(float x in new[]{-.22f,.22f})Cylinder(elev,"Recoil cylinder",new Vector3(x,-.15f,.32f),.09f,.55f,edge,true);
                Box(barrel,"Muzzle brake crosspiece",new Vector3(0,0,length+.025f),new Vector3(.48f,.11f,.13f),metal);
            }
            Group(elev,"Muzzle",new Vector3(0,0,length+.075f));
            for(int i=0;i<4;i++)Box(turret,"Rear ventilation",new Vector3(-.3f+i*.2f,.13f,-.585f),new Vector3(.08f,.09f,.025f),dark);
            Sensor(turret,howitzer?new Vector3(.36f,.78f,.02f):new Vector3(.39f,.46f,-.05f),level);
            if(level>0)foreach(float x in new[]{-.67f,.67f})Facet(turret,howitzer?"Autoloader side housing":"Ammunition feed housing",new Vector3(x,.2f,-.17f),new Vector3(.25f,.39f,.59f),.82f,edge);
            if(level==2){Facet(turret,"Rear service pack",new Vector3(0,.29f,-.7f),new Vector3(.73f,.4f,.25f),.86f,armor);foreach(float x in new[]{-.55f,.55f})Box(turret,"Identification stripe",new Vector3(x,.37f,.27f),new Vector3(.1f,.06f,.02f),accent);}
        }
        static void Ram(Transform t,int level)
        {
            Facet(t,"Bow attachment",new Vector3(0,.12f,-.3f),new Vector3(1.7f,.24f,1.1f),.94f,armor);
            foreach(float x in new[]{-.5f,.5f}){Box(t,"Structural spine",new Vector3(x,.22f,.15f),new Vector3(.23f,.32f,1.65f),metal);for(int z=0;z<3;z++)Cylinder(t,"Mounting bolt",new Vector3(x,.4f,-.6f+z*.35f),.09f,.045f,edge);}
            var nose=Group(t,"Tapered ram",new Vector3(0,.28f,.65f));nose.localRotation=Quaternion.Euler(90,0,0);
            Facet(nose,"Forged wedge",Vector3.zero,new Vector3(1.65f,.95f,.48f),.15f,armor);
            if(level>0)foreach(float x in new[]{-.68f,.68f}){var wing=Box(t,"Reinforced cheek",new Vector3(x,.28f,.56f),new Vector3(.17f,.42f,1.1f),edge);wing.localRotation=Quaternion.Euler(0,x<0?24:-24,0);}
            if(level==2){foreach(float x in new[]{-.45f,.45f}){Box(t,"Absorber saddle",new Vector3(x,.18f,-.4f),new Vector3(.24f,.22f,.28f),metal);Cylinder(t,"Short hydraulic absorber",new Vector3(x,.16f,-.3f),.22f,.3f,edge,true);Cylinder(t,"Piston pin",new Vector3(x,.16f,-.08f),.1f,.14f,metal,true);Box(t,"Absorber access cover",new Vector3(x,.42f,-.35f),new Vector3(.20f,.05f,.27f),dark);}Box(t,"Replaceable impact edge",new Vector3(0,.28f,1.16f),new Vector3(.42f,.34f,.13f),metal);}
            for(int i=0;i<=level;i++)Box(t,"Warning marker",new Vector3(-.18f+i*.16f,.255f,-.76f),new Vector3(.08f,.02f,.12f),accent);
        }
        static void Torpedo(Transform t,int level,bool alternative=false)
        {
            Facet(t,"Low deck adapter",new Vector3(0,.075f,0),new Vector3(1.7f,.15f,1.7f),.96f,armor);
            Cylinder(t,"Low traverse ring",new Vector3(0,.20f,0),.96f,.11f,dark);
            var mount=Group(t,"TubeMount",new Vector3(0,.28f,0));
            Facet(mount,"Cradle",new Vector3(0,-.015f,0),new Vector3(1.25f,.12f,1.0f),.9f,metal);
            Vector3[] positions={new(-.4f,.14f,0),new(0,.14f,0),new(.4f,.14f,0)};
            for(int i=0;i<positions.Length;i++)
            {
                var p=positions[i];Cylinder(mount,"Launch tube",p,.29f,1.42f,armor,true);
                foreach(float z in new[]{-.5f,.5f})Cylinder(mount,"Tube clamp",p+Vector3.forward*z,.33f,.07f,edge,true);
                Cylinder(mount,"End collar",p+Vector3.forward*.72f,.34f,.075f,metal,true);
                Cylinder(mount,"Closed launch cap",p+Vector3.forward*.76f,.26f,.02f,dark,true);
                Box(mount,"Cap hinge",p+new Vector3(0,.17f,.70f),new Vector3(.1f,.06f,.12f),edge);
                Group(mount,$"LaunchPoint_{i+1:00}",p+Vector3.forward*.84f);
            }
            if(level>0)
            {
                Facet(mount,"Valve enclosure",new Vector3(.66f,.12f,-.23f),new Vector3(.22f,.24f,.48f),.88f,edge);
                foreach(float x in new[]{-.59f,.59f})Cylinder(mount,"Air reservoir",new Vector3(x,.07f,0),.1f,.92f,metal,true);
            }
            if(level==2 || alternative)
            {
                Facet(mount,"Rear sequencing unit",new Vector3(0,.13f,-.69f),new Vector3(.94f,.25f,.28f),.88f,armor);
                foreach(float x in new[]{-.64f,.64f})
                {
                    Box(mount,"Service rail",new Vector3(x,-.03f,0),new Vector3(.08f,.1f,1.34f),edge);
                    Cylinder(mount,"Cap actuator",new Vector3(x,.12f,.1f),.075f,.67f,edge,true);
                    Cylinder(mount,"Actuator rod",new Vector3(x,.12f,.52f),.035f,.17f,metal,true);
                    Box(mount,"Actuator bracket",new Vector3(x,.12f,.61f),new Vector3(.12f,.18f,.1f),metal);
                }
                foreach(float z in new[]{-.45f,.45f})
                {
                    Box(mount,"Clamp bridge",new Vector3(0,.32f,z),new Vector3(.22f,.06f,.1f),edge);
                    Box(mount,"Lift eye base",new Vector3(0,.36f,z),new Vector3(.12f,.03f,.1f),metal);
                }
                Facet(mount,"Maintenance console",new Vector3(-.64f,.18f,-.42f),new Vector3(.19f,.19f,.24f),.85f,armor);
                Box(mount,"Status display",new Vector3(-.64f,.2f,-.29f),new Vector3(.12f,.07f,.01f),glass);
                foreach(var p in positions)Box(mount,"Tube identification",p+new Vector3(0,.164f,.23f),new Vector3(.10f,.012f,.13f),accent);
            }
        }
        static void CombinePart(Transform part,string tag)
        {
            var filters=new List<MeshFilter>();
            void Collect(Transform parent)
            {
                foreach(Transform child in parent)
                {
                    if(child.name=="TurretPivot" || child.name=="ElevationPivot" || child.name=="Barrel" || child.name=="TubeMount")continue;
                    if(child.TryGetComponent<MeshFilter>(out var f) && child.TryGetComponent<MeshRenderer>(out _))filters.Add(f);
                    Collect(child);
                }
            }
            Collect(part);
            var byMaterial=new Dictionary<Material,List<MeshFilter>>();
            foreach(var f in filters){var m=f.GetComponent<MeshRenderer>().sharedMaterial;if(!byMaterial.TryGetValue(m,out var list))byMaterial[m]=list=new List<MeshFilter>();list.Add(f);}
            int index=0;
            foreach(var pair in byMaterial)
            {
                var instances=new CombineInstance[pair.Value.Count];
                for(int i=0;i<instances.Length;i++){instances[i].mesh=pair.Value[i].sharedMesh;instances[i].transform=part.worldToLocalMatrix*pair.Value[i].transform.localToWorldMatrix;}
                var merged=new Mesh{name=tag+" "+pair.Key.name};merged.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;merged.CombineMeshes(instances,true,true);
                string path=Folder+"/"+prefix+"_Combined_"+tag+"_"+(index++)+".asset";
                var previous=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(previous==null)AssetDatabase.CreateAsset(merged,path);
                else{EditorUtility.CopySerialized(merged,previous);Object.DestroyImmediate(merged);merged=previous;EditorUtility.SetDirty(merged);}
                var body=Group(part,tag+"_"+pair.Key.name);body.gameObject.AddComponent<MeshFilter>().sharedMesh=merged;body.gameObject.AddComponent<MeshRenderer>().sharedMaterial=pair.Key;
            }
            foreach(var f in filters)Object.DestroyImmediate(f.gameObject);
        }
        static void CombineModel(Transform root)
        {
            CombinePart(root,"Fixed");
            var turret=root.Find("TurretPivot");
            if(turret!=null)
            {
                CombinePart(turret,"Turret");
                var elevation=turret.Find("ElevationPivot");
                if(elevation!=null)
                {
                    CombinePart(elevation,"Elevation");
                    var barrel=elevation.Find("Barrel");if(barrel!=null)CombinePart(barrel,"Barrel");
                }
            }
            var mount=root.Find("TubeMount");if(mount!=null)CombinePart(mount,"Mount");
        }
        static void ValidateModel(GameObject go,string name)
        {
            var renderers=go.GetComponentsInChildren<Renderer>();if(renderers.Length==0)throw new InvalidOperationException(name+": no meshes");
            var bounds=renderers[0].bounds;var materials=new HashSet<Material>();
            foreach(var r in renderers){bounds.Encapsulate(r.bounds);foreach(var m in r.sharedMaterials)if(m!=null)materials.Add(m);}
            const float tolerance=.035f;
            if(bounds.min.x < -1-tolerance || bounds.max.x > 1+tolerance || bounds.min.z < -1-tolerance || bounds.max.z > 1.72f)
                throw new InvalidOperationException($"{name}: outside module envelope: {bounds.min}..{bounds.max}");
            if(name.StartsWith("TorpedoTube") && bounds.max.y>.9f+tolerance)
                throw new InvalidOperationException($"{name}: torpedo launcher is too high ({bounds.max.y:F3}m)");
            if(go.GetComponentInChildren<Collider>()!=null)throw new InvalidOperationException(name+": collider in art-only model");
            Debug.Log($"[WeaponConcepts] {name}: X {bounds.min.x:F2}..{bounds.max.x:F2}, Y {bounds.min.y:F2}..{bounds.max.y:F2}, Z {bounds.min.z:F2}..{bounds.max.z:F2}; meshes {renderers.Length}, materials {materials.Count}");
        }
        static void SaveVisualPack(string name)
        {
            var pack=new GameObject(name+"_UpgradeVisuals");
            for(int level=0;level<3;level++)
            {
                var model=AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{name}_T{level}.prefab");
                var copy=Object.Instantiate(model,pack.transform);copy.name=$"Visual_Level{level+1}";copy.SetActive(level==0);
                if(name=="Nobong" || name=="Howitzer")
                {
                    var turret=copy.transform.Find("TurretPivot");var elev=turret!=null?turret.Find("ElevationPivot"):null;
                    if(turret==null || elev==null || FindDeep(elev,"Muzzle")==null)throw new InvalidOperationException(name+": missing gun socket at level "+level);
                }
                if(name=="TorpedoTube")
                {
                    var mount=copy.transform.Find("TubeMount");
                    if(mount==null || mount.Find("LaunchPoint_01")==null || mount.Find("LaunchPoint_02")==null || mount.Find("LaunchPoint_03")==null)throw new InvalidOperationException(name+": missing torpedo socket at level "+level);
                }
            }
            PrefabUtility.SaveAsPrefabAsset(pack,$"{Folder}/{name}_UpgradeVisuals.prefab");Object.DestroyImmediate(pack);
        }
        static Transform FindDeep(Transform root,string name)
        {if(root.name==name)return root;foreach(Transform child in root){var found=FindDeep(child,name);if(found!=null)return found;}return null;}
        static void CaptureTopDown(Camera cam,string output)
        {
            string[] names={"Nobong","Howitzer","MOD_Gun76","MOD_Autocannon"};
            string[] labels={"NOBONG 40", "HOWITZER", "76 MM GUN", "AUTOCANNON"};
            var board=new Texture2D(2800,700,TextureFormat.RGB24,false);
            cam.orthographicSize=1.65f;
            for(int i=0;i<names.Length;i++)
            {
                string path=i<2?$"{Folder}/{names[i]}_T2.prefab":$"Assets/_Game/Prefabs/Modules/{names[i]}.prefab";
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(source==null)throw new InvalidOperationException("Missing top-down reference: "+path);
                var model=Object.Instantiate(source);model.name=labels[i];
                var focus=new Vector3(0,.42f,.18f);cam.transform.position=focus+new Vector3(1.25f,7f,2.1f);cam.transform.LookAt(focus);
                var label=new GameObject("Comparison label");var tm=label.AddComponent<TextMesh>();tm.text=labels[i];tm.fontSize=48;tm.characterSize=.056f;tm.anchor=TextAnchor.MiddleCenter;tm.color=new Color(.7f,.83f,.87f);label.transform.rotation=cam.transform.rotation;label.transform.position=focus-cam.transform.up*1.4f;
                var rt=new RenderTexture(700,700,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;board.ReadPixels(new Rect(0,0,700,700),i*700,0);RenderTexture.active=null;cam.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(label);Object.DestroyImmediate(model);
            }
            board.Apply();File.WriteAllBytes(Path.Combine(output,"TopDownGunComparison.png"),board.EncodeToPNG());Object.DestroyImmediate(board);
        }
        [MenuItem("Naval/Art/Preview Weapon Upgrade Concepts")]
        public static void RunBatch()
        {RenderConcepts(false);}
        public static void RunTorpedoAlternative()
        {RenderConcepts(true);}
        static void RenderConcepts(bool alternative)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);NavalEditorUtil.EnsureFolder(Folder);NavalEditorUtil.EnsureFolder(Folder+"/Previews");
            armor=Mat("Naval grey",new Color(.38f,.46f,.51f));edge=Mat("Edge grey",new Color(.52f,.59f,.61f));metal=Mat("Gunmetal",new Color(.19f,.25f,.29f));dark=Mat("Recess",new Color(.035f,.055f,.065f));glass=Mat("Sensor teal",new Color(.13f,.49f,.53f));accent=Mat("Safety amber",new Color(.82f,.53f,.19f));
            var light=new GameObject("Studio key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;light.transform.rotation=Quaternion.Euler(40,-35,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.6f,.67f,.72f);
            var cam=new GameObject("Concept camera").AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=1.75f;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.035f,.065f,.09f);
            string output=Path.GetFullPath(Folder+"/Previews");Directory.CreateDirectory(output);
            string[] names=alternative?new[]{"TorpedoTube"}:new[]{"Nobong","Howitzer","RamBow","TorpedoTube"};
            foreach(string name in names)
            {
                var board=new Texture2D(alternative?700:2100,700,TextureFormat.RGB24,false);
                for(int level=alternative?2:0;level<3;level++)
                {
                    prefix=name+"_T"+level+(alternative?"_OpenService":"");serial=0;var root=new GameObject(prefix);
                    if(name=="Nobong"||name=="Howitzer")Gun(root.transform,level,name=="Howitzer");else if(name=="RamBow")Ram(root.transform,level);else Torpedo(root.transform,level,level==2 || alternative);
                    CombineModel(root.transform);ValidateModel(root,prefix);
                    PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+prefix+".prefab");
                    var focus=new Vector3(0,.7f,.25f);cam.transform.position=focus+new Vector3(3.5f,2.8f,4.5f);cam.transform.LookAt(focus);
                    var label=new GameObject("Stage label");var tm=label.AddComponent<TextMesh>();tm.text=alternative?"OPEN SERVICE / T2":level==0?"BASE":"UPGRADE "+level;tm.fontSize=48;tm.characterSize=alternative?.05f:.07f;tm.anchor=TextAnchor.MiddleCenter;tm.color=new Color(.7f,.83f,.87f);label.transform.rotation=cam.transform.rotation;label.transform.position=focus-cam.transform.up*1.45f;
                    var rt=new RenderTexture(700,700,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;board.ReadPixels(new Rect(0,0,700,700),alternative?0:level*700,0);RenderTexture.active=null;cam.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(root);Object.DestroyImmediate(label);
                }
                board.Apply();File.WriteAllBytes(Path.Combine(output,name+(alternative?"_open_service.png":"_stages.png")),board.EncodeToPNG());Object.DestroyImmediate(board);
                if(!alternative)SaveVisualPack(name);
            }
            if(!alternative)CaptureTopDown(cam,output);
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();Debug.Log(alternative?"[WeaponConcepts] PASS: one open-service torpedo alternative. No gameplay assets changed.":"[WeaponConcepts] PASS: four upgrade visual packs, 12 art-only prefabs, stage boards and top-down comparison.");
        }
    }
}

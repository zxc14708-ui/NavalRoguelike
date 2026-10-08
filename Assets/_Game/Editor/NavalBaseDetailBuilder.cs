using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    // Local dimensions follow NavalBaseBuilder's unscaled metre convention.
    public static class NavalBaseDetailBuilder
    {
        const string Folder="Assets/_Game/Art/NavalBaseDetails";
        public static void BuildAndCaptureBatch(){NavalBaseMenuBuilder.RunBatch();CaptureBatch();}
        static Material wall, roof, glass, steel, dark, yellow, red;
        static Transform Group(Transform p,string n,Vector3 at=default)
        { var t=new GameObject(n).transform;t.SetParent(p,false);t.localPosition=at;return t; }
        static Transform Part(Transform p,string n,Vector3 at,Vector3 size,Material m,PrimitiveType shape=PrimitiveType.Cube)
        {var g=GameObject.CreatePrimitive(shape);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=at;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;Object.DestroyImmediate(g.GetComponent<Collider>());return g.transform;}
        static Material Mat(string n,Color color)
        {string path=Folder+"/"+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.color=color;return m;}
        static void Beam(Transform p,Vector3 a,Vector3 b,float width,Material m)
        {var t=Part(p,"Structural member",(a+b)/2,new Vector3(width,(b-a).magnitude,width),m);t.rotation=p.rotation*Quaternion.FromToRotation(Vector3.up,b-a);}
        static void Label(Transform p,string text,Vector3 at,float size)
        {var t=Group(p,text,at);var label=t.gameObject.AddComponent<TextMesh>();label.text=text;label.fontSize=48;label.characterSize=size;label.anchor=TextAnchor.MiddleCenter;label.color=Color.white;}
        static void Clear(Transform t){foreach(Transform c in t.Cast<Transform>().ToArray())Object.DestroyImmediate(c.gameObject);}
        public static void Apply(Transform island)
        {
            NavalEditorUtil.EnsureFolder(Folder);
            wall=Mat("Concrete",new Color(.59f,.65f,.65f));roof=Mat("Roof",new Color(.22f,.31f,.37f));glass=Mat("Glazing",new Color(.08f,.27f,.32f));steel=Mat("Metal",new Color(.72f,.76f,.73f));dark=Mat("Rubber",new Color(.075f,.10f,.11f));yellow=Mat("Safety",new Color(.93f,.63f,.14f));red=Mat("Fire",new Color(.66f,.17f,.12f));
            var all=island.GetComponentsInChildren<Transform>().ToArray();int h=0,b=0;
            foreach(var t in all)
            {
                if(t==null)continue;
                if(t.name=="Hangar"){Clear(t);Hangar(t,++h);Save(t,"Hangar_"+h);}
                else if(t.name=="Headquarters"){Clear(t);Office(t,true,0);Save(t,"Headquarters");}
                else if(t.name=="Barracks"){Clear(t);Office(t,false,++b);if(b==1)Save(t,"Barracks");}
                else if(t.name=="RadarDome"){Clear(t);Radar(t);Save(t,"RadarStation");}
            }
            var harbor=island.Find("Harbor");
            var old=harbor.Find("DetailedSupportMarina");if(old!=null)Object.DestroyImmediate(old.gameObject);
            var marina=Group(harbor,"DetailedSupportMarina");
            // Separate from the four selectable ship berths and their camera corridor.
            Part(marina,"Service quay",new Vector3(270,1,582),new Vector3(260,3,24),wall);
            for(int i=0;i<3;i++)
            {
                float x=185+i*80;
                Part(marina,"Service finger",new Vector3(x,1,630),new Vector3(9,3,90),wall);
                for(int j=0;j<4;j++)Part(marina,"Mooring bollard",new Vector3(x,3,597+j*22),new Vector3(2,2,2),dark,PrimitiveType.Cylinder);
                var boat=Group(marina,new[]{"Harbor tug","Utility workboat","Fire rescue boat"}[i],new Vector3(x+23,0,633));
                Boat(boat,i);Save(boat,new[]{"HarborTug","UtilityWorkboat","FireRescueBoat"}[i]);
            }
            var patrol=Resources.Load<GameObject>("TaskForce/Escorts/ESC_PB_T0");
            if(patrol!=null)
            {
                var p=Object.Instantiate(patrol,marina).transform;p.name="Service patrol boat";
                foreach(var c in p.GetComponentsInChildren<MonoBehaviour>())Object.DestroyImmediate(c);
                var rs=p.GetComponentsInChildren<Renderer>();var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
                p.localScale*=32/(bounds.size.z/island.lossyScale.z);
                p.localPosition=new Vector3(385,0,626);
            }
            Debug.Log($"[HarborDetails] Detailed {h} hangars, {b} barracks, headquarters, radar and four support boats.");
        }
        static void Save(Transform t,string name)
        {
            var copy=Object.Instantiate(t.gameObject);copy.name=name;copy.transform.SetParent(null);copy.transform.position=Vector3.zero;copy.transform.rotation=Quaternion.identity;copy.transform.localScale=Vector3.one;
            PrefabUtility.SaveAsPrefabAsset(copy,Folder+"/"+name+".prefab");Object.DestroyImmediate(copy);
        }
        public static void CaptureBatch()
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
            var light=new GameObject("Preview daylight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(45,-35,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.6f,.65f,.7f);
            string output=System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),"Codex/2026-09-29/referenced-chatgpt-conversation-this-is-an/outputs/harbor_details");System.IO.Directory.CreateDirectory(output);
            var cam=new GameObject("Asset camera").AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.035f,.075f,.10f);cam.fieldOfView=32;
            foreach(string name in new[]{"Hangar_1","Headquarters","Barracks","RadarStation","HarborTug","UtilityWorkboat","FireRescueBoat"})
            {
                var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+name+".prefab"));var rs=go.GetComponentsInChildren<Renderer>();var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
                cam.transform.position=bounds.center+new Vector3(.85f,.75f,-1.2f).normalized*bounds.size.magnitude*1.65f;cam.transform.LookAt(bounds.center);
                var rt=new RenderTexture(1200,900,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var tex=new Texture2D(1200,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1200,900),0,0);tex.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,name+".png"),tex.EncodeToPNG());RenderTexture.active=null;cam.targetTexture=null;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(go);
            }
            Debug.Log("[HarborDetails] Captured seven detailed asset previews.");
        }
        static void Hangar(Transform t,int index)
        {
            Part(t,"Slab",new Vector3(0,4.3f,0),new Vector3(74,1,54),wall);
            foreach(float x in new[]{-35f,35f})Part(t,"Side wall",new Vector3(x,14,0),new Vector3(2,20,50),wall);
            Part(t,"Rear wall",new Vector3(0,14,25),new Vector3(70,20,2),wall);
            Part(t,"Dark interior",new Vector3(0,13,23.8f),new Vector3(66,17,.2f),dark);
            foreach(float x in new[]{-18f,18f}){var r=Part(t,"Pitched roof",new Vector3(x,27,0),new Vector3(38,1.2f,54),roof);r.localRotation=Quaternion.Euler(0,0,x<0?10:-10);}
            for(int i=-4;i<=4;i++)
            {
                float x=i*8;
                Beam(t,new Vector3(x,4,-25),new Vector3(x,23,-25),.65f,steel);
                if(i!=0&&i!=1)Part(t,"Sliding door",new Vector3(x,13,-25.7f),new Vector3(7.5f,18,.6f),roof);
                if(i!=0&&i!=1)for(int y=7;y<22;y+=4)Part(t,"Door glazing",new Vector3(x,y,-26.1f),new Vector3(6,1.3f,.2f),glass);
            }
            for(int z=-20;z<=20;z+=10)
            {Beam(t,new Vector3(-35,24,z),new Vector3(0,30,z),.7f,steel);Beam(t,new Vector3(0,30,z),new Vector3(35,24,z),.7f,steel);}
            Part(t,"Door track",new Vector3(0,23,-26),new Vector3(74,1,1.5f),steel);
            Part(t,"Number board",new Vector3(0,27,-27),new Vector3(18,4,.5f),roof);Label(t,"H-0"+index,new Vector3(0,27,-27.4f),.65f);
            foreach(float x in new[]{-25f,25f}){var sky=Part(t,"Roof skylight",new Vector3(x,26.7f,0),new Vector3(4,.5f,36),glass);sky.localRotation=Quaternion.Euler(0,0,x<0?10:-10);}
            Part(t,"Personnel door",new Vector3(35.9f,6.5f,-15),new Vector3(.3f,5,3),glass);
            for(int i=0;i<3;i++)Part(t,"Roof exhaust",new Vector3(-12+i*12,30,16),new Vector3(3,2,3),steel,PrimitiveType.Cylinder);
        }
        static void Office(Transform t,bool command,int id)
        {
            float w=command?60:52,d=command?26:16,height=command?18:15;
            Part(t,"Foundation",new Vector3(0,4.4f,0),new Vector3(w+2,1,d+2),roof);
            Part(t,"Facade",new Vector3(0,4+height/2,0),new Vector3(w,height,d),wall);
            for(int floor=0;floor<3;floor++)foreach(float side in new[]{-1f,1f})for(int x=0;x<8;x++)
            {Part(t,"Window frame",new Vector3(-w/2+4+x*(w-8)/7,7+floor*4.5f,side*(d/2+.15f)),new Vector3(4,2.8f,.35f),steel);Part(t,"Window",new Vector3(-w/2+4+x*(w-8)/7,7+floor*4.5f,side*(d/2+.36f)),new Vector3(3.4f,2.2f,.15f),glass);}
            Part(t,"Roof slab",new Vector3(0,4+height,0),new Vector3(w+2,1,d+2),roof);
            foreach(float z in new[]{-d/2,d/2})Part(t,"Parapet",new Vector3(0,5+height,z),new Vector3(w,1.5f,.6f),steel);
            Part(t,"Glass lobby",new Vector3(0,7,-d/2-.5f),new Vector3(8,6,1),glass);
            Part(t,"Entrance canopy",new Vector3(0,10.5f,-d/2-4),new Vector3(14,.7f,8),roof);
            foreach(float x in new[]{-6f,6f})Part(t,"Canopy pillar",new Vector3(x,7,-d/2-7),new Vector3(.7f,6,.7f),steel);
            for(int i=0;i<3;i++)Part(t,"Entry step",new Vector3(0,4.2f+i*.25f,-d/2-8+i),new Vector3(14,.5f,3),wall);
            for(int i=0;i<3;i++){Part(t,"HVAC housing",new Vector3(-16+i*14,6+height,2),new Vector3(7,3,5),steel);Part(t,"Vent fan",new Vector3(-16+i*14,7.7f+height,2),new Vector3(3,.2f,3),dark,PrimitiveType.Cylinder);}
            Label(t,command?"NAVAL COMMAND":"QUARTERS 0"+id,new Vector3(0,command?20:17,-d/2-.4f),command?.42f:.32f);
            if(command){Part(t,"Communications mast",new Vector3(20,31,6),new Vector3(.7f,18,.7f),steel);Part(t,"Antenna crossarm",new Vector3(20,37,6),new Vector3(8,.5f,.5f),steel);Part(t,"SATCOM plinth",new Vector3(-23,23,5),new Vector3(4,2,4),steel);Part(t,"SATCOM",new Vector3(-23,27,5),new Vector3(6,6,6),steel,PrimitiveType.Sphere);}
            else for(int i=0;i<4;i++){Part(t,"Fire stair",new Vector3(w/2+2,5+i*3,4-i*3),new Vector3(4,1,4),steel);}
        }
        static void Radar(Transform t)
        {
            Part(t,"Operations shelter",new Vector3(-12,5,0),new Vector3(25,10,23),wall);
            Part(t,"Equipment door",new Vector3(-12,3,-11.7f),new Vector3(4,6,.4f),glass);
            Part(t,"Shelter roof",new Vector3(-12,10.5f,0),new Vector3(27,1,25),roof);
            for(int i=0;i<4;i++)Part(t,"Cooling louvre",new Vector3(-20+i*5,6,-11.7f),new Vector3(3,2,.4f),dark);
            foreach(float x in new[]{-8f,8f})foreach(float z in new[]{-8f,8f})Beam(t,new Vector3(16+x,0,z),new Vector3(16+x*.5f,30,z*.5f),1,steel);
            for(int y=0;y<30;y+=10)foreach(float z in new[]{-6f,6f}){Beam(t,new Vector3(10,y,z),new Vector3(22,y+10,z),.6f,steel);Beam(t,new Vector3(22,y,z),new Vector3(10,y+10,z),.6f,steel);}
            Part(t,"Radar platform",new Vector3(16,30,0),new Vector3(20,1,20),roof);
            Part(t,"Radome",new Vector3(16,40,0),new Vector3(19,19,19),steel,PrimitiveType.Sphere);
            Part(t,"Generator",new Vector3(-12,2,20),new Vector3(12,4,6),roof);
            Part(t,"SATCOM pedestal",new Vector3(-12,13,0),new Vector3(2,4,2),steel);
            var dish=Part(t,"Satellite dish",new Vector3(-12,16,0),new Vector3(9,1.5f,9),steel,PrimitiveType.Sphere);dish.localRotation=Quaternion.Euler(35,0,0);
        }
        static void Boat(Transform t,int kind)
        {
            // Faceted, tapered bow instead of a rectangular block hull.
            var vertices=new[]{new Vector3(-7,-1,-20),new Vector3(7,-1,-20),new Vector3(7,-1,10),new Vector3(0,-1,22),new Vector3(-7,-1,10),new Vector3(-8,3,-20),new Vector3(8,3,-20),new Vector3(8,3,10),new Vector3(0,3,22),new Vector3(-8,3,10)};
            var tris=new System.Collections.Generic.List<int>();for(int i=0;i<5;i++){int j=(i+1)%5;tris.AddRange(new[]{i,j+5,j,i,i+5,j+5});}for(int i=1;i<4;i++)tris.AddRange(new[]{5,5+i+1,5+i});
            string path=Folder+"/SupportHull.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}mesh.vertices=vertices;mesh.triangles=tris.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            var hull=Group(t,"Shaped hull");hull.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;hull.gameObject.AddComponent<MeshRenderer>().sharedMaterial=roof;
            Part(t,"Wheelhouse",new Vector3(0,7,3),new Vector3(10,8,11),kind==2?red:wall);
            Part(t,"Bridge roof",new Vector3(0,11.5f,3),new Vector3(12,1,13),steel);
            Part(t,"Aft access door",new Vector3(0,6,-2.6f),new Vector3(2.5f,5,.25f),roof);
            Part(t,"Door glass",new Vector3(0,7,-2.8f),new Vector3(1.7f,1.5f,.15f),glass);
            foreach(float x in new[]{-3f,3f})Part(t,"Exhaust stack",new Vector3(x,12,-1),new Vector3(1.2f,4,1.2f),dark,PrimitiveType.Cylinder);
            for(int i=-1;i<=1;i++)Part(t,"Bridge window",new Vector3(i*3,9,8.6f),new Vector3(2.5f,2,.3f),glass);
            foreach(float x in new[]{-5.1f,5.1f})Part(t,"Side glazing",new Vector3(x,9,3),new Vector3(.3f,2,8),glass);
            Part(t,"Mast",new Vector3(0,16,3),new Vector3(.5f,9,.5f),steel);Part(t,"Radar bar",new Vector3(0,20,3),new Vector3(5,.6f,1),glass);
            foreach(float x in new[]{-8f,8f})for(int z=-16;z<12;z+=6){Part(t,"Rubber fender",new Vector3(x,2,z),new Vector3(2,3,3),dark,PrimitiveType.Sphere);Part(t,"Rail stanchion",new Vector3(x*.9f,4.5f,z),new Vector3(.3f,3,.3f),steel);}
            foreach(float x in new[]{-7.2f,7.2f})Part(t,"Handrail",new Vector3(x,6,-3),new Vector3(.3f,.3f,27),steel);
            if(kind==0){var winch=Part(t,"Tow winch drum",new Vector3(0,5,-12),new Vector3(5,4,5),dark,PrimitiveType.Cylinder);winch.localRotation=Quaternion.Euler(0,0,90);foreach(float x in new[]{-3f,3f})Part(t,"Tow bitt",new Vector3(x,5,15),new Vector3(1,4,1),steel);Part(t,"Tow crossbar",new Vector3(0,6,15),new Vector3(8,1,1),steel);}
            if(kind==1){Part(t,"Cargo pallet",new Vector3(0,4,-12),new Vector3(10,2,8),yellow);Beam(t,new Vector3(5,3,-6),new Vector3(5,15,-6),1.5f,yellow);Beam(t,new Vector3(5,15,-6),new Vector3(-3,17,-12),1,yellow);Beam(t,new Vector3(-3,17,-12),new Vector3(-3,9,-12),.2f,dark);}
            if(kind==2)foreach(float z in new[]{-12f,14f}){Part(t,"Monitor base",new Vector3(0,4,z),new Vector3(3,3,3),red,PrimitiveType.Cylinder);Beam(t,new Vector3(0,6,z),new Vector3(0,8,z+4),1,steel);}
        }
    }
}

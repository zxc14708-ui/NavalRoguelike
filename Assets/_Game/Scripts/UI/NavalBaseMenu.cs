using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Game.Data;

namespace Game.UI
{
    /// <summary>Isolated live harbor backdrop. All motion uses unscaled time during the launch menu.</summary>
    public sealed class NavalBaseMenu : MonoBehaviour
    {
        public static NavalBaseMenu Active { get; private set; }
        public int SelectedIndex { get; private set; } = -1;
        public int ShipCount => _ships.Count;
        public int TrafficCount => _traffic.Count;
        public Camera ViewCamera => _camera;
        // 개관 시점은 섬을 계속 돌므로 따라가는 지연만큼 여유를 둔다
        public bool CameraSettled => (_camera.transform.position-_destination).sqrMagnitude < (SelectedIndex<0 ? 16f : .02f);
        const int Layer = 28;
        Transform _stage, _island;
        Camera _camera;
        RenderTexture _texture;
        RawImage _image;
        Vector3 _destination, _lookAt, _currentLook;
        float _fov=25, _clock, _orbit;
        readonly List<Transform> _ships = new();
        readonly List<Vector3> _centers = new();
        readonly List<Light> _mutedLights = new();
        readonly List<Traffic> _traffic = new();
        AmbientMode _ambientMode;
        Color _ambient;
        bool _lightingSaved;
        float _shadowDistance = -1f;
        int _shadowResolution = -1;
        sealed class Traffic { public Transform Item; public Vector3[] Points; public float[] Times; public float Offset, Total; }
        // 개관 시점: 섬 중심을 수평 반경 2375m·높이 1650m(섬 좌표, m)로 한 바퀴 3분
        static readonly Vector3 OrbitCenter=new(20,0,60);
        const float OrbitRadius=2375, OrbitHeight=1650, OrbitStart=-35.2f, OrbitDegreesPerSecond=2;
        // 도로 순환 경로(섬 좌표, 도로 중심선). 닫힌 사각 순환 넷 + 왕복 둘. 차는 진행 방향 오른쪽 차로로 달린다
        static readonly Vector2[][] Routes={
            new Vector2[]{new(-80,455),new(470,455),new(470,110),new(-80,110)},
            new Vector2[]{new(-720,455),new(-400,455),new(-400,110),new(-720,110)},
            new Vector2[]{new(-80,110),new(760,110),new(760,-50),new(-80,-50)},
            new Vector2[]{new(-400,455),new(-80,455),new(-80,110),new(-400,110)},
            new Vector2[]{new(-800,-50),new(-80,-50),new(-80,110),new(-860,110),new(-80,110),new(-80,-50)},
            new Vector2[]{new(-860,110),new(860,110)}};

        public static NavalBaseMenu Create(Transform parent)
        {
            var prefab=Resources.Load<GameObject>("Environment/NavalBaseMenu");
            if(prefab==null) { Debug.LogError("Missing Environment/NavalBaseMenu prefab. Run Naval/Art/Prepare Harbor Menu."); return null; }
            var go=new GameObject("Live naval base",typeof(RectTransform),typeof(RawImage));
            go.transform.SetParent(parent,false); go.transform.SetAsFirstSibling();
            var rect=(RectTransform)go.transform; rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one;
            rect.offsetMin=rect.offsetMax=Vector2.zero;
            var view=go.AddComponent<NavalBaseMenu>(); Active=view;
            view.Initialize(prefab,go.GetComponent<RawImage>()); return view;
        }

        void Initialize(GameObject prefab,RawImage image)
        {
            _stage=new GameObject("Harbor menu stage").transform; _stage.position=new Vector3(10000,-2000,10000);
            _island=Instantiate(prefab,_stage).transform;
            _island.localPosition=new Vector3(0,-.9f,0);
            _camera=new GameObject("Harbor menu camera",typeof(Camera)).GetComponent<Camera>();
            _camera.transform.SetParent(_stage,false); _camera.cullingMask=1<<Layer;
            _camera.clearFlags=CameraClearFlags.SolidColor; _camera.backgroundColor=new Color(.42f,.65f,.77f);
            _camera.nearClipPlane=.1f; _camera.farClipPlane=1200; _camera.allowHDR=false;
            _image=image; image.raycastTarget=false; EnsureTexture();
            foreach(var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if(l.enabled) { _mutedLights.Add(l); l.enabled=false; }
            _ambientMode=RenderSettings.ambientMode; _ambient=RenderSettings.ambientLight; _lightingSaved=true;
            RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.63f,.71f,.77f);
            var sun=new GameObject("Harbor daylight",typeof(Light)).GetComponent<Light>();
            sun.transform.SetParent(_stage,false); sun.transform.rotation=Quaternion.Euler(40,150,0);
            sun.type=LightType.Directional; sun.intensity=1.15f; sun.color=new Color(1,.97f,.92f);
            sun.cullingMask=1<<Layer; sun.shadows=LightShadows.Soft; sun.shadowStrength=1f;
            // 섬 전체 시점은 기본 그림자 거리 밖이라 그림자가 없어 건물이 납작해 보인다 — 시작 화면 동안만 늘린다
            // 넓힌 거리만큼 그림자맵도 키워야 그림자 가장자리가 계단처럼 깨지지 않는다
            if(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                _shadowDistance=urp.shadowDistance; urp.shadowDistance=Mathf.Max(urp.shadowDistance,320f);
                _shadowResolution=urp.mainLightShadowmapResolution; urp.mainLightShadowmapResolution=Mathf.Max(_shadowResolution,4096);
            }
            BuildBerths(); BuildTraffic();
            foreach(var t in _island.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=Layer;
            ShowOverview(); _camera.transform.position=_destination; _currentLook=_lookAt;
            _camera.transform.LookAt(_currentLook); _camera.fieldOfView=_fov;
        }

        void BuildBerths()
        {
            var harbor=_island.Find("Harbor");
            var old=harbor.Find("Berths"); if(old!=null) old.gameObject.SetActive(false);
            // Four consecutive berths along the quay. Older backdrops had three piers — add the fourth only then.
            Transform last=null; int piers=0;
            foreach(Transform t in harbor) if(t.name=="Pier") { last=t; piers++; }
            if(last!=null && piers<4) { var fourth=Instantiate(last,harbor); fourth.name="Pier 4"; fourth.localPosition+=Vector3.right*220; }
            int i=0;
            foreach(var concept in StartingShipCatalog.All)
            {
                if(concept==null || concept.StartPreviewPrefab==null) continue;
                var ship=Instantiate(concept.StartPreviewPrefab,_stage).transform;
                ship.name="Moored "+concept.Title;
                var escorts=ship.Find("EscortFormation_VisualOnly"); if(escorts!=null) escorts.gameObject.SetActive(false);
                var bounds=BoundsOf(ship.gameObject);
                ship.localScale*=9.6f/Mathf.Max(1,bounds.size.z);
                bounds=BoundsOf(ship.gameObject);
                Vector3 target=_island.TransformPoint(new Vector3(-578+220*i,0,632));
                ship.position+=target-bounds.center;
                bounds=BoundsOf(ship.gameObject); ship.position+=Vector3.up*(_island.position.y-.28f-bounds.min.y);
                foreach(var t in ship.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=Layer;
                _ships.Add(ship); _centers.Add(BoundsOf(ship.gameObject).center);
                i++;
            }
        }

        void BuildTraffic()
        {
            var air=_island.Find("NavalAirStation");
            var fighter=air.Find("Fighter1");
            if(fighter!=null)
                _traffic.Add(new Traffic { Item=fighter, Offset=5,
                    // 계류장 → 평행 유도로(z -262) → 서쪽 끝 → 활주로(z -330) 이륙 → 선회 → 착륙 → 유도로 → 계류장
                    Points=new[]{new Vector3(60,4.6f,-185),new Vector3(60,4.6f,-262),new Vector3(-740,4.6f,-262),new Vector3(-760,4.6f,-330),new Vector3(400,4.6f,-330),new Vector3(1250,160,-330),new Vector3(1550,220,-1000),new Vector3(-1400,220,-1000),new Vector3(-1250,120,-330),new Vector3(-500,4.6f,-330),new Vector3(330,4.6f,-330),new Vector3(330,4.6f,-262),new Vector3(60,4.6f,-262)},
                    Times=new[]{6f,14f,4f,9f,7f,6f,12f,6f,9f,8f,4f,5f,5f} });
            var harbor=_island.Find("Harbor");
            foreach(Transform t in harbor)
                if(t.name.StartsWith("ESC_PB"))
                {
                    float y=t.localPosition.y;
                    _traffic.Add(new Traffic {Item=t,Offset=0,
                        Points=new[]{new Vector3(-60,y,760),new Vector3(200,y,850),new Vector3(250,y,1200),new Vector3(-900,y,1200),new Vector3(-900,y,850),new Vector3(-60,y,760)},
                        Times=new[]{16f,15f,30f,15f,24f,8f}});
                    break;
                }
            BuildRoadTraffic();
        }

        void BuildRoadTraffic()
        {
            var group=_island.Find("RoadTraffic"); if(group==null) return;
            const float lane=3.5f, roadY=4.4f;
            for(int i=0;i<group.childCount;i++)
            {
                var car=group.GetChild(i);
                int k=i/Routes.Length;
                var route=(Vector2[])Routes[i%Routes.Length].Clone();
                if(k%2==1) System.Array.Reverse(route);
                // 같은 경로의 홀수 번째 차는 반대 방향(반대 차로). 왕복 경로는 끝점에서 그대로 되돌아온다
                int n=route.Length; var points=new Vector3[n]; var times=new float[n];
                bool heavy=car.name is "Bus" or "Truck" or "MilitaryTruck" or "FuelTruck" or "TrailerTruck";
                float speed=heavy ? 11f : 15f;   // m/s
                for(int j=0;j<n;j++)
                {
                    Vector2 prev=route[(j+n-1)%n], here=route[j], next=route[(j+1)%n];
                    Vector2 n1=Right(here-prev), n2=Right(next-here);
                    float d=1+Vector2.Dot(n1,n2);
                    Vector2 off=d>.1f ? (n1+n2)/d*lane : Vector2.zero;   // 직각 모퉁이는 대각 이동, 되돌아가는 끝은 중심선
                    points[j]=new Vector3(here.x+off.x,roadY,here.y+off.y);
                    times[j]=Vector2.Distance(here,next)/speed;
                }
                var t=new Traffic{Item=car,Points=points,Times=times};
                foreach(float x in times) t.Total+=x;
                t.Offset=t.Total*((k*.618f+i*.13f)%1f);
                _traffic.Add(t);
            }
        }
        static Vector2 Right(Vector2 d) { d.Normalize(); return new Vector2(d.y,-d.x); }

        public void ShowOverview()
        {
            SelectedIndex=-1;
            _destination=OrbitPoint(); _lookAt=_island.TransformPoint(OrbitCenter); _fov=25;
        }
        public void FocusShip(int index)
        {
            if(_ships.Count==0)return;
            SelectedIndex=Mathf.Clamp(index,0,_ships.Count-1);
            _lookAt=_centers[SelectedIndex]+Vector3.up*1.4f;
            _destination=_lookAt+new Vector3(0,20,18); _fov=38;
        }
        Vector3 OrbitPoint()
        {
            float a=(OrbitStart+_orbit)*Mathf.Deg2Rad;
            return _island.TransformPoint(OrbitCenter+new Vector3(Mathf.Sin(a)*OrbitRadius,OrbitHeight,Mathf.Cos(a)*OrbitRadius));
        }
        public void RenderNow() { if(_camera!=null) _camera.Render(); }
        /// <summary>
        /// 렌더 텍스처: 세로 최소 1440(창이 작으면 크게 그려 줄여 붙이는 초과 표본화), 최대 2160, 화면비는 창을 따른다.
        /// MSAA(1440 이하 8배, 그 위 4배)에 밉맵을 켜서 줄여 붙일 때 가장자리가 계단처럼 깨지지 않게 한다.
        /// </summary>
        void EnsureTexture()
        {
            int sw=Mathf.Max(Screen.width,1), sh=Mathf.Max(Screen.height,1);
            int h=Mathf.Clamp(sh,1440,2160), w=Mathf.RoundToInt(sw*(float)h/sh);
            if(_texture!=null && _texture.width==w && _texture.height==h) return;
            if(_texture!=null){_camera.targetTexture=null;_texture.Release();Destroy(_texture);}
            _texture=new RenderTexture(w,h,24){name="Live harbor",antiAliasing=h>1440?4:8,useMipMap=true,autoGenerateMips=true,
                filterMode=FilterMode.Trilinear,anisoLevel=4};
            _texture.Create();
            _camera.targetTexture=_texture; _image.texture=_texture;
        }
        void LateUpdate()
        {
            if(_camera==null)return;
            float dt=Time.unscaledDeltaTime; _clock+=dt;
            foreach(var traffic in _traffic)
            {
                if(traffic.Total<=0) foreach(float duration in traffic.Times)traffic.Total+=duration;
                float time=(_clock+traffic.Offset)%traffic.Total; int segment=0;
                while(time>=traffic.Times[segment] && segment<traffic.Times.Length-1) {time-=traffic.Times[segment];segment++;}
                var a=traffic.Points[segment]; var b=traffic.Points[(segment+1)%traffic.Points.Length];
                traffic.Item.localPosition=Vector3.Lerp(a,b,time/traffic.Times[segment]);
                if((b-a).sqrMagnitude>.01f)traffic.Item.localRotation=Quaternion.Slerp(traffic.Item.localRotation,Quaternion.LookRotation(b-a),1-Mathf.Exp(-dt*2));
            }
            if(SelectedIndex<0) { _orbit=(_orbit+dt*OrbitDegreesPerSecond)%360f; _destination=OrbitPoint(); }
            EnsureTexture();
            float blend=1-Mathf.Exp(-dt*3.2f);
            _camera.transform.position=Vector3.Lerp(_camera.transform.position,_destination,blend);
            _currentLook=Vector3.Lerp(_currentLook,_lookAt,blend); _camera.transform.LookAt(_currentLook);
            // 근평면이 0.1이면 멀리 본 섬에서 깊이 정밀도가 모자라 겹친 면(잔디 조각·도로)이 깨진다 — 보는 거리에 맞춰 민다
            _camera.nearClipPlane=Mathf.Clamp(Vector3.Distance(_camera.transform.position,_currentLook)*.08f,.1f,20f);
            _camera.fieldOfView=Mathf.Lerp(_camera.fieldOfView,_fov,blend);
        }
        static Bounds BoundsOf(GameObject go)
        {
            var rs=go.GetComponentsInChildren<Renderer>(); var b=rs[0].bounds;
            for(int i=1;i<rs.Length;i++)b.Encapsulate(rs[i].bounds); return b;
        }
        void OnDisable()
        {
            if(_stage!=null)_stage.gameObject.SetActive(false);
            foreach(var l in _mutedLights)if(l!=null)l.enabled=true;
            if(_lightingSaved) {RenderSettings.ambientMode=_ambientMode;RenderSettings.ambientLight=_ambient;_lightingSaved=false;}
            if(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                if(_shadowDistance>=0f){urp.shadowDistance=_shadowDistance;_shadowDistance=-1f;}
                if(_shadowResolution>0){urp.mainLightShadowmapResolution=_shadowResolution;_shadowResolution=-1;}
            }
        }
        void OnDestroy()
        {
            if(Active==this)Active=null;
            if(_stage!=null)Destroy(_stage.gameObject);
            if(_texture!=null){_texture.Release();Destroy(_texture);}
        }
    }
}

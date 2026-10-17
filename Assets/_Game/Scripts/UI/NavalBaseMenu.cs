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
        int _shadowResolution = -1, _pipelineMsaa = -1;

        /// <summary>
        /// 그리기 줄이기(2026-10-13, 점검 O04 측정 뒤): 시작 화면이 화면 전체를 덮는 동안 뒤의 전투 카메라는 아무것도 그리지 않게
        /// (끄면 Camera.main이 비어 다른 코드가 깨지므로 그릴 층만 비운다 — 나갈 때 되돌림), 작은 물체(난간·창·차량 등
        /// 경계 대각선 SmallCaster 미만)는 그림자를 그리지 않는다(개관 시점에서 안 보이는데 그림자 단계마다 한 번씩 그려졌다).
        /// </summary>
        public const float SmallCaster = 0.6f;
        readonly List<(Camera camera, int mask, bool shadows, bool post)> _mutedCameras = new();
        /// <summary>검증용: 가린 전투 카메라 수 · 그림자를 끈 작은 물체 수.</summary>
        public int MutedCameraCount => _mutedCameras.Count;
        public int SmallCastersOff { get; private set; }
        /// <summary>
        /// 계단 현상 설정(2026-10-11). 렌더 텍스처 세로 = 화면 세로 × Supersample(최소 1440, 최대 MaxRenderHeight),
        /// MSAA 표본 수, 카메라 후처리 안티앨리어싱. URP 설정의 MSAA가 꺼져 있으면(1) 렌더 텍스처의 MSAA도 무시되므로
        /// 시작 화면 동안만 URP MSAA를 켠다(나갈 때 되돌림). 검사(-harborAAOnly)가 바꿔 가며 비교한다.
        /// </summary>
        public static float Supersample = 1.5f;
        public static int Msaa = 8;
        public static int MaxRenderHeight = 2160;
        public static int MinRenderHeight = 1440;
        public static int ShadowResolution = 4096;

        /// <summary>
        /// 항구 화질 단계(2026-10-11 점검 G05, 설정 › 그래픽): 높음 = 지금(1.5배 · MSAA 8 · 1440~2160 · 그림자 4096),
        /// 보통 = 1배 · MSAA 4 · 1080~1440 · 2048, 낮음 = 1배 · MSAA 2 · 720~1080 · 1024.
        /// </summary>
        public static void ApplyQualityPreset(int quality)
        {
            (Supersample, Msaa, MinRenderHeight, MaxRenderHeight, ShadowResolution) = quality switch
            {
                0 => (1.5f, 8, 1440, 2160, 4096),
                1 => (1f, 4, 1080, 1440, 2048),
                _ => (1f, 2, 720, 1080, 1024),
            };
            if (Active != null) Active.ApplyShadowResolution();
        }

        private void ApplyShadowResolution()
        {
            if (_shadowResolution > 0 && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                urp.mainLightShadowmapResolution = Mathf.Max(_shadowResolution, ShadowResolution);
            _dirty = true;
        }

        private bool _dirty = true;
        private Vector3 _lastCameraPosition;
        private float _lastFov;
        private int _appliedQuality = -1;
        public static AntialiasingMode PostAntialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        /// <summary>검사용: 시계·개관 회전을 멈춘다(같은 화면을 설정만 바꿔 찍는다).</summary>
        public bool Paused { get; set; }
        sealed class Traffic { public Transform Item; public Vector3[] Points; public float[] Times; public float Offset, Total; }
        // 활주로 진입: 서쪽 끝(−760) 대신 −380 연결 유도로로 들어가 그 자리에서 이륙한다(2026-10-11) —
        // 평행 유도로 x −470에 서 있는 초계기(P3C_Taxi, 합친 정적 메시라 움직일 수 없다)를 뚫고 지나가지 않게.
        // 착륙 뒤에는 x 330 연결 유도로로 빠져 계류장으로 돌아가므로 그 초계기를 지나지 않는다.
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
            _camera.nearClipPlane=.1f; _camera.farClipPlane=1200; _camera.allowHDR=false; _camera.allowMSAA=true;
            ApplyAntialiasing();
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
                _shadowResolution=urp.mainLightShadowmapResolution; urp.mainLightShadowmapResolution=Mathf.Max(_shadowResolution,ShadowResolution);
                _pipelineMsaa=urp.msaaSampleCount;
            }
            ApplyAntialiasing();
            BuildBerths(); BuildTraffic();
            foreach(var t in _island.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=Layer;
            DropSmallShadows(); MuteSceneCameras();
            ShowOverview(); _camera.transform.position=_destination; _currentLook=_lookAt;
            _camera.transform.LookAt(_currentLook); _camera.fieldOfView=_fov;
        }

        void DropSmallShadows()
        {
            foreach(var r in _stage.GetComponentsInChildren<Renderer>())
                if(r.shadowCastingMode!=ShadowCastingMode.Off && r.bounds.size.magnitude<SmallCaster)
                    { r.shadowCastingMode=ShadowCastingMode.Off; SmallCastersOff++; }
        }

        /// <summary>화면에 그리는 다른 카메라(전투 카메라)는 시작 화면 동안 그릴 층·그림자·후처리를 비운다.</summary>
        void MuteSceneCameras()
        {
            foreach(var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if(c==_camera || !c.enabled || c.targetTexture!=null) continue;
                var data=c.GetUniversalAdditionalCameraData();
                _mutedCameras.Add((c,c.cullingMask,data!=null && data.renderShadows,data!=null && data.renderPostProcessing));
                c.cullingMask=0;
                if(data!=null){data.renderShadows=false;data.renderPostProcessing=false;}
            }
        }

        void RestoreSceneCameras()
        {
            foreach(var (c,mask,shadows,post) in _mutedCameras)
            {
                if(c==null) continue;
                c.cullingMask=mask;
                var data=c.GetUniversalAdditionalCameraData();
                if(data!=null){data.renderShadows=shadows;data.renderPostProcessing=post;}
            }
            _mutedCameras.Clear();
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
                    Points=FighterRoute,
                    Times=new[]{6f,8f,4f,8f,7f,6f,12f,6f,9f,8f,4f,5f,5f} });
            var p3c=air.Find("P3C_Taxi");   // 유도로 대기 초계기(Naval/Art/Detach Taxiing P-3C로 합친 메시에서 떼어 냄)
            if(p3c!=null)
                _traffic.Add(new Traffic { Item=p3c, Offset=3, Points=P3CRoute, Times=P3CTimes });
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
        /// <summary>
        /// 계류장 → 평행 유도로(z −262) → −380 연결 유도로 → 활주로(z −330) 이륙 → 선회 → 착륙 → 330 연결 유도로 → 계류장(섬 좌표).
        /// </summary>
        public static readonly Vector3[] FighterRoute={new(60,4.6f,-185),new(60,4.6f,-262),new(-380,4.6f,-262),new(-380,4.6f,-330),new(400,4.6f,-330),new(1250,160,-330),new(1550,220,-1000),new(-1400,220,-1000),new(-1250,120,-330),new(-500,4.6f,-330),new(330,4.6f,-330),new(330,4.6f,-262),new(60,4.6f,-262)};

        /// <summary>
        /// 유도로 대기 초계기(2026-10-11): 전투기와 같은 88초 주기, 시간을 엇갈려 유도로·활주로를 함께 쓴다(전투기 시각 τ 기준).
        /// τ 2~26 (−470, −262)에서 동쪽을 보고 대기(전투기가 −380 연결 유도로로 이륙하는 동안) → 26 −380 연결 유도로로 활주로 →
        /// 32~40 동쪽으로 이륙(전투기는 이미 공중) → 동쪽 바다 위 큰 선회(전투기 선회 길과 수백 m 떨어짐) →
        /// 76 동쪽에서 서쪽으로 착륙(전투기는 74에 활주로를 비움) → 84 −760 연결 유도로 → 88+2 대기 자리로. Offset 3 = τ − 2.
        /// </summary>
        const float P3CGround=4.45f;
        public static readonly Vector3[] P3CRoute={new(-470,P3CGround,-262),new(-470,P3CGround,-262),new(-380,P3CGround,-262),new(-380,P3CGround,-330),new(500,30,-330),new(1700,250,-500),new(2600,320,-1200),new(3300,300,-450),new(2500,150,-330),new(1400,60,-330),new(650,P3CGround,-330),new(-700,P3CGround,-330),new(-760,P3CGround,-262)};
        public static readonly float[] P3CTimes={24f,3f,3f,8f,6f,7f,7f,6f,7f,3f,8f,2f,4f};

        /// <summary>계단 현상 설정을 카메라·URP에 적용한다(검사가 바꾼 뒤 다시 부른다).</summary>
        public void ApplyAntialiasing()
        {
            if(_camera==null)return;
            var data=_camera.GetUniversalAdditionalCameraData();
            if(data!=null)
            {
                data.antialiasing=PostAntialiasing;
                data.antialiasingQuality=AntialiasingQuality.High;
                data.renderPostProcessing=PostAntialiasing!=AntialiasingMode.None;   // 후처리 AA는 후처리 단계에서 돈다
                data.volumeLayerMask=0;   // 전투 화면의 볼륨(블룸 등)은 시작 화면에 섞지 않는다
            }
            if(_pipelineMsaa>0 && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                urp.msaaSampleCount=Msaa>1 ? Mathf.Max(2,_pipelineMsaa) : _pipelineMsaa;   // 켜져 있어야 렌더 텍스처의 MSAA를 쓴다
            EnsureTexture();
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
        /// 렌더 텍스처: 세로 = 화면 × Supersample(최소 1440, 최대 MaxRenderHeight — 크게 그려 줄여 붙이는 초과 표본화),
        /// 화면비는 창을 따른다. MSAA(세로 1440 이하 Msaa배, 그 위는 절반 — 메모리)에 밉맵을 켜서 줄여 붙일 때 가장자리가 덜 깨지게 한다.
        /// </summary>
        void EnsureTexture()
        {
            if(_camera==null || _image==null)return;
            int sw=Mathf.Max(Screen.width,1), sh=Mathf.Max(Screen.height,1);
            int h=Mathf.Clamp(Mathf.RoundToInt(sh*Mathf.Max(1f,Supersample)),MinRenderHeight,Mathf.Max(MinRenderHeight,MaxRenderHeight)), w=Mathf.RoundToInt(sw*(float)h/sh);
            int msaa=Msaa<=1 ? 1 : h>1440 ? Mathf.Max(2,Msaa/2) : Msaa;
            if(_texture!=null && _texture.width==w && _texture.height==h && _texture.antiAliasing==msaa) return;
            if(_texture!=null){_camera.targetTexture=null;_texture.Release();Destroy(_texture);}
            _texture=new RenderTexture(w,h,24){name="Live harbor",antiAliasing=msaa,useMipMap=true,autoGenerateMips=true,
                filterMode=FilterMode.Trilinear,anisoLevel=4};
            _texture.Create();
            _camera.targetTexture=_texture; _image.texture=_texture;
            _dirty=true;
        }
        void LateUpdate()
        {
            if(_camera==null)return;
            // 항구 화질·배경 정지(설정 › 그래픽): 화질이 바뀌면 프리셋을 다시 적용하고, 정지면 교통·회전을 멈추고 카메라가 움직일 때만 그린다
            int quality=Game.Core.DisplaySettings.HarborQuality;
            if(quality!=_appliedQuality){_appliedQuality=quality;ApplyQualityPreset(quality);}
            bool still=Game.Core.DisplaySettings.HarborStill;
            float cameraDt=Paused ? 0f : Time.unscaledDeltaTime;
            float dt=Paused || still ? 0f : Time.unscaledDeltaTime; _clock+=dt;
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
            float blend=1-Mathf.Exp(-cameraDt*3.2f);
            _camera.transform.position=Vector3.Lerp(_camera.transform.position,_destination,blend);
            _currentLook=Vector3.Lerp(_currentLook,_lookAt,blend); _camera.transform.LookAt(_currentLook);
            // 근평면이 0.1이면 멀리 본 섬에서 깊이 정밀도가 모자라 겹친 면(잔디 조각·도로)이 깨진다 — 보는 거리에 맞춰 민다
            _camera.nearClipPlane=Mathf.Clamp(Vector3.Distance(_camera.transform.position,_currentLook)*.08f,.1f,20f);
            _camera.fieldOfView=Mathf.Lerp(_camera.fieldOfView,_fov,blend);
            // 움직임은 프레임 사이 실제 이동량으로 본다(목표 근처에서 아주 작게 남는 차이로 계속 그리지 않게)
            bool moving=(_camera.transform.position-_lastCameraPosition).sqrMagnitude>1e-4f || Mathf.Abs(_camera.fieldOfView-_lastFov)>.01f;
            _lastCameraPosition=_camera.transform.position; _lastFov=_camera.fieldOfView;
            bool render=!still || moving || _dirty;
            if(_camera.enabled!=render)_camera.enabled=render;
            if(render)_dirty=false;   // 켜진 카메라가 이번 프레임에 그린다
        }

        /// <summary>검증용: 지금 카메라가 매 프레임 그리는가(배경 정지면 멈춘 동안 그리지 않는다).</summary>
        public bool CameraRendering => _camera!=null && _camera.enabled;
        public Vector2Int TextureSize => _texture!=null ? new Vector2Int(_texture.width,_texture.height) : Vector2Int.zero;
        public int TextureMsaa => _texture!=null ? _texture.antiAliasing : 0;
        static Bounds BoundsOf(GameObject go)
        {
            var rs=go.GetComponentsInChildren<Renderer>(); var b=rs[0].bounds;
            for(int i=1;i<rs.Length;i++)b.Encapsulate(rs[i].bounds); return b;
        }
        void OnDisable()
        {
            if(_stage!=null)_stage.gameObject.SetActive(false);
            RestoreSceneCameras();
            foreach(var l in _mutedLights)if(l!=null)l.enabled=true;
            if(_lightingSaved) {RenderSettings.ambientMode=_ambientMode;RenderSettings.ambientLight=_ambient;_lightingSaved=false;}
            if(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                if(_shadowDistance>=0f){urp.shadowDistance=_shadowDistance;_shadowDistance=-1f;}
                if(_shadowResolution>0){urp.mainLightShadowmapResolution=_shadowResolution;_shadowResolution=-1;}
                if(_pipelineMsaa>0){urp.msaaSampleCount=_pipelineMsaa;_pipelineMsaa=-1;}
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

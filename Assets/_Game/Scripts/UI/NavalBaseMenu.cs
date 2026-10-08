using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
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
        public bool CameraSettled => (_camera.transform.position-_destination).sqrMagnitude < .02f;
        const int Layer = 28;
        Transform _stage, _island;
        Camera _camera;
        RenderTexture _texture;
        Vector3 _destination, _lookAt, _currentLook;
        float _fov=25, _clock;
        readonly List<Transform> _ships = new();
        readonly List<Vector3> _centers = new();
        readonly List<Light> _mutedLights = new();
        readonly List<Traffic> _traffic = new();
        AmbientMode _ambientMode;
        Color _ambient;
        bool _lightingSaved;
        sealed class Traffic { public Transform Item; public Vector3[] Points; public float[] Times; public float Offset; }

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
            _texture=new RenderTexture(1920,1080,24){name="Live harbor",antiAliasing=2}; _texture.Create();
            _camera.targetTexture=_texture; image.texture=_texture; image.raycastTarget=false;
            foreach(var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if(l.enabled) { _mutedLights.Add(l); l.enabled=false; }
            _ambientMode=RenderSettings.ambientMode; _ambient=RenderSettings.ambientLight; _lightingSaved=true;
            RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.63f,.71f,.77f);
            var sun=new GameObject("Harbor daylight",typeof(Light)).GetComponent<Light>();
            sun.transform.SetParent(_stage,false); sun.transform.rotation=Quaternion.Euler(48,150,0);
            sun.type=LightType.Directional; sun.intensity=1.15f; sun.color=new Color(1,.97f,.92f);
            sun.cullingMask=1<<Layer; sun.shadows=LightShadows.Soft;
            BuildBerths(); BuildTraffic();
            foreach(var t in _island.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=Layer;
            ShowOverview(); _camera.transform.position=_destination; _currentLook=_lookAt;
            _camera.transform.LookAt(_currentLook); _camera.fieldOfView=_fov;
        }

        void BuildBerths()
        {
            var harbor=_island.Find("Harbor");
            var old=harbor.Find("Berths"); if(old!=null) old.gameObject.SetActive(false);
            // Four consecutive berths along the existing quay; extend the last pier once.
            Transform last=null;
            foreach(Transform t in harbor) if(t.name=="Pier") last=t;
            if(last!=null) { var fourth=Instantiate(last,harbor); fourth.name="Pier 4"; fourth.localPosition+=Vector3.right*220; }
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
                    Points=new[]{new Vector3(60,4.6f,-185),new Vector3(-640,4.6f,-230),new Vector3(-700,4.6f,-330),new Vector3(400,4.6f,-330),new Vector3(1250,160,-330),new Vector3(1550,220,-1000),new Vector3(-1400,220,-1000),new Vector3(-1250,120,-330),new Vector3(-500,4.6f,-330),new Vector3(200,4.6f,-330),new Vector3(270,4.6f,-230),new Vector3(60,4.6f,-185)},
                    Times=new[]{14f,4f,9f,7f,6f,12f,6f,9f,8f,5f,7f,8f} });
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
        }

        public void ShowOverview()
        {
            SelectedIndex=-1;
            _destination=_island.TransformPoint(new Vector3(-1350,1650,2000));
            _lookAt=_island.TransformPoint(new Vector3(20,0,60)); _fov=25;
        }
        public void FocusShip(int index)
        {
            if(_ships.Count==0)return;
            SelectedIndex=Mathf.Clamp(index,0,_ships.Count-1);
            _lookAt=_centers[SelectedIndex]+Vector3.up*1.4f;
            _destination=_lookAt+new Vector3(0,20,18); _fov=38;
        }
        public void RenderNow() { if(_camera!=null) _camera.Render(); }
        void LateUpdate()
        {
            if(_camera==null)return;
            float dt=Time.unscaledDeltaTime; _clock+=dt;
            foreach(var traffic in _traffic)
            {
                float total=0; foreach(float duration in traffic.Times)total+=duration;
                float time=(_clock+traffic.Offset)%total; int segment=0;
                while(time>=traffic.Times[segment] && segment<traffic.Times.Length-1) {time-=traffic.Times[segment];segment++;}
                var a=traffic.Points[segment]; var b=traffic.Points[(segment+1)%traffic.Points.Length];
                traffic.Item.localPosition=Vector3.Lerp(a,b,time/traffic.Times[segment]);
                if((b-a).sqrMagnitude>.01f)traffic.Item.localRotation=Quaternion.Slerp(traffic.Item.localRotation,Quaternion.LookRotation(b-a),1-Mathf.Exp(-dt*2));
            }
            float blend=1-Mathf.Exp(-dt*3.2f);
            _camera.transform.position=Vector3.Lerp(_camera.transform.position,_destination,blend);
            _currentLook=Vector3.Lerp(_currentLook,_lookAt,blend); _camera.transform.LookAt(_currentLook);
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
        }
        void OnDestroy()
        {
            if(Active==this)Active=null;
            if(_stage!=null)Destroy(_stage.gameObject);
            if(_texture!=null){_texture.Release();Destroy(_texture);}
        }
    }
}

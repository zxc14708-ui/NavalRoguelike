using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 사전의 3D 미리보기. 프리팹을 비활성 부모 아래 복제해(스크립트 Awake가 돌지 않음) 메시와 재질만 옮겨 담고,
    /// 바다 아래 멀리 떨어진 무대에서 전용 카메라가 RenderTexture로 찍는다(전용 레이어만 보고, 다른 카메라는 이 레이어를 안 본다).
    /// 천천히 돌고, 끌면 돌릴 수 있다. 시간이 멈춘 메인 화면에서도 돌도록 실제 시간을 쓴다.
    /// </summary>
    public sealed class CodexPreview : MonoBehaviour, IDragHandler
    {
        /// <summary>미리보기 전용 레이어(프로젝트에서 쓰지 않는 칸).</summary>
        public const int Layer = 30;
        private static readonly Vector3 StageOrigin = new(0f, -3000f, 0f);
        private const float Fov = 28f, Pitch = 24f, SpinSpeed = 16f;

        private Camera _cam;
        private RenderTexture _rt;
        private Transform _stage, _turntable, _model;
        private GameObject _emptyLabel;
        private float _yaw = 215f, _lastDrag = -10f, _distance = 10f;

        /// <summary>지금 보이는 메시 수(검증용, 0이면 모델 없음).</summary>
        public int RendererCount { get; private set; }

        public static CodexPreview Create(RawImage image, GameObject emptyLabel)
        {
            var preview = image.gameObject.AddComponent<CodexPreview>();
            preview._emptyLabel = emptyLabel;
            var size = image.rectTransform.sizeDelta;
            preview._rt = new RenderTexture(Mathf.RoundToInt(size.x * 2f), Mathf.RoundToInt(size.y * 2f), 24)
            {
                name = "Codex preview",
                antiAliasing = 4,
            };
            image.texture = preview._rt;
            image.raycastTarget = true;

            preview._stage = new GameObject("Codex preview stage").transform;
            preview._stage.position = StageOrigin;
            preview._turntable = new GameObject("Turntable").transform;
            preview._turntable.SetParent(preview._stage, false);

            var camGo = new GameObject("Codex preview camera");
            camGo.transform.SetParent(preview._stage, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.075f, 0.06f, 1f);
            cam.cullingMask = 1 << Layer;
            cam.fieldOfView = Fov;
            cam.targetTexture = preview._rt;
            cam.allowMSAA = true;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = true;
            preview._cam = cam;

            // 다른 카메라는 미리보기 레이어를 보지 않는다(무대가 멀리 있어도 확실히)
            foreach (var other in Camera.allCameras)
                if (other != cam) other.cullingMask &= ~(1 << Layer);
            preview.Place();
            return preview;
        }

        /// <summary>프리팹의 보이는 메시만 무대에 올린다. onlyChild(Visual_LevelN)가 있으면 그 외형 단계만.</summary>
        public void Show(GameObject prefab, string onlyChild)
        {
            if (_model != null) Destroy(_model.gameObject);
            _model = new GameObject("Model").transform;
            _model.SetParent(_turntable, false);
            RendererCount = 0;
            var bounds = new Bounds();
            bool hasBounds = false;

            if (prefab != null)
            {
                var holder = new GameObject("Codex preview source");
                holder.SetActive(false);
                var source = Instantiate(prefab, holder.transform);
                source.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Transform only = string.IsNullOrEmpty(onlyChild) ? null : FindDeep(source.transform, onlyChild);
                foreach (var r in source.GetComponentsInChildren<Renderer>(true))
                {
                    if (!r.enabled || !Visible(r.transform, source.transform, only)) continue;
                    Mesh mesh = r is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                        : r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var mf) ? mf.sharedMesh : null;
                    if (mesh == null) continue;

                    var copy = new GameObject(r.name, typeof(MeshFilter), typeof(MeshRenderer));
                    copy.layer = Layer;
                    copy.transform.SetParent(_model, false);
                    copy.transform.localPosition = r.transform.position;
                    copy.transform.localRotation = r.transform.rotation;
                    copy.transform.localScale = r.transform.lossyScale;
                    copy.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = copy.GetComponent<MeshRenderer>();
                    mr.sharedMaterials = r.sharedMaterials;
                    mr.shadowCastingMode = ShadowCastingMode.On;
                    RendererCount++;

                    var m = r.transform.localToWorldMatrix;
                    var b = mesh.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        var corner = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents,
                            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                        if (!hasBounds) { bounds = new Bounds(corner, Vector3.zero); hasBounds = true; }
                        else bounds.Encapsulate(corner);
                    }
                }
                Destroy(holder);
            }

            // 모델 중심을 돌림판 가운데로, 크기에 맞춰 카메라 거리
            _model.localPosition = hasBounds ? -bounds.center : Vector3.zero;
            float radius = hasBounds ? Mathf.Max(0.5f, bounds.extents.magnitude) : 1f;
            _distance = radius / Mathf.Sin(Fov * 0.5f * Mathf.Deg2Rad) * 1.02f;
            if (_emptyLabel != null) _emptyLabel.SetActive(RendererCount == 0);
            Place();
        }

        /// <summary>안 보이는 자식은 건너뛴다. only가 있으면 Visual_Level 형제 중 그것만 켠 것으로 본다.</summary>
        private static bool Visible(Transform t, Transform root, Transform only)
        {
            for (var c = t; c != null && c != root; c = c.parent)
            {
                if (only != null && c.name.StartsWith("Visual_Level"))
                {
                    if (c != only) return false;
                    continue;
                }
                if (!c.gameObject.activeSelf) return false;
            }
            return true;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// 검증용: 지금 바로 한 장 그리고(배치 모드 플레이어는 카메라를 저절로 그리지 않을 수 있다) 배경과 다른 픽셀 비율(0~1)을 잰다.
        /// </summary>
        public float RenderAndMeasure()
        {
            if (_cam == null || _rt == null) return 0f;
            Place();
            _cam.Render();
            var flat = RenderTexture.GetTemporary(_rt.width, _rt.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(_rt, flat);
            var prev = RenderTexture.active;
            RenderTexture.active = flat;
            var tex = new Texture2D(flat.width, flat.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, flat.width, flat.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(flat);
            var px = tex.GetPixels32();
            Color32 bg = px[0];   // 모서리 = 배경
            int differ = 0;
            foreach (var p in px)
                if (Mathf.Abs(p.r - bg.r) + Mathf.Abs(p.g - bg.g) + Mathf.Abs(p.b - bg.b) > 24) differ++;
            Destroy(tex);
            return differ / (float)px.Length;
        }

        public void OnDrag(PointerEventData eventData)
        {
            _yaw -= eventData.delta.x * 0.4f;
            _lastDrag = Time.unscaledTime;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime - _lastDrag > 1.5f) _yaw += SpinSpeed * Time.unscaledDeltaTime;
            Place();
        }

        private void Place()
        {
            if (_turntable == null || _cam == null) return;
            _turntable.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            var dir = Quaternion.Euler(Pitch, 0f, 0f) * Vector3.back;
            _cam.transform.localPosition = dir * _distance;
            _cam.transform.LookAt(_stage.position);
            _cam.nearClipPlane = Mathf.Max(0.05f, _distance * 0.05f);
            _cam.farClipPlane = _distance * 4f;
        }

        private void OnEnable()
        {
            if (_cam != null) _cam.enabled = true;
        }

        private void OnDisable()
        {
            if (_cam != null) _cam.enabled = false;
        }

        private void OnDestroy()
        {
            if (_stage != null) Destroy(_stage.gameObject);
            if (_rt != null) _rt.Release();
        }
    }
}

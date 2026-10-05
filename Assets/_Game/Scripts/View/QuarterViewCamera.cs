using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Game.Core;

namespace Game.View
{
    /// <summary>
    /// 45~60도 경사의 쿼터뷰 추적 카메라.
    /// 완전한 탑다운이 아니어서 함선과 모듈의 실루엣이 보이는 것이 핵심이다.
    ///
    /// 전투 시야: 함선 중심에서 가장 가까운 화면 가장자리까지 바다 위 약 42 단위(기본 거리 72, 시야각 45°).
    /// 경사 카메라는 화면 아래쪽(카메라 쪽) 수면이 좁게 보이므로, 초점을 카메라 쪽으로 당겨
    /// 함선을 화면 중앙보다 조금 위에 둔다 — 그래야 사방이 고르게 보인다.
    /// 시야각을 60°에서 45°로 줄이고 더 멀리서 본 것은, 원근 왜곡을 줄여 먼 적도 가까운 적과 비슷한 크기로 보이게 하기 위해서다.
    ///
    /// 초점은 함선 진행 방향이 아니라 화면 기준으로 고정한다(방향에 따라 보이는 거리가 달라지지 않게).
    /// 배치 모드에서는 선수가 항상 화면 위를 향하도록 함선 기준으로 정렬한다.
    /// </summary>
    public class QuarterViewCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Combat View")]
        [SerializeField, Range(30f, 80f)] private float combatPitch = 52f;
        [SerializeField] private float combatYaw = 0f;
        [SerializeField, Range(20f, 70f)] private float combatFieldOfView = 45f;
        [Tooltip("기본 거리. 72 → 가장 가까운 화면 가장자리까지 수면 약 42 단위")]
        [SerializeField] private float combatDistance = 72f;
        [Tooltip("휠 줌 범위. 58 → 약 34 단위, 92 → 약 54 단위")]
        [SerializeField] private float minCombatDistance = 58f;
        [SerializeField] private float maxCombatDistance = 92f;
        [SerializeField] private float zoomStep = 4f;
        [Tooltip("초점을 카메라 쪽(화면 아래)으로 당기는 비율(거리 대비). 경사 원근으로 좁아지는 아래쪽 시야를 보충한다.")]
        [SerializeField, Range(0f, 0.4f)] private float focusPullRatio = 0.1875f;

        [Header("Ship Growth")]
        [Tooltip("함체 길이가 이 칸 수를 넘으면 조금씩 멀어진다")]
        [SerializeField] private int growthStartCells = 6;
        [SerializeField] private float distancePerExtraCell = 0.8f;
        [Tooltip("함체 성장으로 늘어나는 거리 상한")]
        [SerializeField] private float maxGrowthDistance = 6f;

        [Header("Follow")]
        [SerializeField] private float followSmoothTime = 0.25f;
        [SerializeField] private float zoomSmoothTime = 0.2f;

        [Header("Build Mode")]
        [Tooltip("배치 중 시점. 격자가 잘 보이도록 더 위에서 내려다본다.")]
        [SerializeField, Range(50f, 89f)] private float buildPitch = 74f;
        [SerializeField] private float buildDistance = 26f;
        [SerializeField, Range(20f, 70f)] private float buildFieldOfView = 60f;
        [SerializeField] private float buildBlendSpeed = 6f;

        [Header("Miniature Look")]
        [Tooltip("전투 화면에 모형 촬영 같은 얕은 심도와 색 보정을 적용한다. HUD와 정비 미리보기 카메라는 영향받지 않는다.")]
        [SerializeField] private bool miniatureLook = true;
        [Tooltip("작을수록 주변 배경이 더 흐려진다. 함선은 자동 초점으로 선명하게 유지한다.")]
        [SerializeField, Range(1f, 16f)] private float combatAperture = 4.5f;
        [SerializeField, Range(1f, 16f)] private float buildAperture = 5.6f;
        [SerializeField, Range(50f, 250f)] private float miniatureFocalLength = 140f;
        [SerializeField, Range(0f, 30f)] private float miniatureSaturation = 14f;
        [SerializeField, Range(0f, 25f)] private float miniatureContrast = 10f;

        private Camera _camera;
        private Vector3 _velocity;
        private bool _buildFraming;
        private float _zoomTarget;
        private float _zoomCurrent;
        private float _zoomVelocity;
        private Game.Ship.ShipGrid _grid;
        private GameObject _miniatureVolumeObject;
        private VolumeProfile _miniatureProfile;
        private DepthOfField _miniatureDepthOfField;
        private bool _mobileDepthOfField;

        /// <summary>지금 적용 중인 전투 카메라 거리(성장 보정 포함). 개발용 표시가 읽는다.</summary>
        public float CurrentDistance => _zoomCurrent + GrowthDistance();

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _zoomTarget = _zoomCurrent = Mathf.Clamp(combatDistance, minCombatDistance, maxCombatDistance);
            if (_camera != null) _camera.fieldOfView = combatFieldOfView;
            if (miniatureLook && _camera != null) SetupMiniatureLook();
        }

        private void SetupMiniatureLook()
        {
            var cameraData = GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null) cameraData = gameObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = Application.isMobilePlatform ? AntialiasingQuality.Medium : AntialiasingQuality.High;

            _miniatureVolumeObject = new GameObject("Miniature Look Volume", typeof(Volume));
            _miniatureVolumeObject.transform.SetParent(transform, false);
            _miniatureVolumeObject.layer = gameObject.layer;
            cameraData.volumeLayerMask = cameraData.volumeLayerMask.value | (1 << _miniatureVolumeObject.layer);

            var volume = _miniatureVolumeObject.GetComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 50f;
            _miniatureProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _miniatureProfile.name = "Miniature Look (runtime)";
            volume.sharedProfile = _miniatureProfile;

            _miniatureDepthOfField = _miniatureProfile.Add<DepthOfField>(true);
            _mobileDepthOfField = Application.isMobilePlatform;
            _miniatureDepthOfField.mode.Override(_mobileDepthOfField ? DepthOfFieldMode.Gaussian : DepthOfFieldMode.Bokeh);
            _miniatureDepthOfField.focusDistance.Override(target != null ? Vector3.Distance(transform.position, target.position) : combatDistance);
            _miniatureDepthOfField.aperture.Override(combatAperture);
            _miniatureDepthOfField.focalLength.Override(miniatureFocalLength);
            _miniatureDepthOfField.bladeCount.Override(6);
            _miniatureDepthOfField.gaussianMaxRadius.Override(0.7f);
            _miniatureDepthOfField.highQualitySampling.Override(false);

            var color = _miniatureProfile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.08f);
            color.contrast.Override(miniatureContrast);
            color.saturation.Override(miniatureSaturation);
            color.colorFilter.Override(new Color(1f, 0.985f, 0.97f));

            var bloom = _miniatureProfile.Add<Bloom>(true);
            bloom.threshold.Override(1.2f);
            bloom.intensity.Override(0.18f);

            var vignette = _miniatureProfile.Add<Vignette>(true);
            vignette.intensity.Override(0.08f);
            vignette.smoothness.Override(0.4f);
        }

        private void OnDestroy()
        {
            if (_miniatureVolumeObject != null) Destroy(_miniatureVolumeObject);
            if (_miniatureProfile == null) return;
            foreach (var component in _miniatureProfile.components)
                if (component != null) Destroy(component);
            Destroy(_miniatureProfile);
        }

        private void OnEnable() => GameEvents.StateChanged += OnStateChanged;
        private void OnDisable() => GameEvents.StateChanged -= OnStateChanged;

        /// <summary>정비 중에만 함선 기준 시점으로 전환한다.</summary>
        private void OnStateChanged(GameState state)
            => _buildFraming = state == GameState.Refit;

        private void LateUpdate()
        {
            if (target == null) return;

            // 시간이 멈춘 배치 모드에서도 카메라는 움직여야 하므로 unscaled를 쓴다
            float dt = Time.unscaledDeltaTime;

            if (!_buildFraming) HandleZoom();
            _zoomCurrent = Mathf.SmoothDamp(_zoomCurrent, _zoomTarget, ref _zoomVelocity, zoomSmoothTime, Mathf.Infinity, dt);

            float blend = 1f - Mathf.Exp(-buildBlendSpeed * dt);
            float targetPitch = _buildFraming ? buildPitch : combatPitch;
            float targetDistance = _buildFraming ? buildDistance : CurrentDistance;
            float targetYaw = _buildFraming ? target.eulerAngles.y : combatYaw;

            if (_camera != null)
                _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, _buildFraming ? buildFieldOfView : combatFieldOfView, blend);

            var rot = Quaternion.Euler(targetPitch, targetYaw, 0f);

            Vector3 focus = target.position;
            if (!_buildFraming)
            {
                // 화면 아래쪽(카메라가 있는 쪽) 수평 방향으로 초점을 당긴다
                Vector3 towardCamera = rot * Vector3.back;
                towardCamera.y = 0f;
                if (towardCamera.sqrMagnitude > 0.0001f) focus += towardCamera.normalized * (targetDistance * focusPullRatio);
            }

            Vector3 desired = focus - rot * Vector3.forward * targetDistance;

            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity,
                                                    followSmoothTime, Mathf.Infinity, dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, blend);
            UpdateMiniatureFocus();
        }

        private void UpdateMiniatureFocus()
        {
            if (_miniatureDepthOfField == null || target == null) return;
            float distance = Vector3.Distance(transform.position, target.position);
            if (_mobileDepthOfField)
            {
                // 모바일은 비용이 낮은 원거리 가우시안 흐림만 쓴다.
                _miniatureDepthOfField.gaussianStart.value = distance + 16f;
                _miniatureDepthOfField.gaussianEnd.value = distance + 55f;
            }
            else
            {
                _miniatureDepthOfField.focusDistance.value = distance;
                _miniatureDepthOfField.aperture.value = _buildFraming ? buildAperture : combatAperture;
            }
        }

        /// <summary>마우스 휠 줌. 커서가 UI 위에 있으면 무시한다(스킬 버튼·목록 스크롤과 충돌하지 않게).</summary>
        private void HandleZoom()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.01f) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            _zoomTarget = Mathf.Clamp(_zoomTarget - Mathf.Sign(scroll) * zoomStep, minCombatDistance, maxCombatDistance);
        }

        /// <summary>함체가 길어지면 조금 멀어진다. 상한이 있어 과도하게 작아지지 않는다.</summary>
        private float GrowthDistance()
        {
            if (_grid == null && target != null)
            {
                var ship = target.GetComponent<Game.Ship.ShipController>();
                _grid = ship != null ? ship.Grid : null;
            }
            if (_grid == null || _grid.Modules.Count == 0) return 0f;

            _grid.GetExtent(out var min, out var max);
            int length = Mathf.Max(max.X - min.X + 1, max.Z - min.Z + 1);
            return Mathf.Clamp((length - growthStartCells) * distancePerExtraCell, 0f, maxGrowthDistance);
        }

        public void SetTarget(Transform t) => target = t;
    }
}

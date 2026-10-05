using UnityEngine;
using Game.Modules;

namespace Game.Combat
{
    /// <summary>
    /// 포탑 회전 + 사격각 판정 + 재장전을 담당하는 공용 부품.
    /// 기관포/CIWS/VLS 런타임이 공통으로 들고 쓴다(상속 대신 구성).
    ///
    /// 피벗의 로컬 축을 믿지 않는다. FBX는 축 변환을 루트에만 걸기 때문에
    /// 아트 모델의 피벗은 Blender 좌표(Z가 위, Y가 앞)를 그대로 갖고 있다.
    /// LookRotation으로 로컬 +Z를 목표에 맞추면 포탑이 옆으로 누워 포신이 하늘을 본다.
    /// 대신 처음 조준할 때 "피벗 → Muzzle" 방향을 포신 축으로 기억하고,
    /// 이후에는 월드 기준 선회축/고각축에 대한 각도 차이만큼만 돌린다.
    /// </summary>
    [System.Serializable]
    public class WeaponController
    {
        [SerializeField] private Transform turret;         // 좌우 회전부
        [SerializeField] private Transform elevationPivot; // 선택적 상하 고각부
        [SerializeField] private Transform muzzle;         // 발사 지점

        [Header("Elevation")]
        [SerializeField] private float minElevation = -10f;
        [SerializeField] private float maxElevation = 75f;

        [Tooltip("포신이 목표에서 이 각도 이내로 들어와야 사격한다")]
        [SerializeField] private float aimTolerance = 8f;

        private float _cooldown;
        private float _reloadMultiplier = 1f;

        // 설치 방향 기준 사격각. 주변 블록이 바뀔 때마다 모듈이 다시 넣어준다.
        private FireArc _arc = FireArc.Full;

        // 조준 기준값. 직렬화하지 않으므로 프리팹을 복제할 때마다 새로 잡는다.
        private bool _calibrated;
        private Vector3 _upInTurretParent;   // 선회축(월드 위)을 포탑 부모 공간으로
        private Vector3 _restInTurretParent; // 설치 방향(중립 포신)을 포탑 부모 공간으로. 선회 제한의 기준.
        private Vector3 _barrelInTurret;     // 수평 포신 방향을 포탑 공간으로
        private Vector3 _barrelInElevation;  // 포신 방향을 고각부 공간으로

        public Transform Muzzle => muzzle;
        public FireArc Arc => _arc;
        public bool HasTurret => turret != null;

        /// <summary>모델 FBX를 교체한 뒤 새 피벗에 다시 연결한다. 조준 축은 다음 사용 시 재계산한다.</summary>
        public void BindArtTransforms(Transform newTurret, Transform newElevationPivot, Transform newMuzzle)
        {
            turret = newTurret;
            elevationPivot = newElevationPivot;
            muzzle = newMuzzle;
            _calibrated = false;
            _recoilPart = null;
            _recoilAmount = 0f;
        }

        public void SetFireArc(FireArc arc) => _arc = arc;
        public bool IsReady => _cooldown <= 0f;

        /// <summary>탄약고 근접 보너스 등 외부 요인을 반영한다. 1.0 = 보정 없음.</summary>
        public void SetReloadMultiplier(float m) => _reloadMultiplier = Mathf.Max(0.1f, m);

        public void Tick(float dt)
        {
            if (_cooldown > 0f) _cooldown -= dt;
            UpdateRecoil(dt);
        }

        /// <summary>
        /// 실제 포신이 가리키는 방향. 탄은 목표 방향이 아니라 이 방향으로 내보낸다 —
        /// 포구 위치·포신 방향·탄의 첫 비행 방향이 어긋나 보이지 않게. 조준 오차는 IsAimedAt으로 발사 전에 거른다.
        /// </summary>
        public Vector3 FireDirection
        {
            get
            {
                if (!_calibrated) Calibrate();
                return BarrelDirection();
            }
        }

        // --- 반동: 고각부를 포신 반대 방향으로 밀었다가 되돌린다(76mm)
        private Transform _recoilPart;
        private Vector3 _recoilRestLocal;
        private float _recoilAmount;

        /// <summary>발사 반동. distance만큼 뒤로 밀리고 곧 제자리로 돌아온다.</summary>
        public void Kick(float distance)
        {
            var part = elevationPivot != null ? elevationPivot : turret;
            if (part == null) return;
            if (_recoilPart != part)
            {
                _recoilPart = part;
                _recoilRestLocal = part.localPosition;
            }
            _recoilAmount = distance;
            ApplyRecoil();
        }

        private void UpdateRecoil(float dt)
        {
            if (_recoilPart == null || _recoilAmount <= 0f) return;
            _recoilAmount = Mathf.MoveTowards(_recoilAmount, 0f, dt * 1.2f);
            ApplyRecoil();
        }

        private void ApplyRecoil()
        {
            if (_recoilPart == null) return;
            _recoilPart.localPosition = _recoilRestLocal;
            if (_recoilAmount <= 0f) return;

            Vector3 back = -FireDirection * _recoilAmount;
            var parent = _recoilPart.parent;
            _recoilPart.localPosition += parent != null ? parent.InverseTransformVector(back) : back;
        }

        /// <summary>
        /// 중립 자세의 포신 축을 기록한다. 모델은 포신이 수평인 자세로 저장되어 있어야 한다.
        /// </summary>
        private void Calibrate()
        {
            _calibrated = true;
            if (turret == null) return;

            Transform origin = elevationPivot != null ? elevationPivot : turret;
            Vector3 barrel = muzzle != null ? muzzle.position - origin.position : Vector3.zero;

            // Muzzle이 피벗과 겹쳐 있으면 방향을 알 수 없다. 그레이박스 관례(+Z)로 대신한다.
            barrel = Vector3.ProjectOnPlane(barrel, Vector3.up);
            if (barrel.sqrMagnitude < 0.0001f) barrel = Vector3.ProjectOnPlane(turret.forward, Vector3.up);
            if (barrel.sqrMagnitude < 0.0001f) barrel = Vector3.forward;
            barrel.Normalize();

            _upInTurretParent = turret.parent != null
                ? turret.parent.InverseTransformDirection(Vector3.up)
                : Vector3.up;
            _restInTurretParent = turret.parent != null
                ? turret.parent.InverseTransformDirection(barrel)
                : barrel;
            _barrelInTurret = turret.InverseTransformDirection(barrel);
            if (elevationPivot != null) _barrelInElevation = elevationPivot.InverseTransformDirection(barrel);
        }

        /// <summary>선회축. 함선과 함께 기울어도 따라가도록 포탑 부모 기준으로 계산한다.</summary>
        private Vector3 WorldUp => turret != null && turret.parent != null
            ? turret.parent.TransformDirection(_upInTurretParent).normalized
            : Vector3.up;

        /// <summary>설치 방향. 함선이 돌면 같이 돈다.</summary>
        private Vector3 RestDirection => turret != null && turret.parent != null
            ? turret.parent.TransformDirection(_restInTurretParent).normalized
            : _restInTurretParent;

        /// <summary>현재 포신이 가리키는 월드 방향(고각 포함).</summary>
        private Vector3 BarrelDirection()
        {
            if (elevationPivot != null) return elevationPivot.TransformDirection(_barrelInElevation).normalized;
            if (turret != null) return turret.TransformDirection(_barrelInTurret).normalized;
            return Vector3.forward;
        }

        /// <summary>
        /// 포탑을 목표 방향으로 서서히 돌리고, 고각부가 있으면 포신을 목표 높이에 맞춘다.
        /// 사격 금지 구역이 있으면 선회 금지 방향(함교 등)으로는 포신이 돌지 않고, 돌아갈 수 있는 경계까지만 따라간다.
        /// </summary>
        public void AimAt(Vector3 worldTarget, float turnRateDegPerSec, float dt)
        {
            if (turret == null) return;
            if (!_calibrated) Calibrate();

            float maxStep = turnRateDegPerSec * dt;
            Vector3 up = WorldUp;

            // --- 선회: 선회축에 수직인 평면에서 포신과 목표 사이의 각도 차이
            Vector3 toTarget = Vector3.ProjectOnPlane(worldTarget - turret.position, up);
            Vector3 barrelFlat = Vector3.ProjectOnPlane(BarrelDirection(), up);
            if (toTarget.sqrMagnitude > 0.001f && barrelFlat.sqrMagnitude > 0.001f)
            {
                float yaw;
                if (_arc.IsFull)
                {
                    yaw = Vector3.SignedAngle(barrelFlat, toTarget, up);
                }
                else
                {
                    // 설치 방향 기준 각도로 계산한다. 선회 금지 구역(상부 구조물 방향)으로는 돌지 않고,
                    // 그쪽 표적은 돌아갈 수 있는 가장 가까운 경계까지만 따라간다.
                    Vector3 rest = Vector3.ProjectOnPlane(RestDirection, up);
                    float yawNow = Vector3.SignedAngle(rest, barrelFlat, up);
                    float yawWant = _arc.ClampToTrainable(yawNow, Vector3.SignedAngle(rest, toTarget, up));
                    yaw = _arc.TraverseDelta(yawNow, yawWant);
                }
                turret.Rotate(up, Mathf.Clamp(yaw, -maxStep, maxStep), Space.World);
            }

            if (elevationPivot == null) return;

            // --- 고각: 포신의 현재 앙각을 목표 앙각(제한 범위 안)으로 옮긴다
            Vector3 barrel = BarrelDirection();
            Vector3 aim = worldTarget - elevationPivot.position;
            if (aim.sqrMagnitude < 0.001f) return;

            float current = 90f - Vector3.Angle(barrel, up);
            float desired = Mathf.Clamp(90f - Vector3.Angle(aim, up), minElevation, maxElevation);
            float step = Mathf.Clamp(desired - current, -maxStep, maxStep);

            // 고각축 = 포신의 오른쪽. 이 축으로 +회전하면 포신이 내려가므로 부호를 뒤집는다.
            Vector3 right = Vector3.Cross(up, Vector3.ProjectOnPlane(barrel, up));
            if (right.sqrMagnitude > 0.0001f)
                elevationPivot.Rotate(right.normalized, -step, Space.World);
        }

        /// <summary>
        /// 표적을 쏠 수 있는지: 사격 금지 구역 밖이고(구조물 너머 높은 표적은 허용),
        /// 지금 포신 자리에서 선회 금지 구역을 지나지 않고 돌아갈 수 있어야 한다. 제한이 없으면 항상 true.
        /// 표적 고르기에도 쓰므로, 돌아갈 수 없는 반대편 표적은 아예 고르지 않는다.
        /// </summary>
        public bool IsInFireArc(Vector3 worldTarget)
        {
            if (_arc.IsFull || turret == null) return true;
            if (!_calibrated) Calibrate();

            // 포신이 아니라 설치 방향 기준이다. 포신 기준이면 돌아간 만큼 사격각이 따라 움직인다.
            Vector3 up = WorldUp;
            Vector3 toTarget = worldTarget - turret.position;
            Vector3 dir = Vector3.ProjectOnPlane(toTarget, up);
            Vector3 rest = Vector3.ProjectOnPlane(RestDirection, up);
            float rel = Vector3.SignedAngle(rest, dir, up);

            Transform origin = elevationPivot != null ? elevationPivot : turret;
            float elevation = 90f - Vector3.Angle(worldTarget - origin.position, up);
            if (!_arc.AllowsFire(rel, elevation)) return false;

            Vector3 barrelFlat = Vector3.ProjectOnPlane(BarrelDirection(), up);
            float yawNow = barrelFlat.sqrMagnitude > 0.001f ? Vector3.SignedAngle(rest, barrelFlat, up) : 0f;
            return _arc.CanTrainTo(yawNow, rel);
        }

        /// <summary>
        /// 포신이 실제로 목표를 향했는지. 돌아가는 도중에 엉뚱한 방향으로 쏘지 않게 한다.
        /// 고각부가 없거나 목표가 고각 제한 밖이면 수평 방향만 본다.
        /// </summary>
        public bool IsAimedAt(Vector3 worldTarget)
        {
            if (turret == null) return true;
            if (!_calibrated) Calibrate();

            Vector3 up = WorldUp;
            Transform origin = elevationPivot != null ? elevationPivot : turret;
            Vector3 aim = worldTarget - origin.position;
            Vector3 barrel = BarrelDirection();

            bool flatOnly = elevationPivot == null;
            if (!flatOnly)
            {
                float desired = 90f - Vector3.Angle(aim, up);
                flatOnly = desired < minElevation || desired > maxElevation;
            }

            if (flatOnly)
            {
                aim = Vector3.ProjectOnPlane(aim, up);
                barrel = Vector3.ProjectOnPlane(barrel, up);
            }

            if (aim.sqrMagnitude < 0.001f || barrel.sqrMagnitude < 0.001f) return true;
            return Vector3.Angle(barrel, aim) <= aimTolerance;
        }

        /// <summary>발사 성공 시 true. 재장전 시간은 배율이 적용된다.</summary>
        public bool TryFire(float reloadTime)
        {
            if (!IsReady) return false;
            _cooldown = Mathf.Max(0.02f, reloadTime * _reloadMultiplier);
            return true;
        }
    }
}

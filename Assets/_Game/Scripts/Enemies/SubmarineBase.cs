using UnityEngine;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 잠수함 공통 뼈대: 잠항/부상, 수중 접촉, 발사 흔적을 각각 관리한다.
    /// 소나·헬기 탐지는 대잠 무기에만 위치를 알려주며 잠수함을 부상시키지 않는다.
    ///
    /// 잠항 중에는 선체를 렌더링하지 않는다. 소나 확정 접촉은 레이더에만 표시하고,
    /// 발사 흔적은 마지막 발사 위치로만 남긴다. 보이는데 함포로 못 쏘는 상태를 피한다.
    ///
    /// 공격 방식(어뢰·순항미사일)은 파생 클래스가 UpdateBehaviour에서 정한다. 매 프레임 TickStealth를 먼저 부른다.
    /// </summary>
    public abstract class SubmarineBase : EnemyController
    {
        [Header("Depth")]
        [Tooltip("모델을 오르내릴 피벗. 비우면 첫 자식을 쓴다.")]
        [SerializeField] private Transform modelPivot;
        [Tooltip("잠항 깊이. 바다 평면(-0.9) 아래로 선체가 잠기고 함교탑만 물 위에 남는 정도")]
        [SerializeField] private float submergedDepth = -1.05f;
        [SerializeField] private float surfacedDepth = 0f;
        [SerializeField] private float depthSpeed = 2.5f;

        [Header("Sinking Feedback")]
        [Tooltip("격침 순간 수면 위로 솟는 물기둥. 두 잠수함 프리팹 모두 FX_WaterBlast를 사용한다.")]
        [SerializeField] private GameObject sinkingSplash;
        [SerializeField, Min(0.1f)] private float sinkingSplashScale = 2.2f;

        private float _revealTimer;
        private float _contactTimer;
        private float _signatureTimer;
        private Vector3 _lastKnownPosition;
        private Vector3 _lastKnownVelocity;
        private Vector3 _launchPosition;
        private float _currentDepth;
        private Renderer[] _modelRenderers;
        private bool[] _originalRendererStates;

        protected Game.Ship.ShipController PlayerShip { get; private set; }

        public override TargetKind Kind => TargetKind.Submarine;
        public override bool IsRevealed => _revealTimer > 0f;
        public bool IsContactConfirmed => _contactTimer > 0f;
        public bool HasLaunchSignature => _signatureTimer > 0f;
        public Vector3 LastKnownPosition => _lastKnownPosition;
        public Vector3 LastKnownVelocity => _lastKnownVelocity;
        public Vector3 LaunchPosition => _launchPosition;
        public int SpawnRevision { get; private set; }

        /// <summary>남은 노출 시간(개발·검증용).</summary>
        public float RevealRemaining => Mathf.Max(0f, _revealTimer);

        /// <summary>0 = 완전 잠항, 1 = 완전 부상.</summary>
        protected float SurfaceAmount => Mathf.Approximately(surfacedDepth, submergedDepth)
            ? 1f
            : Mathf.InverseLerp(submergedDepth, surfacedDepth, _currentDepth);

        /// <summary>실제로 수면에 노출되는 공격 절차에서만 호출한다.</summary>
        public void Reveal()
            => RevealFor(Definition != null ? Mathf.Max(3f, Definition.RevealDuration) : 5f);

        /// <summary>이미 더 오래 드러나 있으면 줄이지 않는다.</summary>
        public void RevealFor(float seconds)
        {
            _revealTimer = Mathf.Max(_revealTimer, seconds);
            UpdateModelVisibility();
        }

        /// <summary>발사 뒤 잠항. 발사 흔적과 소나 접촉은 유지하되 수면 위 선체만 감춘다.</summary>
        protected void DiveAfterLaunch()
        {
            _revealTimer = 0f;
            UpdateModelVisibility();
        }

        /// <summary>소나·헬기의 수중 접촉. 위치를 알지만 부상하거나 함포의 표적이 되지는 않는다.</summary>
        public void ConfirmContact(float seconds)
        {
            _contactTimer = Mathf.Max(_contactTimer, seconds);
            _lastKnownPosition = transform.position;
            _lastKnownVelocity = transform.forward * CurrentSpeed;
        }

        /// <summary>발사 순간의 위치만 기록한다. 이후 이동한 현재 좌표는 알려주지 않는다.</summary>
        protected void ReportLaunchSignature(float seconds = 4f)
        {
            _launchPosition = transform.position;
            _signatureTimer = Mathf.Max(_signatureTimer, seconds);
        }

        /// <summary>노출 시간을 줄이고 깊이를 옮긴다. 파생 클래스가 UpdateBehaviour 첫머리에서 부른다.</summary>
        protected void TickStealth(float dt)
        {
            if (_revealTimer > 0f) _revealTimer -= dt;
            if (_contactTimer > 0f) _contactTimer -= dt;
            if (_signatureTimer > 0f) _signatureTimer -= dt;
            UpdateDepth(dt);
            UpdateModelVisibility();
        }

        public override void TakeDamage(in DamageInfo info)
        {
            // 탐지된 잠수함도 잠항 중에는 일반 포탄·대함미사일의 피해를 받지 않는다.
            if (!IsRevealed && info.Source != DamageSource.Torpedo) return;
            base.TakeDamage(info);
        }

        protected override void Die()
        {
            // 잠항 중에는 모델이 보이지 않으므로, 반드시 수면 위치에 격침 흔적을 남긴다.
            Vector3 surface = new(transform.position.x, 0f, transform.position.z);
            PooledEffect.Spawn(sinkingSplash, surface, sinkingSplashScale);
            Game.View.OceanSurface.SpawnSinkingTrace(surface);
            base.Die();
        }

        /// <summary>부상/잠항을 부드럽게 오간다.</summary>
        private void UpdateDepth(float dt)
        {
            var pivot = GetPivot();
            if (pivot == null) return;

            float target = IsRevealed ? surfacedDepth : submergedDepth;
            _currentDepth = Mathf.MoveTowards(_currentDepth, target, depthSpeed * dt);

            var p = pivot.localPosition;
            pivot.localPosition = new Vector3(p.x, _currentDepth, p.z);
        }

        private void UpdateModelVisibility()
        {
            if (_modelRenderers == null)
            {
                var pivot = GetPivot();
                if (pivot == null) return;
                _modelRenderers = pivot.GetComponentsInChildren<Renderer>(true);
                _originalRendererStates = new bool[_modelRenderers.Length];
                for (int i = 0; i < _modelRenderers.Length; i++)
                    _originalRendererStates[i] = _modelRenderers[i].enabled;
            }

            bool visible = IsRevealed;
            for (int i = 0; i < _modelRenderers.Length; i++)
                if (_modelRenderers[i] != null)
                    _modelRenderers[i].enabled = visible && _originalRendererStates[i];
        }

        protected Transform GetPivot()
        {
            if (modelPivot != null) return modelPivot;
            if (transform.childCount == 0) return null;

            modelPivot = transform.GetChild(0);
            return modelPivot;
        }

        public override void Setup(Game.Data.EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            PlayerShip = player != null ? player.GetComponent<Game.Ship.ShipController>() : null;
        }

        public override void OnSpawned()
        {
            base.OnSpawned();
            SpawnRevision++;
            ResetStealth();
        }

        public override void OnDespawned()
        {
            base.OnDespawned();
            ResetStealth();
        }

        /// <summary>풀에서 꺼내거나 돌려보낼 때: 노출을 지우고 잠항 깊이로 되돌린다.</summary>
        private void ResetStealth()
        {
            _revealTimer = 0f;
            _contactTimer = 0f;
            _signatureTimer = 0f;
            _lastKnownPosition = _launchPosition = transform.position;
            _lastKnownVelocity = Vector3.zero;
            _currentDepth = submergedDepth;

            var pivot = GetPivot();
            if (pivot == null) return;

            var p = pivot.localPosition;
            pivot.localPosition = new Vector3(p.x, submergedDepth, p.z);
            UpdateModelVisibility();
        }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using Game.Core;
using Game.Modules;

namespace Game.Ship
{
    /// <summary>
    /// 조명탄 [B](2026-11-19 낮과 밤). 마우스가 가리킨 바다 위에 낙하산 조명탄을 쏜다(Game.Combat.ParachuteFlare).
    ///   - 충전: 함교 1발 + 함포·곡사포마다 1발(최대 +2). 25초마다 1발씩 다시 찬다(성장 카드 "스킬 회복"을 따른다).
    ///   - 밤(어둠 0.35 이상)에만 효과: 반경 안의 반잠수정이 드러나고 잠수함이 확정 접촉이 된다. 낮엔 빛만.
    /// 함교 손상통제 횟수(응급 수리)와 섞이지 않게 ActiveSkillBase를 쓰지 않고 따로 센다. HUD 스킬 줄 일곱째 칸.
    /// </summary>
    public sealed class FlareSkill : MonoBehaviour
    {
        public const float RechargeSeconds = 25f;
        public const float MinRange = 10f, MaxRange = 50f;
        public const int MaxGunBonus = 2;

        private ShipController _ship;
        private int _charges = 1;
        private float _rechargeLeft;
        private float _statusTimer;
        private float _gap;

        public static FlareSkill Active { get; private set; }
        public int Charges => _charges;
        public int MaxCharges { get; private set; } = 1;
        /// <summary>쏜 조명탄 수(검증용).</summary>
        public int Fired { get; private set; }

        public static void Ensure(ShipController ship)
        {
            if (ship != null && !ship.TryGetComponent<FlareSkill>(out _)) ship.gameObject.AddComponent<FlareSkill>();
        }

        private void Awake() { _ship = GetComponent<ShipController>(); Active = this; }
        private void OnEnable() => GameEvents.SkillRequested += OnRequested;
        private void OnDisable() => GameEvents.SkillRequested -= OnRequested;
        private void OnDestroy() { if (Active == this) Active = null; }

        private void OnRequested(ActiveSkillId id) { if (id == ActiveSkillId.Flare) TryFire(); }

        /// <summary>지금 함선에서 조명탄을 낼 수 있는 최대 발수(함교가 없거나 부서지면 0).</summary>
        private int CountMax()
        {
            var grid = _ship != null ? _ship.Grid : null;
            if (grid == null) return 0;
            bool bridge = false; int guns = 0;
            foreach (var m in grid.Modules)
            {
                if (m == null || !m.IsOperational || m.Definition == null) continue;
                if (m.Definition.Type == ModuleType.Bridge) bridge = true;
                else if (m.Definition.Type is ModuleType.NavalGun or ModuleType.Howitzer) guns++;
            }
            return bridge ? 1 + Mathf.Min(MaxGunBonus, guns) : 0;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            int max = CountMax();
            if (max != MaxCharges)
            {
                if (max > MaxCharges) _charges += max - MaxCharges;   // 함포를 새로 놓으면 늘어난 만큼 바로
                MaxCharges = max;
                _charges = Mathf.Clamp(_charges, 0, MaxCharges);
            }
            if (_gap > 0f) _gap -= dt;
            if (_charges < MaxCharges)
            {
                _rechargeLeft -= dt * RunUpgrades.SkillRate;
                if (_rechargeLeft <= 0f) { _charges++; _rechargeLeft = _charges < MaxCharges ? RechargeSeconds : 0f; }
            }

            var gm = GameManager.Instance;
            if (gm != null && gm.State == GameState.Playing && (GameSettings.Pressed(NavalControl.Flare) ||
                (Gamepad.current != null && Gamepad.current.dpad.up.wasPressedThisFrame)))
                TryFire();

            _statusTimer -= Time.unscaledDeltaTime;
            if (_statusTimer > 0f) return;
            _statusTimer = 0.1f;
            float next = _charges > 0 ? 1f : 1f - Mathf.Clamp01(_rechargeLeft / RechargeSeconds);
            GameEvents.RaiseSkillStatusChanged(ActiveSkillId.Flare, _charges, MaxCharges, next);
        }

        /// <summary>검증용: 가득 채우고 발사 간격을 없앤다.</summary>
        public void DevRefill() { _charges = MaxCharges; _gap = 0f; }

        /// <summary>마우스가 가리킨 곳(없으면 앞쪽 30m)에 조명탄을 쏜다.</summary>
        public bool TryFire()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.State != GameState.Playing) return false;
            return FireAt(AimPoint());
        }

        /// <summary>이 지점에 조명탄을 쏜다(거리는 10~50m로 맞춘다).</summary>
        public bool FireAt(Vector3 point)
        {
            if (_charges <= 0 || _gap > 0f || _ship == null) return false;
            Vector3 from = _ship.transform.position;
            Vector3 d = point - from; d.y = 0f;
            float dist = d.magnitude;
            if (dist < 0.01f) { d = _ship.transform.forward; dist = 30f; }
            point = from + d.normalized * Mathf.Clamp(dist, MinRange, MaxRange);
            point.y = 0f;
            if (_charges == MaxCharges) _rechargeLeft = RechargeSeconds;
            _charges--;
            _gap = 1f;
            Fired++;
            Game.Combat.ParachuteFlare.Launch(from + Vector3.up * 2f, point);
            AudioManager.Play(Game.Data.SfxId.ChaffLaunch, from, 0.8f, 1.25f);
            return true;
        }

        private Vector3 AimPoint()
        {
            var cam = Camera.main;
            var mouse = Mouse.current;
            if (cam != null && mouse != null)
            {
                var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
                if (Mathf.Abs(ray.direction.y) > 1e-4f)
                {
                    float t = -ray.origin.y / ray.direction.y;
                    if (t > 0f) return ray.origin + ray.direction * t;
                }
            }
            return _ship.transform.position + _ship.transform.forward * 30f;
        }
    }
}

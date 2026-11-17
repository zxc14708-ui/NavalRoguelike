using UnityEngine;
using UnityEngine.InputSystem;
using Game.Core;
using Game.Combat;
using Game.Data;
using Game.Modules;

namespace Game.Ship
{
    /// <summary>함정 고유 스킬 하나(함정 종류마다 다르다).</summary>
    public interface ISignatureAbility
    {
        string Title { get; }
        Game.UI.HudTextures.SkillIcon Icon { get; }
        float Cooldown { get; }
        /// <summary>지금 쓸 수 있는가. 못 쓰면 안내 문구(쿨타임은 쓰지 않는다).</summary>
        bool CanActivate(SignatureSkill owner, out string reason);
        void Activate(SignatureSkill owner);
        /// <summary>매 프레임(발동 뒤 진행 중인 효과·연출).</summary>
        void Tick(SignatureSkill owner, float dt);
    }

    /// <summary>
    /// 함정 고유 스킬 [T](2026-10-11). 출항 때 고른 시작 함정(ShipInitializer.SelectedConcept)에 따라 하나가 붙는다.
    ///   지휘함 = 항공대 호출(<see cref="AirWingAbility"/>) · 미사일 구축함 = 전탄 발사(<see cref="FullSalvoAbility"/>) ·
    ///   고속 강습함 = 돌격 기동(<see cref="AssaultRunAbility"/>) · 범용 초계함 = 광역 탐색(<see cref="RadarSweepAbility"/>).
    /// 쿨타임은 성장 카드 "스킬 회복"(RunUpgrades.SkillRate)을 따른다. HUD 스킬 줄 여섯째 칸(그림은 함정마다 다르다).
    /// </summary>
    public class SignatureSkill : MonoBehaviour
    {
        public static SignatureSkill Active { get; private set; }

        public static void Ensure(ShipController ship)
        {
            if (ship != null && !ship.TryGetComponent<SignatureSkill>(out _)) ship.gameObject.AddComponent<SignatureSkill>();
        }

        public ShipController Ship { get; private set; }
        public ISignatureAbility Ability { get; private set; }
        public StartShipKind Kind { get; private set; }
        public Game.UI.HudTextures.SkillIcon Icon => Ability != null ? Ability.Icon : Game.UI.HudTextures.SkillIcon.Charge;
        public float CooldownLeft { get; private set; }
        public bool IsReady => CooldownLeft <= 0f && Ability != null;
        public int Uses { get; private set; }

        private ShipInitializer _initializer;
        private bool _kindResolved;
        private float _statusTimer;

        private void Awake()
        {
            Ship = GetComponent<ShipController>();
            Active = this;
        }

        private void OnEnable() => GameEvents.SkillRequested += OnRequested;
        private void OnDisable() => GameEvents.SkillRequested -= OnRequested;
        private void OnDestroy() { if (Active == this) Active = null; }

        private void OnRequested(ActiveSkillId id)
        {
            if (id == ActiveSkillId.Signature) TryActivate();
        }

        /// <summary>고른 함정이 바뀌면(출항 전 선택) 스킬도 바꾼다.</summary>
        private void ResolveKind()
        {
            if (_initializer == null) _initializer = FindFirstObjectByType<ShipInitializer>();
            var concept = _initializer != null ? _initializer.SelectedConcept : null;
            var kind = concept != null ? concept.Kind : StartShipKind.Patrol;
            if (_kindResolved && kind == Kind) return;
            _kindResolved = true;
            Kind = kind;
            Ability = Create(kind);
        }

        /// <summary>사전·안내용: 함정 종류의 고유 스킬 이름·한 줄 설명·재사용 시간(초, 스킬 회복 카드 전).</summary>
        public static (string title, string summary, float cooldown) Describe(StartShipKind kind)
        {
            var ability = Create(kind);
            if (ability == null) return ("없음", "고유 기술은 다음 패치(C)에서 정한다 — 구축함 기동.", 0f);
            string summary = kind switch
            {
                StartShipKind.Command =>
                    $"전투기 3기와 조기경보기 1기가 {AirWingAbility.Duration:0}초 동안 함께 싸운다 — 전투기는 둘레 60 m의 적을 기총으로 훑고, 조기경보기는 3초마다 70 m 안 잠수함을 드러낸다.",
                StartShipKind.MissileDestroyer =>
                    $"작동 중인 모든 VLS가 남은 셀을 {FullSalvoAbility.Spacing:0.##}초 간격으로 한꺼번에 쏜다 — 사거리 안 수상함과 항공기에 나눠 쏘고 전량 재장전.",
                StartShipKind.Assault =>
                    $"{AssaultRunAbility.Duration:0}초 동안 최고 속력 ×{AssaultRunAbility.SpeedMultiplier:0.#}, 받는 피해 −{(1f - AssaultRunAbility.DamageScale) * 100f:0}%, 뱃머리에 닿는 적 수상함을 들이받는다(피해 {AssaultRunAbility.RamDamage:0} × 속력).",
                _ =>
                    $"둘레 {RadarSweepAbility.Radius:0} m의 적을 {RadarSweepAbility.MarkSeconds:0}초 동안 표적 지시 — 받는 피해 +{(RadarSweepAbility.DamageMultiplier - 1f) * 100f:0}%, 잠항 중인 잠수함도 드러난다.",
            };
            return (ability.Title, summary, ability.Cooldown);
        }

        private static ISignatureAbility Create(StartShipKind kind) => kind switch
        {
            StartShipKind.Ww2Destroyer or StartShipKind.Ww2Cruiser or StartShipKind.Ww2Battleship => null,   // WWII 시험 전투: 현대 고유 스킬(미사일·항공대)을 쓰지 않는다(패치 A 4장)
            StartShipKind.Command => new AirWingAbility(),
            StartShipKind.MissileDestroyer => new FullSalvoAbility(),
            StartShipKind.Assault => new AssaultRunAbility(),
            _ => new RadarSweepAbility(),
        };

        private bool _forced;

        /// <summary>검사·디버그용: 함정 종류를 직접 정한다(고른 함정과 상관없이).</summary>
        public void ForceKind(StartShipKind kind)
        {
            _forced = _kindResolved = true;
            Kind = kind;
            Ability = Create(kind);
            CooldownLeft = 0f;
        }

        private void Update()
        {
            if (!_forced && (!_kindResolved || (_initializer != null && _initializer.SelectedConcept != null && _initializer.SelectedConcept.Kind != Kind)))
                ResolveKind();
            if (Ability == null) return;

            float dt = Time.deltaTime;
            Ability.Tick(this, dt);
            if (CooldownLeft > 0f) CooldownLeft = Mathf.Max(0f, CooldownLeft - dt * RunUpgrades.SkillRate);

            if (GameSettings.Pressed(NavalControl.Signature)) TryActivate();
            else if (Gamepad.current != null && Gamepad.current.leftShoulder.wasPressedThisFrame) TryActivate();

            _statusTimer -= Time.unscaledDeltaTime;
            if (_statusTimer > 0f) return;
            _statusTimer = 0.1f;
            float ready01 = Ability.Cooldown > 0f ? 1f - Mathf.Clamp01(CooldownLeft / Ability.Cooldown) : 1f;
            GameEvents.RaiseSkillStatusChanged(ActiveSkillId.Signature, IsReady ? 1 : 0, 1, ready01);
        }

        /// <summary>쓴다. 전투 중이 아니거나 쿨타임이면 무시, 조건이 안 되면 이유를 띄운다. 썼으면 true.</summary>
        public bool TryActivate()
        {
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return false;
            if (Ability == null || CooldownLeft > 0f || Ship == null || !Ship.IsAlive) return false;
            if (!Ability.CanActivate(this, out string reason))
            {
                if (!string.IsNullOrEmpty(reason)) GameEvents.RaiseBanner($"{Ability.Title} — {reason}");
                return false;
            }
            Ability.Activate(this);
            CooldownLeft = Ability.Cooldown;
            Uses++;
            CombatLog.Add("고유 스킬", Ability.Title);
            return true;
        }
    }
}

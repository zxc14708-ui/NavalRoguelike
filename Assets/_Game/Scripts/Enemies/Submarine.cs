using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 후반 등장하는 어뢰 잠수함. PreferredRange에서 저속으로 맴돌며 어뢰를 쏜다.
    /// 어뢰 발사는 발사 당시 위치의 흔적을 남기지만 잠수함 자체를 부상시키지 않는다.
    /// 잠항·부상·노출은 SubmarineBase가 맡는다.
    ///
    /// 발사 절차(피할 수 있는 공격)
    ///   1) 사격 제원 결정: 그 순간 함선의 위치·속력·침로를 기록하고 경고(물거품·경고음·화면 "어뢰 준비")를 띄운다.
    ///   2) firingSolutionTime 뒤 발사: 기록한 속력·침로 그대로 갔을 때 어뢰와 만나는 점으로 곧게 쏜다.
    /// 경고를 본 뒤 전령기를 한 칸 바꾸거나 타를 쓰면 빗나가고, 그대로 가면 맞는다.
    /// </summary>
    public class Submarine : SubmarineBase
    {
        private static readonly List<Submarine> s_preparing = new();

        /// <summary>사격 제원을 잡고 발사를 기다리는 잠수함(화면 경고용).</summary>
        public static IReadOnlyList<Submarine> Preparing => s_preparing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_preparing.Clear();

        [Header("Torpedo")]
        [SerializeField] private GameObject torpedoPrefab;
        [Tooltip("이 거리 안에서만 어뢰를 쏜다")]
        [SerializeField] private float torpedoRange = 70f;
        [Tooltip("이보다 가까우면 쏘지 않는다. 가까운 곳에서 쏘면 도착까지 시간이 짧아 피할 수 없다(어뢰 12m/s × 경고 2초 + 비행 3초 이상이면 피할 시간이 있다).")]
        [SerializeField] private float minFireRange = 40f;
        [Tooltip("모든 잠수함을 합쳐 동시에 물속에 있거나 발사 준비 중인 어뢰의 최대 수. 넘으면 다른 어뢰가 사라질 때까지 기다린다.")]
        [SerializeField] private int maxTorpedoesAtOnce = 2;
        [Tooltip("등장 직후 첫 발까지 기다리는 비율(공격 간격 대비). 나타나자마자 쏘지 않게.")]
        [SerializeField, Range(0f, 1f)] private float openingDelayRatio = 0.6f;
        [Tooltip("어뢰 발사 위치의 수색 단서가 남는 시간. 현재 잠수함 위치는 공개하지 않는다")]
        [SerializeField] private float launchRevealSeconds = 6f;
        [Tooltip("사격 제원을 정한 뒤 발사까지(경고가 떠 있는 시간). 이 동안의 조함이 곧 회피다.")]
        [SerializeField] private float firingSolutionTime = 2f;
        [Tooltip("경고 때 잠수함 위에 띄우는 물거품(명중 물기둥 이펙트 재사용) 크기")]
        [SerializeField] private float warningBubbleScale = 0.25f;

        [Header("Model")]
        [Tooltip("스크루(선택, 미니어처 적 v9). 속력에 비례해 돈다.")]
        [SerializeField] private Transform propeller;
        [SerializeField] private float propellerRpm = 90f;

        private bool _preparing;
        private float _solutionTime;
        private Vector3 _solutionPos, _solutionVel;

        public bool IsPreparingTorpedo => _preparing;

        /// <summary>대함미사일·어뢰는 호위함에 쓰지 않는다(호위함이 한 방에 무너지는 것을 막고, 위협은 기함에 모은다).</summary>
        protected override bool TargetsEscorts => false;

        public override void Setup(Game.Data.EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            // 등장하자마자 쏘지 않는다: 공격 간격의 일부는 기다린 뒤 첫 사격
            if (def != null) AttackTimer = def.AttackCooldown * openingDelayRatio;
            CancelSolution();
        }

        /// <summary>지금 물속을 달리는 어뢰와 발사 준비 중인 잠수함·어뢰정의 합(전체 동시 수 제한용).</summary>
        public static int TorpedoesInPlay => Torpedo.Active.Count + s_preparing.Count + TorpedoBoat.PreparingCount;

        protected override void UpdateBehaviour(float dt)
        {
            TickStealth(dt);
            if (propeller != null && CurrentSpeed > 0.05f)
                propeller.Rotate(transform.forward, propellerRpm * 6f * dt * (CurrentSpeed / Mathf.Max(0.1f, Definition.MoveSpeed)), Space.World);

            float dist = Vector3.Distance(transform.position, Player.position);
            OrbitPlayer(Definition.PreferredRange, 0.5f, dt);

            if (_preparing)
            {
                if (PlayerConcealed) { CancelSolution(); return; }   // 연막: 표적을 놓친다
                if (Time.time - _solutionTime < firingSolutionTime) return;
                LaunchTorpedo();
                ReportLaunchSignature(launchRevealSeconds);
                CancelSolution();
                return;
            }

            if (AttackTimer > 0f || PlayerConcealed || PlayerBehindIsland) return;
            if (dist > torpedoRange || dist < minFireRange) return;   // 너무 가까우면 쏘지 않고 선회로 거리를 벌린다
            if (TorpedoesInPlay >= maxTorpedoesAtOnce) return;        // 여러 척이 한꺼번에 쏘지 않는다

            BeginSolution();
            AttackTimer = Definition.AttackCooldown;
        }

        private void BeginSolution()
        {
            _preparing = true;
            _solutionTime = Time.time;
            _solutionPos = Player.position;
            _solutionVel = PlayerShip != null ? Flat(Player.forward) * PlayerShip.CurrentSpeed : Vector3.zero;
            if (!s_preparing.Contains(this)) s_preparing.Add(this);

            // 경고: 발사관 주수 물거품 + 높은 소나 핑
            var torpedoData = torpedoPrefab != null ? torpedoPrefab.GetComponent<Torpedo>() : null;
            var p = transform.position;
            if (torpedoData != null) PooledEffect.Spawn(torpedoData.ImpactEffect, new Vector3(p.x, 0f, p.z), warningBubbleScale);
            AudioManager.Play(Game.Data.SfxId.SonarPing, p, 0.9f, 1.7f);
            CombatLog.Add("어뢰", $"{Definition.DisplayName} 사격 제원 결정 · {firingSolutionTime:0.0}초 뒤 발사");
        }

        private void CancelSolution()
        {
            _preparing = false;
            s_preparing.Remove(this);
        }

        private void LaunchTorpedo()
        {
            var go = torpedoPrefab != null
                ? PoolManager.Instance?.Spawn(torpedoPrefab, transform.position + transform.forward * 3f, transform.rotation)
                : null;

            if (go != null && go.TryGetComponent<Torpedo>(out var torpedo))
            {
                // 제원을 정한 순간의 속력·침로 그대로 갔을 때 어뢰와 만나는 점(제원 이후 흐른 시간 포함)
                Vector3 from = go.transform.position;
                float elapsed = Time.time - _solutionTime;
                float travel = Vector3.Distance(from, _solutionPos) / Mathf.Max(0.1f, torpedo.Speed);
                Vector3 aim = _solutionPos;
                for (int i = 0; i < 4; i++)
                {
                    aim = _solutionPos + _solutionVel * (elapsed + travel);
                    travel = Vector3.Distance(from, aim) / Mathf.Max(0.1f, torpedo.Speed);
                }
                torpedo.Launch(aim, Definition.AttackDamage);
                return;
            }

            // 방어 코드: 어뢰 프리팹이 없으면 즉시 피해로 대신한다
            if (Player.TryGetComponent<IDamageable>(out var target) && target.IsAlive)
                target.TakeDamage(new DamageInfo(Definition.AttackDamage, Player.position, Vector3.up, DamageSource.Torpedo));
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }

        public override void OnSpawned()
        {
            base.OnSpawned();
            CancelSolution();
        }

        public override void OnDespawned()
        {
            base.OnDespawned();
            CancelSolution();
        }
    }
}

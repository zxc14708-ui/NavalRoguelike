using System.Collections;
using UnityEngine;
using Game.Core;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 순항미사일 원자력 잠수함. 어뢰 잠수함(Submarine)과 달리 멀리(PreferredRange 약 52)서 맴돌며
    /// 대함 순항미사일을 쏜다. 가까이 오지 않으므로 소나 범위 밖에 머무는 일이 많다.
    ///
    /// 공격 절차
    ///   잠항 → 잠망경을 올리며 부상(prepTime) → 미사일 발사(SalvoSize, SalvoSpacing)
    ///   → 발사 직후 다시 잠항. launchRevealSeconds 동안 마지막 발사 위치만 남아 헬기 수색 단서가 된다.
    /// 미사일은 기존 적 대함미사일(Missile)을 그대로 써서 기만체·CIWS·재밍의 영향을 받는다. 어뢰는 쏘지 않는다.
    /// </summary>
    public class CruiseMissileSubmarine : SubmarineBase
    {
        [Header("Cruise Missile")]
        [Tooltip("미사일이 나가는 자리(MissileLaunchPoint_01 …). 비어 있으면 선체 중심 위에서 쏜다.")]
        [SerializeField] private Transform[] launchPoints;
        [Tooltip("PreferredRange의 이 배수 안에 플레이어가 있으면 쏜다")]
        [SerializeField] private float fireRangeMultiplier = 1.2f;
        [Tooltip("잠망경을 올리고 부상한 뒤 첫 발까지")]
        [SerializeField] private float prepTime = 1.2f;
        [Tooltip("발사 뒤 마지막 발사 위치의 수색 단서가 남는 시간")]
        [SerializeField] private float launchRevealSeconds = 5.5f;
        [Tooltip("미사일이 수직 발사관에서 솟아오르는 각도(0 = 수평, 90 = 수직)")]
        [SerializeField, Range(0f, 90f)] private float launchPitch = 55f;
        [Tooltip("평소 선회 속력 비율")]
        [SerializeField, Range(0f, 1f)] private float cruiseThrottle = 0.5f;

        [Header("Periscope / Hatches (연출)")]
        [Tooltip("공격 준비 때 올라오는 부품(잠망경 기둥·머리)")]
        [SerializeField] private Transform[] periscopeParts;
        [Tooltip("평소에는 이만큼(월드) 내려 두었다가 공격 준비 때 올린다")]
        [SerializeField] private float periscopeTravel = 0.8f;
        [SerializeField] private float periscopeSpeed = 2f;
        [Tooltip("발사하는 동안 열어 두는 발사관 덮개")]
        [SerializeField] private GameObject[] hatchCovers;
        [Tooltip("추진기(선택). 선체 앞뒤 축으로 돈다.")]
        [SerializeField] private Transform propeller;
        [SerializeField] private float propellerRpm = 90f;

        private Vector3[] _periscopeRest;
        private float _periscopeLift;       // 0 = 내림, 1 = 올림
        private float _periscopeTarget;
        private Coroutine _attack;
        private int _nextLaunchPoint;

        /// <summary>공격 절차 중인가(준비·발사·노출). 개발·검증용.</summary>
        public bool IsAttacking => _attack != null;
        /// <summary>지금까지 쏜 미사일 수(개발·검증용). 스폰마다 0.</summary>
        public int MissilesFired { get; private set; }

        /// <summary>대함미사일·어뢰는 호위함에 쓰지 않는다(호위함이 한 방에 무너지는 것을 막고, 위협은 기함에 모은다).</summary>
        protected override bool TargetsEscorts => false;

        protected override void UpdateBehaviour(float dt)
        {
            TickStealth(dt);
            UpdateVisuals(dt);

            // 멀리서 맴돈다. 가까우면 OrbitPlayer가 바깥쪽으로 돌아 나간다.
            OrbitPlayer(Definition.PreferredRange, cruiseThrottle, dt);

            if (_attack != null || AttackTimer > 0f || PlayerConcealed || PlayerBehindIsland) return;
            if (Definition.MissilePrefab == null)
            {
                Debug.LogWarning($"[CruiseMissileSubmarine] {Definition.DisplayName}에 MissilePrefab이 없습니다.", this);
                AttackTimer = Definition.AttackCooldown;
                return;
            }

            float dist = Vector3.Distance(transform.position, Player.position);
            if (dist > Definition.PreferredRange * fireRangeMultiplier) return;

            AttackTimer = Definition.AttackCooldown;
            _attack = StartCoroutine(AttackRoutine());
        }

        private IEnumerator AttackRoutine()
        {
            // 1) 준비: 잠망경을 올리며 부상한다. 이때부터 이미 보인다.
            _periscopeTarget = 1f;
            RevealFor(prepTime + 0.5f);
            CombatLog.Add("적 잠수함", $"{Definition.DisplayName} 발사 준비(부상)");
            yield return new WaitForSeconds(prepTime);

            if (!IsAlive || Player == null || PlayerConcealed)
            {
                EndAttack();
                yield break;
            }

            // 2) 발사
            SetHatches(open: true);
            int count = Definition.SalvoSize;
            int firedBefore = MissilesFired;
            for (int i = 0; i < count; i++)
            {
                if (!IsAlive || Player == null || PlayerConcealed) break;
                LaunchMissile();
                if (i < count - 1) yield return new WaitForSeconds(Definition.SalvoSpacing);
            }

            // 3) 선체는 바로 사라지지만 발사 위치는 잠시 남아 소나·헬기가 추적할 수 있다.
            if (MissilesFired > firedBefore) ReportLaunchSignature(launchRevealSeconds);
            DiveAfterLaunch();
            yield return new WaitForSeconds(0.6f);
            EndAttack();
        }

        private void LaunchMissile()
        {
            var origin = NextLaunchPoint();
            Vector3 pos = origin != null ? origin.position : transform.position + Vector3.up * 1.2f;
            pos.y = Mathf.Max(pos.y, 0.6f);   // 잠망경 심도에서도 수면 위에서 나가게

            Vector3 flat = Player.position - pos;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) flat = transform.forward;
            Vector3 dir = Vector3.Slerp(flat.normalized, Vector3.up, launchPitch / 90f);

            var go = PoolManager.Instance?.Spawn(Definition.MissilePrefab, pos, Quaternion.LookRotation(dir, Vector3.up));
            var missile = go != null ? go.GetComponent<Missile>() : null;
            if (missile == null) return;

            missile.Launch(Player, Definition.AttackDamage);
            MissilesFired++;
            CombatLog.Add("적 잠수함", $"{Definition.DisplayName} → {missile.LogName} 순항미사일 발사");
        }

        /// <summary>발사 위치를 차례로 돌려 쓴다. 비었거나 모두 없으면 null(선체 중심 위).</summary>
        private Transform NextLaunchPoint()
        {
            if (launchPoints == null || launchPoints.Length == 0) return null;
            for (int tries = 0; tries < launchPoints.Length; tries++)
            {
                var p = launchPoints[_nextLaunchPoint % launchPoints.Length];
                _nextLaunchPoint++;
                if (p != null) return p;
            }
            return null;
        }

        private void EndAttack()
        {
            _attack = null;
            _periscopeTarget = 0f;
            SetHatches(open: false);
        }

        // ------------------------------------------------------------ 연출

        private void UpdateVisuals(float dt)
        {
            if (propeller != null && CurrentSpeed > 0.05f)
                propeller.Rotate(transform.forward, propellerRpm * 6f * dt * (CurrentSpeed / Mathf.Max(0.1f, Definition.MoveSpeed)), Space.World);

            if (Mathf.Approximately(_periscopeLift, _periscopeTarget)) return;
            _periscopeLift = Mathf.MoveTowards(_periscopeLift, _periscopeTarget, periscopeSpeed * dt);
            ApplyPeriscope();
        }

        private void ApplyPeriscope()
        {
            if (periscopeParts == null) return;
            CachePeriscopeRest();
            float down = (1f - _periscopeLift) * periscopeTravel;
            for (int i = 0; i < periscopeParts.Length; i++)
            {
                var part = periscopeParts[i];
                if (part == null) continue;
                // 부모(FBX 루트)는 축이 돌아가 있고 배율이 있으므로 월드 아래 방향을 부모 공간으로 바꿔 더한다
                Vector3 offset = part.parent != null ? part.parent.InverseTransformVector(Vector3.down * down) : Vector3.down * down;
                part.localPosition = _periscopeRest[i] + offset;
            }
        }

        private void CachePeriscopeRest()
        {
            if (_periscopeRest != null && _periscopeRest.Length == periscopeParts.Length) return;
            _periscopeRest = new Vector3[periscopeParts.Length];
            for (int i = 0; i < periscopeParts.Length; i++)
                _periscopeRest[i] = periscopeParts[i] != null ? periscopeParts[i].localPosition : Vector3.zero;
        }

        private void SetHatches(bool open)
        {
            if (hatchCovers == null) return;
            foreach (var h in hatchCovers)
                if (h != null) h.SetActive(!open);
        }

        // ------------------------------------------------------------ 풀

        public override void Setup(Game.Data.EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            // 등장하자마자 쏘지 않게: 한 주기의 절반은 기다린다
            AttackTimer = def != null ? def.AttackCooldown * 0.5f : 0f;
        }

        public override void OnSpawned()
        {
            base.OnSpawned();
            ResetAttack();
        }

        public override void OnDespawned()
        {
            base.OnDespawned();
            ResetAttack();
        }

        /// <summary>사망·스테이지 정리·풀 반환 때 공격 절차와 연출을 처음 상태로 돌린다.</summary>
        private void ResetAttack()
        {
            if (_attack != null) StopCoroutine(_attack);
            StopAllCoroutines();
            _attack = null;
            MissilesFired = 0;
            _nextLaunchPoint = 0;
            _periscopeTarget = 0f;
            _periscopeLift = 0f;
            ApplyPeriscope();
            SetHatches(open: false);
        }
    }
}

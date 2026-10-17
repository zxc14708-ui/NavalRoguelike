using System.Collections;
using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Data;
using Game.Enemies;
using Game.Ship;

namespace Game.WW2
{
    /// <summary>
    /// 임시 포격 보스 — 적 순양함(WWII 패치 B, ww2_boss_gunnery_cruiser). 최종 모델 대신 회색 실루엣(포탑 셋).
    ///   - 주포 일제 사격: 기함 둘레를 돌며 <see cref="salvoInterval"/>초마다 포탄 여러 발. 포탄이 날아가는 동안(약 2.8초)
    ///     떨어질 자리마다 수면에 붉은 원이 차오른다(ArtilleryShell) — 원 밖으로 빠지면 맞지 않는다.
    ///     1단계 "협차": 기함 예상 위치를 가운데 두고 침로를 따라 앞뒤로 늘어선다(속력을 바꾸면 빠져나간다).
    ///     2단계(체력 50% 아래) "집중 일제": 간격이 짧아지고 예상 위치 좌우로 두 줄(타를 써서 빠져나간다).
    ///   - 부포: 가까우면(<see cref="secondaryRange"/>) 기관포탄을 짧게 쏜다.
    ///   - 포탑이 기함을 향해 돌아야 쏜다(보이는 예고). 연막·섬 뒤면 주포를 쏘지 않는다.
    /// 어뢰 일제 발사에 크게 맞는 표적이다(큰 선체). 격침하면 시험 전투 승리.
    /// </summary>
    public class Ww2GunneryBoss : EnemyController, IBossTelegraph
    {
        [Header("Main Battery")]
        [SerializeField] private Transform[] turrets = System.Array.Empty<Transform>();
        [SerializeField] private Transform[] muzzles = System.Array.Empty<Transform>();
        [SerializeField] private GameObject shellPrefab;
        [SerializeField] private GameObject muzzleFlash;
        [SerializeField] private float turretTurnRate = 40f;
        [SerializeField] private float salvoInterval = 8f;
        [SerializeField] private float salvoIntervalPhase2 = 6f;
        [SerializeField] private int shellsPhase1 = 5;
        [SerializeField] private int shellsPhase2 = 8;
        [SerializeField] private float shellFlightTime = 2.8f;
        [SerializeField] private float blastRadius = 3.2f;
        [SerializeField] private float straddleSpacing = 6f;
        [SerializeField] private float maxSalvoRange = 70f;

        [Header("Secondary")]
        [SerializeField] private Transform[] secondaryMuzzles = System.Array.Empty<Transform>();
        [SerializeField] private GameObject secondaryProjectile;
        [SerializeField] private float secondaryRange = 24f;
        [SerializeField] private float secondaryInterval = 1.2f;
        [SerializeField] private float secondaryDamage = 4f;
        [SerializeField] private float secondarySpeed = 46f;

        private static readonly float[] s_thresholds = { 0.5f };
        private ShipController _playerShip;
        private Collider _playerHull;
        private float _salvoTimer, _secondaryTimer;
        private Coroutine _salvo;
        private int _secondaryIndex;
        private Quaternion[] _turretRest;

        public bool Phase2 => HpFraction <= 0.5f;
        public int SalvosFired { get; private set; }
        public string PhaseName => Phase2 ? "2단계 — 집중 일제 사격(좌우 두 줄)" : "1단계 — 협차 사격(침로를 따라 앞뒤)";
        public float[] PhaseThresholds => s_thresholds;
        public (string name, float seconds) NextAttack => ("주포 일제 사격", Mathf.Max(0f, _salvoTimer));

        protected override bool TargetsEscorts => false;

        public override void Setup(EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            _playerShip = player != null ? player.GetComponent<ShipController>() : null;
            _playerHull = player != null ? player.GetComponent<Collider>() : null;
            _salvoTimer = 4f;
            _secondaryTimer = 1f;
            if (_salvo != null) StopCoroutine(_salvo);
            _salvo = null;
            if (_turretRest == null || _turretRest.Length != turrets.Length)
            {
                _turretRest = new Quaternion[turrets.Length];
                for (int i = 0; i < turrets.Length; i++) _turretRest[i] = turrets[i] != null ? turrets[i].localRotation : Quaternion.identity;
            }
            else for (int i = 0; i < turrets.Length; i++) if (turrets[i] != null) turrets[i].localRotation = _turretRest[i];
        }

        protected override void UpdateBehaviour(float dt)
        {
            OrbitPlayer(Definition.PreferredRange, 0.55f, dt);
            Vector3 predicted = Predicted(shellFlightTime);
            bool aimed = TrainTurrets(predicted, dt);

            _salvoTimer -= dt;
            _secondaryTimer -= dt;
            float dist = Vector3.Distance(transform.position, Player.position);

            if (_secondaryTimer <= 0f && dist <= secondaryRange && !PlayerConcealed)
            {
                _secondaryTimer = secondaryInterval;
                FireSecondary();
            }

            if (_salvoTimer > 0f || _salvo != null || !aimed) return;
            if (PlayerConcealed || PlayerBehindIsland || dist > maxSalvoRange) { _salvoTimer = 1f; return; }
            _salvoTimer = Phase2 ? salvoIntervalPhase2 : salvoInterval;
            _salvo = StartCoroutine(Salvo(predicted, Phase2));
        }

        private Vector3 Predicted(float seconds)
        {
            Vector3 v = _playerShip != null ? Player.forward * _playerShip.CurrentSpeed : Vector3.zero;
            v.y = 0f;
            Vector3 p = Player.position + v * seconds;
            p.y = 0f;
            return p;
        }

        /// <summary>포탑을 예상 위치로 돌린다. 하나라도 20° 안이면 쏠 수 있다.</summary>
        private bool TrainTurrets(Vector3 point, float dt)
        {
            bool any = false;
            for (int i = 0; i < turrets.Length; i++)
            {
                var t = turrets[i];
                if (t == null || t.parent == null) continue;
                Vector3 local = t.parent.InverseTransformPoint(point) - t.localPosition;
                local.y = 0f;
                if (local.sqrMagnitude < 0.01f) continue;
                Vector3 rest = _turretRest[i] * Vector3.forward;
                rest.y = 0f;
                var want = Quaternion.AngleAxis(Vector3.SignedAngle(rest, local, Vector3.up), Vector3.up) * _turretRest[i];
                t.localRotation = Quaternion.RotateTowards(t.localRotation, want, turretTurnRate * dt);
                if (Quaternion.Angle(t.localRotation, want) < 20f) any = true;
            }
            return any || turrets.Length == 0;
        }

        private IEnumerator Salvo(Vector3 predicted, bool phase2)
        {
            SalvosFired++;
            Vector3 fwd = Player.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            int count = phase2 ? shellsPhase2 : shellsPhase1;
            for (int i = 0; i < count; i++)
            {
                if (!IsAlive || Player == null) break;
                Vector3 offset;
                if (!phase2)
                    offset = fwd * ((i - (count - 1) * 0.5f) * straddleSpacing);                       // 침로를 따라 앞뒤로
                else
                {
                    int row = i % 2 == 0 ? -1 : 1, col = i / 2;
                    offset = right * (row * blastRadius * 1.25f) + fwd * ((col - (count / 2 - 1) * 0.5f) * straddleSpacing * 0.8f);   // 좌우 두 줄
                }
                Vector2 jitter = Random.insideUnitCircle * 0.6f;
                Vector3 impact = predicted + offset + new Vector3(jitter.x, 0f, jitter.y);
                impact.y = 0f;
                var muzzle = muzzles.Length > 0 && muzzles[i % muzzles.Length] != null ? muzzles[i % muzzles.Length] : transform;
                var go = PoolManager.Instance?.Spawn(shellPrefab, muzzle.position, muzzle.rotation);
                go?.GetComponent<ArtilleryShell>()?.Launch(muzzle.position, impact, shellFlightTime + i * 0.05f, Definition.AttackDamage,
                                                           blastRadius, _playerHull, Ww2BattleLog.BossSalvo);
                PooledEffect.Spawn(muzzleFlash, muzzle.position);
                AudioManager.Play(SfxId.NavalGunShot, muzzle.position, 1f, 0.5f);
                yield return new WaitForSeconds(0.12f);
            }
            Ww2BattleLog.Released(Ww2BattleLog.BossSalvo);
            _salvo = null;
        }

        private void FireSecondary()
        {
            if (secondaryProjectile == null || secondaryMuzzles.Length == 0) return;
            var muzzle = secondaryMuzzles[_secondaryIndex++ % secondaryMuzzles.Length];
            if (muzzle == null) return;
            Vector3 target = Player.position + Vector3.up * 0.8f;
            Vector3 v = _playerShip != null ? Player.forward * _playerShip.CurrentSpeed : Vector3.zero;
            Vector3 aim = Ballistics.PredictIntercept(muzzle.position, target, v, secondarySpeed);
            Vector3 dir = (aim - muzzle.position).normalized;
            for (int i = 0; i < 3; i++)
            {
                Vector3 d = Quaternion.Euler(Random.Range(-1.2f, 1.2f), Random.Range(-1.5f, 1.5f), 0f) * dir;
                var go = PoolManager.Instance?.Spawn(secondaryProjectile, muzzle.position, Quaternion.LookRotation(d));
                go?.GetComponent<Projectile>()?.Launch(d, secondarySpeed, secondaryDamage / 3f, DamageSource.Gun);
            }
            AudioManager.Play(SfxId.AutocannonShot, muzzle.position, 0.5f, 1.1f);
        }

        public override void OnDespawned()
        {
            if (_salvo != null) StopCoroutine(_salvo);
            _salvo = null;
            base.OnDespawned();
        }

        protected override void Die()
        {
            base.Die();
            GameEvents.RaiseBossDefeated();
        }
    }
}

using UnityEngine;
using Game.Core;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 중반 등장. 거리를 유지하며 대함 미사일을 쏜다.
    /// 이 적이 나오는 순간부터 회피만으로는 버틸 수 없고 CIWS/기만체가 필요해진다.
    /// 데이터의 SalvoSize만큼 짧은 간격(SalvoSpacing)으로 연달아 쏜다 — CIWS 교전 채널과 기만체의 한계를 시험하는 포화 공격.
    /// </summary>
    public class MissileBoat : EnemyController
    {
        [SerializeField] private Transform launchPoint;
        [SerializeField] private Transform launcherPivot;
        [SerializeField, Min(1f)] private float launcherTurnSpeed = 120f;
        [SerializeField, Range(1f, 30f)] private float fireAngleTolerance = 8f;

        private bool _launcherPrepared;

        public override void Setup(Game.Data.EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            PrepareLauncher();
        }

        /// <summary>대함미사일·어뢰는 호위함에 쓰지 않는다(호위함이 한 방에 무너지는 것을 막고, 위협은 기함에 모은다).</summary>
        protected override bool TargetsEscorts => false;

        protected override void UpdateBehaviour(float dt)
        {
            // 후진하지 않는다. 사거리 원을 그리며 돌고, 너무 가까우면 바깥으로 돌아 나간다.
            OrbitPlayer(Definition.PreferredRange, 0.7f, dt);
            AimLauncher(dt);

            if (AttackTimer > 0f || PlayerConcealed || PlayerBehindIsland) return;
            if (Definition.MissilePrefab == null)
            {
                Debug.LogWarning($"[MissileBoat] {Definition.DisplayName}에 MissilePrefab이 없습니다.", this);
                AttackTimer = Definition.AttackCooldown;
                return;
            }

            float dist = Vector3.Distance(transform.position, Player.position);
            if (dist > Definition.PreferredRange * 1.5f) return;

            AttackTimer = Definition.AttackCooldown;
            StartCoroutine(FireSalvo());
        }

        private System.Collections.IEnumerator FireSalvo()
        {
            int count = Definition.SalvoSize;
            for (int i = 0; i < count; i++)
            {
                if (!IsAlive || Player == null || PlayerConcealed) yield break;

                // 배가 선회해도 발사관은 독립적으로 표적을 따라간다. 조준이 끝나기 전에는 발사하지 않는다.
                while (!IsAimed())
                {
                    if (!IsAlive || Player == null || PlayerConcealed) yield break;
                    yield return null;
                }

                var origin = launchPoint != null ? launchPoint : transform;
                var go = PoolManager.Instance?.Spawn(Definition.MissilePrefab, origin.position, origin.rotation);
                go?.GetComponent<Missile>()?.Launch(Player, Definition.AttackDamage);

                if (i < count - 1) yield return new WaitForSeconds(Definition.SalvoSpacing);
            }
        }

        private void PrepareLauncher()
        {
            if (_launcherPrepared) return;
            launcherPivot ??= FindChild(transform, "MissileLauncherPivot");
            if (launcherPivot == null) return;

            var assembly = launcherPivot.parent;
            if (assembly == null) return;
            launchPoint ??= FindChild(launcherPivot, "MissileLaunchPoint");
            if (launchPoint == null)
            {
                launchPoint = new GameObject("MissileLaunchPoint").transform;
                launchPoint.SetParent(launcherPivot, false);
            }

            // 구형 FBX는 피벗이 관 입구에 있고 관들이 형제여서 포인트만 회전했다.
            // 구형 프리팹도 즉시 동작하도록 관 묶음을 피벗 아래로 옮기고 축을 받침 중앙으로 보정한다.
            if (launcherPivot.childCount <= (launchPoint != null ? 1 : 0))
            {
                Vector3 socketPosition = launchPoint != null ? launchPoint.position : Vector3.zero;
                Quaternion socketRotation = launchPoint != null ? launchPoint.rotation : Quaternion.identity;
                launcherPivot.position = assembly.position;
                if (launchPoint != null) launchPoint.SetPositionAndRotation(socketPosition, socketRotation);

                for (int j = assembly.childCount - 1; j >= 0; j--)
                {
                    var child = assembly.GetChild(j);
                    if (child == launcherPivot || child.name.StartsWith("LauncherBase")) continue;
                    child.SetParent(launcherPivot, true);
                }
            }

            // 기존 프리팹 소켓은 옛 피벗(관 입구)에 로컬 0으로 붙어 있다.
            // 피벗을 받침으로 옮긴 뒤 실제 관 입구와 발사 방향에서 다시 계산한다.
            var back = FindChild(launcherPivot, "MissileCanister_02");
            var mouth = FindChild(launcherPivot, "CanisterFront_02");
            var otherMouth = FindChild(launcherPivot, "CanisterFront_03");
            if (launchPoint != null && back != null && mouth != null)
            {
                Vector3 direction = mouth.position - back.position;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    Vector3 exit = otherMouth != null ? (mouth.position + otherMouth.position) * 0.5f : mouth.position;
                    launchPoint.SetPositionAndRotation(exit, Quaternion.LookRotation(direction, Vector3.up));
                }
            }

            _launcherPrepared = true;
        }

        private void AimLauncher(float dt)
        {
            if (Player == null || launchPoint == null || launcherPivot == null) return;
            Vector3 desired = Player.position - launchPoint.position;
            desired.y = 0f;
            Vector3 current = launchPoint.forward;
            current.y = 0f;
            if (desired.sqrMagnitude < 0.01f || current.sqrMagnitude < 0.01f) return;

            float angle = Vector3.SignedAngle(current, desired, Vector3.up);
            float step = Mathf.Clamp(angle, -launcherTurnSpeed * dt, launcherTurnSpeed * dt);
            launcherPivot.Rotate(Vector3.up, step, Space.World);
        }

        private bool IsAimed()
        {
            if (Player == null || launchPoint == null || launcherPivot == null) return true;
            Vector3 desired = Player.position - launchPoint.position;
            desired.y = 0f;
            Vector3 current = launchPoint.forward;
            current.y = 0f;
            return desired.sqrMagnitude < 0.01f || current.sqrMagnitude < 0.01f ||
                   Vector3.Angle(current, desired) <= fireAngleTolerance;
        }

        private static Transform FindChild(Transform root, string wanted)
        {
            if (root.name == wanted) return root;
            foreach (Transform child in root)
            {
                var found = FindChild(child, wanted);
                if (found != null) return found;
            }
            return null;
        }
    }
}

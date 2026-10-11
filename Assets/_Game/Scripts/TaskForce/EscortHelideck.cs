using System;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Enemies;

namespace Game.TaskForce
{
    /// <summary>
    /// 대잠 호위함의 헬기 갑판(2026-10-11 호위함 실제 무장). 함선 헬기갑판 블록과 같은 대잠 헬기(HEL_Asw)를 띄운다.
    ///   - 출격: 헬기가 갑판에 있고 재출격 대기가 끝났으면, 호위함 50m 안의 잠수함(드러났거나 접촉 확정) → 없으면 35m 안의 수상함,
    ///     둘 다 없어도 55m 안에 잠수함이 있으면 그 마지막 위치로 수색 비행.
    ///   - 헬기는 잠수함 위를 돌며 접촉을 확정(디핑 소나)하고 폭뢰, 수상함에는 유도로켓. 할 일이 없으면 갑판으로 돌아온다.
    ///   - 착함하면 재출격 대기 18 − 3×개량 단계 초. 호위함이 전투 불능이면 헬기도 돌아간다(사라진다).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EscortHelideck : MonoBehaviour, IHelicopterBase
    {
        public const float SubRange = 50f, SurfaceRange = 35f, CueRange = 55f, OperateRange = 60f;

        private Func<bool> _ready;
        private Func<int> _tier;
        private Func<Transform> _spot;
        private float _readyAt;
        private bool _airborne;

        public int Sorties { get; private set; }
        public bool Airborne => _airborne;

        public void Init(Func<bool> ready, Func<int> tier, Func<Transform> spot)
        {
            _ready = ready;
            _tier = tier;
            _spot = spot;
            _readyAt = Time.time + 4f;
        }

        public bool CanOperate => isActiveAndEnabled && (_ready == null || _ready());
        public HelicopterPolicy Policy => HelicopterPolicy.Automatic;
        public float SearchPersistence => 8f;
        public float HelicopterSpeed => 0f;

        private Vector3 Pos => transform.position;

        private void Update()
        {
            if (_airborne || !CanOperate || Time.time < _readyAt) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;
            var armory = EscortArmory.Instance;
            if (armory == null || armory.helicopter == null) return;

            var target = FindTarget();
            Vector3 cue = default;
            bool search = target == null && TryGetSearchCue(out cue);
            if (target == null && !search) { _readyAt = Time.time + 0.5f; return; }

            var spot = _spot != null ? _spot() : null;
            if (spot == null) spot = transform;
            var go = PoolManager.Instance?.Spawn(armory.helicopter, spot.position + Vector3.up * 0.4f, spot.rotation);
            if (go == null || !go.TryGetComponent<AswHelicopter>(out var heli)) return;
            heli.Sortie(spot, target, this, target != null ? target.Transform.position : cue);
            _airborne = true;
            Sorties++;
            AudioManager.Play(Game.Data.SfxId.EscortTorpedoLaunch, spot.position, 0.4f, 0.6f);
            CombatLog.Add("편대", $"대잠 호위함 헬기 출격 → {(target != null ? CombatLog.Describe(target) : "수색")}");
        }

        public bool IsTargetInRange(ITargetable target)
        {
            if (target == null || !target.IsAlive || target.Transform == null) return false;
            Vector3 d = (target is SubmarineBase s ? s.LastKnownPosition : target.Transform.position) - Pos; d.y = 0f;
            return d.sqrMagnitude <= OperateRange * OperateRange;
        }

        public Vector3 PatrolPoint()
        {
            var f = transform.forward; f.y = 0f;
            return Pos + (f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward) * 18f;
        }

        /// <summary>잠수함(드러났거나 접촉 확정) 먼저, 없으면 가까운 수상함.</summary>
        public ITargetable FindTarget()
        {
            ITargetable best = null;
            float bestSqr = SubRange * SubRange;
            foreach (var t in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Submarine))
            {
                if (t is not SubmarineBase sub || !sub.IsAlive || !(sub.IsRevealed || sub.IsContactConfirmed)) continue;
                Vector3 d = sub.LastKnownPosition - Pos; d.y = 0f;
                if (d.sqrMagnitude < bestSqr) { bestSqr = d.sqrMagnitude; best = sub; }
            }
            if (best != null) return best;
            bestSqr = SurfaceRange * SurfaceRange;
            foreach (var t in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Surface))
            {
                if (t == null || !t.IsAlive || !t.IsRevealed || t.Transform == null) continue;
                Vector3 d = t.Transform.position - Pos; d.y = 0f;
                if (d.sqrMagnitude < bestSqr) { bestSqr = d.sqrMagnitude; best = t; }
            }
            return best;
        }

        /// <summary>헬기 둘레 14m 안 잠수함은 디핑 소나로 접촉을 확정하고 표적으로 준다.</summary>
        public ITargetable FindTargetFromHelicopter(Vector3 position)
        {
            SubmarineBase best = null;
            float bestSqr = 14f * 14f;
            foreach (var t in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Submarine))
            {
                if (t is not SubmarineBase sub || !sub.IsAlive) continue;
                Vector3 d = sub.transform.position - position; d.y = 0f;
                if (d.sqrMagnitude < bestSqr) { bestSqr = d.sqrMagnitude; best = sub; }
            }
            if (best != null) best.ConfirmContact(3f + (_tier != null ? _tier() : 0));
            return best;
        }

        /// <summary>55m 안 잠수함(접촉 전이어도)의 마지막 위치 — 그쪽으로 수색.</summary>
        public bool TryGetSearchCue(out Vector3 cue)
        {
            cue = default;
            float bestSqr = CueRange * CueRange;
            bool found = false;
            foreach (var t in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Submarine))
            {
                if (t is not SubmarineBase sub || !sub.IsAlive) continue;
                Vector3 d = sub.transform.position - Pos; d.y = 0f;
                if (d.sqrMagnitude >= bestSqr) continue;
                bestSqr = d.sqrMagnitude;
                cue = sub.transform.position;
                found = true;
            }
            return found;
        }

        public void OnHelicopterReturned()
        {
            _airborne = false;
            _readyAt = Time.time + 18f - 3f * Mathf.Clamp(_tier != null ? _tier() : 0, 0, 3);
        }

        private void OnDisable() => _airborne = false;
    }
}

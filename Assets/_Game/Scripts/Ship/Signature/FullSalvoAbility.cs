using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;
using Game.Modules.Runtime;

namespace Game.Ship
{
    /// <summary>
    /// 미사일 구축함 고유 스킬 — 전탄 발사(2026-10-11). 작동 중인 모든 VLS가 남은 셀을 한꺼번에 쏜다(VlsModule.FireSalvo):
    /// 덮개가 1번 셀부터 0.14초 간격으로 차례로 열리고 열린 순서대로 솟는다. 표적은 VLS 사거리 안의 적 수상함(가까운 순)과
    /// 항공기(요격탄)를 돌아가며 나눈다. 다 쏘면 평소처럼 전량 재장전에 들어간다.
    /// 연출: 표적마다 주황 조준 괄호, 함선 둘레 발사 충격 고리, 경보음.
    /// </summary>
    public sealed class FullSalvoAbility : ISignatureAbility
    {
        public const float Spacing = 0.14f;
        private static readonly Color LockColor = new(1f, 0.6f, 0.1f, 1f);

        public string Title => "전탄 발사";
        public Game.UI.HudTextures.SkillIcon Icon => Game.UI.HudTextures.SkillIcon.Salvo;
        public float Cooldown => 45f;
        public int LastScheduled { get; private set; }

        private readonly List<VlsModule> _vls = new();
        private readonly List<(ITargetable target, bool air)> _targets = new();

        public bool CanActivate(SignatureSkill owner, out string reason)
        {
            reason = null;
            Collect(owner);
            if (_vls.Count == 0) { reason = "작동 중인 VLS 없음"; return false; }
            bool loaded = false;
            foreach (var v in _vls) if (v.Ammo.CanFire && !v.IsSalvoing) loaded = true;
            if (!loaded) { reason = "VLS 재장전 중"; return false; }
            FindTargets(owner);
            if (_targets.Count == 0) { reason = "사거리 안에 적 없음"; return false; }
            return true;
        }

        public void Activate(SignatureSkill owner)
        {
            LastScheduled = 0;
            foreach (var v in _vls) LastScheduled += v.FireSalvo(_targets, Spacing);
            var marked = new HashSet<ITargetable>();
            foreach (var (t, _) in _targets)
                if (t?.Transform != null && marked.Add(t)) SignatureFx.Marker(t.Transform, 2.5f + LastScheduled * Spacing, LockColor, t is AirEnemy ? 2.4f : 3.6f);
            SignatureFx.Ring(owner.Ship.transform.position, 18f, 0.8f, new Color(1f, 0.65f, 0.2f, 0.9f), 0.8f, 2, 0.2f);
            AudioManager.Play(Game.Data.SfxId.MissileLaunch, owner.Ship.transform.position, 1f, 0.8f);
            GameEvents.RaiseBanner($"전탄 발사 — 미사일 {LastScheduled}발 · 표적 {marked.Count}");
        }

        public void Tick(SignatureSkill owner, float dt) { }

        private void Collect(SignatureSkill owner)
        {
            _vls.Clear();
            var grid = owner.Ship != null ? owner.Ship.Grid : null;
            if (grid == null) return;
            foreach (var m in grid.Modules)
                if (m != null && m.IsOperational && m.Runtime is VlsModule v && v.isActiveAndEnabled) _vls.Add(v);
        }

        /// <summary>가장 긴 VLS 사거리 안의 적: 수상함(가까운 순) 먼저, 그다음 항공기.</summary>
        private void FindTargets(SignatureSkill owner)
        {
            _targets.Clear();
            float range = 0f;
            foreach (var v in _vls) range = Mathf.Max(range, v.Stats.Range);
            Vector3 c = owner.Ship.transform.position;
            void Add(TargetKind kind, bool air)
            {
                var found = new List<(ITargetable, float)>();
                var list = TargetRegistry.HostileTo(CombatFaction.Player, kind);
                for (int i = 0; i < list.Count; i++)
                {
                    var t = list[i];
                    if (t == null || !t.IsAlive || t.Transform == null) continue;
                    Vector3 d = t.Transform.position - c; d.y = 0f;
                    if (d.sqrMagnitude <= range * range) found.Add((t, d.sqrMagnitude));
                }
                found.Sort((a, b) => a.Item2.CompareTo(b.Item2));
                foreach (var (t, _) in found) _targets.Add((t, air));
            }
            Add(TargetKind.Surface, false);
            Add(TargetKind.Aircraft, true);
        }
    }
}

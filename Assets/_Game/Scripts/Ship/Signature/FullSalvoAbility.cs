using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;
using Game.Modules.Runtime;

namespace Game.Ship
{
    /// <summary>
    /// 미사일 구축함 고유 스킬 — 전탄 발사(2026-10-11, 2026-11-21 사용자 시안 반영). 작동 중인 모든 VLS가 남은 셀을 한꺼번에 쏜다(VlsModule.FireSalvo):
    /// 덮개가 1번 셀부터 0.14초 간격으로 차례로 열리고 열린 순서대로 솟는다. 다 쏘면 평소처럼 전량 재장전에 들어간다.
    ///   - 재사용 30초(예전 45초).
    ///   - 표적 배분: VLS 사거리 안의 고가치 표적부터 — 수상함을 표적 가치(보스 10 · 대형 5 · 중형 3 · 소형 1) 높은 순(같으면 가까운 순), 그다음 항공기.
    ///     최대 6표적. 먼저 한 발씩, 남은 발은 가치에 비례해 나누되 남은 체력을 넘겨 몰지 않는다(필요 발 수 + 1). 모두 넉넉하면 남은 발은 순위대로 더 준다.
    ///     배분 목록은 1순위 표적의 발부터 이어지고, VLS 여러 기가 그 목록을 차례로 나눠 쏜다.
    ///   - 수상함을 노린 미사일은 곧게 솟아 표적 위 하늘로 넘어간 뒤 바로 위에서 수직으로 내리꽂힌다(Missile.MarkSalvo).
    ///   - 연출: 표적마다 수직 미사일 마커(SalvoMarker — 순위 번호 · 남은 수), 함선 둘레 충격파 고리 두 겹 · 섬광, 셀마다 발사 연기 기둥(FullSalvoFx).
    /// </summary>
    public sealed class FullSalvoAbility : ISignatureAbility
    {
        public const float Spacing = 0.14f;
        public const int MaxTargets = 6;

        public string Title => "전탄 발사";
        public Game.UI.HudTextures.SkillIcon Icon => Game.UI.HudTextures.SkillIcon.Salvo;
        public float Cooldown => 30f;
        public int LastScheduled { get; private set; }

        /// <summary>검증용: 마지막 발동의 표적 순서 · 배정 발 수.</summary>
        public readonly List<(ITargetable target, int missiles)> LastPlan = new();

        private readonly List<VlsModule> _vls = new();
        private readonly List<Pick> _picks = new();
        private readonly List<(ITargetable target, bool air)> _list = new();

        private sealed class Pick
        {
            public ITargetable Target;
            public bool Air;
            public int Value;
            public float SqrDist;
            public int Need;
            public int Count;
        }

        public bool CanActivate(SignatureSkill owner, out string reason)
        {
            reason = null;
            Collect(owner);
            if (_vls.Count == 0) { reason = "작동 중인 VLS 없음"; return false; }
            bool loaded = false;
            foreach (var v in _vls) if (v.Ammo.CanFire && !v.IsSalvoing) loaded = true;
            if (!loaded) { reason = "VLS 재장전 중"; return false; }
            FindTargets(owner);
            if (_picks.Count == 0) { reason = "사거리 안에 적 없음"; return false; }
            return true;
        }

        public void Activate(SignatureSkill owner)
        {
            int total = 0;
            foreach (var v in _vls) total += v.SalvoCapacity;
            Allocate(total);

            LastScheduled = 0;
            foreach (var v in _vls) LastScheduled += v.FireSalvo(_list, Spacing, LastScheduled);

            // 실제로 예약된 앞쪽 LastScheduled발 기준으로 표적마다 마커
            LastPlan.Clear();
            for (int i = 0; i < _picks.Count; i++)
            {
                var p = _picks[i];
                int n = 0;
                for (int k = 0; k < LastScheduled && k < _list.Count; k++) if (_list[k].target == p.Target) n++;
                if (n <= 0) continue;
                LastPlan.Add((p.Target, n));
                var color = p.Air ? SalvoMarker.Air : p.Value >= 5 ? SalvoMarker.HighValue : SalvoMarker.Normal;
                SalvoMarker.Begin(p.Target.Transform, n, color, LastPlan.Count, p.Air);
            }
            FullSalvoFx.Activate(owner.Ship.transform);
            AudioManager.Play(Game.Data.SfxId.MissileLaunch, owner.Ship.transform.position, 1f, 0.8f);
            GameEvents.RaiseBanner($"전탄 발사 — 미사일 {LastScheduled}발 · 표적 {LastPlan.Count}");
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

        /// <summary>가장 긴 VLS 사거리 안의 적 — 수상함은 가치 높은 순(같으면 가까운 순), 그다음 항공기(가까운 순). 최대 6.</summary>
        private void FindTargets(SignatureSkill owner)
        {
            _picks.Clear();
            float range = 0f;
            foreach (var v in _vls) range = Mathf.Max(range, v.Stats.Range);
            Vector3 c = owner.Ship.transform.position;
            var surface = new List<Pick>();
            var air = new List<Pick>();
            void Add(TargetKind kind, bool isAir, List<Pick> into)
            {
                var list = TargetRegistry.HostileTo(CombatFaction.Player, kind);
                for (int i = 0; i < list.Count; i++)
                {
                    var t = list[i];
                    if (t == null || !t.IsAlive || t.Transform == null) continue;
                    Vector3 d = t.Transform.position - c; d.y = 0f;
                    if (d.sqrMagnitude > range * range) continue;
                    into.Add(new Pick { Target = t, Air = isAir, Value = Mathf.Max(1, TargetInfo.Value(t)), SqrDist = d.sqrMagnitude });
                }
            }
            Add(TargetKind.Surface, false, surface);
            Add(TargetKind.Aircraft, true, air);
            surface.Sort((a, b) => a.Value != b.Value ? b.Value.CompareTo(a.Value) : a.SqrDist.CompareTo(b.SqrDist));
            air.Sort((a, b) => a.SqrDist.CompareTo(b.SqrDist));
            foreach (var p in surface) if (_picks.Count < MaxTargets) _picks.Add(p);
            foreach (var p in air) if (_picks.Count < MaxTargets) _picks.Add(p);
        }

        /// <summary>
        /// 발 배분: 순위대로 한 발씩 → 남은 발은 (가치 ÷ (받은 수 + 1))가 가장 큰 표적에(필요 발 수까지) → 그래도 남으면 순위대로 돌아가며.
        /// 필요 발 수 = 남은 체력 ÷ 한 발 예상 피해(올림) + 1(요격 · 빗나감 여유).
        /// </summary>
        private void Allocate(int total)
        {
            _list.Clear();
            if (_picks.Count == 0 || total <= 0) return;
            var probe = _vls.Count > 0 ? _vls[0] : null;
            foreach (var p in _picks)
            {
                p.Count = 0;
                float hp = p.Target is IHasHealth h ? h.CurrentHp : 0f;
                float dmg = probe != null ? probe.EstimateDamage(p.Target, p.Air) : 0f;
                p.Need = hp > 0f && dmg > 0.01f ? Mathf.CeilToInt(hp / dmg) + 1 : 99;
            }
            int left = total;
            foreach (var p in _picks) { if (left <= 0) break; p.Count = 1; left--; }
            while (left > 0)
            {
                Pick best = null; float bestScore = -1f;
                foreach (var p in _picks)
                {
                    if (p.Count >= p.Need) continue;
                    float s = p.Value / (p.Count + 1f);
                    if (s > bestScore) { bestScore = s; best = p; }
                }
                if (best == null) break;
                best.Count++; left--;
            }
            for (int i = 0; left > 0; i = (i + 1) % _picks.Count) { _picks[i].Count++; left--; }   // 모두 넉넉하면 순위대로
            foreach (var p in _picks) for (int k = 0; k < p.Count; k++) _list.Add((p.Target, p.Air));
        }
    }
}

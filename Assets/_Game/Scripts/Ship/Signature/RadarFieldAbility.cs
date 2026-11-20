using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;

namespace Game.Ship
{
    /// <summary>
    /// 범용 초계함 고유 스킬 — 레이더망(2026-11-20, 예전 광역 탐색 즉발을 장판으로 바꿈).
    /// 초계함 둘레에 레이더 화면 문양(방위 눈금 · 거리 고리 · 회전 스윕)을 Duration초 동안 펼친다. 장판은 배를 따라 움직인다.
    ///   - 장판(반경 Radius) 안의 적: 받는 피해 ×DamageMultiplier(벗어나도 Linger초 남음, EnemyController.Mark) · 머리 위에 갈라진 방패 문장.
    ///   - 장판 안 잠수함은 계속 드러나고(RevealFor — 일반 무장으로도 맞음), 숨은 반잠수정도 드러난다(ILowObservable).
    ///   - 스윕이 지나가는 적 자리에 초록 에코가 번쩍인다(보기 전용).
    /// 판정은 0.25초마다. 연출: View/RadarFieldFx.
    /// </summary>
    public sealed class RadarFieldAbility : ISignatureAbility
    {
        public const float Radius = 45f, Duration = 10f, DamageMultiplier = 1.3f, Linger = 1f;
        private const float Interval = 0.25f;
        private static readonly Color MarkColor = new(1f, 0.25f, 0.2f, 1f);

        public string Title => "레이더망";
        public Game.UI.HudTextures.SkillIcon Icon => Game.UI.HudTextures.SkillIcon.Sweep;
        public float Cooldown => 35f;

        private float _until = -1f, _scan;
        private Game.View.RadarFieldFx _fx;
        private readonly HashSet<EnemyController> _seen = new();
        private readonly HashSet<SubmarineBase> _subs = new();

        public bool IsRunning => Time.time < _until;
        /// <summary>검증용: 지금 장판 안에 든 적 수 · 이번에 장판에 든 적(중복 없이) · 드러낸 잠수함 수.</summary>
        public int Inside { get; private set; }
        public int TotalMarked => _seen.Count;
        public int RevealedSubs => _subs.Count;

        public bool CanActivate(SignatureSkill owner, out string reason)
        {
            reason = null;
            return owner.Ship != null;
        }

        public void Activate(SignatureSkill owner)
        {
            _until = Time.time + Duration;
            _scan = 0f;
            _seen.Clear(); _subs.Clear();
            _fx = Game.View.RadarFieldFx.Begin(owner.Ship.transform, Radius, Duration);
            AudioManager.Play(Game.Data.SfxId.SonarPing, owner.Ship.transform.position, 1f, 1.1f);
            GameEvents.RaiseBanner($"레이더망 — {Duration:0}초간 둘레 {Radius:0}m: 잠수함 탐지 · 적 받는 피해 +{(DamageMultiplier - 1f) * 100f:0}%");
        }

        public void Tick(SignatureSkill owner, float dt)
        {
            if (_until < 0f) return;
            if (!IsRunning || owner.Ship == null)
            {
                _until = -1f;
                Inside = 0;
                if (_fx != null) _fx.End();
                _fx = null;
                return;
            }
            _scan -= dt;
            if (_scan > 0f) return;
            _scan = Interval;

            Vector3 center = owner.Ship.transform.position;
            float r2 = Radius * Radius;
            int inside = 0;
            foreach (var kind in new[] { TargetKind.Surface, TargetKind.Aircraft, TargetKind.Submarine })
            {
                var list = TargetRegistry.HostileTo(CombatFaction.Player, kind);
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i] is not EnemyController e || !e.IsAlive) continue;
                    Vector3 d = e.transform.position - center; d.y = 0f;
                    if (d.sqrMagnitude > r2) continue;
                    inside++;
                    e.Mark(Interval + Linger, DamageMultiplier);
                    if (e is SubmarineBase sub)
                    {
                        sub.RevealFor(Interval + Linger);
                        if (_subs.Add(sub)) _fx?.Echo(sub.transform.position, true);
                    }
                    if (e is ILowObservable low) low.MarkDetected(Interval + Linger);
                    SignatureFx.BrokenShield(e.transform, Interval + Linger, MarkColor, e is AirEnemy ? 2.2f : 2.7f);
                    _seen.Add(e);
                }
            }
            Inside = inside;
        }
    }
}

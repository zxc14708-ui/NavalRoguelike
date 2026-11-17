using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;

namespace Game.Ship
{
    /// <summary>
    /// 범용 초계함 고유 스킬 — 광역 탐색(2026-10-11). 함선 둘레 80m의 적(수상함·항공기·잠수함)을 15초 동안 표적 지시한다:
    /// 받는 피해 +25%, 잠항 중인 잠수함도 드러난다(RevealFor — 일반 무장으로도 맞는다).
    /// 연출: 수면에 초록 레이더 고리 셋이 번지고, 지시된 적 위에 붉은 마름모 괄호가 돈다(끝나기 1초 전 깜빡임). 소나 핑.
    /// </summary>
    public sealed class RadarSweepAbility : ISignatureAbility
    {
        public const float Radius = 80f, MarkSeconds = 15f, DamageMultiplier = 1.25f;
        private static readonly Color RingColor = new(0.3f, 1f, 0.55f, 0.95f);
        private static readonly Color MarkColor = new(1f, 0.25f, 0.2f, 1f);

        public string Title => "광역 탐색";
        public Game.UI.HudTextures.SkillIcon Icon => Game.UI.HudTextures.SkillIcon.Sweep;
        public float Cooldown => 35f;
        public int LastMarked { get; private set; }
        public int LastRevealedSubs { get; private set; }

        public bool CanActivate(SignatureSkill owner, out string reason)
        {
            reason = null;
            return owner.Ship != null;
        }

        public void Activate(SignatureSkill owner)
        {
            Vector3 center = owner.Ship.transform.position;
            SignatureFx.Ring(center, Radius, 1.6f, RingColor, 1.1f, 3, 0.35f);
            AudioManager.Play(Game.Data.SfxId.SonarPing, center, 1f, 1.1f);

            LastMarked = LastRevealedSubs = 0;
            float r2 = Radius * Radius;
            foreach (var kind in new[] { TargetKind.Surface, TargetKind.Aircraft, TargetKind.Submarine })
            {
                var list = TargetRegistry.HostileTo(CombatFaction.Player, kind);
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i] is not EnemyController e || !e.IsAlive) continue;
                    Vector3 d = e.transform.position - center; d.y = 0f;
                    if (d.sqrMagnitude > r2) continue;
                    e.Mark(MarkSeconds, DamageMultiplier);
                    if (e is SubmarineBase sub) { sub.RevealFor(MarkSeconds); LastRevealedSubs++; }
                    SignatureFx.Marker(e.transform, MarkSeconds, MarkColor, e is AirEnemy ? 2.6f : 3.4f);
                    LastMarked++;
                }
            }
            GameEvents.RaiseBanner(LastMarked > 0
                ? $"광역 탐색 — 적 {LastMarked}척 표적 지시(15초 · 받는 피해 +25%){(LastRevealedSubs > 0 ? $" · 잠수함 {LastRevealedSubs}척 노출" : "")}"
                : "광역 탐색 — 80m 안에 적 없음");
        }

        public void Tick(SignatureSkill owner, float dt) { }
    }
}

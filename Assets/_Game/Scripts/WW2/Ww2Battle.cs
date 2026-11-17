using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Ship;

namespace Game.WW2
{
    /// <summary>
    /// WWII 시험 전투 시작 준비(패치 B). StageDirector.BeginWw2Battle이 부른다.
    ///   - 기함에 수동 어뢰 조작(<see cref="Ww2TorpedoControl"/>)을 붙이고, HUD에서 WWII에 없는 칸(기만체·재밍·고유 스킬)을 숨긴다.
    ///   - 안내판(<see cref="Ww2BattlePanel"/>): 구간 · 어뢰 장전 · 공습 경보.
    ///   - 적 어뢰·폭탄·포탄의 명중/빗나감을 공격 이름별로 센다(Ww2BattleLog — 결과 분석·검증).
    ///   - 같이 쓰는 현대 키와 WWII 어뢰 키가 겹치면 알린다.
    /// </summary>
    public static class Ww2Battle
    {
        public static bool Active { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active = false;

        public static void Begin(ShipController ship, Game.UI.HUDView hud)
        {
            Active = true;
            Ww2TorpedoControl.Attach(ship);
            Ww2SalvoControl.Attach(ship);   // 주포 일제(패치 C3a) — 대구경 주포가 있을 때만 일한다
            if (hud != null)
            {
                hud.ApplyWw2Hud();
                var canvas = hud.GetComponentInParent<Canvas>();
                if (canvas != null) Ww2BattlePanel.Create(canvas.rootCanvas.transform, hud.Font);
            }
            Torpedo.Finished -= OnTorpedo;
            Torpedo.Finished += OnTorpedo;
            ArtilleryShell.Landed -= OnShell;
            ArtilleryShell.Landed += OnShell;

            var conflicts = Ww2Controls.Conflicts();
            GameEvents.RaiseBanner(conflicts.Count > 0
                ? $"WWII 시험 전투 — 어뢰 키가 겹칩니다: {conflicts[0]} (설정 › 조작키)"
                : $"WWII 시험 전투 — [{Ww2Controls.BindingLabel(Ww2Control.TorpedoPort)}] 좌현 · [{Ww2Controls.BindingLabel(Ww2Control.TorpedoStarboard)}] 우현 어뢰: 누르고 조준, 떼면 발사");
            CombatLog.Add("WWII", $"시험 전투 시작 · 규칙 집합 {Ruleset.Current}");
        }

        private static void OnTorpedo(Torpedo torpedo, bool hit)
        {
            if (torpedo == null || string.IsNullOrEmpty(torpedo.Cause)) return;
            if (torpedo.Cause == Ww2BattleLog.TorpedoBoat) Ww2BattleLog.Released(torpedo.Cause);   // 어뢰정은 투하 단계가 따로 없어 끝날 때 함께 센다
            if (hit) Ww2BattleLog.Hit(torpedo.Cause); else Ww2BattleLog.Miss(torpedo.Cause);
        }

        private static void OnShell(ArtilleryShell shell, bool hit)
        {
            if (shell == null || string.IsNullOrEmpty(shell.Cause)) return;
            if (hit) Ww2BattleLog.Hit(shell.Cause); else Ww2BattleLog.Miss(shell.Cause);
        }
    }
}

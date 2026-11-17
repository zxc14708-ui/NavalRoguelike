#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using UnityEngine;
using Game.Core;
using Game.Data;

namespace Game.Dev
{
    public partial class CombatVerificationRunner
    {
        /// <summary>-sfxOnly: 무장 효과음 교체(2026-11-15) — 기관포 · CIWS · 76mm · 40mm 클립이 연결됐고 형식(48kHz 모노)이 맞으며 실제로 재생되는가.</summary>
        public bool SfxOnly;

        private IEnumerator SfxCheck()
        {
            _report.AppendLine("\n## 무장 효과음(2026-11-15 교체)");
            var audio = Object.FindFirstObjectByType<AudioManager>();
            var lib = audio != null ? Get<SfxLibrary>(audio, "library") : null;
            if (lib == null) { Fail("AudioManager · SfxLibrary가 없음"); yield break; }
            var expect = new (SfxId id, string clip, float minLen, float maxLen)[]
            {
                (SfxId.AutocannonShot, "SFX_Autocannon_Shot", 0.4f, 0.8f),
                (SfxId.CiwsBurst, "SFX_CIWS_Burst", 3f, 4.2f),
                (SfxId.NavalGunShot, "SFX_NavalGun76_Shot", 1.5f, 2.2f),
                (SfxId.TwinGunShot, "SFX_TwinGun40_Shot", 0.7f, 1.2f),
                (SfxId.HullHitGun, "SFX_HullHit_Gun", 0.3f, 0.9f),
                // 2026-11-16 전투 효과음(합성)
                (SfxId.EnemyGunShot, "SFX_EnemyGun_Shot", 1.3f, 2.1f),
                (SfxId.MissileLaunch, "SFX_Missile_Launch", 1.6f, 2.4f),
                (SfxId.MissileHit, "SFX_Missile_Hit", 2f, 3f),
                (SfxId.MissileBurst, "SFX_Missile_Burst", 1f, 1.6f),
                (SfxId.ShellImpact, "SFX_Shell_Impact", 0.6f, 1.2f),
                (SfxId.HullHitShell, "SFX_HullHit_Shell", 1f, 1.8f),
                (SfxId.HullHitMissile, "SFX_HullHit_Missile", 1.6f, 2.6f),
                (SfxId.HullHitTorpedo, "SFX_HullHit_Torpedo", 1.8f, 2.8f),
                (SfxId.HullHitCollision, "SFX_HullHit_Collision", 0.9f, 1.6f),
            };
            if (GameManager.Instance.State != GameState.Playing) GameManager.Instance.SetState(GameState.Playing);
            var at = GameManager.Instance.Player != null ? GameManager.Instance.Player.transform.position : Vector3.zero;
            foreach (var (id, clip, minLen, maxLen) in expect)
            {
                var e = lib.Get(id);
                var c = e?.Clip;
                bool ok = c != null && c.name == clip && c.frequency == 48000 && c.channels == 1 && c.length >= minLen && c.length <= maxLen;
                var h = AudioManager.Play(id, at);
                yield return null;
                bool playing = h.IsPlaying;
                _report.AppendLine($"- {id}: {(c != null ? c.name : "없음")} · {(c != null ? $"{c.frequency}Hz {c.channels}ch {c.length:0.00}초" : "-")} · 음량 {e?.Volume:0.##} · 재생 {Yes(playing)}");
                if (!ok) Fail($"{id} 효과음 클립·형식이 다름(기대 {clip}, 48kHz 모노, {minLen}~{maxLen}초)");
                if (!playing) Fail($"{id} 효과음이 재생되지 않음");
                yield return new WaitForSeconds(0.3f);
            }

            // 기함 피격음: 무기 종류 → 소리
            var cases = new (Game.Combat.DamageSource src, float amount, SfxId want, string label)[]
            {
                (Game.Combat.DamageSource.Gun, 2f, SfxId.HullHitGun, "기관포탄 2"),
                (Game.Combat.DamageSource.Gun, 12f, SfxId.HullHitShell, "포탄 12"),
                (Game.Combat.DamageSource.Missile, 30f, SfxId.HullHitMissile, "미사일"),
                (Game.Combat.DamageSource.Torpedo, 30f, SfxId.HullHitTorpedo, "어뢰"),
                (Game.Combat.DamageSource.Collision, 10f, SfxId.HullHitCollision, "충돌"),
                (Game.Combat.DamageSource.CookOff, 20f, SfxId.HullHitShell, "유폭"),
            };
            var map = new System.Text.StringBuilder();
            foreach (var (src, amount, want, label) in cases)
            {
                var got = Game.Ship.ShipController.HitSound(new Game.Combat.DamageInfo(amount, at, Vector3.down, src));
                map.Append($"{label} → {got} · ");
                if (got != want) Fail($"기함 피격음 대응이 다름: {label} → {got}(기대 {want})");
            }
            _report.AppendLine($"- 기함 피격음 대응: {map}");
            // 실제 피격 한 번(기관포탄)으로 그 소리가 울리는가
            var ship = GameManager.Instance.Player;
            if (ship != null)
            {
                var last = Get<System.Collections.Generic.Dictionary<SfxId, float>>(audio, "_lastPlayed");
                float before = last != null && last.TryGetValue(SfxId.HullHitGun, out float b0) ? b0 : -1f;
                yield return new WaitForSeconds(0.2f);   // 위에서 튼 소리의 최소 간격이 지나게
                ship.TakeDamage(new Game.Combat.DamageInfo(1f, ship.transform.position + Vector3.up, Vector3.down, Game.Combat.DamageSource.Gun));
                yield return null;
                bool rang = last != null && last.TryGetValue(SfxId.HullHitGun, out float b1) && b1 > before;
                _report.AppendLine($"- 실제 피격(기관포탄 1) → HullHitGun 재생 기록 {Yes(rang)}");
                if (!rang) Fail("기함이 맞았는데 기관포탄 피격음이 울리지 않음");
                ship.RepairHull(ship.HullMaxHp);
            }
        }
    }
}
#endif

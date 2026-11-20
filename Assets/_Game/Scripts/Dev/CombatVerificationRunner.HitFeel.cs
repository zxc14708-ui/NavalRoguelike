#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Enemies;
using Game.View;

namespace Game.Dev
{
    /// <summary>자동 전투 검증 — 타격감(2026-11-20): 피격 번쩍임 · 피격 흔들림 · 구경별 명중 연출 · 함포 포구 화염.</summary>
    public partial class CombatVerificationRunner
    {
        private IEnumerator HitFeelCheck()
        {
            _report.AppendLine("\n## 타격감(2026-11-20)");
            var ship = GameManager.Instance.Player;
            EnemySpawner.Instance.enabled = false;
            CombatDevTools.ClearBattlefield();
            yield return null;

            // ---- 1) 피격 번쩍임 · 흔들림: 멈춰 둔 미사일정(체력 55)에 기관포 한 발(2) · 76mm 한 발(24)
            var list = CombatDevTools.SpawnRing("ene_missileboat", 1, 24f, 40f);
            var boat = list.Count > 0 ? list[0] : null;
            if (boat == null) { Fail("검사용 미사일정을 띄우지 못함"); yield break; }
            boat.DevFrozen = true;
            // 기함 무장이 표적을 쏘면 흔들림 · 번쩍임이 섞인다 — 1 · 2단계 동안 무장을 멈춘다
            var modules = ship.GetComponentsInChildren<Game.Modules.ModuleRuntime>(true);
            var wasOn = new bool[modules.Length];
            for (int i = 0; i < modules.Length; i++) { wasOn[i] = modules[i].enabled; modules[i].enabled = false; }
            yield return new WaitForSeconds(0.5f);
            Vector3 from = ship.transform.position;
            Vector3 dir = boat.transform.position - from; dir.y = 0f; dir.Normalize();
            var sway = boat.transform.Find("ModelPivot")?.GetComponent<ShipVisualSway>();

            yield return CloseShot("hit_before", boat.transform, 11f);
            boat.TakeDamage(new DamageInfo(2f, boat.transform.position, dir, DamageSource.Gun, "검증"));
            yield return new WaitForEndOfFrame();   // 이 프레임의 LateUpdate(번쩍임 갱신) 뒤
            var flash = boat.GetComponent<HitFlash>();
            float smallLevel = flash != null ? flash.Level : 0f;
            int layers = flash != null ? flash.LayerCount : 0, shown = flash != null ? flash.Shown : 0;
            yield return new WaitForSeconds(0.35f);
            bool off = flash != null && flash.Level == 0f && flash.Shown == 0;
            _report.AppendLine($"- 기관포 한 발(2): 덧그림 {layers}조각 · 켜짐 {shown} · 세기 {smallLevel:0.00} · 0.35초 뒤 꺼짐 {Yes(off)}");
            if (flash == null || layers == 0) Fail("맞아도 번쩍임 덧그림이 생기지 않음");
            else if (shown == 0 || smallLevel <= 0f) Fail("맞았는데 번쩍이지 않음");
            if (!off) Fail("번쩍임이 꺼지지 않음");
            // 덧그림은 투명 큐 — 격침 잔해가 복사하지 않는다
            bool queueOk = true;
            foreach (var r in boat.GetComponentsInChildren<MeshRenderer>(true))
                if (r.name == HitFlash.LayerName && r.sharedMaterial != null && r.sharedMaterial.renderQueue < 2500) queueOk = false;
            if (!queueOk) Fail("번쩍임 덧그림이 불투명 큐 — 격침 잔해에 복사됨");

            float kick0 = 0f;
            boat.TakeDamage(new DamageInfo(24f, boat.transform.position, dir, DamageSource.Gun, "검증"));
            yield return new WaitForEndOfFrame();
            float bigLevel = flash != null ? flash.Level : 0f;
            yield return CloseShot("hit_flash_76mm", boat.transform, 11f);
            for (int i = 0; i < 8; i++) { yield return null; if (sway != null) kick0 = Mathf.Max(kick0, sway.KickDegrees); }
            yield return new WaitForSeconds(1.2f);
            float settled = sway != null ? sway.KickDegrees : 0f;
            _report.AppendLine($"- 76mm 한 발(24): 세기 {bigLevel:0.00}(기관포 {smallLevel:0.00}보다 커야) · 기울기 최대 {kick0:0.0}° · 1.2초 뒤 {settled:0.00}°");
            if (bigLevel <= smallLevel) Fail("큰 포탄의 번쩍임이 기관포보다 세지 않음");
            if (sway == null || kick0 < 0.8f) Fail("맞아도 배가 기울지 않음");
            if (settled > 0.3f) Fail("피격 기울기가 제자리로 돌아오지 않음");

            // ---- 2) 구경별 명중 연출: 작은 탄은 불덩이 없음, 큰 포탄은 불덩이 · 연기
            {
                int fire0 = DecorFx.Count(DecorFx.BlastFire), hits0 = Explosions.ShellHits, blasts0 = Explosions.SpawnCount;
                Vector3 p = boat.transform.position + Vector3.up;
                Explosions.ShellHit(p, dir, 2f);
                int smallFire = DecorFx.Count(DecorFx.BlastFire) - fire0;
                Explosions.ShellHit(p, dir, 24f);
                int bigFire = DecorFx.Count(DecorFx.BlastFire) - fire0 - smallFire;
                _report.AppendLine($"- 명중 연출: 크기 기관포 {Explosions.CaliberScale(2f):0.00} · 76mm {Explosions.CaliberScale(24f):0.00} · 불덩이 기관포 {smallFire}개 · 76mm {bigFire}개 · 폭발 수에 안 셈 {Yes(Explosions.SpawnCount == blasts0)}");
                if (Explosions.ShellHits - hits0 != 2) Fail("명중 연출 수가 맞지 않음");
                if (smallFire != 0) Fail("기관포 명중에 불덩이가 생김");
                if (DecorFx.BlastFire != null && bigFire < 3) Fail("76mm 명중에 불덩이가 없음");
                if (Explosions.SpawnCount != blasts0) Fail("포탄 명중이 미사일 폭발 수에 섞임");
            }

            // ---- 3) 실제 사격: 76mm를 달고 쏘게 해 포구 화염(함포 크기) · 포탄 명중 연출이 생기는지
            {
                var gun = CombatDevTools.InstallModuleNear("mod_gun76", new Game.Ship.GridCoord(0, 0), 8);
                bool installed = gun != null && gun.Runtime != null;
                // 레이더(탐지)와 새로 단 76mm만 켠다 — 다른 무장이 먼저 격침하지 않게
                foreach (var r in Object.FindObjectsByType<Game.Modules.ModuleRuntime>(FindObjectsSortMode.None))
                    r.enabled = r is Game.Modules.Runtime.IRadarSource || (installed && r == gun.Runtime);
                // 사격각이 어느 쪽이든 하나는 들어오게 세 척을 둘러 둔다(멈춤 · 체력 계속 채움)
                var targets = CombatDevTools.SpawnRing("ene_missileboat", 3, 20f, 30f);
                targets.Add(boat);
                foreach (var e in targets) e.DevFrozen = true;
                int big0 = MuzzleBurstVfx.BigBursts, hits0 = Explosions.ShellHits, flashes0 = HitFlash.Flashes;
                int bigHits0 = Explosions.BigShellHits;
                float t0 = Time.time;
                while (Time.time - t0 < 15f && (MuzzleBurstVfx.BigBursts == big0 || Explosions.BigShellHits == bigHits0))
                {
                    foreach (var e in targets) if (e != null && e.IsAlive) e.Repair(999f);
                    yield return null;
                }
                _report.AppendLine($"- 실제 사격(76mm 설치 {Yes(installed)}): 함포 포구 화염 {MuzzleBurstVfx.BigBursts - big0} · 포탄 명중 연출 {Explosions.ShellHits - hits0} · 번쩍임 {HitFlash.Flashes - flashes0} · 크기 함포 {MuzzleBurstVfx.SizeFor(MuzzleBurstVfx.Style.NavalGun):0.0} / 기관포 {MuzzleBurstVfx.SizeFor(MuzzleBurstVfx.Style.Autocannon):0.00} · {Time.time - t0:0.0}초");
                if (MuzzleBurstVfx.BigBursts == big0) Fail("함포가 쏴도 큰 포구 화염이 없음");
                if (Explosions.ShellHits == hits0) Fail("함포탄이 맞아도 명중 연출이 없음");
                if (HitFlash.Flashes == flashes0) Fail("함포탄이 맞아도 번쩍이지 않음");
                yield return new WaitForSeconds(0.1f);
            }

            for (int i = 0; i < modules.Length; i++) if (modules[i] != null) modules[i].enabled = wasOn[i];
            boat.DevFrozen = false;
            CombatDevTools.ClearBattlefield();
            EnemySpawner.Instance.enabled = true;
        }
    }
}
#endif

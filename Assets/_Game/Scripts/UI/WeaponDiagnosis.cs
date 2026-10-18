using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Game.Combat;
using Game.Enemies;
using Game.Modules;
using Game.Modules.Runtime;

namespace Game.UI
{
    /// <summary>
    /// 무장이 지금 쏘지 않는 이유(2026-10-11 점검 G03). 무장 탄약 패널이 줄마다 짧게, 줄을 누르면 문마다 자세히 보여 준다.
    /// 무기 코드를 바꾸지 않고 밖에서 판단한다 — 순서대로 처음 걸리는 것:
    ///   파괴 → 탄약 없음·재장전 → 교전 중 → 사거리 안 적 없음(가장 가까운 적 거리) → 최소 거리 안 → 탐지 안 됨 → 사각(구조물에 가림) → 조준 중.
    /// 대잠 무장은 잠수함이 드러나지 않았으면 "미탐지". 사각은 포탑식(WeaponController가 있는) 무장만 판단한다.
    /// </summary>
    public static class WeaponDiagnosis
    {
        public readonly struct Result
        {
            public readonly string Short, Detail;
            public readonly bool Idle;   // 쏠 수 있는데 안 쏘는 상태(사유를 보여 줄 만한)
            public Result(string s, string d, bool idle) { Short = s; Detail = d; Idle = idle; }
        }

        private static readonly Dictionary<System.Type, FieldInfo> s_weaponFields = new();
        private static readonly List<ITargetable> s_candidates = new(32);

        public static Result Explain(ModuleInstance module, TargetingSystem targeting)
        {
            if (module == null || module.Definition == null) return new Result("", "", false);
            if (module.IsDestroyed) return new Result("파괴", "파괴됨 — 정비 화면에서 수리된다", false);
            var runtime = module.Runtime;
            if (runtime is IAmmoUser user && user.Ammo != null)
            {
                var ammo = user.Ammo;
                if (ammo.Status == AmmoMagazine.State.Empty) return new Result("탄 없음", $"탄약 없음 — {ammo.SecondsToNext:0}초 뒤 보급", false);
                if (ammo.IsReloading) return new Result("재장전", $"전량 재장전 중 — {ammo.SecondsToNext:0.0}초", false);
                if (user.IsEngaged) return new Result("교전", "교전 중", false);
            }
            var stats = module.EffectiveStats;
            var type = module.Definition.Type;
            if (!Classes(type, out bool surface, out bool air, out bool sub)) return new Result("", "", false);

            Vector3 from = runtime != null ? runtime.transform.position : Vector3.zero;
            float range = stats.Range, min = stats.MinRange;

            // 적이 있는가 · 가장 가까운 거리 — 실제 위치(탐지와 무관)
            float nearest = float.MaxValue;
            bool inBand = false, insideMin = false, hiddenSub = false;
            s_candidates.Clear();
            void Scan(TargetKind kind)
            {
                var list = TargetRegistry.HostileTo(CombatFaction.Player, kind);
                for (int i = 0; i < list.Count; i++)
                {
                    var t = list[i];
                    if (t == null || !t.IsAlive || t.Transform == null) continue;
                    if (kind == TargetKind.Missile && t is Missile m && !m.IsThreat) continue;
                    Vector3 d = t.Transform.position - from; d.y = 0f;
                    float dist = d.magnitude;
                    float max = kind == TargetKind.Aircraft || kind == TargetKind.Missile ? (type == ModuleType.Nobong ? stats.Range * 0.7f : range) : range;
                    nearest = Mathf.Min(nearest, dist);
                    if (dist > max) continue;
                    if (dist < min && kind != TargetKind.Missile) { insideMin = true; continue; }
                    if (kind == TargetKind.Submarine && !(t.IsRevealed || (t is SubmarineBase s && s.IsContactConfirmed))) { hiddenSub = true; continue; }
                    inBand = true;
                    s_candidates.Add(t);
                }
            }
            if (surface) Scan(TargetKind.Surface);
            if (air) { Scan(TargetKind.Aircraft); if (type is ModuleType.Ciws or ModuleType.SamLauncher) Scan(TargetKind.Missile); }
            if (sub) Scan(TargetKind.Submarine);

            if (!inBand)
            {
                if (hiddenSub) return new Result("미탐지", "잠수함이 사거리 안에 있지만 드러나지 않음 — 소나·헬기로 접촉을 잡아야 쏜다", true);
                if (insideMin) return new Result("근접", $"적이 최소 거리 {min:0} m 안 — 너무 가까워 쏘지 않는다(조금 떨어지면 다시 쏜다)", true);
                if (nearest < float.MaxValue) return new Result("범위 밖", $"사거리 {range:0} m 안에 맡을 적이 없음 — 가장 가까운 적 {nearest:0} m", true);
                return new Result("대기", "맡을 적이 없음", true);
            }

            // 탐지: 무장은 함선이 탐지한 표적만 쏜다(CIWS는 자체 센서)
            bool detected = type == ModuleType.Ciws;
            if (!detected && targeting != null)
                foreach (var t in s_candidates)
                    if (Contains(targeting.DetectedSurface, t) || Contains(targeting.DetectedAircraft, t) ||
                        Contains(targeting.DetectedSubmarines, t) || Contains(targeting.IncomingMissiles, t)) { detected = true; break; }
            if (!detected)
                return new Result("미탐지", $"사거리 안의 적이 탐지 범위 밖 — 함교 레이더 {targeting?.RadarRange ?? 0f:0} m · 전용 레이더가 있으면 더 멀리 본다", true);

            var weapon = WeaponOf(runtime);
            if (weapon != null)
            {
                bool any = false;
                foreach (var t in s_candidates) if (weapon.IsInFireArc(t.Transform.position)) { any = true; break; }
                if (!any) return new Result("사각", "적이 사격 금지 구역(함교·이웃 블록에 가린 방향)에 있음 — 배를 돌리거나 승강 거치대로 올린다", true);
            }
            return new Result("조준", "표적을 향해 선회·조준 중", true);
        }

        /// <summary>무장 종류가 맡는 표적(수상·항공·잠수함). 판단하지 않는 무장이면 false.</summary>
        private static bool Classes(ModuleType type, out bool surface, out bool air, out bool sub)
        {
            surface = air = sub = false;
            switch (type)
            {
                case ModuleType.Autocannon: surface = air = sub = true; return true;
                case ModuleType.Nobong:
                case ModuleType.NavalGun: surface = air = true; return true;
                case ModuleType.GuidedRocket:
                case ModuleType.Howitzer: surface = true; return true;
                case ModuleType.Vls: surface = sub = true; return true;
                case ModuleType.SamLauncher:
                case ModuleType.Ciws: air = true; return true;
                case ModuleType.AswLauncher:
                case ModuleType.TorpedoTube: sub = true; return true;
                case ModuleType.Ww2DualPurposeGun:
                case ModuleType.Ww2QuadAa: surface = air = true; return true;
                case ModuleType.Ww2HeavyGun: surface = true; return true;
                default: return false;   // 현측 어뢰는 수동 발사라 "안 쏘는 이유"를 따지지 않는다
            }
        }

        private static bool Contains(IReadOnlyList<ITargetable> list, ITargetable t)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++) if (ReferenceEquals(list[i], t)) return true;
            return false;
        }

        /// <summary>포탑식 무장의 WeaponController(필드 하나) — 사각 판단용.</summary>
        private static WeaponController WeaponOf(ModuleRuntime runtime)
        {
            if (runtime == null) return null;
            var type = runtime.GetType();
            if (!s_weaponFields.TryGetValue(type, out var field))
            {
                field = null;
                foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                    if (f.FieldType == typeof(WeaponController)) { field = f; break; }
                s_weaponFields[type] = field;
            }
            return field?.GetValue(runtime) as WeaponController;
        }
    }
}

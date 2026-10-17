using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Data;
using Game.Enemies;
using Game.Modules;
using Game.View;

namespace Game.TaskForce
{
    /// <summary>
    /// 호위함 실제 무장(2026-10-11). 모델에 실린 무장이 각자 표적을 겨눠 실제 탄·미사일·어뢰·헬기를 쓴다(EscortArmory 프리팹).
    ///   - 함포(모든 호위함): 포탑마다 따로 — 주포·후방포·현측포는 76mm 탄(30m, 0.9초), 원격·현측 기관총은 기관포 탄(20m, 0.18초).
    ///     사격각(포탑 선회 한도) 안의 가장 가까운 적 수상함·항공기, 포탑이 향하면 쏜다.
    ///   - 방공: CIWS(22m 안 적 미사일에 탄막 — 실제 탄이 맞아야 격추), VLS·SAM 발사기에서 함대공 미사일(42m, 기함에 가장 급한 미사일).
    ///   - 미사일: 미사일 발사관마다 대함 미사일 한 발씩(0.25초 간격) — 발사관이 많은 개량형일수록 한 번에 많이 쏜다.
    ///   - 대잠: 대잠 헬기(EscortHelideck), 표적 쪽 현측 어뢰관에서 경어뢰(실제로 달려가 추적), T3 대잠 로켓(경어뢰 탄두 6연발 중 2발).
    ///   - 전자전은 예전과 같다(근접 교란).
    /// 표적·피해 판정은 탄이 맞을 때 난다(예전 즉시 판정 + 예광선 연출 대신). EscortArmory가 없으면 예전 방식으로 싸운다.
    /// </summary>
    public sealed partial class EscortDefense
    {
        public const float HeavyRange = 30f, LightRange = 20f, CiwsRange = 22f;
        public const float HeavyInterval = 0.9f, LightInterval = 0.18f;
        /// <summary>결과 분석에서 호위함이 준 피해의 이름(같은 탄을 쓰는 기함 무장과 가른다).</summary>
        public const string EscortKey = "호위함";

        private float[] _mountAt = new float[0];
        private float[] _ciwsAt = new float[0];
        private int[] _ciwsBurst = new int[0];
        private int _samLauncher;
        private float _rocketAt;
        private readonly List<(float at, Transform point, ITargetable target, float damage)> _strikeQueue = new();
        private EscortHelideck _deck;
        private readonly List<(ITargetable t, float sqr)> _candidates = new();
        private readonly List<Missile> _samTargets = new();   // 함대공 미사일을 쏜 적 미사일 — 사라지면 요격으로 센다

        /// <summary>검증용: 실제로 쏜 수.</summary>
        public int CiwsRounds { get; private set; }
        public int SamLaunches { get; private set; }
        public int StrikeLaunches { get; private set; }
        public int TorpedoLaunches { get; private set; }
        public int RocketLaunches { get; private set; }
        public EscortHelideck Helideck => _deck;
        /// <summary>실제 무장을 쓰는가(EscortArmory가 있다).</summary>
        public bool UsesRealWeapons => EscortArmory.Instance != null;

        private void UpdateArmament(EscortArmory armory, int tier, EscortRole role, Vector3 flagship)
        {
            var rig = Turrets;
            if (Time.time >= _gunAt) FireGuns(armory, rig, tier);   // _gunAt: 함포를 잠깐 막을 때(검증)
            UpdateStrikeQueue();
            for (int i = _samTargets.Count - 1; i >= 0; i--)
            {
                var m = _samTargets[i];
                if (m != null && m.IsAlive && m.gameObject.activeInHierarchy) continue;
                _samTargets.RemoveAt(i);
                SamKills++;
            }
            switch (role)
            {
                case EscortRole.AirDefense:
                    FireCiws(armory, rig);
                    if (Time.time >= _samAt) LaunchSam(armory, rig, tier, flagship);
                    break;
                case EscortRole.ElectronicWarfare when Time.time >= _jamAt: Jam(tier); break;
                case EscortRole.AntiSubmarine:
                    EnsureHelideck(rig);
                    if (Time.time >= _aswAt) LaunchTorpedo(armory, rig, tier);
                    if (Time.time >= _rocketAt) LaunchAswRockets(armory, rig, tier);
                    break;
                case EscortRole.SurfaceStrike when Time.time >= _strikeAt: LaunchStrike(armory, rig, tier); break;
            }
            if (role != EscortRole.AntiSubmarine && _deck != null) _deck.enabled = false;
        }

        // ------------------------------------------------------------ 함포(포탑마다)

        private void FireGuns(EscortArmory armory, EscortTurrets rig, int tier)
        {
            int n = rig != null ? rig.Mounts(EscortTurrets.MountKind.Gun) : 0;
            if (n == 0 || armory.heavyRound == null)
            {
                FireGun(tier);   // 포탑이 없는 모델: 예전 방식(FireGun이 _gunAt을 미룬다)
                return;
            }
            if (_mountAt.Length != n) _mountAt = new float[n];
            float rate = RunUpgrades.FireRateMultiplier(ModuleType.Autocannon) * _relayRate;
            float dmgMul = 1f + RunUpgrades.Get(RunStat.Damage);
            for (int i = 0; i < n; i++)
            {
                if (Time.time < _mountAt[i]) continue;
                bool light = rig.IsLight(EscortTurrets.MountKind.Gun, i) && armory.lightRound != null;
                float range = light ? LightRange : HeavyRange;
                if (!rig.MountPosition(EscortTurrets.MountKind.Gun, i, out var from)) continue;

                // 사격각 안의 가장 가까운 표적
                Gather(from, range, surface: true, air: true);
                ITargetable pick = null;
                Vector3 aim = default, muzzle = default;
                bool aligned = false;
                foreach (var (t, _) in _candidates)
                {
                    Vector3 p = Lead(from, t, light ? 80f : 75f) + Vector3.up * (t is AirEnemy ? 0f : 0.5f);
                    if (!rig.EngageMount(EscortTurrets.MountKind.Gun, i, p, out muzzle, out aligned)) continue;
                    pick = t; aim = p;
                    break;
                }
                if (pick == null) { _mountAt[i] = Time.time + 0.25f; continue; }
                if (!aligned) { _mountAt[i] = Time.time + 0.05f; continue; }

                Vector3 dir = (aim - muzzle).normalized;
                float spread = light ? 1.4f : 0.6f;
                dir = Quaternion.AngleAxis(Random.Range(-spread, spread), Vector3.up) * dir;
                var go = PoolManager.Instance?.Spawn(light ? armory.lightRound : armory.heavyRound, muzzle, Quaternion.LookRotation(dir));
                float damage = GunDamageFor(tier) * (light ? 0.45f : 1.6f) * dmgMul;
                if (go != null && go.TryGetComponent<Projectile>(out var round)) { round.Launch(dir, light ? 80f : 75f, damage, DamageSource.Gun); round.StatsKey = EscortKey; }
                _mountAt[i] = Time.time + (light ? LightInterval : HeavyInterval) / rate;
                GunShots++;
                if (!light) Explosions.Spawn(muzzle, 0.12f, Explosions.Kind.Air);   // 포구 섬광
                if (!light || GunShots % 3 == 0)
                    AudioManager.Play(SfxId.EscortGunShot, muzzle, light ? 0.45f : 0.9f, light ? 1.45f : (tier >= 2 ? 0.82f : 1.05f));
            }
        }

        // ------------------------------------------------------------ 방공: CIWS · 함대공 미사일

        private void FireCiws(EscortArmory armory, EscortTurrets rig)
        {
            int n = rig != null ? rig.Mounts(EscortTurrets.MountKind.Ciws) : 0;
            if (n == 0 || armory.ciwsRound == null) return;
            if (_ciwsAt.Length != n) { _ciwsAt = new float[n]; _ciwsBurst = new int[n]; }
            for (int i = 0; i < n; i++)
            {
                if (Time.time < _ciwsAt[i] || !rig.MountPosition(EscortTurrets.MountKind.Ciws, i, out var from)) continue;
                Missile pick = null;
                float best = CiwsRange * CiwsRange;
                var missiles = TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Missile);
                for (int k = 0; k < missiles.Count; k++)
                {
                    if (missiles[k] is not Missile m || !m.IsAlive || !m.IsThreat) continue;
                    float sqr = (m.transform.position - from).sqrMagnitude;
                    if (sqr < best) { best = sqr; pick = m; }
                }
                if (pick == null) { _ciwsAt[i] = Time.time + 0.1f; _ciwsBurst[i] = 0; continue; }
                Vector3 aim = Lead(from, pick, 140f);
                if (!rig.EngageMount(EscortTurrets.MountKind.Ciws, i, aim, out var muzzle, out bool aligned, 0.8f) || !aligned) { _ciwsAt[i] = Time.time + 0.03f; continue; }
                Vector3 dir = Quaternion.AngleAxis(Random.Range(-1.2f, 1.2f), Vector3.up) * (aim - muzzle).normalized;
                var go = PoolManager.Instance?.Spawn(armory.ciwsRound, muzzle, Quaternion.LookRotation(dir));
                if (go != null && go.TryGetComponent<Projectile>(out var round)) { round.Launch(dir, 140f, 0.75f, DamageSource.Gun); round.StatsKey = EscortKey; }
                CiwsRounds++;
                if (CiwsRounds % 5 == 0) AudioManager.Play(SfxId.CiwsBurst, muzzle, 0.5f, 1.1f);
                if (++_ciwsBurst[i] >= 15) { _ciwsBurst[i] = 0; _ciwsAt[i] = Time.time + 0.25f; }
                else _ciwsAt[i] = Time.time + 0.04f;
            }
        }

        /// <summary>VLS(수직 발사)와 SAM 발사기를 돌아가며 — 기함에 가장 급하고 아직 아무도 맡지 않은 적 미사일에 함대공 미사일.</summary>
        private void LaunchSam(EscortArmory armory, EscortTurrets rig, int tier, Vector3 flagship)
        {
            if (armory.samMissile == null) { FireSam(tier, flagship); return; }
            Missile pick = null;
            float best = float.MaxValue;
            var missiles = TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Missile);
            for (int i = 0; i < missiles.Count; i++)
            {
                if (missiles[i] is not Missile m || !m.IsAlive || !m.IsThreat || TargetAllocator.IsCovered(m)) continue;
                if ((m.transform.position - transform.position).sqrMagnitude > SamRange * SamRange) continue;
                float urgency = (m.transform.position - flagship).sqrMagnitude;
                if (urgency < best) { best = urgency; pick = m; }
            }
            if (pick == null) { _samAt = Time.time + 0.2f; return; }

            int samMounts = rig != null ? rig.Mounts(EscortTurrets.MountKind.Sam) : 0;
            bool hasVls = rig != null && rig.TrySocket("VLSLaunchPoint", out _);
            int launchers = samMounts + (hasVls ? 1 : 0);
            Vector3 from = transform.position + Vector3.up * 2f;
            Quaternion rot = Quaternion.LookRotation(Vector3.up, transform.forward);
            int which = launchers > 0 ? _samLauncher++ % launchers : -1;
            if (which >= 0 && which < samMounts && rig.EngageMount(EscortTurrets.MountKind.Sam, which, pick.transform.position, out var mz, out _))
            { from = mz; rot = Quaternion.LookRotation((pick.transform.position - mz).normalized); }
            else if (hasVls && rig.TrySocket("VLSLaunchPoint", out var vls)) from = vls;   // 수직 발사

            var go = PoolManager.Instance?.Spawn(armory.samMissile, from, rot);
            if (go != null && go.TryGetComponent<Missile>(out var sam)) { sam.Launch(pick.transform, 10f); sam.StatsKey = EscortKey; }
            if (go != null) _samTargets.Add(pick);
            _samAt = Time.time + SamReload(tier) / _relayRate / Mathf.Max(1f, launchers * 0.75f);
            SamLaunches++;
            AudioManager.Play(SfxId.EscortMissileLaunch, from, 1f, 1.2f);
            CombatLog.Add("편대", "방공 호위함 함대공 미사일 발사");
        }

        // ------------------------------------------------------------ 미사일: 대함 미사일

        private void LaunchStrike(EscortArmory armory, EscortTurrets rig, int tier)
        {
            var points = rig != null ? rig.Sockets("LaunchPoint_MissileBank") : null;
            if (armory.strikeMissile == null || points == null || points.Count == 0) { SurfaceStrike(tier); return; }
            ITargetable pick = null;
            float bestScore = float.MaxValue;
            foreach (var t in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Surface))
            {
                if (t == null || !t.IsAlive || !t.IsRevealed || t.Transform == null || t is not IDamageable) continue;
                float sqr = (t.Transform.position - transform.position).sqrMagnitude;
                if (sqr > StrikeRange * StrikeRange) continue;
                float score = -TargetInfo.Value(t) * 100000f + sqr;
                if (score < bestScore) { bestScore = score; pick = t; }
            }
            if (pick == null) { _strikeAt = Time.time + 0.5f; return; }

            // 발사관을 표적 쪽으로 돌리고(연출), 발사점마다 한 발씩 0.25초 간격
            for (int i = 0; i < rig.Mounts(EscortTurrets.MountKind.Strike); i++)
                rig.EngageMount(EscortTurrets.MountKind.Strike, i, pick.Transform.position, out _, out _, 2.5f);
            float damage = StrikeDamage(tier) * 0.5f * RunUpgrades.DamageMultiplier(ModuleType.Vls);
            points.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            for (int k = 0; k < points.Count; k++) _strikeQueue.Add((Time.time + 0.35f + k * 0.25f, points[k], pick, damage));
            _strikeAt = Time.time + StrikeReload(tier) / (RunUpgrades.FireRateMultiplier(ModuleType.Vls) * _relayRate);
            var flat = pick.Transform.position - transform.position; flat.y = 0f;
            TaskForceWorldFeedback.Instance?.PlayRig("STK", pick.Transform.position, flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flat).eulerAngles.y + 42f : 0f, 8f, 1.3f, 0f);
            CombatLog.Add("편대", $"미사일 호위함 대함 미사일 {points.Count}발 → {CombatLog.Describe(pick)}");
        }

        private void UpdateStrikeQueue()
        {
            var armory = EscortArmory.Instance;
            for (int i = _strikeQueue.Count - 1; i >= 0; i--)
            {
                var q = _strikeQueue[i];
                if (Time.time < q.at) continue;
                _strikeQueue.RemoveAt(i);
                if (armory == null || q.point == null || q.target == null || !q.target.IsAlive || q.target.Transform == null) continue;
                var go = PoolManager.Instance?.Spawn(armory.strikeMissile, q.point.position, Quaternion.LookRotation(q.point.forward));
                if (go != null && go.TryGetComponent<Missile>(out var strike)) { strike.Launch(q.target.Transform, q.damage); strike.StatsKey = EscortKey; }
                StrikeLaunches++;
                SurfaceStrikes++;
                Explosions.Spawn(q.point.position, 0.25f, Explosions.Kind.Air);
                AudioManager.Play(SfxId.EscortMissileLaunch, q.point.position, 1f, 0.92f);
            }
        }

        // ------------------------------------------------------------ 대잠: 헬기 · 어뢰 · 대잠 로켓

        private void EnsureHelideck(EscortTurrets rig)
        {
            if (_deck == null)
            {
                if (rig == null || !rig.TrySocket("HelicopterLaunch", out _)) return;   // 헬기 갑판이 없는 모델
                _deck = GetComponent<EscortHelideck>() ?? gameObject.AddComponent<EscortHelideck>();
                _deck.Init(_ready, _tier, () =>
                {
                    var r = Turrets;
                    if (r == null) return transform;
                    var list = r.Sockets("HelicopterLaunch");
                    return list.Count > 0 ? list[0] : transform;
                });
            }
            if (!_deck.enabled) _deck.enabled = true;
        }

        /// <summary>40m 안 가장 가까운 잠수함 쪽 현측 어뢰관에서 경어뢰 — 실제로 달려가 추적한다.</summary>
        private void LaunchTorpedo(EscortArmory armory, EscortTurrets rig, int tier)
        {
            if (armory.aswTorpedo == null) { AswStrike(tier); return; }
            SubmarineBase pick = null;
            float best = AswRange * AswRange;
            foreach (var t in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Submarine))
            {
                if (t is not SubmarineBase sub || !sub.IsAlive) continue;
                float sqr = (sub.transform.position - transform.position).sqrMagnitude;
                if (sqr < best) { best = sqr; pick = sub; }
            }
            if (pick == null) { _aswAt = Time.time + 0.5f; return; }

            pick.ConfirmContact(6f + 2f * tier);   // 선체 소나
            Vector3 cue = pick.LastKnownPosition;
            Vector3 from = transform.position;
            if (rig != null && rig.TorpedoPoint(cue, out var tube)) from = tube;
            Vector3 heading = cue - from; heading.y = 0f;
            var go = PoolManager.Instance?.Spawn(armory.aswTorpedo, from, Quaternion.LookRotation(heading.sqrMagnitude > 0.01f ? heading.normalized : transform.forward));
            float damage = AswDamage(tier) * RunUpgrades.DamageMultiplier(ModuleType.AswLauncher);
            if (go != null && go.TryGetComponent<AswTorpedo>(out var torpedo)) { torpedo.LaunchFromTube(from, heading.normalized, cue, damage); torpedo.StatsKey = EscortKey; }
            _aswAt = Time.time + AswReload(tier) / (RunUpgrades.FireRateMultiplier(ModuleType.AswLauncher) * _relayRate);
            TorpedoLaunches++;
            AswStrikes++;
            AudioManager.Play(SfxId.EscortTorpedoLaunch, from, 1f, 0.7f);
            Explosions.Spawn(new Vector3(from.x, 0f, from.z), 0.3f, Explosions.Kind.Water);
            TaskForceWorldFeedback.Instance?.PlayRig("ASW", transform.position, transform.eulerAngles.y, 12f, 1.4f, 240f);
            CombatLog.Add("편대", $"대잠 호위함 경어뢰 발사 → {CombatLog.Describe(pick)}");
        }

        /// <summary>T3 대잠 로켓: 접촉 확정·드러난 잠수함 쪽으로 2발(경어뢰 탄두). 18초.</summary>
        private void LaunchAswRockets(EscortArmory armory, EscortTurrets rig, int tier)
        {
            if (rig == null || rig.Mounts(EscortTurrets.MountKind.Asw) == 0 || armory.aswRocket == null || armory.aswTorpedo == null) { _rocketAt = Time.time + 5f; return; }
            SubmarineBase pick = null;
            float best = StrikeRange * StrikeRange;
            foreach (var t in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Submarine))
            {
                if (t is not SubmarineBase sub || !sub.IsAlive || !(sub.IsContactConfirmed || sub.IsRevealed)) continue;
                float sqr = (sub.LastKnownPosition - transform.position).sqrMagnitude;
                if (sqr < best) { best = sqr; pick = sub; }
            }
            if (pick == null) { _rocketAt = Time.time + 0.5f; return; }
            float damage = AswDamage(tier) * 0.8f * RunUpgrades.DamageMultiplier(ModuleType.AswLauncher);
            for (int k = 0; k < 2; k++)
            {
                if (!rig.EngageMount(EscortTurrets.MountKind.Asw, 0, pick.LastKnownPosition, out var mz, out _, 2f)) break;
                var go = PoolManager.Instance?.Spawn(armory.aswRocket, mz, Quaternion.LookRotation((pick.LastKnownPosition - mz).normalized + Vector3.up));
                go?.GetComponent<Missile>()?.LaunchAsw(pick.LastKnownPosition + Random.insideUnitSphere * 2f, armory.aswTorpedo, damage);
                RocketLaunches++;
            }
            _rocketAt = Time.time + 18f / _relayRate;
            AudioManager.Play(SfxId.EscortMissileLaunch, transform.position, 0.8f, 0.75f);
        }

        // ------------------------------------------------------------ 도구

        /// <summary>from 둘레 range 안의 드러난 적(수상함·항공기)을 가까운 순으로 모은다. 섬에 가린 수상함은 뺀다.</summary>
        private void Gather(Vector3 from, float range, bool surface, bool air)
        {
            _candidates.Clear();
            float r2 = range * range;
            void Add(IReadOnlyList<ITargetable> list, bool isSurface)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var t = list[i];
                    if (t == null || !t.IsAlive || !t.IsRevealed || t.Transform == null) continue;
                    Vector3 d = t.Transform.position - from; d.y = 0f;
                    if (d.sqrMagnitude > r2) continue;
                    if (isSurface && Game.World.Islands.BlocksShipLine(from, t.Transform.position)) continue;
                    _candidates.Add((t, d.sqrMagnitude));
                }
            }
            if (surface) Add(TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Surface), true);
            if (air) Add(TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Aircraft), false);
            _candidates.Sort((a, b) => a.sqr.CompareTo(b.sqr));
        }

        private static Vector3 Lead(Vector3 from, ITargetable t, float speed)
        {
            Vector3 p = t.Transform.position;
            Vector3 v = t switch
            {
                IHasVelocity moving => moving.Velocity,
                EnemyController e => e.MoveVelocity,
                _ => Vector3.zero,
            };
            return v.sqrMagnitude > 0.01f ? Ballistics.PredictIntercept(from, p, v, speed) : p;
        }
    }
}

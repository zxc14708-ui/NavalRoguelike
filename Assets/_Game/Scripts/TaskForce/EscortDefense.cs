using System;
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
    /// 호위함 자율 능력(지원 스킬 대체, 2026-10-02). 전투 불능이 아니면 항상 스스로 작동한다.
    ///   - 모든 호위함: 소형 함포 — 반경 <see cref="GunRange"/>m 안의 가장 가까운 적 수상함·항공기(섬에 가리면 못 쏨).
    ///   - 방공(CAP): 함대공 요격 — 반경 <see cref="SamRange"/>m 안의 적 미사일 중 기함에 가장 가까운 것을 격추.
    ///   - 전자전(EW): 근접 교란 — 반경 <see cref="JamRange"/>m 안의 교란되지 않은 적 미사일 하나.
    ///   - 대잠(ASW): 자동 대잠 타격 — 반경 <see cref="AswRange"/>m 안의 잠수함(잠항 중이어도)을 찾아 접촉을 확정하고 경어뢰 피해.
    ///   - 미사일(STK): 주기적 대함 타격 — 반경 <see cref="StrikeRange"/>m 안 드러난 수상 표적 중 가치가 가장 높은 것.
    /// 개량 단계(T0~T3)마다 강해진다. 성장 카드의 "공격력"·"연사력"(모든 무장)은 호위함 함포·타격에도 적용된다.
    /// 판정은 즉시 적용하고 예광선·전술 리그(TaskForceWorldFeedback)는 연출이다. 격침 보상은 적 격침과 같다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EscortDefense : MonoBehaviour
    {
        public const float GunRange = 24f, GunInterval = 0.5f, GunDamage = 2.5f;
        public const float SamRange = 42f, JamRange = 36f, AswRange = 40f, StrikeRange = 60f;

        public static float GunDamageFor(int tier) => GunDamage * (1f + 0.2f * Mathf.Clamp(tier, 0, 3));
        public static float SamReload(int tier) => 4.0f - 0.6f * Mathf.Clamp(tier, 0, 3);
        public static float JamReload(int tier) => 6.0f - 1.0f * Mathf.Clamp(tier, 0, 3);
        public static float JamDuration(int tier) => 1.6f + 0.3f * Mathf.Clamp(tier, 0, 3);
        public static float AswReload(int tier) => 12f - 2f * Mathf.Clamp(tier, 0, 3);
        public static float AswDamage(int tier) => 20f + 8f * Mathf.Clamp(tier, 0, 3);
        public static float StrikeReload(int tier) => 14f - 2f * Mathf.Clamp(tier, 0, 3);
        public static float StrikeDamage(int tier) => 30f + 12f * Mathf.Clamp(tier, 0, 3);

        private Func<EscortRole> _role;
        private Func<bool> _ready;
        private Func<int> _tier;
        private Func<Vector3> _muzzle;
        private float _gunAt, _samAt, _jamAt, _aswAt, _strikeAt;
        private EscortTurrets _turrets;
        private ITargetable _gunAimTarget;
        private float _gunAimDeadline;

        /// <summary>포탑이 표적으로 도는 동안 함포 사격을 미루는 최대 시간(초). 그보다 오래 걸리면 그냥 쏜다.</summary>
        public const float GunSlewWait = 0.6f;

        /// <summary>지금 모델의 가동부(모델을 갈아 끼우면 새로 찾는다). 없으면 null.</summary>
        private EscortTurrets Turrets
        {
            get
            {
                if (_turrets == null) _turrets = GetComponentInChildren<EscortTurrets>();
                return _turrets;
            }
        }

        /// <summary>검증용 누적 수.</summary>
        public int GunShots { get; private set; }
        public int SamKills { get; private set; }
        public int Jams { get; private set; }
        public int AswStrikes { get; private set; }
        public int SurfaceStrikes { get; private set; }

        public void Init(Func<EscortRole> role, Func<bool> ready, Func<int> tier, Func<Vector3> muzzle)
        {
            _role = role;
            _ready = ready;
            _tier = tier;
            _muzzle = muzzle;
            float now = Time.time;
            _gunAt = now + UnityEngine.Random.Range(0f, GunInterval);
            // 합류·역할 지정 직후 곧바로 큰 타격이 나가지 않게 첫 재장전의 절반을 기다린다
            _aswAt = now + AswReload(0) * 0.5f;
            _strikeAt = now + StrikeReload(0) * 0.5f;
        }

        /// <summary>재장전을 비운다(검증용).</summary>
        public void ResetCooldowns() => _gunAt = _samAt = _jamAt = _aswAt = _strikeAt = 0f;

        private void Update()
        {
            if (_ready == null || !_ready()) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;
            int tier = _tier != null ? _tier() : 0;
            var role = _role != null ? _role() : EscortRole.None;
            Vector3 flagship = gm.Player != null ? gm.Player.transform.position : transform.position;

            if (Time.time >= _gunAt) FireGun(tier);
            switch (role)
            {
                case EscortRole.AirDefense when Time.time >= _samAt: FireSam(tier, flagship); break;
                case EscortRole.ElectronicWarfare when Time.time >= _jamAt: Jam(tier); break;
                case EscortRole.AntiSubmarine when Time.time >= _aswAt: AswStrike(tier); break;
                case EscortRole.SurfaceStrike when Time.time >= _strikeAt: SurfaceStrike(tier); break;
            }
        }

        // ------------------------------------------------------------ 함포(공통)

        private void FireGun(int tier)
        {
            Vector3 self = transform.position;
            ITargetable best = null;
            float bestSqr = GunRange * GunRange;
            Consider(TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Surface), true);
            Consider(TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Aircraft), false);
            if (best is not IDamageable target) { _gunAt = Time.time + 0.2f; _gunAimTarget = null; return; }

            Vector3 hit = best.Transform.position + Vector3.up * 0.6f;
            // 포탑을 표적으로 돌린다. 새 표적이면 포탑이 향할 때까지(최대 GunSlewWait) 기다렸다 쏜다.
            Vector3 muzzle = _muzzle();
            if (!ReferenceEquals(best, _gunAimTarget)) { _gunAimTarget = best; _gunAimDeadline = Time.time + GunSlewWait; }
            var rig = Turrets;
            if (rig != null && rig.Engage(EscortTurrets.MountKind.Gun, hit, out var mz, out bool aligned))
            {
                if (!aligned && Time.time < _gunAimDeadline) { _gunAt = Time.time + 0.06f; return; }
                muzzle = mz;
            }

            _gunAt = Time.time + GunInterval / RunUpgrades.FireRateMultiplier(ModuleType.Autocannon);
            float damage = GunDamageFor(tier) * (1f + RunUpgrades.Get(RunStat.Damage));
            target.TakeDamage(new DamageInfo(damage, hit, (hit - self).normalized, DamageSource.Gun, "Escort gun"));
            GunShots++;
            Tracer.Spawn(muzzle, hit, new Color(1f, 0.85f, 0.45f, 1f), 0.10f, 0.09f);
            Explosions.Spawn(hit, 0.22f, Explosions.Kind.Impact);
            // 개량될수록 함포가 커진다(T2부터 초계함급 주포): 낮고 묵직한 소리
            AudioManager.Play(SfxId.EscortGunShot, muzzle, 1f, tier >= 2 ? 0.82f : 1.12f);

            void Consider(IReadOnlyList<ITargetable> list, bool surface)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var t = list[i];
                    if (t == null || !t.IsAlive || !t.IsRevealed || t.Transform == null) continue;
                    Vector3 d = t.Transform.position - self; d.y = 0f;
                    float sqr = d.sqrMagnitude;
                    if (sqr >= bestSqr) continue;
                    if (surface && Game.World.Islands.BlocksShipLine(self, t.Transform.position)) continue;
                    bestSqr = sqr;
                    best = t;
                }
            }
        }

        // ------------------------------------------------------------ 방공: 함대공 요격

        /// <summary>기함에 가장 가까운(가장 급한) 적 미사일을 반경 안에서 한 발 격추.</summary>
        private void FireSam(int tier, Vector3 flagship)
        {
            Missile pick = null;
            float best = float.MaxValue;
            var missiles = TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Missile);
            for (int i = 0; i < missiles.Count; i++)
            {
                if (missiles[i] is not Missile m || !m.IsAlive || !m.IsThreat) continue;
                if ((m.transform.position - transform.position).sqrMagnitude > SamRange * SamRange) continue;
                float urgency = (m.transform.position - flagship).sqrMagnitude;
                if (urgency < best) { best = urgency; pick = m; }
            }
            if (pick == null) { _samAt = Time.time + 0.2f; return; }

            _samAt = Time.time + SamReload(tier);
            Vector3 at = pick.transform.position;
            Vector3 from = LaunchPoint(EscortTurrets.MountKind.Sam, at, "VLSLaunchPoint");
            Tracer.Spawn(from, at, TaskForceEscortFormation.RoleColor(EscortRole.AirDefense), 0.35f, 0.16f);
            AudioManager.Play(SfxId.EscortMissileLaunch, from, 1f, 1.2f);
            pick.TakeDamage(new DamageInfo(999f, at, (at - transform.position).normalized, DamageSource.Missile, "Escort SAM"));
            SamKills++;
            CombatLog.Add("편대", "방공 호위함 함대공 요격");
        }

        // ------------------------------------------------------------ 전자전: 근접 교란

        /// <summary>반경 안의 교란되지 않은 적 미사일 중 가장 가까운 것 하나를 교란.</summary>
        private void Jam(int tier)
        {
            Missile pick = null;
            float best = JamRange * JamRange;
            var missiles = TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Missile);
            for (int i = 0; i < missiles.Count; i++)
            {
                if (missiles[i] is not Missile m || !m.IsAlive || !m.IsThreat || m.IsJammed) continue;
                float sqr = (m.transform.position - transform.position).sqrMagnitude;
                if (sqr < best) { best = sqr; pick = m; }
            }
            if (pick == null || !pick.Jam(JamDuration(tier))) { _jamAt = Time.time + 0.2f; return; }

            _jamAt = Time.time + JamReload(tier);
            Jams++;
            Vector3 from = LaunchPoint(EscortTurrets.MountKind.Decoy, pick.transform.position, "EWOrigin");
            Tracer.Spawn(from, pick.transform.position, TaskForceEscortFormation.RoleColor(EscortRole.ElectronicWarfare), 0.45f, 0.12f);
            AudioManager.Play(SfxId.EscortJam, from, 1f, 1.6f);
            TaskForceWorldFeedback.Instance?.PlayRig("EW", transform.position, transform.eulerAngles.y, 10f, 1.0f, 0f);
        }

        // ------------------------------------------------------------ 대잠: 자동 대잠 타격

        /// <summary>반경 안 가장 가까운 잠수함(잠항 중이어도)의 접촉을 확정하고 경어뢰 피해를 준다.</summary>
        private void AswStrike(int tier)
        {
            SubmarineBase pick = null;
            float best = AswRange * AswRange;
            foreach (var t in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Submarine))
            {
                if (t is not SubmarineBase sub || !sub.IsAlive) continue;
                float sqr = (sub.transform.position - transform.position).sqrMagnitude;
                if (sqr < best) { best = sqr; pick = sub; }
            }
            if (pick == null) { _aswAt = Time.time + 0.5f; return; }

            _aswAt = Time.time + AswReload(tier) / RunUpgrades.FireRateMultiplier(ModuleType.AswLauncher);
            Vector3 at = pick.transform.position;
            pick.ConfirmContact(6f + 2f * tier);
            float damage = AswDamage(tier) * RunUpgrades.DamageMultiplier(ModuleType.AswLauncher);
            pick.TakeDamage(new DamageInfo(damage, at, Vector3.down, DamageSource.Torpedo, "Escort ASW"));
            AswStrikes++;
            Vector3 from = _muzzle();
            if (Turrets != null && Turrets.TorpedoPoint(at, out var tube)) from = tube;   // 표적 쪽 현측 어뢰관
            Tracer.Spawn(from, new Vector3(at.x, 0f, at.z), TaskForceEscortFormation.RoleColor(EscortRole.AntiSubmarine), 0.6f, 0.14f);
            Explosions.Spawn(new Vector3(at.x, 0f, at.z), 0.7f, Explosions.Kind.Water);
            AudioManager.Play(SfxId.EscortTorpedoLaunch, from, 1f, 0.7f);
            AudioManager.Play(SfxId.Explosion, new Vector3(at.x, 0f, at.z), 0.45f, 0.75f);   // 수중 폭발
            TaskForceWorldFeedback.Instance?.PlayRig("ASW", transform.position, transform.eulerAngles.y, 12f, 1.4f, 240f);
            CombatLog.Add("편대", $"대잠 호위함 자동 대잠 타격 → {CombatLog.Describe(pick)} · 피해 {damage:0}");
        }

        // ------------------------------------------------------------ 미사일: 주기적 대함 타격

        /// <summary>반경 안 드러난 수상 표적 중 가치가 가장 높은 것(같으면 가까운 것)에 대함 타격.</summary>
        private void SurfaceStrike(int tier)
        {
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

            _strikeAt = Time.time + StrikeReload(tier) / RunUpgrades.FireRateMultiplier(ModuleType.Vls);
            Vector3 at = pick.Transform.position;
            float damage = StrikeDamage(tier) * RunUpgrades.DamageMultiplier(ModuleType.Vls);
            ((IDamageable)pick).TakeDamage(new DamageInfo(damage, at, (at - transform.position).normalized, DamageSource.Missile, "Escort strike"));
            SurfaceStrikes++;
            Vector3 from = LaunchPoint(EscortTurrets.MountKind.Strike, at, null);
            Tracer.Spawn(from, at + Vector3.up, TaskForceEscortFormation.RoleColor(EscortRole.SurfaceStrike), 0.7f, 0.2f);
            Explosions.Spawn(at + Vector3.up * 0.6f, 0.85f, Explosions.Kind.Impact);
            var flat = at - transform.position; flat.y = 0f;
            TaskForceWorldFeedback.Instance?.PlayRig("STK", at, flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flat).eulerAngles.y + 42f : 0f, 8f, 1.3f, 0f);
            AudioManager.Play(SfxId.EscortMissileLaunch, from, 1f, 0.92f);
            AudioManager.Play(SfxId.Explosion, at, 0.55f, 1f);
            CombatLog.Add("편대", $"미사일 호위함 대함 타격 → {CombatLog.Describe(pick)} · 피해 {damage:0}");
        }

        /// <summary>발사 위치: 그 종류의 포탑을 표적으로 돌려 발사점을, 없으면 이름 붙은 소켓(fallbackSocket)을, 그것도 없으면 SupportOrigin.</summary>
        private Vector3 LaunchPoint(EscortTurrets.MountKind kind, Vector3 target, string fallbackSocket)
        {
            var rig = Turrets;
            if (rig != null && rig.Engage(kind, target, out var p, out _)) return p;
            if (rig != null && fallbackSocket != null && rig.TrySocket(fallbackSocket, out var s)) return s;
            return _muzzle();
        }

        /// <summary>짧은 예광선·유도선(판정 없음).</summary>
        private sealed class Tracer : MonoBehaviour
        {
            private static Material s_material;
            private LineRenderer _line;
            private Color _color;
            private float _life, _age;

            public static void Spawn(Vector3 from, Vector3 to, Color color, float life, float width)
            {
                var go = new GameObject("Escort tracer");
                var line = go.AddComponent<LineRenderer>();
                if (s_material == null)
                {
                    var shader = Shader.Find("Sprites/Default");
                    if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                    s_material = new Material(shader) { name = "Escort tracer (Runtime)" };
                }
                line.sharedMaterial = s_material;
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.SetPosition(0, from);
                line.SetPosition(1, to);
                line.startWidth = width;
                line.endWidth = width * 0.4f;
                line.numCapVertices = 2;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.startColor = line.endColor = color;
                var tracer = go.AddComponent<Tracer>();
                tracer._line = line;
                tracer._color = color;
                tracer._life = Mathf.Max(0.05f, life);
            }

            private void Update()
            {
                _age += Time.deltaTime;
                float a = 1f - Mathf.Clamp01(_age / _life);
                var c = new Color(_color.r, _color.g, _color.b, _color.a * a);
                _line.startColor = _line.endColor = c;
                if (_age >= _life) Destroy(gameObject);
            }
        }
    }
}

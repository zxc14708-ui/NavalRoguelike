using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Modules;
using Game.View;

namespace Game.Ship
{
    /// <summary>
    /// 손상 통제 2단계 — 화재 · 탄약고(2026-11-21, Docs/SESSION_LOG_DAMAGE_PLAN_2026-11-21.md 기획 A).
    ///   - 불이 붙음: 맞은 블록에 — 미사일 35%(위에서 꽂히면 50%), 큰 포탄(8 이상) 12%, 유폭 80%. "자동 소화 장치" 카드가 줄인다.
    ///   - 불: 세기 0~1. 처음 0.35, 초당 6%씩 커진다. 블록 내구를 초당 2.5 × 세기, 선체를 초당 0.35 × 세기 깎는다.
    ///     3초마다 세기 × 35% 확률로 맞닿은(맨해튼 1) 블록에 옮겨 붙는다. 부서진 블록에서도 탄다(잔해 화재).
    ///   - 탄약고 · VLS · 유도로켓 발사관에 세기 0.35 이상 불이 붙으면 9초 유폭 카운트다운(0.2 아래로 줄면 멈춤) — 0이 되면 그 블록이 터진다.
    ///   - 끄기: 손상 통제반(ShipDamageControl) 1조가 초당 세기 0.2(교전 중 절반)를 줄인다.
    ///   - 연기: 타는 블록마다 레이더 탐지 −8%(최대 −30%).
    ///   - 정비 화면에 들어가면 모두 꺼진다.
    /// </summary>
    public sealed class ShipFire : MonoBehaviour
    {
        public static bool Enabled = true;
        /// <summary>검증용: 옮겨 붙지 않게(불 하나 진화만 따로 잴 때).</summary>
        public static bool DevNoSpread;

        public const float IgniteMissile = 0.35f, IgniteMissileTop = 0.5f, IgniteShell = 0.12f, IgniteShellMinDamage = 8f, IgniteCookOff = 0.8f;
        public const float StartIntensity = 0.35f, Growth = 0.06f, ModuleBurn = 2.5f, HullBurn = 0.35f;
        public const float SpreadEvery = 3f, SpreadChance = 0.35f;
        public const float CookOffSeconds = 9f, CookOffStart = 0.35f, CookOffStop = 0.2f;
        public const float FightRate = 0.2f;
        public const float SmokePerFire = 0.08f, SmokeMax = 0.3f;

        public sealed class Fire
        {
            public ModuleInstance Module;
            public float Intensity;
            public float CookOff = -1f;   // 남은 초(−1 = 카운트다운 없음)
            public float SpreadTimer;
            public float FxDebt, SmokeDebt;
        }

        private ShipController _ship;
        private ShipGrid _grid;
        private DamageResolver _resolver;
        private readonly List<Fire> _fires = new();
        private float _bannerAt = -99f, _cookBannerAt = -99f;

        public IReadOnlyList<Fire> Fires => _fires;
        /// <summary>레이더 탐지 배율(연기).</summary>
        public float DetectionMultiplier => 1f - Mathf.Min(SmokeMax, SmokePerFire * _fires.Count);
        /// <summary>검증용: 붙은 불 · 끈 불 · 옮겨 붙은 수 · 유폭 수.</summary>
        public int Ignited { get; private set; }
        public int Extinguished { get; private set; }
        public int Spread { get; private set; }
        public int CookOffs { get; private set; }

        public static bool IsCookOffRisk(ModuleInstance m) => m?.Definition != null &&
            m.Definition.Type is ModuleType.Magazine or ModuleType.Vls or ModuleType.GuidedRocket;

        private void Awake()
        {
            _ship = GetComponentInParent<ShipController>();
            _resolver = GetComponent<DamageResolver>();
            _grid = _resolver != null ? _resolver.Grid : null;
        }

        private void OnEnable()
        {
            DamageResolver.Resolved += OnHit;
            GameEvents.StateChanged += OnState;
            GameEvents.ModuleRemoved += OnRemoved;
        }

        private void OnDisable()
        {
            DamageResolver.Resolved -= OnHit;
            GameEvents.StateChanged -= OnState;
            GameEvents.ModuleRemoved -= OnRemoved;
        }

        private void OnState(GameState s) { if (s == GameState.Refit) ExtinguishAll(); }
        private void OnRemoved(ModuleInstance m) => _fires.RemoveAll(f => f.Module == m);

        public void ExtinguishAll()
        {
            _fires.Clear();
            ApplySmoke();
        }

        public bool IsBurning(ModuleInstance m) => _fires.Exists(f => f.Module == m);
        public Fire FireOn(ModuleInstance m) => _fires.Find(f => f.Module == m);

        private void OnHit(DamageResolver r, DamageResolver.HitRecord rec)
        {
            if (!Enabled || DamageResolver.DevMeasureOnly || r != _resolver || rec.Module == null || _ship == null || !_ship.IsAlive) return;
            var info = rec.Info;
            float chance = info.Source switch
            {
                DamageSource.Missile => info.Direction.y < -0.5f ? IgniteMissileTop : IgniteMissile,
                DamageSource.Gun when info.Amount >= IgniteShellMinDamage => IgniteShell,
                DamageSource.CookOff => IgniteCookOff,
                _ => 0f,
            };
            chance *= 1f - Mathf.Clamp01(RunUpgrades.Get(RunStat.Firefighting));
            if (chance > 0f && Random.value < chance) Ignite(rec.Module);
        }

        /// <summary>블록에 불을 붙인다(검사도 쓴다). 이미 타면 세기를 올린다.</summary>
        public Fire Ignite(ModuleInstance m, float intensity = StartIntensity)
        {
            if (m == null) return null;
            var f = FireOn(m);
            if (f != null) { f.Intensity = Mathf.Min(1f, f.Intensity + intensity * 0.5f); return f; }
            f = new Fire { Module = m, Intensity = intensity, SpreadTimer = SpreadEvery };
            _fires.Add(f);
            Ignited++;
            CombatLog.Add("화재", $"{m.Definition.DisplayName} ({m.Origin.X},{m.Origin.Z}) 불이 붙음{(IsCookOffRisk(m) ? " — 유폭 위험" : "")}");
            if (Time.time - _bannerAt > 8f)
            {
                _bannerAt = Time.time;
                GameEvents.RaiseBanner(IsCookOffRisk(m) ? $"화재 — {m.Definition.DisplayName}에 불! 유폭 위험(손상 통제반 진화 중)" : $"화재 — {m.Definition.DisplayName}에 불이 붙었습니다");
            }
            ApplySmoke();
            return f;
        }

        /// <summary>손상 통제반이 불 하나를 끈다(amount = 줄일 세기). 다 껐으면 true.</summary>
        public bool Work(Fire f, float amount)
        {
            if (f == null || !_fires.Contains(f)) return false;
            f.Intensity -= amount;
            if (f.Intensity > 0f) return false;
            _fires.Remove(f);
            Extinguished++;
            CombatLog.Add("화재", $"{f.Module.Definition.DisplayName} 불 끔 · 남은 {_fires.Count}");
            ApplySmoke();
            return true;
        }

        private void ApplySmoke()
        {
            if (_ship != null && _ship.Systems != null) _ship.Systems.SmokeDetectionMultiplier = DetectionMultiplier;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _ship == null || _grid == null) return;
            float auto = Mathf.Clamp01(RunUpgrades.Get(RunStat.Firefighting));
            for (int i = _fires.Count - 1; i >= 0; i--)
            {
                var f = _fires[i];
                if (f.Module == null || !_grid.Modules.Contains(f.Module)) { _fires.RemoveAt(i); continue; }
                f.Intensity = Mathf.Min(1f, f.Intensity + Growth * (1f - auto) * dt - auto * 0.08f * dt);
                if (f.Intensity <= 0f) { _fires.RemoveAt(i); Extinguished++; continue; }

                if (!f.Module.IsDestroyed) f.Module.TakeDamage(ModuleBurn * f.Intensity * dt);
                if (_ship.IsAlive) _ship.ApplyHullDamage(HullBurn * f.Intensity * dt, "화재", hostile: false);

                // 옮겨 붙기
                f.SpreadTimer -= dt;
                if (f.SpreadTimer <= 0f)
                {
                    f.SpreadTimer = SpreadEvery;
                    if (!DevNoSpread && Random.value < SpreadChance * f.Intensity * (1f - auto)) TrySpread(f);
                }

                // 유폭 카운트다운
                if (IsCookOffRisk(f.Module) && !f.Module.IsDestroyed)
                {
                    if (f.CookOff < 0f && f.Intensity >= CookOffStart) f.CookOff = CookOffSeconds;
                    else if (f.CookOff >= 0f && f.Intensity < CookOffStop) f.CookOff = -1f;
                    if (f.CookOff >= 0f)
                    {
                        f.CookOff -= dt;
                        if (Time.time - _cookBannerAt > 3f)
                        {
                            _cookBannerAt = Time.time;
                            GameEvents.RaiseBanner($"유폭 경고 — {f.Module.Definition.DisplayName} 화재, {Mathf.CeilToInt(Mathf.Max(0f, f.CookOff))}초 안에 끄지 못하면 폭발");
                        }
                        if (f.CookOff <= 0f) CookOff(f);
                    }
                }
                Emit(f, dt);
            }
            ApplySmoke();
        }

        private void TrySpread(Fire f)
        {
            var o = f.Module.Origin;
            var options = new List<ModuleInstance>();
            foreach (var c in f.Module.OccupiedCoords)
                for (int d = 0; d < 4; d++)
                {
                    var n = new GridCoord(c.X + (d == 0 ? 1 : d == 1 ? -1 : 0), c.Z + (d == 2 ? 1 : d == 3 ? -1 : 0));
                    var m = _grid.Get(n);
                    if (m != null && m != f.Module && !IsBurning(m) && !options.Contains(m)) options.Add(m);
                }
            if (options.Count == 0) return;
            var target = options[Random.Range(0, options.Count)];
            Ignite(target, StartIntensity * 0.8f);
            Spread++;
        }

        private void CookOff(Fire f)
        {
            var m = f.Module;
            f.CookOff = -1f;
            CookOffs++;
            CombatLog.Add("유폭", $"{m.Definition.DisplayName} ({m.Origin.X},{m.Origin.Z}) 화재 유폭");
            GameEvents.RaiseBanner($"유폭 — {m.Definition.DisplayName}이 터졌습니다");
            if (m.Definition.Type == ModuleType.Magazine)
            {
                m.TakeDamage(m.Hp + 1f);   // 탄약고는 부서질 때 MagazineModule이 주변 유폭을 처리한다
            }
            else
            {
                m.TakeDamage(m.Hp + 1f);
                _resolver?.ApplyCookOff(m.Origin, 30f, 1, 0.25f);
                Vector3 at = m.Runtime != null ? m.Runtime.transform.position + Vector3.up * 0.6f : _ship.transform.position;
                Explosions.Spawn(at, 1.6f, Explosions.Kind.Impact);
                AudioManager.Play(Game.Data.SfxId.Explosion, at, 1f, 0.7f);
            }
        }

        private void Emit(Fire f, float dt)
        {
            var rt = f.Module.Runtime;
            if (rt == null) return;
            Vector3 at = rt.transform.position + Vector3.up * 0.55f;
            float density = DisplaySettings.DecorDensity;
            f.FxDebt += (6f + 14f * f.Intensity) * density * dt;
            while (f.FxDebt >= 1f)
            {
                f.FxDebt -= 1f;
                DecorFx.Emit(DecorFx.Fire, at + Random.insideUnitSphere * 0.35f, Vector3.up * Random.Range(1.0f, 2.2f) + Random.insideUnitSphere * 0.3f,
                    Random.Range(0.25f, 0.55f), Random.Range(0.4f, 0.8f) * (0.6f + f.Intensity),
                    Color.Lerp(new Color(1f, 0.85f, 0.45f, 0.95f), new Color(1f, 0.35f, 0.08f, 0.85f), Random.value));
            }
            f.SmokeDebt += (3f + 5f * f.Intensity) * density * dt;
            while (f.SmokeDebt >= 1f)
            {
                f.SmokeDebt -= 1f;
                float g = Random.Range(0.08f, 0.16f);
                DecorFx.Emit(DecorFx.WreckSmoke, at + Vector3.up * 0.4f + Random.insideUnitSphere * 0.3f, Vector3.up * Random.Range(1.5f, 2.6f) + DecorFx.Wind * 0.6f,
                    Random.Range(2f, 3.2f), Random.Range(0.7f, 1.3f), new Color(g, g, g, 0.6f));
            }
        }
    }
}

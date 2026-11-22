using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;
using Game.View;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 포격 지원 요청(2026-11-22, 지원 블록). 일정 시간마다 기함 둘레(80m)에서 적 수상함이 가장 많이 모인 곳에 경고 원을 띄우고,
    /// 2.2초 뒤 화면 밖 순양함의 포탄이 쏟아진다. 단계별: 간격 30 · 27 · 24초, 포탄 6 · 9 · 12발, 낙탄 반경 7 · 8.5 · 10m.
    /// 포탄 한 발 = 반경 3.5m 안 적 수상함에 피해 18(성장 카드 "공격력" 적용). 아군 · 기함에는 피해가 없다. 표적이 없으면 기다린다.
    /// </summary>
    public sealed class FireSupportModule : ModuleRuntime, ISupportTimer
    {
        public const float SearchRadius = 80f;
        public const float WarnTime = 2.2f;
        public const float ShellBlast = 3.5f;
        public const float ShellDamage = 18f;
        public const float BarrageSpan = 1.4f;   // 포탄이 떨어지는 시간 폭

        public static float IntervalFor(int level) => level >= 3 ? 24f : level == 2 ? 27f : 30f;
        public static int ShellsFor(int level) => level >= 3 ? 12 : level == 2 ? 9 : 6;
        public static float RadiusFor(int level) => level >= 3 ? 10f : level == 2 ? 8.5f : 7f;

        private static readonly Color WarnColor = new(1f, 0.42f, 0.18f, 0.95f);

        private float _timer;
        private bool _firing;

        private int Level => Instance != null ? Instance.Level : 1;
        public float TimeLeft => Mathf.Max(0f, _timer);
        /// <summary>검증용: 요청 수 · 떨어진 포탄 수 · 준 피해 합 · 마지막 낙탄 중심.</summary>
        public int Barrages { get; private set; }
        public int ShellsLanded { get; private set; }
        public float DamageDealt { get; private set; }
        public Vector3 LastCenter { get; private set; }

        protected override void OnInitialized()
        {
            _timer = IntervalFor(Level) * 0.5f;   // 첫 요청은 절반만 기다린다
            SupportTimers.Register(this);
        }

        private void OnDestroy() => SupportTimers.Unregister(this);

        protected override void Tick(float dt)
        {
            if (_firing) return;
            _timer -= dt;
            if (_timer > 0f) return;
            if (!TryPickCenter(out var center)) { _timer = 1f; return; }   // 표적이 없으면 1초마다 다시 본다
            StartCoroutine(Barrage(center, Level));
            _timer = IntervalFor(Level);
        }

        /// <summary>검증용: 지금 바로 요청한다(표적이 없으면 false).</summary>
        public bool DevCallNow()
        {
            if (_firing || !TryPickCenter(out var center)) return false;
            StartCoroutine(Barrage(center, Level));
            _timer = IntervalFor(Level);
            return true;
        }

        /// <summary>기함 둘레에서 반경 안 적 수상함이 가장 많은 적의 자리(같으면 체력이 큰 쪽).</summary>
        private bool TryPickCenter(out Vector3 center)
        {
            center = default;
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null) return false;
            Vector3 self = gm.Player.transform.position;
            float r = RadiusFor(Level);
            var list = TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Surface);
            int best = 0; float bestHp = -1f; bool found = false;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is not EnemyController e || !e.IsAlive || !e.IsRevealed) continue;
                Vector3 p = e.transform.position;
                if (Flat(p - self).sqrMagnitude > SearchRadius * SearchRadius) continue;
                if (Flat(p - self).sqrMagnitude < 12f * 12f) continue;   // 기함에 붙은 적은 피한다(낙탄이 기함 옆)
                int n = 0;
                for (int j = 0; j < list.Count; j++)
                    if (list[j] is EnemyController o && o.IsAlive && Flat(o.transform.position - p).sqrMagnitude <= r * r) n++;
                if (n > best || (n == best && e.CurrentHp > bestHp)) { best = n; bestHp = e.CurrentHp; center = p; found = true; }
            }
            return found;
        }

        private IEnumerator Barrage(Vector3 center, int level)
        {
            _firing = true;
            Barrages++;
            LastCenter = center;
            float radius = RadiusFor(level);
            int shells = ShellsFor(level);
            SupportFx.WarnRing(center, radius, WarnTime, WarnColor);
            GameEvents.RaiseBanner($"포격 지원 — {shells}발 낙탄 {WarnTime:0.#}초 전");
            CombatLog.Add(LogName, $"포격 지원 요청 · {shells}발 · 반경 {radius:0.#}m");
            yield return new WaitForSeconds(WarnTime - 0.6f);

            // 낙하 줄기는 0.6초 먼저 하늘에서 내려온다
            var points = new List<Vector3>();
            for (int i = 0; i < shells; i++)
            {
                Vector2 o = Random.insideUnitCircle * radius;
                points.Add(new Vector3(center.x + o.x, 0f, center.z + o.y));
            }
            float step = BarrageSpan / Mathf.Max(1, shells - 1);
            for (int i = 0; i < shells; i++)
            {
                StartCoroutine(Shell(points[i], 0.6f));
                yield return new WaitForSeconds(step);
            }
            yield return new WaitForSeconds(0.7f);
            _firing = false;
        }

        private IEnumerator Shell(Vector3 at, float fall)
        {
            // 화면 밖 순양함 쪽(기함 기준 뒤 위)에서 비스듬히 내려오는 줄기
            var gm = GameManager.Instance;
            Vector3 from = at + new Vector3(18f, 60f, -26f);
            if (gm != null && gm.Player != null) from = at + (Flat(at - gm.Player.transform.position).normalized * -20f) + Vector3.up * 60f;
            float t = 0f;
            Vector3 prev = from;
            while (t < fall)
            {
                t += Time.deltaTime;
                Vector3 p = Vector3.Lerp(from, at, Mathf.Clamp01(t / fall));
                SupportFx.Tracer(prev, p, new Color(1f, 0.85f, 0.55f, 0.9f), 0.12f, 0.18f);
                prev = p;
                yield return null;
            }
            Land(at);
        }

        private void Land(Vector3 at)
        {
            ShellsLanded++;
            float damage = ShellDamage * (1f + RunUpgrades.Get(RunStat.Damage));
            var list = TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Surface);
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (i >= list.Count) continue;
                var t = list[i];
                if (t == null || !t.IsAlive || t.Transform == null) continue;
                if (Flat(t.Transform.position - at).sqrMagnitude > ShellBlast * ShellBlast) continue;
                if (t is IDamageable d && d.IsAlive)
                {
                    d.TakeDamage(new DamageInfo(damage, t.Transform.position, Vector3.down, DamageSource.Gun, "포격 지원"));
                    DamageDealt += damage;
                }
            }
            bool hitShip = false;
            foreach (var t in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Surface))
                if (t != null && t.Transform != null && Flat(t.Transform.position - at).sqrMagnitude <= ShellBlast * ShellBlast) { hitShip = true; break; }
            Explosions.Spawn(at, hitShip ? 0.75f : 0.6f, hitShip ? Explosions.Kind.Impact : Explosions.Kind.Water);
            AudioManager.Play(Game.Data.SfxId.ShellImpact, at, 0.9f, Random.Range(0.85f, 1.05f));
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        // --- 지원 판
        public string TimerLabel => "포격 지원";
        public string TimerStatus => Instance == null || !Instance.IsOperational ? "<color=#e0956a>파손</color>"
            : _firing ? "낙탄 중" : _timer <= 1f ? "표적 찾는 중" : $"다음 요청 {_timer:0}초";
        public bool TimerActive => _firing;
    }
}

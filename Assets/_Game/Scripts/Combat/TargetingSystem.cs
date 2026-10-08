using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Ship;
using Game.Enemies;
using Game.Modules;
using Game.Modules.Runtime;

namespace Game.Combat
{
    /// <summary>
    /// 함선의 탐지 담당. 일정 간격으로만 스캔해 프레임당 검색 횟수를 제한한다.
    /// 무기 모듈은 직접 탐색하지 않고 이 결과만 조회한다(CIWS·폭뢰는 자체 센서 범위를 따로 본다).
    ///
    /// 탐지 계층
    ///   - 레이더 탐지거리(함교 32, 전용 레이더 52): 수상함·항공기·미사일. VLS(48)는 전용 레이더가 있어야 끝까지 쓴다.
    ///   - 육안·근접 센서(visualRange 24): 레이더가 없거나 부서져도 이 안의 표적은 항상 잡힌다 — 기관포(22)가 스스로 교전.
    ///   - 소나: 지속 수색과 쿨타임마다 자동으로 발동하는 능동 핑으로 수중 접촉을 확정한다.
    /// 동시 추적 수(MaxTrackedTargets)는 가까운 순으로 채우고, 근접 센서 안의 표적은 수 제한에 넣지 않는다.
    /// </summary>
    public class TargetingSystem : MonoBehaviour
    {
        [SerializeField] private ShipSystems systems;
        [SerializeField, Min(0.05f)] private float scanInterval = 0.2f;

        [Tooltip("레이더 없이도 항상 탐지되는 근접 범위. 기관포 최대 사거리(22)보다 약간 길게.")]
        [SerializeField] private float visualRange = 24f;
        [SerializeField] private float sonarAcquireSeconds = 2f;
        [SerializeField] private float activePingCooldown = 12f;
        private readonly Dictionary<SubmarineBase, (int spawn, float elapsed)> _sonarProgress = new();
        private float _nextPingTime;
        private ShipGrid _grid;

        private readonly List<ITargetable> _detectedSurface = new(64);
        private readonly List<ITargetable> _detectedSubmarine = new(16);
        private readonly List<Vector3> _suspectedSonar = new(16);
        private readonly List<ITargetable> _incomingMissiles = new(32);
        private readonly List<ITargetable> _detectedAircraft = new(16);

        // 직전 스캔에 있던 잠수함. 새로 잡힌 것만 소나 접촉으로 알리기 위함.
        private readonly HashSet<ITargetable> _knownSubmarines = new();
        private readonly HashSet<ITargetable> _scratchSubmarines = new();

        private readonly List<(ITargetable t, float sqr)> _sortScratch = new(64);

        private float _timer;

        public IReadOnlyList<ITargetable> DetectedSurface => _detectedSurface;
        public IReadOnlyList<ITargetable> DetectedSubmarines => _detectedSubmarine;
        /// <summary>확정 전 소나 접촉. 방위만 30도 단위로 표시하며 실제 거리/좌표를 공개하지 않는다.</summary>
        public IReadOnlyList<Vector3> SuspectedSonar => _suspectedSonar;
        public IReadOnlyList<ITargetable> IncomingMissiles => _incomingMissiles;
        public IReadOnlyList<ITargetable> DetectedAircraft => _detectedAircraft;

        /// <summary>현재 레이더 탐지거리(개발용 표시). 적 전자전 방해 중이면 줄어든다(육안 거리 아래로는 안 줄어듦).</summary>
        public float RadarRange => systems != null
            ? Mathf.Max((systems.DetectionRange + Mathf.Max(0f, ExternalDetectionRangeBonus)) * EnemyJamming.RadarMultiplier, visualRange)
            : visualRange;
        public float VisualRange => visualRange;
        public float PingRemaining => Mathf.Max(0f, _nextPingTime - Time.time);

        /// <summary>전투단 지원(AWACS 등)이 제공하는 임시 외부 센서 보너스.</summary>
        public float ExternalDetectionRangeBonus { get; set; }
        public int ExternalTrackedTargetsBonus { get; set; }

        /// <summary>소나 안의 접촉을 즉시 확정한다. 쿨타임마다 자동 호출된다.</summary>
        public bool TryActivePing()
        {
            if (Game.Core.GameManager.Instance == null || Game.Core.GameManager.Instance.State != Game.Core.GameState.Playing) return false;
            if (systems == null || systems.SonarRange <= 0f || Time.time < _nextPingTime) return false;
            _nextPingTime = Time.time + activePingCooldown *
                (CombatPolicies.Doctrine == NavalDoctrine.AntiSub ? 0.75f : 1f);
            Game.Core.AudioManager.Play(Game.Data.SfxId.SonarPing, transform.position, 0.7f, 1f);
            // 소나 형태별 반경·방향(선수 소나는 앞쪽만, 예인 소나는 빠르면 짧다)
            foreach (var t in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Submarine))
                if (t is SubmarineBase sub && sub.IsAlive && systems.SonarCovers(sub.transform.position))
                {
                    sub.ConfirmContact(5f);
                    _sonarProgress[sub] = (sub.SpawnRevision, sonarAcquireSeconds);
                }
            Scan();
            return true;
        }

        /// <summary>어뢰 발사 당시의 위치. 현재 잠수함 좌표를 유출하지 않는다.</summary>
        public bool TryGetLaunchCue(Vector3 from, float range, out Vector3 position)
        {
            position = default;
            float best = range * range;
            foreach (var t in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Submarine))
            {
                if (t is not SubmarineBase sub || !sub.IsAlive || !sub.HasLaunchSignature) continue;
                Vector3 delta = sub.LaunchPosition - from;
                delta.y = 0f;
                if (delta.sqrMagnitude >= best) continue;
                best = delta.sqrMagnitude;
                position = sub.LaunchPosition;
            }
            return best < range * range;
        }

        private void Update()
        {
            if (systems != null && systems.SonarRange > 0f && Time.time >= _nextPingTime && TryActivePing())
            {
                _timer = scanInterval;
                return;
            }
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = scanInterval;
            Scan();
        }

        /// <summary>탐지거리 안의 표적 목록을 갱신한다.</summary>
        private void Scan()
        {
            if (systems == null)
            {
                Debug.LogError("[TargetingSystem] ShipSystems 미할당.", this);
                return;
            }

            // 적 전자전 방해(스테이지 3 전자전 코르벳): 레이더만 줄고 육안 거리는 그대로
            float radarRange = Mathf.Max((systems.DetectionRange + Mathf.Max(0f, ExternalDetectionRangeBonus)) * EnemyJamming.RadarMultiplier, visualRange);
            float sonarRange = systems.SonarRange;
            int maxTracked = Mathf.Max(1, systems.MaxTrackedTargets + Mathf.Max(0, ExternalTrackedTargetsBonus));
            Vector3 self = transform.position;

            Collect(TargetKind.Surface, self, radarRange, maxTracked, false, _detectedSurface);
            // 미사일은 위협이라 수 제한 없이 모두 추적한다
            Collect(TargetKind.Missile, self, radarRange, int.MaxValue, false, _incomingMissiles);
            Collect(TargetKind.Aircraft, self, radarRange, maxTracked, false, _detectedAircraft);
            UpdateSonarContacts(self, sonarRange);
            // 소나 확정 접촉 또는 실제 부상한 잠수함만 목록에 넣는다.
            Collect(TargetKind.Submarine, self, sonarRange, maxTracked, true, _detectedSubmarine);
            AnnounceNewSubmarines();
        }

        /// <summary>
        /// 소나 반경 안의 잠수함을 일정 시간 수색하면 대잠 무기용 접촉을 만든다.
        /// </summary>
        private void UpdateSonarContacts(Vector3 self, float sonarRange)
        {
            _suspectedSonar.Clear();
            _grid ??= GetComponentInParent<ShipGrid>();
            bool aswGroup = false;
            if (_grid != null)
                foreach (var module in _grid.Modules)
                    if (module != null && module.IsOperational && module.Definition.Type == ModuleType.Sonar &&
                        (ModuleSynergy.Adjacent(_grid, module, ModuleType.HelicopterDeck) ||
                         ModuleSynergy.Adjacent(_grid, module, ModuleType.AswLauncher) ||
                         ModuleSynergy.Adjacent(_grid, module, ModuleType.TorpedoTube)))
                    { aswGroup = true; break; }
            float acquireSeconds = (aswGroup ? 1.2f : sonarAcquireSeconds) *
                (CombatPolicies.Doctrine == NavalDoctrine.AntiSub ? 0.7f : 1f);
            foreach (var t in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Submarine))
            {
                if (t is not SubmarineBase sub) continue;
                if (!sub.IsAlive) { _sonarProgress.Remove(sub); continue; }
                if (sonarRange <= 0f || systems == null || !systems.SonarCovers(sub.transform.position))
                {
                    _sonarProgress.Remove(sub);
                    continue;
                }
                _sonarProgress.TryGetValue(sub, out var previous);
                float progress = previous.spawn == sub.SpawnRevision ? previous.elapsed : 0f;
                progress = Mathf.Min(acquireSeconds, progress + scanInterval);
                _sonarProgress[sub] = (sub.SpawnRevision, progress);
                if (progress >= acquireSeconds) sub.ConfirmContact(aswGroup ? 6f : scanInterval * 3f);
                else if (!sub.IsRevealed && !sub.IsContactConfirmed)
                {
                    Vector3 direction = sub.transform.position - self;
                    float bearing = Mathf.Round(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg / 30f) * 30f * Mathf.Deg2Rad;
                    float displayRange = sonarRange * 0.7f;
                    _suspectedSonar.Add(self + new Vector3(Mathf.Sin(bearing), 0f, Mathf.Cos(bearing)) * displayRange);
                }
            }
        }

        private void AnnounceNewSubmarines()
        {
            _scratchSubmarines.Clear();
            foreach (var sub in _detectedSubmarine)
            {
                _scratchSubmarines.Add(sub);
                if (!_knownSubmarines.Contains(sub)) Game.Core.GameEvents.RaiseSubmarineContact(sub.Transform);
            }

            _knownSubmarines.Clear();
            _knownSubmarines.UnionWith(_scratchSubmarines);
        }

        /// <summary>가까운 순으로 채운다. 근접 센서(visualRange) 안은 수 제한과 무관하게 넣는다.</summary>
        private void Collect(TargetKind kind, Vector3 origin, float range, int max,
                             bool requireRevealed, List<ITargetable> output)
        {
            output.Clear();
            _sortScratch.Clear();

            // 드러난 대상까지 버리면 안 되므로 범위 0이어도 순회한다
            if (range <= 0f && !requireRevealed) return;

            float sqr = range * range;
            var all = TargetRegistry.HostileTo(CombatFaction.Player, kind);

            for (int i = 0; i < all.Count; i++)
            {
                var t = all[i];
                if (t == null || !t.IsAlive || t.Transform == null) continue;

                float actualDistance = (t.Transform.position - origin).sqrMagnitude;
                bool inRange = actualDistance <= sqr;

                // 소나 범위 밖이라도 이미 드러난 잠수함은 계속 추적한다
                if (requireRevealed && !t.IsRevealed && (t is not SubmarineBase sub || !sub.IsContactConfirmed)) continue;
                if (!requireRevealed && !inRange) continue;

                // 섬 뒤(레이더 그림자)의 함정·저공 항공기는 잡히지 않는다. 미사일은 CIWS가 자체 센서로 따로 본다.
                if ((kind == TargetKind.Surface || kind == TargetKind.Aircraft) &&
                    Game.World.Islands.BlocksLine(origin + Vector3.up * 2.5f, t.Transform.position + Vector3.up * (kind == TargetKind.Surface ? 1.2f : 0f)))
                    continue;

                Vector3 reported = t is SubmarineBase subContact && !t.IsRevealed
                    ? subContact.LastKnownPosition : t.Transform.position;
                float d = (reported - origin).sqrMagnitude;
                _sortScratch.Add((t, d));
            }

            _sortScratch.Sort((a, b) => a.sqr.CompareTo(b.sqr));

            float visualSqr = visualRange * visualRange;
            int tracked = 0;
            foreach (var (t, d) in _sortScratch)
            {
                bool close = !requireRevealed && d <= visualSqr;
                if (!close && tracked >= max) continue;
                if (!close) tracked++;
                output.Add(t);
            }
        }

        /// <summary>가장 가까운 수상 표적. 없으면 null.</summary>
        public ITargetable GetNearestSurface(Vector3 from, float maxRange)
            => GetNearest(_detectedSurface, from, maxRange);

        /// <summary>
        /// 일반 무기가 쏠 수 있는 표적. 수상함과 "드러난" 잠수함을 함께 본다.
        ///
        /// 숨은 잠수함은 여전히 조준할 수 없다. 소나나 헬기로 드러내야 비로소
        /// 기관포와 VLS가 잡을 수 있고, 그래서 탐지 수단이 의미를 갖는다.
        /// </summary>
        /// <param name="filter">추가 조건. 사격각이 제한된 무기가 뒤쪽 표적에 묶이지 않게 한다.</param>
        /// <param name="includeAir">항공기도 노리는가.</param>
        public ITargetable GetNearestAttackable(Vector3 from, float maxRange,
                                                System.Predicate<Vector3> filter = null, bool includeAir = false)
            => GetBest(from, maxRange, (includeAir ? TargetClass.Air : 0) | TargetClass.Surface | TargetClass.Submarine,
                       (t, sqr) => sqr, filter);

        /// <summary>CIWS·함대공용. 반경 안의 항공기 중 가장 가까운 것.</summary>
        public ITargetable GetNearestAircraft(Vector3 from, float maxRange, System.Predicate<Vector3> filter = null)
            => GetNearest(_detectedAircraft, from, maxRange, filter);

        /// <summary>
        /// 조건을 만족하는 표적 중 점수가 가장 낮은 것. 무기마다 역할에 맞는 점수(거리·우선순위·예상 충돌 시간)를 준다.
        /// </summary>
        /// <param name="score">(표적, 거리²) → 점수. 낮을수록 먼저 쏜다. float.MaxValue면 제외.</param>
        public ITargetable GetBest(Vector3 from, float maxRange, TargetClass classes,
                                   System.Func<ITargetable, float, float> score, System.Predicate<Vector3> filter = null)
        {
            ITargetable best = null;
            float bestScore = float.MaxValue;
            float maxSqr = maxRange * maxRange;

            void Consider(IReadOnlyList<ITargetable> list, bool requireRevealed)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var t = list[i];
                    if (t == null || !t.IsAlive || t.Transform == null) continue;
                    if (requireRevealed && !t.IsRevealed) continue;

                    float d = (t.Transform.position - from).sqrMagnitude;
                    if (d > maxSqr) continue;

                    float s = score(t, d);
                    if (s >= bestScore) continue;
                    if (filter != null && !filter(t.Transform.position)) continue;

                    best = t;
                    bestScore = s;
                }
            }

            if ((classes & TargetClass.Surface) != 0) Consider(_detectedSurface, false);
            if ((classes & TargetClass.Submarine) != 0) Consider(_detectedSubmarine, true);
            if ((classes & TargetClass.Air) != 0) Consider(_detectedAircraft, false);
            if ((classes & TargetClass.Missile) != 0) Consider(_incomingMissiles, false);
            return best;
        }

        /// <summary>
        /// 조건을 만족하는 표적 중 최대 체력이 가장 높은 것(같으면 가까운 쪽). VLS 같은 대물 무기용.
        /// 한 발이 강한 무기가 가장 가까운 고속정에 낭비되지 않게 한다.
        /// </summary>
        public ITargetable GetToughestAttackable(Vector3 from, float maxRange, System.Predicate<Vector3> filter = null)
            => GetBest(from, maxRange, TargetClass.Surface | TargetClass.Submarine, (t, sqr) =>
            {
                float hp = t is Game.Enemies.EnemyController e && e.Definition != null ? e.Definition.MaxHp : 0f;
                return -hp * 10000f + sqr;   // 체력 우선, 같으면 가까운 쪽
            }, filter);

        /// <summary>CIWS용. 반경 안으로 들어온 미사일 중 가장 가까운 것.</summary>
        public ITargetable GetNearestMissile(Vector3 from, float maxRange)
            => GetNearest(_incomingMissiles, from, maxRange);

        private static ITargetable GetNearest(IReadOnlyList<ITargetable> list, Vector3 from, float maxRange,
                                              System.Predicate<Vector3> filter = null)
        {
            ITargetable best = null;
            float bestSqr = maxRange * maxRange;

            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                if (t == null || !t.IsAlive || t.Transform == null) continue;

                float d = (t.Transform.position - from).sqrMagnitude;
                if (d >= bestSqr) continue;
                if (filter != null && !filter(t.Transform.position)) continue;
                bestSqr = d;
                best = t;
            }
            return best;
        }

        /// <summary>
        /// 날아오는 위협이 함선에 닿기까지 남은 예상 시간(초). 가까워지지 않으면 거리 기반의 큰 값.
        /// SAM·CIWS가 "먼저 막아야 할 위협"을 고르는 기준이다.
        /// </summary>
        public static float TimeToImpact(ITargetable threat, Vector3 shipPosition)
        {
            if (threat?.Transform == null) return float.MaxValue;

            Vector3 to = shipPosition - threat.Transform.position;
            float dist = to.magnitude;
            if (dist < 0.01f) return 0f;

            float closing = threat is IHasVelocity v ? Vector3.Dot(v.Velocity, to / dist) : 0f;
            return closing > 0.5f ? dist / closing : 100f + dist;
        }
    }

    /// <summary>표적 조회 범위.</summary>
    [System.Flags]
    public enum TargetClass
    {
        Surface = 1,
        Submarine = 2,
        Air = 4,
        Missile = 8,
    }
}

using System.Collections.Generic;
using UnityEngine;
using Game.World;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 섬 우회(2026-11-26, 섬 끼임 수정). 예전 이동은 앞쪽 탐지선 하나 · 부딪히면 미끄러짐 · 3.5초마다 선회 방향 뒤집기뿐이라
    /// 플레이어가 섬 뒤 · 두 섬 사이 · 오목한 해안에 있으면 선회 원(플레이어 중심)이 섬을 관통해 같은 바위를 번갈아 들이받고,
    /// 섬이 가려 쏘지도 못해 "끼어서 아무것도 안 하는 적"이 됐다.
    ///
    /// 이제 ① 2초 구간 두 번 연속 기대 거리의 30%도 못 움직였거나 ② 플레이어가 섬에 6초 넘게 가렸으면 <b>우회 길</b>을 고른다
    /// (원래 이동이 대개 잘 빠져나오므로 그때만 — 일찍 끼어들면 만 안에서 U턴부터 하다 막히거나 지나갈 틈을 두고 크게 돌았다):
    ///   - 목표 후보: 플레이어 둘레(교전 반경 × 1 · 0.7 · 1.4, 16방향)와 내 둘레(8 · 16 · 24m, 교전 반경 × 1.5 안) — 빈 물이고 플레이어가 보이는 곳.
    ///   - 경유점 후보: 근처 섬마다 둘레 8방향(섬 반경 + 배 폭 + 6m) — 만을 빠져나와 섬을 돌아가려면 둘이 필요할 때가 있어 최대 2개.
    ///   - 길이 열렸는지는 수면 바로 위(0.7m)에서 배 폭만큼 구를 굴려 본다(갑판 높이 사선은 낮은 모래톱을 못 본다).
    ///   - 비용 = 가는 길이 + 플레이어까지 거리 × 0.3(가까운 공격 자리를 좋아함). 좁은 수로면 물 안쪽만. 가다 막힌 지점은 6초 동안 빼고 고른다.
    /// 우회 중에는 다음 지점으로 곧장 키를 잡고, 플레이어가 다시 보이고(1초 유지) 교전 반경 근처이거나, 목표에 닿거나, 제한 시간(길 길이에 맞춤)이 지나면 끝낸다.
    /// 항공기는 섬 위로 날아가므로 쓰지 않는다.
    /// </summary>
    public abstract partial class EnemyController
    {
        private bool _detourOn;
        private readonly List<Vector3> _detourPath = new(3);   // 경유점(0~2) + 목표
        private float _detourUntil, _detourCheckAt, _hiddenSince = -1f, _seenSince = -1f;
        private Vector3 _progressFrom;
        private float _progressAt;
        private readonly List<(Vector3 at, float until)> _badSpots = new();
        private float _reverseUntil;
        private int _slowWindows;

        /// <summary>검증용: 우회를 끈다(예전 동작과 비교).</summary>
        [System.NonSerialized] public static bool DevDetourOff;

        /// <summary>검증용: 우회를 시작한 횟수 · 지금 우회 중인가 · 지금 향하는 지점.</summary>
        public int DevDetours { get; private set; }
        public bool DevDetouring => _detourOn;
        public Vector3 DevDetourTarget => _detourPath.Count > 0 ? _detourPath[0] : transform.position;

        private void ResetDetour()
        {
            _detourOn = false;
            _detourPath.Clear();
            _detourUntil = _detourCheckAt = 0f;
            _badSpots.Clear();
            _reverseUntil = 0f;
            _slowWindows = 0;
            _hiddenSince = _seenSince = -1f;
            _progressFrom = transform.position;
            _progressAt = Time.time;
            DevDetours = 0;
        }

        /// <summary>선회 방향(OrbitPlayer)에 섬 우회를 덧씌운다.</summary>
        private Vector3 ApplyDetour(Vector3 heading, float radius)
        {
            if (DevDetourOff || Kind == TargetKind.Aircraft || Islands.All.Count == 0 || Player == null) { _detourOn = false; return heading; }
            Vector3 pos = transform.position; pos.y = 0f;
            Vector3 target = Player.position; target.y = 0f;
            float now = Time.time;

            // 가림 · 보임 지속 시간
            bool hidden = Islands.BlocksShipLine(pos, target);
            if (hidden) { if (_hiddenSince < 0f) _hiddenSince = now; _seenSince = -1f; }
            else { if (_seenSince < 0f) _seenSince = now; _hiddenSince = -1f; }

            // 움직임 정체: 2초마다 기대 거리의 30%도 못 갔나
            bool stalled = false;
            if (now - _progressAt > 3f) { _progressFrom = pos; _progressAt = now; }   // 다른 이동을 하다 돌아왔다: 새로 잰다
            else if (now - _progressAt >= 2f)
            {
                // 우회 중에는 틈을 비집고 지나가느라 느릴 수 있어 15%만(30%면 다 빠져나가던 길을 버렸다)
                float expect = (definition != null ? definition.MoveSpeed * 2f : 10f) * (_detourOn ? 0.15f : 0.3f);
                Vector3 moved = pos - new Vector3(_progressFrom.x, 0f, _progressFrom.z);
                bool slow = moved.magnitude < expect && definition != null && definition.MoveSpeed > 0.5f;
                // 우회 밖에서는 두 구간(4초) 연속일 때만 — 섬에 잠깐 스친 것으로 큰 길을 돌았다
                _slowWindows = slow ? _slowWindows + 1 : 0;
                stalled = slow && (_detourOn || _slowWindows >= 2);
                _progressFrom = pos; _progressAt = now;
            }

            // 물러나기: 가다 막혔으면 1.8초 후진(같은 틈을 향한 키와 밀어내는 힘이 맞서 속력은 있는데 제자리에 서 버렸다)
            if (now < _reverseUntil) return heading;

            if (_detourOn)
            {
                float reach = Mathf.Max(6f, OwnHalfLength() + 3f);
                while (_detourPath.Count > 1 && Flat(_detourPath[0] - pos).magnitude < reach) _detourPath.RemoveAt(0);
                Vector3 aim = _detourPath.Count > 0 ? _detourPath[0] : target;
                Vector3 to = Flat(aim - pos);
                float distP = Vector3.Distance(pos, target);
                bool done = now >= _detourUntil
                    || (_detourPath.Count <= 1 && to.magnitude < reach)
                    || (_seenSince >= 0f && now - _seenSince >= 1f && distP <= radius * 1.6f);
                if (stalled && now < _detourUntil)
                {
                    // 가다 막혔다: 막힌 자리(선수 앞)를 6초 동안 피해 다시 고른다 — 목표를 빼면 같은 틈으로 다른 목표를 또 골랐다
                    done = true;
                    Vector3 tried = to.sqrMagnitude > 1e-4f ? to.normalized : Flat(transform.forward).normalized;
                    _badSpots.Add((pos + tried * Mathf.Max(OwnHalfLength() + 2f, BadRadius + 1f), now + 8f));   // 가려던 쪽 바로 앞(내 자리는 반경 밖)
                    _reverseUntil = now + 1.8f;
                    _detourOn = false;
                    _detourPath.Clear();
                    _detourCheckAt = now + 1.8f;
                    _progressFrom = pos; _progressAt = now + 1.8f;   // 물러나는 동안은 정체로 세지 않는다
                    return heading;
                }
                if (!done) return to.sqrMagnitude > 1e-4f ? to.normalized : heading;
                _detourOn = false;
                _detourPath.Clear();
                _detourCheckAt = now + 0.5f;
                if (!stalled) return heading;
            }

            // 원래 이동(섬 따라 미끄러짐 · 선회 뒤집기)이 대개 잘 빠져나온다(작은 배는 좁은 틈도 비집고 지나감) —
            // 실제로 멈췄거나 6초 넘게 가렸을 때만 끼어든다(3초에 끼어들면 6초면 빠져나갈 배를 큰 길로 돌렸다)
            bool want = (hidden && _hiddenSince >= 0f && now - _hiddenSince >= 6f) || stalled;
            if (!want || now < _detourCheckAt) return heading;
            _detourCheckAt = now + 0.5f;
            if (!PickDetour(pos, target, radius)) return heading;
            _detourOn = true;
            // 제한 시간: 고른 길 길이를 최고 속력으로 가는 시간의 1.5배 + 4초(8~30초) — 막다른 만을 돌아 나오는 긴 길도 끝까지 간다
            float pathLen = 0f; Vector3 prev = pos;
            foreach (var w in _detourPath) { pathLen += Vector3.Distance(prev, w); prev = w; }
            float vmax = definition != null ? Mathf.Max(1f, definition.MoveSpeed * SpeedMultiplier) : 6f;
            _detourUntil = now + Mathf.Clamp(pathLen / vmax * 1.5f + 4f, 8f, 30f);
            DevDetours++;
            Vector3 first = Flat(_detourPath[0] - pos);
            return first.sqrMagnitude > 1e-4f ? first.normalized : heading;
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        private float DetourHalfWidth()
        {
            float half = _ownHull != null ? _ownHull.size.x * 0.5f * transform.lossyScale.x : 1.5f;
            return Mathf.Max(1f, half) + 1f;
        }

        /// <summary>
        /// 그 길로 가면 섬에 안 닿는가: 이 배의 판정 상자(섬에 박힘을 푸는 <see cref="Islands.ResolveOverlap"/>와 같은 상자, 폭 +0.5m)를
        /// 진행 방향으로 돌려 밀어 본다. 사선(갑판 높이 1.5m)이나 수면 위 구로 보면 물속으로 뻗은 섬 경사를 못 봐
        /// 폭 2m 틈을 "열림"으로 보고 들어가 박혔다(첫 검사 오목한 해안).
        /// </summary>
        private bool PathClear(Vector3 a, Vector3 b, float half)
        {
            if (_badSpots.Count > 0 && CrossesBad(a, b)) return false;
            Vector3 d = Flat(b - a);
            float len = d.magnitude;
            if (len < 0.01f) return true;
            Vector3 dir = d / len;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            Vector3 ext; float cy;
            if (_ownHull != null)
            {
                Vector3 sc = _ownHull.transform.lossyScale;
                Vector3 size = Vector3.Scale(_ownHull.size, sc);
                ext = new Vector3(size.x * 0.5f + 1f, size.y * 0.5f, 0.5f);   // 양옆 1m 여유 — 배 폭과 비슷한 틈은 고르지 않는다(0.5m면 비스듬히 들어가 끼었다). 아슬아슬한 틈은 원래 이동이 먼저 시도한다
                cy = transform.position.y + _ownHull.center.y * sc.y;
            }
            else { ext = new Vector3(half, 1f, 0.5f); cy = 0.5f; }
            Vector3 o = new(a.x, cy, a.z);
            // 지금 해안에 닿아 있으면(시작부터 겹친 섬은 BoxCast가 못 본다) 상자 하나 길이만큼 나아간 데서부터 본다
            if (Physics.CheckBox(o, ext, rot, Islands.Mask, QueryTriggerInteraction.Ignore))
            {
                float skip = Mathf.Min(len, ext.x * 2f + 1f);
                o += dir * skip; len -= skip;
                if (len <= 0.01f) return true;
                if (Physics.CheckBox(o, ext, rot, Islands.Mask, QueryTriggerInteraction.Ignore)) return false;
            }
            return !Physics.BoxCast(o, ext, dir, rot, len, Islands.Mask, QueryTriggerInteraction.Ignore);
        }

        private const float BadRadius = 6f;

        /// <summary>선분 a–b가 막혔던 자리 가까이(BadRadius)를 지나는가.</summary>
        private bool CrossesBad(Vector3 a, Vector3 b)
        {
            Vector3 ab = Flat(b - a);
            float l2 = ab.sqrMagnitude;
            foreach (var (at, until) in _badSpots)
            {
                if (Time.time >= until) continue;
                Vector3 ap = Flat(at - a);
                float t = l2 > 1e-4f ? Mathf.Clamp01(Vector3.Dot(ap, ab) / l2) : 0f;
                if ((ap - ab * t).sqrMagnitude < BadRadius * BadRadius) return true;
            }
            return false;
        }

        private bool NearBad(Vector3 p)
        {
            for (int i = _badSpots.Count - 1; i >= 0; i--)
            {
                if (Time.time >= _badSpots[i].until) { _badSpots.RemoveAt(i); continue; }
                if (Flat(p - _badSpots[i].at).sqrMagnitude < BadRadius * BadRadius) return true;
            }
            return false;
        }

        private bool SpotOk(Vector3 p, float half)
        {
            if (!Islands.IsClear(p, half + 1f)) return false;
            if (SeaRegion.IsStrait && SeaRegion.WaterRoom(p) < 9f) return false;
            return !NearBad(p);
        }

        private static readonly List<Vector3> s_vias = new(48);
        private static readonly List<Vector3> s_goals = new(48);
        private static float[] s_viaCost = new float[48];
        private static int[] s_viaPrev = new int[48];

        /// <summary>목표 · 경유점 후보를 만들고 경유점 최대 2개짜리 가장 싼 길을 고른다(<see cref="_detourPath"/>).</summary>
        private bool PickDetour(Vector3 pos, Vector3 target, float radius)
        {
            float half = DetourHalfWidth();
            s_vias.Clear(); s_goals.Clear();

            Vector3 mid = (pos + target) * 0.5f;
            float span = Vector3.Distance(pos, target) * 0.5f + 60f;
            foreach (var isl in Islands.All)
            {
                if (isl == null) continue;
                Vector3 c = Flat(isl.Center);
                if (Vector3.Distance(c, mid) > span + isl.Radius) continue;
                float rr = isl.Radius * Mathf.Max(1f, isl.Aspect) + half + 6f;
                for (int k = 0; k < 8 && s_vias.Count < 48; k++)
                {
                    float a = (k + 0.5f) * Mathf.PI * 2f / 8f;
                    var v = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rr;
                    if (SpotOk(v, half)) s_vias.Add(v);
                }
            }
            foreach (float ring in new[] { 1f, 0.7f, 1.4f })
            {
                float r = Mathf.Max(8f, radius * ring);
                for (int i = 0; i < 16; i++)
                {
                    float a = i * Mathf.PI * 2f / 16f;
                    Vector3 goal = target + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    if (SpotOk(goal, half) && !Islands.BlocksShipLine(goal, target)) s_goals.Add(goal);
                }
            }
            // 가까운 사격 자리: 내 둘레(8 · 16 · 24m, 12방향)에서 틈 사이로 플레이어가 보이고 교전 반경 × 1.5 안인 곳 —
            // 플레이어 둘레만 보면 만 안에서 옆으로 조금 비키면 되는데도 섬 바깥을 크게 돌았다
            foreach (float r in new[] { 8f, 16f, 24f })
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI * 2f / 12f;
                    Vector3 goal = pos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    if (Vector3.Distance(goal, target) > radius * 1.5f) continue;
                    if (SpotOk(goal, half) && !Islands.BlocksShipLine(goal, target)) s_goals.Add(goal);
                }
            if (s_goals.Count == 0) return false;

            // 경유점까지 비용(한 번 또는 두 번 거쳐)
            int nv = s_vias.Count;
            if (s_viaCost.Length < nv) { s_viaCost = new float[nv]; s_viaPrev = new int[nv]; }
            for (int i = 0; i < nv; i++)
            {
                s_viaCost[i] = PathClear(pos, s_vias[i], half) ? Vector3.Distance(pos, s_vias[i]) : float.MaxValue;
                s_viaPrev[i] = -1;
            }
            var direct = new bool[nv];
            for (int i = 0; i < nv; i++) direct[i] = s_viaCost[i] < float.MaxValue;
            for (int j = 0; j < nv; j++)
            {
                if (direct[j]) continue;
                for (int i = 0; i < nv; i++)
                {
                    if (!direct[i]) continue;
                    float c = s_viaCost[i] + Vector3.Distance(s_vias[i], s_vias[j]);
                    if (c >= s_viaCost[j]) continue;
                    if (!PathClear(s_vias[i], s_vias[j], half)) continue;
                    s_viaCost[j] = c; s_viaPrev[j] = i;
                }
            }

            // 첫 구간이 지금 선수 방향에서 크게 꺾이면 비용을 더한다(180°에 25m) — 좁은 곳에서 U턴은 오래 걸린다
            Vector3 fwdNow = Flat(transform.forward).normalized;
            float TurnCost(Vector3 first)
            {
                Vector3 d = Flat(first - pos);
                return d.sqrMagnitude < 1e-4f ? 0f : Vector3.Angle(fwdNow, d) / 180f * 25f;
            }
            for (int i = 0; i < nv; i++)
                if (s_viaCost[i] < float.MaxValue)
                    s_viaCost[i] += s_viaPrev[i] >= 0 ? TurnCost(s_vias[s_viaPrev[i]]) : TurnCost(s_vias[i]);
            float best = float.MaxValue;
            int bestGoal = -1, bestVia = -2;   // -1 = 곧장
            for (int g = 0; g < s_goals.Count; g++)
            {
                Vector3 goal = s_goals[g];
                float tail = Vector3.Distance(goal, target) * 0.3f;
                float straight = Vector3.Distance(pos, goal) + tail + TurnCost(goal);
                if (straight < best && PathClear(pos, goal, half)) { best = straight; bestGoal = g; bestVia = -1; continue; }
                for (int i = 0; i < nv; i++)
                {
                    if (s_viaCost[i] == float.MaxValue) continue;
                    float c = s_viaCost[i] + Vector3.Distance(s_vias[i], goal) + tail;
                    if (c >= best) continue;
                    if (!PathClear(s_vias[i], goal, half)) continue;
                    best = c; bestGoal = g; bestVia = i;
                }
            }
            if (bestGoal < 0) return false;
            _detourPath.Clear();
            if (bestVia >= 0)
            {
                if (s_viaPrev[bestVia] >= 0) _detourPath.Add(s_vias[s_viaPrev[bestVia]]);
                _detourPath.Add(s_vias[bestVia]);
            }
            _detourPath.Add(s_goals[bestGoal]);
            return true;
        }
    }
}

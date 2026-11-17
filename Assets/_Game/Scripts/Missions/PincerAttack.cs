using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Data;
using Game.Enemies;
using Game.View;

namespace Game.Missions
{
    /// <summary>
    /// 전장 이벤트 "자폭 보트 3방향 협공"(2026-11-17). 제자리에 머무는 기함을 벌하고, 피해 내는 손맛을 준다.
    ///   1. 예고(WarnTime): 기함 둘레 세 방향(120° 간격)에 붉은 돌진선과 가운데 표적 원이 뜬다. 선은 기함을 따라 움직인다.
    ///   2. 돌진: 예고가 끝나는 순간 기함의 예상 위치(지금 속력 × LeadRatio × 도착 시간 앞)를 굳히고, 그 둘레 Distance 원 위
    ///      세 곳에서 자폭 보트(ene_suicide_boat)가 동시에 나와 그 점으로 곧게 달린다(꺾지 않음). 거리가 같아 같은 순간에 닿는다.
    ///   3. 기함이 그 자리에 있으면 부딪혀 터진다(평소 자폭 피해). 피했으면 보트끼리 한가운데서 부딪혀 함께 터지고(격침 · 경험치),
    ///      그 폭발이 둘레 BlastRadius 안의 다른 적에게 BlastDamage — 적을 가운데로 끌어들이는 플레이도 된다.
    ///   4. 하나도 맞지 않고 둘 이상 부딪히면 "협공 회피" 보너스 경험치(다음 레벨까지의 15%).
    ///   보트 하나만 남아 한가운데에 닿으면 평소 자폭 보트처럼 기함을 쫓는다. 섬에 가로막힌 방향은 원을 돌려 비켜 고른다.
    /// </summary>
    public sealed class PincerAttack : MonoBehaviour
    {
        public const float WarnTime = 2.6f;
        public const float Distance = 62f;
        public const float LeadRatio = 0.6f;
        public const float MeetRadius = 3.5f;
        public const float BlastRadius = 14f, BlastDamage = 40f;
        public const float BonusXpRatio = 0.15f;
        private const int Boats = 3;

        public enum Phase { Warning, Charging, Done }
        public enum Result { None, Collided, Hit, Partial, Cancelled }

        private Game.Ship.ShipController _player;
        private Game.Progression.ExperienceSystem _xp;
        private EnemyDefinition _def;
        private float _age, _baseAngle;
        private Vector3 _aim;
        private readonly Vector3[] _dirs = new Vector3[Boats];   // 기함 → 보트가 나오는 쪽(돌진 방향의 반대)
        private readonly List<SuicideBoat> _boats = new();
        private LineRenderer[] _lanes;
        private LineRenderer _aimRing;

        public Phase State { get; private set; } = Phase.Warning;
        public Result Outcome { get; private set; } = Result.None;
        /// <summary>기함에 부딪혀 터진 보트 수 · 서로 부딪혀 터진 보트 수(검증 · 보상).</summary>
        public int Hits { get; private set; }
        public int Collided { get; private set; }
        public int Launched { get; private set; }
        public int BonusXp { get; private set; }
        public Vector3 AimPoint => _aim;
        public IReadOnlyList<SuicideBoat> BoatsList => _boats;

        public static PincerAttack Begin(Game.Ship.ShipController player, Game.Progression.ExperienceSystem xp, EnemyDefinition def)
        {
            if (player == null || def == null || EnemySpawner.Instance == null) return null;
            var go = new GameObject("Pincer attack");
            var p = go.AddComponent<PincerAttack>();
            p._player = player;
            p._xp = xp;
            p._def = def;
            p._baseAngle = Random.Range(0f, 360f);
            p.SetDirections(p._baseAngle);
            p._lanes = new LineRenderer[Boats];
            for (int i = 0; i < Boats; i++) p._lanes[i] = MissionMarks.Line(go.transform, $"Pincer lane {i}", new Color(1f, 0.25f, 0.2f, 0.85f), 1.3f, false);
            p._aimRing = MissionMarks.Line(go.transform, "Pincer aim ring", new Color(1f, 0.3f, 0.2f, 0.9f), 0.9f, true);
            UiSound.Play(UiSound.Cue.Fail);
            GameEvents.RaiseBanner("자폭 보트 협공 — 세 방향에서 돌진! 붉은 원에서 벗어나세요(피하면 보트끼리 부딪혀 터집니다)");
            p.Refresh();
            return p;
        }

        private void SetDirections(float baseAngle)
        {
            for (int i = 0; i < Boats; i++)
                _dirs[i] = Quaternion.Euler(0f, baseAngle + i * 360f / Boats, 0f) * Vector3.forward;
        }

        /// <summary>보트가 지금 기함 속력으로 도착하기까지 걸리는 시간(가속 포함 대략).</summary>
        private float ArrivalTime => Distance / Mathf.Max(1f, _def.MoveSpeed * 0.88f);

        private Vector3 PredictAim()
        {
            var t = _player.transform;
            Vector3 v = MissionMarks.Flat(t.forward) * _player.CurrentSpeed;
            return MissionMarks.Flat(t.position) + v * (ArrivalTime * LeadRatio);
        }

        private bool LaneClear(Vector3 aim, Vector3 dir)
        {
            Vector3 from = aim + dir * Distance;
            return MissionMarks.OpenWater(from, 6f) && !Game.World.Islands.BlocksShipLine(from, aim);
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.IsRunOver || _player == null) { Cancel(); return; }
            if (gm.State != GameState.Playing) return;
            _age += Time.deltaTime;

            if (State == Phase.Warning)
            {
                Refresh();
                if (_age >= WarnTime) Launch();
                return;
            }
            if (State == Phase.Charging) TickCharge();
        }

        private void Refresh()
        {
            _aim = PredictAim();
            float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(_age * 9f));
            for (int i = 0; i < Boats; i++)
            {
                MissionMarks.Segment(_lanes[i], _aim + _dirs[i] * Distance, _aim + _dirs[i] * 4f);
                MissionMarks.Tint(_lanes[i], new Color(1f, 0.25f, 0.2f, 0.85f * pulse));
            }
            MissionMarks.Ring(_aimRing, _aim, 6f + 3f * (1f - Mathf.Clamp01(_age / WarnTime)));
        }

        private void Launch()
        {
            _aim = PredictAim();
            // 섬 · 기슭에 막힌 방향이 있으면 원 전체를 돌려 본다(15°씩). 끝까지 막힌 방향은 빼고 낸다.
            bool[] clear = new bool[Boats];
            int best = -1, bestCount = -1;
            for (int k = 0; k < 24; k++)
            {
                SetDirections(_baseAngle + k * 15f);
                int n = 0;
                for (int i = 0; i < Boats; i++) if (LaneClear(_aim, _dirs[i])) n++;
                if (n > bestCount) { bestCount = n; best = k; }
                if (n == Boats) break;
            }
            SetDirections(_baseAngle + best * 15f);
            for (int i = 0; i < Boats; i++) clear[i] = LaneClear(_aim, _dirs[i]);

            foreach (var lane in _lanes) if (lane != null) lane.positionCount = 0;
            State = Phase.Charging;
            if (bestCount < 2) { Finish(Result.Cancelled); return; }   // 사방이 섬 — 협공이 성립하지 않는다

            for (int i = 0; i < Boats; i++)
            {
                if (!clear[i]) continue;
                Vector3 from = _aim + _dirs[i] * Distance;
                var rot = Quaternion.LookRotation(-_dirs[i], Vector3.up);
                var e = EnemySpawner.Instance.SpawnAt(_def, from, rot) as SuicideBoat;
                if (e == null) continue;
                e.BeginCharge(this, -_dirs[i]);
                _boats.Add(e);
                Launched++;
                Explosions.Spawn(from, 0.7f, Explosions.Kind.Water, false);   // 물보라를 일으키며 튀어나온다
            }
            AudioManager.Play(SfxId.DroneBuzz, _player.transform.position, 0.9f, 0.75f);
            if (Launched < 2) { foreach (var b in _boats) if (b != null) b.EndCharge(); Finish(Result.Cancelled); }
        }

        private void TickCharge()
        {
            MissionMarks.Ring(_aimRing, _aim, 4f + 1.5f * Mathf.Abs(Mathf.Sin(_age * 10f)));
            int alive = 0;
            bool met = false;
            for (int i = _boats.Count - 1; i >= 0; i--)
            {
                var b = _boats[i];
                if (b == null || !b.IsAlive || !b.Charging) { _boats.RemoveAt(i); continue; }
                alive++;
                // 한가운데에 닿았거나 지나쳤다(돌진 방향으로 잰 남은 거리)
                Vector3 to = MissionMarks.Flat(_aim - b.transform.position);
                if (Vector3.Dot(to, b.transform.forward) <= MeetRadius) met = true;
            }
            if (alive == 0) { Finish(Hits > 0 ? (Collided > 0 ? Result.Partial : Result.Hit) : Result.Partial); return; }
            if (!met) return;
            if (alive >= 2) Collide();
            else
            {
                foreach (var b in _boats) if (b != null) b.EndCharge();   // 혼자 남은 보트는 평소처럼 쫓는다
                Finish(Hits > 0 ? Result.Hit : Result.Partial);
            }
        }

        /// <summary>보트가 기함에 부딪혀 터졌다(SuicideBoat.Detonate가 알린다).</summary>
        internal void NoteHit(SuicideBoat boat)
        {
            Hits++;
            _boats.Remove(boat);
        }

        private void Collide()
        {
            Vector3 center = Vector3.zero;
            var snapshot = _boats.ToArray();
            foreach (var b in snapshot) center += b.transform.position;
            center /= snapshot.Length;
            foreach (var b in snapshot)
            {
                if (b == null || !b.IsAlive) continue;
                b.CollideInPincer();
                Collided++;
            }
            _boats.Clear();
            Explosions.Spawn(center + Vector3.up * 0.8f, 2.6f, Explosions.Kind.Charge);
            Explosions.FlashLight(center + Vector3.up * 2f, 3f, new Color(1f, 0.6f, 0.3f));
            AudioManager.Play(SfxId.WreckBlast, center, 1f, 0.9f);

            // 한가운데 폭발이 둘레의 다른 적까지 휩쓴다(기함 · 아군은 맞지 않는다)
            int swept = 0;
            var list = TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Surface);
            var victims = new List<IDamageable>();
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                if (t == null || !t.IsAlive || t.Transform == null || t is not IDamageable d) continue;
                if (MissionMarks.Flat(t.Transform.position - center).sqrMagnitude > BlastRadius * BlastRadius) continue;
                victims.Add(d);
            }
            foreach (var d in victims)
            {
                if (!d.IsAlive) continue;
                d.TakeDamage(new DamageInfo(BlastDamage, center, Vector3.down, DamageSource.Missile, "협공 유폭"));
                swept++;
            }

            if (Hits == 0)
            {
                BonusXp = _xp != null ? Mathf.Max(1, Mathf.CeilToInt(_xp.XpToNext * BonusXpRatio)) : 0;
                if (_xp != null) _xp.AddXp(BonusXp);
                UiSound.Play(UiSound.Cue.Confirm);
                GameEvents.RaiseBanner($"협공 회피 — 자폭 보트 {Collided}척이 서로 부딪혀 폭발{(swept > 0 ? $" · 휩쓸린 적 {swept}" : "")} · 경험치 +{BonusXp}");
                Finish(Result.Collided);
            }
            else Finish(Result.Partial);
        }

        public void Cancel()
        {
            if (State == Phase.Done) return;
            foreach (var b in _boats) if (b != null && b.IsAlive) b.EndCharge();
            Finish(Result.Cancelled);
        }

        private void Finish(Result result)
        {
            if (State == Phase.Done) return;
            State = Phase.Done;
            Outcome = result;
            Destroy(gameObject, 0.6f);
            if (_aimRing != null) _aimRing.positionCount = 0;
        }
    }
}

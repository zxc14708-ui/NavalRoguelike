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
    /// 전장 이벤트 "반잠수정 포위"(2026-11-17 자폭 보트 3방향 협공 → 2026-11-21 사용자: 반잠수정 기습 · 시안 A1).
    ///   1. 경고(WarnTime 2.6초): 기함을 따라다니는 붉은 점선 원이 돌며 조여들고(30 → 18m), 원 위 세 방향(120° 간격)에 안쪽을 가리키는
    ///      이중 꺾쇠가 공격 방향을 알린다. 끝나기 0.3초 전부터 흰색 · 빨강으로 번쩍이며 옅어지고, 끝나면 사라진다.
    ///   2. 부상: 경고가 끝나는 순간 기함의 지금 자리 둘레 Distance(34m) 세 곳에서 잠겨 있던 반잠수 자폭정(ene_semisub_suicide)이
    ///      거품을 뿜으며 떠올라(RiseTime 0.7초) 물보라와 함께 수면에 오른다 — 드러난 채(12초).
    ///   3. 공격: 기함을 쫓아 들이받는다(평소 자폭 돌진, 기습 중에는 35% 빨라 도망만으로는 못 떨친다) — 쏘아 막거나 섬으로 가로막는다.
    ///   4. 하나도 맞지 않고 모두 격침하면 "포위 격퇴" 보너스 경험치(다음 레벨까지의 10%).
    ///   섬에 가로막힌 방향은 원을 15°씩 돌려 비켜 고른다. 두 곳도 안 나오면 취소. 반잠수정 정의가 지금 판에 없으면 열리지 않는다.
    ///   강화판(2026-11-21): 3스테이지부터 4척 · 4스테이지부터 5척(방향 간격 360/척수), 밤(어둠 0.5 이상)에는 경고가 2.6 → 2.0초.
    /// </summary>
    public sealed class PincerAttack : MonoBehaviour
    {
        public const float WarnTime = 2.6f, NightWarnTime = 2.0f, FadeTime = 0.3f;
        public const float Distance = 34f;
        public const float RiseTime = 0.7f;
        public const float RingStart = 30f, RingEnd = 18f;
        public const float BonusXpRatio = 0.1f;
        public const int Boats = 3, MaxBoats = 5;
        private static readonly Color Warn = new(1f, 0.3f, 0.23f, 0.95f);

        public enum Phase { Warning, Attacking, Done }
        public enum Result { None, Repelled, Hit, Cancelled }

        private Game.Ship.ShipController _player;
        private Game.Progression.ExperienceSystem _xp;
        private EnemyDefinition _def;
        private float _age, _baseAngle;
        private readonly Vector3[] _dirs = new Vector3[MaxBoats];   // 기함 → 반잠수정이 떠오르는 쪽
        private int _count = Boats;
        private float _warn = WarnTime;
        private readonly List<SemiSubSuicideBoat> _boats = new();
        private readonly HashSet<SemiSubSuicideBoat> _counted = new();
        private LineRenderer _ring;
        private readonly List<LineRenderer> _chevrons = new();
        private static Material s_dash;

        public Phase State { get; private set; } = Phase.Warning;
        public Result Outcome { get; private set; } = Result.None;
        /// <summary>기함에 부딪혀 터진 수 · 격침된 수 · 떠오른 수(검증 · 보상).</summary>
        public int Hits { get; private set; }
        public int Killed { get; private set; }
        public int Launched { get; private set; }
        public int BonusXp { get; private set; }
        /// <summary>경고 표시가 보이는가(검증).</summary>
        public bool WarningVisible => _ring != null && _ring.positionCount > 0;
        public float RingRadius { get; private set; }
        public IReadOnlyList<SemiSubSuicideBoat> BoatsList => _boats;
        public IReadOnlyList<Vector3> Directions => _dirs;
        /// <summary>이번 포위의 반잠수정 수 · 경고 시간(강화판 — 스테이지 · 밤).</summary>
        public int Count => _count;
        public float WarnSeconds => _warn;

        /// <summary>스테이지에 따른 반잠수정 수: 1~2스테이지 3척 · 3스테이지 4척 · 4스테이지부터 5척.</summary>
        public static int CountFor(int stage) => stage >= 4 ? 5 : stage >= 3 ? 4 : Boats;

        public static PincerAttack Begin(Game.Ship.ShipController player, Game.Progression.ExperienceSystem xp, EnemyDefinition def, int stage = 1)
        {
            if (player == null || def == null || EnemySpawner.Instance == null) return null;
            if (def.Prefab == null || def.Prefab.GetComponent<SemiSubSuicideBoat>() == null) return null;
            var go = new GameObject("Pincer attack");
            var p = go.AddComponent<PincerAttack>();
            p._player = player;
            p._xp = xp;
            p._def = def;
            p._count = CountFor(stage);
            p._warn = Game.World.DayNight.Darkness01 >= 0.5f ? NightWarnTime : WarnTime;
            p._baseAngle = Random.Range(0f, 360f);
            p.PickDirections(MissionMarks.Flat(player.transform.position));
            p._ring = MissionMarks.Line(go.transform, "Pincer warning ring", Warn, 0.9f, true);
            p._ring.sharedMaterial = DashMaterial();
            p._ring.textureMode = LineTextureMode.Tile;
            p._ring.textureScale = new Vector2(0.18f, 1f);
            for (int i = 0; i < p._count * 2; i++)
            {
                var c = MissionMarks.Line(go.transform, $"Pincer chevron {i}", Warn, 1.1f, false);
                c.numCornerVertices = 2;
                p._chevrons.Add(c);
            }
            UiSound.Play(UiSound.Cue.Fail);
            GameEvents.RaiseBanner($"반잠수정 포위 — {p._count}방향 물속에서 떠오릅니다! 떠오르면 바로 쏘세요");
            p.Refresh();
            return p;
        }

        private static Material DashMaterial()
        {
            if (s_dash != null) return s_dash;
            var tex = new Texture2D(8, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, name = "Pincer dash" };
            for (int x = 0; x < 8; x++) for (int y = 0; y < 2; y++) tex.SetPixel(x, y, x < 5 ? Color.white : Color.clear);
            tex.Apply();
            s_dash = new Material(Shader.Find("Sprites/Default")) { name = "Pincer dash", mainTexture = tex };
            return s_dash;
        }

        private void SetDirections(float baseAngle)
        {
            for (int i = 0; i < _count; i++)
                _dirs[i] = Quaternion.Euler(0f, baseAngle + i * 360f / _count, 0f) * Vector3.forward;
        }

        private bool Clear(Vector3 center, Vector3 dir)
        {
            Vector3 from = center + dir * Distance;
            return MissionMarks.OpenWater(from, 5f) && !Game.World.Islands.BlocksShipLine(from, center);
        }

        /// <summary>섬 · 기슭에 막힌 방향이 있으면 원 전체를 15°씩 돌려 가장 많이 열리는 쪽으로. 열린 방향 수를 돌려준다.</summary>
        private int PickDirections(Vector3 center)
        {
            int best = 0, bestCount = -1;
            for (int k = 0; k < 24; k++)
            {
                SetDirections(_baseAngle + k * 15f);
                int n = 0;
                for (int i = 0; i < _count; i++) if (Clear(center, _dirs[i])) n++;
                if (n > bestCount) { bestCount = n; best = k; }
                if (n == _count) break;
            }
            _baseAngle += best * 15f;
            SetDirections(_baseAngle);
            return bestCount;
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.IsRunOver || _player == null) { Cancel(); return; }
            if (gm.State != GameState.Playing) return;
            _age += Time.deltaTime;

            if (State == Phase.Warning)
            {
                if (_age >= _warn) { HideWarning(); Launch(); return; }
                Refresh();
                return;
            }
            if (State == Phase.Attacking) TickAttack();
        }

        /// <summary>경고: 기함을 따라다니며 돌고 조여드는 점선 원 + 세 방향 안쪽 이중 꺾쇠. 끝나기 0.3초 전부터 번쩍이며 옅어진다.</summary>
        private void Refresh()
        {
            Vector3 c = MissionMarks.Flat(_player.transform.position);
            float e = Mathf.Clamp01(_age / _warn);
            RingRadius = Mathf.Lerp(RingStart, RingEnd, e * e * (3f - 2f * e));
            float left = _warn - _age;
            float fade = left < FadeTime ? Mathf.Clamp01(left / FadeTime) : 1f;
            bool flash = left < FadeTime && Mathf.Repeat(_age * 16f, 1f) < 0.5f;
            Color col = flash ? Color.white : Warn;
            col.a *= fade;
            MissionMarks.Ring(_ring, c, RingRadius, 72);
            MissionMarks.Tint(_ring, col);
            _ring.material.mainTextureOffset = new Vector2(-_age * 1.5f, 0f);   // 점선이 원을 따라 돈다
            float pulse = 1f + 0.12f * Mathf.Sin(_age * 9f);
            for (int i = 0; i < _count; i++)
            {
                Vector3 d = _dirs[i];
                Vector3 side = Vector3.Cross(Vector3.up, d);
                for (int k = 0; k < 2; k++)
                {
                    // 원 바깥에서 안쪽(기함)을 가리키는 꺾쇠 두 개
                    float r = RingRadius + 4f + k * 3.2f;
                    float s = 2.4f * pulse;
                    Vector3 tip = c + d * r, back = c + d * (r + 2.6f);
                    var line = _chevrons[i * 2 + k];
                    line.positionCount = 3;
                    line.SetPosition(0, Y(back + side * s));
                    line.SetPosition(1, Y(tip));
                    line.SetPosition(2, Y(back - side * s));
                    MissionMarks.Tint(line, col);
                }
            }
        }

        private static Vector3 Y(Vector3 p) => new(p.x, MissionMarks.WaterLine, p.z);

        private void HideWarning()
        {
            if (_ring != null) _ring.positionCount = 0;
            foreach (var c in _chevrons) if (c != null) c.positionCount = 0;
        }

        private void Launch()
        {
            State = Phase.Attacking;
            Vector3 center = MissionMarks.Flat(_player.transform.position);
            if (PickDirections(center) < 2) { Finish(Result.Cancelled); return; }   // 사방이 섬 — 포위가 성립하지 않는다
            for (int i = 0; i < _count; i++)
            {
                if (!Clear(center, _dirs[i])) continue;
                Vector3 from = center + _dirs[i] * Distance;
                var rot = Quaternion.LookRotation(-_dirs[i], Vector3.up);
                var e = EnemySpawner.Instance.SpawnAt(_def, from, rot) as SemiSubSuicideBoat;
                if (e == null) continue;
                e.BeginAmbush(RiseTime);
                _boats.Add(e);
                Launched++;
            }
            AudioManager.Play(SfxId.DroneBuzz, _player.transform.position, 0.9f, 0.6f);
            if (Launched < 2) { Finish(Result.Cancelled); return; }
        }

        private void TickAttack()
        {
            int alive = 0;
            foreach (var b in _boats)
            {
                if (b == null || _counted.Contains(b)) continue;
                if (b.IsAlive && b.gameObject.activeInHierarchy) { alive++; continue; }
                _counted.Add(b);
                if (b.Detonated) Hits++; else Killed++;
            }
            if (alive > 0) return;
            if (Hits == 0 && Killed > 0)
            {
                BonusXp = _xp != null ? Mathf.Max(1, Mathf.CeilToInt(_xp.XpToNext * BonusXpRatio)) : 0;
                if (_xp != null) _xp.AddXp(BonusXp);
                UiSound.Play(UiSound.Cue.Confirm);
                GameEvents.RaiseBanner($"포위 격퇴 — 반잠수정 {Killed}척 격침 · 경험치 +{BonusXp}");
                Finish(Result.Repelled);
            }
            else Finish(Result.Hit);
        }

        public void Cancel()
        {
            if (State == Phase.Done) return;
            HideWarning();
            Finish(Result.Cancelled);
        }

        private void Finish(Result result)
        {
            if (State == Phase.Done) return;
            State = Phase.Done;
            Outcome = result;
            HideWarning();
            Destroy(gameObject, 0.6f);
        }
    }
}

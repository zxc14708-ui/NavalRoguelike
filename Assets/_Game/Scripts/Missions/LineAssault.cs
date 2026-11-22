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
    /// 전장 이벤트 "자폭 보트 일렬 돌격"(2026-11-21 사용자 · 시안 B1). 자폭 보트 5척이 횡대(옆으로 나란히) 또는 종대(앞뒤로 한 줄)로
    /// 한꺼번에 쓸고 지나간다 — 띠 밖으로 비키면 피한다.
    ///   1. 경고(WarnTime 2.6초): 대형이 지나갈 넓은 붉은 띠(가장자리 점선)와 그 위로 흐르는 꺾쇠 화살이 기함을 따라다닌다
    ///      (띠는 기함의 예상 위치를 지난다). 끝나기 0.3초 전부터 번쩍이며 옅어지고, 끝나면 사라진다.
    ///      횡대는 기함 앞쪽(±30°)에서 마주 오고, 종대는 옆(±90° ± 20°)에서 기함의 항로를 가로지른다.
    ///   2. 돌격: 경고가 끝나는 순간 경로를 굳히고, 띠 시작(StartDist 70m 앞)에서 보트가 물보라와 함께 나와 띠를 따라 전속 직진한다(꺾지 않음).
    ///   3. 기함에 닿으면 평소처럼 터진다. 경로 끝(PassDist 60m 뒤)을 지나면 그대로 빠져나간다(사라짐 — 경험치 없음).
    ///   4. 하나도 맞지 않으면 "돌격 회피" 보너스 경험치(다음 레벨까지의 10%).
    ///   섬에 막힌 경로는 20°씩 돌려 비켜 고른다. 끝까지 막히면 열리지 않는다.
    ///   강화판(2026-11-21): 2스테이지부터 한 줄 6척, 3스테이지부터 횡대와 종대가 동시에 온다(두 띠가 기함 근처에서 엇갈림 — 짝 돌격은
    ///   배너 · 보너스를 따로 내지 않고 본 돌격이 둘을 합쳐 판정), 밤(어둠 0.5 이상)에는 경고가 2.6 → 2.0초.
    /// </summary>
    public sealed class LineAssault : MonoBehaviour, ISuicideChargeOwner
    {
        public const float WarnTime = 2.6f, NightWarnTime = 2.0f, FadeTime = 0.3f;
        public const int Boats = 5, MaxBoats = 6;
        public const float AbreastGap = 5f, ColumnGap = 7f, Margin = 4f;
        public const float StartDist = 70f, PassDist = 60f, LeadRatio = 0.5f;
        public const float BonusXpRatio = 0.1f;
        private const int Chevrons = 7;
        private static readonly Color Band = new(1f, 0.3f, 0.23f, 0.18f), Edge = new(1f, 0.3f, 0.23f, 0.9f), Arrow = new(1f, 0.72f, 0.66f, 0.95f);

        public enum Formation { Abreast, Column }
        public enum Phase { Warning, Charging, Done }
        public enum Result { None, Evaded, Hit, Cancelled }

        private Game.Ship.ShipController _player;
        private Game.Progression.ExperienceSystem _xp;
        private EnemyDefinition _def;
        private float _age;
        private Vector3 _dir, _aim;
        private readonly List<SuicideBoat> _boats = new();
        private LineRenderer _band, _edgeL, _edgeR;
        private readonly List<LineRenderer> _arrows = new();
        private static Material s_dash;

        private int _count = Boats;
        private float _warn = WarnTime;
        private LineAssault _partner, _leader;

        public Formation Shape { get; private set; }
        /// <summary>한 줄 보트 수 · 경고 시간 · 함께 오는 짝 돌격(3스테이지부터 — 없으면 null).</summary>
        public int Count => _count;
        public float WarnSeconds => _warn;
        public LineAssault Partner => _partner;
        public static int CountFor(int stage) => stage >= 2 ? MaxBoats : Boats;
        public Phase State { get; private set; } = Phase.Warning;
        public Result Outcome { get; private set; } = Result.None;
        public int Hits { get; private set; }
        public int Launched { get; private set; }
        public int Passed { get; private set; }
        public int BonusXp { get; private set; }
        public Vector3 Direction => _dir;
        public Vector3 AimPoint => _aim;
        public float Width => Shape == Formation.Abreast ? (_count - 1) * AbreastGap + Margin * 2f : Margin * 2.5f;
        public bool WarningVisible => _band != null && _band.positionCount > 0;
        public IReadOnlyList<SuicideBoat> BoatsList => _boats;

        public static LineAssault Begin(Game.Ship.ShipController player, Game.Progression.ExperienceSystem xp, EnemyDefinition def, Formation? shape = null, int stage = 1)
            => Begin(player, xp, def, shape, stage, null);

        private static LineAssault Begin(Game.Ship.ShipController player, Game.Progression.ExperienceSystem xp, EnemyDefinition def, Formation? shape, int stage, LineAssault leader)
        {
            if (player == null || def == null || EnemySpawner.Instance == null) return null;
            var go = new GameObject(leader != null ? "Line assault partner" : "Line assault");
            var a = go.AddComponent<LineAssault>();
            a._player = player; a._xp = xp; a._def = def; a._leader = leader;
            a._count = CountFor(stage);
            a._warn = Game.World.DayNight.Darkness01 >= 0.5f ? NightWarnTime : WarnTime;
            a.Shape = shape ?? (Random.value < 0.5f ? Formation.Abreast : Formation.Column);
            Vector3 fwd = MissionMarks.Flat(player.transform.forward);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            // 보트가 달리는 방향: 횡대는 기함 쪽으로 마주(앞쪽 ±30°에서), 종대는 옆에서 항로를 가로질러
            float yaw = a.Shape == Formation.Abreast ? 180f + Random.Range(-30f, 30f) : (Random.value < 0.5f ? 90f : -90f) + Random.Range(-20f, 20f);
            a._dir = Quaternion.Euler(0f, yaw, 0f) * fwd;
            a._aim = a.PredictAim();
            if (!a.FindClearPath()) { Destroy(go); return null; }
            string tag = leader != null ? "Line assault partner" : "Line assault";
            a._band = MissionMarks.Line(go.transform, tag + " band", Band, a.Width, false);
            a._band.numCapVertices = 0;
            a._edgeL = a.DashLine(go.transform, tag + " edge L");
            a._edgeR = a.DashLine(go.transform, tag + " edge R");
            for (int i = 0; i < Chevrons; i++) a._arrows.Add(MissionMarks.Line(go.transform, $"{tag} arrow {i}", Arrow, 1.2f, false));
            if (leader == null)
            {
                // 3스테이지부터: 다른 대형이 함께 온다(횡대면 종대, 종대면 횡대) — 띠가 기함 근처에서 엇갈린다
                if (stage >= 3 && shape == null)
                    a._partner = Begin(player, xp, def, a.Shape == Formation.Abreast ? Formation.Column : Formation.Abreast, stage, a);
                UiSound.Play(UiSound.Cue.Fail);
                GameEvents.RaiseBanner(a._partner != null ? "자폭 보트 횡대 · 종대 협공 — 두 붉은 띠를 모두 피하세요!"
                    : a.Shape == Formation.Abreast ? "자폭 보트 횡대 돌격 — 붉은 띠 밖으로 비키세요!" : "자폭 보트 종대 돌격 — 붉은 띠 밖으로 비키세요!");
            }
            a.Refresh();
            return a;
        }

        private LineRenderer DashLine(Transform parent, string name)
        {
            if (s_dash == null)
            {
                var tex = new Texture2D(8, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, name = "Line assault dash" };
                for (int x = 0; x < 8; x++) for (int y = 0; y < 2; y++) tex.SetPixel(x, y, x < 5 ? Color.white : Color.clear);
                tex.Apply();
                s_dash = new Material(Shader.Find("Sprites/Default")) { name = "Line assault dash", mainTexture = tex };
            }
            var l = MissionMarks.Line(parent, name, Edge, 0.6f, false);
            l.sharedMaterial = s_dash;
            l.textureMode = LineTextureMode.Tile;
            l.textureScale = new Vector2(0.25f, 1f);
            return l;
        }

        private Vector3 PredictAim()
        {
            var t = _player.transform;
            float travel = StartDist / Mathf.Max(1f, _def.MoveSpeed * 0.88f);
            return MissionMarks.Flat(t.position) + MissionMarks.Flat(t.forward) * _player.CurrentSpeed * travel * LeadRatio;
        }

        private IEnumerable<Vector3> StartPoints()
        {
            Vector3 side = Vector3.Cross(Vector3.up, _dir);
            Vector3 start = _aim - _dir * StartDist;
            for (int i = 0; i < _count; i++)
                yield return Shape == Formation.Abreast ? start + side * ((i - (_count - 1) * 0.5f) * AbreastGap)
                                                         : start - _dir * (i * ColumnGap);
        }

        private bool PathClear()
        {
            foreach (var p in StartPoints())
                if (!MissionMarks.OpenWater(p, 4f) || Game.World.Islands.BlocksShipLine(p, _aim)) return false;
            return !Game.World.Islands.BlocksShipLine(_aim, _aim + _dir * PassDist);
        }

        /// <summary>섬에 막히면 20°씩 돌려 본다.</summary>
        private bool FindClearPath()
        {
            Vector3 d0 = _dir;
            for (int k = 0; k < 18; k++)
            {
                _dir = Quaternion.Euler(0f, (k % 2 == 0 ? 1 : -1) * ((k + 1) / 2) * 20f, 0f) * d0;
                if (PathClear()) return true;
            }
            _dir = d0;
            return false;
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
                _aim = PredictAim();
                Refresh();
                return;
            }
            if (State == Phase.Charging) TickCharge();
        }

        /// <summary>경고 띠: 시작 → 끝까지 넓은 반투명 붉은 띠, 양쪽 가장자리 점선, 띠 위로 흐르는 꺾쇠 화살.</summary>
        private void Refresh()
        {
            float left = _warn - _age;
            float fade = left < FadeTime ? Mathf.Clamp01(left / FadeTime) : 1f;
            bool flash = left < FadeTime && Mathf.Repeat(_age * 16f, 1f) < 0.5f;
            Vector3 a = _aim - _dir * StartDist, b = _aim + _dir * PassDist;
            Vector3 side = Vector3.Cross(Vector3.up, _dir) * (Width * 0.5f);
            MissionMarks.Segment(_band, a, b);
            MissionMarks.Tint(_band, new Color(Band.r, Band.g, Band.b, Band.a * fade * (flash ? 2f : 1f)));
            MissionMarks.Segment(_edgeL, a + side, b + side);
            MissionMarks.Segment(_edgeR, a - side, b - side);
            var ec = flash ? Color.white : Edge; ec.a *= fade;
            MissionMarks.Tint(_edgeL, ec); MissionMarks.Tint(_edgeR, ec);
            float len = StartDist + PassDist;
            float s = Mathf.Min(Width * 0.32f, 6f);
            var ac = flash ? Color.white : Arrow; ac.a *= fade;
            Vector3 perp = Vector3.Cross(Vector3.up, _dir);
            for (int i = 0; i < _arrows.Count; i++)
            {
                float u = Mathf.Repeat(_age * 28f + i * len / _arrows.Count, len);
                Vector3 tip = a + _dir * u;
                var l = _arrows[i];
                l.positionCount = 3;
                l.SetPosition(0, Y(tip - _dir * s + perp * s));
                l.SetPosition(1, Y(tip));
                l.SetPosition(2, Y(tip - _dir * s - perp * s));
                MissionMarks.Tint(l, ac);
            }
        }

        private static Vector3 Y(Vector3 p) => new(p.x, MissionMarks.WaterLine + 0.02f, p.z);

        private void HideWarning()
        {
            if (_band != null) _band.positionCount = 0;
            if (_edgeL != null) _edgeL.positionCount = 0;
            if (_edgeR != null) _edgeR.positionCount = 0;
            foreach (var l in _arrows) if (l != null) l.positionCount = 0;
        }

        private void Launch()
        {
            State = Phase.Charging;
            _aim = PredictAim();
            if (!PathClear() && !FindClearPath()) { Finish(Result.Cancelled); return; }
            var rot = Quaternion.LookRotation(_dir, Vector3.up);
            foreach (var p in StartPoints())
            {
                var e = EnemySpawner.Instance.SpawnAt(_def, p, rot) as SuicideBoat;
                if (e == null) continue;
                e.BeginCharge(this, _dir);
                _boats.Add(e);
                Launched++;
                Explosions.Splash(p, 0.8f);   // 물보라를 일으키며 튀어나온다
            }
            AudioManager.Play(SfxId.DroneBuzz, _player.transform.position, 0.9f, 0.75f);
            if (Launched == 0) Finish(Result.Cancelled);
        }

        private void TickCharge()
        {
            Vector3 end = _aim + _dir * PassDist;
            int alive = 0;
            for (int i = _boats.Count - 1; i >= 0; i--)
            {
                var b = _boats[i];
                if (b == null || !b.IsAlive || !b.Charging) { _boats.RemoveAt(i); continue; }
                if (Vector3.Dot(MissionMarks.Flat(b.transform.position - end), _dir) > 0f) { b.LeaveCharge(); Passed++; _boats.RemoveAt(i); continue; }
                alive++;
            }
            if (alive > 0) return;
            if (_leader != null) { Finish(Hits == 0 ? Result.Evaded : Result.Hit); return; }   // 짝 돌격: 판정 · 보상은 본 돌격이 합쳐서
            if (_partner != null && _partner.State != Phase.Done) return;
            int hits = Hits + (_partner != null ? _partner.Hits : 0);
            int launched = Launched + (_partner != null ? _partner.Launched : 0);
            if (hits == 0)
            {
                BonusXp = _xp != null ? Mathf.Max(1, Mathf.CeilToInt(_xp.XpToNext * BonusXpRatio * (_partner != null ? 1.5f : 1f))) : 0;
                if (_xp != null) _xp.AddXp(BonusXp);
                UiSound.Play(UiSound.Cue.Confirm);
                GameEvents.RaiseBanner($"돌격 회피 — 자폭 보트 {launched}척에게 한 번도 맞지 않음 · 경험치 +{BonusXp}");
                Finish(Result.Evaded);
            }
            else Finish(Result.Hit);
        }

        public void NoteHit(SuicideBoat boat)
        {
            Hits++;
            _boats.Remove(boat);
        }

        public void Cancel()
        {
            if (State == Phase.Done) return;
            if (_partner != null) _partner.Cancel();
            foreach (var b in _boats) if (b != null && b.IsAlive) b.EndCharge();
            Finish(Result.Cancelled);
        }

        private void Finish(Result result)
        {
            if (State == Phase.Done) return;
            State = Phase.Done;
            Outcome = result;
            HideWarning();
            if (_leader != null) return;   // 짝 돌격은 본 돌격이 결과를 읽은 뒤 함께 정리한다
            Destroy(gameObject, 0.6f);
            if (_partner != null) { if (_partner.State != Phase.Done) _partner.Cancel(); Destroy(_partner.gameObject, 0.6f); }
        }
    }
}

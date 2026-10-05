#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Combat;
using Game.Core;
using Game.Data;
using Game.Enemies;
using Game.Modules;
using Game.Ship;

namespace Game.Dev
{
    /// <summary>
    /// 개발용 전투 도구(에디터·개발 빌드 전용, 일반 UI에 나오지 않는다). 전투 씬이 열리면 스스로 생긴다.
    ///
    ///   F3   오버레이 켜기/끄기: 함선 둘레 22·34·48 거리 링, 무기별 발사·명중·요격·화면 내 명중률,
    ///        적 미사일이 화면에 들어온 뒤 끝나기까지 걸린 시간, 실제 카메라로 잰 화면 가장자리까지의 수면 거리.
    ///   아래는 오버레이가 켜져 있을 때만:
    ///   F5   고속정 무리(8척)       F6  미사일정(3척)      F7  적 미사일 동시 접근(6발)
    ///   F8   잠수함(2척, 드러냄)    F9  시험 무장 장착      F10 통계 초기화     F11 드론 4 + 전투기 2
    /// </summary>
    public class CombatDevTools : MonoBehaviour
    {
        public static readonly float[] RingDistances = { 22f, 34f, 48f };
        private static readonly Color[] RingColors =
        {
            new(0.4f, 1f, 0.5f, 0.8f), new(1f, 0.85f, 0.3f, 0.8f), new(1f, 0.45f, 0.35f, 0.8f),
        };

        private bool _visible;
        private readonly List<LineRenderer> _rings = new();
        private GUIStyle _style;
        private readonly StringBuilder _sb = new();

        public static CombatDevTools Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance != null || Object.FindFirstObjectByType<ShipController>() == null) return;
            new GameObject("CombatDevTools").AddComponent<CombatDevTools>();

            // 개발 빌드를 "-combatVerify <폴더>"로 실행하면 자동 전투 검증을 돌리고 끝나면 종료한다
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-combatVerify") continue;
                var runner = new GameObject("CombatVerificationRunner").AddComponent<CombatVerificationRunner>();
                runner.OutputDirectory = args[i + 1];
                runner.QuitWhenDone = true;
                runner.RadarOnly = System.Array.IndexOf(args, "-radarOnly") >= 0;
                runner.AmmoOnly = System.Array.IndexOf(args, "-ammoOnly") >= 0;
                runner.DefenseOnly = System.Array.IndexOf(args, "-defenseOnly") >= 0;
                runner.LogisticsOnly = System.Array.IndexOf(args, "-logisticsOnly") >= 0;
                runner.SpecOnly = System.Array.IndexOf(args, "-specOnly") >= 0;
                runner.TabOnly = System.Array.IndexOf(args, "-tabOnly") >= 0;
                runner.BurstOnly = System.Array.IndexOf(args, "-burstOnly") >= 0;
                runner.EnemyOnly = System.Array.IndexOf(args, "-enemyOnly") >= 0;
                runner.HelmOnly = System.Array.IndexOf(args, "-helmOnly") >= 0;
                runner.HudOnly = System.Array.IndexOf(args, "-hudOnly") >= 0;
                runner.WaterOnly = System.Array.IndexOf(args, "-waterOnly") >= 0;
                runner.TorpedoOnly = System.Array.IndexOf(args, "-torpedoOnly") >= 0;
                runner.IslandOnly = System.Array.IndexOf(args, "-islandOnly") >= 0;
                runner.SuicideOnly = System.Array.IndexOf(args, "-suicideOnly") >= 0;
                runner.DecorOnly = System.Array.IndexOf(args, "-decorOnly") >= 0;
                runner.LifeOnly = System.Array.IndexOf(args, "-lifeOnly") >= 0;
                runner.UpgradeOnly = System.Array.IndexOf(args, "-upgradeOnly") >= 0;
                runner.UiShots = System.Array.IndexOf(args, "-uiShots") >= 0;
                runner.FactionOnly = System.Array.IndexOf(args, "-factionOnly") >= 0;
                runner.EscortOnly = System.Array.IndexOf(args, "-escortOnly") >= 0;
                runner.ThreatOnly = System.Array.IndexOf(args, "-threatOnly") >= 0;
                runner.GrowthOnly = System.Array.IndexOf(args, "-growthOnly") >= 0;
                runner.EnvOnly = System.Array.IndexOf(args, "-envOnly") >= 0;
                runner.VariantOnly = System.Array.IndexOf(args, "-variantOnly") >= 0;
                runner.FormationOnly = System.Array.IndexOf(args, "-formationOnly") >= 0;
                runner.CodexOnly = System.Array.IndexOf(args, "-codexOnly") >= 0;
                runner.PccSoak = System.Array.IndexOf(args, "-pccSoak") >= 0;
                break;
            }
        }

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetOverlay(bool visible)
        {
            _visible = visible;
            foreach (var r in _rings) if (r != null) r.gameObject.SetActive(visible);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f3Key.wasPressedThisFrame) SetOverlay(!_visible);

            EnsureRings();
            FollowRings();

            if (!_visible || kb == null) return;
            if (kb.f5Key.wasPressedThisFrame) SpawnRing("ene_fastboat", 8, 44f);
            if (kb.f6Key.wasPressedThisFrame) SpawnRing("ene_missileboat", 3, 50f);
            if (kb.f7Key.wasPressedThisFrame) MissileVolley(6, 52f);
            if (kb.f8Key.wasPressedThisFrame) SpawnSubmarines(2, 18f);
            if (kb.f9Key.wasPressedThisFrame) InstallTestLoadout();
            if (kb.f10Key.wasPressedThisFrame) { CombatStats.Reset(); CombatLog.Clear(); }
            if (kb.f4Key.wasPressedThisFrame) CombatLog.ToConsole = !CombatLog.ToConsole;
            if (kb.f11Key.wasPressedThisFrame) { SpawnRing("ene_drone", 4, 46f); SpawnRing("ene_fighter", 2, 50f); }
        }

        // ------------------------------------------------------------ 거리 링

        private void EnsureRings()
        {
            if (_rings.Count > 0) return;

            var shader = Shader.Find("Sprites/Default");
            for (int i = 0; i < RingDistances.Length; i++)
            {
                var go = new GameObject($"DevRange_{RingDistances[i]:0}");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.loop = true;
                lr.useWorldSpace = true;
                lr.positionCount = 96;
                lr.widthMultiplier = 0.25f;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                if (shader != null) lr.sharedMaterial = new Material(shader);
                lr.startColor = lr.endColor = RingColors[i];
                go.SetActive(_visible);
                _rings.Add(lr);
            }
        }

        private void FollowRings()
        {
            var player = PlayerTransform();
            if (player == null) return;

            for (int r = 0; r < _rings.Count; r++)
            {
                var lr = _rings[r];
                if (lr == null || !lr.gameObject.activeSelf) continue;
                float radius = RingDistances[r];
                for (int i = 0; i < lr.positionCount; i++)
                {
                    float a = i * Mathf.PI * 2f / lr.positionCount;
                    lr.SetPosition(i, player.position + new Vector3(Mathf.Sin(a) * radius, 0.15f, Mathf.Cos(a) * radius));
                }
            }
        }

        // ------------------------------------------------------------ 오버레이

        private readonly List<CombatLog.Entry> _recentLog = new(16);

        private void OnGUI()
        {
            if (!_visible) return;
            _style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14, richText = true };

            _sb.Clear();
            _sb.AppendLine("<b>전투 개발 도구</b>  F3 닫기 · F4 기록 콘솔 · F5 고속정 · F6 미사일정 · F7 미사일 · F8 잠수함 · F9 시험 무장 · F10 초기화 · F11 항공");

            var cam = Camera.main;
            var player = PlayerTransform();
            if (cam != null && player != null)
            {
                var edges = MeasureVisibleSeaDistance(cam, player.position);
                _sb.AppendLine($"화면 가장자리까지 수면 거리: 최소 {edges.min:0.0}  (위 {edges.up:0} · 아래 {edges.down:0} · 좌우 {edges.side:0})  카메라 거리 {Vector3.Distance(cam.transform.position, player.position):0}");
            }

            var targeting = Object.FindFirstObjectByType<TargetingSystem>();
            if (targeting != null) _sb.AppendLine($"레이더 탐지 {targeting.RadarRange:0} · 근접 센서 {targeting.VisualRange:0}");

            _sb.AppendLine("\n<b>무기</b>        발사   명중   요격   화면내명중");
            foreach (var kv in CombatStats.Weapons)
            {
                var e = kv.Value;
                string ratio = e.Hits > 0 ? $"{100f * e.OnScreenHits / e.Hits:0}%" : "-";
                _sb.AppendLine($"{kv.Key,-8} {e.Fired,6} {e.Hits,6} {e.Intercepts,6}   {ratio}");
            }

            _sb.AppendLine("\n<b>적 미사일</b>   수   화면 진입 후 시간(평균/최소/최대)");
            foreach (var kv in CombatStats.Threats)
            {
                var e = kv.Value;
                string label = kv.Key switch { ThreatOutcome.Intercepted => "요격", ThreatOutcome.HitShip => "피격", _ => "소멸" };
                string t = e.Seen > 0 ? $"{e.SumSeconds / e.Seen:0.0} / {e.MinSeconds:0.0} / {e.MaxSeconds:0.0}초" : "-";
                _sb.AppendLine($"{label,-6} {e.Count,4}   {t}");
            }

            // 방어 흐름 추적: 최근 전투 기록
            CombatLog.CopyRecent(_recentLog, 14);
            _sb.AppendLine($"\n<b>전투 기록</b> (최근 {_recentLog.Count} / 콘솔 {(CombatLog.ToConsole ? "켬" : "끔")})");
            foreach (var e in _recentLog) _sb.AppendLine($"<size=12>{e}</size>");

            GUI.Box(new Rect(10, 10, 760, 22 + 18 * CountLines(_sb)), _sb.ToString(), _style);

            // 링 거리 글자
            if (cam != null && player != null)
            {
                for (int i = 0; i < RingDistances.Length; i++)
                {
                    Vector3 sp = cam.WorldToScreenPoint(player.position + new Vector3(RingDistances[i], 0f, 0f));
                    if (sp.z > 0f) GUI.Label(new Rect(sp.x + 4, Screen.height - sp.y - 10, 60, 20), $"{RingDistances[i]:0}");
                }
            }
        }

        private static int CountLines(StringBuilder sb)
        {
            int n = 1;
            for (int i = 0; i < sb.Length; i++) if (sb[i] == '\n') n++;
            return n;
        }

        /// <summary>실제 카메라 광선으로 화면 네 가장자리 중점이 닿는 수면(y=0)까지 함선에서의 거리를 잰다.</summary>
        public static (float min, float up, float down, float side) MeasureVisibleSeaDistance(Camera cam, Vector3 ship)
        {
            float Hit(Vector2 viewport)
            {
                var ray = cam.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
                if (ray.direction.y >= -0.0001f) return float.PositiveInfinity;
                float t = -ray.origin.y / ray.direction.y;
                Vector3 p = ray.origin + ray.direction * t;
                return Vector2.Distance(new Vector2(p.x, p.z), new Vector2(ship.x, ship.z));
            }

            // 함선이 보이는 화면 높이에서 좌우 가장자리를 잰다
            Vector3 v = cam.WorldToViewportPoint(new Vector3(ship.x, 0f, ship.z));
            float up = Hit(new Vector2(0.5f, 1f));
            float down = Hit(new Vector2(0.5f, 0f));
            float side = Mathf.Min(Hit(new Vector2(0f, Mathf.Clamp01(v.y))), Hit(new Vector2(1f, Mathf.Clamp01(v.y))));
            return (Mathf.Min(up, Mathf.Min(down, side)), up, down, side);
        }

        // ------------------------------------------------------------ 시나리오

        public static Transform PlayerTransform()
            => GameManager.Instance != null && GameManager.Instance.Player != null ? GameManager.Instance.Player.transform : null;

        public static EnemyDefinition FindEnemy(string id)
        {
            foreach (var def in Resources.FindObjectsOfTypeAll<EnemyDefinition>())
                if (def != null && def.Id == id) return def;
            return null;
        }

        public static ModuleDefinition FindModule(string id)
        {
            foreach (var def in Resources.FindObjectsOfTypeAll<ModuleDefinition>())
                if (def != null && def.Id == id) return def;
            return null;
        }

        /// <summary>함선 둘레 원 위에 적을 고르게 띄운다.</summary>
        public static List<EnemyController> SpawnRing(string enemyId, int count, float radius, float startAngleDeg = 0f)
        {
            var result = new List<EnemyController>();
            var def = FindEnemy(enemyId);
            var player = PlayerTransform();
            var spawner = EnemySpawner.Instance;
            if (def == null || player == null || spawner == null)
            {
                Debug.LogWarning($"[CombatDevTools] {enemyId} 스폰 불가 (정의/플레이어/스포너 없음)");
                return result;
            }

            for (int i = 0; i < count; i++)
            {
                float a = (startAngleDeg + i * 360f / count) * Mathf.Deg2Rad;
                Vector3 pos = player.position + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
                pos.y = 0f;
                Vector3 to = player.position - pos; to.y = 0f;
                var enemy = spawner.SpawnAt(def, pos, Quaternion.LookRotation(to.normalized, Vector3.up));
                if (enemy != null) result.Add(enemy);
            }
            return result;
        }

        /// <summary>함선 둘레에서 적 미사일을 한꺼번에 발사한다(순항 고도에서 출발).</summary>
        public static int MissileVolley(int count, float radius, float startAngleDeg = 15f, float damageOverride = -1f)
        {
            var def = FindEnemy("ene_missileboat");
            var player = PlayerTransform();
            if (def == null || def.MissilePrefab == null || player == null || PoolManager.Instance == null) return 0;

            int fired = 0;
            for (int i = 0; i < count; i++)
            {
                float a = (startAngleDeg + i * 360f / count) * Mathf.Deg2Rad;
                Vector3 pos = player.position + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
                pos.y = 3f;
                Vector3 to = player.position - pos;
                var go = PoolManager.Instance.Spawn(def.MissilePrefab, pos, Quaternion.LookRotation(to.normalized, Vector3.up));
                if (go != null && go.TryGetComponent<Missile>(out var m)) { m.Launch(player, damageOverride > 0f ? damageOverride : def.AttackDamage); fired++; }
            }
            return fired;
        }

        public static void SpawnSubmarines(int count, float radius)
        {
            foreach (var e in SpawnRing("ene_submarine", count, radius, 45f))
                if (e is SubmarineBase sub) sub.RevealFor(30f);
        }

        /// <summary>시험 무장: 76mm·유도로켓·SAM·VLS·레이더·폭뢰·기관포를 빈 칸에 붙인다. 붙인 수를 돌려준다.</summary>
        public static int InstallTestLoadout()
        {
            var factory = Object.FindFirstObjectByType<ModuleFactory>();
            var ship = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (factory == null || ship == null || ship.Grid == null) return 0;

            string[] ids = { "mod_gun76", "mod_rocket", "mod_sam", "mod_vls", "mod_radar", "mod_asw", "mod_autocannon" };
            int installed = 0;
            foreach (var id in ids)
            {
                var def = FindModule(id);
                if (def == null) continue;
                if (TryInstallNear(factory, ship.Grid, def)) installed++;
            }
            ship.Systems?.Recalculate();
            return installed;
        }

        /// <summary>모듈 하나를 함선 가운데 가까운 빈 자리에 설치한다.</summary>
        public static bool InstallModule(string id)
        {
            var factory = Object.FindFirstObjectByType<ModuleFactory>();
            var ship = GameManager.Instance != null ? GameManager.Instance.Player : null;
            var def = FindModule(id);
            if (factory == null || ship == null || ship.Grid == null || def == null) return false;
            bool ok = TryInstallNear(factory, ship.Grid, def);
            ship.Systems?.Recalculate();
            return ok;
        }

        /// <summary>center에서 맨해튼 거리 maxDistance 이내(가까운 순)의 빈 자리에 모듈을 설치한다. 실패하면 null.</summary>
        public static ModuleInstance InstallModuleNear(string id, GridCoord center, int maxDistance)
        {
            var factory = Object.FindFirstObjectByType<ModuleFactory>();
            var ship = GameManager.Instance != null ? GameManager.Instance.Player : null;
            var def = FindModule(id);
            if (factory == null || ship == null || ship.Grid == null || def == null) return null;

            for (int d = 1; d <= maxDistance; d++)
                for (int x = -d; x <= d; x++)
                    for (int z = -d; z <= d; z++)
                    {
                        if (Mathf.Abs(x) + Mathf.Abs(z) != d) continue;
                        var c = new GridCoord(center.X + x, center.Z + z);
                        for (int rot = 0; rot < (def.CanRotate ? 4 : 1); rot++)
                        {
                            if (!ship.Grid.CanPlace(def, c, rot, out _)) continue;
                            var m = factory.Install(def, c, rot);
                            if (m == null) continue;
                            ship.Systems?.Recalculate();
                            return m;
                        }
                    }
            return null;
        }

        public static void RemoveModule(ModuleInstance instance)
        {
            var factory = Object.FindFirstObjectByType<ModuleFactory>();
            if (factory == null || instance == null) return;
            factory.Uninstall(instance);
            GameManager.Instance?.Player?.Systems?.Recalculate();
        }

        private static bool TryInstallNear(ModuleFactory factory, ShipGrid grid, ModuleDefinition def)
        {
            // 가운데에서 가까운 칸부터: 좌우로 벌리고, 앞뒤로 늘린다
            for (int ring = 1; ring <= 10; ring++)
            {
                for (int x = -ring; x <= ring; x++)
                {
                    for (int z = -ring; z <= ring; z++)
                    {
                        if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) != ring) continue;
                        var c = new GridCoord(x, z);
                        for (int rot = 0; rot < (def.CanRotate ? 4 : 1); rot++)
                        {
                            if (!grid.CanPlace(def, c, rot, out _)) continue;
                            if (factory.Install(def, c, rot) != null) return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>남은 적과 적 미사일을 보상 없이 치운다.</summary>
        public static void ClearBattlefield()
        {
            EnemySpawner.Instance?.ClearAll();
            var missiles = new List<ITargetable>(TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Missile));
            foreach (var t in missiles)
                if (t is Missile m && m.isActiveAndEnabled && PoolManager.Instance != null) PoolManager.Instance.Despawn(m.gameObject);
        }
    }
}
#endif

using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Codex IslandBiomePack_Refined(2026-11-09) 섬 · 시설 · 자연물(2026-11-15). FBX를 실행 중에 불러 조립한다(에디터 프리팹 생성 없음).
    ///   - 적 기지 섬(군도): 지형 MIL_Island_S/M/L(평평한 부지 y ≈ 1) 위에 Codex 후속 계획 순서로 —
    ///     부지 → 철조망 · 8m 정문 → 도로(중·대) → 필수 건물(소초 · 레이더 · 지휘소) → 선택(막사 · 창고 · 유류 · 발전기 · 감시탑 · 미사일 차량 …).
    ///     미사일 포대 자리(소 1 · 중 1 · 대 2)는 비워 두고 IslandInfo.BatterySpots로 넘긴다 — CoastalBatteryField가 해안 미사일 포대 적을 세운다
    ///   - 자연 섬: 모래섬 NAT_IsletBase(야자수 · 텐트 · 모닥불 · 유목 · 난파 보트 · 관목) · 바위섬 NAT_RockIslet
    ///   - 절차형 무인도 · 바위섬 장식(DressNature): 야자수 · 사람 흔적(텐트 · 모닥불) · 해안 유목 · 난파 보트 · 바위 관목
    /// 해안선 반경과 평평한 부지는 메시 꼭짓점에서 잰다(섬 지형 FBX는 읽기 가능으로 가져온다). 판정은 메시마다 볼록 콜라이더.
    /// 섬 재질 이름(MAT_Island…)은 게임 섬 재질로 바꿔 끼운다. 모델 아래에는 KeepSharedMeshes — 섬을 치울 때 FBX 메시를 지우지 않는다.
    /// </summary>
    public static partial class IslandBuilder
    {
        public enum Kit { BaseS, BaseM, BaseL, SandIslet, RockIslet }

        public const int KitRimSamples = 48;

        private sealed class KitShape
        {
            public GameObject Prefab;
            public float[] Rim, Plateau;
            public float ShoreRadius, Height, PlateauY;
        }

        private static readonly Dictionary<string, KitShape> s_kit = new();

        /// <summary>
        /// 기지 섬 지형 변형 수(2026-11-21 Codex IslandBaseFollowup_v1: 규모마다 B · C형 추가 → A · B · C 세 가지).
        /// 섬 자리로 정해 같은 판 · 같은 자리에는 늘 같은 변형(무작위 회전과 함께 반복을 가린다). 변형 모델이 없으면 A형.
        /// </summary>
        public const int BaseVariants = 3;
        private static readonly string[] VariantSuffix = { "", "_B", "_C" };

        /// <summary>섬 자리(정수로 반올림한 x, z)로 정하는 지형 변형 번호(0 = A).</summary>
        public static int VariantFor(Kit k, Vector3 center)
        {
            if (!IsBase(k)) return 0;
            unchecked
            {
                uint h = (uint)Mathf.RoundToInt(center.x) * 73856093u ^ (uint)Mathf.RoundToInt(center.z) * 19349663u ^ (uint)k * 83492791u;
                h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
                return (int)(h % BaseVariants);
            }
        }
        private static readonly Dictionary<string, GameObject> s_kitModels = new();
        private static readonly Dictionary<string, Material> s_kitIslandMats = new();

        private static string KitPath(Kit k, int variant) => KitPath(k) + (IsBase(k) && variant > 0 && variant < BaseVariants ? VariantSuffix[variant] : "");

        private static string KitPath(Kit k) => k switch
        {
            Kit.BaseS => "Environment/Military/MIL_Island_S",
            Kit.BaseM => "Environment/Military/MIL_Island_M",
            Kit.BaseL => "Environment/Military/MIL_Island_L",
            Kit.SandIslet => "Environment/Nature/NAT_IsletBase",
            _ => "Environment/Nature/NAT_RockIslet",
        };

        public static bool IsBase(Kit k) => k is Kit.BaseS or Kit.BaseM or Kit.BaseL;

        /// <summary>그 섬 종류가 쓸 수 있는가(모델이 있는가).</summary>
        public static bool KitAvailable(Kit k) => !ArtDisabled && Shape(k) != null;

        /// <summary>해안선 최대 반경(축척 1) — 기지 섬은 변형 중 가장 큰 것(배치 간격용).</summary>
        public static float KitRadius(Kit k)
        {
            float r = Shape(k)?.ShoreRadius ?? 0f;
            if (IsBase(k)) for (int v = 1; v < BaseVariants; v++) r = Mathf.Max(r, Shape(k, v)?.ShoreRadius ?? 0f);
            return r;
        }

        /// <summary>그 변형의 모양. 변형 모델이 없으면 A형.</summary>
        private static KitShape Shape(Kit k, int variant = 0)
        {
            string path = KitPath(k, variant);
            if (s_kit.TryGetValue(path, out var s)) return s ?? (variant > 0 ? Shape(k) : null);
            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null) { s_kit[path] = null; return variant > 0 ? Shape(k) : null; }
            s = new KitShape { Prefab = prefab, Rim = new float[KitRimSamples], Plateau = new float[KitRimSamples] };
            var pts = new List<Vector3>();
            float top = float.MinValue;
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                var m = mf.transform.localToWorldMatrix;   // 프리팹 루트 기준(인스턴스를 부모 원점에 두면 같은 변환)
                if (mesh.isReadable)
                    foreach (var v in mesh.vertices) { var p = m.MultiplyPoint3x4(v); pts.Add(p); top = Mathf.Max(top, p.y); }
                else
                {
                    var b = mesh.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        var p = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                        pts.Add(p); top = Mathf.Max(top, p.y);
                    }
                }
            }
            s.Height = top;
            s.PlateauY = top;
            // 평평한 부지: 꼭대기에서 12cm 안의 꼭짓점(군사기지 섬 부지 y ≈ 1). 바위섬은 의미 없음(반경만 씀)
            foreach (var p in pts)
            {
                float r = new Vector2(p.x, p.z).magnitude;
                int i = Bucket(Mathf.Atan2(p.z, p.x));
                if (p.y >= SeaY - 0.05f) s.Rim[i] = Mathf.Max(s.Rim[i], r);
                if (p.y >= top - 0.12f) s.Plateau[i] = Mathf.Max(s.Plateau[i], r);
            }
            FillGaps(s.Rim);
            FillGaps(s.Plateau);
            foreach (var r in s.Rim) s.ShoreRadius = Mathf.Max(s.ShoreRadius, r);
            s_kit[path] = s;
            return s;
        }

        private static int Bucket(float angle) => Mathf.FloorToInt(Mathf.Repeat(angle, Mathf.PI * 2f) / (Mathf.PI * 2f) * KitRimSamples) % KitRimSamples;

        private static void FillGaps(float[] a)
        {
            int n = a.Length;
            for (int i = 0; i < n; i++)
            {
                if (a[i] > 0f) continue;
                int l = i, r = i;
                for (int k = 1; k < n && a[l] <= 0f; k++) l = (i - k + n) % n;
                for (int k = 1; k < n && a[r] <= 0f; k++) r = (i + k) % n;
                a[i] = Mathf.Max(0.5f, Mathf.Min(a[l], a[r]));
            }
        }

        private static float Sample(float[] a, float angle)
        {
            int n = a.Length;
            float f = Mathf.Repeat(angle, Mathf.PI * 2f) / (Mathf.PI * 2f) * n;
            int i = Mathf.FloorToInt(f) % n;
            return Mathf.Lerp(a[i], a[(i + 1) % n], f - Mathf.Floor(f));
        }

        /// <summary>섬 로컬(x, z)이 평평한 부지 안(가장자리에서 margin m)인가.</summary>
        private static bool OnPlateau(KitShape s, float x, float z, float margin)
            => new Vector2(x, z).magnitude <= Sample(s.Plateau, Mathf.Atan2(z, x)) - margin;

        /// <summary>발자국(가로 w, 세로 d, yaw)의 네 모서리가 모두 부지 안인가.</summary>
        private static bool FitsPlateau(KitShape s, Vector3 c, float w, float d, float yaw, float margin)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            for (int i = 0; i < 4; i++)
            {
                var corner = c + rot * new Vector3((i & 1) == 0 ? -w * 0.5f : w * 0.5f, 0f, (i & 2) == 0 ? -d * 0.5f : d * 0.5f);
                if (!OnPlateau(s, corner.x, corner.z, margin)) return false;
            }
            return true;
        }

        /// <summary>Codex 키트 모델 하나(Resources 경로)를 부모 로컬 자리에 둔다. 섬 재질은 게임 섬 재질로, 콜라이더는 뺀다.</summary>
        public static GameObject PlaceKitModel(Transform parent, string path, Vector3 local, float yaw, float scale = 1f)
        {
            if (!s_kitModels.TryGetValue(path, out var prefab)) s_kitModels[path] = prefab = Resources.Load<GameObject>(path);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, parent, false);
            go.name = prefab.name;
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * prefab.transform.localRotation;
            go.transform.localScale = prefab.transform.localScale * scale;
            go.AddComponent<KeepSharedMeshes>();
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            RemapIslandMaterials(go);
            return go;
        }

        private static void RemapIslandMaterials(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string n = mats[i].name.Replace(" (Instance)", "");
                    if (!n.StartsWith("MAT_Island", System.StringComparison.Ordinal)) continue;
                    if (!s_kitIslandMats.TryGetValue(n, out var m)) s_kitIslandMats[n] = m = Resources.Load<Material>("Islands/" + n);
                    if (m != null) { mats[i] = m; changed = true; }
                }
                if (changed) r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        /// <summary>키트 섬 하나를 세운다. 군사기지 섬이면 시설을 조립하고 미사일 포대 자리를 IslandInfo.BatterySpots로 넘긴다.</summary>
        public static IslandInfo BuildKit(Vector3 center, Kit kind, System.Random rng, Transform parent, float scale = 1f, int variant = -1)
        {
            if (variant < 0) variant = VariantFor(kind, center);
            var s = Shape(kind, variant);
            if (s == null) return null;
            LoadMaterials();
            var root = new GameObject($"Island ({s.Prefab.name})");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(new Vector3(center.x, 0f, center.z), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));

            var terrain = Object.Instantiate(s.Prefab, root.transform, false);
            terrain.name = "Kit terrain";
            terrain.transform.localScale = s.Prefab.transform.localScale * scale;
            terrain.transform.localPosition = new Vector3(0f, SeaY * (1f - scale), 0f);   // 수면선(−0.9)은 축척과 상관없이
            terrain.AddComponent<KeepSharedMeshes>();
            RemapIslandMaterials(terrain);
            foreach (var mf in terrain.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = true;
            }

            AddFoamRing(root.transform, a => Sample(s.Rim, a) * scale, rng);

            var info = new IslandInfo
            {
                Center = root.transform.position, Radius = s.ShoreRadius * scale, Height = s.Height * scale + 0.9f,
                Rocky = kind == Kit.RockIslet, Root = root, Art = s.Prefab.name,
                ShoreAt = a => Sample(s.Rim, a) * scale * 0.92f,
            };
            if (IsBase(kind)) ComposeBase(root.transform, s, kind, rng, info);
            else if (kind == Kit.SandIslet) DressIslet(root.transform, s, rng);
            else if (kind == Kit.RockIslet && rng.NextDouble() < 0.6)
                PlaceKitModel(root.transform, "Environment/Nature/NAT_RockShrub", new Vector3(0f, s.Height - 0.6f, 0f), (float)rng.NextDouble() * 360f);

            var rim = new Vector2[KitRimSamples];
            for (int i = 0; i < KitRimSamples; i++)
            {
                float a = i * Mathf.PI * 2f / KitRimSamples;
                float r = s.Rim[i] * scale * 0.92f;
                var w = root.transform.TransformPoint(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                rim[i] = new Vector2(w.x, w.z);
            }
            info.Rim = rim;
            SetLayer(root, Islands.Layer);
            Islands.Register(info);
            return info;
        }

        // ------------------------------------------------------------ 적 기지 조립

        private readonly struct Facility
        {
            public readonly string Model; public readonly float W, D; public readonly bool Battery;
            public Facility(string model, float w, float d, bool battery = false) { Model = model; W = w; D = d; Battery = battery; }
        }

        private const string Mil = "Environment/Military/";
        private static readonly Facility Battery = new(null, 5.2f, 6f, true);
        private static readonly Facility CommandPost = new(Mil + "MIL_CommandPost", 11f, 7.5f);
        private static readonly Facility Barracks = new(Mil + "MIL_Barracks", 9.4f, 6.4f);
        private static readonly Facility Storage = new(Mil + "MIL_Storage", 8.5f, 6.5f);
        private static readonly Facility Outpost = new(Mil + "MIL_Outpost", 4.8f, 4f);
        private static readonly Facility Radar = new(Mil + "MIL_Radar", 4f, 4f);
        private static readonly Facility Generator = new(Mil + "MIL_Generator", 3f, 2f);
        private static readonly Facility FuelTanks = new(Mil + "MIL_FuelTanks", 4.8f, 3f);
        private static readonly Facility Truck = new(Mil + "MIL_MissileTruck_2", 3.35f, 7.4f);
        private static readonly Facility Shelter = new(Mil + "MIL_CamoShelter", 5.5f, 8.8f);
        private static readonly Facility Crates = new(Mil + "MIL_SupplyCrates", 2.6f, 1.6f);

        /// <summary>
        /// 시설 조립. 부지 안쪽 직사각형(평평한 부지에 네 모서리가 들어가게 줄인다)을 철조망으로 두르고 앞(−z)에 정문,
        /// 중 · 대는 정문에서 안쪽으로 도로. 건물은 도로 양옆 두 줄에 차례로 넣되 부지에 들어가지 않으면 건너뛴다.
        /// </summary>
        private static void ComposeBase(Transform root, KitShape s, Kit kind, System.Random rng, IslandInfo info)
        {
            float y = s.PlateauY;
            var site = new GameObject("Base site").transform;
            site.SetParent(root, false);
            site.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() < 0.5 ? 0f : 90f, 0f);   // 섬 긴 쪽에 맞춰 둘 중 하나
            float siteYaw = site.localEulerAngles.y;

            // 부지 직사각형: 부지 반경으로 시작해 네 모서리가 들어갈 때까지 줄인다(4m 철조망 칸에 맞춤)
            float Ax(float a) => Sample(s.Plateau, a + siteYaw * Mathf.Deg2Rad);
            float hx = Mathf.Min(Ax(0f), Ax(Mathf.PI)) * 0.8f, hz = Mathf.Min(Ax(Mathf.PI * 0.5f), Ax(Mathf.PI * 1.5f)) * 0.8f;
            for (int it = 0; it < 30; it++)
            {
                bool ok = true;
                for (int i = 0; i < 4 && ok; i++)
                {
                    var c = site.localRotation * new Vector3((i & 1) == 0 ? -hx : hx, 0f, (i & 2) == 0 ? -hz : hz);
                    ok = OnPlateau(s, c.x, c.z, 1.2f);
                }
                if (ok) break;
                hx *= 0.94f; hz *= 0.94f;
            }
            int nx = Mathf.Max(2, Mathf.FloorToInt(hx * 2f / 4f)), nz = Mathf.Max(2, Mathf.FloorToInt(hz * 2f / 4f));
            hx = nx * 2f; hz = nz * 2f;
            bool road = kind != Kit.BaseS && hx >= 11f;

            // 철조망 · 정문(앞 −z 가운데 8m)
            for (int i = 0; i < nx; i++)
            {
                float x = -hx + 2f + i * 4f;
                PlaceKitModel(site, Mil + "MIL_Fence_4m", new Vector3(x, y, hz), 0f);
                if (Mathf.Abs(x) > 4.1f || nx < 4) PlaceKitModel(site, Mil + "MIL_Fence_4m", new Vector3(x, y, -hz), 0f);
            }
            for (int i = 0; i < nz; i++)
            {
                float z = -hz + 2f + i * 4f;
                PlaceKitModel(site, Mil + "MIL_Fence_4m", new Vector3(-hx, y, z), 90f);
                PlaceKitModel(site, Mil + "MIL_Fence_4m", new Vector3(hx, y, z), 90f);
            }
            if (nx >= 4) PlaceKitModel(site, Mil + "MIL_Gate_8m", new Vector3(0f, y, -hz), 0f);
            PlaceKitModel(site, Mil + "MIL_GuardHut", new Vector3(road ? 6.5f : hx - 3f, y, -hz + 2.6f), 180f);

            // 임시 부두: 정문 앞 해안에 기존 해안 소품 부잔교(Codex 기지 섬 부두 모델이 오면 바꾼다 — Docs/NEXT_TASKS 참고)
            Vector3 gateDir = site.localRotation * Vector3.back;
            float gateAngle = Mathf.Atan2(gateDir.z, gateDir.x);
            Vector3 shoreLocal = gateDir * Sample(s.Rim, gateAngle) * 0.93f;
            PlaceAnchored("ENV_FloatingPontoon", root, root.TransformPoint(shoreLocal), root.TransformDirection(gateDir), "ShoreConnection");

            // 도로(중 · 대): 정문에서 안쪽으로 12m 칸
            float laneHalf = 0f;
            if (road)
            {
                laneHalf = 4.6f;
                for (float z = -hz + 6f; z + 6f <= hz - 1f; z += 12f)
                    PlaceKitModel(site, Mil + "MIL_RoadStraight", new Vector3(0f, y + 0.01f, z), 0f);
                for (float z = -hz + 8f; z < hz - 2f; z += 12f)
                {
                    PlaceKitModel(site, Mil + "MIL_LightPole", new Vector3(-4.6f, y, z), 90f);
                    PlaceKitModel(site, Mil + "MIL_LightPole", new Vector3(4.6f, y, z), -90f);
                }
            }

            // 감시탑: 뒤쪽 두 모서리(중 · 대)
            if (kind != Kit.BaseS)
            {
                PlaceKitModel(site, Mil + "MIL_Watchtower", new Vector3(-hx + 2f, y, hz - 2f), 135f);
                PlaceKitModel(site, Mil + "MIL_Watchtower", new Vector3(hx - 2f, y, hz - 2f), 225f);
            }

            var list = kind switch
            {
                Kit.BaseS => new List<Facility> { Outpost, Radar, Generator, Crates },
                Kit.BaseM => new List<Facility> { Outpost, Radar, Outpost, Storage, FuelTanks, Truck, Crates },
                _ => new List<Facility> { CommandPost, Radar, Barracks, Outpost, Barracks, Outpost, Storage, Truck, Generator, FuelTanks, Shelter, Crates },
            };

            // 두 줄(도로 왼쪽 · 오른쪽)에 앞에서부터 채운다. 왼쪽 줄 건물은 +x(도로)를, 오른쪽 줄은 −x를 본다(도로가 없으면 −z 정문 쪽)
            float colInner = laneHalf + 0.8f, colOuter = hx - 1.2f;
            var cursor = new[] { -hz + (road ? 3.2f : 4.5f), -hz + (road ? 3.2f : 4.5f) };
            var spots = new List<Vector3>();
            int col = 0;
            foreach (var f in list)
            {
                bool placed = false;
                for (int tries = 0; tries < 2 && !placed; tries++, col ^= 1)
                {
                    float side = col == 0 ? -1f : 1f;
                    float yaw = road ? (col == 0 ? 90f : -90f) : 180f;
                    // 도로를 보면 발자국이 돌아간다(가로 ↔ 세로)
                    float w = road ? f.D : f.W, d = road ? f.W : f.D;
                    if (w > colOuter - colInner + 0.01f) { (w, d) = (d, w); yaw += 90f; }
                    if (w > colOuter - colInner + 0.01f) continue;
                    float z = cursor[col] + d * 0.5f;
                    if (z + d * 0.5f > hz - (kind != Kit.BaseS ? 4.2f : 1.2f)) continue;
                    float x = side * (colInner + (colOuter - colInner) * 0.5f);
                    var local = new Vector3(x, y, z);
                    var inRoot = site.localRotation * local;
                    if (!FitsPlateau(s, inRoot, w, d, siteYaw + yaw, 0.6f)) continue;
                    if (f.Battery) spots.Add(root.TransformPoint(inRoot));
                    else
                    {
                        var go = PlaceKitModel(site, f.Model, local, yaw);
                        if (go != null && f.Model.EndsWith("MIL_Radar")) SpinRadar(go);
                    }
                    cursor[col] = z + d * 0.5f + 1.4f;
                    placed = true;
                }
            }
            // 미사일 포대 자리: 철조망 바깥 해안 쪽 평지(좌 · 우 → 뒤 → 앞 모서리 순서로 들어가는 곳). 바다에서 보이고 사선이 트인다
            int wantBatteries = kind == Kit.BaseL ? 2 : 1;
            var candidates = new[]
            {
                new Vector3(hx + 4.5f, y, 0f), new Vector3(-hx - 4.5f, y, 0f), new Vector3(0f, y, hz + 4.5f),
                new Vector3(hx + 4f, y, hz * 0.5f), new Vector3(-hx - 4f, y, -hz * 0.5f), new Vector3(hx + 3.5f, y, -hz * 0.6f),
                new Vector3(-hx - 3.5f, y, hz * 0.6f), new Vector3(hx * 0.5f, y, hz + 4f), new Vector3(-hx * 0.5f, y, hz + 4f),
            };
            foreach (var c in candidates)
            {
                if (spots.Count >= wantBatteries) break;
                var inRoot = site.localRotation * c;
                if (!FitsPlateau(s, inRoot, Battery.W, Battery.D, siteYaw, 0.4f)) continue;
                spots.Add(root.TransformPoint(inRoot));
            }
            info.MilitaryBase = true;
            info.BatterySpots = spots.ToArray();
            info.BaseSize = kind switch { Kit.BaseS => 1, Kit.BaseM => 2, _ => 3 };
        }

        /// <summary>레이더 안테나를 돌린다(EnvironmentArt의 회전부 — 6초에 한 바퀴).</summary>
        private static void SpinRadar(GameObject radar)
        {
            Transform pivot = null;
            foreach (var t in radar.GetComponentsInChildren<Transform>(true)) if (t.name == "RadarPivot") { pivot = t; break; }
            if (pivot == null) return;
            var art = radar.AddComponent<EnvironmentArt>();
            art.spinners = new[] { pivot };
        }

        // ------------------------------------------------------------ 자연 장식

        private const string Nat = "Environment/Nature/";

        /// <summary>모래섬: 야자수 1~2 · 사람 흔적(텐트 + 모닥불) · 해안 유목 · 난파 보트(물가) · 관목.</summary>
        private static void DressIslet(Transform root, KitShape s, System.Random rng)
        {
            float y = s.PlateauY;
            float a0 = (float)rng.NextDouble() * Mathf.PI * 2f;
            Vector3 At(float a, float frac) { float r = Sample(s.Plateau, a) * frac; return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r); }
            int palms = 1 + (rng.NextDouble() < 0.6 ? 1 : 0);
            for (int i = 0; i < palms; i++)
                PlaceKitModel(root, Nat + (i == 0 ? "NAT_Palm_A" : "NAT_Palm_B"), At(a0 + i * 0.9f, 0.35f), (float)rng.NextDouble() * 360f, Range(rng, 0.9f, 1.15f));
            PlaceKitModel(root, Nat + "NAT_CastawayTent", At(a0 + Mathf.PI, 0.3f), (float)rng.NextDouble() * 360f);
            PlaceKitModel(root, Nat + "NAT_CampfireCold", At(a0 + Mathf.PI * 0.75f, 0.25f), (float)rng.NextDouble() * 360f);
            PlaceKitModel(root, Nat + "NAT_Driftwood", At(a0 + Mathf.PI * 1.4f, 0.75f), (float)rng.NextDouble() * 360f);
            PlaceKitModel(root, Nat + "NAT_CoastalShrub", At(a0 + 0.4f, 0.6f), (float)rng.NextDouble() * 360f);
            if (rng.NextDouble() < 0.5)
            {
                // 난파 보트: 물가(해안선 안쪽)에 얹고 바다 쪽으로 조금 기울인다
                float a = a0 + Mathf.PI * 0.5f;
                float r = Sample(s.Rim, a) * 0.86f;
                var boat = PlaceKitModel(root, Nat + "NAT_WreckBoat", new Vector3(Mathf.Cos(a) * r, SeaY + 0.35f, Mathf.Sin(a) * r), a * Mathf.Rad2Deg + 70f);
                if (boat != null) boat.transform.Rotate(Vector3.forward, 8f, Space.Self);
            }
        }

        /// <summary>
        /// 절차형 섬 장식(군도 · 개방해역): 무인도는 야자수 · 사람 흔적 · 유목 · 난파 보트, 바위섬은 바위 관목.
        /// 높이는 섬 판정에 위에서 광선을 쏴 잰다. 난수는 섬 자리로 따로 만든다(기존 섬 배치 난수를 건드리지 않게).
        /// </summary>
        public static void DressNature(IslandInfo info)
        {
            if (ArtDisabled || info == null || info.Root == null || info.Bank || info.MilitaryBase) return;
            var root = info.Root.transform;
            var rng = new System.Random(unchecked(Mathf.RoundToInt(info.Center.x * 13.7f) * 73856093 ^ Mathf.RoundToInt(info.Center.z * 7.3f) * 19349663));
            Physics.SyncTransforms();
            float Shore(float a) => info.ShoreAt != null ? info.ShoreAt(a) : info.Radius * 0.9f;
            bool Ground(float a, float frac, out Vector3 local)
            {
                local = default;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var w = root.TransformPoint(dir * Shore(a) * frac) + Vector3.up * 40f;
                if (!Physics.Raycast(w, Vector3.down, out var hit, 60f, Islands.Mask, QueryTriggerInteraction.Ignore)) return false;
                if (!hit.collider.transform.IsChildOf(root)) return false;
                local = root.InverseTransformPoint(hit.point);
                return local.y > SeaY + 0.4f;
            }
            float a0 = (float)rng.NextDouble() * Mathf.PI * 2f;
            if (info.Rocky)
            {
                if (rng.NextDouble() < 0.5 && Ground(a0, 0.45f, out var p)) PlaceKitModel(root, Nat + "NAT_RockShrub", p + Vector3.down * 0.25f, (float)rng.NextDouble() * 360f, Range(rng, 0.8f, 1.2f));
                return;
            }
            if (info.Art != null) return;   // Codex 모델 섬은 자체 장식이 있다
            if (rng.NextDouble() < 0.55 && Ground(a0, 0.4f, out var palm))
                PlaceKitModel(root, Nat + (rng.NextDouble() < 0.5 ? "NAT_Palm_A" : "NAT_Palm_B"), palm + Vector3.down * 0.1f, (float)rng.NextDouble() * 360f, Range(rng, 0.9f, 1.2f));
            if (rng.NextDouble() < 0.3 && Ground(a0 + 1.4f, 0.45f, out var tent))
            {
                PlaceKitModel(root, Nat + "NAT_CastawayTent", tent, (float)rng.NextDouble() * 360f);
                if (Ground(a0 + 1.75f, 0.42f, out var fire)) PlaceKitModel(root, Nat + "NAT_CampfireCold", fire, (float)rng.NextDouble() * 360f);
            }
            if (rng.NextDouble() < 0.5 && Ground(a0 + 2.6f, 0.82f, out var drift)) PlaceKitModel(root, Nat + "NAT_Driftwood", drift, (float)rng.NextDouble() * 360f);
            if (rng.NextDouble() < 0.4 && Ground(a0 + 3.6f, 0.6f, out var shrub)) PlaceKitModel(root, Nat + "NAT_CoastalShrub", shrub, (float)rng.NextDouble() * 360f);
            if (rng.NextDouble() < 0.15 && Ground(a0 + 4.4f, 0.9f, out var wreck))
                PlaceKitModel(root, Nat + "NAT_WreckBoat", wreck + Vector3.down * 0.15f, (a0 + 4.4f) * Mathf.Rad2Deg + 70f);
        }
    }
}

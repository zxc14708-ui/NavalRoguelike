using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// 수로 출구 적 군항 외형(2026-11-15, 2스테이지 보스 무대). SeaRegion 군항 좌표(만 가운데 t = 0, 안벽 t = HarborQuayOffset)에 세운다.
    ///   - 안벽 부지: 만 폭 그대로의 콘크리트 판(윗면 1.3m) + 안벽 가장자리 · 계선주. 뒤쪽은 기슭이 모여 언덕이 된다(IslandBuilder.BankShore)
    ///   - Codex 군항 시설(`Resources/Environment/HarborFacilityDetails` — 시작 화면 군항과 같은 프리팹)에서
    ///     화물 부두(돌제 · 컨테이너 크레인 · 창고 · 화물선 · 예인선)와 정비창(갠트리 크레인 · 정비동)을 0.3배로 떼어 와 왼쪽·오른쪽에 둔다
    ///   - 오른쪽 군함 부두(돌제) 끝에 등대, 부지 뒤에 레이더 기지(해안 소품)
    ///   - 판정: 부지 · 돌제마다 상자(Terrain 레이어) — 좌초 · 적 회피 · 사선 가림이 기슭과 같게. 섬 목록에는 기슭처럼(Bank) 열린 선으로 등록
    /// 군항 로컬 좌표: +z = 안벽에서 육지 쪽, −z = 만 쪽, x = 군항을 바라볼 때 오른쪽.
    /// </summary>
    public static class HarborBuilder
    {
        public const float FacilityScale = 0.3f;
        public const float DeckY = 1.3f;
        private const float YardDepth = 90f;

        /// <summary>군함 부두(오른쪽 돌제): 가운데 x · 길이 · 폭.</summary>
        public const float NavalPierX = 22f, NavalPierLength = 38f, NavalPierWidth = 8f;
        /// <summary>화물 부두 돌제가 놓이는 x(왼쪽).</summary>
        public const float CargoPierX = -58f;

        private static GameObject s_facility;
        private static readonly Dictionary<string, Material> s_mats = new();

        /// <summary>보스가 출항하는 자리(군항 로컬): 두 돌제 사이 안벽 앞.</summary>
        public static readonly Vector3 BossBerth = new(-16f, 0f, -16f);
        /// <summary>호위정 정박 자리(군항 로컬).</summary>
        public static readonly Vector3[] EscortBerths = { new(NavalPierX + 10f, 0f, -14f), new(NavalPierX - 10f, 0f, -26f), new(-38f, 0f, -30f) };
        /// <summary>안벽 양 끝 해안포대 자리(군항 로컬, 안벽에서 2.2m 바다 쪽 — CoastalBatteryField와 같은 규칙).</summary>
        public static Vector3[] BatterySpots => new[] { new Vector3(-(SeaRegion.HarborQuayHalfWidth - 16f), 0f, -2.2f), new Vector3(SeaRegion.HarborQuayHalfWidth - 16f, 0f, -2.2f) };

        /// <summary>군항 로컬 → 월드(+z = 육지 쪽 = 군항 방향, 군항 외형 루트와 같은 축).</summary>
        public static Vector3 ToWorld(Vector3 local)
            => SeaRegion.HarborPoint(SeaRegion.HarborQuayOffset + local.z, local.x) + Vector3.up * local.y;

        public static IslandInfo Build(Transform parent, int seed)
        {
            float hw = SeaRegion.HarborQuayHalfWidth;
            var root = new GameObject("Enemy harbor");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(SeaRegion.HarborPoint(SeaRegion.HarborQuayOffset, 0f), Quaternion.LookRotation(SeaRegion.HarborForward, Vector3.up));
            var rng = new System.Random(seed ^ 0x5eed);

            LoadFacility();
            var concrete = Mat("concrete", new Color(0.55f, 0.55f, 0.53f));
            var light = Mat("concrete_light", new Color(0.66f, 0.66f, 0.63f));
            var dark = Mat("steel_dark", new Color(0.22f, 0.23f, 0.25f));
            var yellow = Mat("safety_yellow", new Color(0.9f, 0.72f, 0.12f));
            var wall = Mat("wall_grey", new Color(0.45f, 0.46f, 0.47f));

            // ---- 안벽 부지(판정 겸용) · 안벽 가장자리
            Block(root.transform, "Harbor yard", new Vector3(0f, DeckY - 2.65f, YardDepth * 0.5f), new Vector3(2f * hw + 14f, 5.3f, YardDepth), concrete, collider: true);
            Block(root.transform, "Quay face", new Vector3(0f, DeckY - 1.6f, 0.35f), new Vector3(2f * hw + 14f, 3.4f, 0.7f), wall, collider: false);
            Block(root.transform, "Quay edge", new Vector3(0f, DeckY + 0.08f, 0.8f), new Vector3(2f * hw + 14f, 0.16f, 0.5f), yellow, collider: false);
            // 계선주
            for (float x = -hw + 6f; x <= hw - 6f; x += 11f)
                Block(root.transform, "Bollard", new Vector3(x, DeckY + 0.35f, 1.4f), new Vector3(0.6f, 0.7f, 0.6f), dark, collider: false);

            // ---- 군함 부두(오른쪽 돌제)
            Block(root.transform, "Naval pier", new Vector3(NavalPierX, DeckY - 2.4f, -NavalPierLength * 0.5f + 0.5f),
                             new Vector3(NavalPierWidth, 4.8f, NavalPierLength + 1f), light, collider: true);
            Block(root.transform, "Naval pier edge L", new Vector3(NavalPierX - NavalPierWidth * 0.5f + 0.25f, DeckY + 0.08f, -NavalPierLength * 0.5f), new Vector3(0.5f, 0.16f, NavalPierLength), yellow, collider: false);
            Block(root.transform, "Naval pier edge R", new Vector3(NavalPierX + NavalPierWidth * 0.5f - 0.25f, DeckY + 0.08f, -NavalPierLength * 0.5f), new Vector3(0.5f, 0.16f, NavalPierLength), yellow, collider: false);
            for (float z = -4f; z > -NavalPierLength; z -= 8f)
                for (int s = -1; s <= 1; s += 2)
                    Block(root.transform, "Bollard", new Vector3(NavalPierX + s * (NavalPierWidth * 0.5f - 0.7f), DeckY + 0.35f, z), new Vector3(0.6f, 0.7f, 0.6f), dark, collider: false);
            PlaceProp(root.transform, "ENV_Lighthouse", new Vector3(NavalPierX, DeckY, -NavalPierLength + 2.5f), 1f);

            // ---- Codex 군항 시설: 화물 부두(왼쪽) · 정비창(오른쪽 뒤)
            var cargo = Facility(root.transform, "CargoPort", new Vector3(CargoPierX, 0f, 0f), 180f, new Vector3(15f, 0f, 30f));
            if (cargo != null)
            {
                // 돌제 판정: 원래 좌표 x −15~45, z 30~180(만 쪽으로 뻗은 부분)
                Block(root.transform, "Cargo pier collider", new Vector3(CargoPierX, DeckY - 2.4f, -FacilityScale * 75f), new Vector3(FacilityScale * 60f, 4.8f, FacilityScale * 150f), null, collider: true);
                // 정박한 화물선 · 예인선도 부딪힌다(렌더러 범위로 상자)
                foreach (var hull in new[] { "CargoShip", "CargoSupportTug" })
                {
                    var t = cargo.transform.Find(hull);
                    if (t != null) BoundsCollider(root.transform, t, "Moored " + hull + " collider");
                }
            }
            Facility(root.transform, "Workshop", new Vector3(62f, 0f, 46f), 180f, Vector3.zero);
            // ---- 군사 구역: Codex 군사시설 모델(IslandBiomePack_Refined, 2026-11-09 — `Resources/Environment/Military`, 실제 크기 m).
            // 정면(+Z)이 만을 보게 180° 돌린다. 모델이 없으면 같은 군항 재질의 단순 건물로 대신한다.
            const float face = 180f;
            if (!Mil(root.transform, "MIL_CommandPost", 20f, 30f, face))
                Building(root.transform, new Vector3(20f, 0f, 30f), new Vector3(11f, 7f, 7.5f), Mat("wall_white", Color.white), Mat("glass_blue", Color.blue), Mat("roof_slate", Color.gray), 2);
            for (int i = 0; i < 3; i++)
                if (!Mil(root.transform, "MIL_Barracks", -2f + i * 13f, 58f, face))
                    Building(root.transform, new Vector3(-2f + i * 13f, 0f, 58f), new Vector3(9.4f, 4f, 6.4f), Mat("wall_white", Color.white), Mat("glass_blue", Color.blue), Mat("roof_slate", Color.gray), 1);
            Mil(root.transform, "MIL_Generator", 30f, 44f, face);
            Mil(root.transform, "MIL_Storage", -34f, 48f, 90f);
            Mil(root.transform, "MIL_Storage", -34f, 60f, 90f);
            if (!Mil(root.transform, "MIL_Radar", -20f, 64f, face)) PlaceProp(root.transform, "ENV_RadarStation", new Vector3(-20f, DeckY, 62f), 1.6f);
            Mil(root.transform, "MIL_CamoShelter", -26f, 75f, face);
            Mil(root.transform, "MIL_MissileTruck_2", -14f, 46f, face);
            Mil(root.transform, "MIL_MissileTruck_2", -8f, 46f, face);
            Mil(root.transform, "MIL_Watchtower", -40f, 6f, face);
            Mil(root.transform, "MIL_Watchtower", 74f, 6f, face);
            foreach (float x in new[] { -36f, -12f, 6f, 36f, 52f }) Mil(root.transform, "MIL_LightPole", x, 3f, face);
            Mil(root.transform, "MIL_SupplyCrates", NavalPierX - 8f, 4f, 0f);
            Mil(root.transform, "MIL_SupplyCrates", NavalPierX + 8f, 5f, 90f);
            // 뒤쪽 철조망 · 정문 · 위병소
            for (float x = -46f; x < 30f; x += 4f)
            {
                if (x >= -10f && x < -2f) continue;   // 정문 자리(−10 ~ −2)
                Mil(root.transform, "MIL_Fence_4m", x + 2f, 84f, 0f);
            }
            Mil(root.transform, "MIL_Gate_8m", -6f, 84f, 0f);
            Mil(root.transform, "MIL_GuardHut", -14f, 80f, face);
            Mil(root.transform, "MIL_Barrier", -2f, 78f, 0f);

            // 부지 위 장식: 컨테이너 줄(단순 상자) · 큰 연료 탱크
            var boxes = new[] { Mat("box_orange", new Color(0.85f, 0.45f, 0.15f)), Mat("box_blue", new Color(0.2f, 0.35f, 0.6f)), Mat("box_red", new Color(0.65f, 0.18f, 0.15f)), Mat("box_teal", new Color(0.15f, 0.5f, 0.5f)) };
            for (int col = 0; col < 3; col++)
                for (int row = 0; row < 4; row++)
                {
                    int tiers = rng.Next(4);
                    for (int k = 0; k < tiers; k++)
                        Block(root.transform, "Container", new Vector3(2f + col * 3f, DeckY + 0.65f + k * 1.3f, 9f + row * 7f), new Vector3(2.4f, 1.3f, 6f), boxes[rng.Next(boxes.Length)], collider: false);
                }
            for (int i = 0; i < 3; i++)
            {
                var tank = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tank.name = "Fuel tank";
                Object.Destroy(tank.GetComponent<Collider>());
                tank.transform.SetParent(root.transform, false);
                tank.transform.localPosition = new Vector3(-24f + i * 9f, DeckY + 3f, 30f);
                tank.transform.localScale = new Vector3(7f, 3f, 7f);
                tank.GetComponent<MeshRenderer>().sharedMaterial = light;
            }

            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Islands.Layer;

            // ---- 섬 목록 등록: 안벽 선(돌제를 돌아가는 열린 선) — 해안 물빛 · 레이더 · 섬 간격 판정
            var rim = new List<Vector2>();
            void R(float x, float z) { var w = root.transform.TransformPoint(new Vector3(x, 0f, z)); rim.Add(new Vector2(w.x, w.z)); }
            R(-hw - 7f, 0f);
            float cx0 = CargoPierX - FacilityScale * 30f, cx1 = CargoPierX + FacilityScale * 30f;
            R(cx0, 0f); R(cx0, -FacilityScale * 150f); R(cx1, -FacilityScale * 150f); R(cx1, 0f);
            float nx0 = NavalPierX - NavalPierWidth * 0.5f, nx1 = NavalPierX + NavalPierWidth * 0.5f;
            R(nx0, 0f); R(nx0, -NavalPierLength); R(nx1, -NavalPierLength); R(nx1, 0f);
            R(hw + 7f, 0f);
            var info = new IslandInfo
            {
                Center = root.transform.TransformPoint(new Vector3(0f, 0f, YardDepth * 0.5f)),
                Radius = hw, Height = 15f, Rocky = true, Rim = rim.ToArray(), Root = root, Bank = true,
            };
            Islands.Register(info);
            return info;
        }

        // ------------------------------------------------------------ 부품

        private static void LoadFacility()
        {
            if (s_facility == null) s_facility = Resources.Load<GameObject>("Environment/HarborFacilityDetails");
            if (s_facility == null || s_mats.Count > 0) return;
            foreach (var r in s_facility.GetComponentsInChildren<MeshRenderer>(true))
                if (r.sharedMaterial != null && !s_mats.ContainsKey(r.name)) s_mats[r.name] = r.sharedMaterial;
        }

        /// <summary>Codex 군항 재질(렌더러 이름 = 재질 종류). 없으면 단색 대체.</summary>
        private static Material Mat(string kind, Color fallback)
        {
            if (s_mats.TryGetValue(kind, out var m) && m != null) return m;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(shader) { name = "Harbor " + kind + " (Runtime)", color = fallback };
            s_mats[kind] = m;
            return m;
        }

        private static GameObject Block(Transform parent, string name, Vector3 center, Vector3 size, Material mat, bool collider)
        {
            var go = mat != null ? GameObject.CreatePrimitive(PrimitiveType.Cube) : new GameObject(name);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            if (mat != null)
            {
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
                if (!collider) Object.Destroy(go.GetComponent<Collider>());
            }
            else if (collider) go.AddComponent<BoxCollider>();
            return go;
        }

        /// <summary>
        /// Codex 시설 하나를 떼어 온다: 프리팹 안의 그 자식(재질별 병합 메시)을 복제해 0.3배, yaw만큼 돌려,
        /// 원래 좌표의 기준점(pivot)이 군항 로컬 at에 오게 둔다.
        /// </summary>
        private static GameObject Facility(Transform root, string child, Vector3 at, float yaw, Vector3 pivot, float scale = FacilityScale)
        {
            if (s_facility == null) return null;
            var src = s_facility.transform.Find(child);
            if (src == null) return null;
            var go = Object.Instantiate(src.gameObject, root, false);
            go.name = "Codex " + child;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localRotation = rot;
            go.transform.localScale = Vector3.one * scale;
            go.transform.localPosition = at - rot * (pivot * scale);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            return go;
        }

        private static readonly Dictionary<string, GameObject> s_mil = new();
        private static readonly Dictionary<string, Material> s_islandMats = new();

        /// <summary>
        /// Codex 군사시설 모델 하나를 부지 위(x, z)에 yaw만큼 돌려 둔다. 섬 재질 이름(MAT_Island…)은 게임의 섬 재질로 바꿔 끼운다
        /// (나머지는 FBX에 든 색 그대로). 없으면 false.
        /// </summary>
        private static bool Mil(Transform root, string name, float x, float z, float yaw)
        {
            if (!s_mil.TryGetValue(name, out var prefab)) s_mil[name] = prefab = Resources.Load<GameObject>($"Environment/Military/{name}");
            if (prefab == null) return false;
            var go = Object.Instantiate(prefab, root, false);
            go.name = name;
            go.transform.localPosition = new Vector3(x, DeckY, z);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * prefab.transform.localRotation;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string n = mats[i].name.Replace(" (Instance)", "");
                    if (!n.StartsWith("MAT_Island", System.StringComparison.Ordinal)) continue;
                    if (!s_islandMats.TryGetValue(n, out var m)) s_islandMats[n] = m = Resources.Load<Material>("Islands/" + n);
                    if (m != null) { mats[i] = m; changed = true; }
                }
                if (changed) r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            return true;
        }

        /// <summary>단순 건물: 벽 상자 + 층마다 창 띠(앞뒤) + 지붕 판.</summary>
        private static void Building(Transform root, Vector3 at, Vector3 size, Material wall, Material glass, Material roof, int floors)
        {
            Block(root, "Building", new Vector3(at.x, DeckY + size.y * 0.5f, at.z), size, wall, collider: false);
            float floorH = size.y / floors;
            for (int f = 0; f < floors; f++)
            {
                float y = DeckY + floorH * (f + 0.55f);
                Block(root, "Windows", new Vector3(at.x, y, at.z), new Vector3(size.x * 0.86f, floorH * 0.32f, size.z + 0.12f), glass, collider: false);
            }
            Block(root, "Roof", new Vector3(at.x, DeckY + size.y + 0.2f, at.z), new Vector3(size.x + 0.8f, 0.4f, size.z + 0.8f), roof, collider: false);
        }

        /// <summary>그 물체(자식 렌더러 전체)를 덮는 판정 상자를 군항 로컬 축으로 만든다.</summary>
        private static void BoundsCollider(Transform root, Transform target, string name)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var r in target.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var lb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = root.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            if (!any) return;
            var size = b.size;
            Block(root, name, new Vector3(b.center.x, DeckY - 2.4f, b.center.z), new Vector3(size.x, 4.8f, size.z), null, collider: true);
        }

        private static void PlaceProp(Transform root, string name, Vector3 local, float scale)
        {
            var prefab = Resources.Load<GameObject>($"Environment/Props/{name}");
            if (prefab == null) return;
            var go = Object.Instantiate(prefab, root, false);
            go.name = name;
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale *= scale;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
        }
    }
}

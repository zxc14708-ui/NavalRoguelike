using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 해군기지 모델 키트(2026-10-09). 모든 치수는 실제 m, 각 모델은 자기 그룹의 지면(y = 0)을 기준으로 짓는다.
    /// 시작 화면 전체 시점에서 1m ≈ 1.6px이라 작은 장식보다 지붕·바닥 색 대비, 실루엣, 색 있는 차량·컨테이너로 밀도를 낸다.
    /// </summary>
    internal static partial class NavalBaseBuilder
    {
        // ================================================================ 색(대비를 키운 팔레트)

        private static readonly Dictionary<string, Color> Palette = new()
        {
            // 바다·땅
            ["water"] = new Color(0.27f, 0.52f, 0.68f), ["water_shallow"] = new Color(0.42f, 0.7f, 0.78f),
            ["grass"] = new Color(0.45f, 0.6f, 0.36f), ["grass_light"] = new Color(0.56f, 0.69f, 0.43f),
            ["grass_dark"] = new Color(0.33f, 0.48f, 0.28f), ["sand"] = new Color(0.9f, 0.84f, 0.68f),
            ["sand_wet"] = new Color(0.4f, 0.42f, 0.4f), ["dirt"] = new Color(0.55f, 0.47f, 0.36f),
            // 포장
            ["road"] = new Color(0.22f, 0.24f, 0.27f), ["runway"] = new Color(0.27f, 0.29f, 0.32f),
            ["concrete"] = new Color(0.66f, 0.68f, 0.68f), ["concrete_light"] = new Color(0.79f, 0.8f, 0.79f),
            ["apron"] = new Color(0.6f, 0.62f, 0.62f), ["sidewalk"] = new Color(0.74f, 0.73f, 0.69f),
            ["plinth"] = new Color(0.36f, 0.37f, 0.38f), ["quay_face"] = new Color(0.42f, 0.42f, 0.4f),
            ["mark_white"] = new Color(0.96f, 0.96f, 0.94f), ["mark_yellow"] = new Color(0.95f, 0.75f, 0.15f),
            ["mark_red"] = new Color(0.8f, 0.2f, 0.16f), ["track"] = new Color(0.72f, 0.33f, 0.24f),
            ["court_blue"] = new Color(0.22f, 0.42f, 0.62f), ["court_green"] = new Color(0.25f, 0.5f, 0.38f),
            // 건물 벽
            ["wall_white"] = new Color(0.9f, 0.9f, 0.87f), ["wall_beige"] = new Color(0.85f, 0.78f, 0.64f),
            ["wall_brick"] = new Color(0.66f, 0.36f, 0.27f), ["wall_grey"] = new Color(0.62f, 0.65f, 0.68f),
            ["wall_blue"] = new Color(0.47f, 0.58f, 0.7f), ["wall_sand"] = new Color(0.78f, 0.72f, 0.58f),
            ["wall_green"] = new Color(0.46f, 0.55f, 0.45f),
            // 지붕
            ["roof_slate"] = new Color(0.24f, 0.28f, 0.33f), ["roof_green"] = new Color(0.22f, 0.4f, 0.32f),
            ["roof_red"] = new Color(0.62f, 0.27f, 0.2f), ["roof_blue"] = new Color(0.2f, 0.33f, 0.5f),
            ["roof_metal"] = new Color(0.55f, 0.6f, 0.64f), ["roof_white"] = new Color(0.88f, 0.89f, 0.88f),
            // 재료
            ["glass"] = new Color(0.12f, 0.18f, 0.24f), ["glass_blue"] = new Color(0.2f, 0.36f, 0.5f),
            ["trim_white"] = new Color(0.95f, 0.95f, 0.93f), ["steel"] = new Color(0.55f, 0.58f, 0.6f),
            ["steel_dark"] = new Color(0.22f, 0.24f, 0.26f), ["door_dark"] = new Color(0.16f, 0.17f, 0.18f),
            ["hangar_door"] = new Color(0.78f, 0.8f, 0.8f), ["hangar_band"] = new Color(0.25f, 0.36f, 0.5f),
            ["safety_yellow"] = new Color(0.95f, 0.72f, 0.12f), ["safety_red"] = new Color(0.78f, 0.16f, 0.13f),
            ["crane_red"] = new Color(0.78f, 0.2f, 0.15f), ["crane_white"] = new Color(0.93f, 0.93f, 0.9f),
            ["rubber"] = new Color(0.08f, 0.08f, 0.09f), ["pile"] = new Color(0.25f, 0.27f, 0.29f),
            ["bollard"] = new Color(0.16f, 0.17f, 0.19f), ["rope"] = new Color(0.85f, 0.8f, 0.64f),
            ["gangway"] = new Color(0.62f, 0.65f, 0.68f), ["tank_white"] = new Color(0.92f, 0.93f, 0.92f),
            ["tank_band"] = new Color(0.2f, 0.36f, 0.56f), ["dome"] = new Color(0.95f, 0.95f, 0.94f),
            ["lamp"] = new Color(1f, 0.86f, 0.62f), ["beacon_red"] = new Color(1f, 0.2f, 0.12f),
            ["flag_navy"] = new Color(0.1f, 0.2f, 0.45f), ["flag_white"] = new Color(0.96f, 0.96f, 0.96f),
            ["wood"] = new Color(0.45f, 0.32f, 0.2f), ["gravel"] = new Color(0.56f, 0.54f, 0.5f),
            ["solar"] = new Color(0.1f, 0.16f, 0.32f),
            // 나무
            ["tree_dark"] = new Color(0.18f, 0.34f, 0.2f), ["tree_mid"] = new Color(0.27f, 0.45f, 0.24f),
            ["tree_light"] = new Color(0.4f, 0.55f, 0.28f), ["trunk"] = new Color(0.36f, 0.27f, 0.19f),
            // 컨테이너·차량
            ["box_red"] = new Color(0.7f, 0.2f, 0.16f), ["box_blue"] = new Color(0.17f, 0.33f, 0.58f),
            ["box_orange"] = new Color(0.92f, 0.5f, 0.12f), ["box_green"] = new Color(0.22f, 0.45f, 0.3f),
            ["box_white"] = new Color(0.88f, 0.88f, 0.86f), ["box_teal"] = new Color(0.12f, 0.5f, 0.52f),
            ["car_white"] = new Color(0.93f, 0.93f, 0.92f), ["car_black"] = new Color(0.1f, 0.1f, 0.11f),
            ["car_silver"] = new Color(0.66f, 0.68f, 0.7f), ["car_red"] = new Color(0.7f, 0.12f, 0.12f),
            ["car_blue"] = new Color(0.16f, 0.28f, 0.55f), ["olive"] = new Color(0.36f, 0.42f, 0.28f),
            ["olive_dark"] = new Color(0.28f, 0.33f, 0.22f), ["bus_white"] = new Color(0.9f, 0.9f, 0.88f),
            // 함정·항공기
            ["hull_black"] = new Color(0.13f, 0.14f, 0.15f), ["hull_red"] = new Color(0.62f, 0.16f, 0.13f),
            ["hull_grey"] = new Color(0.45f, 0.48f, 0.5f), ["deck_tan"] = new Color(0.6f, 0.55f, 0.45f),
            ["dock_grey"] = new Color(0.38f, 0.4f, 0.42f),
            ["aircraft"] = new Color(0.84f, 0.86f, 0.88f), ["aircraft_grey"] = new Color(0.6f, 0.64f, 0.68f),
            ["aircraft_fighter"] = new Color(0.5f, 0.54f, 0.58f), ["aircraft_dark"] = new Color(0.25f, 0.28f, 0.31f),
            ["cockpit"] = new Color(0.1f, 0.14f, 0.2f), ["prop_disc"] = new Color(0.32f, 0.33f, 0.35f),
            ["rotor"] = new Color(0.16f, 0.17f, 0.18f),
        };

        private static Transform _hullMeshHolder;
        private static Mesh _smallHull;

        // ================================================================ 기본 조각

        /// <summary>회전된 그룹(지면 y = 0).</summary>
        private static Transform G(Transform parent, string name, Vector3 at, float yaw = 0f)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = at;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }

        /// <summary>상자: c = 중심, s = 크기(m).</summary>
        private static GameObject B(Transform p, Vector3 c, Vector3 s, string mat, float yaw = 0f, float pitch = 0f, float roll = 0f)
        {
            var go = Box("Part", p, c, s, M(mat));
            if (yaw != 0f || pitch != 0f || roll != 0f) go.transform.localRotation = Quaternion.Euler(pitch, yaw, roll);
            return go;
        }

        /// <summary>원기둥: s = (지름 x, 높이, 지름 z). 세운 원통. pitch 90이면 Z축으로 눕는다.</summary>
        private static GameObject C(Transform p, Vector3 c, Vector3 s, string mat, float pitch = 0f, float yaw = 0f, float roll = 0f)
        {
            var go = Cyl("Part", p, c, new Vector3(s.x, s.y * 0.5f, s.z), M(mat));
            if (pitch != 0f || yaw != 0f || roll != 0f) go.transform.localRotation = Quaternion.Euler(pitch, yaw, roll);
            return go;
        }

        private static GameObject S(Transform p, Vector3 c, Vector3 s, string mat) => Sph("Part", p, c, s, M(mat));

        /// <summary>캡슐: 길이 len(Z축으로 눕힘), 지름 dia.</summary>
        private static GameObject Tube(Transform p, Vector3 c, float len, float dia, string mat, float yaw = 0f, float pitch = 0f)
        {
            var go = Cap("Part", p, c, new Vector3(dia, len * 0.5f, dia), M(mat));
            go.transform.localRotation = Quaternion.Euler(90f + pitch, yaw, 0f);
            return go;
        }

        /// <summary>박공지붕(맞배): 밑면 중심 c, 경사 방향 폭 w(로컬 X), 용마루 방향 길이 d(Z), 높이 rise.</summary>
        private static void Gable(Transform p, Vector3 c, float w, float d, float rise, string mat, float overhang = 1f)
        {
            var pivot = new GameObject("Roof").transform;
            pivot.SetParent(p, false);
            pivot.localPosition = c;
            float half = (w + overhang * 2f) * 0.5f;
            pivot.localScale = new Vector3(1f, rise / half, 1f);
            float side = half * Mathf.Sqrt(2f);
            var prism = Box("Roof", pivot, Vector3.zero, new Vector3(side, side, d + overhang * 2f), M(mat));
            prism.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        private static void Beam(Transform p, Vector3 a, Vector3 b, float t, string mat) => Line("Beam", p, a, b, t, M(mat));

        // ================================================================ 건물

        /// <summary>평지붕 사무동: 층마다 창 띠, 바닥 띠, 옥상 설비, 현관 캐노피. 정면 = -Z.</summary>
        private static Transform OfficeBlock(Transform parent, Vector3 at, float yaw, float w, float d, int floors,
                                             string wall, string roof, string name = "Office", string trim = "trim_white")
        {
            var t = G(parent, name, at, yaw);
            const float fh = 3.8f;
            float h = floors * fh + 0.8f;
            B(t, new Vector3(0f, 0.4f, 0f), new Vector3(w + 1.6f, 0.8f, d + 1.6f), "plinth");
            B(t, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), wall);
            for (int f = 0; f < floors; f++)
            {
                float y = 2.2f + f * fh;
                foreach (float s in new[] { -1f, 1f })
                {
                    B(t, new Vector3(0f, y, s * (d * 0.5f + 0.06f)), new Vector3(w - 3f, 1.5f, 0.15f), "glass");
                    B(t, new Vector3(s * (w * 0.5f + 0.06f), y, 0f), new Vector3(0.15f, 1.5f, d - 3f), "glass");
                }
                if (f > 0) B(t, new Vector3(0f, f * fh + 0.4f, 0f), new Vector3(w + 0.3f, 0.35f, d + 0.3f), trim);
            }
            B(t, new Vector3(0f, h + 0.35f, 0f), new Vector3(w + 0.8f, 0.7f, d + 0.8f), roof);
            B(t, new Vector3(-w * 0.22f, h + 1.6f, d * 0.1f), new Vector3(Mathf.Min(8f, w * 0.2f), 2.2f, Mathf.Min(6f, d * 0.35f)), "steel");
            B(t, new Vector3(w * 0.25f, h + 1.2f, -d * 0.1f), new Vector3(4f, 1.4f, 4f), "steel");
            B(t, new Vector3(w * 0.3f, h + 2.2f, d * 0.15f), new Vector3(5f, 3f, 5f), wall);   // 계단실
            B(t, new Vector3(0f, 3.4f, -d * 0.5f - 2.2f), new Vector3(Mathf.Min(10f, w * 0.4f), 0.4f, 4.4f), roof);
            B(t, new Vector3(0f, 1.5f, -d * 0.5f - 0.1f), new Vector3(3f, 2.6f, 0.3f), "door_dark");
            return t;
        }

        /// <summary>숙소동(BEQ): 발코니 띠 + 박공지붕.</summary>
        private static Transform Barracks(Transform parent, Vector3 at, float yaw, float w, float d, int floors, string wall, string roof)
        {
            var t = G(parent, "Barracks", at, yaw);
            const float fh = 3.4f;
            float h = floors * fh + 0.6f;
            B(t, new Vector3(0f, 0.4f, 0f), new Vector3(w + 1.6f, 0.8f, d + 1.6f), "plinth");
            B(t, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), wall);
            for (int f = 0; f < floors; f++)
            {
                float y = 2f + f * fh;
                B(t, new Vector3(0f, y, d * 0.5f + 0.06f), new Vector3(w - 4f, 1.3f, 0.15f), "glass");
                // 정면 발코니(복도) 띠
                B(t, new Vector3(0f, f * fh + 0.9f, -d * 0.5f - 0.9f), new Vector3(w - 2f, 0.3f, 1.8f), "trim_white");
                B(t, new Vector3(0f, f * fh + 1.6f, -d * 0.5f - 1.75f), new Vector3(w - 2f, 1f, 0.12f), "trim_white");
                for (float x = -w * 0.5f + 4f; x < w * 0.5f - 2f; x += 4f)
                    B(t, new Vector3(x, y + 0.2f, -d * 0.5f - 0.06f), new Vector3(1.6f, 1.8f, 0.12f), "glass");
            }
            Gable(t, new Vector3(0f, h, 0f), d, w, d * 0.22f, roof);
            t.Find("Roof").localRotation = Quaternion.Euler(0f, 90f, 0f);
            foreach (float x in new[] { -w * 0.5f - 2f, w * 0.5f + 2f })
                B(t, new Vector3(x, h * 0.5f, 0f), new Vector3(4f, h, 6f), wall);   // 양끝 계단실
            return t;
        }

        /// <summary>사령부: 5층 중앙동 + 유리 중앙 탑 + 뒤로 꺾인 양 날개 + 열주 현관 + 국기 게양대 셋.</summary>
        private static Transform Headquarters(Transform parent, Vector3 at, float yaw)
        {
            var t = G(parent, "Headquarters", at, yaw);
            OfficeBlock(t, new Vector3(0f, 0f, 0f), 0f, 86f, 22f, 5, "wall_white", "roof_slate", "Main block");
            OfficeBlock(t, new Vector3(-50f, 0f, 24f), 0f, 22f, 56f, 3, "wall_white", "roof_slate", "West wing");
            OfficeBlock(t, new Vector3(50f, 0f, 24f), 0f, 22f, 56f, 3, "wall_white", "roof_slate", "East wing");
            // 유리 중앙 탑
            B(t, new Vector3(0f, 14f, -1f), new Vector3(20f, 28f, 25f), "glass_blue");
            B(t, new Vector3(0f, 28.4f, -1f), new Vector3(22f, 0.8f, 27f), "roof_slate");
            C(t, new Vector3(6f, 36f, 4f), new Vector3(0.8f, 14f, 0.8f), "steel");
            B(t, new Vector3(6f, 40f, 4f), new Vector3(8f, 0.4f, 0.4f), "steel");
            S(t, new Vector3(-5f, 30.5f, 6f), new Vector3(5f, 5f, 5f), "dome");
            // 열주 현관
            B(t, new Vector3(0f, 9f, -16f), new Vector3(30f, 0.8f, 9f), "trim_white");
            for (int i = 0; i < 6; i++) C(t, new Vector3(-12.5f + i * 5f, 4.4f, -19.5f), new Vector3(1.2f, 8.8f, 1.2f), "trim_white");
            for (int i = 0; i < 4; i++) B(t, new Vector3(0f, 0.2f + i * 0.2f, -22f - i * 1.4f + 4f), new Vector3(30f - i * 2f, 0.4f, 3f), "sidewalk");
            // 광장·연병장·국기
            B(t, new Vector3(0f, 0.15f, -55f), new Vector3(120f, 0.3f, 56f), "concrete_light");
            C(t, new Vector3(0f, 0.35f, -55f), new Vector3(26f, 0.1f, 26f), "plinth");
            C(t, new Vector3(0f, 0.45f, -55f), new Vector3(22f, 0.1f, 22f), "grass_light");
            for (int i = 0; i < 3; i++)
            {
                float x = -12f + i * 12f;
                C(t, new Vector3(x, 9f, -36f), new Vector3(0.5f, 18f, 0.5f), "trim_white");
                B(t, new Vector3(x + 2.6f, 16.4f, -36f), new Vector3(5f, 3.2f, 0.15f), i == 1 ? "flag_navy" : "flag_white");
            }
            for (int i = 0; i < 2; i++)
                B(t, new Vector3(i == 0 ? -40f : 40f, 0.5f, -55f), new Vector3(24f, 0.6f, 40f), "grass_light");
            return t;
        }

        /// <summary>창고: 낮은 박공, 앞면 셔터·하역장, 지붕 채광 띠. 정면 = -Z.</summary>
        private static Transform Warehouse(Transform parent, Vector3 at, float yaw, float w, float d, float h,
                                           string wall = "wall_sand", string roof = "roof_metal", string name = "Warehouse")
        {
            var t = G(parent, name, at, yaw);
            B(t, new Vector3(0f, 0.4f, 0f), new Vector3(w + 1.2f, 0.8f, d + 1.2f), "plinth");
            B(t, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), wall);
            Gable(t, new Vector3(0f, h, 0f), d, w, d * 0.1f, roof);
            t.Find("Roof").localRotation = Quaternion.Euler(0f, 90f, 0f);
            for (float x = -w * 0.5f + 8f; x < w * 0.5f - 4f; x += 12f)
            {
                B(t, new Vector3(x, h * 0.32f, -d * 0.5f - 0.1f), new Vector3(6f, h * 0.6f, 0.3f), "door_dark");
                B(t, new Vector3(x, h * 0.64f, -d * 0.5f - 0.2f), new Vector3(7f, 0.4f, 0.3f), "safety_yellow");
            }
            B(t, new Vector3(0f, 0.7f, -d * 0.5f - 2.5f), new Vector3(w - 4f, 1.4f, 5f), "concrete");   // 하역장
            for (int i = -1; i <= 1; i += 2)
                B(t, new Vector3(i * w * 0.2f, h + d * 0.05f + 0.25f, -d * 0.18f), new Vector3(w * 0.3f, 0.3f, d * 0.12f), "roof_white");
            return t;
        }

        /// <summary>함정 정비창: 높은 공장동 + 측창 띠 + 끝면 대형 문(황흑 띠) + 부속 사무동 + 야외 갠트리 크레인. 정면(문) = +X.</summary>
        private static Transform RepairShop(Transform parent, Vector3 at, float yaw)
        {
            var t = G(parent, "ShipRepairShop", at, yaw);
            const float w = 110f, d = 54f, h = 26f;
            B(t, new Vector3(0f, 0.4f, 0f), new Vector3(w + 2f, 0.8f, d + 2f), "plinth");
            B(t, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), "wall_blue");
            Gable(t, new Vector3(0f, h, 0f), d, w, 6f, "roof_slate");
            t.Find("Roof").localRotation = Quaternion.Euler(0f, 90f, 0f);
            foreach (float s in new[] { -1f, 1f })
            {
                B(t, new Vector3(0f, h - 4f, s * (d * 0.5f + 0.06f)), new Vector3(w - 6f, 3f, 0.15f), "glass");
                for (float x = -w * 0.5f + 6f; x < w * 0.5f; x += 12f)
                    B(t, new Vector3(x, h * 0.5f, s * (d * 0.5f + 0.4f)), new Vector3(0.8f, h, 0.8f), "steel");
            }
            B(t, new Vector3(w * 0.5f + 0.15f, 10f, 0f), new Vector3(0.3f, 20f, 30f), "door_dark");
            for (int i = 0; i < 6; i++)
                B(t, new Vector3(w * 0.5f + 0.35f, 20.6f, -13.75f + i * 5.5f), new Vector3(0.2f, 1.2f, 2.75f), i % 2 == 0 ? "safety_yellow" : "door_dark");
            OfficeBlock(t, new Vector3(-10f, 0f, -d * 0.5f - 9f), 0f, 40f, 14f, 2, "wall_white", "roof_slate", "Shop office");
            // 야외 갠트리 크레인(문 앞 작업장)
            B(t, new Vector3(w * 0.5f + 30f, 0.15f, 0f), new Vector3(56f, 0.3f, 50f), "concrete");
            foreach (float z in new[] { -22f, 22f })
            {
                Beam(t, new Vector3(w * 0.5f + 10f, 0f, z), new Vector3(w * 0.5f + 14f, 22f, z), 1.6f, "safety_yellow");
                Beam(t, new Vector3(w * 0.5f + 18f, 0f, z), new Vector3(w * 0.5f + 14f, 22f, z), 1.6f, "safety_yellow");
                B(t, new Vector3(w * 0.5f + 30f, 0.3f, z), new Vector3(56f, 0.5f, 0.8f), "steel_dark");   // 레일
            }
            B(t, new Vector3(w * 0.5f + 14f, 23f, 0f), new Vector3(3f, 2.4f, 50f), "safety_yellow");
            B(t, new Vector3(w * 0.5f + 14f, 21f, 6f), new Vector3(4f, 2.5f, 4f), "steel_dark");
            Beam(t, new Vector3(w * 0.5f + 14f, 20f, 6f), new Vector3(w * 0.5f + 14f, 8f, 6f), 0.3f, "steel_dark");
            B(t, new Vector3(w * 0.5f + 34f, 2f, -8f), new Vector3(16f, 4f, 6f), "hull_grey");   // 정비 중인 부품
            B(t, new Vector3(w * 0.5f + 40f, 1.5f, 12f), new Vector3(8f, 3f, 8f), "box_orange");
            return t;
        }

        /// <summary>정비 격납고: 넓은 문 판넬(밝은 판·파란 띠), 낮은 박공, 양옆 부속동, 문 위 번호판. 문 = -Z.</summary>
        private static Transform MaintenanceHangar(Transform parent, Vector3 at, float yaw, bool open, string roof = "roof_blue")
        {
            var t = G(parent, "MaintenanceHangar", at, yaw);
            const float w = 84f, d = 66f, h = 22f;
            B(t, new Vector3(0f, 0.4f, 0f), new Vector3(w + 26f, 0.8f, d + 2f), "plinth");
            B(t, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), "wall_grey");
            Gable(t, new Vector3(0f, h, 0f), w, d, 5f, roof);
            for (int i = 0; i < 6; i++)
            {
                float x = -w * 0.5f + 7f + i * 14f;
                bool gap = open && (i == 2 || i == 3);
                B(t, new Vector3(x, h * 0.42f, -d * 0.5f - 0.4f), new Vector3(13.6f, h * 0.84f, 0.5f), gap ? "door_dark" : "hangar_door");
                if (!gap) B(t, new Vector3(x, h * 0.62f, -d * 0.5f - 0.7f), new Vector3(13.6f, 1.6f, 0.2f), "hangar_band");
            }
            B(t, new Vector3(0f, h * 0.9f, -d * 0.5f - 0.6f), new Vector3(w + 1f, h * 0.2f, 1f), "wall_grey");
            B(t, new Vector3(0f, h * 0.9f, -d * 0.5f - 1.2f), new Vector3(16f, 3.4f, 0.3f), "hangar_band");
            foreach (float s in new[] { -1f, 1f })
            {
                B(t, new Vector3(s * (w * 0.5f + 6f), 4.5f, 2f), new Vector3(12f, 9f, d - 6f), "wall_white");
                B(t, new Vector3(s * (w * 0.5f + 6f), 9.3f, 2f), new Vector3(13f, 0.6f, d - 5f), "roof_slate");
                B(t, new Vector3(s * (w * 0.5f + 12.06f), 5f, 2f), new Vector3(0.15f, 1.6f, d - 10f), "glass");
            }
            for (int i = -2; i <= 2; i++) C(t, new Vector3(i * 14f, h + 4.5f, 10f), new Vector3(2.4f, 2f, 2.4f), "steel");
            return t;
        }

        /// <summary>반원 아치 격납고(또는 강화 엄체호): 원통 절반이 땅 위로. 문 = -Z.</summary>
        private static Transform ArchHangar(Transform parent, Vector3 at, float yaw, float span, float depth, string shell, string front, string name = "ArchHangar")
        {
            var t = G(parent, name, at, yaw);
            C(t, new Vector3(0f, 0f, 0f), new Vector3(span, depth, span * 0.7f), shell, 90f);
            B(t, new Vector3(0f, span * 0.16f, -depth * 0.5f - 0.3f), new Vector3(span * 0.92f, span * 0.32f, 0.6f), front);
            B(t, new Vector3(0f, span * 0.15f, -depth * 0.5f - 0.7f), new Vector3(span * 0.6f, span * 0.28f, 0.3f), "door_dark");
            B(t, new Vector3(0f, 0.15f, -depth * 0.5f - 12f), new Vector3(span * 0.8f, 0.3f, 24f), "concrete");
            return t;
        }

        /// <summary>관제탑: 운항동 + 사각 탑 + 팔각 유리 관제실 + 통로 + 안테나·경광등.</summary>
        private static Transform ControlTower(Transform parent, Vector3 at, float yaw)
        {
            var t = G(parent, "ControlTower", at, yaw);
            OfficeBlock(t, new Vector3(-14f, 0f, 0f), 0f, 34f, 18f, 2, "wall_white", "roof_slate", "Base operations");
            B(t, new Vector3(8f, 15f, 0f), new Vector3(9f, 30f, 9f), "wall_white");
            B(t, new Vector3(8f, 15f, -4.56f), new Vector3(1.6f, 26f, 0.15f), "glass");
            C(t, new Vector3(8f, 30.6f, 0f), new Vector3(16f, 1.2f, 16f), "roof_slate");
            C(t, new Vector3(8f, 33.4f, 0f), new Vector3(14f, 4.6f, 14f), "glass_blue");
            C(t, new Vector3(8f, 36.1f, 0f), new Vector3(16f, 0.8f, 16f), "roof_slate");
            C(t, new Vector3(8f, 30.8f, 0f), new Vector3(18f, 0.3f, 18f), "steel");   // 통로
            C(t, new Vector3(8f, 40f, 0f), new Vector3(0.5f, 7f, 0.5f), "steel");
            S(t, new Vector3(8f, 43.6f, 0f), new Vector3(1.2f, 1.2f, 1.2f), "beacon_red");
            B(t, new Vector3(5f, 37.6f, 3f), new Vector3(3f, 2f, 0.3f), "steel");
            return t;
        }

        /// <summary>비행장 소방대: 붉은 차고문 넷 + 호스 건조탑 + 소방차.</summary>
        private static Transform FireStation(Transform parent, Vector3 at, float yaw)
        {
            var t = G(parent, "CrashFireStation", at, yaw);
            B(t, new Vector3(0f, 0.4f, 0f), new Vector3(46f, 0.8f, 24f), "plinth");
            B(t, new Vector3(0f, 4.5f, 0f), new Vector3(44f, 9f, 22f), "wall_white");
            B(t, new Vector3(0f, 9.3f, 0f), new Vector3(45f, 0.6f, 23f), "roof_red");
            for (int i = 0; i < 4; i++) B(t, new Vector3(-15f + i * 10f, 3.2f, -11.1f), new Vector3(7.5f, 6f, 0.3f), "safety_red");
            B(t, new Vector3(25f, 9f, 6f), new Vector3(6f, 18f, 6f), "wall_white");
            B(t, new Vector3(25f, 18.3f, 6f), new Vector3(6.6f, 0.6f, 6.6f), "safety_red");
            for (int i = 0; i < 3; i++) Vehicle(t, new Vector3(-15f + i * 10f, 0f, -20f), 0f, VehicleKind.FireTruck);
            return t;
        }

        /// <summary>감시 레이더(ASR): 격자 탑 + 회전 반사판 + 장비 쉘터.</summary>
        private static Transform SearchRadar(Transform parent, Vector3 at, float yaw)
        {
            var t = G(parent, "SearchRadar", at, yaw);
            const float top = 20f;
            foreach (float x in new[] { -4f, 4f })
            foreach (float z in new[] { -4f, 4f })
                Beam(t, new Vector3(x, 0f, z), new Vector3(x * 0.4f, top, z * 0.4f), 0.6f, "crane_white");
            for (float y = 5f; y < top; y += 5f)
            {
                float k = 1f - 0.6f * y / top;
                B(t, new Vector3(0f, y, -4f * k), new Vector3(8f * k, 0.4f, 0.4f), "crane_red");
                B(t, new Vector3(0f, y, 4f * k), new Vector3(8f * k, 0.4f, 0.4f), "crane_red");
            }
            B(t, new Vector3(0f, top + 0.3f, 0f), new Vector3(6f, 0.6f, 6f), "steel");
            B(t, new Vector3(0f, top + 3.6f, 0.6f), new Vector3(14f, 5f, 0.6f), "crane_white", 0f, -12f);
            B(t, new Vector3(0f, top + 1.2f, -1.6f), new Vector3(1f, 1.4f, 3f), "steel_dark");
            B(t, new Vector3(-14f, 2.2f, 0f), new Vector3(12f, 4.4f, 8f), "wall_white");
            B(t, new Vector3(-14f, 4.6f, 0f), new Vector3(12.6f, 0.4f, 8.6f), "roof_slate");
            return t;
        }

        /// <summary>레이더돔(장거리 감시): 원통 받침 + 흰 돔 + 장비동.</summary>
        private static Transform RadomeSite(Transform parent, Vector3 at, float yaw)
        {
            var t = G(parent, "RadomeSite", at, yaw);
            B(t, new Vector3(0f, 0.15f, 0f), new Vector3(60f, 0.3f, 50f), "concrete");
            C(t, new Vector3(-8f, 7f, 0f), new Vector3(14f, 14f, 14f), "wall_white");
            S(t, new Vector3(-8f, 21f, 0f), new Vector3(20f, 20f, 20f), "dome");
            C(t, new Vector3(-8f, 14.2f, 0f), new Vector3(15f, 0.6f, 15f), "tank_band");
            OfficeBlock(t, new Vector3(14f, 0f, 8f), 0f, 20f, 12f, 1, "wall_white", "roof_slate", "Radar ops");
            C(t, new Vector3(16f, 11f, -12f), new Vector3(0.6f, 22f, 0.6f), "steel");
            S(t, new Vector3(12f, 3f, -14f), new Vector3(6f, 6f, 2f), "dome");   // 위성 안테나
            return t;
        }

        private static Transform WaterTower(Transform parent, Vector3 at)
        {
            var t = G(parent, "WaterTower", at);
            foreach (float x in new[] { -4f, 4f })
            foreach (float z in new[] { -4f, 4f })
                Beam(t, new Vector3(x, 0f, z), new Vector3(x * 0.6f, 26f, z * 0.6f), 0.7f, "steel");
            S(t, new Vector3(0f, 31f, 0f), new Vector3(14f, 11f, 14f), "tank_white");
            C(t, new Vector3(0f, 31f, 0f), new Vector3(14.2f, 2f, 14.2f), "tank_band");
            return t;
        }

        /// <summary>유류 저장소: 방유제 + 흰 탱크 + 배관 + 펌프실.</summary>
        private static Transform TankFarm(Transform parent, Vector3 at, float yaw, int cols, int rows, float dia = 22f)
        {
            var t = G(parent, "TankFarm", at, yaw);
            float pitch = dia + 10f;
            float w = cols * pitch + 10f, d = rows * pitch + 10f;
            B(t, new Vector3(0f, 0.1f, 0f), new Vector3(w, 0.2f, d), "dirt");
            foreach (float s in new[] { -1f, 1f })
            {
                B(t, new Vector3(0f, 1.2f, s * d * 0.5f), new Vector3(w, 2.4f, 1.2f), "concrete");
                B(t, new Vector3(s * w * 0.5f, 1.2f, 0f), new Vector3(1.2f, 2.4f, d), "concrete");
            }
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var p = new Vector3(-w * 0.5f + 5f + pitch * (c + 0.5f), 0f, -d * 0.5f + 5f + pitch * (r + 0.5f));
                C(t, p + Vector3.up * 7f, new Vector3(dia, 14f, dia), "tank_white");
                C(t, p + Vector3.up * 14.2f, new Vector3(dia * 0.98f, 0.4f, dia * 0.98f), "steel");
                C(t, p + Vector3.up * 10f, new Vector3(dia + 0.1f, 1.2f, dia + 0.1f), "tank_band");
                Beam(t, p + new Vector3(0f, 1f, 0f), new Vector3(p.x, 1f, -d * 0.5f - 6f), 0.8f, "steel_dark");
            }
            B(t, new Vector3(-w * 0.5f + 8f, 3f, -d * 0.5f - 10f), new Vector3(12f, 6f, 8f), "wall_white");
            B(t, new Vector3(-w * 0.5f + 8f, 6.3f, -d * 0.5f - 10f), new Vector3(12.6f, 0.6f, 8.6f), "roof_red");
            B(t, new Vector3(0f, 1f, -d * 0.5f - 6f), new Vector3(w, 0.8f, 0.8f), "steel_dark");
            return t;
        }

        /// <summary>흙 덮은 탄약고(이글루): 잔디 둔덕 + 콘크리트 전면벽 + 철문.</summary>
        private static Transform Igloo(Transform parent, Vector3 at, float yaw)
        {
            var t = G(parent, "AmmoIgloo", at, yaw);
            C(t, Vector3.zero, new Vector3(18f, 30f, 14f), "grass_dark", 90f);
            B(t, new Vector3(0f, 3.5f, -15.4f), new Vector3(20f, 7f, 1.2f), "concrete");
            B(t, new Vector3(0f, 2.3f, -16.1f), new Vector3(6f, 4.6f, 0.3f), "steel_dark");
            B(t, new Vector3(0f, 0.15f, -22f), new Vector3(12f, 0.3f, 12f), "concrete");
            return t;
        }

        // ================================================================ 항만 설비

        /// <summary>안벽 포털 크레인(레일 이동형): 붉은 문형 다리 + 흰 기계실 + 붉은 지브. 지브 = +Z(바다 쪽).</summary>
        private static Transform PortalCrane(Transform parent, Vector3 at, float yaw, float slew = 0f)
        {
            var t = G(parent, "PortalCrane", at, yaw);
            foreach (float x in new[] { -6f, 6f })
            {
                Beam(t, new Vector3(x, 0f, -5f), new Vector3(x * 0.8f, 14f, 0f), 1.2f, "crane_red");
                Beam(t, new Vector3(x, 0f, 5f), new Vector3(x * 0.8f, 14f, 0f), 1.2f, "crane_red");
                B(t, new Vector3(x, 0.6f, 0f), new Vector3(2f, 1.2f, 12f), "steel_dark");
            }
            B(t, new Vector3(0f, 14.6f, 0f), new Vector3(12f, 1.2f, 6f), "crane_red");
            var top = G(t, "Slew", new Vector3(0f, 15.2f, 0f), slew);
            C(top, new Vector3(0f, 0.5f, 0f), new Vector3(6f, 1f, 6f), "steel_dark");
            B(top, new Vector3(0f, 3.5f, -2f), new Vector3(7f, 5f, 9f), "crane_white");
            B(top, new Vector3(0f, 3.8f, 2.7f), new Vector3(4f, 2f, 0.2f), "glass");
            B(top, new Vector3(0f, 2.5f, -7.5f), new Vector3(6f, 4f, 3f), "steel_dark");
            Beam(top, new Vector3(0f, 2f, 1f), new Vector3(0f, 22f, 30f), 1.6f, "crane_red");
            Beam(top, new Vector3(0f, 8f, -2f), new Vector3(0f, 22f, 30f), 0.25f, "steel_dark");
            Beam(top, new Vector3(0f, 22f, 30f), new Vector3(0f, 6f, 30f), 0.2f, "steel_dark");
            B(top, new Vector3(0f, 5.5f, 30f), new Vector3(1.4f, 1.4f, 1.4f), "safety_yellow");
            return t;
        }

        /// <summary>안벽 소품: 계선주 줄, 육상전원 박스, 사다리, 방현재.</summary>
        private static void QuayFurniture(Transform parent, float x0, float x1, float edgeZ)
        {
            for (float x = x0; x <= x1; x += 18f)
            {
                C(parent, new Vector3(x, Ground + 0.6f, edgeZ - 2f), new Vector3(1.4f, 1.2f, 1.4f), "bollard");
                B(parent, new Vector3(x + 9f, Ground - 1.5f, edgeZ + 0.6f), new Vector3(3.5f, 3.5f, 1.2f), "rubber");
            }
        }

        /// <summary>작은 배 선체(뾰족한 선수, 공유 메시). 길이 = Z, 폭 = X, 높이 = 건현.</summary>
        private static GameObject SmallHull(Transform p, Vector3 c, float length, float beam, float freeboard, string mat)
        {
            if (_smallHull == null)
            {
                string path = $"{MatFolder}/MESH_base_SmallHull.asset";
                _smallHull = new Mesh { name = "SmallHull" };
                // 단위 선체: 선미 z -0.5(평평), 선수 z +0.5(뾰족), 바닥 y 0 → 갑판 y 1, 바닥은 폭 0.7로 좁다
                var v = new List<Vector3>
                {
                    new(-0.35f, 0f, -0.5f), new(0.35f, 0f, -0.5f), new(0.35f, 0f, 0.2f), new(0f, 0f, 0.45f), new(-0.35f, 0f, 0.2f),
                    new(-0.5f, 1f, -0.5f), new(0.5f, 1f, -0.5f), new(0.5f, 1f, 0.15f), new(0f, 1f, 0.5f), new(-0.5f, 1f, 0.15f),
                };
                var tris = new List<int>();
                for (int i = 0; i < 5; i++) { int j = (i + 1) % 5; tris.AddRange(new[] { i, i + 5, j + 5, i, j + 5, j }); }
                tris.AddRange(new[] { 5, 9, 6, 6, 9, 7, 7, 9, 8 });          // 갑판
                tris.AddRange(new[] { 0, 1, 4, 1, 2, 4, 2, 3, 4 });          // 바닥
                _smallHull.SetVertices(v);
                _smallHull.SetTriangles(tris, 0);
                _smallHull.RecalculateNormals();
                _smallHull.RecalculateBounds();
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(_smallHull, path);
            }
            var go = new GameObject("Hull", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(p, false);
            go.transform.localPosition = c;
            go.transform.localScale = new Vector3(beam, freeboard, length);
            go.GetComponent<MeshFilter>().sharedMesh = _smallHull;
            go.GetComponent<MeshRenderer>().sharedMaterial = M(mat);
            return go;
        }

        public enum CraftKind { Tug, FireBoat, Workboat, FuelBarge, Launch }

        /// <summary>항만 지원정. 선수 = +Z. 물에 뜬 높이(흘수 1.5m)로 놓는다.</summary>
        private static Transform HarborCraft(Transform parent, Vector3 at, float yaw, CraftKind kind)
        {
            var t = G(parent, kind.ToString(), at, yaw);
            switch (kind)
            {
                case CraftKind.Tug:
                {
                    SmallHull(t, new Vector3(0f, -1.5f, 0f), 30f, 10f, 4f, "hull_black");
                    B(t, new Vector3(0f, 0.9f, 0f), new Vector3(10.1f, 0.6f, 25f), "hull_red");   // 수선 띠
                    B(t, new Vector3(0f, 2.55f, 0f), new Vector3(8.8f, 0.1f, 26f), "deck_tan");
                    B(t, new Vector3(0f, 4.2f, 2f), new Vector3(7f, 3.2f, 10f), "trim_white");
                    B(t, new Vector3(0f, 7.2f, 4f), new Vector3(6f, 2.8f, 5f), "trim_white");
                    B(t, new Vector3(0f, 7.4f, 6.55f), new Vector3(5.6f, 1.2f, 0.2f), "glass");
                    B(t, new Vector3(0f, 8.8f, 4f), new Vector3(6.6f, 0.4f, 5.6f), "steel_dark");
                    C(t, new Vector3(-1.6f, 8f, -1f), new Vector3(1.4f, 4f, 1.4f), "safety_yellow");
                    C(t, new Vector3(1.6f, 8f, -1f), new Vector3(1.4f, 4f, 1.4f), "safety_yellow");
                    C(t, new Vector3(0f, 11f, 4f), new Vector3(0.4f, 5f, 0.4f), "steel");
                    C(t, new Vector3(0f, 3.4f, -8f), new Vector3(3f, 2f, 3f), "steel_dark", 0f, 0f, 90f);   // 예인 윈치
                    for (int i = 0; i < 6; i++)
                    {
                        float a = Mathf.Lerp(-60f, 60f, i / 5f) * Mathf.Deg2Rad;
                        C(t, new Vector3(Mathf.Sin(a) * 4.2f, 1.6f, 12.5f + Mathf.Cos(a) * 1.5f), new Vector3(1.8f, 1f, 1.8f), "rubber", 90f);
                    }
                    break;
                }
                case CraftKind.FireBoat:
                    SmallHull(t, new Vector3(0f, -1.5f, 0f), 26f, 7.5f, 3.6f, "safety_red");
                    B(t, new Vector3(0f, 2.15f, 0f), new Vector3(6.4f, 0.1f, 22f), "deck_tan");
                    B(t, new Vector3(0f, 3.8f, 1f), new Vector3(5.6f, 3.2f, 9f), "trim_white");
                    B(t, new Vector3(0f, 4.2f, 5.55f), new Vector3(5f, 1.2f, 0.2f), "glass");
                    B(t, new Vector3(0f, 5.6f, 1f), new Vector3(6f, 0.4f, 9.6f), "safety_red");
                    foreach (float z in new[] { -7f, 9f })
                    {
                        C(t, new Vector3(0f, 3.2f, z), new Vector3(1.4f, 2f, 1.4f), "safety_red");
                        B(t, new Vector3(0f, 4.4f, z + 1.2f), new Vector3(0.5f, 0.5f, 2.6f), "steel", 0f, -25f);
                    }
                    C(t, new Vector3(0f, 8f, 2f), new Vector3(0.3f, 4f, 0.3f), "steel");
                    break;
                case CraftKind.Workboat:
                    SmallHull(t, new Vector3(0f, -1.5f, 0f), 24f, 8f, 3.4f, "hull_grey");
                    B(t, new Vector3(0f, 1.95f, 0f), new Vector3(7f, 0.1f, 20f), "deck_tan");
                    B(t, new Vector3(0f, 3.6f, 6f), new Vector3(5f, 3.2f, 6f), "trim_white");
                    B(t, new Vector3(0f, 4f, 9.05f), new Vector3(4.4f, 1f, 0.2f), "glass");
                    C(t, new Vector3(2f, 3f, -2f), new Vector3(1.4f, 2.4f, 1.4f), "safety_yellow");
                    Beam(t, new Vector3(2f, 4f, -2f), new Vector3(-1f, 9f, -8f), 0.7f, "safety_yellow");
                    B(t, new Vector3(-1f, 2.6f, -7f), new Vector3(4f, 1.2f, 4f), "box_orange");
                    break;
                case CraftKind.FuelBarge:
                    B(t, new Vector3(0f, 0.2f, 0f), new Vector3(12f, 3.4f, 40f), "hull_black");
                    B(t, new Vector3(0f, 1.95f, 0f), new Vector3(11.6f, 0.1f, 39.6f), "hull_red");
                    for (int i = 0; i < 3; i++) C(t, new Vector3(0f, 3.4f, -12f + i * 11f), new Vector3(7f, 9f, 7f), "tank_white", 90f, 90f);
                    Beam(t, new Vector3(3f, 3f, -16f), new Vector3(3f, 3f, 16f), 0.5f, "steel_dark");
                    B(t, new Vector3(0f, 3.6f, 17f), new Vector3(6f, 3f, 4f), "trim_white");
                    break;
                case CraftKind.Launch:
                    SmallHull(t, new Vector3(0f, -1f, 0f), 14f, 4.4f, 2.4f, "trim_white");
                    B(t, new Vector3(0f, 2f, 0.5f), new Vector3(3.4f, 1.8f, 4f), "hull_grey");
                    B(t, new Vector3(0f, 2.2f, 2.55f), new Vector3(3f, 0.8f, 0.15f), "glass");
                    break;
            }
            return t;
        }

        /// <summary>부유식 독(선박 정비용): 폰툰 + 양쪽 벽 + 벽 위 지브 크레인. 길이 = Z.</summary>
        private static Transform FloatingDock(Transform parent, Vector3 at, float yaw, float length)
        {
            var t = G(parent, "FloatingDock", at, yaw);
            B(t, new Vector3(0f, 0.5f, 0f), new Vector3(44f, 4f, length), "hull_red");
            B(t, new Vector3(0f, 2.6f, 0f), new Vector3(30f, 0.2f, length - 2f), "dock_grey");
            foreach (float s in new[] { -1f, 1f })
            {
                B(t, new Vector3(s * 18.5f, 9f, 0f), new Vector3(7f, 17f, length), "dock_grey");
                B(t, new Vector3(s * 18.5f, 17.7f, 0f), new Vector3(7.4f, 0.6f, length + 0.4f), "safety_yellow");
                var crane = G(t, "DockCrane", new Vector3(s * 18.5f, 18f, s * length * 0.2f), s > 0 ? -90f : 90f);
                B(crane, new Vector3(0f, 1.5f, 0f), new Vector3(4f, 3f, 4f), "safety_yellow");
                Beam(crane, new Vector3(0f, 3f, 0f), new Vector3(0f, 12f, 18f), 0.9f, "safety_yellow");
            }
            for (int i = 0; i < 9; i++) B(t, new Vector3(0f, 3.4f, -length * 0.4f + i * length * 0.1f), new Vector3(3f, 1.6f, 2f), "wood");   // 받침목
            return t;
        }

        // ================================================================ 차량

        public enum VehicleKind { Car, Truck, MilitaryTruck, FuelTruck, FireTruck, Forklift, Bus, TowTractor, MobileCrane, TrailerTruck, TrailerEmpty, ReachStacker }

        private static readonly string[] CarColors = { "car_white", "car_white", "car_silver", "car_black", "car_red", "car_blue", "car_silver" };

        /// <summary>차량. 앞 = +Z.</summary>
        private static Transform Vehicle(Transform parent, Vector3 at, float yaw, VehicleKind kind, string paint = null)
        {
            var t = G(parent, kind.ToString(), at, yaw);
            switch (kind)
            {
                case VehicleKind.Car:
                    paint ??= CarColors[(int)R(0f, CarColors.Length - 0.01f)];
                    B(t, new Vector3(0f, 0.7f, 0f), new Vector3(1.9f, 0.8f, 4.6f), paint);
                    B(t, new Vector3(0f, 1.35f, -0.3f), new Vector3(1.7f, 0.6f, 2.4f), "glass");
                    break;
                case VehicleKind.Truck:
                case VehicleKind.MilitaryTruck:
                    paint ??= kind == VehicleKind.MilitaryTruck ? "olive" : "box_white";
                    B(t, new Vector3(0f, 1.9f, -1.2f), new Vector3(2.5f, 2.8f, 6f), paint);
                    B(t, new Vector3(0f, 1.5f, 2.9f), new Vector3(2.4f, 2.2f, 2.2f), kind == VehicleKind.MilitaryTruck ? "olive_dark" : "car_blue");
                    B(t, new Vector3(0f, 2f, 4.01f), new Vector3(2f, 0.8f, 0.05f), "glass");
                    B(t, new Vector3(0f, 0.5f, 0.6f), new Vector3(2.4f, 0.8f, 9f), "rubber");
                    break;
                case VehicleKind.FuelTruck:
                    C(t, new Vector3(0f, 2f, -1.4f), new Vector3(2.4f, 6f, 2.4f), "tank_white", 90f);
                    B(t, new Vector3(0f, 1.6f, 2.9f), new Vector3(2.4f, 2.2f, 2.2f), "safety_yellow");
                    B(t, new Vector3(0f, 0.5f, 0.6f), new Vector3(2.4f, 0.8f, 9f), "rubber");
                    break;
                case VehicleKind.FireTruck:
                    B(t, new Vector3(0f, 1.8f, -0.8f), new Vector3(2.6f, 2.6f, 7f), "safety_red");
                    B(t, new Vector3(0f, 1.7f, 3.4f), new Vector3(2.5f, 2.4f, 2.2f), "safety_red");
                    B(t, new Vector3(0f, 2.2f, 4.51f), new Vector3(2.1f, 0.9f, 0.05f), "glass");
                    B(t, new Vector3(0f, 3.2f, -0.8f), new Vector3(1.6f, 0.3f, 6f), "steel");
                    break;
                case VehicleKind.Forklift:
                    B(t, new Vector3(0f, 0.9f, 0f), new Vector3(1.3f, 1.2f, 2.4f), "safety_yellow");
                    B(t, new Vector3(0f, 1.9f, 1.4f), new Vector3(1.1f, 2.6f, 0.15f), "steel_dark");
                    B(t, new Vector3(0f, 2.1f, -0.2f), new Vector3(1.2f, 0.12f, 1.4f), "steel_dark");
                    break;
                case VehicleKind.Bus:
                    B(t, new Vector3(0f, 1.8f, 0f), new Vector3(2.5f, 2.9f, 11f), paint ?? "bus_white");
                    B(t, new Vector3(0f, 2.3f, 0f), new Vector3(2.55f, 1f, 9.6f), "glass");
                    B(t, new Vector3(0f, 1f, 0f), new Vector3(2.56f, 0.4f, 11f), "car_blue");
                    break;
                case VehicleKind.TowTractor:
                    B(t, new Vector3(0f, 0.8f, 0f), new Vector3(2f, 1f, 3f), "safety_yellow");
                    B(t, new Vector3(0f, 1.6f, -0.6f), new Vector3(1.6f, 0.8f, 1.2f), "glass");
                    break;
                case VehicleKind.MobileCrane:
                    B(t, new Vector3(0f, 1.6f, 0f), new Vector3(2.8f, 2f, 10f), "safety_yellow");
                    B(t, new Vector3(0f, 3.2f, -2f), new Vector3(2.4f, 1.6f, 3f), "safety_yellow");
                    Beam(t, new Vector3(0f, 3.4f, -2f), new Vector3(0f, 18f, 12f), 0.9f, "safety_yellow");
                    break;
                case VehicleKind.TrailerTruck:
                case VehicleKind.TrailerEmpty:
                    // 트랙터(앞) + 40ft 평판 섀시. 컨테이너는 TrailerTruck만 싣는다
                    B(t, new Vector3(0f, 1.9f, 6.6f), new Vector3(2.5f, 2.8f, 2.6f), paint ?? CarColors[(int)R(0f, CarColors.Length - 0.01f)]);
                    B(t, new Vector3(0f, 2.3f, 7.91f), new Vector3(2.1f, 1f, 0.05f), "glass");
                    B(t, new Vector3(0f, 0.55f, 0f), new Vector3(2.4f, 0.9f, 16.2f), "rubber");
                    B(t, new Vector3(0f, 1.15f, -1.6f), new Vector3(2.5f, 0.3f, 12.6f), "steel_dark");
                    if (kind == VehicleKind.TrailerTruck)
                        B(t, new Vector3(0f, 2.6f, -1.6f), new Vector3(2.44f, 2.59f, 12.2f), ContainerColors[(int)R(0f, ContainerColors.Length - 0.01f)]);
                    break;
                case VehicleKind.ReachStacker:
                    // 리치스태커: 뒤 평형추·운전실, 뒤에서 앞으로 뻗은 붐, 끝의 스프레더가 컨테이너를 가로로 문다
                    B(t, new Vector3(0f, 1.3f, 0f), new Vector3(4.2f, 1.6f, 11f), "safety_yellow");
                    B(t, new Vector3(0f, 0.9f, 0f), new Vector3(4.4f, 1.8f, 8.6f), "rubber");
                    B(t, new Vector3(0f, 2.6f, -4.6f), new Vector3(4f, 1.6f, 1.8f), "steel_dark");      // 평형추
                    B(t, new Vector3(0f, 3.6f, -1.2f), new Vector3(1.8f, 2.2f, 2f), "glass");           // 운전실
                    Beam(t, new Vector3(0f, 2.6f, -3.4f), new Vector3(0f, 9.4f, 6.4f), 1.2f, "safety_yellow");
                    B(t, new Vector3(0f, 8.6f, 6.8f), new Vector3(6f, 0.5f, 1.4f), "steel_dark");      // 스프레더
                    B(t, new Vector3(0f, 6.9f, 6.8f), new Vector3(12.2f, 2.59f, 2.44f), ContainerColors[(int)R(0f, ContainerColors.Length - 0.01f)]);
                    break;
            }
            return t;
        }

        /// <summary>주차장: 아스팔트 + 흰 선 + 차(약 75%).</summary>
        private static void ParkingLot(Transform parent, Vector3 center, float yaw, int rows, int cols)
        {
            var t = G(parent, "ParkingLot", center, yaw);
            const float sw = 2.8f, rowGap = 14f;
            float w = cols * sw + 4f, d = rows * rowGap + 2f;
            B(t, new Vector3(0f, 0.12f, 0f), new Vector3(w, 0.24f, d), "road");
            for (int r = 0; r < rows; r++)
            {
                float z = -d * 0.5f + 1f + rowGap * (r + 0.5f);
                for (int c = 0; c <= cols; c++)
                    B(t, new Vector3(-w * 0.5f + 2f + c * sw, 0.26f, z - 2.4f), new Vector3(0.15f, 0.05f, 5f), "mark_white");
                for (int c = 0; c < cols; c++)
                    if (R(0f, 1f) < 0.75f)
                        Vehicle(t, new Vector3(-w * 0.5f + 2f + (c + 0.5f) * sw, 0.24f, z - 2.4f), r % 2 == 0 ? 0f : 180f, VehicleKind.Car);
            }
        }

        private static readonly string[] ContainerColors = { "box_red", "box_blue", "box_orange", "box_green", "box_white", "box_teal", "box_blue", "box_red" };

        /// <summary>컨테이너 블록(12m): 가운데 정렬, 행 = 로컬 Z(컨테이너 길이 방향으로 이어짐), 열 = 로컬 X. 무작위 높이·색.</summary>
        private static void ContainerBlock(Transform parent, Vector3 center, float yaw, int rows, int cols, int maxStack)
        {
            var t = G(parent, "Containers", center, yaw);
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                int stack = 1 + (int)R(0f, maxStack - 0.01f);
                for (int s = 0; s < stack; s++)
                    B(t, new Vector3((c - (cols - 1) * 0.5f) * 2.8f, 1.3f + s * 2.6f, (r - (rows - 1) * 0.5f) * 13.2f),
                      new Vector3(2.44f, 2.59f, 12.2f), ContainerColors[(int)R(0f, ContainerColors.Length - 0.01f)]);
            }
        }

        // ================================================================ 항공기(기수 = +Z, 바퀴 = y 0)

        /// <summary>P-3C 해상초계기: 저익 4발 터보프롭, 꼬리 자기탐지 붐(MAD). 길이 35.6, 날개폭 30.4.</summary>
        private static Transform P3C(Transform parent, Vector3 at, float yaw, string name = "P3C")
        {
            var t = G(parent, name, at, yaw);
            Tube(t, new Vector3(0f, 3.3f, 0f), 33f, 3.5f, "aircraft");
            S(t, new Vector3(0f, 3.1f, 16.5f), new Vector3(2.6f, 2.6f, 3f), "aircraft_dark");
            B(t, new Vector3(0f, 4.4f, 14.2f), new Vector3(2.4f, 0.9f, 2.4f), "cockpit");
            B(t, new Vector3(0f, 2.3f, 0f), new Vector3(3.6f, 0.9f, 33f), "aircraft_grey");   // 아랫배 회색
            foreach (float s in new[] { -1f, 1f })
            {
                B(t, new Vector3(s * 8f, 2.6f, 1.6f), new Vector3(13.5f, 0.55f, 4.6f), "aircraft", 0f, 0f, s * -4f);
                foreach (float x in new[] { 4.6f, 9.6f })
                {
                    Tube(t, new Vector3(s * x, 2.4f, 3.4f), 5.6f, 1.5f, "aircraft");
                    C(t, new Vector3(s * x, 2.4f, 6.4f), new Vector3(4.1f, 0.06f, 4.1f), "prop_disc", 90f);
                }
                B(t, new Vector3(s * 3.3f, 3.9f, -14.5f), new Vector3(6.6f, 0.35f, 2.8f), "aircraft");
            }
            B(t, new Vector3(0f, 7.2f, -14.4f), new Vector3(0.45f, 7f, 4.6f), "aircraft", 0f, -14f);
            Tube(t, new Vector3(0f, 3.6f, -18.6f), 4.4f, 0.6f, "aircraft_dark");
            B(t, new Vector3(0f, 0.8f, 11f), new Vector3(0.3f, 1.6f, 0.3f), "steel_dark");
            return t;
        }

        /// <summary>P-8A 해상초계기(737 기반): 후퇴익 쌍발 제트. 길이 39.5, 날개폭 37.6.</summary>
        private static Transform P8A(Transform parent, Vector3 at, float yaw)
        {
            var t = G(parent, "P8A", at, yaw);
            Tube(t, new Vector3(0f, 3.4f, 0f), 37f, 3.8f, "aircraft_grey");
            B(t, new Vector3(0f, 4.6f, 17.4f), new Vector3(2.6f, 0.9f, 2.2f), "cockpit");
            foreach (float s in new[] { -1f, 1f })
            {
                B(t, new Vector3(s * 9.6f, 2.5f, -0.5f), new Vector3(17f, 0.6f, 5.2f), "aircraft_grey", s * 25f, 0f, s * -5f);
                Tube(t, new Vector3(s * 5.8f, 1.6f, 4.2f), 4.6f, 2.2f, "aircraft_dark");
                B(t, new Vector3(s * 4.6f, 4.4f, -16.5f), new Vector3(8.6f, 0.4f, 3f), "aircraft_grey", s * 28f);
                B(t, new Vector3(s * 17.8f, 3.6f, -4.6f), new Vector3(0.3f, 2f, 1.6f), "aircraft_grey");
            }
            B(t, new Vector3(0f, 8.2f, -16.2f), new Vector3(0.5f, 8f, 5.6f), "aircraft_grey", 0f, -24f);
            return t;
        }

        /// <summary>E-2 조기경보기: 고익 쌍발 터보프롭, 동체 위 회전 레이더돔, 꼬리 수직판 넷.</summary>
        private static Transform E2(Transform parent, Vector3 at, float yaw)
        {
            var t = G(parent, "E2", at, yaw);
            Tube(t, new Vector3(0f, 2.8f, 0f), 17f, 3f, "aircraft");
            B(t, new Vector3(0f, 3.8f, 7.6f), new Vector3(2.2f, 0.8f, 1.8f), "cockpit");
            foreach (float s in new[] { -1f, 1f })
            {
                B(t, new Vector3(s * 6.4f, 4.5f, 1.2f), new Vector3(11f, 0.5f, 3.2f), "aircraft");
                Tube(t, new Vector3(s * 4f, 3.9f, 2.6f), 5f, 1.6f, "aircraft");
                C(t, new Vector3(s * 4f, 3.9f, 5.2f), new Vector3(4.1f, 0.06f, 4.1f), "prop_disc", 90f);
            }
            B(t, new Vector3(0f, 3f, -8.4f), new Vector3(8f, 0.35f, 2.2f), "aircraft");
            for (int i = 0; i < 4; i++) B(t, new Vector3(-3.9f + i * 2.6f, 4.6f, -8.6f), new Vector3(0.25f, 3.4f, 2.2f), "aircraft");
            foreach (float z in new[] { -1.4f, -4.2f }) B(t, new Vector3(0f, 4.9f, z), new Vector3(0.5f, 1.6f, 0.8f), "aircraft_dark");
            C(t, new Vector3(0f, 6.2f, -2.8f), new Vector3(7.3f, 0.8f, 7.3f), "aircraft_dark");
            C(t, new Vector3(0f, 6.2f, -2.8f), new Vector3(7.36f, 0.2f, 7.36f), "trim_white");
            return t;
        }

        /// <summary>F/A-18 계열 함재 전투기: 쌍발, 기울어진 쌍수직꼬리, 날개 앞전 연장부(LEX). 길이 17.</summary>
        private static Transform Fighter(Transform parent, Vector3 at, float yaw, string name = "Fighter")
        {
            var t = G(parent, name, at, yaw);
            Tube(t, new Vector3(0f, 2.1f, 0f), 15.5f, 1.8f, "aircraft_fighter");
            Tube(t, new Vector3(0f, 2.1f, 8f), 2.6f, 1f, "aircraft_dark");
            S(t, new Vector3(0f, 2.9f, 4.2f), new Vector3(0.9f, 0.8f, 3f), "cockpit");
            B(t, new Vector3(0f, 1.9f, 2.6f), new Vector3(3.8f, 0.25f, 5f), "aircraft_fighter");   // LEX
            foreach (float s in new[] { -1f, 1f })
            {
                B(t, new Vector3(s * 3.6f, 1.9f, -1.6f), new Vector3(4.8f, 0.3f, 4f), "aircraft_fighter", s * 22f);
                B(t, new Vector3(s * 1.4f, 3.8f, -5.2f), new Vector3(0.25f, 3.2f, 3f), "aircraft_fighter", 0f, -18f, s * -20f);
                B(t, new Vector3(s * 2.4f, 1.8f, -6.8f), new Vector3(3f, 0.2f, 2.4f), "aircraft_fighter", s * 18f);
                Tube(t, new Vector3(s * 1f, 1.7f, -1f), 9f, 1.1f, "aircraft_dark");
            }
            B(t, new Vector3(0f, 0.7f, 5f), new Vector3(0.25f, 1.4f, 0.25f), "steel_dark");
            return t;
        }

        /// <summary>해상작전헬기(MH-60 계열): 동체 + 꼬리붐 + 주로터 4엽(16m) + 꼬리로터.</summary>
        private static Transform Helicopter(Transform parent, Vector3 at, float yaw, bool large, string name = "Helicopter")
        {
            var t = G(parent, name, at, yaw);
            float k = large ? 1f : 0.8f;
            Tube(t, new Vector3(0f, 2f * k, 1f * k), 9f * k, 2.6f * k, large ? "aircraft_grey" : "aircraft");
            B(t, new Vector3(0f, 2.6f * k, 4.6f * k), new Vector3(2f * k, 1f * k, 1.6f * k), "cockpit");
            Tube(t, new Vector3(0f, 2.7f * k, -6f * k), 8f * k, 0.9f * k, large ? "aircraft_grey" : "aircraft");
            B(t, new Vector3(0.2f, 3.8f * k, -10f * k), new Vector3(0.2f, 2.6f * k, 1.6f * k), large ? "aircraft_grey" : "aircraft");
            C(t, new Vector3(0.5f, 4f * k, -10.2f * k), new Vector3(2.8f * k, 0.05f, 2.8f * k), "rotor", 0f, 0f, 90f);
            C(t, new Vector3(0f, 3.6f * k, 1f * k), new Vector3(1.2f, 0.8f, 1.2f), "steel_dark");
            for (int i = 0; i < 2; i++)   // 4엽 = 가로지르는 날개 둘
                B(t, new Vector3(0f, 4f * k, 1f * k), new Vector3(0.5f, 0.08f, 16f * k), "rotor", 25f + i * 90f);
            foreach (float s in new[] { -1f, 1f }) C(t, new Vector3(s * 1.4f * k, 0.4f, 2.4f * k), new Vector3(0.8f, 0.3f, 0.8f), "rubber", 0f, 0f, 90f);
            return t;
        }

        // ================================================================ 비행장 표시·조경

        /// <summary>헬기 착륙장: 진한 콘크리트 + 노란 원 + 흰 H.</summary>
        private static void HeliPad(Transform parent, Vector3 at)
        {
            var t = G(parent, "HeliPad", at);
            B(t, new Vector3(0f, 0.2f, 0f), new Vector3(30f, 0.4f, 30f), "apron");
            C(t, new Vector3(0f, 0.42f, 0f), new Vector3(22f, 0.04f, 22f), "mark_yellow");
            C(t, new Vector3(0f, 0.44f, 0f), new Vector3(20f, 0.04f, 20f), "apron");
            B(t, new Vector3(-3f, 0.47f, 0f), new Vector3(1.4f, 0.04f, 9f), "mark_white");
            B(t, new Vector3(3f, 0.47f, 0f), new Vector3(1.4f, 0.04f, 9f), "mark_white");
            B(t, new Vector3(0f, 0.47f, 0f), new Vector3(6f, 0.04f, 1.4f), "mark_white");
        }

        /// <summary>항공기 주기장 표시: 노란 유도선 + 정지 막대.</summary>
        private static void Stand(Transform parent, Vector3 at, float yaw, float length)
        {
            var t = G(parent, "Stand", at, yaw);
            B(t, new Vector3(0f, 0.45f, 0f), new Vector3(0.5f, 0.04f, length), "mark_yellow");
            B(t, new Vector3(0f, 0.45f, length * 0.5f - 4f), new Vector3(6f, 0.04f, 0.5f), "mark_yellow");
        }

        /// <summary>활엽수(둥근 수관 두세 개) 또는 침엽수(길쭉한 수관).</summary>
        private static void Tree(Transform parent, Vector3 at, float size, bool conifer)
        {
            var t = G(parent, "Tree", at, R(0f, 360f));
            C(t, new Vector3(0f, size * 0.25f, 0f), new Vector3(size * 0.08f, size * 0.5f, size * 0.08f), "trunk");
            string m = R(0f, 1f) < 0.5f ? "tree_dark" : (R(0f, 1f) < 0.6f ? "tree_mid" : "tree_light");
            if (conifer)
            {
                S(t, new Vector3(0f, size * 0.75f, 0f), new Vector3(size * 0.45f, size * 1.1f, size * 0.45f), "tree_dark");
            }
            else
            {
                S(t, new Vector3(0f, size * 0.7f, 0f), new Vector3(size * 0.7f, size * 0.6f, size * 0.7f), m);
                S(t, new Vector3(size * 0.2f, size * 0.85f, size * 0.1f), new Vector3(size * 0.5f, size * 0.45f, size * 0.5f), m);
            }
        }

        /// <summary>울타리: 4m마다 기둥 + 철망(얇은 판).</summary>
        private static void Fence(Transform parent, Vector3 a, Vector3 b, float h = 2.4f)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            var mesh = Box("FenceMesh", parent, (a + b) * 0.5f + Vector3.up * (Ground + h * 0.5f), new Vector3(0.08f, h, len), M("steel"));
            mesh.transform.localRotation = Quaternion.LookRotation(d.normalized);
            for (float s = 0f; s <= len; s += 6f)
                B(parent, a + d.normalized * s + Vector3.up * (Ground + h * 0.55f), new Vector3(0.25f, h * 1.1f, 0.25f), "steel_dark");
        }

        private static void LampPost(Transform parent, Vector3 at)
        {
            var t = G(parent, "LampPost", at);
            C(t, new Vector3(0f, 5f, 0f), new Vector3(0.35f, 10f, 0.35f), "steel");
            B(t, new Vector3(0f, 10f, 0.8f), new Vector3(0.8f, 0.4f, 2f), "lamp");
        }

        private static void GuardTower(Transform parent, Vector3 at, float yaw)
        {
            var t = G(parent, "GuardTower", at, yaw);
            foreach (float x in new[] { -1.8f, 1.8f })
            foreach (float z in new[] { -1.8f, 1.8f })
                B(t, new Vector3(x, 4.5f, z), new Vector3(0.4f, 9f, 0.4f), "steel_dark");
            B(t, new Vector3(0f, 10.4f, 0f), new Vector3(5f, 2.8f, 5f), "wall_beige");
            B(t, new Vector3(0f, 10.8f, 0f), new Vector3(5.1f, 0.9f, 5.1f), "glass");
            B(t, new Vector3(0f, 12.1f, 0f), new Vector3(6f, 0.5f, 6f), "roof_green");
        }
    }
}

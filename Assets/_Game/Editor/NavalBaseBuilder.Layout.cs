using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// 해군기지 배치(2026-10-09). 실제 해군기지·해군항공기지의 구역 나누기를 따랐다
    /// (Norfolk·Earle 군항: 해안에 직각인 돌제, 돌제 뿌리에 항무 건물·보급 창고·정비 시설, 연료 부두는 따로 /
    ///  해군항공기지: 활주로와 평행 유도로, 계류장 뒤 격납고 줄, 관제탑·소방대는 비행장 가운데 옆,
    ///  탄약고·연료는 비행장 가장자리에 떨어뜨림).
    /// 좌표(m, 섬 로컬): +X 동, +Z 북(바다). 섬 윗면 y = Ground. 시작 화면이 쓰는 자리는 바꾸지 않는다:
    ///   돌제 x = -620 / -400 / -180 / 40(함선 4척이 각 돌제 동쪽 x+42, z 632에 선다), Harbor/ESC_PB*(항로 순찰),
    ///   NavalAirStation/Fighter1(계류장 (60, -185)에서 출발해 활주로로 이륙).
    /// </summary>
    internal static partial class NavalBaseBuilder
    {
        private static readonly List<Rect> Reserved = new();
        private static readonly float[] PierX = { -620f, -400f, -180f, 40f };
        private const float RunwayZ = -330f, TaxiZ = -262f;

        private static void Reserve(float x0, float z0, float x1, float z1)
            => Reserved.Add(Rect.MinMaxRect(Mathf.Min(x0, x1), Mathf.Min(z0, z1), Mathf.Max(x0, x1), Mathf.Max(z0, z1)));

        private static bool IsReserved(float x, float z)
        {
            foreach (var r in Reserved) if (r.Contains(new Vector2(x, z))) return true;
            return false;
        }

        private static void BuildLayout()
        {
            Reserved.Clear();
            BuildRoadNetwork();
            BuildWaterfront();
            BuildHarborSupport();
            BuildIndustrial();
            BuildCommand();
            BuildHousing();
            BuildSupportBand();
            BuildWestCompound();
            BuildAirStation();
            BuildRadarAndOrdnance();
            BuildPerimeter();
            BuildGreenery();
        }

        // ================================================================ 도로

        private static void BuildRoadNetwork()
        {
            var roads = Group("Roads");
            void R(Vector3 a, Vector3 b, float w) { Road(roads, a, b, w); Reserve(a.x - w, a.z - w, b.x + w, b.z + w); }
            R(new Vector3(-720f, 0f, 455f), new Vector3(480f, 0f, 455f), 20f);     // 안벽 도로
            R(new Vector3(-880f, 0f, 110f), new Vector3(880f, 0f, 110f), 22f);     // 기지 동서 간선
            R(new Vector3(-80f, 0f, 455f), new Vector3(-80f, 0f, -50f), 22f);      // 남북 간선(군항 ↔ 비행장)
            R(new Vector3(-400f, 0f, 455f), new Vector3(-400f, 0f, 110f), 16f);
            R(new Vector3(470f, 0f, 455f), new Vector3(470f, 0f, -50f), 16f);
            R(new Vector3(-820f, 0f, -50f), new Vector3(820f, 0f, -50f), 18f);     // 비행장 지원 도로
            R(new Vector3(760f, 0f, 110f), new Vector3(760f, 0f, -50f), 14f);
            R(new Vector3(-720f, 0f, 455f), new Vector3(-720f, 0f, 110f), 14f);
            foreach (var p in new[] { new Vector2(-80f, 455f), new Vector2(-80f, 110f), new Vector2(-80f, -50f), new Vector2(-400f, 455f),
                                      new Vector2(-400f, 110f), new Vector2(470f, 455f), new Vector2(470f, 110f), new Vector2(470f, -50f),
                                      new Vector2(760f, 110f), new Vector2(760f, -50f), new Vector2(-720f, 455f), new Vector2(-720f, 110f) })
                B(roads, new Vector3(p.x, Ground + 0.21f, p.y), new Vector3(26f, 0.42f, 26f), "road");
            // 횡단보도·가로등
            foreach (var p in new[] { new Vector2(-80f, 110f), new Vector2(470f, 110f), new Vector2(-400f, 110f) })
                for (int i = -3; i <= 3; i++)
                    B(roads, new Vector3(p.x + i * 2.4f, Ground + 0.45f, p.y + 17f), new Vector3(1.2f, 0.05f, 5f), "mark_white");
            for (float x = -860f; x <= 860f; x += 60f) { LampPost(roads, new Vector3(x, Ground, 124f)); LampPost(roads, new Vector3(x + 30f, Ground, 96f)); }

            // 움직이는 차량: 합치지 않고 따로 둔다 — 시작 화면(NavalBaseMenu)이 도로 순환 경로를 정해 돌린다. 여기서는 간선 도로에 세워 둔다
            var traffic = Group("RoadTraffic");
            VehicleKind[] kinds = { VehicleKind.Car, VehicleKind.Car, VehicleKind.MilitaryTruck, VehicleKind.Car, VehicleKind.Bus, VehicleKind.Car,
                                    VehicleKind.TrailerTruck, VehicleKind.Car, VehicleKind.Truck, VehicleKind.Car, VehicleKind.MilitaryTruck, VehicleKind.FuelTruck };
            for (int i = 0; i < 28; i++)
                Vehicle(traffic, new Vector3(-820f + i * 60f, Ground + 0.4f, 110f + (i % 2 == 0 ? -4f : 4f)), i % 2 == 0 ? 90f : 270f, kinds[i % kinds.Length]);
        }

        /// <summary>도로: 아스팔트 + 양옆 보도 + 가운데 점선.</summary>
        private static void Road(Transform parent, Vector3 a, Vector3 b, float width)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            var rot = Quaternion.LookRotation(d.normalized);
            var road = Box("Road", parent, (a + b) * 0.5f + Vector3.up * (Ground + 0.2f), new Vector3(width, 0.4f, len), M("road"));
            road.transform.localRotation = rot;
            var walk = Box("Sidewalk", parent, (a + b) * 0.5f + Vector3.up * (Ground + 0.12f), new Vector3(width + 6f, 0.24f, len), M("sidewalk"));
            walk.transform.localRotation = rot;
            for (float t = 12f; t < len - 12f; t += 18f)
            {
                var dash = Box("Dash", parent, a + d.normalized * t + Vector3.up * (Ground + 0.43f), new Vector3(0.6f, 0.06f, 7f), M("mark_white"));
                dash.transform.localRotation = rot;
            }
        }

        // ================================================================ 군항(돌제·안벽)

        private static void BuildWaterfront()
        {
            var harbor = Group("Harbor");
            // 안벽 매립지(콘크리트), 물에 닿는 면은 어둡게
            // 윗면을 잔디보다 조금 높게(같은 높이면 겹친 곳이 깜빡인다)
            Box("Quay", harbor, new Vector3(-290f, Ground * 0.5f - 2.95f, 470f), new Vector3(860f, Ground + 6.1f, 180f), M("concrete"));
            Box("QuayFace", harbor, new Vector3(-290f, -1f, 559.6f), new Vector3(860f, 6f, 0.6f), M("quay_face"));
            Box("QuayApron", harbor, new Vector3(-290f, Ground + 0.12f, 532f), new Vector3(860f, 0.24f, 52f), M("concrete_light"));
            Box("QuayEdge", harbor, new Vector3(-290f, Ground + 0.32f, 557.5f), new Vector3(860f, 0.3f, 1.2f), M("mark_yellow"));
            Reserve(-720f, 380f, 140f, 560f);

            foreach (float px in PierX) Pier(harbor, px);
            var berths = Group("Berths", harbor);
            Berth(berths, "ESC_CAP_T3", PierX[0], -1);
            Berth(berths, "ESC_ASW_T3", PierX[0], +1);
            Berth(berths, "ESC_STK_T3", PierX[1], +1);
            Berth(berths, "ESC_EW_T2", PierX[2], +1);
            Berth(berths, "ESC_CAP_T2", PierX[3], +1);

            // 항로를 순찰하는 고속정(시작 화면이 움직인다)
            var pb = Place(EscortFolder + "ESC_PB_T0.prefab", harbor, new Vector3(-60f, 0f, 760f), 200f, 0.36f / K);
            SitOnWater(pb);

            var quay = Group("QuayEquipment", harbor);
            // 안벽 레일과 포털 크레인(돌제 사이)
            foreach (float z in new[] { 528f, 540f }) B(quay, new Vector3(-290f, Ground + 0.3f, z), new Vector3(860f, 0.2f, 0.6f), "steel_dark");
            PortalCrane(quay, new Vector3(-510f, Ground, 534f), 0f, -30f);
            PortalCrane(quay, new Vector3(-290f, Ground, 534f), 0f, 25f);
            PortalCrane(quay, new Vector3(-70f, Ground, 534f), 0f, -10f);
            QuayFurniture(quay, -715f, 135f, 558f);
            for (float x = -700f; x <= 130f; x += 45f) LampPost(quay, new Vector3(x, Ground, 552f));
            foreach (float px in PierX)
            {
                // 돌제 뿌리: 육상전원 변전함, 계류 대기소, 지게차·트럭·소형 컨테이너
                B(quay, new Vector3(px - 22f, Ground + 1.5f, 548f), new Vector3(6f, 3f, 4f), "wall_grey");
                B(quay, new Vector3(px - 22f, Ground + 3.2f, 548f), new Vector3(6.4f, 0.3f, 4.4f), "safety_yellow");
                B(quay, new Vector3(px + 24f, Ground + 1.8f, 515f), new Vector3(8f, 3.6f, 5f), "wall_white");
                B(quay, new Vector3(px + 24f, Ground + 3.8f, 515f), new Vector3(8.6f, 0.4f, 5.6f), "roof_red");
                Vehicle(quay, new Vector3(px - 30f, Ground, 520f), 90f, VehicleKind.Forklift);
                Vehicle(quay, new Vector3(px + 40f, Ground, 505f), 90f, VehicleKind.Truck);
                ContainerBlock(quay, new Vector3(px - 45f, Ground, 505f), 90f, 2, 2, 2);
            }
            Vehicle(quay, new Vector3(-150f, Ground, 515f), 0f, VehicleKind.MobileCrane);
            Vehicle(quay, new Vector3(-460f, Ground, 512f), 90f, VehicleKind.FuelTruck);
            Vehicle(quay, new Vector3(-250f, Ground, 498f), -90f, VehicleKind.Bus);
        }

        private static void Pier(Transform parent, float x)
        {
            var p = Group("Pier", parent);
            const float len = 170f, width = 26f, z0 = 556f;
            Box("Deck", p, new Vector3(x, PierDeck - 0.75f, z0 + len * 0.5f), new Vector3(width, 1.5f, len), M("concrete_light"));
            Box("Lane", p, new Vector3(x, PierDeck + 0.05f, z0 + len * 0.5f), new Vector3(0.8f, 0.1f, len - 8f), M("mark_yellow"));
            Box("Cap", p, new Vector3(x, PierDeck - 2.2f, z0 + len * 0.5f), new Vector3(width - 2f, 1.4f, len), M("concrete"));
            foreach (float s in new[] { -1f, 1f })
            {
                Box("Edge", p, new Vector3(x + s * (width * 0.5f - 0.4f), PierDeck + 0.2f, z0 + len * 0.5f), new Vector3(0.8f, 0.4f, len), M("mark_yellow"));
                for (float z = z0 + 12f; z < z0 + len; z += 24f)
                    Box("Pile", p, new Vector3(x + s * (width * 0.5f - 3f), -2f, z), new Vector3(3f, 9f, 3f), M("pile"));
                for (float z = z0 + 20f; z < z0 + len; z += 30f)
                {
                    Cyl("Bollard", p, new Vector3(x + s * (width * 0.5f - 1.5f), PierDeck + 0.6f, z), new Vector3(1.2f, 0.6f, 1.2f), M("bollard"));
                    Box("Fender", p, new Vector3(x + s * (width * 0.5f + 0.5f), 1.2f, z + 12f), new Vector3(1f, 3.2f, 3.2f), M("rubber"));
                }
                for (float z = z0 + 30f; z < z0 + len; z += 50f)
                {
                    Cyl("PierLight", p, new Vector3(x + s * 9f, PierDeck + 4f, z), new Vector3(0.3f, 4f, 0.3f), M("steel"));
                    Box("PierLamp", p, new Vector3(x + s * 9f, PierDeck + 8.1f, z), new Vector3(0.8f, 0.4f, 1.6f), M("lamp"));
                }
            }
            // 돌제 위 장비: 육상전원 박스·호스 릴·현문 대기소
            Box("ShorePower", p, new Vector3(x - 6f, PierDeck + 1.2f, z0 + 60f), new Vector3(3f, 2.4f, 4f), M("wall_grey"));
            Box("ShorePower", p, new Vector3(x + 6f, PierDeck + 1.2f, z0 + 120f), new Vector3(3f, 2.4f, 4f), M("wall_grey"));
            Box("HoseReel", p, new Vector3(x - 5f, PierDeck + 0.8f, z0 + 95f), new Vector3(2f, 1.6f, 2f), M("safety_red"));
            Box("BrowHut", p, new Vector3(x + 5f, PierDeck + 1.4f, z0 + 30f), new Vector3(4f, 2.8f, 3f), M("wall_white"));
        }

        /// <summary>돌제 옆 계류: 함미를 안벽 쪽, 함수를 외해로. 홋줄 4가닥 + 현문 사다리 + 방현재.</summary>
        private static void Berth(Transform parent, string escort, float pierX, int side)
        {
            float tierScale = escort.EndsWith("T3") ? 1.08f : 0.92f;
            float beam = 2.9f * tierScale / K;
            float length = 9f * tierScale / K;
            float x = pierX + side * (13f + 2.5f + beam * 0.5f);
            const float sternZ = 566f;
            var ship = Place(EscortFolder + escort + ".prefab", parent, new Vector3(x, 0f, sternZ + length * 0.5f), 0f, tierScale / K);
            if (ship == null) return;
            ship.name = $"Berth_{escort}";
            SitOnWater(ship);
            const float deck = 9f;
            float hull = x - side * beam * 0.45f;
            float pierEdge = pierX + side * 11.5f;
            var lines = Group("Mooring", parent);
            Line("BowLine", lines, new Vector3(hull, deck, sternZ + length * 0.88f), new Vector3(pierEdge, PierDeck + 1f, sternZ + length * 0.88f + 22f), 0.8f, M("rope"));
            Line("SternLine", lines, new Vector3(hull, deck, sternZ + 6f), new Vector3(pierEdge, PierDeck + 1f, sternZ - 10f), 0.8f, M("rope"));
            Line("Spring", lines, new Vector3(hull, deck, sternZ + length * 0.55f), new Vector3(pierEdge, PierDeck + 1f, sternZ + length * 0.35f), 0.8f, M("rope"));
            Line("Brow", lines, new Vector3(pierEdge - side * 1f, PierDeck + 0.3f, sternZ + length * 0.47f),
                 new Vector3(hull, deck + 0.5f, sternZ + length * 0.47f), 3f, M("gangway"), flat: true);
        }

        // ================================================================ 항만 지원(지원정 정박지·항무청·연료 부두·부유식 독)

        private static void BuildHarborSupport()
        {
            var s = Group("HarborSupport");
            // 지원정 정박지: 안벽 동쪽, 작은 방파제로 감싼 소형 정박지와 손가락 부두 셋
            B(s, new Vector3(310f, Ground * 0.5f - 2.95f, 515f), new Vector3(340f, Ground + 6.1f, 90f), "concrete");
            B(s, new Vector3(310f, Ground + 0.12f, 540f), new Vector3(340f, 0.24f, 36f), "concrete_light");
            B(s, new Vector3(310f, Ground + 0.32f, 559.4f), new Vector3(340f, 0.3f, 1.2f), "mark_yellow");
            Reserve(140f, 460f, 480f, 700f);
            foreach (float fx in new[] { 200f, 290f, 380f })
            {
                B(s, new Vector3(fx, PierDeck - 0.6f, 600f), new Vector3(9f, 1.2f, 80f), "concrete_light");
                for (float z = 570f; z < 640f; z += 18f)
                {
                    B(s, new Vector3(fx, -2f, z), new Vector3(2f, 9f, 2f), "pile");
                    C(s, new Vector3(fx - 3.6f, PierDeck + 0.4f, z), new Vector3(0.9f, 0.6f, 0.9f), "bollard");
                    C(s, new Vector3(fx + 3.6f, PierDeck + 0.4f, z), new Vector3(0.9f, 0.6f, 0.9f), "bollard");
                }
            }
            // 방파제: 동쪽에서 북쪽으로 올라가 서쪽으로 꺾인다(정박지 입구는 서쪽)
            void Mole(Vector3 a, Vector3 b)
            {
                Vector3 d = b - a; var rot = Quaternion.LookRotation(d.normalized); Vector3 mid = (a + b) * 0.5f;
                var core = B(s, new Vector3(mid.x, -0.5f, mid.z), new Vector3(16f, 7f, d.magnitude + 16f), "dock_grey"); core.transform.localRotation = rot;
                var crown = B(s, new Vector3(mid.x, Ground - 0.4f, mid.z), new Vector3(8f, 1.2f, d.magnitude + 8f), "concrete"); crown.transform.localRotation = rot;
                Vector3 right = rot * Vector3.right;
                for (float t = 0f; t <= d.magnitude; t += 6f)
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var blk = B(s, a + d.normalized * t + right * side * R(7f, 9f) + Vector3.up * R(0.5f, 2f), Vector3.one * R(3f, 4.5f), "concrete");
                        blk.transform.localRotation = Quaternion.Euler(R(0f, 60f), R(0f, 360f), R(0f, 60f));
                    }
            }
            Mole(new Vector3(470f, 0f, 560f), new Vector3(470f, 0f, 690f));
            Mole(new Vector3(470f, 0f, 690f), new Vector3(330f, 0f, 690f));
            C(s, new Vector3(330f, Ground + 4f, 690f), new Vector3(3f, 8f, 3f), "crane_red");   // 입구 등표
            S(s, new Vector3(330f, Ground + 8.6f, 690f), new Vector3(1.6f, 1.6f, 1.6f), "beacon_red");

            // 지원정: 예인선 둘, 소방정, 작업정, 연료 바지, 소형정 둘
            HarborCraft(s, new Vector3(186f, 0f, 606f), 0f, CraftKind.Tug);
            HarborCraft(s, new Vector3(214f, 0f, 606f), 0f, CraftKind.Tug);
            HarborCraft(s, new Vector3(275f, 0f, 602f), 0f, CraftKind.FireBoat);
            HarborCraft(s, new Vector3(303f, 0f, 588f), 0f, CraftKind.Launch);
            HarborCraft(s, new Vector3(303f, 0f, 622f), 0f, CraftKind.Launch);
            HarborCraft(s, new Vector3(365f, 0f, 600f), 0f, CraftKind.Workboat);
            HarborCraft(s, new Vector3(400f, 0f, 612f), 0f, CraftKind.FuelBarge);
            HarborCraft(s, new Vector3(250f, 0f, 668f), 80f, CraftKind.Tug);   // 들어오는 예인선

            // 정박지 뒤 시설: 항무청(신호탑), 지원정 정비고, 항만 소방대, 연료 탱크
            var portOps = OfficeBlock(s, new Vector3(420f, Ground, 505f), 180f, 34f, 16f, 3, "wall_white", "roof_blue", "PortOperations");
            B(portOps, new Vector3(-12f, 18f, 0f), new Vector3(7f, 10f, 7f), "wall_white");
            B(portOps, new Vector3(-12f, 23.6f, 0f), new Vector3(8f, 1.2f, 8f), "glass_blue");
            B(portOps, new Vector3(-12f, 24.6f, 0f), new Vector3(8.6f, 0.6f, 8.6f), "roof_blue");
            C(portOps, new Vector3(-12f, 30f, 0f), new Vector3(0.4f, 10f, 0.4f), "steel");
            B(portOps, new Vector3(-12f, 33f, 0f), new Vector3(6f, 0.3f, 0.3f), "steel");
            Warehouse(s, new Vector3(240f, Ground, 500f), 180f, 60f, 34f, 14f, "wall_grey", "roof_blue", "BoatShop");
            FireStation(s, new Vector3(330f, Ground, 498f), 180f);
            TankFarm(s, new Vector3(560f, Ground, 420f), 0f, 3, 1, 16f);
            Reserve(500f, 380f, 640f, 470f);

            // 부유식 독: 정비 중인 초계함
            var dock = FloatingDock(s, new Vector3(640f, 0f, 650f), 0f, 170f);
            var ship = Place(EscortFolder + "ESC_ASW_T2.prefab", _root, new Vector3(640f, 0f, 650f), 0f, 0.92f / K);
            if (ship != null)
            {
                ship.name = "DockedShip_ASW";
                SitOnWater(ship);
                ship.transform.localPosition += Vector3.up * 5.5f;   // 독 위로 들어 올린 상태
            }
            _ = dock;
        }

        // ================================================================ 함정 정비창·보급 창고

        /// <summary>
        /// 컨테이너 야적장(x -370..-120, z 232..348): 컨테이너 블록 5줄 × 2열(줄마다 6×4칸), 줄 사이 통로 11m,
        /// 가운데·양 끝은 장비 통로(30m). 바닥에 칸 구획선(흰색)과 통로선(노란색), 리치스태커 3대·트레일러 트럭.
        /// </summary>
        private static void BuildContainerYard(Transform parent)
        {
            var y = G(parent, "ContainerYard", new Vector3(0f, Ground, 0f));
            const float top = 0.32f;
            B(y, new Vector3(-245f, 0.15f, 290f), new Vector3(250f, 0.3f, 116f), "concrete");
            void Line(float x, float z, float sx, float sz, string mat) => B(y, new Vector3(x, top, z), new Vector3(sx, 0.04f, sz), mat);

            // 가장자리 노란 띠
            Line(-245f, 233f, 248f, 0.5f, "mark_yellow"); Line(-245f, 347f, 248f, 0.5f, "mark_yellow");
            Line(-369f, 290f, 0.5f, 114f, "mark_yellow"); Line(-121f, 290f, 0.5f, 114f, "mark_yellow");

            float[] rowZ = { 246f, 268f, 290f, 312f, 334f };
            float[] colX = { -300f, -190f };
            const float halfX = 40.2f, halfZ = 6.2f;   // 블록(6칸 × 13.2m, 4열 × 2.8m) + 여유
            foreach (float bz in rowZ)
            foreach (float bx in colX)
            {
                ContainerBlock(y, new Vector3(bx, 0.3f, bz), 90f, 6, 4, bz == 290f ? 4 : 3);
                // 칸 구획: 테두리 + 칸 사이 가로선
                Line(bx, bz - halfZ, halfX * 2f, 0.3f, "mark_white"); Line(bx, bz + halfZ, halfX * 2f, 0.3f, "mark_white");
                for (int k = 0; k <= 6; k++) Line(bx - halfX + 0.6f + k * 13.2f, bz, 0.3f, halfZ * 2f, "mark_white");
            }
            // 줄 사이 통로: 노란 점선
            for (int i = 0; i < rowZ.Length - 1; i++)
            {
                float az = (rowZ[i] + rowZ[i + 1]) * 0.5f;
                foreach (float bx in colX)
                    for (float x = bx - 36f; x <= bx + 36f; x += 9f) Line(x, az, 5f, 0.35f, "mark_yellow");
            }
            // 장비 통로(가운데·동·서): 양옆 실선 + 가운데 점선
            foreach (float ax in new[] { -245f, -135f, -355f })
            {
                Line(ax - 12f, 290f, 0.4f, 108f, "mark_yellow"); Line(ax + 12f, 290f, 0.4f, 108f, "mark_yellow");
                for (float z = 240f; z <= 340f; z += 10f) Line(ax, z, 0.35f, 5f, "mark_white");
            }

            // 장비: 리치스태커(컨테이너를 가로로 물고 통로를 오감), 트레일러 트럭(실은 것/빈 섀시), 지게차
            Vehicle(y, new Vector3(-245f, 0.3f, 300f), 0f, VehicleKind.ReachStacker);
            Vehicle(y, new Vector3(-135f, 0.3f, 268f), 180f, VehicleKind.ReachStacker);
            Vehicle(y, new Vector3(-355f, 0.3f, 318f), 0f, VehicleKind.ReachStacker);
            Vehicle(y, new Vector3(-232f, 0.3f, 258f), 180f, VehicleKind.TrailerTruck);
            Vehicle(y, new Vector3(-258f, 0.3f, 252f), 0f, VehicleKind.TrailerEmpty);
            Vehicle(y, new Vector3(-126f, 0.3f, 316f), 0f, VehicleKind.TrailerTruck);
            Vehicle(y, new Vector3(-362f, 0.3f, 262f), 180f, VehicleKind.TrailerTruck);
            Vehicle(y, new Vector3(-320f, 0.3f, 343.5f), 90f, VehicleKind.TrailerTruck);
            Vehicle(y, new Vector3(-292f, 0.3f, 343.5f), 90f, VehicleKind.TrailerEmpty);
            Vehicle(y, new Vector3(-170f, 0.3f, 343.5f), 270f, VehicleKind.TrailerTruck);
            Vehicle(y, new Vector3(-140f, 0.3f, 238f), 90f, VehicleKind.Forklift);
        }

        private static void BuildIndustrial()
        {
            var ind = Group("Industrial");
            RepairShop(ind, new Vector3(-640f, Ground, 365f), 0f);
            Reserve(-720f, 300f, -480f, 420f);
            Warehouse(ind, new Vector3(-340f, Ground, 400f), 180f, 70f, 36f, 14f, "wall_sand", "roof_metal", "SupplyWarehouse");
            Warehouse(ind, new Vector3(-250f, Ground, 400f), 180f, 70f, 36f, 14f, "wall_sand", "roof_green", "SupplyWarehouse");
            Warehouse(ind, new Vector3(-160f, Ground, 400f), 180f, 70f, 36f, 14f, "wall_sand", "roof_metal", "SupplyWarehouse");
            Reserve(-380f, 375f, -120f, 430f);
            BuildContainerYard(ind);
            Reserve(-375f, 228f, -115f, 352f);
            // 차량 정비대: 군용 트럭 줄 + 정비고
            B(ind, new Vector3(-600f, Ground + 0.15f, 220f), new Vector3(150f, 0.3f, 110f), "concrete");
            Warehouse(ind, new Vector3(-640f, Ground, 250f), 180f, 60f, 26f, 11f, "wall_green", "roof_slate", "VehicleShop");
            for (int r = 0; r < 2; r++)
            for (int c = 0; c < 9; c++)
                Vehicle(ind, new Vector3(-660f + c * 13f, Ground + 0.3f, 195f - r * 20f), 0f, VehicleKind.MilitaryTruck);
            Reserve(-690f, 160f, -520f, 280f);
            WaterTower(ind, new Vector3(-470f, Ground, 200f));
            Reserve(-485f, 185f, -455f, 215f);
        }

        // ================================================================ 사령부·행정

        private static void BuildCommand()
        {
            var cmd = Group("Command");
            Headquarters(cmd, new Vector3(110f, Ground, 330f), 0f);
            Reserve(40f, 260f, 190f, 390f);
            OfficeBlock(cmd, new Vector3(-10f, Ground, 330f), 90f, 50f, 18f, 3, "wall_beige", "roof_green", "AdminA");
            OfficeBlock(cmd, new Vector3(-10f, Ground, 225f), 90f, 44f, 18f, 2, "wall_beige", "roof_green", "AdminB");
            OfficeBlock(cmd, new Vector3(240f, Ground, 400f), 0f, 40f, 16f, 2, "wall_white", "roof_blue", "CommsCenter");
            Reserve(-35f, 190f, 15f, 365f);
            Reserve(215f, 385f, 265f, 415f);
            for (int i = 0; i < 3; i++)
            {
                C(cmd, new Vector3(220f + i * 16f, Ground + 15f, 425f), new Vector3(0.7f, 30f, 0.7f), "crane_white");
                B(cmd, new Vector3(220f + i * 16f, Ground + 28f, 425f), new Vector3(5f, 0.3f, 0.3f), "crane_red");
            }
            ParkingLot(cmd, new Vector3(110f, Ground, 175f), 0f, 3, 22);
            ParkingLot(cmd, new Vector3(210f, Ground, 320f), 90f, 2, 12);
            ParkingLot(cmd, new Vector3(-10f, Ground, 410f), 0f, 1, 12);
            Reserve(25f, 150f, 195f, 200f);
            Reserve(185f, 285f, 240f, 355f);
            Reserve(-30f, 395f, 15f, 425f);
            Vehicle(cmd, new Vector3(150f, Ground, 140f), 90f, VehicleKind.Bus);
            Vehicle(cmd, new Vector3(60f, Ground, 140f), 90f, VehicleKind.Bus, "olive");
        }

        // ================================================================ 숙소·식당·체육

        private static void BuildHousing()
        {
            var h = Group("Housing");
            string[] walls = { "wall_beige", "wall_white", "wall_brick" };
            string[] roofs = { "roof_green", "roof_red", "roof_slate" };
            int n = 0;
            foreach (float z in new[] { 190f, 270f, 350f })
            foreach (float x in new[] { 320f, 415f })
            {
                Barracks(h, new Vector3(x, Ground, z), 0f, 72f, 16f, 4, walls[n % 3], roofs[n % 3]);
                B(h, new Vector3(x, Ground + 0.12f, z - 22f), new Vector3(76f, 0.24f, 10f), "sidewalk");
                n++;
            }
            Reserve(275f, 160f, 460f, 380f);
            // 식당·체육관·의무대·복지회관
            Warehouse(h, new Vector3(570f, Ground, 365f), 180f, 64f, 32f, 9f, "wall_white", "roof_green", "MessHall");
            Warehouse(h, new Vector3(570f, Ground, 290f), 180f, 56f, 38f, 15f, "wall_grey", "roof_blue", "Gym");
            OfficeBlock(h, new Vector3(680f, Ground, 385f), 180f, 44f, 18f, 2, "wall_white", "roof_red", "Clinic");
            OfficeBlock(h, new Vector3(560f, Ground, 205f), 0f, 40f, 18f, 1, "wall_beige", "roof_red", "Exchange");
            Reserve(530f, 180f, 610f, 390f);
            Reserve(655f, 370f, 705f, 400f);
            // 운동장: 육상 트랙 + 축구장, 농구장 넷
            var f = G(h, "SportsField", new Vector3(690f, Ground, 270f));
            C(f, new Vector3(0f, 0.15f, 0f), new Vector3(150f, 0.3f, 92f), "track");
            C(f, new Vector3(0f, 0.32f, 0f), new Vector3(128f, 0.3f, 70f), "grass_light");
            B(f, new Vector3(0f, 0.5f, 0f), new Vector3(90f, 0.05f, 0.6f), "mark_white");
            B(f, new Vector3(0f, 0.5f, 0f), new Vector3(0.6f, 0.05f, 56f), "mark_white");
            foreach (float s in new[] { -1f, 1f })
            {
                B(f, new Vector3(s * 45f, 0.5f, 0f), new Vector3(0.6f, 0.05f, 56f), "mark_white");
                B(f, new Vector3(s * 45.5f, 1.3f, 0f), new Vector3(0.4f, 2.4f, 7.3f), "trim_white");
            }
            C(f, new Vector3(0f, 0.52f, 0f), new Vector3(18f, 0.04f, 18f), "mark_white");
            C(f, new Vector3(0f, 0.54f, 0f), new Vector3(17f, 0.04f, 17f), "grass_light");
            Reserve(610f, 220f, 770f, 320f);
            for (int i = 0; i < 4; i++)
            {
                var court = G(h, "Court", new Vector3(650f + (i % 2) * 34f, Ground, 175f + (i / 2) * 22f));
                B(court, new Vector3(0f, 0.15f, 0f), new Vector3(30f, 0.3f, 18f), i < 2 ? "court_blue" : "court_green");
                B(court, new Vector3(0f, 0.32f, 0f), new Vector3(28f, 0.04f, 0.3f), "mark_white");
                B(court, new Vector3(0f, 0.32f, 0f), new Vector3(0.3f, 0.04f, 16f), "mark_white");
            }
            Reserve(630f, 160f, 705f, 210f);
            WaterTower(h, new Vector3(780f, Ground, 380f));
            ParkingLot(h, new Vector3(368f, Ground, 140f), 0f, 1, 40);
            Reserve(300f, 125f, 440f, 155f);
            Vehicle(h, new Vector3(500f, Ground, 330f), 0f, VehicleKind.Truck);
        }

        // ================================================================ 지원 지대(간선 도로와 비행장 사이)

        /// <summary>
        /// 간선 도로(z 110)와 비행장 지원 도로(z -50) 사이: 서쪽부터 교육대(훈련병 숙소·연병장·사격장) →
        /// 보급 물류 창고 → 관사 단지(가족 숙소·학교·교회) → 공공시설(발전소·변전소·태양광). 비행장 쪽 엄체호 뒤에는 항공 정비동.
        /// </summary>
        private static void BuildSupportBand()
        {
            var band = Group("SupportBand");

            // 교육대
            foreach (var (x, z) in new[] { (-770f, 78f), (-680f, 78f), (-770f, 40f) })
                Barracks(band, new Vector3(x, Ground, z), 0f, 70f, 14f, 3, "wall_beige", "roof_green");
            var drill = G(band, "DrillSquare", new Vector3(-660f, Ground, 22f));
            B(drill, new Vector3(0f, 0.15f, 0f), new Vector3(70f, 0.3f, 40f), "concrete_light");
            for (int i = -3; i <= 3; i++) B(drill, new Vector3(i * 9f, 0.32f, 0f), new Vector3(0.4f, 0.04f, 34f), "mark_white");
            Warehouse(band, new Vector3(-560f, Ground, 70f), 180f, 50f, 34f, 13f, "wall_grey", "roof_blue", "TrainingHall");
            // 사격장: 흙 방벽 사이 사로, 사대 건물, 표적
            var range = G(band, "FiringRange", new Vector3(-560f, Ground, 0f));
            B(range, new Vector3(0f, 0.12f, 0f), new Vector3(110f, 0.24f, 36f), "dirt");
            foreach (float z in new[] { -19f, 19f }) B(range, new Vector3(0f, 2.5f, z), new Vector3(110f, 5f, 4f), "grass_dark");
            B(range, new Vector3(56f, 4f, 0f), new Vector3(6f, 8f, 40f), "grass_dark");
            B(range, new Vector3(-50f, 2.2f, 0f), new Vector3(8f, 4.4f, 30f), "wall_white");
            B(range, new Vector3(-50f, 4.6f, 0f), new Vector3(9f, 0.4f, 31f), "roof_red");
            for (int i = 0; i < 6; i++) B(range, new Vector3(45f, 1.5f, -12.5f + i * 5f), new Vector3(0.3f, 2f, 1.4f), "trim_white");
            Reserve(-820f, -30f, -500f, 100f);

            // 보급 물류: 대형 창고 셋 + 트럭 대기장
            Warehouse(band, new Vector3(-355f, Ground, 60f), 180f, 76f, 40f, 15f, "wall_sand", "roof_metal", "LogisticsWarehouse");
            Warehouse(band, new Vector3(-265f, Ground, 60f), 180f, 76f, 40f, 15f, "wall_sand", "roof_red", "LogisticsWarehouse");
            Warehouse(band, new Vector3(-175f, Ground, 60f), 180f, 76f, 40f, 15f, "wall_sand", "roof_metal", "LogisticsWarehouse");
            B(band, new Vector3(-265f, Ground + 0.15f, 5f), new Vector3(270f, 0.3f, 44f), "concrete");
            for (int i = 0; i < 12; i++)
                Vehicle(band, new Vector3(-375f + i * 20f, Ground + 0.3f, 5f), 0f, i % 3 == 0 ? VehicleKind.MilitaryTruck : VehicleKind.Truck);
            ContainerBlock(band, new Vector3(-120f, Ground, 5f), 0f, 2, 4, 2);
            Reserve(-400f, -20f, -100f, 90f);

            // 관사 단지: 가족 숙소 줄 + 학교 + 교회 + 놀이터
            string[] roofs = { "roof_red", "roof_slate", "roof_green", "roof_blue", "roof_red" };
            string[] walls = { "wall_white", "wall_beige", "wall_white", "wall_sand" };
            int n = 0;
            for (float z = 75f; z >= -20f; z -= 31f)
            {
                for (float x = 0f; x <= 280f; x += 22f)
                {
                    if (x > 95f && x < 155f && z < 20f) continue;   // 학교 자리
                    House(band, new Vector3(x, Ground, z), 0f, walls[n % walls.Length], roofs[(n * 7 + (int)(x / 22f)) % roofs.Length], (n + (int)x) % 3 != 0);
                    n++;
                }
                B(band, new Vector3(140f, Ground + 0.14f, z - 15f), new Vector3(300f, 0.28f, 7f), "road");   // 골목
            }
            OfficeBlock(band, new Vector3(125f, Ground, -10f), 0f, 44f, 20f, 2, "wall_beige", "roof_red", "School");
            var chapel = G(band, "Chapel", new Vector3(330f, Ground, 50f));
            B(chapel, new Vector3(0f, 4f, 0f), new Vector3(14f, 8f, 24f), "wall_white");
            Gable(chapel, new Vector3(0f, 8f, 0f), 14f, 24f, 5f, "roof_slate");
            B(chapel, new Vector3(0f, 9f, -13f), new Vector3(5f, 18f, 5f), "wall_white");
            B(chapel, new Vector3(0f, 20f, -13f), new Vector3(2f, 4f, 2f), "roof_slate");
            var play = G(band, "Playground", new Vector3(330f, Ground, 0f));
            B(play, new Vector3(0f, 0.12f, 0f), new Vector3(30f, 0.24f, 24f), "sand");
            B(play, new Vector3(-6f, 2f, 0f), new Vector3(6f, 4f, 3f), "box_orange");
            B(play, new Vector3(6f, 1.2f, 4f), new Vector3(4f, 2.4f, 4f), "box_blue");
            Reserve(-15f, -40f, 360f, 95f);

            // 공공시설: 발전소(굴뚝), 변전소, 태양광 발전장
            var power = Warehouse(band, new Vector3(540f, Ground, 70f), 180f, 50f, 30f, 18f, "wall_grey", "roof_slate", "PowerPlant");
            C(power, new Vector3(18f, 22f, 8f), new Vector3(5f, 44f, 5f), "trim_white");
            for (int i = 0; i < 4; i++) C(power, new Vector3(18f, 30f + i * 4f, 8f), new Vector3(5.1f, 2f, 5.1f), i % 2 == 0 ? "crane_red" : "trim_white");
            var sub = G(band, "Substation", new Vector3(620f, Ground, 70f));
            B(sub, new Vector3(0f, 0.12f, 0f), new Vector3(44f, 0.24f, 34f), "gravel");
            for (int i = 0; i < 6; i++) B(sub, new Vector3(-15f + (i % 3) * 15f, 2f, -7f + (i / 3) * 14f), new Vector3(5f, 4f, 4f), "steel");
            for (int i = 0; i < 2; i++)
            {
                Beam(sub, new Vector3(-20f + i * 40f, 0f, 0f), new Vector3(-20f + i * 40f, 14f, 0f), 0.8f, "steel");
                B(sub, new Vector3(-20f + i * 40f, 13f, 0f), new Vector3(1f, 0.6f, 10f), "steel");
            }
            Fence(band, new Vector3(598f, 0f, 53f), new Vector3(642f, 0f, 53f));
            var solar = G(band, "SolarField", new Vector3(600f, Ground, -15f));
            for (int r = 0; r < 6; r++)
                B(solar, new Vector3(0f, 1.4f, -18f + r * 7.5f), new Vector3(130f, 0.2f, 4.2f), "solar", 0f, -25f);
            Reserve(505f, -45f, 680f, 95f);

            // 항공 정비동(AIMD)·엔진 시험동 — 엄체호 뒤
            Warehouse(band, new Vector3(-700f, Ground, -95f), 0f, 70f, 34f, 14f, "wall_grey", "roof_blue", "AircraftMaintenance");
            Warehouse(band, new Vector3(-610f, Ground, -95f), 0f, 70f, 34f, 14f, "wall_grey", "roof_slate", "AircraftMaintenance");
            OfficeBlock(band, new Vector3(-520f, Ground, -95f), 0f, 44f, 18f, 2, "wall_white", "roof_blue", "EngineTestCell");
            Reserve(-740f, -118f, -490f, -72f);

            // 정비창 옆 공병·기술 사무동
            OfficeBlock(band, new Vector3(-445f, Ground, 330f), 90f, 60f, 18f, 3, "wall_beige", "roof_slate", "EngineeringOffice");
            Reserve(-460f, 295f, -428f, 365f);
        }

        /// <summary>가족 관사: 박공지붕 단독주택 + 진입로(차 있음/없음). 정면 = -Z.</summary>
        private static void House(Transform parent, Vector3 at, float yaw, string wall, string roof, bool car)
        {
            var t = G(parent, "House", at, yaw);
            B(t, new Vector3(0f, 2f, 0f), new Vector3(13f, 4f, 9f), wall);
            Gable(t, new Vector3(0f, 4f, 0f), 9f, 13f, 3.2f, roof, 0.8f);
            t.Find("Roof").localRotation = Quaternion.Euler(0f, 90f, 0f);
            B(t, new Vector3(-2f, 1.4f, -4.56f), new Vector3(1.4f, 2.4f, 0.12f), "door_dark");
            B(t, new Vector3(3f, 2f, -4.56f), new Vector3(3f, 1.4f, 0.12f), "glass");
            B(t, new Vector3(6f, 0.1f, -8f), new Vector3(3.4f, 0.2f, 7f), "concrete");
            if (car) Vehicle(t, new Vector3(6f, 0.2f, -8f), 0f, VehicleKind.Car);
            B(t, new Vector3(-1f, 0.06f, -8.5f), new Vector3(10f, 0.12f, 7f), "grass_light");
        }

        /// <summary>
        /// 서쪽 해안 특수전 부대 영내(간선 도로 북쪽, 정비창 서쪽): 숙소 둘, 본부, 보트 창고·경사로(해안), 장애물 훈련장, 헬기 강하장.
        /// </summary>
        private static void BuildWestCompound()
        {
            var w = Group("SpecialWarfareCompound");
            Barracks(w, new Vector3(-830f, Ground, 170f), 90f, 60f, 14f, 3, "wall_sand", "roof_green");
            Barracks(w, new Vector3(-790f, Ground, 170f), 90f, 60f, 14f, 3, "wall_sand", "roof_green");
            OfficeBlock(w, new Vector3(-810f, Ground, 250f), 0f, 44f, 16f, 2, "wall_white", "roof_green", "UnitHQ");
            FlagPole(w, new Vector3(-810f, Ground, 232f));
            // 장애물 훈련장: 흙 바닥 위 담·그물·통나무
            var course = G(w, "ObstacleCourse", new Vector3(-800f, Ground, 310f));
            B(course, new Vector3(0f, 0.12f, 0f), new Vector3(50f, 0.24f, 70f), "dirt");
            for (int i = 0; i < 8; i++)
            {
                B(course, new Vector3(-12f, 1f + (i % 3) * 0.6f, -30f + i * 8f), new Vector3(10f, 2f + (i % 3) * 1.2f, 0.6f), "wood");
                B(course, new Vector3(12f, 1.5f, -30f + i * 8f), new Vector3(0.4f, 3f, 0.4f), "wood");
            }
            Beam(course, new Vector3(6f, 6f, -30f), new Vector3(6f, 6f, 30f), 0.4f, "wood");
            B(course, new Vector3(18f, 4f, 0f), new Vector3(3f, 8f, 3f), "wood");   // 레펠 탑
            HeliPad(w, new Vector3(-850f, Ground, 300f));
            // 보트 창고와 해안 경사로
            Warehouse(w, new Vector3(-805f, Ground, 372f), 180f, 40f, 24f, 10f, "wall_grey", "roof_green", "BoatHouse");
            B(w, new Vector3(-805f, 0.8f, 402f), new Vector3(14f, 1.2f, 30f), "concrete", 0f, -9f);
            HarborCraft(w, new Vector3(-835f, 0f, 432f), 20f, CraftKind.Launch);
            HarborCraft(w, new Vector3(-780f, 0f, 440f), 340f, CraftKind.Launch);
            Fence(w, new Vector3(-760f, 0f, 140f), new Vector3(-760f, 0f, 380f));
            GuardTower(w, new Vector3(-765f, Ground, 145f), 0f);
            ParkingLot(w, new Vector3(-850f, Ground, 125f), 90f, 1, 10);
            Reserve(-900f, 120f, -750f, 400f);
        }

        private static void FlagPole(Transform parent, Vector3 at)
        {
            var f = G(parent, "FlagPole", at);
            C(f, new Vector3(0f, 7f, 0f), new Vector3(0.4f, 14f, 0.4f), "trim_white");
            B(f, new Vector3(2.2f, 12.6f, 0f), new Vector3(4f, 2.6f, 0.12f), "flag_navy");
        }

        // ================================================================ 해군 항공대

        private static void BuildAirStation()
        {
            var air = Group("NavalAirStation");

            // 활주로(1,500 × 45m): 갓길, 가장자리선, 중심선, 활주로 시단 표지, 접지대·조준점 표지
            B(air, new Vector3(-50f, Ground + 0.2f, RunwayZ), new Vector3(1520f, 0.4f, 60f), "apron");
            B(air, new Vector3(-50f, Ground + 0.25f, RunwayZ), new Vector3(1500f, 0.5f, 45f), "runway");
            for (float x = -740f; x < 640f; x += 40f) B(air, new Vector3(x, Ground + 0.53f, RunwayZ), new Vector3(24f, 0.06f, 0.9f), "mark_white");
            foreach (float s in new[] { -1f, 1f })
                B(air, new Vector3(-50f, Ground + 0.53f, RunwayZ + s * 21.5f), new Vector3(1500f, 0.06f, 0.9f), "mark_white");
            foreach (float end in new[] { -790f, 690f })
            {
                float dir = end < 0f ? 1f : -1f;
                for (int i = 0; i < 12; i++)
                    if (i != 5 && i != 6) B(air, new Vector3(end + dir * 18f, Ground + 0.53f, RunwayZ - 19f + i * 3.45f), new Vector3(30f, 0.06f, 1.8f), "mark_white");
                foreach (float s in new[] { -1f, 1f })
                {
                    B(air, new Vector3(end + dir * 300f, Ground + 0.53f, RunwayZ + s * 9f), new Vector3(45f, 0.06f, 6f), "mark_white");   // 조준점
                    foreach (float tz in new[] { 150f, 450f, 600f })   // 접지대 표지
                        B(air, new Vector3(end + dir * tz, Ground + 0.53f, RunwayZ + s * 9f), new Vector3(22f, 0.06f, 1.8f), "mark_white");
                }
                // 진입등(활주로 밖 300m, 가로대 다섯)
                for (int k = 1; k <= 6; k++)
                {
                    B(air, new Vector3(end - dir * k * 22f, Ground + 0.6f, RunwayZ), new Vector3(1f, 0.8f, 1f), "lamp");
                    if (k % 2 == 0) B(air, new Vector3(end - dir * k * 22f, Ground + 0.6f, RunwayZ), new Vector3(1f, 0.6f, 18f), "steel_dark");
                }
                // PAPI(진입각 지시등) 넷
                for (int k = 0; k < 4; k++) B(air, new Vector3(end + dir * 120f, Ground + 0.8f, RunwayZ - 32f - k * 4f), new Vector3(2f, 1.2f, 1.6f), k < 2 ? "trim_white" : "safety_red");
            }
            Reserve(-860f, RunwayZ - 45f, 760f, RunwayZ + 45f);

            // 평행 유도로 + 연결 유도로
            B(air, new Vector3(-50f, Ground + 0.2f, TaxiZ), new Vector3(1420f, 0.4f, 23f), "runway");
            B(air, new Vector3(-50f, Ground + 0.45f, TaxiZ), new Vector3(1420f, 0.05f, 0.7f), "mark_yellow");
            foreach (float x in new[] { -760f, -380f, 0f, 330f, 660f })
            {
                B(air, new Vector3(x, Ground + 0.21f, (RunwayZ + TaxiZ) * 0.5f), new Vector3(23f, 0.42f, 50f), "runway");
                B(air, new Vector3(x, Ground + 0.46f, (RunwayZ + TaxiZ) * 0.5f), new Vector3(0.7f, 0.05f, 50f), "mark_yellow");
                for (int k = 0; k < 4; k++) B(air, new Vector3(x - 9f + k * 6f, Ground + 0.46f, TaxiZ - 15f), new Vector3(0.5f, 0.05f, 2f), "mark_yellow");   // 정지선
                B(air, new Vector3(x + 16f, Ground + 1.4f, TaxiZ - 16f), new Vector3(4f, 1.6f, 0.4f), "safety_yellow");   // 유도로 표지판
            }
            Reserve(-790f, TaxiZ - 15f, 690f, TaxiZ + 15f);

            // 주 계류장
            B(air, new Vector3(25f, Ground + 0.18f, -190f), new Vector3(710f, 0.36f, 104f), "apron");
            B(air, new Vector3(25f, Ground + 0.41f, -240f), new Vector3(710f, 0.05f, 0.6f), "mark_yellow");
            foreach (float x in new[] { -330f, 0f, 330f }) B(air, new Vector3(x, Ground + 0.19f, -248f), new Vector3(30f, 0.38f, 20f), "apron");
            Reserve(-330f, -245f, 385f, -135f);

            // 격납고 줄(문은 계류장 쪽): 정비 격납고 셋 + 아치 격납고 둘 + 헬기 격납고
            MaintenanceHangar(air, new Vector3(-268f, Ground, -102f), 0f, false);
            MaintenanceHangar(air, new Vector3(-153f, Ground, -102f), 0f, true, "roof_slate");
            MaintenanceHangar(air, new Vector3(-38f, Ground, -102f), 0f, false);
            ArchHangar(air, new Vector3(60f, Ground, -102f), 0f, 52f, 64f, "roof_metal", "wall_grey");
            ArchHangar(air, new Vector3(122f, Ground, -102f), 0f, 52f, 64f, "roof_metal", "wall_grey");
            Warehouse(air, new Vector3(220f, Ground, -102f), 0f, 70f, 50f, 14f, "wall_grey", "roof_blue", "HelicopterHangar");
            OfficeBlock(air, new Vector3(320f, Ground, -96f), 0f, 50f, 20f, 3, "wall_white", "roof_blue", "SquadronHQ");
            Reserve(-330f, -138f, 350f, -65f);

            // 항공기: 해상초계기(P-3C·P-8A), 조기경보기(E-2), 함재 전투기, 해상작전헬기
            P3C(air, new Vector3(-290f, Ground, -190f), 180f);
            P3C(air, new Vector3(-245f, Ground, -190f), 180f);
            P3C(air, new Vector3(-153f, Ground, -130f), 180f);   // 정비 격납고 앞(문이 열린 곳)
            P8A(air, new Vector3(-180f, Ground, -195f), 180f);
            P8A(air, new Vector3(-125f, Ground, -195f), 180f);
            E2(air, new Vector3(-55f, Ground, -185f), 180f);
            E2(air, new Vector3(-20f, Ground, -185f), 180f);
            Fighter(air, new Vector3(60f, Ground, -185f), 180f, "Fighter1");   // 시작 화면에서 이륙한다
            Fighter(air, new Vector3(85f, Ground, -185f), 180f);
            Fighter(air, new Vector3(110f, Ground, -185f), 180f);
            Fighter(air, new Vector3(135f, Ground, -185f), 180f);
            foreach (float x in new[] { -290f, -245f, -180f, -125f, -55f, -20f, 85f, 110f, 135f })
                Stand(air, new Vector3(x, Ground, -205f), 0f, 40f);
            P3C(air, new Vector3(-470f, Ground, TaxiZ), 90f, "P3C_Taxi");   // 유도로를 따라 나가는 초계기
            for (int i = 0; i < 3; i++)
            {
                HeliPad(air, new Vector3(210f + i * 42f, Ground, -195f));
                if (i < 2) Helicopter(air, new Vector3(210f + i * 42f, Ground + 0.4f, -195f), 160f, true, "MH60");
                else Helicopter(air, new Vector3(210f + i * 42f, Ground + 0.4f, -195f), 200f, false, "Lynx");
            }
            Helicopter(air, new Vector3(320f, Ground + 0.4f, -205f), 180f, false, "Lynx");
            // 지상 장비
            Vehicle(air, new Vector3(-230f, Ground, -160f), 90f, VehicleKind.FuelTruck);
            Vehicle(air, new Vector3(-100f, Ground, -165f), 0f, VehicleKind.TowTractor);
            Vehicle(air, new Vector3(30f, Ground, -170f), 90f, VehicleKind.FuelTruck);
            Vehicle(air, new Vector3(160f, Ground, -170f), 0f, VehicleKind.TowTractor);
            Vehicle(air, new Vector3(-280f, Ground, -150f), 0f, VehicleKind.Truck);

            // 강화 엄체호(전투기) 넷 — 비행장 서쪽, 각자 유도로로 이어짐
            for (int i = 0; i < 4; i++)
            {
                float x = -700f + i * 58f;
                ArchHangar(air, new Vector3(x, Ground, -150f), 0f, 30f, 36f, "concrete", "plinth", "AircraftShelter");
                B(air, new Vector3(x, Ground + 0.19f, -205f), new Vector3(20f, 0.38f, 90f), "runway");
                if (i % 2 == 0) Fighter(air, new Vector3(x, Ground, -185f), 180f);
            }
            Reserve(-730f, -250f, -500f, -125f);

            // 관제탑·운항동, 소방대, 헬기 계류장 동쪽의 항공 정비 지원동
            ControlTower(air, new Vector3(470f, Ground, -150f), 0f);
            FireStation(air, new Vector3(560f, Ground, -150f), 0f);
            Reserve(420f, -185f, 600f, -120f);
            ParkingLot(air, new Vector3(-380f, Ground, -90f), 0f, 3, 16);
            Reserve(-410f, -115f, -350f, -65f);

            // 방위 보정장(컴퍼스 로즈)·엔진 시운전장·세척장·풍향계
            var rose = G(air, "CompassRose", new Vector3(-470f, Ground, -170f));
            C(rose, new Vector3(0f, 0.2f, 0f), new Vector3(44f, 0.4f, 44f), "apron");
            C(rose, new Vector3(0f, 0.42f, 0f), new Vector3(36f, 0.04f, 36f), "mark_white");
            C(rose, new Vector3(0f, 0.44f, 0f), new Vector3(34f, 0.04f, 34f), "apron");
            for (int i = 0; i < 4; i++) B(rose, new Vector3(0f, 0.46f, 0f), new Vector3(0.6f, 0.04f, 40f), "mark_white", i * 45f);
            Reserve(-495f, -195f, -445f, -145f);
            B(air, new Vector3(450f, Ground + 0.18f, -215f), new Vector3(60f, 0.36f, 40f), "apron");   // 시운전장
            foreach (float s in new[] { -1f, 1f }) B(air, new Vector3(450f + s * 31f, Ground + 3f, -215f), new Vector3(1f, 6f, 40f), "concrete");
            Reserve(415f, -240f, 485f, -190f);
            var sock = G(air, "Windsock", new Vector3(-50f, Ground, RunwayZ - 60f));
            C(sock, new Vector3(0f, 4f, 0f), new Vector3(0.3f, 8f, 0.3f), "trim_white");
            C(sock, new Vector3(1.6f, 7.6f, 0f), new Vector3(0.9f, 3f, 0.9f), "box_orange", 0f, 0f, 80f);
        }

        // ================================================================ 레이더·탄약고·항공유

        private static void BuildRadarAndOrdnance()
        {
            var r = Group("RadarAndOrdnance");
            SearchRadar(r, new Vector3(120f, Ground, -440f), 30f);
            Fence(r, new Vector3(95f, 0f, -420f), new Vector3(150f, 0f, -420f));
            Reserve(85f, -460f, 160f, -410f);
            RadomeSite(r, new Vector3(830f, 12f, 20f), 0f);
            Reserve(760f, -40f, 900f, 80f);
            // 항공유 저장소(비행장 동쪽 가장자리)
            TankFarm(r, new Vector3(650f, Ground, -140f), 0f, 3, 2, 18f);
            Reserve(590f, -200f, 720f, -70f);
            // 탄약고(이글루) — 활주로 동쪽 끝 너머 따로, 철조망으로 둘러쌈
            for (int i = 0; i < 6; i++)
                Igloo(r, new Vector3(790f + (i % 3) * 42f, Ground, -165f - (i / 3) * 62f), 0f);
            B(r, new Vector3(832f, Ground + 0.15f, -230f), new Vector3(130f, 0.3f, 14f), "road");
            Fence(r, new Vector3(760f, 0f, -125f), new Vector3(905f, 0f, -125f));
            Fence(r, new Vector3(760f, 0f, -125f), new Vector3(760f, 0f, -300f));
            Fence(r, new Vector3(760f, 0f, -300f), new Vector3(905f, 0f, -300f));
            GuardTower(r, new Vector3(765f, Ground, -130f), 45f);
            Reserve(755f, -305f, 910f, -120f);
        }

        // ================================================================ 해안 경계

        private static void BuildPerimeter()
        {
            var per = Group("Perimeter");
            var ring = Outline(0.935f, Ground);
            for (int i = 0; i < ring.Length; i++)
            {
                var a = ring[i]; var b = ring[(i + 1) % ring.Length];
                bool harbor = (a.z > 380f && a.x < 500f && a.x > -740f) || (b.z > 380f && b.x < 500f && b.x > -740f);
                if (!harbor) Fence(per, new Vector3(a.x, 0f, a.z), new Vector3(b.x, 0f, b.z), 2.2f);
                if (i % 8 == 3 && !harbor) GuardTower(per, a + (Vector3.zero - a).normalized * 6f + Vector3.up * (Ground - a.y), R(0f, 90f));
            }
        }

        // ================================================================ 조경

        private static void BuildGreenery()
        {
            var g = Group("Greenery");
            // 잔디 짙은·옅은 조각으로 넓은 녹지에 결을 준다. 같은 높이라 서로 겹치면 깜빡이므로 겹치는 자리는 건너뛴다
            var patches = new List<Vector3>();   // x, z, 반지름
            for (int i = 0; i < 40; i++)
            {
                float x = R(-880f, 880f), z = R(-480f, 440f);
                float sx = R(40f, 110f), sz = R(30f, 80f), radius = Mathf.Max(sx, sz) * 0.5f;
                if (!InsideIsland(x, z, 0.9f) || IsReserved(x, z)) continue;
                bool overlaps = false;
                foreach (var p in patches) if (new Vector2(p.x - x, p.y - z).magnitude < p.z + radius) { overlaps = true; break; }
                if (overlaps) continue;
                patches.Add(new Vector3(x, z, radius));
                var patch = C(g, new Vector3(x, Ground + 0.03f, z), new Vector3(sx, 0.06f, sz), R(0f, 1f) < 0.5f ? "grass_dark" : "grass_light");
                patch.transform.localRotation = Quaternion.Euler(0f, R(0f, 180f), 0f);
            }
            // 나무 군락: 격자 위 노이즈로 모아 심는다(시설·도로 자리는 피함)
            for (float x = -900f; x <= 900f; x += 16f)
            for (float z = -500f; z <= 460f; z += 16f)
            {
                float jx = x + R(-7f, 7f), jz = z + R(-7f, 7f);
                if (!InsideIsland(jx, jz, 0.915f) || IsReserved(jx, jz)) continue;
                float cluster = Mathf.PerlinNoise(jx * 0.006f + 13f, jz * 0.006f + 7f);
                if (cluster < 0.47f || R(0f, 1f) > (cluster - 0.4f) * 2.4f) continue;
                Tree(g, new Vector3(jx, Ground, jz), R(9f, 15f), R(0f, 1f) < 0.35f);
            }
            // 가로수: 간선 도로 양옆
            for (float x = -860f; x <= 860f; x += 22f)
                foreach (float z in new[] { 128f, 92f })
                    if (!IsReservedExceptRoads(x, z)) Tree(g, new Vector3(x + R(-2f, 2f), Ground, z), R(8f, 10f), false);
        }

        private static bool IsReservedExceptRoads(float x, float z)
        {
            // 도로 자리(첫 8개)는 빼고 본다 — 가로수는 보도 바깥 줄에 선다
            for (int i = 8; i < Reserved.Count; i++) if (Reserved[i].Contains(new Vector2(x, z))) return true;
            return false;
        }

        private static bool InsideIsland(float x, float z, float scale)
        {
            float ex = Mathf.Abs(x) / (1000f * scale), ez = Mathf.Abs(z + 10f) / (520f * scale);
            return Mathf.Pow(ex, 4.2f) + Mathf.Pow(ez, 3.2f) < 0.92f;
        }

        // ================================================================ 메시 합치기

        /// <summary>
        /// 같은 재질의 정적 조각을 하나의 메시로 합친다(수천 개 → 재질 수만큼). 시작 화면은 이 섬을 매 프레임 그린다.
        /// 시작 화면이 찾아 쓰는 것(돌제·계류 함정·순찰 고속정·이륙 전투기·독 위 함정·야간 조명)은 그대로 둔다.
        /// </summary>
        private static void CombineStatic()
        {
            var keep = new HashSet<Transform>();
            foreach (var t in _root.GetComponentsInChildren<Transform>(true))
                if (t.name is "Pier" or "Berths" or "Fighter1" or "NightLights" or "RoadTraffic" || t.name.StartsWith("ESC_PB") || t.name.StartsWith("DockedShip"))
                    keep.Add(t);
            bool Kept(Transform t)
            {
                for (var p = t; p != null && p != _root; p = p.parent) if (keep.Contains(p)) return true;
                return false;
            }

            var byMat = new Dictionary<Material, List<CombineInstance>>();
            var done = new List<GameObject>();
            var toLocal = _root.worldToLocalMatrix;
            foreach (var mr in _root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (Kept(mr.transform)) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || mr.sharedMaterial == null) continue;
                if (!byMat.TryGetValue(mr.sharedMaterial, out var list)) byMat[mr.sharedMaterial] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = 0, transform = toLocal * mr.transform.localToWorldMatrix });
                done.Add(mr.gameObject);
            }

            var holder = Group("Combined");
            int meshes = 0, parts = done.Count;
            foreach (var kv in byMat)
            {
                var chunk = new List<CombineInstance>();
                int verts = 0, part = 0;
                void Flush()
                {
                    if (chunk.Count == 0) return;
                    string name = $"{kv.Key.name}_{part}";
                    var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(chunk.ToArray(), true, true);
                    mesh.RecalculateBounds();
                    string path = $"{MatFolder}/MESH_combined_{name}.asset";
                    AssetDatabase.DeleteAsset(path);
                    AssetDatabase.CreateAsset(mesh, path);
                    var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(holder, false);
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    go.GetComponent<MeshRenderer>().sharedMaterial = kv.Key;
                    go.isStatic = true;
                    chunk.Clear(); verts = 0; part++; meshes++;
                }
                foreach (var ci in kv.Value)
                {
                    chunk.Add(ci);
                    verts += ci.mesh.vertexCount;
                    if (verts > 400000) Flush();
                }
                Flush();
            }
            foreach (var go in done) if (go != null) Object.DestroyImmediate(go);
            RemoveEmpty(_root, keep);
            Debug.Log($"[NavalBase] 조각 {parts}개 → 합친 메시 {meshes}개(재질 {byMat.Count}종)");
        }

        private static void RemoveEmpty(Transform t, HashSet<Transform> keep)
        {
            for (int i = t.childCount - 1; i >= 0; i--) RemoveEmpty(t.GetChild(i), keep);
            if (t == _root || keep.Contains(t) || t.name is "Harbor" or "NavalAirStation" or "Combined") return;
            if (t.childCount == 0 && t.GetComponents<Component>().Length == 1) Object.DestroyImmediate(t.gameObject);
        }
    }
}

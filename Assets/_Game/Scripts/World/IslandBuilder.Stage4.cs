using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// 내해(4스테이지, 2026-11-16) 구조물: Codex Stage4Ekranoplan_v1의 유전 플랫폼 · 모래톱 표지(Resources/Environment/Stage4).
    ///   - 유전 플랫폼(대 22m · 소 12m 갑판): 기둥 4개에만 판정(배는 갑판 아래로 못 지나간다 — 기둥 사이가 좁다).
    ///     가스 불기둥(FlareTip)이 타오르고 크레인이 천천히 돈다. 무장한 플랫폼은 갑판 가장자리 받침(BatteryMount)에 플랫폼 포대가 선다
    ///     (CoastalBatteryField — IslandInfo.BatterySpots · BatteryEnemy). 포대가 부서지면 수면에 불타는 기름띠(OilSlick).
    ///   - 모래톱: 판정 없음. 해안 물빛(ShoreWaterMask)이 얕은 물색으로 칠하고 둘레에 노란 표지 부표. 기함은 지나가면 느려진다.
    /// </summary>
    public static partial class IslandBuilder
    {
        private const string Stage4Env = "Environment/Stage4/";
        public const string PlatformBatteryPath = "Enemies/ene_platform_battery";

        /// <summary>유전 플랫폼 하나. armed면 포대 자리(대 2 · 소 1)를 IslandInfo.BatterySpots로 넘긴다.</summary>
        public static IslandInfo BuildPlatform(Vector3 center, bool large, bool armed, System.Random rng, Transform parent)
        {
            var model = PlaceKitModel(parent, Stage4Env + (large ? "ENV_OilPlatform_L" : "ENV_OilPlatform_S"),
                                      new Vector3(center.x, 0f, center.z), (float)rng.NextDouble() * 360f);
            if (model == null) return null;
            model.name = large ? "Oil platform (L)" : "Oil platform (S)";
            var root = model.transform;
            Transform F(string n) { foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t; return null; }

            // 기둥 판정: 기둥 메시 범위를 플랫폼 루트 축으로 상자
            var legs = new List<Vector2>();
            for (int i = 1; i <= 4; i++)
            {
                var leg = F($"Leg_0{i}");
                if (leg == null) continue;
                var b = WorldBounds(leg);
                var box = new GameObject($"Leg collider {i}").AddComponent<BoxCollider>();
                box.transform.SetParent(root, true);
                box.transform.SetPositionAndRotation(new Vector3(b.center.x, 2f, b.center.z), Quaternion.identity);
                box.transform.localScale = Vector3.one;
                // 모델 루트 축척(FBX 단위 · 회전)과 무관하게 월드 크기로 맞춘다
                Vector3 ls = box.transform.lossyScale;
                Vector3 world = new Vector3(Mathf.Max(1f, b.size.x), 12f, Mathf.Max(1f, b.size.z));
                box.size = new Vector3(world.x / Mathf.Max(1e-4f, Mathf.Abs(ls.x)), world.y / Mathf.Max(1e-4f, Mathf.Abs(ls.y)), world.z / Mathf.Max(1e-4f, Mathf.Abs(ls.z)));
                legs.Add(new Vector2(b.center.x, b.center.z));
            }

            // 불기둥 · 크레인
            var flare = F("FlareTip");
            if (flare != null) model.AddComponent<FlareFlame>().Init(flare, large ? 1f : 0.7f);
            var jib = F("CraneJib");
            if (jib != null) model.AddComponent<Game.View.PartSpin>().Configure(new[] { jib }, Game.View.PartSpin.Axis.Up, 1.2f);

            float radius = large ? 15.5f : 9f;
            var rim = new Vector2[Mathf.Max(4, legs.Count)];
            for (int i = 0; i < rim.Length; i++) rim[i] = i < legs.Count ? legs[i] : new Vector2(center.x, center.z);
            var slick = F("OilSlickPoint");
            var info = new IslandInfo
            {
                Center = new Vector3(center.x, 0f, center.z), Radius = radius, Height = large ? 23f : 10f, Rocky = false,
                Rim = rim, Root = model, Art = model.name, Platform = true,
                OilSlickPoint = slick != null ? new Vector3(slick.position.x, 0f, slick.position.z) : new Vector3(center.x, 0f, center.z),
            };
            if (armed)
            {
                var spots = new List<Vector3>();
                for (int i = 1; i <= 2; i++) { var m = F($"BatteryMount_0{i}"); if (m != null) spots.Add(m.position); }
                if (spots.Count > 0)
                {
                    info.MilitaryBase = true;
                    info.BatterySpots = spots.ToArray();
                    info.BatteryEnemy = PlatformBatteryPath;
                }
            }
            SetLayer(model, Islands.Layer);
            Islands.Register(info);
            return info;
        }

        /// <summary>모래톱 하나(반경 radius). 판정 없음 — 표지 부표 3~4개, 해안 물빛이 얕은 물색으로 칠한다.</summary>
        public static IslandInfo BuildSandbar(Vector3 center, float radius, System.Random rng, Transform parent)
        {
            LoadMaterials();
            var root = new GameObject("Sandbar");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(center.x, 0f, center.z);
            root.AddComponent<KeepSharedMeshes>();
            int n = 3 + rng.Next(2);
            float a0 = (float)rng.NextDouble() * Mathf.PI * 2f;
            for (int i = 0; i < n; i++)
            {
                float a = a0 + i * Mathf.PI * 2f / n;
                var p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (radius + 1.5f);
                var buoy = PlaceKitModel(root.transform, Stage4Env + "ENV_Sandbar_Marker", p, (float)rng.NextDouble() * 360f);
                if (buoy != null)
                {
                    buoy.transform.localPosition = new Vector3(p.x, 0f, p.z);
                    // 등부표(2026-11-19): 모래톱 표지 — 밤에 노랗게 깜박인다
                    Game.View.NightLampSet.On(buoy).Add(Vector3.up * 2f / Mathf.Max(0.01f, buoy.transform.lossyScale.y), new Color(1f, 0.8f, 0.15f), 0.45f, 3f, 0.3f, (float)rng.NextDouble());
                }
            }
            // 불규칙한 윤곽(해안 물빛이 얕은 물로 칠한다)
            const int k = 20;
            var rim = new Vector2[k];
            float phase = (float)rng.NextDouble() * 6f;
            for (int i = 0; i < k; i++)
            {
                float a = i * Mathf.PI * 2f / k;
                float r = radius * (1f + 0.18f * Mathf.Sin(a * 2f + phase) + 0.08f * Mathf.Sin(a * 5f + phase * 1.7f)) * 0.85f;
                rim[i] = new Vector2(center.x + Mathf.Cos(a) * r, center.z + Mathf.Sin(a) * r);
            }
            if (!Game.View.ShoreWaterMask.Enabled) AddFoamRing(root.transform, radius * 0.85f, rng);
            var info = new IslandInfo { Center = root.transform.position, Radius = radius, Height = 0f, Rim = rim, Root = root, Sandbar = true };
            Islands.Register(info);
            return info;
        }

        private static Bounds WorldBounds(Transform t)
        {
            bool any = false;
            var b = new Bounds(t.position, Vector3.zero);
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }
    }
}

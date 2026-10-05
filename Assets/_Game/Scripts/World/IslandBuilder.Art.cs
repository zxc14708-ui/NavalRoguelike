using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Codex 섬·해안 소품(Resources/Environment, NavalEnvironmentArtBuilder가 만든다)으로 섬을 만든다.
    ///   - 섬: 바위섬 35%는 ENV_RockIslet, 무인도 65%는 소나무섬·등대섬·난파선 만·레이더 기지(시드 변형 4개를 한 종류로)·해안 초소·항구섬·환초 중 하나.
    ///     축척 0.9~1.1, 방향은 무작위. 판정은 프리팹의 볼록 조각(환초 석호는 비어 있어 배가 들어갈 수 있다).
    ///   - 해안 소품(자동 생성 섬에만): 무인도 — 어선(잔교가 있으면 잔교 옆에 계류), 부잔교, 방파제(1~2칸), 해변 어구(절차형 섬),
    ///     바위섬 — 해식 아치, 절차형 섬 — 항로 부표. 어선·부표는 물결에 출렁인다.
    /// 프리팹이 없거나 ArtDisabled면 절차형 섬만 만든다.
    /// </summary>
    public static partial class IslandBuilder
    {
        /// <summary>검증용: 켜면 Codex 섬·해안 소품을 쓰지 않는다(절차형만).</summary>
        public static bool ArtDisabled;

        private const double RockArtChance = 0.35, GreenArtChance = 0.65;

        private static List<EnvironmentArt> s_rockArt, s_greenArt;
        private static readonly Dictionary<string, GameObject> s_props = new();

        private static void LoadArt()
        {
            if (s_rockArt != null) return;
            s_rockArt = new List<EnvironmentArt>();
            s_greenArt = new List<EnvironmentArt>();
            foreach (var go in Resources.LoadAll<GameObject>("Environment/Islands"))
            {
                var art = go.GetComponent<EnvironmentArt>();
                if (art == null || !art.island || art.rimRadii == null || art.rimRadii.Length == 0) continue;
                (art.rocky ? s_rockArt : s_greenArt).Add(art);
            }
            s_rockArt.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            s_greenArt.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }

        /// <summary>모든 Codex 섬 프리팹(검증용).</summary>
        public static IEnumerable<EnvironmentArt> ArtIslands
        {
            get
            {
                LoadArt();
                foreach (var a in s_rockArt) yield return a;
                foreach (var a in s_greenArt) yield return a;
            }
        }

        public static EnvironmentArt FindArt(string name)
        {
            foreach (var a in ArtIslands) if (a.name == name) return a;
            return null;
        }

        /// <summary>
        /// 이 자리에 Codex 섬을 쓸지 정한다(쓰지 않으면 null). 난수는 늘 같은 수만큼 쓴다(배치 재현).
        /// </summary>
        public static EnvironmentArt PickArt(bool rocky, System.Random rng, out float scale)
        {
            // 구역 난수는 비슷한 시드에서 시작해 앞쪽 값끼리 닮는다 — 섞어서 쓴다(그대로 쓰면 한 종류로 몰림)
            double roll = Mix01(rng.Next()), pick = Mix01(rng.Next()), size = Mix01(rng.Next());
            scale = 1f;
            if (ArtDisabled) return null;
            LoadArt();
            var list = rocky ? s_rockArt : s_greenArt;
            if (list.Count == 0 || roll >= (rocky ? RockArtChance : GreenArtChance)) return null;

            // 레이더 기지는 같은 섬의 시드 변형이 여러 개 — 묶어서 한 종류만큼만 나오게
            int radar = 0;
            foreach (var a in list) if (a.name.StartsWith("ENV_RadarOutpost")) radar++;
            float Weight(EnvironmentArt a) => a.name.StartsWith("ENV_RadarOutpost") ? 1f / radar : 1f;
            float total = 0f;
            foreach (var a in list) total += Weight(a);
            float x = (float)pick * total;
            EnvironmentArt chosen = list[list.Count - 1];
            foreach (var a in list)
            {
                x -= Weight(a);
                if (x < 0f) { chosen = a; break; }
            }
            scale = Mathf.Lerp(0.9f, 1.1f, (float)size);
            return chosen;
        }

        /// <summary>Codex 섬 하나. 수면선(-0.9)은 축척과 상관없이 맞춘다.</summary>
        public static IslandInfo BuildArt(Vector3 center, EnvironmentArt art, float scale, System.Random rng, Transform parent)
        {
            LoadMaterials();
            LoadDecorMaterials();
            var root = new GameObject($"Island ({art.name})");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(new Vector3(center.x, 0f, center.z), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));

            var inst = Object.Instantiate(art.gameObject, root.transform, false);
            inst.name = "Art";
            inst.transform.localPosition = new Vector3(0f, SeaY * (1f - scale), 0f);
            inst.transform.localScale = Vector3.one * scale;
            var shape = inst.GetComponent<EnvironmentArt>();

            AddFoamRing(root.transform, a => shape.RimAt(a) * scale, rng);

            // 등대 불빛(기존 등대와 같은 4초 회전등)
            if (s_glow != null && shape.lamps != null)
                foreach (var lamp in shape.lamps)
                {
                    if (lamp == null) continue;
                    var glow = new GameObject("Lamp glow", typeof(MeshFilter), typeof(MeshRenderer));
                    glow.transform.SetParent(root.transform, false);   // 모델 밖(섬을 치울 때 런타임 메시로 지워진다)
                    glow.transform.position = lamp.position;
                    glow.GetComponent<MeshFilter>().sharedMesh = GlowQuad(new Color(1f, 0.9f, 0.55f, 1f));
                    var r = glow.GetComponent<MeshRenderer>();
                    r.sharedMaterial = s_glow;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    if (!root.TryGetComponent<IslandLights>(out var lights)) lights = root.AddComponent<IslandLights>();
                    lights.Add(glow.transform, (float)rng.NextDouble() * 4f);
                }

            int n = shape.rimRadii.Length;
            var rim = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                float r = shape.rimRadii[i] * scale * 0.9f;
                var w = root.transform.TransformPoint(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                rim[i] = new Vector2(w.x, w.z);
            }

            SetLayer(root, Islands.Layer);
            var info = new IslandInfo
            {
                Center = root.transform.position, Radius = shape.shoreRadius * scale, Height = shape.height * scale, Rocky = shape.rocky,
                Rim = rim, Root = root, Lighthouse = shape.lighthouse, Wreck = shape.wreck, Seals = shape.seals, Art = art.name,
            };
            Islands.Register(info);
            return info;
        }

        // ------------------------------------------------------------ 해안 소품

        private static GameObject Prop(string name)
        {
            if (!s_props.TryGetValue(name, out var go))
            {
                go = Resources.Load<GameObject>($"Environment/Props/{name}");
                s_props[name] = go;
            }
            return go;
        }

        /// <summary>
        /// 자동 생성 섬 둘레에 해안 소품을 놓는다(검증용 수동 배치 섬에는 놓지 않는다). 난수는 늘 같은 수만큼 쓴다.
        /// 놓은 소품까지 덮도록 섬 반경(간격 판정)을 넓힌다.
        /// </summary>
        public static void DressCoast(IslandInfo info, System.Random rng)
        {
            var r = new double[12];
            for (int i = 0; i < r.Length; i++) r[i] = Mix01(rng.Next());
            if (ArtDisabled || info == null || info.Root == null) return;

            var root = info.Root.transform;
            var art = root.GetComponentInChildren<EnvironmentArt>();
            float scale = art != null && art.island ? art.transform.localScale.x : 1f;
            bool hasArt = art != null && art.island;
            float Rim(float a) => hasArt ? art.RimAt(a) * scale : info.Radius * 0.92f;
            float Angle(double t) => (float)t * Mathf.PI * 2f;
            Physics.SyncTransforms();
            float reach = info.Radius;
            int placed = 0;

            void Count(Vector3 local, float extent)
            {
                placed++;
                reach = Mathf.Max(reach, new Vector2(local.x, local.z).magnitude + extent);
            }

            if (!info.Rocky)
            {
                // 어선: 잔교가 있으면 잔교 끝 옆에 대고, 없으면 해안 앞바다에
                if (r[0] < 0.35)
                {
                    var ends = hasArt ? art.jettyEnds : null;
                    if (ends != null && ends.Length > 0 && ends[0] != null && r[1] < 0.6)
                    {
                        var end = ends[0];
                        Vector3 along = end.position - end.parent.position; along.y = 0f;
                        if (along.sqrMagnitude > 0.01f)
                        {
                            along.Normalize();
                            Vector3 side = Vector3.Cross(Vector3.up, along) * (r[2] < 0.5 ? 2.9f : -2.9f);
                            Vector3 pos = end.position - along * 1.2f + side;
                            if (PlaceProp("ENV_FishingBoat", root, pos, along, "MooringPoint_01", 1.0f) != null)
                                Count(root.InverseTransformPoint(pos), 4f);
                        }
                    }
                    else
                    {
                        float a = Angle(r[3]);
                        var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        Vector3 local = dir * (Rim(a) + 4.5f);
                        Vector3 heading = Vector3.Cross(Vector3.up, dir) * (r[2] < 0.5 ? 1f : -1f);
                        if (PlaceProp("ENV_FishingBoat", root, root.TransformPoint(local), root.TransformDirection(heading), "MooringPoint_01", 1.6f) != null)
                            Count(local, 4f);
                    }
                }

                // 부잔교(잔교가 없는 섬): 육지 쪽 연결점을 해안에 대고 바다 쪽으로
                bool hasJetty = hasArt && art.jettyEnds != null && art.jettyEnds.Length > 0;
                if (!hasJetty && r[4] < 0.22)
                {
                    float a = Angle(r[5]);
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var shoreLocal = dir * Rim(a) * 0.93f;
                    if (PlaceAnchored("ENV_FloatingPontoon", root, root.TransformPoint(shoreLocal), root.TransformDirection(dir), "ShoreConnection") != null)
                        Count(shoreLocal + dir * 9.5f, 2f);
                }

                // 방파제: 앞바다에 해안과 나란히(35%는 두 칸)
                if (r[6] < 0.16)
                {
                    float a = Angle(r[7]);
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var tangent = Vector3.Cross(Vector3.up, dir);
                    var local = dir * (Rim(a) + 9f);
                    bool two = r[8] < 0.35;
                    if (two) local -= tangent * 6f;
                    var first = PlaceProp("ENV_Breakwater", root, root.TransformPoint(local), root.TransformDirection(tangent), "Snap_End", 2.6f);
                    if (first != null)
                    {
                        Count(local, 6.5f);
                        if (two)
                        {
                            var l2 = local + tangent * 12f;
                            if (PlaceProp("ENV_Breakwater", root, root.TransformPoint(l2), root.TransformDirection(tangent), "Snap_End", 2.6f) != null) Count(l2, 6.5f);
                        }
                    }
                }

                // 해변 어구(절차형 섬 — 땅 높이를 섬 판정에서 읽는다)
                if (!hasArt && r[9] < 0.3)
                {
                    for (int tries = 0; tries < 6; tries++)
                    {
                        float a = Angle((r[10] + tries * 0.17) % 1.0);
                        var local = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * info.Radius * 0.68f;
                        if (!Ground(root, local, out float y, out Vector3 nrm) || nrm.y < 0.85f || y < SeaY + 0.25f || y > 1.2f) continue;
                        var prefab = Prop("ENV_BeachFishingGear");
                        if (prefab == null) break;
                        var go = Object.Instantiate(prefab, root, false);
                        go.name = "Coast ENV_BeachFishingGear";
                        go.transform.localPosition = new Vector3(local.x, y - 0.05f, local.z);
                        go.transform.localRotation = Quaternion.LookRotation(local.normalized, Vector3.up);
                        placed++;
                        break;
                    }
                }
            }
            else if (r[0] < 0.3)
            {
                // 해식 아치: 바위섬 앞바다
                float a = Angle(r[3]);
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var local = dir * (Rim(a) + 6f);
                var yaw = Quaternion.Euler(0f, (float)r[2] * 360f, 0f) * Vector3.forward;
                if (PlaceProp("ENV_SeaRockArch", root, root.TransformPoint(local), yaw, null, 4.6f) != null) Count(local, 4.5f);
            }

            // 항로 부표(Codex 섬에는 이미 있다)
            if (!hasArt && r[11] < 0.25)
            {
                for (int i = 0; i < 2; i++)
                {
                    float a = Angle((r[11] * 7.3 + i * 0.13) % 1.0);
                    var local = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (Rim(a) + 7f + i * 2.5f);
                    if (PlaceProp("ENV_NavigationBuoy", root, root.TransformPoint(local), Vector3.forward, null, 0.8f) != null) Count(local, 1f);
                }
            }

            info.CoastProps += placed;
            if (placed > 0) SetLayer(info.Root, Islands.Layer);
            info.Radius = Mathf.Max(info.Radius, reach);
        }

        /// <summary>
        /// 물 위 소품: 수면(-0.9)에 원점을 두고 axisSocket(원점 → 소켓) 방향을 heading으로 돌린다. 자리에 땅·다른 판정이 있으면 놓지 않는다.
        /// </summary>
        private static GameObject PlaceProp(string name, Transform root, Vector3 world, Vector3 heading, string axisSocket, float clearRadius)
        {
            var prefab = Prop(name);
            if (prefab == null) return null;
            var pos = new Vector3(world.x, SeaY, world.z);
            if (!Islands.IsClear(pos, clearRadius)) return null;
            heading.y = 0f;
            if (heading.sqrMagnitude < 1e-4f) heading = Vector3.forward;
            var axis = SocketAxis(prefab.transform, axisSocket);
            var rot = Quaternion.LookRotation(heading.normalized, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(axis, Vector3.up));
            var go = Object.Instantiate(prefab, pos, rot, root);
            go.name = "Coast " + name;
            Physics.SyncTransforms();
            return go;
        }

        /// <summary>육지에 붙는 물 위 소품(부잔교): 소켓(해안 연결점)을 shore에 대고, 소켓 → 원점 방향을 outward로.</summary>
        private static GameObject PlaceAnchored(string name, Transform root, Vector3 shore, Vector3 outward, string socket)
        {
            var prefab = Prop(name);
            if (prefab == null) return null;
            var s = Find(prefab.transform, socket);
            Vector3 local = s != null ? prefab.transform.InverseTransformPoint(s.position) : Vector3.zero;
            local.y = 0f;
            Vector3 axis = local.sqrMagnitude > 1e-4f ? -local.normalized : Vector3.forward;   // 소켓 → 원점
            outward.y = 0f;
            var rot = Quaternion.LookRotation(outward.normalized, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(axis, Vector3.up));
            var pos = shore - rot * local;
            pos.y = SeaY;
            // 바다 쪽 끝이 물 위여야 한다(육지에 파묻히면 놓지 않는다)
            Vector3 tip = pos + rot * (axis * 3f);
            if (!Islands.IsClear(new Vector3(tip.x, 0f, tip.z), 1.2f)) return null;
            var go = Object.Instantiate(prefab, pos, rot, root);
            go.name = "Coast " + name;
            Physics.SyncTransforms();
            return go;
        }

        /// <summary>프리팹 로컬에서 원점 → 소켓의 수평 방향(소켓이 없으면 +Z).</summary>
        private static Vector3 SocketAxis(Transform root, string socket)
        {
            if (string.IsNullOrEmpty(socket)) return Vector3.forward;
            var s = Find(root, socket);
            if (s == null) return Vector3.forward;
            var local = root.InverseTransformPoint(s.position);
            local.y = 0f;
            return local.sqrMagnitude > 1e-4f ? local.normalized : Vector3.forward;
        }

        /// <summary>정수를 섞어 [0, 1) 값으로(SplitMix/Murmur 마무리 단계).</summary>
        private static double Mix01(int x)
        {
            uint z = unchecked((uint)x + 0x9E3779B9u);
            z = unchecked((z ^ (z >> 16)) * 0x85EBCA6Bu);
            z = unchecked((z ^ (z >> 13)) * 0xC2B2AE35u);
            z ^= z >> 16;
            return z / 4294967296.0;
        }

        private static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = Find(c, name); if (f != null) return f; }
            return null;
        }
    }
}

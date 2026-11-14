using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>해역 종류(2026-10-11 점검 K04). 스테이지마다 정해진다. 값은 저장되지 않는다(스테이지 번호에서 다시 정함).</summary>
    public enum SeaKind { Archipelago, Strait, Open }

    /// <summary>
    /// 해역 차별화(2026-10-11 점검 K04). 스테이지 1 = 군도, 2 = 좁은 수로, 3 = 개방해역.
    /// 배경색만이 아니라 전술이 달라지게 여러 곳이 이 값을 읽는다 — 원본 데이터(RoundSet·모듈 정의)는 바꾸지 않고 배율만 얹는다.
    ///   - 섬(IslandField): 군도는 섬이 많고 수로가 좁다, 수로는 양안 육지(StraitBanks)와 물길 안 작은 바위섬, 개방해역은 섬이 거의 없다
    ///   - 레이더(TargetingSystem): 군도 −20%(섬 반사), 수로 −10%, 개방해역 +15%
    ///   - 적 구성(EnemySpawner): 적 id별 가중치 배율 — 군도 고속정·자폭정, 수로 기뢰·잠수함·어뢰정·포격정, 개방해역 미사일정·항공기
    ///   - 정비 카드(RefitDraft): 모듈 id별 가중치 배율 — 군도 근접 무장, 수로 대잠·곡사포, 개방해역 방공
    ///   - 해안포대(CoastalBatteryField): 군도의 큰 섬 일부·수로 양안에 고정 포대(개방해역은 없음)
    ///   - 물색·안개(SeaAtmosphere)
    /// 좁은 수로: 스테이지 시작 때 함선 위치·침로로 물길(축)을 정한다. 물길 폭 2 × <see cref="ChannelHalfWidth"/>.
    /// 양안 너머로는 나갈 수 없다(<see cref="ClampToChannel"/> — 기함·호위함·보급선·적 수상함이 같은 경계를 쓴다. 항공기는 넘는다).
    /// </summary>
    public static class SeaRegion
    {
        public const float ChannelHalfWidth = 62f;

        public sealed class Profile
        {
            public SeaKind Kind;
            public string Name, Title, Tactics;
            public float Radar;
            /// <summary>구역(110m)당 섬 수 확률: 0개, 1개, 2개까지의 누적(나머지는 3개).</summary>
            public float P0, P1, P2;
            public float MinSpacing;
            public float RockyChance;
            /// <summary>섬 하나에 해안포대가 붙을 확률(큰 섬만).</summary>
            public float BatteryChance;
            public Color Deep, Scatter, Transmission;
            public float FogDensity;
            public Dictionary<string, float> EnemyWeights, CardWeights;
        }

        private static readonly Profile[] s_profiles =
        {
            new()
            {
                Kind = SeaKind.Archipelago, Name = "군도", Title = "제1 해역 · 군도",
                Tactics = "섬이 많고 물길이 좁다 — 고속정·자폭정이 섬 뒤에서 기습한다. 섬이 미사일을 가리고 레이더는 −20%. 큰 섬에 해안포대.",
                Radar = 0.8f, P0 = 0.08f, P1 = 0.45f, P2 = 0.85f, MinSpacing = 24f, RockyChance = 0.45f, BatteryChance = 0.35f,
                Deep = new Color(0.10f, 0.23f, 0.26f), Scatter = new Color(0.22f, 0.42f, 0.44f), Transmission = new Color(0.20f, 0.41f, 0.41f),
                FogDensity = 0.0025f,
                EnemyWeights = new() { ["ene_fastboat"] = 1.4f, ["ene_suicide_boat"] = 1.6f, ["ene_usv"] = 1.4f, ["ene_armored_boat"] = 1.3f, ["ene_torpedo_boat"] = 1.3f,
                                       ["ene_missileboat"] = 0.7f, ["ene_drone"] = 0.7f, ["ene_fighter"] = 0.7f, ["ene_recon"] = 0.8f, ["ene_cruise_submarine"] = 0.8f },
                CardWeights = new() { ["mod_autocannon"] = 1.3f, ["mod_nobong"] = 1.35f, ["mod_gun76"] = 1.25f, ["mod_rambow"] = 1.3f, ["mod_rocket"] = 1.2f,
                                      ["mod_sam"] = 0.85f, ["mod_vls"] = 0.85f },
            },
            new()
            {
                Kind = SeaKind.Strait, Name = "좁은 수로", Title = "제2 해역 · 좁은 수로",
                Tactics = "양안 육지 사이 폭 124m 물길 — 피할 곳이 좁고 기뢰·잠수함·어뢰정·포격정이 많다. 양안에 해안포대, 레이더 −10%.",
                Radar = 0.9f, P0 = 0.72f, P1 = 1f, P2 = 1f, MinSpacing = 30f, RockyChance = 1f, BatteryChance = 0f,
                Deep = new Color(0.10f, 0.17f, 0.19f), Scatter = new Color(0.23f, 0.31f, 0.32f), Transmission = new Color(0.21f, 0.29f, 0.30f),
                FogDensity = 0.0055f,
                EnemyWeights = new() { ["ene_minelayer"] = 2f, ["ene_sea_mine"] = 2f, ["ene_submarine"] = 1.5f, ["ene_attack_submarine"] = 1.5f, ["ene_torpedo_boat"] = 1.4f,
                                       ["ene_artillery_boat"] = 1.4f, ["ene_drone"] = 0.8f, ["ene_fighter"] = 0.7f, ["ene_missileboat"] = 0.8f },
                CardWeights = new() { ["mod_asw"] = 1.5f, ["mod_torpedotube"] = 1.4f, ["mod_helideck"] = 1.5f, ["mod_sonar"] = 1.4f, ["mod_howitzer"] = 1.35f,
                                      ["mod_minerail"] = 1.2f },
            },
            new()
            {
                Kind = SeaKind.Open, Name = "개방해역", Title = "제3 해역 · 개방해역",
                Tactics = "섬이 거의 없는 넓은 바다 — 숨을 곳이 없고 미사일정·항공기·순항미사일 잠수함이 많다. 레이더 +15%.",
                Radar = 1.15f, P0 = 0.86f, P1 = 1f, P2 = 1f, MinSpacing = 40f, RockyChance = 0.7f, BatteryChance = 0f,
                Deep = new Color(-1f, 0f, 0f), FogDensity = 0.0008f,   // 물색 음수 = 머티리얼 원래 색
                EnemyWeights = new() { ["ene_missileboat"] = 1.4f, ["ene_drone"] = 1.4f, ["ene_fighter"] = 1.4f, ["ene_recon"] = 1.3f, ["ene_cruise_submarine"] = 1.3f,
                                       ["ene_aa_frigate"] = 1.2f, ["ene_fastboat"] = 0.7f, ["ene_suicide_boat"] = 0.6f, ["ene_minelayer"] = 0.5f },
                CardWeights = new() { ["mod_sam"] = 1.4f, ["mod_ciws"] = 1.4f, ["mod_vls"] = 1.35f, ["mod_decoy"] = 1.3f, ["mod_ew"] = 1.3f, ["mod_radar"] = 1.2f,
                                      ["mod_nobong"] = 0.9f, ["mod_rambow"] = 0.8f },
            },
        };

        public static bool Active { get; private set; }
        public static SeaKind Current { get; private set; } = SeaKind.Open;
        public static Profile CurrentProfile => For(Current);
        public static Profile For(SeaKind kind) => s_profiles[Mathf.Clamp((int)kind, 0, s_profiles.Length - 1)];

        /// <summary>스테이지 번호(1부터) → 해역.</summary>
        public static SeaKind ForStage(int stageNumber) => stageNumber <= 1 ? SeaKind.Archipelago : stageNumber == 2 ? SeaKind.Strait : SeaKind.Open;

        public static Vector3 ChannelOrigin { get; private set; }
        public static Vector3 ChannelAxis { get; private set; } = Vector3.forward;
        public static Vector3 ChannelRight => new(ChannelAxis.z, 0f, -ChannelAxis.x);
        public static bool IsStrait => Active && Current == SeaKind.Strait;

        /// <summary>해역이 바뀌었다(스테이지 시작). 섬·포대·물색이 듣는다.</summary>
        public static event Action<SeaKind> Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = false;
            Current = SeaKind.Open;
            ChannelOrigin = Vector3.zero;
            ChannelAxis = Vector3.forward;
            Changed = null;
            HarborActive = false;
            HarborChanged = null;
        }

        /// <summary>스테이지 시작(StageDirector). 좁은 수로면 origin·heading이 물길 가운데·방향이 된다.</summary>
        public static void Enter(SeaKind kind, Vector3 origin, Vector3 heading)
        {
            Active = true;
            Current = kind;
            heading.y = 0f;
            ChannelAxis = heading.sqrMagnitude > 1e-4f ? heading.normalized : Vector3.forward;
            ChannelOrigin = new Vector3(origin.x, 0f, origin.z);
            HarborActive = false;
            Changed?.Invoke(kind);
        }

        /// <summary>해역 효과를 끈다(훈련·검사).</summary>
        public static void Leave()
        {
            if (!Active) return;
            Active = false;
            Current = SeaKind.Open;
            HarborActive = false;
            Changed?.Invoke(SeaKind.Open);
        }

        public static float RadarMultiplier => Active ? CurrentProfile.Radar : 1f;
        public static float EnemyWeight(string id) => Active && id != null && CurrentProfile.EnemyWeights.TryGetValue(id, out float w) ? w : 1f;
        public static float CardWeight(string id) => Active && id != null && CurrentProfile.CardWeights.TryGetValue(id, out float w) ? w : 1f;

        // ------------------------------------------------------------ 좁은 수로

        /// <summary>물길 축에서 옆으로 떨어진 거리(오른쪽 +).</summary>
        public static float Lateral(Vector3 p) => Vector3.Dot(new Vector3(p.x - ChannelOrigin.x, 0f, p.z - ChannelOrigin.z), ChannelRight);
        /// <summary>물길 축을 따라간 거리.</summary>
        public static float Along(Vector3 p) => Vector3.Dot(new Vector3(p.x - ChannelOrigin.x, 0f, p.z - ChannelOrigin.z), ChannelAxis);
        /// <summary>물길 위의 점(along, lateral) → 월드.</summary>
        public static Vector3 ChannelPoint(float along, float lateral) => ChannelOrigin + ChannelAxis * along + ChannelRight * lateral;

        /// <summary>그 자리가 물길 안(양안에서 margin 이상)인가. 수로가 아니면 늘 true. 군항이 열려 있으면 만(灣) 모양과 안벽까지.</summary>
        public static bool InWater(Vector3 p, float margin) => !IsStrait || WaterRoom(p) >= margin;

        /// <summary>보이지 않는 경계: 물길 밖이면 안쪽으로 되돌린다. 되돌렸으면 true.</summary>
        public static bool ClampToChannel(ref Vector3 p, float margin)
        {
            if (!IsStrait) return false;
            bool moved = false;
            float along = Along(p);
            if (HarborActive)
            {
                // 안벽 너머(군항 육지)로는 못 간다
                float t = HarborT(along), limitT = HarborQuayOffset - margin;
                if (t > limitT) { p -= HarborForward * (t - limitT); along = Along(p); moved = true; }
            }
            float lat = Lateral(p), limit = HalfWidthAt(along) - margin;
            if (Mathf.Abs(lat) <= limit) return moved;
            p -= ChannelRight * (lat - Mathf.Sign(lat) * limit);
            return true;
        }

        /// <summary>
        /// 그 자리에서 가장 가까운 경계(양안 · 군항 안벽)까지 남은 거리(안쪽 +, 밖이면 −). 수로가 아니면 큰 값.
        /// 적이 기슭에 박히기 전에 미리 돌게 하는 데 쓴다(EnemyController.KeepInWater).
        /// </summary>
        public static float WaterRoom(Vector3 p)
        {
            if (!IsStrait) return 1e6f;
            float along = Along(p);
            float room = HalfWidthAt(along) - Mathf.Abs(Lateral(p));
            if (HarborActive) room = Mathf.Min(room, HarborQuayOffset - HarborT(along));
            return room;
        }

        /// <summary>가장 가까운 경계에서 물 안쪽을 향하는 수평 방향.</summary>
        public static Vector3 InwardNormal(Vector3 p)
        {
            float along = Along(p), lat = Lateral(p);
            float side = HalfWidthAt(along) - Mathf.Abs(lat);
            if (HarborActive && HarborQuayOffset - HarborT(along) < side) return -HarborForward;
            if (HarborActive)
            {
                // 만의 둥근 기슭: 만 가운데 쪽으로(곧은 물길에서는 축에 수직과 같다)
                float t = HarborT(along);
                if (t > HarborEntranceT && t < HarborQuayOffset)
                {
                    Vector3 c = HarborPoint(0f, 0f) - new Vector3(p.x, 0f, p.z);
                    c.y = 0f;
                    if (c.sqrMagnitude > 1f) return c.normalized;
                }
            }
            return -ChannelRight * Mathf.Sign(lat == 0f ? 1f : lat);
        }

        // ------------------------------------------------------------ 수로 출구 군항(2026-11-15)
        // 2스테이지 보스 구간: 앞쪽 수로가 지름 약 240m의 만으로 벌어지고, 만 안쪽 끝이 적 군항 안벽이다.
        // 좌표 t = 만 가운데에서 군항 쪽으로 잰 물길 축 거리. 만 = 반경 HarborRadius의 원, t = HarborQuayOffset에서 곧은 안벽.

        public const float HarborRadius = 120f;
        public const float HarborQuayOffset = 66f;
        /// <summary>만 입구(원이 물길 폭과 만나는 곳, 만 가운데 기준 −).</summary>
        public static float HarborEntranceT => -Mathf.Sqrt(HarborRadius * HarborRadius - ChannelHalfWidth * ChannelHalfWidth);
        /// <summary>안벽에서의 만 절반 폭.</summary>
        public static float HarborQuayHalfWidth => Mathf.Sqrt(HarborRadius * HarborRadius - HarborQuayOffset * HarborQuayOffset);

        public static bool HarborActive { get; private set; }
        public static float HarborCenterAlong { get; private set; }
        /// <summary>군항이 물길 축의 어느 쪽인가(+1 = 축 방향, −1 = 반대).</summary>
        public static int HarborDir { get; private set; } = 1;
        /// <summary>군항이 열렸다/닫혔다. 기슭·섬·포대·군항 외형이 듣는다.</summary>
        public static event Action HarborChanged;

        public static Vector3 HarborForward => ChannelAxis * HarborDir;
        public static float HarborT(float along) => (along - HarborCenterAlong) * HarborDir;
        public static float HarborT(Vector3 p) => HarborT(Along(p));
        /// <summary>만 좌표(t, 오른쪽 거리 — 군항을 바라볼 때의 오른쪽) → 월드.</summary>
        public static Vector3 HarborPoint(float t, float right) => ChannelPoint(HarborCenterAlong + t * HarborDir, right * HarborDir);

        /// <summary>군항을 연다(EnemySpawner 보스 구간). 수로가 아니면 무시.</summary>
        public static void OpenHarbor(float centerAlong, int dir)
        {
            if (!IsStrait) return;
            HarborActive = true;
            HarborCenterAlong = centerAlong;
            HarborDir = dir >= 0 ? 1 : -1;
            HarborChanged?.Invoke();
        }

        public static void CloseHarbor()
        {
            if (!HarborActive) return;
            HarborActive = false;
            HarborChanged?.Invoke();
        }

        /// <summary>그 지점(물길 축 거리)에서 물길 가운데부터 경계까지(군항이 열려 있으면 만 모양).</summary>
        public static float HalfWidthAt(float along) => HarborActive ? HarborShore(HarborT(along), ChannelHalfWidth) : ChannelHalfWidth;

        /// <summary>
        /// 만 모양: 곧은 물길 폭(baseHalf)과 원을 부드럽게 합친다(입구가 꺾이지 않게). 안벽 뒤는 안벽 폭 그대로.
        /// 기슭 외형(IslandBuilder.BankShore)도 같은 식에 잡음을 얹어 쓴다.
        /// </summary>
        public static float HarborShore(float t, float baseHalf, float circleNoise = 0f)
        {
            float tc = Mathf.Min(t, HarborQuayOffset);
            float circle = Mathf.Abs(tc) < HarborRadius ? Mathf.Sqrt(HarborRadius * HarborRadius - tc * tc) + circleNoise : 0f;
            return SmoothMax(baseHalf, circle, 14f);
        }

        /// <summary>다항식 부드러운 최댓값: b가 a보다 k 넘게 작으면 정확히 a(멀리 있는 기슭은 군항을 열어도 그대로).</summary>
        private static float SmoothMax(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(a, b, h) + k * h * (1f - h);
        }
    }
}

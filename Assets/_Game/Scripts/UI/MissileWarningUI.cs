using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using Game.Combat;
using Game.Enemies;

namespace Game.UI
{
    /// <summary>
    /// 위협 표시. 가운데가 빈 조준선만 그려 표적을 가리지 않는다.
    ///   - 적 미사일(빨강): 네 모서리 괄호 [ ]. 화면 안에서는 미사일을 감싸고, 화면 밖이면 가장자리에 "미사일 38".
    ///   - 함정(노랑)·항공기(주황)·드러난 잠수함(하늘색): 같은 괄호를 45° 돌린 마름모. 화면 밖에 있을 때만 가장자리에 방향·종류·거리.
    /// 화면 밖 표식에는 표적 쪽을 가리키는 작은 화살표가 붙는다.
    /// 가까운 순으로 몇 개만 보이고, 서로 겹치거나 HUD 패널(진행·스킬·함 현황·레이더) 위에 놓이지 않게 밀어낸다.
    /// 자동으로 카메라를 돌리지 않는다 — 방향만 알려 준다.
    /// </summary>
    public class MissileWarningUI : MonoBehaviour
    {
        [Tooltip("사용하지 않음(예전 채운 마름모 표식). 표식은 코드로 만든다.")]
        [SerializeField] private RectTransform indicatorPrefab;
        [SerializeField] private RectTransform canvasRoot;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private float edgePadding = 56f;

        [Header("Threats")]
        [Tooltip("화면 밖 함정·항공기를 표시하는 최대 거리")]
        [SerializeField] private float offscreenRange = 80f;
        [SerializeField, Min(1)] private int maxIndicators = 10;

        [Header("Look")]
        [SerializeField] private float missileMarkerSize = 34f;
        [SerializeField] private float edgeMarkerSize = 28f;
        [Tooltip("표식끼리 이보다 가까우면 가장자리를 따라 비켜 놓는다(HUD 픽셀)")]
        [SerializeField] private float minSeparation = 46f;

        private static readonly Color MissileColor = new(1f, 0.25f, 0.2f, 0.95f);
        private static readonly Color AirColor = new(1f, 0.65f, 0.2f, 0.95f);
        private static readonly Color ShipColor = new(1f, 0.9f, 0.35f, 0.95f);
        private static readonly Color SubColor = new(0.45f, 0.9f, 1f, 0.95f);
        private static readonly Color ShadowColor = new(0f, 0f, 0f, 0.45f);

        private class Indicator
        {
            public RectTransform Root;
            public RawImage Shadow, Shape, Arrow;
            public TMP_Text Label;
        }

        private readonly List<Indicator> _pool = new();
        private readonly List<(ITargetable t, float dist, bool missile)> _threats = new(32);
        private readonly List<Vector2> _placed = new(16);
        private readonly List<Rect> _avoid = new(8);
        private RectTransform[] _avoidRects = System.Array.Empty<RectTransform>();
        private TMP_FontAsset _font;
        private readonly Vector3[] _corners = new Vector3[4];
        private static readonly float[] SlideSteps = { 0f, 1f, -1f, 2f, -2f, 3f, -3f };
        private const float LabelHalfWidth = 90f, LabelHalfHeight = 12f;

        /// <summary>표식 중심에서 거리 글자 중심까지(HUD 픽셀). 좌우 가장자리면 글자 폭만큼 더 안쪽으로 둬 표식과 겹치지 않게.</summary>
        private static Vector2 LabelOffset(Vector2 outward)
            => -outward * (30f + Mathf.Abs(outward.x) * LabelHalfWidth + Mathf.Abs(outward.y) * LabelHalfHeight);

        /// <summary>런타임 생성용(HUDView). avoid 패널 위에는 가장자리 표식을 놓지 않는다.</summary>
        public void Setup(RectTransform root, Camera cam, TMP_FontAsset font, params RectTransform[] avoid)
        {
            canvasRoot = root;
            worldCamera = cam;
            _font = font;
            _avoidRects = avoid ?? System.Array.Empty<RectTransform>();
        }

        private void LateUpdate()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null || canvasRoot == null) return;

            var player = GameManager.Instance != null && GameManager.Instance.Player != null
                ? GameManager.Instance.Player.transform
                : null;

            GatherThreats(player);
            GatherAvoidRects();
            _placed.Clear();

            int used = 0;
            foreach (var (t, dist, missile) in _threats)
            {
                if (used >= maxIndicators) break;
                if (Place(GetIndicator(used), t, dist, missile)) used++;
            }

            for (int i = used; i < _pool.Count; i++)
                if (_pool[i].Root.gameObject.activeSelf) _pool[i].Root.gameObject.SetActive(false);
        }

        private void GatherThreats(Transform player)
        {
            _threats.Clear();
            Vector3 origin = player != null ? player.position : worldCamera.transform.position;

            void Add(TargetKind kind, bool missile, float range)
            {
                var list = TargetRegistry.HostileTo(CombatFaction.Player, kind);
                for (int i = 0; i < list.Count; i++)
                {
                    var t = list[i];
                    if (t == null || !t.IsAlive || t.Transform == null) continue;
                    if (kind == TargetKind.Submarine && !t.IsRevealed) continue;
                    if (kind == TargetKind.Missile && t is Missile projectile && !projectile.IsThreat) continue;
                    float d = Vector3.Distance(new Vector3(origin.x, 0f, origin.z), new Vector3(t.Transform.position.x, 0f, t.Transform.position.z));
                    if (d > range) continue;
                    _threats.Add((t, d, missile));
                }
            }

            Add(TargetKind.Missile, true, float.MaxValue);

            // 어뢰(표적 등록소에 없음): 미사일과 같은 붉은 괄호, 글자는 "어뢰"
            var torpedoes = Torpedo.Active;
            for (int i = 0; i < torpedoes.Count; i++)
            {
                var t = torpedoes[i];
                if (t == null || !t.IsAlive) continue;
                float d = Vector3.Distance(new Vector3(origin.x, 0f, origin.z), new Vector3(t.transform.position.x, 0f, t.transform.position.z));
                if (d <= offscreenRange) _threats.Add((t, d, true));
            }

            // 사격 제원을 잡은 어뢰 잠수함(곧 발사): 잠항 중이어도 "어뢰 준비"로 알린다
            var preparing = Submarine.Preparing;
            for (int i = 0; i < preparing.Count; i++)
            {
                var s = preparing[i];
                if (s == null || !s.IsAlive) continue;
                // 사격 제원 경고는 전역 경고다. 잠항 중인 잠수함의 실제 거리와 위치를 표시하지 않는다.
                _threats.Add((s, float.MaxValue, true));
                break;
            }

            Add(TargetKind.Aircraft, false, offscreenRange);
            Add(TargetKind.Surface, false, offscreenRange);
            Add(TargetKind.Submarine, false, offscreenRange);

            // 미사일 먼저, 그다음 가까운 순
            _threats.Sort((a, b) => a.missile != b.missile ? (a.missile ? -1 : 1) : a.dist.CompareTo(b.dist));
        }

        private void GatherAvoidRects()
        {
            _avoid.Clear();
            float margin = 22f * UiScale;
            foreach (var r in _avoidRects)
            {
                if (r == null || !r.gameObject.activeInHierarchy) continue;
                r.GetWorldCorners(_corners);   // 오버레이 캔버스: 월드 = 화면 픽셀
                _avoid.Add(Rect.MinMaxRect(_corners[0].x - margin, _corners[0].y - margin, _corners[2].x + margin, _corners[2].y + margin));
            }
        }

        /// <summary>HUD 기준 픽셀 → 화면 픽셀 배율.</summary>
        private float UiScale => canvasRoot != null && canvasRoot.lossyScale.x > 0f ? canvasRoot.lossyScale.x : 1f;

        /// <summary>화면 밖이면 가장자리에 방향·종류·거리를, 화면 안이면 미사일만 대상 위에 표식.</summary>
        private bool Place(Indicator ind, ITargetable t, float dist, bool missile)
        {
            bool hiddenPreparation = t is Submarine { IsPreparingTorpedo: true } preparing &&
                                     !preparing.IsRevealed && !preparing.IsContactConfirmed;
            if (hiddenPreparation)
            {
                ind.Root.gameObject.SetActive(true);
                ind.Root.position = new Vector3(Screen.width * 0.5f, Screen.height * 0.78f, 0f);
                ind.Shape.color = MissileColor;
                ind.Shape.rectTransform.sizeDelta = ind.Shadow.rectTransform.sizeDelta = new Vector2(missileMarkerSize, missileMarkerSize);
                ind.Shape.rectTransform.localRotation = ind.Shadow.rectTransform.localRotation = Quaternion.identity;
                ind.Arrow.enabled = false;
                ind.Label.text = "어뢰 준비 — 회피 기동";
                ind.Label.color = MissileColor;
                ind.Label.rectTransform.anchoredPosition = new Vector2(0f, -34f);
                return true;
            }
            Vector3 reported = t is SubmarineBase sub && !sub.IsRevealed ? sub.LastKnownPosition : t.Transform.position;
            if (t is SubmarineBase && !t.IsRevealed)
                dist = Vector3.Distance(GameManager.Instance.Player.transform.position, reported);
            Vector3 sp = worldCamera.WorldToScreenPoint(reported);
            bool behind = sp.z < 0f;
            if (behind) { sp.x = Screen.width - sp.x; sp.y = Screen.height - sp.y; }

            bool onScreen = !behind && sp.x >= 0f && sp.x <= Screen.width && sp.y >= 0f && sp.y <= Screen.height;
            // HUD 패널 밑에 가려진 함정·항공기는 화면 밖과 같게 가장자리에 알린다
            if (onScreen && !missile && UnderPanel(sp)) onScreen = false;
            if (onScreen && !missile) return false;

            float uiScale = UiScale;
            Color color = missile ? MissileColor : ColorFor(t);
            ind.Root.gameObject.SetActive(true);
            ind.Shape.color = color;

            // 미사일은 정사각 괄호, 나머지는 45° 마름모(돌리면 대각선이 길어져 조금 작게)
            float size = (onScreen ? missileMarkerSize : edgeMarkerSize) * (missile ? 1f : 0.82f);
            var shapeRot = Quaternion.Euler(0f, 0f, missile ? 0f : 45f);
            ind.Shape.rectTransform.sizeDelta = ind.Shadow.rectTransform.sizeDelta = new Vector2(size, size);
            ind.Shape.rectTransform.localRotation = ind.Shadow.rectTransform.localRotation = shapeRot;

            if (onScreen)
            {
                ind.Root.position = new Vector3(sp.x, sp.y, 0f);
                ind.Arrow.enabled = false;
                float eta = SecondsToImpact(t);
                ind.Label.text = eta > 0f && eta < 20f ? $"{eta:0.0}초" : "";
                ind.Label.color = color;
                ind.Label.rectTransform.anchoredPosition = new Vector2(0f, -32f);
                _placed.Add(sp);
                return true;
            }

            // 화면 중심에서 표적 방향으로, 여백 안쪽 사각형 가장자리에 붙인다
            Vector2 center = new(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 dir = new Vector2(sp.x, sp.y) - center;
            if (dir.sqrMagnitude < 1f) dir = Vector2.up;
            Vector2 n = dir.normalized;
            float pad = edgePadding * uiScale;
            float halfW = Screen.width * 0.5f - pad, halfH = Screen.height * 0.5f - pad;
            float sx = halfW / Mathf.Max(Mathf.Abs(dir.x), 0.001f), sy = halfH / Mathf.Max(Mathf.Abs(dir.y), 0.001f);
            Vector2 edge = center + dir * Mathf.Min(sx, sy);
            Vector2 tangent = sx < sy ? Vector2.up : Vector2.right;   // 좌우 가장자리면 세로로, 위아래면 가로로 비킨다

            edge = Resolve(edge, n, tangent, uiScale);
            _placed.Add(edge);
            ind.Root.position = new Vector3(edge.x, edge.y, 0f);

            ind.Arrow.enabled = true;
            ind.Arrow.color = color;
            ind.Arrow.rectTransform.anchoredPosition = n * (size * 0.5f + 11f);
            ind.Arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(n.y, n.x) * Mathf.Rad2Deg - 90f);

            float impactTime = SecondsToImpact(t);
            ind.Label.text = impactTime > 0f && impactTime < 20f
                ? $"{LabelFor(t, missile)} {dist:0} · {impactTime:0.0}초"
                : $"{LabelFor(t, missile)} {dist:0}";
            ind.Label.color = color;
            ind.Label.rectTransform.anchoredPosition = LabelOffset(n);   // 가장자리 안쪽으로 글자
            return true;
        }

        private static float SecondsToImpact(ITargetable threat)
        {
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null || threat == null || threat.Transform == null) return -1f;
            Vector3 to = player.transform.position - threat.Transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance < 0.01f) return 0f;
            Vector3 velocity = threat is Torpedo torpedo ? threat.Transform.forward * torpedo.Speed :
                               threat is IHasVelocity moving ? moving.Velocity : Vector3.zero;
            velocity.y = 0f;
            float closing = Vector3.Dot(velocity, to / distance);
            return closing > 0.1f ? distance / closing : -1f;
        }

        /// <summary>HUD 패널 위라면 화면 안쪽으로, 다른 표식과 겹치면 가장자리를 따라 비켜 놓는다.</summary>
        private Vector2 Resolve(Vector2 p, Vector2 outward, Vector2 tangent, float uiScale)
        {
            float sep = minSeparation * uiScale;
            foreach (float k in SlideSteps)
            {
                Vector2 candidate = PushOutOfPanels(p + tangent * (k * sep), outward, uiScale);
                if (IsClear(candidate, sep)) return candidate;
            }
            return PushOutOfPanels(p, outward, uiScale);
        }

        private Vector2 PushOutOfPanels(Vector2 p, Vector2 outward, float uiScale)
        {
            for (int step = 0; step < 60; step++)
            {
                if (!Blocked(p, outward, uiScale)) return p;
                p -= outward * 8f * uiScale;
            }
            return p;
        }

        private bool UnderPanel(Vector2 p)
        {
            float margin = 22f * UiScale;   // GatherAvoidRects가 넓힌 여백은 빼고 실제 패널만
            foreach (var r in _avoid)
                if (p.x > r.xMin + margin && p.x < r.xMax - margin && p.y > r.yMin + margin && p.y < r.yMax - margin) return true;
            return false;
        }

        /// <summary>표식이나 그 안쪽 거리 글자가 HUD 패널에 걸리는가.</summary>
        private bool Blocked(Vector2 p, Vector2 outward, float uiScale)
        {
            var marker = new Rect(p.x - 18f * uiScale, p.y - 18f * uiScale, 36f * uiScale, 36f * uiScale);
            Vector2 lc = p + LabelOffset(outward) * uiScale;
            var label = new Rect(lc.x - LabelHalfWidth * uiScale, lc.y - LabelHalfHeight * uiScale, LabelHalfWidth * 2f * uiScale, LabelHalfHeight * 2f * uiScale);
            foreach (var r in _avoid)
                if (r.Overlaps(marker) || r.Overlaps(label)) return true;
            return false;
        }

        private bool IsClear(Vector2 p, float sep)
        {
            float sqr = sep * sep;
            foreach (var q in _placed) if ((q - p).sqrMagnitude < sqr) return false;
            return true;
        }

        private static string LabelFor(ITargetable t, bool missile)
        {
            if (t is Torpedo) return "어뢰";
            if (t is Submarine { IsPreparingTorpedo: true }) return "어뢰 준비";
            if (t is TorpedoBoat { IsPreparingTorpedo: true }) return "어뢰 준비";
            if (t is ModernCorvetteBoss { IsPreparingTorpedo: true }) return "어뢰 준비";
            if (missile) return "미사일";
            return t switch
            {
                KamikazeDrone => "드론",
                FighterJet => "전투기",
                ReconAircraft => "정찰기",
                CruiseMissileSubmarine => "미사일 잠수함",
                Submarine { Definition: { Id: "ene_attack_submarine" } } => "공격 잠수함",
                SubmarineBase => "잠수함",
                PccCorvette => "초계함",
                MissileBoat => "미사일정",
                SuicideBoat => "자폭 보트",
                TorpedoBoat => "어뢰정",
                ArtilleryBoat => "포격정",
                RepairBoat => "수리정",
                MineLayer => "기뢰부설정",
                SeaMine => "기뢰",
                FastAttackBoat { Definition: { Id: "ene_armored_boat" } } => "돌격정",
                FastAttackBoat { Definition: { Id: "ene_usv" } } => "무인정",
                FastAttackBoat { Definition: { Id: "ene_ew_corvette" } } => "전자전함",
                FastAttackBoat { Definition: { Id: "ene_aa_frigate" } } => "방공함",
                FastAttackBoat => "고속정",
                BossShip or HybridBattleshipBoss or ModernCorvetteBoss => "보스",
                _ => t.Kind == TargetKind.Aircraft ? "항공기" : "함정",
            };
        }

        private static Color ColorFor(ITargetable t) => t.Kind switch
        {
            TargetKind.Aircraft => AirColor,
            TargetKind.Submarine => SubColor,
            _ => ShipColor,
        };

        private Indicator GetIndicator(int index)
        {
            while (_pool.Count <= index)
            {
                var root = new GameObject($"Threat {_pool.Count}", typeof(RectTransform)).GetComponent<RectTransform>();
                root.SetParent(canvasRoot, false);
                root.sizeDelta = Vector2.zero;

                var ind = new Indicator
                {
                    Root = root,
                    Shadow = CreateImage(root, "Shadow", HudTextures.CornerBrackets, ShadowColor, new Vector2(1.5f, -1.5f)),
                    Shape = CreateImage(root, "Reticle", HudTextures.CornerBrackets, Color.white, Vector2.zero),
                    Arrow = CreateImage(root, "Arrow", HudTextures.Triangle, Color.white, Vector2.zero),
                    Label = CreateLabel(root),
                };
                ind.Arrow.rectTransform.sizeDelta = new Vector2(14f, 12f);
                root.gameObject.SetActive(false);
                _pool.Add(ind);
            }
            return _pool[index];
        }

        private static RawImage CreateImage(RectTransform parent, string name, Texture texture, Color color, Vector2 offset)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<RawImage>();
            img.texture = texture;
            img.color = color;
            img.raycastTarget = false;   // 가장자리 버튼 클릭을 막지 않게
            img.rectTransform.anchoredPosition = offset;
            return img;
        }

        /// <summary>HUD가 쓰는 한글 폰트를 빌려 거리 글자를 만든다. 표식과 함께 켜고 꺼진다.</summary>
        private TMP_Text CreateLabel(RectTransform parent)
        {
            if (_font == null)
            {
                var canvas = canvasRoot.GetComponentInParent<Canvas>();
                var any = canvas != null ? canvas.GetComponentInChildren<TMP_Text>(true) : null;
                _font = any != null ? any.font : null;
            }

            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (_font != null) text.font = _font;
            text.fontSize = 18;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(180f, 24f);
            return text;
        }
    }
}

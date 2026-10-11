using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.Core;
using Game.View;

namespace Game.TaskForce
{
    /// <summary>
    /// 선택 임무 "보급선 호위"(2026-10-11 점검 K03). 보스 격침 뒤 항로 선택에서 [3] 임무 항로를 고르면 다음 스테이지 시작에 열린다.
    ///   - 스테이지가 시작되고 잠시 뒤 보급선 한 척이 기함 좌현 뒤쪽에 나타나 따라온다(기함보다 느리다 — 너무 앞서 가면 뒤처진다).
    ///   - 보급선은 아군 수상 표적이라 호위함을 노리는 적이 우선해서 노린다(최대 4척 동시). 아군 탄에는 맞지 않는다.
    ///   - 전투 시간 90초를 버티면 성공: 선체 완전 수리 · 다시 뽑기 +1 · 정비 1회(레벨업 카드). 보급선은 물러난다.
    ///   - 보급선이 격침되면 실패: 보상 없음(벌칙도 없음).
    /// 화면 위쪽 가운데에 남은 시간과 보급선 선체 막대가 나온다. 판이 끝나거나 씬이 바뀌면 함께 사라진다.
    /// </summary>
    public sealed class SupplyEscortMission : MonoBehaviour
    {
        public const float Duration = 90f;
        public const float StartDelay = 3f;
        public const float SupplyHullHp = 140f;

        public enum Outcome { Pending, Running, Succeeded, Failed }

        /// <summary>지금 진행 중인(또는 막 끝난) 임무. 없으면 null.</summary>
        public static SupplyEscortMission Current { get; private set; }

        private Game.Ship.ShipController _player;
        private Game.Progression.ExperienceSystem _xp;
        private SupplyShip _ship;
        private float _delay, _elapsed, _duration;
        private RectTransform _panel, _fill;
        private TMP_Text _label;

        public Outcome State { get; private set; } = Outcome.Pending;
        /// <summary>마지막으로 끝난 임무의 결과(검증·결과 화면용).</summary>
        public static Outcome LastOutcome { get; private set; } = Outcome.Pending;
        public float Remaining => Mathf.Max(0f, _duration - _elapsed);
        public SupplyShip Ship => _ship;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRun() { Current = null; LastOutcome = Outcome.Pending; }

        public static SupplyEscortMission Begin(Game.Ship.ShipController player, Game.Progression.ExperienceSystem xp, float duration = Duration, float delay = StartDelay)
        {
            if (Current != null) Destroy(Current.gameObject);
            var mission = new GameObject("Supply escort mission").AddComponent<SupplyEscortMission>();
            mission._player = player;
            mission._xp = xp;
            mission._duration = duration;
            mission._delay = delay;
            Current = mission;
            GameEvents.RaiseBanner($"임무 항로 — 곧 보급선이 합류합니다. {duration:0}초 동안 지키면 선체 완전 수리 · 다시 뽑기 +1 · 정비 1회");
            return mission;
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
            if (_panel != null) Destroy(_panel.gameObject);
            if (_ship != null && State == Outcome.Running) Destroy(_ship.gameObject);
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.IsRunOver) { if (State == Outcome.Running) Fail(false); return; }
            if (gm.State != GameState.Playing) return;
            float dt = Time.deltaTime;

            if (State == Outcome.Pending)
            {
                _delay -= dt;
                if (_delay <= 0f) Spawn();
                return;
            }
            if (State != Outcome.Running) return;

            _elapsed += dt;
            if (_ship == null || !_ship.IsAlive) { Fail(true); return; }
            if (_elapsed >= _duration) { Succeed(); return; }
            RefreshPanel();
        }

        private void Spawn()
        {
            if (_player == null) { Destroy(gameObject); return; }
            var t = _player.transform;
            Vector3 pos = t.position - t.forward * 28f - t.right * 18f;
            pos.y = 0f;
            _ship = SupplyShip.Create(pos, t.eulerAngles.y, t);
            State = Outcome.Running;
            BuildPanel();
            RefreshPanel();
            UiSound.Play(UiSound.Cue.Confirm);
            GameEvents.RaiseBanner($"보급선 합류 — {_duration:0}초 동안 지키세요(기함 좌현 뒤쪽, ◆ 표시)");
        }

        private void Succeed()
        {
            State = LastOutcome = Outcome.Succeeded;
            _ship.Depart();
            if (_player != null && _player.IsAlive) _player.RepairHull(_player.HullMaxHp);
            Game.Refit.RefitDraft.GrantReroll();
            PlayerProfile.Unlock("supply");
            UiSound.Play(UiSound.Cue.Victory);
            GameEvents.RaiseBanner("보급 완료 — 선체 완전 수리 · 다시 뽑기 +1 · 정비 1회");
            // 정비 1회: 다음 레벨까지 남은 경험치를 채워 레벨업 정비를 연다
            if (_xp != null) _xp.AddXp(Mathf.Max(1, _xp.XpToNext - _xp.Xp));
            Close();
        }

        private void Fail(bool sunk)
        {
            State = LastOutcome = Outcome.Failed;
            if (sunk)
            {
                UiSound.Play(UiSound.Cue.Fail);
                GameEvents.RaiseBanner("보급선 격침 — 임무 실패(보상 없음)");
            }
            Close();
        }

        private void Close()
        {
            if (_panel != null) Destroy(_panel.gameObject);
            _panel = null;
            Destroy(gameObject, 0.1f);
        }

        // ------------------------------------------------------------ 화면 위 막대

        private void BuildPanel()
        {
            var hud = FindFirstObjectByType<Game.UI.HUDView>();
            if (hud == null) return;
            var go = new GameObject("Supply escort panel", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(hud.transform, false);
            _panel = (RectTransform)go.transform;
            _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(0.5f, 1f);
            _panel.anchoredPosition = new Vector2(0f, -212f);
            _panel.sizeDelta = new Vector2(460f, 52f);
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.02f, 0.07f, 0.09f, 0.82f);
            bg.raycastTarget = false;

            var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(_panel, false);
            _label = label.GetComponent<TextMeshProUGUI>();
            _label.font = hud.Font;
            _label.fontSize = 18;
            _label.color = new Color(0.66f, 1f, 0.75f);
            _label.alignment = TextAlignmentOptions.Center;
            _label.raycastTarget = false;
            _label.richText = true;
            var lr = _label.rectTransform;
            lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(0.5f, 1f);
            lr.anchoredPosition = new Vector2(0f, -4f);
            lr.sizeDelta = new Vector2(440f, 28f);

            var back = new GameObject("Hull back", typeof(RectTransform), typeof(Image));
            back.transform.SetParent(_panel, false);
            var br = (RectTransform)back.transform;
            br.anchorMin = br.anchorMax = br.pivot = new Vector2(0f, 0f);
            br.anchoredPosition = new Vector2(14f, 9f);
            br.sizeDelta = new Vector2(432f, 9f);
            back.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            back.GetComponent<Image>().raycastTarget = false;
            var fill = new GameObject("Hull fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(br, false);
            _fill = (RectTransform)fill.transform;
            _fill.anchorMin = _fill.anchorMax = _fill.pivot = Vector2.zero;
            _fill.sizeDelta = new Vector2(432f, 9f);
            fill.GetComponent<Image>().raycastTarget = false;
        }

        private void RefreshPanel()
        {
            if (_panel == null || _ship == null) return;
            float hp = _ship.Hull01;
            _label.text = $"<color=#FFD166>임무</color>  보급선 호위   <b>{Mathf.CeilToInt(Remaining)}초</b>   ·   선체 {hp * 100f:0}%";
            _fill.sizeDelta = new Vector2(432f * hp, 9f);
            _fill.GetComponent<Image>().color = hp > 0.5f ? new Color(0.35f, 0.9f, 0.55f) : hp > 0.25f ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.4f, 0.3f);
        }

        // ------------------------------------------------------------ 보급선

        /// <summary>
        /// 보급선 한 척: 아군 수상 표적(ITargetable)이자 피해 대상(IDamageable). 콜라이더는 PlayerShip 레이어라 적 탄에만 맞는다.
        /// 기함 좌현 뒤쪽 자리를 배처럼(뱃머리 방향으로만, 선회율 한계) 따라간다. 격침되면 기울며 가라앉는다.
        /// </summary>
        public sealed class SupplyShip : MonoBehaviour, ITargetable, IDamageable, ITargetPriority
        {
            public const float MaxSpeed = 12.5f;
            private const float TurnRate = 34f, Accel = 4f;
            private const float VisualScale = 0.75f;

            private Transform _flagship;
            private float _hp, _heading, _speed, _sinkAge;
            private bool _departing, _sinking;
            private BoxCollider _hull;
            private Transform _visual;

            public Transform Transform => transform;
            public TargetKind Kind => TargetKind.Surface;
            public CombatFaction Faction => CombatFaction.Player;
            public bool IsAlive => this != null && isActiveAndEnabled && !_sinking && !_departing && _hp > 0f;
            public bool IsRevealed => true;
            /// <summary>호위함(0.4)보다 먼저 노린다. 기함은 1.</summary>
            public float TargetPriority => 0.8f;
            public int MaxAttackers => 4;
            public float Hull01 => Mathf.Clamp01(_hp / SupplyHullHp);

            public static SupplyShip Create(Vector3 position, float heading, Transform flagship)
            {
                var root = new GameObject("Supply ship");
                root.layer = Factions.PlayerShipLayer;
                root.SetActive(false);
                root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, heading, 0f));
                var ship = root.AddComponent<SupplyShip>();
                ship._flagship = flagship;
                ship._heading = heading;
                ship._hp = SupplyHullHp;
                ship._hull = root.AddComponent<BoxCollider>();
                // 기함(약 3×10m)보다 조금 큰 화물선: 모델 15.5m를 0.75배
                ship._hull.size = new Vector3(4.4f, 3f, 16f) * VisualScale;
                ship._hull.center = new Vector3(0f, 1f, 0f) * VisualScale;
                ship._visual = BuildModel(root.transform);
                ship._visual.localScale = Vector3.one * VisualScale;
                Game.View.ShipWake.Ensure(root, () => GameManager.Instance != null && GameManager.Instance.State == GameState.Playing ? 1f : 0f);
                root.SetActive(true);
                return ship;
            }

            private void OnEnable() { if (IsAlive) TargetRegistry.Register(this); }
            private void OnDisable() => Release();

            private void Release()
            {
                TargetRegistry.Unregister(this);
                TargetAllocator.ReleaseTarget(transform);
            }

            public void TakeDamage(in DamageInfo info)
            {
                if (!IsAlive) return;
                _hp -= info.Amount;
                if (_hp > 0f) return;
                _hp = 0f;
                _sinking = true;
                if (_hull != null) _hull.enabled = false;
                Release();
                Explosions.Spawn(transform.position + Vector3.up * 1.5f, 1.6f, Explosions.Kind.Impact);
                AudioManager.Play(Game.Data.SfxId.Explosion, transform.position);
            }

            /// <summary>임무 성공: 표적에서 빠지고 기함 반대쪽으로 물러난 뒤 사라진다.</summary>
            public void Depart()
            {
                _departing = true;
                if (_hull != null) _hull.enabled = false;
                Release();
                Destroy(gameObject, 12f);
            }

            private void Update()
            {
                if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing && !_sinking) return;
                float dt = Time.deltaTime;
                if (_sinking)
                {
                    _sinkAge += dt;
                    transform.position += Vector3.down * (0.7f * dt);
                    if (_visual != null) _visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Min(24f, _sinkAge * 6f));
                    if (_sinkAge > 6f) Destroy(gameObject);
                    return;
                }

                float desired = _heading, wantSpeed;
                if (_departing || _flagship == null)
                {
                    wantSpeed = MaxSpeed;
                }
                else
                {
                    Vector3 slot = _flagship.position - _flagship.forward * 20f - _flagship.right * 14f;
                    Vector3 to = slot - transform.position;
                    to.y = 0f;
                    float dist = to.magnitude;
                    if (dist > 4f) desired = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    else desired = _flagship.eulerAngles.y;
                    // 자리에 가까우면 기함 속력에 맞추고, 멀면 최고 속력. 자리가 뒤에 있으면 줄인다.
                    float ahead = Vector3.Dot(Quaternion.Euler(0f, _heading, 0f) * Vector3.forward, dist > 0.01f ? to / dist : Vector3.zero);
                    wantSpeed = Mathf.Clamp(dist * 0.45f, 0f, MaxSpeed) * Mathf.Clamp01(ahead + 0.6f);
                }
                _heading = Mathf.MoveTowardsAngle(_heading, desired, TurnRate * dt * Mathf.Lerp(0.5f, 1f, _speed / MaxSpeed));
                _speed = Mathf.MoveTowards(_speed, wantSpeed, Accel * dt);
                var rot = Quaternion.Euler(0f, _heading, 0f);
                Vector3 p = transform.position + rot * Vector3.forward * (_speed * dt);
                p.y = 0f;
                transform.SetPositionAndRotation(p, rot);
            }

            /// <summary>회색박스 화물선: 붉은 선체, 흰 선교(뒤쪽), 갑판의 컨테이너 줄.</summary>
            private static Transform BuildModel(Transform root)
            {
                var visual = new GameObject("Supply visual").transform;
                visual.SetParent(root, false);
                Box(visual, "Hull", new Vector3(0f, 0.2f, 0f), new Vector3(4.2f, 2.2f, 15.5f), new Color(0.42f, 0.13f, 0.1f));
                Box(visual, "Deck", new Vector3(0f, 1.32f, 0f), new Vector3(4f, 0.08f, 15f), new Color(0.32f, 0.33f, 0.31f));
                Box(visual, "Bow", new Vector3(0f, 0.6f, 7.9f), new Vector3(2.4f, 1.6f, 1.2f), new Color(0.42f, 0.13f, 0.1f));
                Box(visual, "Bridge", new Vector3(0f, 2.6f, -5.6f), new Vector3(3.6f, 2.5f, 2.6f), new Color(0.88f, 0.88f, 0.84f));
                Box(visual, "Bridge windows", new Vector3(0f, 3.3f, -4.28f), new Vector3(3.2f, 0.45f, 0.06f), new Color(0.08f, 0.12f, 0.16f));
                Box(visual, "Funnel", new Vector3(0f, 4.3f, -6.4f), new Vector3(1f, 1.4f, 1f), new Color(0.15f, 0.15f, 0.16f));
                Color[] colors = { new(0.15f, 0.35f, 0.6f), new(0.75f, 0.45f, 0.12f), new(0.2f, 0.45f, 0.3f), new(0.6f, 0.18f, 0.16f) };
                int k = 0;
                for (int row = 0; row < 4; row++)
                    for (int col = -1; col <= 1; col += 2)
                        Box(visual, "Container", new Vector3(col * 0.95f, 1.95f, -2.6f + row * 2.6f), new Vector3(1.75f, 1.2f, 2.4f), colors[k++ % colors.Length]);
                Box(visual, "Container top", new Vector3(0f, 3.15f, 0f), new Vector3(1.75f, 1.2f, 2.4f), colors[1]);
                return visual;
            }

            private static Material s_lit;

            private static void Box(Transform parent, string name, Vector3 position, Vector3 size, Color color)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                Destroy(go.GetComponent<Collider>());
                go.layer = parent.gameObject.layer;
                go.transform.SetParent(parent, false);
                go.transform.localPosition = position;
                go.transform.localScale = size;
                var r = go.GetComponent<MeshRenderer>();
                if (s_lit == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit");
                    s_lit = new Material(shader != null ? shader : Shader.Find("Standard")) { name = "Supply ship" };
                    if (s_lit.HasProperty("_Smoothness")) s_lit.SetFloat("_Smoothness", 0.25f);
                }
                r.sharedMaterial = s_lit;
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", color);
                r.SetPropertyBlock(block);
            }
        }
    }
}

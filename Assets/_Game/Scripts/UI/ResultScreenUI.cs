using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 승리/패배 화면. 상태 변경 이벤트만 보고 켜진다.
    /// 2026-10-11 점검 U02: 결과 한 줄(패배면 격침 원인) · 이번 판 요약 · 재출격/항구/종료 버튼.
    ///   - 재출격: 같은 시작 함정으로 씬을 다시 열어 바로 출항(Enter·R). 항구: 시작 화면에서 함정을 다시 고른다(H).
    ///   - 버튼은 씬을 다시 만들지 않도록 처음 열 때 코드로 붙인다(제목·설명 글자는 씬의 것을 그대로 쓴다).
    /// 점검 O02: 아래에 결과 분석 두 칸 — 왼쪽 무장 기여(실제로 깎은 체력·비율·격침, RunReport), 오른쪽 적 미사일 처리
    /// (요격/피격/빗나감, CombatStats.Threats)와 최종 빌드(블록 수·역할 구성·성장 카드·편대·교리).
    /// </summary>
    public class ResultScreenUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text detailText;

        private static readonly Color ButtonIdle = new Color(0.035f, 0.19f, 0.12f, 1f);
        private static readonly Color ButtonMain = new Color(0.12f, 0.52f, 0.29f, 1f);
        private static readonly Color Muted = new Color(0.66f, 1f, 0.75f);

        private bool _built;
        private TMP_Text _contribution, _overview, _footer;
        private Game.Data.StartingShipConcept _concept;
        private float _inputReadyAt;

        /// <summary>격침 순간 누르던 R(응급수리)·H(헬기) 입력이 결과 화면 단축키로 새지 않게 잠시 막는 시간.</summary>
        private const float InputGraceSeconds = 0.6f;

        private void OnEnable() => GameEvents.StateChanged += OnStateChanged;
        private void OnDisable() => GameEvents.StateChanged -= OnStateChanged;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
        }

        public bool IsShown => root != null && root.activeSelf;

        /// <summary>검증용: 결과 화면이 막 떠서 키 입력을 아직 받지 않는 중인가.</summary>
        public bool InputBlocked => IsShown && Time.unscaledTime < _inputReadyAt;

        private void OnStateChanged(GameState state)
        {
            bool show = state == GameState.Victory || state == GameState.GameOver;
            if (root != null) root.SetActive(show);
            if (!show) return;
            _inputReadyAt = Time.unscaledTime + InputGraceSeconds;

            var init = FindFirstObjectByType<Game.Ship.ShipInitializer>();
            _concept = init != null ? init.SelectedConcept : null;
            UiSound.Play(state == GameState.Victory ? UiSound.Cue.Victory : UiSound.Cue.Defeat);
            AchievementTracker.RecordRunEnd(state);   // 기록·판 끝 도전 과제를 먼저(점검 K06)
            BuildButtons();

            if (titleText != null)
                titleText.text = state == GameState.Victory ? "VICTORY" : "SHIP LOST";

            if (detailText != null && GameManager.Instance != null)
                detailText.text = Summary(state == GameState.Victory);
            if (_contribution != null) _contribution.text = Contribution();
            if (_overview != null) _overview.text = Overview();
            if (_footer != null) _footer.text = $"런 시드 {RunSeed.Current} · {Diagnostics.Version} · 오류 제보: 설정 › 정보 › 진단 내보내기";
        }

        public string ContributionText => _contribution != null ? _contribution.text : "";
        public string OverviewText => _overview != null ? _overview.text : "";

        /// <summary>무장 기여: 실제로 깎은 체력이 큰 순 5개(명중 수가 아니라 피해량).</summary>
        private static string Contribution()
        {
            var sb = new System.Text.StringBuilder("<color=#e0c05a><b>무장 기여</b></color>  <size=80%><color=#9fb3c0>적에게 실제로 깎은 체력</color></size>");
            var ranked = Game.Combat.RunReport.Ranked();
            float total = Mathf.Max(1f, Game.Combat.RunReport.TotalDamage);
            if (ranked.Count == 0) sb.Append("\n<color=#9fb3c0>기록 없음</color>");
            for (int i = 0; i < ranked.Count && i < 5; i++)
            {
                var e = ranked[i];
                sb.Append($"\n{e.Key}<pos=42%>{e.Damage:N0}  <color=#9fb3c0>{e.Damage / total * 100f:0}%</color><pos=78%>격침 {e.Kills}");
            }
            if (ranked.Count > 5)
            {
                float rest = 0f; int kills = 0;
                for (int i = 5; i < ranked.Count; i++) { rest += ranked[i].Damage; kills += ranked[i].Kills; }
                sb.Append($"\n<color=#9fb3c0>그 밖 {ranked.Count - 5}종</color><pos=42%>{rest:N0}  <color=#9fb3c0>{rest / total * 100f:0}%</color><pos=78%>격침 {kills}");
            }
            return sb.ToString();
        }

        /// <summary>적 미사일 처리와 최종 빌드.</summary>
        private string Overview()
        {
            if (Ruleset.IsWw2) return Ww2Overview();
            var sb = new System.Text.StringBuilder("<color=#e0c05a><b>적 미사일</b></color>");
            int Count(Game.Combat.ThreatOutcome o) => Game.Combat.CombatStats.Threats.TryGetValue(o, out var t) ? t.Count : 0;
            int intercepted = Count(Game.Combat.ThreatOutcome.Intercepted), hit = Count(Game.Combat.ThreatOutcome.HitShip), lost = Count(Game.Combat.ThreatOutcome.Lost);
            int all = intercepted + hit + lost;
            sb.Append(all == 0 ? "\n<color=#9fb3c0>없음</color>"
                : $"\n{all}발 — 요격 {intercepted} · <color=#FF9A7A>피격 {hit}</color> · 빗나감 {lost}  <color=#9fb3c0>(막은 비율 {(intercepted + lost) * 100f / all:0}%)</color>");

            var ship = GameManager.Instance != null ? GameManager.Instance.Player : null;
            var grid = ship != null ? ship.Grid : null;
            int blocks = 0;
            if (grid != null) foreach (var m in grid.Modules) if (m != null && !m.IsDestroyed) blocks++;
            var c = BuildGuide.Count(grid);
            var fleet = ship != null ? ship.GetComponent<Game.TaskForce.TaskForceEscortFormation>() : null;
            sb.Append("\n\n<color=#e0c05a><b>최종 빌드</b></color>");
            sb.Append($"\n{(_concept != null ? _concept.Title : "기본 함정")} · 블록 {blocks}개 · 대공 {c.Air} · 대함 {c.Surface} · 대잠 {c.Sub}");
            sb.Append($"\n성장 카드 {Game.Modules.RunUpgrades.Cards.Count}장 · 편대 {(fleet != null ? fleet.EscortCount : 0)}척 · {CombatPolicies.DoctrineLabel}");

            // 이번 판에 달성한 도전 과제(점검 K06) — 두 개까지 이름, 나머지는 수만
            var news = PlayerProfile.NewThisRun;
            if (news.Count > 0)
            {
                sb.Append($"\n<size=88%><color=#FFD166>도전 과제 달성 {news.Count}</color> ");
                for (int i = 0; i < news.Count && i < 2; i++)
                    if (PlayerProfile.Find(news[i]) is { } a) sb.Append($"{(i > 0 ? " · " : "")}{a.Name}「{a.Title}」");
                if (news.Count > 2) sb.Append($" 외 {news.Count - 2}");
                sb.Append("</size>");
            }
            return sb.ToString();
        }

        /// <summary>
        /// WWII 시험 전투(패치 B): 적 미사일 대신 공격 단계 기록 — 공습·포격이 몇 번 와서 몇 번 맞았고(피격),
        /// 몇 번 피했고(빗나감), 몇 번 투하 전에 떨어뜨렸는지. 내 현측 어뢰 명중률도 함께.
        /// </summary>
        private string Ww2Overview()
        {
            var sb = new System.Text.StringBuilder("<color=#e0c05a><b>공습 · 어뢰 · 포격</b></color>");
            bool any = false;
            foreach (var a in Game.WW2.Ww2BattleLog.All)
            {
                any = true;
                bool mine = a.Name == Game.WW2.Ww2BattleLog.PlayerTorpedo;
                if (mine) sb.Append($"\n{a.Name} {a.Released}발 — 명중 {a.Hits} · 빗나감 {a.Misses}");
                else
                {
                    string cancelled = a.Cancelled > 0 ? $" · 투하 전 격추 {a.Cancelled}" : "";
                    sb.Append($"\n{a.Name} — <color=#FF9A7A>피격 {a.Hits}</color> · 피함 {a.Misses}{cancelled}");
                }
            }
            if (!any) sb.Append("\n<color=#9fb3c0>없음</color>");
            var ship = GameManager.Instance != null ? GameManager.Instance.Player : null;
            var grid = ship != null ? ship.Grid : null;
            int blocks = 0;
            if (grid != null) foreach (var m in grid.Modules) if (m != null && !m.IsDestroyed) blocks++;
            sb.Append("\n\n<color=#e0c05a><b>최종 빌드</b></color>");
            sb.Append($"\n{(_concept != null ? _concept.Title : "기본 함정")} · 블록 {blocks}개 · 성장 카드 {Game.Modules.RunUpgrades.Cards.Count}장");
            sb.Append("\n<size=88%><color=#9fb3c0>WWII 시험 전투 — 기록·도전 과제·이어하기에 남기지 않음</color></size>");
            return sb.ToString();
        }

        private void Update()
        {
            if (!IsShown || RunRestart.InProgress || Keyboard.current == null) return;
            if (Time.unscaledTime < _inputReadyAt) return;
            var kb = Keyboard.current;
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.rKey.wasPressedThisFrame) Retry();
            else if (kb.hKey.wasPressedThisFrame) RunRestart.ReturnToHarbor();
        }

        private void Retry() => RunRestart.Retry(_concept);

        /// <summary>결과 한 줄 + 이번 판 요약.</summary>
        private string Summary(bool victory)
        {
            var gm = GameManager.Instance;
            var ship = gm.Player;
            string headline;
            if (victory) headline = "<color=#FFD27A>모든 해역 돌파 — 작전 성공</color>";
            else
            {
                string last = ship != null ? ship.LastHullDamageCause : null;
                var (top, share) = ship != null ? ship.TopHullDamageCause() : (null, 0f);
                headline = $"<color=#FF9A7A>격침 원인: {last ?? "알 수 없음"}</color>";
                // 보스전에서 졌으면 다음 시도의 대응법(사전과 같은 글)을 같이 보여 준다(점검 K05)
                var boss = FindFirstObjectByType<BossBarUI>()?.Boss;
                if (boss != null && boss.IsAlive && boss.Definition != null && CodexCatalog.CounterTip(boss.Definition) is { } tip)
                    headline = $"<size=80%><color=#e0c05a>보스 {boss.Definition.DisplayName} 대응</color> {tip}</size>\n" + headline;
                if (top != null && top != last) headline += $"   <size=80%>(가장 큰 피해: {top} {share * 100f:0}%)</size>";
                else if (top != null) headline += $"   <size=80%>(선체 피해의 {share * 100f:0}%)</size>";
            }

            var stage = FindFirstObjectByType<StageDirector>();
            var xp = FindFirstObjectByType<Game.Progression.ExperienceSystem>();
            int seconds = Mathf.FloorToInt(gm.ElapsedTime);
            string shipName = _concept != null ? _concept.Title : "기본 함정";
            if (PlayerProfile.Title is { } captain) shipName = $"「{captain}」 {shipName}";   // 칭호(점검 K06)
            return $"{headline}\n<size=75%><color=#A8FFC0>{shipName} · 스테이지 {(stage != null ? stage.StageNumber : 1)} · " +
                   $"레벨 {(xp != null ? xp.Level : 1)} · 격침 {gm.TotalKills}척 · 작전 시간 {seconds / 60}:{seconds % 60:00}</color></size>";
        }

        private void BuildButtons()
        {
            if (_built || root == null) return;
            _built = true;
            var font = titleText != null ? titleText.font : TMP_Settings.defaultFontAsset;

            // 제목을 조금 올리고 설명을 두 줄로 넓힌다
            if (titleText != null) titleText.rectTransform.anchoredPosition = new Vector2(0f, 285f);
            if (detailText != null)
            {
                detailText.rectTransform.anchoredPosition = new Vector2(0f, 125f);
                detailText.rectTransform.sizeDelta = new Vector2(1300f, 180f);
                detailText.fontSize = 32;
                detailText.textWrappingMode = TextWrappingModes.Normal;
            }

            // 결과 분석 두 칸(O02)
            _contribution = Text(root.transform, font, "Contribution", "", new Vector2(-300f, -105f), new Vector2(560f, 190f), 19, Color.white);
            _contribution.alignment = TextAlignmentOptions.TopLeft;
            _overview = Text(root.transform, font, "Overview", "", new Vector2(300f, -105f), new Vector2(560f, 190f), 19, Color.white);
            _overview.alignment = TextAlignmentOptions.TopLeft;
            _overview.textWrappingMode = TextWrappingModes.Normal;

            Button(root.transform, font, "재출격  [Enter]", new Vector2(-350f, -265f), ButtonMain, Retry);
            Button(root.transform, font, "항구로  /  함정 변경  [H]", new Vector2(0f, -265f), ButtonIdle, RunRestart.ReturnToHarbor);
            Button(root.transform, font, "게임 종료", new Vector2(350f, -265f), ButtonIdle, RunRestart.Quit);
            var hint = Text(root.transform, font, "Hint", "재출격은 같은 시작 함정으로 처음부터 다시 시작합니다. 성장·편대·교리는 초기화됩니다.",
                new Vector2(0f, -330f), new Vector2(1100f, 34f), 18, Muted);
            _footer = Text(root.transform, font, "Footer", "", new Vector2(0f, -370f), new Vector2(1100f, 26f), 15, new Color(0.62f, 0.7f, 0.74f));
            _footer.alignment = TextAlignmentOptions.Center;
            hint.alignment = TextAlignmentOptions.Center;
        }

        private static void Button(Transform parent, TMP_FontAsset font, string title, Vector2 position, Color color, System.Action action)
        {
            var go = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(320f, 64f);
            var image = go.GetComponent<Image>();
            image.color = color;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => { if (!RunRestart.InProgress) action(); });
            var label = Text(rect, font, "Text", title, Vector2.zero, rect.sizeDelta, 22, Color.white);
            label.alignment = TextAlignmentOptions.Center;
        }

        private static TMP_Text Text(Transform parent, TMP_FontAsset font, string name, string value, Vector2 position, Vector2 size, float fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.raycastTarget = false;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            text.rectTransform.anchoredPosition = position;
            text.rectTransform.sizeDelta = size;
            return text;
        }
    }
}

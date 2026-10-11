using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using Game.Data;
using Game.Enemies;
using Game.Progression;
using Game.Refit;

namespace Game.UI
{
    /// <summary>
    /// 첫 출격 훈련(2026-10-11 점검 U03). 시작 화면의 [훈련]으로 들어오는 3~5분짜리 안내 판. 일반 웨이브 대신 단계마다 적을 하나씩 낸다:
    ///   1 조타: 전령기를 올려 출항하고 타로 90° 돌기
    ///   2 교전: 고속정 3척 — 무장은 자동으로 쏜다, 붉은 ▼가 적
    ///   3 미사일 방어: 미사일정 1척 — 붉은 괄호가 오면 기만체, CIWS가 마무리
    ///   4 정비: 레벨업 카드 고르기·배치(이번 정비에는 대잠 블록 카드가 들어온다 — RefitDraft.ForceAntiSubOnce).
    ///     앞 단계 격침으로 이미 레벨업 정비를 했으면 건너뛴다. 정비 화면이 열려 있는 동안은 안내판이 정비 설명으로 바뀐다.
    ///   5 대잠: 잠수함 1척 — 어뢰 경고에 진로를 바꾸고, 폭뢰·소나로 격침
    /// 단계마다 시간 제한이 있어 막히면 다음으로 넘어간다. 왼쪽 위 안내판에 [건너뛰기](항구로). 끝나면 항구로 / 바로 출격.
    /// 완료하면 PlayerPrefs "Naval.Training.Done" — 시작 화면이 훈련을 처음 온 사람에게만 권한다.
    /// </summary>
    public sealed class TrainingDirector : MonoBehaviour
    {
        public const string DonePref = "Naval.Training.Done";
        public static bool Done => PlayerPrefs.GetInt(DonePref, 0) == 1;
        public static TrainingDirector Active { get; private set; }

        private static readonly Color Panel = new(0.015f, 0.09f, 0.065f, 0.94f);
        private static readonly Color Accent = new(0.12f, 0.52f, 0.29f, 1f);
        private static readonly Color Text = new(0.66f, 1f, 0.75f);
        private static readonly Color Key = new(0.88f, 0.75f, 0.35f);

        private enum Step { Helm, Engage, MissileDefense, Refit, AntiSub, Done }
        private static readonly float[] Limits = { 60f, 90f, 120f, 90f, 150f, 0f };

        private Step _step;
        private float _stepStarted;
        private TMP_FontAsset _font;
        private RectTransform _panel;
        private TMP_Text _header, _body, _progress;
        private GameObject _finish;
        private Vector3 _startForward;
        private float _turned;
        private Vector3 _lastForward;
        private readonly List<EnemyController> _targets = new();
        private bool _decoyUsed, _sawRefit;

        public int StepIndex => (int)_step;
        public bool Finished => _step == Step.Done;

        /// <summary>훈련 시작(출항 직후 StageDirector가 부른다).</summary>
        public static TrainingDirector Begin(Transform canvas, TMP_FontAsset font)
        {
            var go = new GameObject("Training", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var t = go.AddComponent<TrainingDirector>();
            t._font = font;
            t.Build(rect);
            Active = t;
            RefitDraft.ForceAntiSubOnce = true;
            var gm = GameManager.Instance;
            if (EnemySpawner.Instance != null && gm != null && gm.Player != null) EnemySpawner.Instance.SetPlayer(gm.Player.transform);
            t.Enter(Step.Helm);
            return t;
        }

        private void OnEnable()
        {
            GameEvents.DecoyRequested += OnDecoy;
            GameEvents.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.DecoyRequested -= OnDecoy;
            GameEvents.StateChanged -= OnStateChanged;
        }

        /// <summary>정비 화면이 열리면(언제든) 정비 설명을 위에 띄우고, 닫히면 지금 단계 설명으로 돌아간다.</summary>
        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Refit)
            {
                _sawRefit = true;
                transform.SetAsLastSibling();
                Show("정비", RefitText);
            }
            else if (state == GameState.Playing && _step != Step.Done && _header != null && _header.text.EndsWith("정비"))
                ShowStep(_step);
        }

        private const string RefitText = "레벨이 오르면 시간이 멈추고 카드 3장이 나옵니다(숫자 1~3). 블록 카드를 고르면 함선 격자에 놓습니다 — " +
                                         "초록 부채꼴이 사격각, 붉은 쪽은 구조물에 가린 방향. 이번에는 <b>대잠 블록</b>을 놓아 보세요.";
        private void OnDestroy() { if (Active == this) Active = null; }
        private void OnDecoy() => _decoyUsed = true;

        private static string K(NavalControl c) => $"<color=#e0c05a>[{GameSettings.BindingLabel(c)}]</color>";

        private void Enter(Step step)
        {
            _step = step;
            _stepStarted = Time.time;
            _targets.Clear();
            var ship = GameManager.Instance != null ? GameManager.Instance.Player : null;
            switch (step)
            {
                case Step.Helm:
                    if (ship != null) { _startForward = _lastForward = Flat(ship.transform.forward); }
                    _turned = 0f;
                    break;
                case Step.Engage:
                    Spawn("ene_fastboat", 3, 34f, 40f);
                    break;
                case Step.MissileDefense:
                    _decoyUsed = false;
                    Spawn("ene_missileboat", 1, 42f, 0f);
                    break;
                case Step.Refit:
                    if (_sawRefit) { Enter(Step.AntiSub); return; }   // 앞 단계 격침으로 이미 정비를 했다
                    ShowStep(step);
                    var xp = FindFirstObjectByType<ExperienceSystem>();
                    if (xp != null && GameManager.Instance.State == GameState.Playing) xp.AddXp(Mathf.Max(1, xp.XpToNext - xp.Xp));   // 레벨을 올려 정비를 연다
                    return;
                case Step.AntiSub:
                    Spawn("ene_submarine", 1, 40f, 0f);
                    break;
                case Step.Done:
                    PlayerPrefs.SetInt(DonePref, 1);
                    PlayerPrefs.Save();
                    PlayerProfile.Unlock("training");   // 도전 과제(점검 K06) — 건너뛰기는 세지 않는다
                    _finish.SetActive(true);
                    break;
            }
            ShowStep(step);
        }

        private void ShowStep(Step step)
        {
            switch (step)
            {
                case Step.Helm:
                    Show("1 / 5  조타", $"{K(NavalControl.ThrottleUp)}로 기관 전령기를 올려 출항하고, {K(NavalControl.RudderLeft)} / {K(NavalControl.RudderRight)}로 타를 써서 90° 돌아 보세요. " +
                         $"타는 놓아도 그대로 남습니다 — {K(NavalControl.CenterRudder)}로 가운데.");
                    break;
                case Step.Engage:
                    Show("2 / 5  교전", "무장은 사거리 안의 적을 <b>자동으로</b> 쏩니다. 붉은 ▼ 표식이 적, 청록 ◆가 아군입니다. " +
                         "고속정 3척에게 다가가 격침하세요. 오른쪽 아래 무장 줄을 누르면 쏘지 않는 이유가 보입니다.");
                    break;
                case Step.MissileDefense:
                    Show("3 / 5  미사일 방어", $"미사일정이 대함미사일을 쏩니다. 붉은 괄호(가장 급한 것은 ▶)가 다가오면 {K(NavalControl.Decoy)} 기만체로 유인하세요 — " +
                         "남은 미사일은 CIWS가 막습니다. 미사일정을 격침하세요.");
                    break;
                case Step.Refit:
                    Show("4 / 5  정비", RefitText);
                    break;
                case Step.AntiSub:
                    Show("5 / 5  대잠", "잠수함은 잠항 중엔 보이지 않습니다. 소나·폭뢰가 찾아 공격합니다. 붉은 \"어뢰 준비\"가 뜨면 " +
                         $"전령기({K(NavalControl.ThrottleUp)}/{K(NavalControl.ThrottleDown)})나 타로 진로를 바꿔 어뢰를 피하세요. 잠수함을 격침하세요.");
                    break;
                case Step.Done:
                    Show("훈련 완료", "조타 · 자동 교전 · 미사일 방어 · 정비 · 대잠을 모두 해 보았습니다. 설정 › 조작키에서 키를 바꿀 수 있고, " +
                         "ESC 일시정지 메뉴에서 사전(함정·용어 탭)을 볼 수 있습니다.");
                    break;
            }
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.IsRunOver) { if (_panel != null) _panel.gameObject.SetActive(false); return; }
            _panel.gameObject.SetActive(gm.State is GameState.Playing or GameState.Refit);
            if (_step == Step.Done || gm.State != GameState.Playing) return;

            float elapsed = Time.time - _stepStarted;
            bool timeUp = elapsed > Limits[(int)_step];
            var ship = gm.Player;
            switch (_step)
            {
                case Step.Helm:
                    if (ship == null) break;
                    var f = Flat(ship.transform.forward);
                    _turned += Vector3.Angle(_lastForward, f);
                    _lastForward = f;
                    float speed01 = ship.BaseMaxSpeed > 0f ? Mathf.Abs(ship.CurrentSpeed) / ship.BaseMaxSpeed : 0f;
                    _progress.text = $"속력 {speed01 * 100f:0}%  ·  선회 {Mathf.Min(_turned, 90f):0}° / 90°";
                    if ((speed01 > 0.25f && _turned >= 90f) || timeUp) Enter(Step.Engage);
                    break;
                case Step.Engage:
                case Step.MissileDefense:
                case Step.AntiSub:
                    int alive = 0;
                    foreach (var e in _targets) if (e != null && e.isActiveAndEnabled && e.IsAlive) alive++;
                    _progress.text = _step == Step.MissileDefense
                        ? $"남은 적 {alive} · 기만체 {(_decoyUsed ? "사용함" : "아직")}"
                        : $"남은 적 {alive} / {_targets.Count}";
                    if (alive == 0 || timeUp)
                    {
                        if (alive > 0) EnemySpawner.Instance?.ClearAll();   // 시간이 다 됐으면 남은 적을 치우고 다음 단계로
                        Enter(_step == Step.Engage ? Step.MissileDefense : _step == Step.MissileDefense ? Step.Refit : Step.Done);
                    }
                    break;
                case Step.Refit:
                    _progress.text = _sawRefit ? "정비 완료" : "레벨업 정비를 기다리는 중…";
                    if (_sawRefit || timeUp) Enter(Step.AntiSub);
                    break;
            }
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward; }

        /// <summary>기함 앞쪽 부채꼴에 적을 낸다(일반 웨이브와 같은 데이터).</summary>
        private void Spawn(string id, int count, float distance, float spreadDeg)
        {
            var gm = GameManager.Instance;
            var spawner = EnemySpawner.Instance;
            if (gm == null || gm.Player == null || spawner == null) return;
            EnemyDefinition def = null;
            foreach (var (d, _) in CodexCatalog.CollectEnemies()) if (d.Id == id) { def = d; break; }
            if (def == null) { Debug.LogWarning($"[Training] 적 데이터 없음: {id}"); return; }
            var ship = gm.Player.transform;
            Vector3 fwd = Flat(ship.forward);
            for (int i = 0; i < count; i++)
            {
                float a = count > 1 ? Mathf.Lerp(-spreadDeg * 0.5f, spreadDeg * 0.5f, i / (float)(count - 1)) : 0f;
                Vector3 p = ship.position + Quaternion.Euler(0f, a, 0f) * fwd * distance;
                p.y = 0f;
                var e = spawner.SpawnAt(def, p, Quaternion.LookRotation(-fwd));
                if (e != null) _targets.Add(e);
            }
        }

        // ------------------------------------------------------------ 안내판

        private void Show(string header, string body)
        {
            _header.text = $"첫 출격 훈련  ·  {header}";
            _body.text = body;
            _progress.text = "";
            _body.ForceMeshUpdate();
            float h = Mathf.Max(60f, _body.preferredHeight);
            _body.rectTransform.sizeDelta = new Vector2(452f, h);
            _panel.sizeDelta = new Vector2(480f, h + 104f);
            _body.rectTransform.anchoredPosition = new Vector2(14f, 58f);
        }

        private void Build(RectTransform root)
        {
            _panel = Box(root, "Training panel", new Vector2(0f, 1f), Panel);
            _panel.pivot = new Vector2(0f, 1f);
            _panel.anchoredPosition = new Vector2(14f, -14f);
            _panel.sizeDelta = new Vector2(480f, 200f);
            var top = Box(_panel, "Top", new Vector2(0f, 1f), Accent);
            top.pivot = new Vector2(0f, 1f);
            top.sizeDelta = new Vector2(480f, 4f);
            _header = Label(_panel, "Header", 18, Color.white, new Vector2(0f, 1f));
            _header.rectTransform.pivot = new Vector2(0f, 1f);
            _header.rectTransform.anchoredPosition = new Vector2(14f, -10f);
            _header.rectTransform.sizeDelta = new Vector2(360f, 26f);
            _body = Label(_panel, "Body", 15, Color.white, Vector2.zero);
            _body.textWrappingMode = TextWrappingModes.Normal;
            _body.alignment = TextAlignmentOptions.TopLeft;
            _body.rectTransform.pivot = Vector2.zero;
            _progress = Label(_panel, "Progress", 14, Text, Vector2.zero);
            _progress.rectTransform.pivot = Vector2.zero;
            _progress.rectTransform.anchoredPosition = new Vector2(14f, 14f);
            _progress.rectTransform.sizeDelta = new Vector2(300f, 24f);

            var skip = Button(_panel, "건너뛰기", new Vector2(1f, 1f), new Vector2(-10f, -8f), new Vector2(96f, 30f));
            skip.name = "Training skip";
            skip.onClick.AddListener(Skip);

            _finish = new GameObject("Finish", typeof(RectTransform));
            _finish.transform.SetParent(_panel, false);
            var fr = (RectTransform)_finish.transform;
            fr.anchorMin = fr.anchorMax = Vector2.zero;
            fr.pivot = Vector2.zero;
            fr.anchoredPosition = Vector2.zero;
            fr.sizeDelta = new Vector2(480f, 50f);
            var harbor = Button(fr, "항구로", Vector2.zero, new Vector2(150f, 10f), new Vector2(150f, 36f));
            harbor.name = "Training harbor";
            harbor.onClick.AddListener(RunRestart.ReturnToHarbor);
            var sortie = Button(fr, "바로 출격", Vector2.zero, new Vector2(310f, 10f), new Vector2(156f, 36f));
            sortie.name = "Training sortie";
            sortie.GetComponent<Image>().color = Accent;
            sortie.onClick.AddListener(() =>
            {
                var init = FindFirstObjectByType<Game.Ship.ShipInitializer>();
                RunRestart.Retry(init != null ? init.SelectedConcept : null);
            });
            _finish.SetActive(false);
        }

        /// <summary>건너뛰기: 훈련을 마친 것으로 치고 항구로.</summary>
        public void Skip()
        {
            PlayerPrefs.SetInt(DonePref, 1);
            PlayerPrefs.Save();
            RunRestart.ReturnToHarbor();
        }

        private static RectTransform Box(Transform parent, string name, Vector2 anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return rect;
        }

        private TMP_Text Label(Transform parent, string name, float size, Color color, Vector2 anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = _font;
            text.fontSize = size;
            text.color = color;
            text.raycastTarget = false;
            text.richText = true;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = anchor;
            return text;
        }

        private Button Button(Transform parent, string title, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = Box(parent, title, anchor, new Color(0.035f, 0.19f, 0.12f, 1f));
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var label = Label(rect, "Text", 16, Color.white, Vector2.zero);
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.alignment = TextAlignmentOptions.Center;
            label.text = title;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            return button;
        }
    }
}

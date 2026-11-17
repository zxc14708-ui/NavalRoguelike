using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using Game.Data;

namespace Game.UI
{
    /// <summary>
    /// 출격 기록 · 도전 과제 화면(2026-10-11 점검 K06). 항구 콘솔의 [기록] 버튼이 연다.
    ///   - 왼쪽: 전체 요약, 함정별 기록(출격 · 승리 · 최고 스테이지 · 최다 격침 · 가장 빠른 승리 · 마지막 패배 원인)
    ///   - 오른쪽: 도전 과제 목록(달성/잠김 · 조건 · 칭호). 달성한 줄을 누르면 그 칭호를 단다.
    ///   - 아래: 고난도 출항 켜기/끄기(스테이지 3 도달로 해금)
    /// 기록은 <see cref="PlayerProfile"/>이 갖고 있고, 이 화면은 읽고 칭호·고난도 출항 선택만 바꾼다. ESC로 닫는다.
    /// </summary>
    public sealed class RecordsUI : MonoBehaviour
    {
        private static readonly Color Backdrop = new(0.005f, 0.025f, 0.03f, 0.97f);
        private static readonly Color ConsoleBackground = new(0.015f, 0.09f, 0.065f, 1f);
        private static readonly Color ConsoleIdle = new(0.035f, 0.19f, 0.12f, 1f);
        private static readonly Color ConsoleActive = new(0.12f, 0.52f, 0.29f, 1f);
        private static readonly Color ConsoleText = new(0.66f, 1f, 0.75f);
        private static readonly Color PagePanel = new(0.012f, 0.065f, 0.05f, 0.85f);
        private static readonly Color Locked = new(0.03f, 0.05f, 0.05f, 1f);
        private static readonly Color Gold = new(1f, 0.82f, 0.4f);

        private TMP_FontAsset _font;
        private TMP_Text _summary, _ships, _dangerNote;
        private Button _danger;
        private readonly List<(string id, Image image, TMP_Text text)> _rows = new();

        /// <summary>검증용: 달성 표시가 된 줄 수와 전체 줄 수.</summary>
        public int UnlockedRows { get; private set; }
        public int RowCount => _rows.Count;
        public string ShipsText => _ships != null ? _ships.text : "";

        public static RecordsUI Create(Transform parent, TMP_FontAsset font)
        {
            var overlay = Box(parent, "Records overlay", Vector2.zero, Vector2.zero, Backdrop, true);
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            var ui = overlay.gameObject.AddComponent<RecordsUI>();
            ui._font = font;
            ui.Build(overlay);
            return ui;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Refresh();
        }

        public void Close() => gameObject.SetActive(false);

        private void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true) Close();
        }

        // ------------------------------------------------------------ 화면 구성

        private void Build(RectTransform overlay)
        {
            var content = Box(overlay, "Records console", new Vector2(-580, -335), new Vector2(1160, 670), ConsoleBackground);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            Box(content, "Top signal", new Vector2(0, 666), new Vector2(1160, 4), ConsoleActive);
            Text(content, "Eyebrow", "FLEET COMMAND  /  SERVICE RECORD", new Vector2(26, 624), new Vector2(700, 27), 16, ConsoleText);
            Text(content, "Title", "출격 기록 · 도전 과제", new Vector2(26, 572), new Vector2(600, 51), 35, Color.white);
            var close = Btn(content, "닫기  ESC", new Vector2(1004, 578), new Vector2(130, 42), 18);
            close.onClick.AddListener(Close);

            _summary = Text(content, "Summary", "", new Vector2(26, 530), new Vector2(1108, 32), 19, Gold);

            // 왼쪽: 함정별 기록
            var left = Box(content, "Ship records", new Vector2(26, 118), new Vector2(540, 400), PagePanel);
            Text(left, "Header", "함정별 기록", new Vector2(16, 360), new Vector2(500, 30), 20, ConsoleText);
            _ships = Text(left, "Table", "", new Vector2(16, 12), new Vector2(510, 344), 16, Color.white);
            _ships.alignment = TextAlignmentOptions.TopLeft;
            _ships.richText = true;

            // 왼쪽 아래: 고난도 출항
            _danger = Btn(content, "고난도 출항", new Vector2(26, 58), new Vector2(260, 46), 19);
            _danger.name = "Danger start toggle";
            _danger.onClick.AddListener(() =>
            {
                if (!PlayerProfile.DangerStartUnlocked) { UiSound.Play(UiSound.Cue.Fail); return; }
                PlayerProfile.SetDangerStart(!PlayerProfile.DangerStart);
                UiSound.Play(UiSound.Cue.Confirm);
                Refresh();
            });
            _dangerNote = Text(content, "Danger note", "", new Vector2(300, 52), new Vector2(266, 58), 14, ConsoleText);

            // 오른쪽: 도전 과제(누르면 칭호 선택)
            var right = Box(content, "Achievements", new Vector2(590, 58), new Vector2(544, 460), PagePanel);
            Text(right, "Header", "도전 과제  —  달성한 줄을 누르면 칭호로 답니다", new Vector2(14, 424), new Vector2(520, 30), 17, ConsoleText);
            const float rowH = 26f, gap = 1.5f;
            for (int i = 0; i < PlayerProfile.Catalog.Length; i++)
            {
                var a = PlayerProfile.Catalog[i];
                var rect = Box(right, $"Achievement {a.Id}", new Vector2(10, 418 - (i + 1) * (rowH + gap)), new Vector2(524, rowH), Locked, true);
                var label = Text(rect, "Text", "", new Vector2(10, 0), new Vector2(508, rowH), 14, Color.white);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.richText = true;
                label.textWrappingMode = TextWrappingModes.NoWrap;   // 긴 조건은 줄이지 않고 글자를 작게(한 줄 고정)
                label.enableAutoSizing = true;
                label.fontSizeMin = 10f;
                label.fontSizeMax = 14f;
                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = rect.GetComponent<Image>();
                string id = a.Id;
                button.onClick.AddListener(() => ChooseTitle(id));
                _rows.Add((a.Id, rect.GetComponent<Image>(), label));
            }
            Text(content, "Footer", "칭호는 꾸밈입니다 — 전투력은 바뀌지 않습니다. 훈련 판은 기록하지 않습니다.",
                new Vector2(26, 16), new Vector2(1108, 26), 14, new Color(0.55f, 0.73f, 0.65f));
        }

        public void ChooseTitle(string id)
        {
            if (!PlayerProfile.Has(id)) { UiSound.Play(UiSound.Cue.Fail); return; }
            PlayerProfile.SetTitle(id);
            UiSound.Play(UiSound.Cue.Confirm);
            Refresh();
        }

        public void Refresh()
        {
            var d = PlayerProfile.Current;
            string title = PlayerProfile.Title;
            _summary.text = $"총 출격 {d.runs} · 승리 {d.wins} · 누적 격침 {d.totalKills} · 도전 과제 {d.achievements.Count}/{PlayerProfile.Catalog.Length}"
                            + (title != null ? $"   ·   칭호 「{title}」" : "");

            var sb = new StringBuilder();
            sb.Append("<color=#A8FFC0>함정<pos=150>출격<pos=210>승리<pos=270>최고<pos=330>최다 격침<pos=420>최단 승리</color>\n");
            var ships = new List<StartingShipConcept>();
            foreach (var s in StartingShipCatalog.All) if (s != null) ships.Add(s);
            foreach (var concept in ships)
            {
                var r = d.ships.Find(x => x.ship == concept.Id);
                sb.Append($"\n{concept.Title}");
                if (r == null || r.runs == 0) { sb.Append("<pos=150><color=#7A9A8A>아직 출격 기록 없음</color>\n"); continue; }
                sb.Append($"<pos=150>{r.runs}<pos=210>{r.wins}<pos=270>S{Mathf.Max(1, r.bestStage)}<pos=330>{r.bestKills}<pos=420>{(r.fastestWin > 0f ? Clock(r.fastestWin) : "—")}\n");
                if (!string.IsNullOrEmpty(r.lastDeath)) sb.Append($"<size=13><color=#FFB08A>  마지막 패배: {r.lastDeath}</color></size>\n");
            }
            _ships.text = sb.ToString();

            bool unlocked = PlayerProfile.DangerStartUnlocked, on = PlayerProfile.DangerStart;
            _danger.GetComponent<Image>().color = !unlocked ? Locked : on ? new Color(0.55f, 0.2f, 0.12f, 1f) : ConsoleIdle;
            _danger.GetComponentInChildren<TMP_Text>().text = !unlocked ? "고난도 출항  (잠김)" : on ? "고난도 출항  켜짐" : "고난도 출항  꺼짐";
            _dangerNote.text = unlocked
                ? "첫 스테이지부터 위험 항로: 적 +25%, 경험치 +25%. 다음 출항부터 적용."
                : "스테이지 3에 도달하면 열립니다.";

            UnlockedRows = 0;
            foreach (var (id, image, text) in _rows)
            {
                var a = PlayerProfile.Find(id).Value;
                bool has = PlayerProfile.Has(id);
                bool worn = has && d.title == id;
                if (has) UnlockedRows++;
                image.color = worn ? ConsoleActive : has ? ConsoleIdle : Locked;
                text.text = has
                    ? $"<color=#FFD166>달성</color>  <b>{a.Name}</b>  <color=#A8FFC0>— {a.Condition}</color>  <color=#FFD166>「{a.Title}」</color>{(worn ? "  <color=#FFFFFF>· 장착</color>" : "")}"
                    : $"<color=#6A7A74>잠김  <b>{a.Name}</b>  — {a.Condition}</color>";
            }
        }

        private static string Clock(float seconds) => $"{(int)(seconds / 60f)}:{(int)(seconds % 60f):00}";

        // ------------------------------------------------------------ 도우미

        private static RectTransform Box(Transform parent, string name, Vector2 position, Vector2 size, Color color, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return rect;
        }

        private TMP_Text Text(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.raycastTarget = false;
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return text;
        }

        private Button Btn(Transform parent, string title, Vector2 position, Vector2 size, float fontSize)
        {
            var rect = Box(parent, title, position, size, ConsoleIdle, true);
            var label = Text(rect, "Text", title, Vector2.zero, size, fontSize, Color.white);
            label.alignment = TextAlignmentOptions.Center;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(() => UiSound.Play(UiSound.Cue.Select));
            return button;
        }
    }
}

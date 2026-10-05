using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Game.Core;
using Game.Modules.Runtime;

namespace Game.UI
{
    /// <summary>화면 오른쪽의 VLS 사통 콘솔. 발사기별 탄종을 직접 선택한다.</summary>
    public class VlsModePanelUI : MonoBehaviour
    {
        private readonly List<VlsModule> _launchers = new();
        private RectTransform _rect;
        private TMP_Text _header, _selected, _status;
        private Image[] _modeImages;
        private int _index;
        private float _refresh;

        private static readonly Color Background = new(0.015f, 0.09f, 0.065f, 0.93f);
        private static readonly Color Idle = new(0.035f, 0.19f, 0.12f, 1f);
        private static readonly Color Active = new(0.12f, 0.52f, 0.29f, 1f);
        private static readonly Color TextGreen = new(0.66f, 1f, 0.75f);

        public static VlsModePanelUI Create(Transform parent, TMP_FontAsset font)
        {
            var go = new GameObject("VLS payload panel", typeof(RectTransform), typeof(Image), typeof(VlsModePanelUI));
            go.transform.SetParent(parent, false);
            var ui = go.GetComponent<VlsModePanelUI>();
            ui.Build(font);
            return ui;
        }

        private void Build(TMP_FontAsset font)
        {
            _rect = (RectTransform)transform;
            _rect.anchorMin = _rect.anchorMax = _rect.pivot = Vector2.one;
            _rect.anchoredPosition = new Vector2(-14f, -76f);
            _rect.sizeDelta = new Vector2(270f, 112f);
            GetComponent<Image>().color = Background;

            _header = Label(transform, font, "VLS", new Vector2(10f, -6f), new Vector2(175f, 25f), 18f);
            var previous = Button(transform, "Previous VLS", new Vector2(207f, -6f), new Vector2(25f, 25f), Idle);
            Label(previous.transform, font, "◀", new Vector2(5f, -2f), new Vector2(20f, 22f), 14f);
            previous.onClick.AddListener(() => Cycle(-1));
            var next = Button(transform, "Next VLS", new Vector2(237f, -6f), new Vector2(25f, 25f), Idle);
            Label(next.transform, font, "▶", new Vector2(5f, -2f), new Vector2(20f, 22f), 14f);
            next.onClick.AddListener(() => Cycle(1));
            _selected = Label(transform, font, "", new Vector2(10f, -31f), new Vector2(250f, 18f), 11f);

            _modeImages = new Image[3];
            string[] names = { "대함", "대공", "대잠" };
            for (int i = 0; i < names.Length; i++)
            {
                var mode = (VlsModule.PayloadMode)i;
                var button = Button(transform, names[i], new Vector2(8f + 86f * i, -51f), new Vector2(82f, 38f), Idle);
                _modeImages[i] = button.GetComponent<Image>();
                Label(button.transform, font, names[i], new Vector2(14f, -5f), new Vector2(60f, 28f), 16f);
                button.onClick.AddListener(() => { Current()?.SelectMode(mode); Refresh(); });
            }
            _status = Label(transform, font, "", new Vector2(9f, -91f), new Vector2(254f, 18f), 10f);
            Refresh();
        }

        private static Button Button(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            var button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            return button;
        }

        private static TMP_Text Label(Transform parent, TMP_FontAsset font, string value,
                                      Vector2 position, Vector2 size, float fontSize)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.color = TextGreen;
            text.raycastTarget = false;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0f, 1f);
            text.rectTransform.pivot = new Vector2(0f, 1f);
            text.rectTransform.anchoredPosition = position;
            text.rectTransform.sizeDelta = size;
            return text;
        }

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;
            if (GameSettings.Pressed(NavalControl.VlsSelector)) Cycle(1);
            _refresh -= Time.unscaledDeltaTime;
            if (_refresh <= 0f) Refresh();
        }

        private void Cycle(int direction)
        {
            if (_launchers.Count == 0) return;
            _index = (_index + direction + _launchers.Count) % _launchers.Count;
            Refresh();
        }

        private VlsModule Current() => _launchers.Count > 0 ? _launchers[Mathf.Clamp(_index, 0, _launchers.Count - 1)] : null;

        private void Refresh()
        {
            _refresh = 0.25f;
            _launchers.Clear();
            var grid = GameManager.Instance?.Player?.Grid;
            if (grid != null)
                foreach (var instance in grid.Modules)
                    if (instance != null && !instance.IsDestroyed && instance.Runtime is VlsModule vls)
                        _launchers.Add(vls);

            _index = _launchers.Count == 0 ? 0 : Mathf.Clamp(_index, 0, _launchers.Count - 1);
            var current = Current();
            if (_header == null) return;
            _header.text = current == null ? "VLS · 대기" : $"VLS  {_index + 1}/{_launchers.Count}";
            if (_selected != null) _selected.text = current == null ? "설치된 발사기 없음" :
                $"{current.Ammo.Current}/{current.Ammo.Capacity}셀  ·  [{GameSettings.BindingLabel(NavalControl.VlsSelector)}] 발사기 전환";
            if (_status != null) _status.text = current == null ? "VLS를 설치하면 선택할 수 있습니다." :
                current.SwitchRemaining > 0f ? $"탄종 전환 {current.SwitchRemaining:0.0}초" :
                current.Mode == VlsModule.PayloadMode.AntiSubmarine ? "소나 접촉 후 발사 · 능동 핑 자동" :
                current.Mode == VlsModule.PayloadMode.Air ? "레이더 탐지 후 자동 요격" : "수상 표적 자동 공격";
            if (_modeImages != null)
                for (int i = 0; i < _modeImages.Length; i++)
                    _modeImages[i].color = current != null && (int)current.Mode == i ? Active : Idle;
        }
    }
}

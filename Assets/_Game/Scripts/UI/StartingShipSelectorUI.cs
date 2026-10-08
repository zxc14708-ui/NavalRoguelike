using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Data;
using Game.Ship;

namespace Game.UI
{
    /// <summary>Pre-launch native model preview. Only the selected basic loadout is installed on confirmation.</summary>
    public sealed class StartingShipSelectorUI : MonoBehaviour
    {
        private readonly List<StartingShipConcept> _ships = new();
        private readonly List<Image> _choices = new();
        private TMP_FontAsset _font;
        private TMP_Text _title, _summary, _message;
        private CodexPreview _preview;
        private ShipInitializer _initializer;
        private Action _confirmed, _cancelled;
        private int _selected;

        public static bool Show(Transform parent, TMP_FontAsset font, ShipInitializer initializer, Action confirmed, Action cancelled)
        {
            if (initializer == null || StartingShipCatalog.All.Count == 0) return false;
            var go = new GameObject("Starting ship selection", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(.008f,.025f,.035f,.99f);
            var ui = go.AddComponent<StartingShipSelectorUI>();
            ui._font = font; ui._initializer = initializer;
            ui._confirmed = confirmed; ui._cancelled = cancelled;
            foreach (var ship in StartingShipCatalog.All)
                if (ship != null && ship.StartLoadout != null) ui._ships.Add(ship);
            if (ui._ships.Count == 0) { Destroy(go); return false; }
            ui.Build();
            return true;
        }

        private void Build()
        {
            var content = Panel(transform,"Hull selection console",new Vector2(1120,660),Vector2.zero,new Color(.025f,.065f,.08f));
            content.anchorMin = content.anchorMax = new Vector2(.5f,.5f);
            content.pivot = new Vector2(.5f,.5f);
            Label(content,"FLEET COMMAND  /  출항 준비",new Vector2(22,610),new Vector2(1070,27),17,new Color(.5f,.83f,.8f));
            Label(content,"시작 함선 선택",new Vector2(22,555),new Vector2(1070,49),33,Color.white);
            Label(content,"특화 블록이 설치된 기본형으로 출항합니다. 추가 장비와 편대는 정비 카드로 성장합니다.",
                new Vector2(22,516),new Vector2(1070,30),17,new Color(.63f,.78f,.8f));

            var previewRect = Panel(content,"Native hull preview",new Vector2(620,328),new Vector2(22,170),new Color(.02f,.075f,.06f));
            var raw = new GameObject("3D preview",typeof(RectTransform),typeof(RawImage));
            raw.transform.SetParent(previewRect,false);
            var rawRect = (RectTransform)raw.transform;
            rawRect.anchorMin = rawRect.anchorMax = Vector2.zero; rawRect.pivot = Vector2.zero;
            rawRect.sizeDelta = previewRect.sizeDelta;
            _preview = CodexPreview.Create(raw.GetComponent<RawImage>(),null);
            _preview.FramingScale = .7f;
            _preview.EnableStudioLighting();
            _title = Label(content,"",new Vector2(24,132),new Vector2(617,34),23,Color.white);
            _summary = Label(content,"",new Vector2(24,51),new Vector2(617,76),17,new Color(.73f,.85f,.86f));
            _summary.textWrappingMode = TextWrappingModes.Normal;

            for (int i=0;i<_ships.Count;i++)
            {
                int index = i;
                var ship = _ships[i];
                var button = Button(content,ship.Title,new Vector2(432,87),new Vector2(666,405-i*98),new Color(.06f,.12f,.15f),22);
                _choices.Add(button.GetComponent<Image>());
                Label(button.transform,$"{ship.StartLoadout.Entries.Count}개 기본 모듈"+(ship.InitialEscortCount>0?$"  ·  고속정 {ship.InitialEscortCount}척":""),
                    new Vector2(14,9),new Vector2(405,25),15,new Color(.65f,.8f,.84f));
                var text = button.GetComponentInChildren<TMP_Text>();
                text.rectTransform.anchoredPosition = new Vector2(14,39); text.rectTransform.sizeDelta = new Vector2(402,35);
                text.alignment = TextAlignmentOptions.MidlineLeft;
                button.onClick.AddListener(()=>Select(index));
            }
            _message = Label(content,"함급별 고정 능력치·무기 제한은 아직 적용하지 않습니다.",
                new Vector2(22,11),new Vector2(620,28),14,new Color(.56f,.7f,.75f));
            Button(content,"돌아가기",new Vector2(145,48),new Vector2(666,24),new Color(.12f,.2f,.24f),19)
                .onClick.AddListener(()=>{ var cb=_cancelled; Destroy(gameObject); cb?.Invoke(); });
            Button(content,"선택 확정 · 출항",new Vector2(270,48),new Vector2(828,24),new Color(.08f,.39f,.31f),21)
                .onClick.AddListener(Confirm);
            int initial = _ships.IndexOf(_initializer.SelectedConcept);
            Select(Mathf.Max(0,initial));
        }

        private void Select(int index)
        {
            _selected = index;
            var ship = _ships[index];
            for (int i=0;i<_choices.Count;i++)
                _choices[i].color = i==index ? Color.Lerp(new Color(.06f,.12f,.15f),ship.Color,.28f) : new Color(.06f,.12f,.15f);
            _title.text = ship.Title;
            _summary.text = ship.StartSummary;
            _preview.Show(ship.StartPreviewPrefab,null);
        }

        private void Confirm()
        {
            if (!_initializer.TrySelect(_ships[_selected],out string reason))
            { _message.text = reason; _message.color = new Color(1f,.66f,.38f); return; }
            _initializer.LockForLaunch();
            var cb = _confirmed;
            Destroy(gameObject);
            cb?.Invoke();
        }

        private RectTransform Panel(Transform parent,string name,Vector2 size,Vector2 position,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform; rect.anchorMin=rect.anchorMax=Vector2.zero; rect.pivot=Vector2.zero;
            rect.sizeDelta=size; rect.anchoredPosition=position;
            var img=go.GetComponent<Image>(); img.color=color; img.raycastTarget=false;
            return rect;
        }

        private TMP_Text Label(Transform parent,string value,Vector2 position,Vector2 size,float fontSize,Color color)
        {
            var go=new GameObject("Text",typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(parent,false);
            var text=go.GetComponent<TextMeshProUGUI>(); text.font=_font; text.text=value; text.fontSize=fontSize;
            text.color=color; text.raycastTarget=false;
            var rect=text.rectTransform; rect.anchorMin=rect.anchorMax=Vector2.zero; rect.pivot=Vector2.zero;
            rect.anchoredPosition=position; rect.sizeDelta=size;
            return text;
        }

        private Button Button(Transform parent,string title,Vector2 size,Vector2 position,Color color,float fontSize)
        {
            var rect=Panel(parent,title,size,position,color); var image=rect.GetComponent<Image>(); image.raycastTarget=true;
            Label(rect,title,Vector2.zero,size,fontSize,Color.white).alignment=TextAlignmentOptions.Center;
            var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=image;
            return button;
        }
    }
}

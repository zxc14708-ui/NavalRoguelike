using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
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
        private NavalBaseMenu _harbor;
        private bool _confirmedOnce;
        public int SelectedIndex => _selected;

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
            _harbor=NavalBaseMenu.Active;
            if(_harbor!=null){BuildDock();return;}
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
            if(_harbor!=null)_harbor.FocusShip(index);
            else _preview.Show(ship.StartPreviewPrefab,null);
        }

        private void BuildDock()
        {
            GetComponent<Image>().color=Color.clear;
            var content=Panel(transform,"Dock selection console",new Vector2(1080,204),new Vector2(0,26),new Color(.012f,.04f,.055f,.92f));
            content.anchorMin=content.anchorMax=new Vector2(.5f,0);content.pivot=new Vector2(.5f,0);
            _title=Label(content,"",new Vector2(24,152),new Vector2(740,40),29,Color.white);
            _summary=Label(content,"",new Vector2(24,63),new Vector2(690,80),18,new Color(.73f,.85f,.86f));
            _summary.textWrappingMode=TextWrappingModes.Normal;
            _message=Label(content,"← / →  함선 이동     Enter  출항     Esc  돌아가기",new Vector2(24,16),new Vector2(730,32),17,new Color(.56f,.8f,.8f));
            Button(content,"돌아가기",new Vector2(140,46),new Vector2(754,24),new Color(.12f,.2f,.24f),19).onClick.AddListener(Cancel);
            Button(content,"선택 확정 · 출항",new Vector2(292,58),new Vector2(754,84),new Color(.08f,.39f,.31f),23).onClick.AddListener(Confirm);
            var tabs=Panel(transform,"Berth tabs",new Vector2(1080,58),new Vector2(0,-30),Color.clear);
            tabs.anchorMin=tabs.anchorMax=new Vector2(.5f,1);tabs.pivot=new Vector2(.5f,1);
            for(int i=0;i<_ships.Count;i++)
            {
                int index=i;
                var b=Button(tabs,_ships[i].Title,new Vector2(258,52),new Vector2(i*274,0),new Color(.06f,.12f,.15f,.88f),21);
                _choices.Add(b.GetComponent<Image>()); b.onClick.AddListener(()=>Select(index));
            }
            foreach(int direction in new[]{-1,1})
            {
                var b=Button(transform,direction<0?"〈":"〉",new Vector2(64,96),Vector2.zero,new Color(.02f,.08f,.1f,.8f),40);
                var r=(RectTransform)b.transform;r.anchorMin=r.anchorMax=new Vector2(direction<0?.04f:.96f,.53f);r.pivot=new Vector2(.5f,.5f);
                int step=direction;b.onClick.AddListener(()=>Navigate(step));
            }
            Select(Mathf.Max(0,_ships.IndexOf(_initializer.SelectedConcept)));
        }

        public void Navigate(int direction)
        {
            if(_confirmedOnce || _ships.Count==0)return;
            Select((_selected+direction+_ships.Count)%_ships.Count);
        }
        private void Cancel()
        {
            if(_confirmedOnce)return;
            _harbor?.ShowOverview();var cb=_cancelled;Destroy(gameObject);cb?.Invoke();
        }
        private void Update()
        {
            if(_harbor==null || Keyboard.current==null)return;
            var k=Keyboard.current;
            if(k.leftArrowKey.wasPressedThisFrame)Navigate(-1);
            else if(k.rightArrowKey.wasPressedThisFrame)Navigate(1);
            else if(k.escapeKey.wasPressedThisFrame)Cancel();
            else if(k.enterKey.wasPressedThisFrame)Confirm();
        }

        private void Confirm()
        {
            if(_confirmedOnce)return;
            if (!_initializer.TrySelect(_ships[_selected],out string reason))
            { _message.text = reason; _message.color = new Color(1f,.66f,.38f); return; }
            _initializer.LockForLaunch();
            _confirmedOnce=true;
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
            button.navigation=new Navigation{mode=Navigation.Mode.None};
            return button;
        }
    }
}

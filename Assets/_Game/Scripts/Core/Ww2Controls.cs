using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Core
{
    /// <summary>WWII 전용 조작(패치 B). 값은 저장 키 순번이므로 순서를 바꾸지 말고 끝에만 덧붙인다.</summary>
    public enum Ww2Control
    {
        TorpedoPort,       // 좌현 어뢰: 누르고 있으면 예상 경로, 떼면 발사
        TorpedoStarboard,  // 우현 어뢰
    }

    /// <summary>
    /// WWII 조작키(패치 A 4장 "행동 ID로 저장하며 WWII 전용 설정 이름공간"). PlayerPrefs "Naval.WW2.Control.N"에 따로 저장해
    /// 현대 키 설정(Naval.Control.N)을 바꾸지 않는다.
    /// 조타·전령기·응급 수리·연막·전속·함 현황·고유 스킬은 현대 키를 그대로 같이 쓴다(<see cref="SharedControls"/>).
    /// 기만체·재밍·VLS·헬기·편대 키는 WWII에서 쓰지 않으므로 기본값으로 그 자리(Q/E)를 어뢰에 준다.
    /// 같이 쓰는 현대 키와 겹치면 WWII 쪽 키 변경을 거부하고, 현대 쪽에서 겹치게 바꾸면 <see cref="Conflicts"/>가 알려 준다.
    /// </summary>
    public static class Ww2Controls
    {
        private const string Prefix = "Naval.WW2.Control.";
        private static readonly Key[] Defaults = { Key.Q, Key.E };
        private static readonly Key[] Bindings = new Key[Defaults.Length];
        private static bool _loaded;

        /// <summary>WWII에서도 쓰는 현대 조작.</summary>
        public static readonly NavalControl[] SharedControls =
        {
            NavalControl.ThrottleUp, NavalControl.ThrottleDown, NavalControl.RudderLeft, NavalControl.RudderRight,
            NavalControl.CenterRudder, NavalControl.Repair, NavalControl.Smoke, NavalControl.Flank,
            NavalControl.ShipStatus, NavalControl.Signature,
        };

        public static string Title(Ww2Control c) => c switch
        {
            Ww2Control.TorpedoPort => "좌현 어뢰(누르고 조준 · 떼면 발사)",
            _ => "우현 어뢰(누르고 조준 · 떼면 발사)",
        };

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            for (int i = 0; i < Bindings.Length; i++)
            {
                var saved = (Key)PlayerPrefs.GetInt(Prefix + i, (int)Defaults[i]);
                Bindings[i] = Enum.IsDefined(typeof(Key), saved) && saved != Key.None ? saved : Defaults[i];
            }
        }

        public static Key Binding(Ww2Control c) { Load(); return Bindings[(int)c]; }
        public static string BindingLabel(Ww2Control c) => GameSettings.KeyLabel(Binding(c));

        public static bool Pressed(Ww2Control c) { var k = Keyboard.current; return k != null && k[Binding(c)].wasPressedThisFrame; }
        public static bool Held(Ww2Control c) { var k = Keyboard.current; return k != null && k[Binding(c)].isPressed; }
        public static bool Released(Ww2Control c) { var k = Keyboard.current; return k != null && k[Binding(c)].wasReleasedThisFrame; }

        public static bool TryBind(Ww2Control control, Key key, out string message)
        {
            Load();
            if (key == Key.None || key == Key.Escape || key == Key.Enter || key == Key.NumpadEnter ||
                key == Key.Digit1 || key == Key.Digit2 || key == Key.Digit3)
            { message = "ESC·ENTER·1~3은 메뉴/배치 전용 키입니다."; return false; }
            if (GameSettings.IsShiftKey(key)) { message = "SHIFT는 항로 경유지 추가(SHIFT+우클릭) 전용 키입니다."; return false; }
            foreach (var shared in SharedControls)
                if (GameSettings.Binding(shared) == key)
                { message = $"{GameSettings.KeyLabel(key)}는 함께 쓰는 조작({shared})에 이미 쓰입니다."; return false; }
            for (int i = 0; i < Bindings.Length; i++)
                if (i != (int)control && Bindings[i] == key)
                {
                    Bindings[i] = Bindings[(int)control];   // WWII 조작끼리는 서로 바꾼다
                    PlayerPrefs.SetInt(Prefix + i, (int)Bindings[i]);
                }
            Bindings[(int)control] = key;
            PlayerPrefs.SetInt(Prefix + (int)control, (int)key);
            PlayerPrefs.Save();
            message = "WWII 조작키를 저장했습니다(현대 키 설정과 따로 저장).";
            return true;
        }

        public static void ResetBindings()
        {
            Load();
            for (int i = 0; i < Bindings.Length; i++)
            {
                Bindings[i] = Defaults[i];
                PlayerPrefs.SetInt(Prefix + i, (int)Defaults[i]);
            }
            PlayerPrefs.Save();
        }

        /// <summary>같이 쓰는 현대 키와 겹친 WWII 키(설정 화면·시험 전투 시작 알림).</summary>
        public static List<string> Conflicts()
        {
            Load();
            var list = new List<string>();
            foreach (Ww2Control c in Enum.GetValues(typeof(Ww2Control)))
                foreach (var shared in SharedControls)
                    if (GameSettings.Binding(shared) == Bindings[(int)c])
                        list.Add($"{Title(c)} ↔ {shared}: {GameSettings.KeyLabel(Bindings[(int)c])}");
            return list;
        }
    }
}

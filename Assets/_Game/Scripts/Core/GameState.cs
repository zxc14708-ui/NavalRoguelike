using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Core
{
    /// <summary>게임 전체의 배타적 상태. 시간 정지의 기준이 된다.</summary>
    public enum GameState
    {
        Boot,
        Playing,     // 스테이지 전투 중
        Refit,       // 레벨업 — 카드 선택과 모듈 배치 (timeScale = 0)
        Paused,
        Victory,
        GameOver,
    }

    public enum NavalDoctrine { None, AirDefense, AntiSub, Gunnery }
    public enum HelicopterPolicy { Automatic, AntiSubPatrol, SurfaceStrike }

    /// <summary>이번 회차에 선택한 교리. 헬기 출격 정책은 각 데크가 개별 관리한다.</summary>
    public static class CombatPolicies
    {
        public static NavalDoctrine Doctrine { get; private set; } = NavalDoctrine.None;
        public static event Action PolicyChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Doctrine = NavalDoctrine.None;
            PolicyChanged = null;
        }

        public static void ChooseDoctrine(NavalDoctrine doctrine)
        {
            if (Doctrine != NavalDoctrine.None || doctrine == NavalDoctrine.None) return;
            Doctrine = doctrine;
            PolicyChanged?.Invoke();
        }

        public static string DoctrineLabel => Doctrine switch
        {
            NavalDoctrine.AirDefense => "방공 교리",
            NavalDoctrine.AntiSub => "대잠 교리",
            NavalDoctrine.Gunnery => "포격 교리",
            _ => "교리 미선택",
        };

    }

    public enum NavalControl
    {
        ThrottleUp, ThrottleDown, RudderLeft, RudderRight, CenterRudder,
        Decoy, Jam, Repair, Smoke, Flank, VlsSelector, HelicopterSelector, ShipStatus,
        Formation   // 편대 진형 전환(2026-10-03, 뒤에만 덧붙임)
    }

    public enum SoundChannel { Master, Effects, Engine, Warning }

    /// <summary>플레이어 로컬 설정. 전투 조작과 음량을 PlayerPrefs에 저장한다.</summary>
    public static class GameSettings
    {
        private const string KeyPrefix = "Naval.Control.";
        private const string VolumePrefix = "Naval.Volume.";
        private static readonly Key[] Defaults =
        {
            Key.W, Key.S, Key.A, Key.D, Key.X, Key.Q, Key.E, Key.R, Key.F,
            Key.C, Key.V, Key.H, Key.Tab, Key.G   // 전속: Shift → C(2026-10-07, Shift+우클릭 = 항로 경유지 추가)
        };
        private const string FlankKeyMigration = "Naval.Control.Migrated.FlankC";
        private static readonly Key[] Bindings = new Key[Defaults.Length];
        private static readonly float[] Volumes = new float[4];
        private static bool _loaded;

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            for (int i = 0; i < Bindings.Length; i++)
            {
                var saved = (Key)PlayerPrefs.GetInt(KeyPrefix + i, (int)Defaults[i]);
                Bindings[i] = Enum.IsDefined(typeof(Key), saved) && saved != Key.None ? saved : Defaults[i];
            }
            MigrateFlankKey();
            for (int i = 0; i < Volumes.Length; i++)
                Volumes[i] = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePrefix + i, 1f));
            AudioListener.volume = Volumes[(int)SoundChannel.Master];
        }

        /// <summary>
        /// 한 번만: 예전 기본값(Shift)으로 저장된 전속 키를 C로 옮긴다. Shift는 항로 경유지 추가(Shift+우클릭)에 쓴다.
        /// C를 이미 다른 조작에 쓰고 있으면 건드리지 않는다(설정 화면에서 직접 바꿀 수 있다).
        /// </summary>
        private static void MigrateFlankKey()
        {
            if (PlayerPrefs.GetInt(FlankKeyMigration, 0) == 1) return;
            PlayerPrefs.SetInt(FlankKeyMigration, 1);
            int flank = (int)NavalControl.Flank;
            if (Bindings[flank] != Key.LeftShift || Array.IndexOf(Bindings, Key.C) >= 0) { PlayerPrefs.Save(); return; }
            Bindings[flank] = Key.C;
            PlayerPrefs.SetInt(KeyPrefix + flank, (int)Key.C);
            PlayerPrefs.Save();
        }

        /// <summary>Shift는 항로 경유지 추가(Shift+우클릭) 수식키라 조작 키로 쓰지 않는다.</summary>
        public static bool IsShiftKey(Key key) => key == Key.LeftShift || key == Key.RightShift;

        public static Key Binding(NavalControl control) { Load(); return Bindings[(int)control]; }
        public static string BindingLabel(NavalControl control) => Binding(control) switch
        {
            Key.LeftShift => "SHIFT",
            Key.RightShift => "R-SHIFT",
            Key.LeftArrow => "LEFT",
            Key.RightArrow => "RIGHT",
            Key.UpArrow => "UP",
            Key.DownArrow => "DOWN",
            Key.Tab => "TAB",
            Key.Space => "SPACE",
            _ => Binding(control).ToString().ToUpperInvariant()
        };
        public static bool Pressed(NavalControl control)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            var key = Binding(control);
            return keyboard[key].wasPressedThisFrame ||
                   (key == Key.LeftShift && Array.IndexOf(Bindings, Key.RightShift) < 0 &&
                    keyboard.rightShiftKey.wasPressedThisFrame);
        }
        public static bool Held(NavalControl control)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[Binding(control)].isPressed;
        }

        public static bool TryBind(NavalControl control, Key key, out string message)
        {
            Load();
            if (key == Key.None || key == Key.Escape || key == Key.Enter || key == Key.NumpadEnter ||
                key == Key.Digit1 || key == Key.Digit2 || key == Key.Digit3)
            {
                message = "ESC·ENTER·1~3은 메뉴/배치 전용 키입니다.";
                return false;
            }
            if (IsShiftKey(key))
            {
                message = "SHIFT는 항로 경유지 추가(SHIFT+우클릭) 전용 키입니다.";
                return false;
            }
            for (int i = 0; i < Bindings.Length; i++)
                if (i != (int)control && Bindings[i] == key)
                {
                    Bindings[i] = Bindings[(int)control];
                    PlayerPrefs.SetInt(KeyPrefix + i, (int)Bindings[i]);
                    break;
                }
            Bindings[(int)control] = key;
            PlayerPrefs.SetInt(KeyPrefix + (int)control, (int)key);
            PlayerPrefs.Save();
            message = "키 설정을 저장했습니다.";
            return true;
        }

        public static void ResetBindings()
        {
            Load();
            for (int i = 0; i < Bindings.Length; i++)
            {
                Bindings[i] = Defaults[i];
                PlayerPrefs.SetInt(KeyPrefix + i, (int)Defaults[i]);
            }
            PlayerPrefs.Save();
        }

        public static float Volume(SoundChannel channel) { Load(); return Volumes[(int)channel]; }
        public static void SetVolume(SoundChannel channel, float value)
        {
            Load();
            Volumes[(int)channel] = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumePrefix + (int)channel, Volumes[(int)channel]);
            if (channel == SoundChannel.Master) AudioListener.volume = Volumes[(int)channel];
        }
    }
}

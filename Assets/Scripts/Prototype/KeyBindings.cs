using System;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SteamGame.Prototype
{
    /// <summary>키 설정에서 바꿀 수 있는 동작.</summary>
    public enum GameAction
    {
        MoveLeft,
        MoveRight,
        Jump,
        Down,
        Attack,
        Dash,
        Swap,
        Anchor,
        Interact,
        Restart,
        Mute,
    }

    /// <summary>
    /// 키보드 키 설정. 동작마다 키를 세 개까지 둘 수 있고, Esc 설정 화면의 키 설정에서 바꾼다.
    /// 바꾼 키는 PlayerPrefs에 저장되어 게임을 다시 켜도 남는다.
    /// 마우스(왼쪽 공격, 오른쪽 자리 바꾸기)와 게임패드는 바꾸지 않는다. Esc는 설정 화면을 여는 키라서 쓸 수 없다.
    /// </summary>
    public static class KeyBindings
    {
        public const int Slots = 3;
        public static readonly int ActionCount = Enum.GetValues(typeof(GameAction)).Length;

        private const string PrefsKey = "settings.keys.v1";

        private static readonly Key[][] Defaults =
        {
            new[] { Key.A, Key.LeftArrow },
            new[] { Key.D, Key.RightArrow },
            new[] { Key.Space, Key.W, Key.UpArrow },
            new[] { Key.S, Key.DownArrow },
            new[] { Key.J, Key.Z },
            new[] { Key.LeftShift, Key.L, Key.C },
            new[] { Key.K, Key.X },
            new[] { Key.Q, Key.V },
            new[] { Key.E, Key.F },
            new[] { Key.R },
            new[] { Key.M },
        };

        private static readonly string[] Names =
        {
            "왼쪽으로 이동",
            "오른쪽으로 이동",
            "점프",
            "내려가기",
            "공격",
            "대시",
            "자리 바꾸기",
            "분신 고정",
            "상호작용",
            "다시 시작",
            "소리 끄기/켜기",
        };

        private static readonly string[] Notes =
        {
            null,
            null,
            null,
            "누른 채 점프",
            "마우스 왼쪽도",
            null,
            "마우스 오른쪽도",
            null,
            "문, 상점, 제단",
            "이번 판 포기",
            null,
        };

        private static Key[,] _keys;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _keys = null;
        }

        private static Key[,] Keys
        {
            get
            {
                if (_keys == null) Load();
                return _keys;
            }
        }

        /// <summary>설정 화면에 쓰는 동작 이름.</summary>
        public static string Label(GameAction action) => Names[(int)action];

        /// <summary>동작 이름 옆에 작게 붙이는 설명. 없으면 null.</summary>
        public static string Note(GameAction action) => Notes[(int)action];

        /// <summary>다시 시작과 소리는 일시 정지 메뉴에도 있으므로 키를 모두 비워도 된다.</summary>
        public static bool CanBeEmpty(GameAction action) => action == GameAction.Restart || action == GameAction.Mute;

        public static Key Get(GameAction action, int slot) => Keys[(int)action, slot];

        /// <summary>그 동작의 키 중 앞에 있는 것. 화면의 키 안내에 쓴다. 키가 없으면 Key.None.</summary>
        public static Key Primary(GameAction action)
        {
            Key[,] keys = Keys;
            for (int slot = 0; slot < Slots; slot++)
            {
                if (keys[(int)action, slot] != Key.None) return keys[(int)action, slot];
            }
            return Key.None;
        }

        /// <summary>그 동작의 첫 번째 키 이름. 키가 없으면 "-".</summary>
        public static string PrimaryName(GameAction action)
        {
            Key key = Primary(action);
            return key == Key.None ? "-" : KeyName(key);
        }

        /// <summary>그 키가 어느 동작에든 쓰이고 있는지. 테스트용 키가 게임 키와 겹치지 않게 할 때 쓴다.</summary>
        public static bool IsBound(Key key)
        {
            if (key == Key.None) return false;
            Key[,] keys = Keys;
            for (int action = 0; action < ActionCount; action++)
            {
                for (int slot = 0; slot < Slots; slot++)
                {
                    if (keys[action, slot] == key) return true;
                }
            }
            return false;
        }

        /// <summary>그 키를 쓰는 칸을 찾는다. 없으면 false.</summary>
        public static bool Find(Key key, out GameAction action, out int slot)
        {
            action = GameAction.MoveLeft;
            slot = -1;
            if (key == Key.None) return false;
            Key[,] keys = Keys;
            for (int a = 0; a < ActionCount; a++)
            {
                for (int s = 0; s < Slots; s++)
                {
                    if (keys[a, s] != key) continue;
                    action = (GameAction)a;
                    slot = s;
                    return true;
                }
            }
            return false;
        }

        /// <summary>그 동작에 넣어 둔 키의 수.</summary>
        public static int Count(GameAction action)
        {
            int count = 0;
            Key[,] keys = Keys;
            for (int slot = 0; slot < Slots; slot++)
            {
                if (keys[(int)action, slot] != Key.None) count++;
            }
            return count;
        }

        /// <summary>그 동작의 키 중 하나라도 누르고 있는지.</summary>
        public static bool Held(GameAction action)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;
            Key[,] keys = Keys;
            for (int slot = 0; slot < Slots; slot++)
            {
                KeyControl control = Control(keyboard, keys[(int)action, slot]);
                if (control != null && control.isPressed) return true;
            }
            return false;
        }

        /// <summary>그 동작의 키 중 하나라도 이번 프레임에 눌렀는지.</summary>
        public static bool Pressed(GameAction action)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;
            Key[,] keys = Keys;
            for (int slot = 0; slot < Slots; slot++)
            {
                KeyControl control = Control(keyboard, keys[(int)action, slot]);
                if (control != null && control.wasPressedThisFrame) return true;
            }
            return false;
        }

        private static KeyControl Control(Keyboard keyboard, Key key)
        {
            // 저장된 값이 이 버전의 키보드에 없는 키라도 오류가 나지 않게 범위를 먼저 본다.
            int index = (int)key;
            if (index <= 0 || index > keyboard.allKeys.Count) return null;
            return keyboard[key];
        }

        /// <summary>
        /// action의 slot번째 칸에 key를 넣는다. 다른 칸이 이미 그 키를 쓰고 있었다면 그 칸에는 이 칸의 원래 키를 넣어
        /// 서로 바꾼다. 바꾼 칸이 있으면 true이고 other, otherSlot에 그 칸을 돌려준다.
        /// </summary>
        public static bool Assign(GameAction action, int slot, Key key, out GameAction other, out int otherSlot)
        {
            Key[,] keys = Keys;
            Key old = keys[(int)action, slot];
            other = action;
            otherSlot = -1;
            if (key == old) return false;

            for (int a = 0; a < ActionCount && otherSlot < 0; a++)
            {
                for (int s = 0; s < Slots; s++)
                {
                    if (keys[a, s] != key || (a == (int)action && s == slot)) continue;
                    keys[a, s] = old;
                    other = (GameAction)a;
                    otherSlot = s;
                    break;
                }
            }

            keys[(int)action, slot] = key;
            Save();
            return otherSlot >= 0;
        }

        /// <summary>
        /// 그 칸의 키를 지운다. 동작에 남는 키가 하나도 없게 되면 지우지 않고 false를 돌려준다
        /// (다시 시작과 소리는 메뉴에도 있으므로 예외).
        /// </summary>
        public static bool Clear(GameAction action, int slot)
        {
            Key[,] keys = Keys;
            if (keys[(int)action, slot] == Key.None) return true;
            if (!CanBeEmpty(action))
            {
                int remaining = 0;
                for (int s = 0; s < Slots; s++)
                {
                    if (s != slot && keys[(int)action, s] != Key.None) remaining++;
                }
                if (remaining == 0) return false;
            }
            keys[(int)action, slot] = Key.None;
            Save();
            return true;
        }

        /// <summary>모든 키를 처음 설정으로 되돌린다.</summary>
        public static void ResetDefaults()
        {
            _keys = DefaultKeys();
            Save();
        }

        /// <summary>바꿀 수 있는 키인지. Esc는 설정 화면을 여는 키라서 안 된다.</summary>
        public static bool CanBind(Key key)
        {
            return key != Key.None && key != Key.Escape && key.ToString() != "IMESelected";
        }

        private static Key[,] DefaultKeys()
        {
            var keys = new Key[ActionCount, Slots];
            for (int action = 0; action < ActionCount && action < Defaults.Length; action++)
            {
                for (int slot = 0; slot < Slots && slot < Defaults[action].Length; slot++)
                {
                    keys[action, slot] = Defaults[action][slot];
                }
            }
            return keys;
        }

        private static void Load()
        {
            _keys = DefaultKeys();
            string saved = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrEmpty(saved)) return;

            string[] parts = saved.Split(',');
            if (parts.Length != ActionCount * Slots) return;

            var loaded = new Key[ActionCount, Slots];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i], out int value)) return;
                var key = (Key)value;
                if (value != 0 && (!Enum.IsDefined(typeof(Key), key) || !CanBind(key))) return;
                loaded[i / Slots, i % Slots] = key;
            }

            // 저장이 망가져서 꼭 필요한 동작의 키가 모두 비었다면 처음 설정을 쓴다.
            for (int action = 0; action < ActionCount; action++)
            {
                if (CanBeEmpty((GameAction)action)) continue;
                bool any = false;
                for (int slot = 0; slot < Slots; slot++) any |= loaded[action, slot] != Key.None;
                if (!any) return;
            }
            _keys = loaded;
        }

        private static void Save()
        {
            var text = new StringBuilder();
            Key[,] keys = Keys;
            for (int action = 0; action < ActionCount; action++)
            {
                for (int slot = 0; slot < Slots; slot++)
                {
                    if (text.Length > 0) text.Append(',');
                    text.Append((int)keys[action, slot]);
                }
            }
            PlayerPrefs.SetString(PrefsKey, text.ToString());
            PlayerPrefs.Save();
        }

        private static bool IsMac => Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor;

        /// <summary>
        /// 키 이름. 키보드 배열(한글 입력 등)에 따라 바뀌지 않도록 키 자리로 정한 이름을 쓴다.
        /// LeftMeta와 LeftCommand처럼 같은 값을 가진 이름이 있어서 한쪽만 적는다.
        /// </summary>
        public static string KeyName(Key key)
        {
            switch (key)
            {
                case Key.None: return "-";
                case Key.Space: return "Space";
                case Key.Enter: return "Enter";
                case Key.Tab: return "Tab";
                case Key.Backquote: return "`";
                case Key.Quote: return "'";
                case Key.Semicolon: return ";";
                case Key.Comma: return ",";
                case Key.Period: return ".";
                case Key.Slash: return "/";
                case Key.Backslash: return "\\";
                case Key.LeftBracket: return "[";
                case Key.RightBracket: return "]";
                case Key.Minus: return "-";
                case Key.Equals: return "=";
                case Key.LeftShift: return "Shift";
                case Key.RightShift: return "R Shift";
                case Key.LeftCtrl: return "Ctrl";
                case Key.RightCtrl: return "R Ctrl";
                case Key.LeftAlt: return IsMac ? "Option" : "Alt";
                case Key.RightAlt: return IsMac ? "R Option" : "R Alt";
                case Key.LeftMeta: return IsMac ? "Cmd" : "Win";
                case Key.RightMeta: return IsMac ? "R Cmd" : "R Win";
                case Key.ContextMenu: return "Menu";
                case Key.Escape: return "Esc";
                case Key.LeftArrow: return "←";
                case Key.RightArrow: return "→";
                case Key.UpArrow: return "↑";
                case Key.DownArrow: return "↓";
                case Key.Backspace: return IsMac ? "Delete" : "Backspace";
                case Key.Delete: return "Del";
                case Key.PageDown: return "PgDn";
                case Key.PageUp: return "PgUp";
                case Key.Insert: return "Ins";
                case Key.CapsLock: return "Caps";
                case Key.NumLock: return "NumLk";
                case Key.PrintScreen: return "PrtSc";
                case Key.ScrollLock: return "ScrLk";
                case Key.NumpadEnter: return "Num Enter";
                case Key.NumpadDivide: return "Num /";
                case Key.NumpadMultiply: return "Num *";
                case Key.NumpadPlus: return "Num +";
                case Key.NumpadMinus: return "Num -";
                case Key.NumpadPeriod: return "Num .";
                case Key.NumpadEquals: return "Num =";
            }

            string name = key.ToString();
            if (name.StartsWith("Digit", StringComparison.Ordinal)) return name.Substring(5);
            if (name.StartsWith("Numpad", StringComparison.Ordinal)) return "Num " + name.Substring(6);
            return name;
        }
    }
}

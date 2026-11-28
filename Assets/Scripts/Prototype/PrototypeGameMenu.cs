using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SteamGame.Prototype
{
    /// <summary>
    /// Esc로 여는 설정 화면. 일시 정지 메뉴(계속하기, 키 설정, 소리, 메인 화면으로, 다시 시작)와
    /// 키를 보고 바꾸는 키 설정 화면이 있다. 메인 화면(PrototypeGameTitle.cs)도 같은 틀을 쓴다. 열려 있는 동안 게임은 멈춘다.
    /// </summary>
    public sealed partial class PrototypeGame
    {
        private enum MenuScreen
        {
            None,
            Pause,
            Keys,
            /// <summary>메인 화면. 게임을 켜면 처음 뜬다.</summary>
            Title,
        }

        private const int PauseResume = 0;
        private const int PauseKeys = 1;
        private const int PauseSound = 2;
        private const int PauseTitle = 3;
        private const int PauseRestart = 4;
        private const int PauseItemCount = 5;
        private const float MenuNoteTime = 2.8f;
        private const float MenuConfirmWindow = 3f;

        private MenuScreen _menu = MenuScreen.None;
        /// <summary>일시 정지 메뉴에서는 고른 항목, 키 설정에서는 고른 줄(동작, 기본값 버튼, 뒤로 버튼 순서).</summary>
        private int _menuIndex;
        /// <summary>키 설정에서 고른 칸(키 1, 2, 3).</summary>
        private int _menuColumn;
        private bool _listening;
        private int _listenFrame = -1;
        /// <summary>메뉴를 닫은 프레임. 메뉴를 닫은 키가 점프나 공격으로 이어지지 않게 이 프레임까지 멈춘 것으로 친다.</summary>
        private int _menuClosedFrame = -1;
        private float _menuOpenedAt = -10f;
        private float _menuScreenAt = -10f;
        /// <summary>다시 시작이나 기본값 되돌리기를 한 번 누른 뒤, 한 번 더 누르면 실행되는 시각까지.</summary>
        private float _menuArmedUntil;
        private string _menuNote;
        private Color _menuNoteColor;
        private float _menuNoteAt = -10f;
        private Vector2 _menuMouse;
        private bool _menuMouseKnown;
        private bool _menuMouseMoved;

        private bool MenuOpen => _menu != MenuScreen.None;
        private static int KeyRowReset => KeyBindings.ActionCount;
        private static int KeyRowBack => KeyBindings.ActionCount + 1;
        private static int KeyRowCount => KeyBindings.ActionCount + 2;

        // ---- 입력 ----

        /// <summary>메뉴가 이번 프레임의 입력을 가져갔으면 true. 그러면 게임 쪽 Update는 건너뛴다.</summary>
        private bool UpdateMenu()
        {
            if (_menu == MenuScreen.None)
            {
                if (MenuTogglePressed() && CanOpenMenu()) OpenMenu();
                return _menu != MenuScreen.None;
            }

            if (_menu == MenuScreen.Pause) UpdatePauseMenu();
            else if (_menu == MenuScreen.Title) UpdateTitleMenu();
            else UpdateKeyMenu();
            return true;
        }

        /// <summary>강화 고르기, 보스 등장 연출, 클리어 화면에서는 Esc가 원래 하던 일을 한다.</summary>
        private bool CanOpenMenu()
        {
            return _choices == null && !_introActive && !_runCleared && Time.frameCount != _introEndFrame && !_restartRequested;
        }

        private static bool MenuTogglePressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) return true;
            Gamepad gamepad = Gamepad.current;
            return gamepad != null && gamepad.startButton.wasPressedThisFrame;
        }

        private static bool MenuBackPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) return true;
            Gamepad gamepad = Gamepad.current;
            return gamepad != null && (gamepad.buttonEast.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame);
        }

        private static bool MenuConfirmPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) return true;
            Gamepad gamepad = Gamepad.current;
            return gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
        }

        /// <summary>메뉴에서 위아래로 움직이기. 방향키, W/S, 게임패드 십자키.</summary>
        private static int MenuVertical()
        {
            int move = 0;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) move--;
                if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) move++;
            }
            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                if (gamepad.dpad.up.wasPressedThisFrame) move--;
                if (gamepad.dpad.down.wasPressedThisFrame) move++;
            }
            return move;
        }

        /// <summary>메뉴에서 좌우로 움직이기. 방향키, A/D, 게임패드 십자키.</summary>
        private static int MenuHorizontal()
        {
            int move = 0;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) move--;
                if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) move++;
            }
            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                if (gamepad.dpad.left.wasPressedThisFrame) move--;
                if (gamepad.dpad.right.wasPressedThisFrame) move++;
            }
            return move;
        }

        private void OpenMenu()
        {
            _menu = MenuScreen.Pause;
            _menuIndex = PauseResume;
            _listening = false;
            _menuArmedUntil = 0f;
            _menuOpenedAt = Time.unscaledTime;
            _menuScreenAt = Time.unscaledTime;
            _menuNote = null;
            _menuMouseKnown = false;
            Time.timeScale = 0f;
            Sfx.Play(Sound.Tick, 0.7f);
        }

        private void CloseMenu()
        {
            _menu = MenuScreen.None;
            _listening = false;
            _menuClosedFrame = Time.frameCount;
            Time.timeScale = 1f;
        }

        private void ShowMenuNote(string text, Color color)
        {
            _menuNote = text;
            _menuNoteColor = color;
            _menuNoteAt = Time.unscaledTime;
        }

        private void SelectMenuRow(int index)
        {
            if (index == _menuIndex) return;
            _menuIndex = index;
            _menuArmedUntil = 0f;
            Sfx.Play(Sound.Tick, 0.5f);
        }

        private void UpdatePauseMenu()
        {
            if (MenuBackPressed())
            {
                CloseMenu();
                return;
            }

            int move = MenuVertical();
            if (move != 0) SelectMenuRow((_menuIndex + move + PauseItemCount) % PauseItemCount);
            if (MenuConfirmPressed()) ActivatePauseItem(_menuIndex);
        }

        private void ActivatePauseItem(int item)
        {
            switch (item)
            {
                case PauseResume:
                    CloseMenu();
                    break;
                case PauseKeys:
                    OpenKeyMenu(MenuScreen.Pause);
                    break;
                case PauseSound:
                    Sfx.Muted = !Sfx.Muted;
                    Sfx.Play(Sound.Tick, 0.7f);
                    break;
                case PauseTitle:
                    // 진행은 방에 들어올 때 저장되어 있다. 메인 화면 뒤에는 처음처럼 첫 거점을 깔고, 저장은 지우지 않는다.
                    CloseMenu();
                    Feel.CancelFreeze();
                    StartRun(false);
                    ShowTitle();
                    break;
                case PauseRestart:
                    if (Time.unscaledTime <= _menuArmedUntil)
                    {
                        CloseMenu();
                        _restartRequested = true;
                    }
                    else
                    {
                        _menuArmedUntil = Time.unscaledTime + MenuConfirmWindow;
                        Sfx.Play(Sound.Deny, 0.6f);
                    }
                    break;
            }
        }

        /// <summary>키 설정 화면을 연다. 뒤로 가면 returnTo(일시 정지 메뉴나 메인 화면)로 돌아간다.</summary>
        private void OpenKeyMenu(MenuScreen returnTo)
        {
            _menuReturn = returnTo;
            _menu = MenuScreen.Keys;
            _menuIndex = 0;
            _menuColumn = 0;
            _menuArmedUntil = 0f;
            _menuScreenAt = Time.unscaledTime;
            _menuNote = null;
            Sfx.Play(Sound.Tick, 0.7f);
        }

        /// <summary>키 설정 화면에서 연 곳(일시 정지 메뉴나 메인 화면)으로 돌아간다.</summary>
        private void BackToPauseMenu()
        {
            if (_menuReturn == MenuScreen.Title)
            {
                _menu = MenuScreen.Title;
                _menuIndex = Mathf.Max(0, _titleItems.IndexOf(TitleItem.Keys));
            }
            else
            {
                _menu = MenuScreen.Pause;
                _menuIndex = PauseKeys;
            }
            _menuArmedUntil = 0f;
            _listening = false;
            _menuScreenAt = Time.unscaledTime;
            Sfx.Play(Sound.Tick, 0.5f);
        }

        private void UpdateKeyMenu()
        {
            if (_listening)
            {
                UpdateListening();
                return;
            }

            if (MenuBackPressed())
            {
                BackToPauseMenu();
                return;
            }

            int vertical = MenuVertical();
            if (vertical != 0) SelectMenuRow((_menuIndex + vertical + KeyRowCount) % KeyRowCount);

            int horizontal = MenuHorizontal();
            if (horizontal != 0)
            {
                if (_menuIndex < KeyBindings.ActionCount)
                {
                    _menuColumn = (_menuColumn + horizontal + KeyBindings.Slots) % KeyBindings.Slots;
                    Sfx.Play(Sound.Tick, 0.5f);
                }
                else
                {
                    // 아래 버튼 두 개는 좌우로도 오간다.
                    SelectMenuRow(_menuIndex == KeyRowReset ? KeyRowBack : KeyRowReset);
                }
            }

            if (MenuConfirmPressed())
            {
                ActivateKeyRow(_menuIndex, _menuColumn);
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && _menuIndex < KeyBindings.ActionCount && (keyboard.deleteKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame))
            {
                ClearSelectedKey();
            }
        }

        private void ActivateKeyRow(int row, int column)
        {
            if (row == KeyRowBack)
            {
                BackToPauseMenu();
            }
            else if (row == KeyRowReset)
            {
                if (Time.unscaledTime <= _menuArmedUntil)
                {
                    _menuArmedUntil = 0f;
                    KeyBindings.ResetDefaults();
                    ShowMenuNote("모든 키를 처음 설정으로 되돌렸다", Ui.Good);
                    Sfx.Play(Sound.Chime, 0.6f);
                }
                else
                {
                    _menuArmedUntil = Time.unscaledTime + MenuConfirmWindow;
                    Sfx.Play(Sound.Deny, 0.6f);
                }
            }
            else
            {
                _menuIndex = row;
                _menuColumn = Mathf.Clamp(column, 0, KeyBindings.Slots - 1);
                _listening = true;
                _listenFrame = Time.frameCount;
                Sfx.Play(Sound.Tick, 0.7f);
            }
        }

        /// <summary>새 키를 기다리는 중. 키보드의 아무 키나 누르면 그 칸에 들어간다. Esc는 취소.</summary>
        private void UpdateListening()
        {
            // 칸을 고른 Enter나 Space가 바로 들어가지 않게, 고른 프레임에는 받지 않는다.
            if (Time.frameCount == _listenFrame) return;

            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || (gamepad != null && gamepad.buttonEast.wasPressedThisFrame))
            {
                CancelListening();
                return;
            }
            if (keyboard == null) return;

            var keys = keyboard.allKeys;
            for (int i = 0; i < keys.Count; i++)
            {
                KeyControl control = keys[i];
                if (control == null || !control.wasPressedThisFrame) continue;
                Key key = control.keyCode;
                if (!KeyBindings.CanBind(key)) continue;
                BindSelectedKey(key);
                return;
            }
        }

        private void CancelListening()
        {
            _listening = false;
            ShowMenuNote("바꾸지 않았다", Ui.TextDim);
        }

        private void BindSelectedKey(Key key)
        {
            _listening = false;
            var action = (GameAction)_menuIndex;
            string name = KeyBindings.KeyName(key);
            Key old = KeyBindings.Get(action, _menuColumn);

            // 다른 동작의 하나뿐인 키를 빼앗으면 그 동작을 할 수 없게 되므로 막는다.
            if (KeyBindings.Find(key, out GameAction owner, out int ownerSlot) && owner != action && old == Key.None
                && KeyBindings.Count(owner) <= 1 && !KeyBindings.CanBeEmpty(owner))
            {
                ShowMenuNote(name + " 키는 " + KeyBindings.Label(owner) + "의 하나뿐인 키다. " + KeyBindings.Label(owner) + "에 다른 키를 먼저 넣어 주세요", Ui.Danger);
                Sfx.Play(Sound.Deny, 0.7f);
                return;
            }

            if (KeyBindings.Assign(action, _menuColumn, key, out GameAction other, out int otherSlot))
            {
                if (other == action)
                {
                    ShowMenuNote(KeyBindings.Label(action) + ": " + name + " 키의 자리를 옮겼다", Ui.TextColor);
                }
                else if (old != Key.None)
                {
                    ShowMenuNote(name + " 키를 " + KeyBindings.Label(other) + "에서 가져오고, 그 자리에는 " + KeyBindings.KeyName(old) + " 키를 넣었다", Ui.Gold);
                }
                else
                {
                    ShowMenuNote(name + " 키를 " + KeyBindings.Label(other) + "에서 가져왔다", Ui.Gold);
                }
            }
            else
            {
                ShowMenuNote(KeyBindings.Label(action) + ": " + name, Ui.Good);
            }
            Sfx.Play(Sound.Chime, 0.5f);
        }

        private void ClearSelectedKey()
        {
            var action = (GameAction)_menuIndex;
            if (KeyBindings.Get(action, _menuColumn) == Key.None) return;
            if (KeyBindings.Clear(action, _menuColumn))
            {
                ShowMenuNote(KeyBindings.Label(action) + " 칸을 비웠다", Ui.TextColor);
                Sfx.Play(Sound.Tick, 0.6f);
            }
            else
            {
                ShowMenuNote(KeyBindings.Label(action) + "에는 키가 하나는 있어야 한다", Ui.Danger);
                Sfx.Play(Sound.Deny, 0.7f);
            }
        }

        // ---- 그리기 ----

        /// <summary>설정 화면을 맨 위에 그린다. 그리는 차례가 아닐 때도 불러서 클릭을 받는다.</summary>
        private void DrawMenu()
        {
            Vector2 mouse = Event.current.mousePosition;
            _menuMouseMoved = false;
            if (_hudRepaint)
            {
                // 메뉴를 연 뒤 마우스를 실제로 움직였을 때만 마우스 아래 항목을 고른다.
                _menuMouseMoved = _menuMouseKnown && (mouse - _menuMouse).sqrMagnitude > 1f;
                _menuMouse = mouse;
                _menuMouseKnown = true;

                // 메인 화면은 자기 배경을 따로 그린다.
                if (_menu != MenuScreen.Title)
                {
                    float dim = Mathf.Clamp01((Time.unscaledTime - _menuOpenedAt) / 0.15f);
                    Ui.Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.01f, 0f, 0.03f, 0.74f * dim));
                }
            }

            if (_menu == MenuScreen.Pause) DrawPauseMenu(mouse);
            else if (_menu == MenuScreen.Title) DrawTitle(mouse);
            else DrawKeyMenu(mouse);
        }

        /// <summary>화면이 바뀔 때 판이 살짝 올라오며 나타난다. 0에서 1.</summary>
        private float MenuEase()
        {
            float t = Mathf.Clamp01((Time.unscaledTime - _menuScreenAt) / 0.18f);
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        private string PauseItemLabel(int item, bool armed)
        {
            switch (item)
            {
                case PauseResume: return "계속하기";
                case PauseKeys: return "키 설정";
                case PauseSound: return "소리";
                case PauseTitle: return "메인 화면으로";
                default: return armed ? "한 번 더 누르면 처음부터" : "처음부터 다시 시작";
            }
        }

        private void DrawPauseMenu(Vector2 mouse)
        {
            float ease = MenuEase();
            float alpha = ease;
            float width = Mathf.Round(Ui.U(252f));
            float itemHeight = Mathf.Round(Ui.U(26f));
            float gap = Mathf.Round(Ui.U(6f));
            float titleSpace = Ui.U(58f);
            float footer = Ui.U(40f);
            float height = Mathf.Round(titleSpace + PauseItemCount * itemHeight + (PauseItemCount - 1) * gap + footer);
            var panel = new Rect(Mathf.Round((Screen.width - width) * 0.5f), Mathf.Round((Screen.height - height) * 0.5f + (1f - ease) * Ui.U(12f)), width, height);
            float edge = Mathf.Max(1f, Mathf.Round(Ui.U(1f)));
            bool armed = Time.unscaledTime <= _menuArmedUntil;

            if (_hudRepaint)
            {
                Ui.Glow(Grow(panel, Ui.U(36f)), Ui.Alpha(Ui.Accent, 0.14f * alpha));
                Ui.Panel(panel, alpha, Ui.Alpha(Ui.Accent, 0.6f));
                Ui.Fill(new Rect(panel.x + Ui.U(16f), panel.y, panel.width - Ui.U(32f), Mathf.Max(2f, Mathf.Round(Ui.U(2f)))), Ui.Alpha(Ui.Accent, 0.8f * alpha));
                Ui.Text(panel.center.x, panel.y + Ui.U(24f), Spaced("일시 정지"), 18f, Ui.Alpha(Ui.TextColor, alpha), true, TextAnchor.MiddleCenter);
                float lineWidth = Ui.U(40f);
                Ui.Fill(new Rect(panel.center.x - lineWidth * 0.5f, panel.y + Ui.U(42f), lineWidth, edge), Ui.Alpha(Ui.Accent, 0.6f * alpha));
            }

            for (int i = 0; i < PauseItemCount; i++)
            {
                var item = new Rect(panel.x + Ui.U(18f), Mathf.Round(panel.y + titleSpace + i * (itemHeight + gap)), panel.width - Ui.U(36f), itemHeight);
                if (_menuMouseMoved && item.Contains(mouse)) SelectMenuRow(i);
                if (GUI.Button(item, GUIContent.none, GUIStyle.none))
                {
                    _menuIndex = i;
                    ActivatePauseItem(i);
                    // 메뉴가 닫히거나 화면이 바뀌었으면 이번 차례에는 더 그리지 않는다.
                    if (_menu != MenuScreen.Pause) return;
                }
                if (!_hudRepaint) continue;

                bool selected = _menuIndex == i;
                bool danger = i == PauseRestart && armed;
                Color accent = danger ? Ui.Danger : Ui.Accent;
                float radius = Ui.U(4f);
                if (selected)
                {
                    Ui.Fill(item, Ui.Alpha(accent, 0.2f * alpha), radius);
                    Ui.Border(item, Ui.Alpha(accent, 0.95f * alpha), edge, radius);
                    Ui.Text(item.x + Ui.U(9f), item.center.y, "▶", 8f, Ui.Alpha(accent, alpha), true, TextAnchor.MiddleLeft);
                }
                else
                {
                    Ui.Fill(item, new Color(1f, 1f, 1f, 0.035f * alpha), radius);
                }

                Color textColor = danger ? Ui.Danger : (selected ? Color.white : Ui.TextColor);
                Ui.Label(item, PauseItemLabel(i, danger), Ui.Style(11f, selected, TextAnchor.MiddleCenter), Ui.Alpha(textColor, alpha * (selected ? 1f : 0.85f)));

                if (i == PauseSound)
                {
                    string state = Sfx.Muted ? "꺼짐" : "켜짐";
                    Color stateColor = Sfx.Muted ? Ui.TextDim : Ui.Good;
                    GUIStyle pillStyle = Ui.Style(8f, true, TextAnchor.MiddleCenter);
                    float pillWidth = Ui.Measure(state, pillStyle).x + Ui.U(12f);
                    var pill = new Rect(Mathf.Round(item.xMax - pillWidth - Ui.U(8f)), Mathf.Round(item.center.y - Ui.U(7f)), Mathf.Round(pillWidth), Mathf.Round(Ui.U(14f)));
                    Ui.Pill(pill, Ui.Alpha(stateColor, 0.16f * alpha), Ui.Alpha(stateColor, 0.8f * alpha));
                    Ui.Label(pill, state, pillStyle, Ui.Alpha(stateColor, alpha), 0f);
                }
                else if (i == PauseKeys)
                {
                    Ui.Text(item.xMax - Ui.U(10f), item.center.y, "▶", 8f, Ui.Alpha(Ui.TextDim, alpha * 0.7f), true, TextAnchor.MiddleRight);
                }
            }

            if (!_hudRepaint) return;

            float keyHeight = Ui.U(13f);
            float hintY = panel.yMax - Ui.U(26f);
            if (armed)
            {
                Ui.Text(panel.center.x, hintY + keyHeight * 0.5f, "이번 판의 진행이 사라지고 거점에서 다시 시작한다", 8f, Ui.Alpha(Ui.Danger, alpha), false, TextAnchor.MiddleCenter);
            }
            else if (_menuIndex == PauseTitle && !(_stage == 1 && _room == 0))
            {
                Ui.Text(panel.center.x, hintY + keyHeight * 0.5f, "이어하면 이 방에 들어온 때부터 다시 한다", 8f, Ui.Alpha(Ui.Gold, alpha), false, TextAnchor.MiddleCenter);
            }
            else
            {
                DrawHintRow(panel.center.x, hintY, keyHeight, alpha * 0.9f,
                    new[] { new[] { "↑", "↓" }, new[] { "Enter" }, new[] { "Esc" } },
                    new[] { "고르기", "선택", "돌아가기" });
            }
        }

        private void DrawKeyMenu(Vector2 mouse)
        {
            float now = Time.unscaledTime;
            float ease = MenuEase();
            float alpha = ease;
            int actions = KeyBindings.ActionCount;
            int slots = KeyBindings.Slots;

            float rowHeight = Mathf.Round(Ui.U(19f));
            float labelWidth = Ui.U(168f);
            float cellWidth = Mathf.Round(Ui.U(76f));
            float cellGap = Mathf.Round(Ui.U(6f));
            float padX = Ui.U(20f);
            float width = Mathf.Round(Mathf.Min(padX * 2f + labelWidth + slots * cellWidth + (slots - 1) * cellGap, Screen.width - Ui.U(16f)));
            float titleSpace = Ui.U(52f);
            float headerHeight = Ui.U(16f);
            float buttonsHeight = Ui.U(36f);
            float footer = Ui.U(50f);
            float height = Mathf.Round(titleSpace + headerHeight + actions * rowHeight + buttonsHeight + footer);
            var panel = new Rect(Mathf.Round((Screen.width - width) * 0.5f), Mathf.Round((Screen.height - height) * 0.5f + (1f - ease) * Ui.U(12f)), width, height);
            float edge = Mathf.Max(1f, Mathf.Round(Ui.U(1f)));
            float cellsLeft = panel.xMax - padX - slots * cellWidth - (slots - 1) * cellGap;
            float headerTop = panel.y + titleSpace;
            float rowsTop = headerTop + headerHeight;

            if (_hudRepaint)
            {
                Ui.Glow(Grow(panel, Ui.U(36f)), Ui.Alpha(Ui.Accent, 0.12f * alpha));
                Ui.Panel(panel, alpha, Ui.Alpha(Ui.Accent, 0.6f));
                Ui.Fill(new Rect(panel.x + Ui.U(16f), panel.y, panel.width - Ui.U(32f), Mathf.Max(2f, Mathf.Round(Ui.U(2f)))), Ui.Alpha(Ui.Accent, 0.8f * alpha));
                Ui.Text(panel.center.x, panel.y + Ui.U(22f), Spaced("키 설정"), 18f, Ui.Alpha(Ui.TextColor, alpha), true, TextAnchor.MiddleCenter);
                Ui.Text(panel.center.x, panel.y + Ui.U(40f), "칸을 클릭하거나 Enter를 누른 뒤 새 키를 누르면 바뀐다", 8f, Ui.Alpha(Ui.TextDim, alpha), false, TextAnchor.MiddleCenter);

                Ui.Text(panel.x + padX, headerTop + headerHeight * 0.5f, "동작", 8f, Ui.Alpha(Ui.TextDim, alpha * 0.85f), true, TextAnchor.MiddleLeft);
                for (int c = 0; c < slots; c++)
                {
                    float cx = cellsLeft + c * (cellWidth + cellGap) + cellWidth * 0.5f;
                    Ui.Text(cx, headerTop + headerHeight * 0.5f, "키 " + (c + 1), 8f, Ui.Alpha(Ui.TextDim, alpha * 0.85f), true, TextAnchor.MiddleCenter);
                }
                Ui.Fill(new Rect(panel.x + Ui.U(12f), rowsTop - edge, panel.width - Ui.U(24f), edge), new Color(1f, 1f, 1f, 0.1f * alpha));
            }

            for (int r = 0; r < actions; r++)
            {
                var action = (GameAction)r;
                float y = Mathf.Round(rowsTop + r * rowHeight);
                var row = new Rect(panel.x + Ui.U(12f), y, panel.width - Ui.U(24f), rowHeight);
                bool rowSelected = _menuIndex == r;

                if (_hudRepaint)
                {
                    float radius = Ui.U(3f);
                    if (rowSelected) Ui.Fill(row, Ui.Alpha(Ui.Accent, 0.1f * alpha), radius);
                    else if (r % 2 == 0) Ui.Fill(row, new Color(1f, 1f, 1f, 0.025f * alpha), radius);

                    float labelX = panel.x + padX;
                    float written = Ui.Text(labelX, y + rowHeight * 0.5f, KeyBindings.Label(action), 10f, Ui.Alpha(rowSelected ? Color.white : Ui.TextColor, alpha), rowSelected, TextAnchor.MiddleLeft);
                    string note = KeyBindings.Note(action);
                    if (note != null)
                    {
                        Ui.Text(labelX + written + Ui.U(6f), y + rowHeight * 0.5f, note, 8f, Ui.Alpha(Ui.TextDim, alpha * 0.75f), false, TextAnchor.MiddleLeft);
                    }
                }

                for (int c = 0; c < slots; c++)
                {
                    var cell = new Rect(Mathf.Round(cellsLeft + c * (cellWidth + cellGap)), y + Mathf.Round(Ui.U(2f)), cellWidth, rowHeight - Mathf.Round(Ui.U(4f)));
                    if (_menuMouseMoved && !_listening && cell.Contains(mouse) && (_menuIndex != r || _menuColumn != c))
                    {
                        if (_menuIndex == r) Sfx.Play(Sound.Tick, 0.5f);
                        SelectMenuRow(r);
                        _menuColumn = c;
                    }
                    if (GUI.Button(cell, GUIContent.none, GUIStyle.none))
                    {
                        // 키를 기다리는 중에 클릭하면 취소한다.
                        if (_listening) CancelListening();
                        else ActivateKeyRow(r, c);
                    }
                    if (!_hudRepaint) continue;

                    bool selected = rowSelected && _menuColumn == c;
                    float radius = Ui.U(3f);
                    Key key = KeyBindings.Get(action, c);
                    if (selected && _listening)
                    {
                        float pulse = 0.5f + 0.5f * Mathf.Sin(now * 9f);
                        Ui.Glow(Grow(cell, Ui.U(8f)), Ui.Alpha(Ui.Accent, 0.3f * alpha));
                        Ui.Fill(cell, Ui.Alpha(Ui.Accent, (0.22f + 0.18f * pulse) * alpha), radius);
                        Ui.Border(cell, Ui.Alpha(Ui.Accent, alpha), edge, radius);
                        Ui.Label(cell, "키를 누르세요", Ui.Style(8f, true, TextAnchor.MiddleCenter), Ui.Alpha(Color.white, alpha), 0f);
                        continue;
                    }

                    if (selected)
                    {
                        Ui.Fill(cell, Ui.Alpha(Ui.Accent, 0.16f * alpha), radius);
                        Ui.Border(cell, Ui.Alpha(Ui.Accent, 0.95f * alpha), edge, radius);
                    }
                    else
                    {
                        Ui.Border(cell, new Color(1f, 1f, 1f, 0.08f * alpha), edge, radius);
                    }

                    if (key == Key.None)
                    {
                        Ui.Label(cell, "비어 있음", Ui.Style(8f, false, TextAnchor.MiddleCenter), Ui.Alpha(Ui.TextDim, (selected ? 0.8f : 0.4f) * alpha), 0f);
                    }
                    else
                    {
                        string name = KeyBindings.KeyName(key);
                        float keyHeight = Ui.U(13f);
                        float keyWidth = KeycapWidth(name, keyHeight);
                        Ui.Keycap(cell.center.x - keyWidth * 0.5f, cell.center.y - keyHeight * 0.5f - Ui.U(1f), name, keyHeight, alpha, selected);
                    }
                }
            }

            // 아래 버튼: 기본값으로 되돌리기, 뒤로.
            bool armed = Time.unscaledTime <= _menuArmedUntil;
            float buttonHeight = Mathf.Round(Ui.U(22f));
            float buttonY = Mathf.Round(rowsTop + actions * rowHeight + Ui.U(10f));
            float resetWidth = Mathf.Round(Ui.U(170f));
            float backWidth = Mathf.Round(Ui.U(90f));
            float buttonGap = Mathf.Round(Ui.U(10f));
            float buttonsLeft = Mathf.Round(panel.center.x - (resetWidth + buttonGap + backWidth) * 0.5f);
            for (int b = 0; b < 2; b++)
            {
                int rowIndex = b == 0 ? KeyRowReset : KeyRowBack;
                var button = b == 0
                    ? new Rect(buttonsLeft, buttonY, resetWidth, buttonHeight)
                    : new Rect(buttonsLeft + resetWidth + buttonGap, buttonY, backWidth, buttonHeight);
                if (_menuMouseMoved && !_listening && button.Contains(mouse)) SelectMenuRow(rowIndex);
                if (GUI.Button(button, GUIContent.none, GUIStyle.none))
                {
                    if (_listening)
                    {
                        CancelListening();
                    }
                    else
                    {
                        _menuIndex = rowIndex;
                        ActivateKeyRow(rowIndex, 0);
                        if (_menu != MenuScreen.Keys) return;
                    }
                }
                if (!_hudRepaint) continue;

                bool selected = _menuIndex == rowIndex;
                bool danger = b == 0 && armed;
                Color accent = danger ? Ui.Danger : Ui.Accent;
                float radius = Ui.U(4f);
                if (selected)
                {
                    Ui.Fill(button, Ui.Alpha(accent, 0.2f * alpha), radius);
                    Ui.Border(button, Ui.Alpha(accent, 0.95f * alpha), edge, radius);
                }
                else
                {
                    Ui.Fill(button, new Color(1f, 1f, 1f, 0.04f * alpha), radius);
                    Ui.Border(button, new Color(1f, 1f, 1f, 0.12f * alpha), edge, radius);
                }
                string label = b == 0 ? (danger ? "한 번 더 누르면 되돌린다" : "기본값으로 되돌리기") : "뒤로";
                Color textColor = danger ? Ui.Danger : (selected ? Color.white : Ui.TextColor);
                Ui.Label(button, label, Ui.Style(9f, selected, TextAnchor.MiddleCenter), Ui.Alpha(textColor, alpha));
            }

            if (!_hudRepaint) return;

            // 알림 한 줄: 방금 바꾼 키, 서로 바꾼 키, 막힌 이유. 없으면 마우스와 게임패드 안내.
            float noteY = buttonY + buttonHeight + Ui.U(13f);
            float noteAge = now - _menuNoteAt;
            if (_menuNote != null && noteAge < MenuNoteTime)
            {
                float fade = Mathf.Clamp01((MenuNoteTime - noteAge) / 0.4f);
                float pop = noteAge < 0.15f ? 1f - noteAge / 0.15f : 0f;
                Ui.Text(panel.center.x, noteY - pop * Ui.U(2f), _menuNote, 9f, Ui.Alpha(_menuNoteColor, alpha * fade), true, TextAnchor.MiddleCenter);
            }
            else
            {
                Ui.Text(panel.center.x, noteY, "마우스 왼쪽 공격, 오른쪽 자리 바꾸기와 게임패드 버튼은 그대로 쓴다", 8f, Ui.Alpha(Ui.TextDim, alpha * 0.7f), false, TextAnchor.MiddleCenter);
            }

            float keyHeightHint = Ui.U(13f);
            float hintY = panel.yMax - Ui.U(24f);
            if (_listening)
            {
                DrawHintRow(panel.center.x, hintY, keyHeightHint, alpha * 0.9f,
                    new[] { new[] { "Esc" } },
                    new[] { "바꾸지 않고 취소" });
            }
            else
            {
                DrawHintRow(panel.center.x, hintY, keyHeightHint, alpha * 0.9f,
                    new[] { new[] { "↑", "↓", "←", "→" }, new[] { "Enter" }, new[] { KeyBindings.KeyName(Key.Backspace) }, new[] { "Esc" } },
                    new[] { "고르기", "바꾸기", "칸 비우기", "뒤로" });
            }
        }

        /// <summary>키캡과 설명을 가운데 정렬로 한 줄에 늘어놓는다.</summary>
        private static void DrawHintRow(float cx, float y, float keyHeight, float alpha, string[][] keys, string[] labels)
        {
            float spacing = Ui.U(14f);
            float total = 0f;
            for (int i = 0; i < labels.Length; i++)
            {
                if (i > 0) total += spacing;
                total += ControlWidth(keys[i], labels[i], keyHeight);
            }

            float x = Mathf.Round(cx - total * 0.5f);
            for (int i = 0; i < labels.Length; i++)
            {
                DrawControl(x, y, keys[i], labels[i], keyHeight, alpha);
                x += ControlWidth(keys[i], labels[i], keyHeight) + spacing;
            }
        }
    }
}

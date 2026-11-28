using System.Collections.Generic;
using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 메인 화면. Play를 누르면 첫 거점 위에 뜬다. 이어하기(저장된 판이 있을 때), 새 게임, 키 설정, 소리, 끝내기.
    /// 저장된 판이 있으면 새 게임은 한 번 더 눌러야 시작한다(저장이 지워진다).
    /// 일시 정지 메뉴의 "메인 화면으로"로도 돌아온다. 그러면 지금 방의 진행은 방에 들어온 때로 돌아간다.
    /// </summary>
    public sealed partial class PrototypeGame
    {
        private enum TitleItem
        {
            Continue,
            NewGame,
            Keys,
            Sound,
            Quit,
        }

        /// <summary>게임 제목은 아직 정하지 않았다. 정하면 여기만 바꾼다.</summary>
        private const string GameTitle = "그림자 분신";
        private const string GameSubtitle = "가제 · 프로토타입";

        private readonly List<TitleItem> _titleItems = new List<TitleItem>();
        private RunSaveData _titleSave;
        private float _titleShownAt = -10f;
        /// <summary>키 설정 화면에서 뒤로 가면 돌아갈 화면(일시 정지 메뉴나 메인 화면).</summary>
        private MenuScreen _menuReturn = MenuScreen.Pause;

        /// <summary>메인 화면이 떠 있거나, 메인 화면에서 연 키 설정 화면인지.</summary>
        private bool OnTitle => _menu == MenuScreen.Title || (_menu == MenuScreen.Keys && _menuReturn == MenuScreen.Title);

        private void ShowTitle()
        {
            _titleSave = RunSave.Load();
            _titleItems.Clear();
            if (_titleSave != null) _titleItems.Add(TitleItem.Continue);
            _titleItems.Add(TitleItem.NewGame);
            _titleItems.Add(TitleItem.Keys);
            _titleItems.Add(TitleItem.Sound);
            _titleItems.Add(TitleItem.Quit);

            _menu = MenuScreen.Title;
            _menuReturn = MenuScreen.Title;
            _menuIndex = 0;
            _listening = false;
            _menuArmedUntil = 0f;
            _menuNote = null;
            _menuMouseKnown = false;
            _menuOpenedAt = Time.unscaledTime;
            _menuScreenAt = Time.unscaledTime;
            _titleShownAt = Time.unscaledTime;
            _choices = null;
            Time.timeScale = 0f;
            Feel.CancelFreeze();
            MusicPlayer.Play(MusicTrack.Hub);
        }

        private void UpdateTitleMenu()
        {
            int count = _titleItems.Count;
            int move = MenuVertical();
            if (move != 0) SelectMenuRow((_menuIndex + move + count) % count);
            if (MenuConfirmPressed()) ActivateTitleItem(_menuIndex);
        }

        private void ActivateTitleItem(int index)
        {
            if (index < 0 || index >= _titleItems.Count) return;

            switch (_titleItems[index])
            {
                case TitleItem.Continue:
                    {
                        RunSaveData save = RunSave.Load();
                        CloseMenu();
                        Sfx.Play(Sound.DoorOpen, 0.8f);
                        if (save != null) ResumeRun(save);
                        else StartRun();
                        break;
                    }
                case TitleItem.NewGame:
                    if (_titleSave != null && Time.unscaledTime > _menuArmedUntil)
                    {
                        // 저장된 판이 지워지므로 한 번 더 눌러야 한다.
                        _menuArmedUntil = Time.unscaledTime + MenuConfirmWindow;
                        Sfx.Play(Sound.Deny, 0.6f);
                        break;
                    }
                    CloseMenu();
                    Sfx.Play(Sound.DoorOpen, 0.8f);
                    StartRun();
                    break;
                case TitleItem.Keys:
                    OpenKeyMenu(MenuScreen.Title);
                    break;
                case TitleItem.Sound:
                    Sfx.Muted = !Sfx.Muted;
                    Sfx.Play(Sound.Tick, 0.7f);
                    break;
                case TitleItem.Quit:
                    Sfx.Play(Sound.Tick, 0.7f);
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                    break;
            }
        }

        private string TitleItemLabel(TitleItem item, bool armed)
        {
            switch (item)
            {
                case TitleItem.Continue: return "이어하기";
                case TitleItem.NewGame: return armed ? "한 번 더 누르면 새 게임" : "새 게임";
                case TitleItem.Keys: return "키 설정";
                case TitleItem.Sound: return "소리";
                default: return "끝내기";
            }
        }

        /// <summary>메인 화면의 이어하기 줄 아래에 쓰는 저장된 판의 요약. 예: "2-4 상점 · 보통 · 12분 30초".</summary>
        private string SaveSummary(RunSaveData save)
        {
            if (save == null) return "";
            string where = save.Room == 0 ? save.Stage + "스테이지 거점" : save.Stage + "-" + save.Room + " " + RoomName((RoomType)save.RoomKind);
            string level = save.Difficulty >= 0 && save.Difficulty < Difficulty.Levels.Length ? Difficulty.Levels[save.Difficulty].Title : "";
            string text = where;
            if (!string.IsNullOrEmpty(level)) text += " · " + level;
            text += " · " + FormatTime(save.Elapsed);
            return text;
        }

        // ---- 그리기 ----

        private void DrawTitle(Vector2 mouse)
        {
            float now = Time.unscaledTime;
            float age = now - _titleShownAt;
            float alpha = Mathf.Clamp01(age / 0.5f);

            if (_hudRepaint)
            {
                // 뒤에 깔린 거점을 어둡게 덮고, 아래쪽을 더 어둡게 해서 메뉴가 잘 보이게 한다.
                Ui.Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.02f, 0.01f, 0.05f, 0.62f * alpha));
                Ui.FadeDown(new Rect(0f, 0f, Screen.width, Screen.height * 0.4f), Ui.Alpha(Ui.Ink, 0.45f * alpha));
                for (int k = 0; k < 4; k++)
                {
                    // 아래로 갈수록 몇 겹씩 더 어둡게 덮는다.
                    float from = Screen.height * (0.5f + k * 0.1f);
                    Ui.Fill(new Rect(0f, from, Screen.width, Screen.height - from), new Color(0.01f, 0f, 0.03f, 0.1f * alpha));
                }

                float cx = Screen.width * 0.5f;
                float titleY = Screen.height * 0.25f;
                float rise = (1f - Mathf.Clamp01(age / 0.8f)) * Ui.U(10f);

                // 제목 뒤를 따라 움직이는 보랏빛 그림자. 이 게임의 분신처럼 조금 늦게 따라온다.
                GUIStyle titleStyle = Ui.Style(44f, true, TextAnchor.MiddleCenter);
                string title = Spaced(GameTitle);
                Vector2 size = Ui.Measure(title, titleStyle);
                var rect = new Rect(cx - size.x * 0.5f - Ui.U(12f), titleY - size.y * 0.5f + rise, size.x + Ui.U(24f), size.y);
                Ui.Glow(new Rect(cx - size.x * 0.7f, titleY - size.y, size.x * 1.4f, size.y * 2f), Ui.Alpha(Ui.Accent, 0.22f * alpha));
                float lag = Mathf.Sin(now * 0.9f - 0.8f);
                var shadowRect = new Rect(rect.x + Ui.U(5f) + lag * Ui.U(4f), rect.y + Ui.U(4f), rect.width, rect.height);
                Ui.Label(shadowRect, title, titleStyle, new Color(0.55f, 0.35f, 1f, 0.45f * alpha), 0f);
                Ui.Label(rect, title, titleStyle, Ui.Alpha(Color.white, alpha), 2f);

                float lineWidth = Ui.U(150f) * Mathf.Clamp01(age / 0.6f);
                float edge = Mathf.Max(1f, Mathf.Round(Ui.U(1f)));
                Ui.Fill(new Rect(cx - lineWidth * 0.5f, titleY + size.y * 0.5f + Ui.U(6f) + rise, lineWidth, edge), Ui.Alpha(Ui.Accent, 0.7f * alpha));
                Ui.Text(cx, titleY + size.y * 0.5f + Ui.U(14f) + rise, Spaced(GameSubtitle), 9f, Ui.Alpha(Ui.TextDim, 0.9f * alpha), false, TextAnchor.UpperCenter);
            }

            DrawTitleItems(mouse, alpha);

            if (!_hudRepaint) return;

            // 아래쪽: 조작 안내와, 죽어도 남는 진행.
            float keyHeight = Ui.U(13f);
            bool armed = Time.unscaledTime <= _menuArmedUntil;
            float hintY = Screen.height - Ui.U(34f);
            if (armed)
            {
                Ui.Text(Screen.width * 0.5f, hintY + keyHeight * 0.5f, "저장된 판이 지워지고 처음부터 시작한다", 9f, Ui.Alpha(Ui.Danger, alpha), false, TextAnchor.MiddleCenter);
            }
            else
            {
                DrawHintRow(Screen.width * 0.5f, hintY, keyHeight, alpha * 0.85f,
                    new[] { new[] { "↑", "↓" }, new[] { "Enter" } },
                    new[] { "고르기", "선택" });
            }

            string meta = MetaProgress.CurrencyName + " " + MetaProgress.Shards + "개";
            if (MetaProgress.Clears > 0) meta += "   ·   클리어 " + MetaProgress.Clears + "번, 최고 " + FormatTime(MetaProgress.BestClearSeconds);
            Ui.Text(Screen.width - Ui.U(16f), Screen.height - Ui.U(12f), meta, 8f, Ui.Alpha(Ui.TextDim, 0.75f * alpha), false, TextAnchor.LowerRight);
        }

        private void DrawTitleItems(Vector2 mouse, float alpha)
        {
            int count = _titleItems.Count;
            float width = Mathf.Round(Ui.U(230f));
            float itemHeight = Mathf.Round(Ui.U(26f));
            float continueExtra = Mathf.Round(Ui.U(12f));
            float gap = Mathf.Round(Ui.U(6f));
            float left = Mathf.Round((Screen.width - width) * 0.5f);
            float top = Mathf.Round(Screen.height * 0.47f);
            float edge = Mathf.Max(1f, Mathf.Round(Ui.U(1f)));
            bool armed = Time.unscaledTime <= _menuArmedUntil;

            float y = top;
            for (int i = 0; i < count; i++)
            {
                TitleItem kind = _titleItems[i];
                float height = kind == TitleItem.Continue ? itemHeight + continueExtra : itemHeight;
                // 줄마다 조금씩 늦게 올라오며 나타난다.
                float appear = Mathf.Clamp01((Time.unscaledTime - _titleShownAt - 0.15f - i * 0.05f) / 0.25f);
                float itemAlpha = alpha * appear;
                var item = new Rect(left, Mathf.Round(y + (1f - appear) * Ui.U(8f)), width, height);
                y += height + gap;

                if (_menuMouseMoved && item.Contains(mouse)) SelectMenuRow(i);
                if (GUI.Button(item, GUIContent.none, GUIStyle.none))
                {
                    _menuIndex = i;
                    ActivateTitleItem(i);
                    if (_menu != MenuScreen.Title) return;
                }
                if (!_hudRepaint) continue;

                bool selected = _menuIndex == i;
                bool danger = kind == TitleItem.NewGame && armed;
                Color accent = danger ? Ui.Danger : (kind == TitleItem.Continue ? Ui.Gold : Ui.Accent);
                float radius = Ui.U(4f);
                if (selected)
                {
                    Ui.Glow(Grow(item, Ui.U(10f)), Ui.Alpha(accent, 0.12f * itemAlpha));
                    Ui.Fill(item, Ui.Alpha(accent, 0.2f * itemAlpha), radius);
                    Ui.Border(item, Ui.Alpha(accent, 0.95f * itemAlpha), edge, radius);
                    Ui.Text(item.x + Ui.U(9f), item.y + itemHeight * 0.5f, "▶", 8f, Ui.Alpha(accent, itemAlpha), true, TextAnchor.MiddleLeft);
                }
                else
                {
                    Ui.Fill(item, new Color(0f, 0f, 0f, 0.35f * itemAlpha), radius);
                    Ui.Border(item, new Color(1f, 1f, 1f, 0.08f * itemAlpha), edge, radius);
                }

                Color textColor = danger ? Ui.Danger : (selected ? Color.white : Ui.TextColor);
                var labelRect = new Rect(item.x, item.y, item.width, itemHeight);
                Ui.Label(labelRect, TitleItemLabel(kind, danger), Ui.Style(12f, selected, TextAnchor.MiddleCenter), Ui.Alpha(textColor, itemAlpha * (selected ? 1f : 0.85f)));

                if (kind == TitleItem.Continue)
                {
                    Ui.Text(item.center.x, item.y + itemHeight - Ui.U(3f), SaveSummary(_titleSave), 8f, Ui.Alpha(Ui.Gold, 0.85f * itemAlpha), false, TextAnchor.UpperCenter);
                }
                else if (kind == TitleItem.Sound)
                {
                    string state = Sfx.Muted ? "꺼짐" : "켜짐";
                    Color stateColor = Sfx.Muted ? Ui.TextDim : Ui.Good;
                    GUIStyle pillStyle = Ui.Style(8f, true, TextAnchor.MiddleCenter);
                    float pillWidth = Ui.Measure(state, pillStyle).x + Ui.U(12f);
                    var pill = new Rect(Mathf.Round(item.xMax - pillWidth - Ui.U(8f)), Mathf.Round(item.center.y - Ui.U(7f)), Mathf.Round(pillWidth), Mathf.Round(Ui.U(14f)));
                    Ui.Pill(pill, Ui.Alpha(stateColor, 0.16f * itemAlpha), Ui.Alpha(stateColor, 0.8f * itemAlpha));
                    Ui.Label(pill, state, pillStyle, Ui.Alpha(stateColor, itemAlpha), 0f);
                }
                else if (kind == TitleItem.Keys)
                {
                    Ui.Text(item.xMax - Ui.U(10f), item.center.y, "▶", 8f, Ui.Alpha(Ui.TextDim, itemAlpha * 0.7f), true, TextAnchor.MiddleRight);
                }
            }
        }
    }
}

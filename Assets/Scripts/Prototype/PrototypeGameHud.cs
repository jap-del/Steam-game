using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 화면 표시. 왼쪽 위에 체력과 기술 칸, 가운데 위에 스테이지 진행, 오른쪽 위에 골드와 그림자 조각,
    /// 아래에 조작 키와 보스 체력 막대를 그린다. 보스 등장, 강화 선택, 클리어 화면, 떠오르는 글자도 여기서 그린다.
    /// 그리는 도구는 Ui.cs에 있다.
    /// </summary>
    public sealed partial class PrototypeGame
    {
        private const float RoomBannerTime = 2.4f;
        private const float RoomFadeTime = 0.35f;
        private const float CalloutLifetime = 1.15f;

        private static readonly Color DashSlotColor = new Color(0.5f, 0.86f, 1f, 1f);
        private static readonly Color CalloutFill = new Color(0.24f, 0.03f, 0.07f, 0.9f);

        private readonly float[] _bossTrail = new float[4];
        private readonly float[] _bossTrailHold = new float[4];
        private readonly float[] _bossShown = new float[4];
        private readonly float[] _bossHitAt = { -10f, -10f, -10f, -10f };
        private readonly float[] _slotReadyAt = { -10f, -10f, -10f };
        private readonly bool[] _slotWasReady = { true, true, true };
        private int _hudHealth = -1;
        private int _hudLostTo;
        private float _hudHurtAt = -10f;
        private float _hudHealAt = -10f;
        private int _hudGold = -1;
        private float _hudGoldAt = -10f;
        private int _hudShards = -1;
        private float _hudShardsAt = -10f;
        private float _roomBannerAt = -10f;
        private float _roomFadeAt = -10f;
        private float _choicesOpenedAt;
        /// <summary>이번 OnGUI가 실제로 그리는 차례(Repaint)인지. 상태를 바꾸는 계산은 이때만 한다.</summary>
        private bool _hudRepaint;

        /// <summary>방을 새로 만들 때 화면 표시의 상태를 처음으로 돌린다.</summary>
        private void ResetHudForRoom(bool showBanner)
        {
            for (int i = 0; i < _bossTrail.Length; i++)
            {
                _bossTrail[i] = 0f;
                _bossTrailHold[i] = 0f;
                _bossShown[i] = 0f;
                _bossHitAt[i] = -10f;
            }
            _roomFadeAt = Time.unscaledTime;
            _roomBannerAt = showBanner ? Time.unscaledTime : -10f;
            _hudHealth = -1;
        }

        /// <summary>떠오르는 공격 이름. 붉은 알약 모양 판에 담겨 튀어나온다.</summary>
        public void ShowCallout(Vector2 position, string text, Color color)
        {
            _popups.Add(new Popup { Position = position, Text = text, Color = color, Age = 0f, Life = CalloutLifetime, Callout = true });
        }

        private void OnGUI()
        {
            Event current = Event.current;
            _hudRepaint = current.type == EventType.Repaint;
            if (!_hudRepaint)
            {
                // 그리는 차례가 아니면 클릭만 받는다. 설정 화면이 열려 있으면 설정 화면의 클릭만 받는다.
                if (MenuOpen)
                {
                    Ui.Begin();
                    DrawMenu();
                    return;
                }
                // 강화 카드의 클릭을 받을 때 말고는 그리는 차례에만 그린다.
                if (_choices == null) return;
            }

            Ui.Begin();

            // 메인 화면에서는 게임 화면 글자를 그리지 않는다.
            if (OnTitle)
            {
                DrawMenu();
                GUI.color = Color.white;
                return;
            }

            // 보스전의 어두운 화면 가장자리. 글자보다 먼저 그려서 글자를 가리지 않게 한다.
            Feel.DrawVignette();

            if (_runCleared)
            {
                DrawClearScreen();
                return;
            }

            if (_introActive)
            {
                DrawBossIntro();
                return;
            }

            if (_camera != null)
            {
                DrawHubLabels();
                DrawShopLabels();
                DrawPopups();
                DrawRaidLabels();
            }

            if (_player != null && _layout != null) DrawHud();
            DrawBossBars();
            DrawRoomBanner();

            if (_choices != null) DrawChoices();
            DrawRoomFade();
            if (MenuOpen) DrawMenu();
            GUI.color = Color.white;
        }

        // ---- 기본 화면 ----

        private void DrawHud()
        {
            // 화면 위쪽을 살짝 어둡게 깔아 글자가 배경에 묻히지 않게 한다.
            Ui.FadeDown(new Rect(0f, 0f, Screen.width, Ui.U(86f)), Ui.Alpha(Ui.Ink, 0.6f));

            DrawHealthAndSkills(Ui.U(14f), Ui.U(12f));
            // 보스와 싸우는 동안에는 가운데 위를 보스 체력 막대가 쓴다.
            if (_bosses.Count == 0) DrawStageTrack();
            DrawResources();
            if (_bosses.Count == 0 && !MenuOpen) DrawMenuHint();
        }

        private void DrawHealthAndSkills(float left, float top)
        {
            float now = Time.unscaledTime;
            int health = Mathf.Max(0, _player.Health);
            int max = Mathf.Max(1, Stats.MaxHealth);
            if (_hudRepaint)
            {
                if (_hudHealth < 0) _hudHealth = health;
                if (health < _hudHealth)
                {
                    _hudHurtAt = now;
                    _hudLostTo = _hudHealth;
                }
                else if (health > _hudHealth)
                {
                    _hudHealAt = now;
                }
                _hudHealth = health;
            }

            int px = Ui.IconPx;
            Vector2 heart = Ui.IconSize("Heart", px);
            float gap = px * 2f;
            float sinceHurt = now - _hudHurtAt;
            float sinceHeal = now - _hudHealAt;
            for (int i = 0; i < max; i++)
            {
                bool full = i < health;
                float x = left + i * (heart.x + gap);
                float y = top;
                Color tint = full ? Ui.Heart : new Color(0.24f, 0.19f, 0.32f, 0.95f);
                if (sinceHurt < 0.4f)
                {
                    // 맞으면 하트가 흔들리고, 방금 잃은 하트는 하얗게 번쩍인다.
                    float fade = 1f - sinceHurt / 0.4f;
                    x += Mathf.Sin(sinceHurt * 70f + i) * Ui.U(1.6f) * fade;
                    if (!full && i < _hudLostTo) tint = Color.Lerp(tint, Color.white, fade);
                }
                if (full && sinceHeal < 0.5f) tint = Color.Lerp(tint, Ui.Good, 1f - sinceHeal / 0.5f);
                if (full && health == 1)
                {
                    // 마지막 하나가 남으면 맥박처럼 뛴다.
                    float beat = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(now * 7f), 4f);
                    y -= beat * Ui.U(1.5f);
                    tint = Color.Lerp(tint, Color.white, beat * 0.45f);
                }
                Ui.Icon("Heart", x, y, px, tint);
            }

            float slot = Ui.U(30f);
            float slotGap = Ui.U(7f);
            float slotTop = top + heart.y + Ui.U(9f);
            bool shadowReady = _player.ShadowFill >= 1f;

            float dashTotal = Mathf.Max(0.01f, Stats.DashCooldown);
            float dashLeft = _player.DashCooldownRemaining;
            DrawSkillSlot(0, new Rect(left, slotTop, slot, slot), "Dash", DashSlotColor, KeyBindings.PrimaryName(GameAction.Dash), dashLeft / dashTotal, dashLeft, true, false, 0f);

            float swapTotal = Mathf.Max(0.01f, Stats.SwapCooldown);
            float swapLeft = _player.SwapCooldownRemaining;
            DrawSkillSlot(1, new Rect(left + slot + slotGap, slotTop, slot, slot), "Swap", Ui.Accent, KeyBindings.PrimaryName(GameAction.Swap), swapLeft / swapTotal, swapLeft, shadowReady, false, 0f);

            float anchorTotal = Mathf.Max(0.01f, Stats.AnchorCooldown);
            float anchorLeft = _player.AnchorCooldownRemaining;
            bool anchored = _player.AreShadowsAnchored;
            float anchorActive = anchored ? _player.AnchorRemaining / Mathf.Max(0.01f, Stats.AnchorDuration) : 0f;
            DrawSkillSlot(2, new Rect(left + (slot + slotGap) * 2f, slotTop, slot, slot), "Anchor", Ui.Accent, KeyBindings.PrimaryName(GameAction.Anchor), anchored ? 0f : anchorLeft / anchorTotal, anchored ? _player.AnchorRemaining : anchorLeft, shadowReady, anchored, anchorActive);

            if (!shadowReady)
            {
                // 분신이 아직 나타나지 않았다. 칸 아래에 얼마나 찼는지 보여 준다.
                float barTop = slotTop + slot + Ui.U(12f);
                float barWidth = slot * 3f + slotGap * 2f;
                Ui.Bar(new Rect(left, barTop, barWidth, Ui.U(3f)), _player.ShadowFill, Ui.Accent, Ui.Alpha(Ui.Ink, 0.8f));
                Ui.Text(left, barTop + Ui.U(5f), "분신이 깨어나는 중", 9f, Ui.Alpha(Ui.TextDim, 0.9f));
            }
        }

        /// <summary>
        /// 기술 칸 하나. 대기시간이 돌면 위에서부터 어둡게 덮이고 남은 초가 뜬다.
        /// 다시 쓸 수 있게 되면 테두리가 번쩍인다. 분신이 없어 못 쓰면 흐리게 그린다.
        /// </summary>
        private void DrawSkillSlot(int index, Rect rect, string icon, Color tint, string key, float cooldown01, float secondsLeft, bool usable, bool active, float active01)
        {
            float now = Time.unscaledTime;
            bool ready = usable && cooldown01 <= 0.001f;
            if (_hudRepaint)
            {
                if (ready && !_slotWasReady[index]) _slotReadyAt[index] = now;
                _slotWasReady[index] = ready;
            }

            float radius = Ui.U(5f);
            float edge = Mathf.Max(1f, Mathf.Round(Ui.U(1f)));
            if (ready || active)
            {
                float pulse = active ? 0.5f + 0.5f * Mathf.Sin(now * 9f) : 0.6f;
                Ui.Glow(Grow(rect, Ui.U(9f)), Ui.Alpha(tint, 0.22f + 0.18f * pulse));
            }
            Ui.Fill(new Rect(rect.x, rect.y + edge * 2f, rect.width, rect.height), Ui.Alpha(Ui.Ink, 0.7f), radius);
            Ui.Fill(rect, new Color(0.09f, 0.07f, 0.15f, 0.9f), radius);

            Color iconTint = ready || active ? tint : Color.Lerp(tint, new Color(0.35f, 0.33f, 0.42f, 1f), 0.65f);
            Ui.IconCentered(icon, rect.center.x, rect.center.y, Mathf.Max(1, Ui.IconPx), iconTint);

            if (!usable)
            {
                // 분신이 아직 없으면 쓸 수 없다. 아래에서부터 보라색으로 차오른다.
                Ui.Fill(Grow(rect, -edge), new Color(0f, 0f, 0f, 0.5f), radius);
                float fill = Mathf.Clamp01(_player.ShadowFill);
                float h = (rect.height - edge * 2f) * fill;
                Ui.Fill(new Rect(rect.x + edge, rect.yMax - edge - h, rect.width - edge * 2f, h), Ui.Alpha(Ui.Accent, 0.28f), Ui.U(3f));
            }
            else if (cooldown01 > 0.001f)
            {
                float h = (rect.height - edge * 2f) * Mathf.Clamp01(cooldown01);
                Ui.Fill(new Rect(rect.x + edge, rect.y + edge, rect.width - edge * 2f, h), new Color(0f, 0f, 0f, 0.62f), Ui.U(3f));
                string seconds = secondsLeft >= 10f ? Mathf.CeilToInt(secondsLeft).ToString() : secondsLeft.ToString("0.0");
                Ui.Label(new Rect(rect.x, rect.y - Ui.U(3f), rect.width, rect.height), seconds, Ui.Style(10f, true, TextAnchor.MiddleCenter), Ui.TextColor);
            }

            if (active)
            {
                // 분신을 고정한 동안: 테두리가 맥박 치고 아래 막대가 줄어든다.
                float barWidth = rect.width - Ui.U(8f);
                Ui.Bar(new Rect(rect.x + Ui.U(4f), rect.y + Ui.U(3f), barWidth, Ui.U(3f)), active01, tint, Ui.Alpha(Ui.Ink, 0.9f));
                Ui.Border(rect, Ui.Alpha(tint, 0.7f + 0.3f * Mathf.Sin(now * 9f)), edge * 2f, radius);
            }
            else
            {
                Ui.Border(rect, ready ? Ui.Alpha(tint, 0.95f) : new Color(1f, 1f, 1f, 0.16f), edge, radius);
            }

            float sinceReady = now - _slotReadyAt[index];
            if (sinceReady < 0.35f)
            {
                float t = sinceReady / 0.35f;
                Ui.Border(Grow(rect, Ui.U(7f) * t), new Color(1f, 1f, 1f, 1f - t), edge * 2f, radius + Ui.U(4f) * t);
            }

            // 키 표시는 칸 아래 가장자리 가운데에 걸쳐 둔다.
            float keyHeight = Ui.U(12f);
            float keyWidth = KeycapWidth(key, keyHeight);
            Ui.Keycap(rect.center.x - keyWidth * 0.5f, rect.yMax - keyHeight * 0.45f, key, keyHeight, ready || active ? 1f : 0.75f, false);
        }

        private static float KeycapWidth(string key, float height)
        {
            return Mathf.Round(Mathf.Max(height, Ui.Measure(key, Ui.Style(9f, true, TextAnchor.MiddleCenter)).x + Ui.U(7f)));
        }

        private static Rect Grow(Rect rect, float amount)
        {
            return new Rect(rect.x - amount, rect.y - amount, rect.width + amount * 2f, rect.height + amount * 2f);
        }

        /// <summary>가운데 위: 지금 방 이름, 스테이지의 방 여섯 개, 할 일.</summary>
        private void DrawStageTrack()
        {
            float cx = Screen.width * 0.5f;
            float y = Ui.U(9f);
            bool hub = _layout.Type == RoomType.Hub;

            if (hub)
            {
                Ui.Text(cx, y, StageBuilder.AreaNameFor(_stage), 13f, Ui.TextColor, true, TextAnchor.UpperCenter);
                y += Ui.U(19f);
                Ui.Text(cx, y, _stage + "스테이지 거점", 9f, Ui.Alpha(Ui.Accent, 0.95f), true, TextAnchor.UpperCenter);
                y += Ui.U(16f);
            }
            else
            {
                string number = _stage + "-" + _room;
                string name = "  " + RoomName(_layout.Type);
                GUIStyle style = Ui.Style(12f, true, TextAnchor.UpperLeft);
                float numberWidth = Ui.Measure(number, style).x;
                float nameWidth = Ui.Measure(name, style).x;
                float left = cx - (numberWidth + nameWidth) * 0.5f;
                Ui.Text(left, y, number, 12f, Ui.Accent, true);
                Ui.Text(left + numberWidth, y, name, 12f, Ui.TextColor, true);
                y += Ui.U(20f);
                DrawRoomPips(cx, y + Ui.U(7f));
                y += Ui.U(21f);
            }

            string goal = ObjectiveText();
            if (!string.IsNullOrEmpty(goal))
            {
                Color goalColor = _enemies.Count == 0 && !hub && _layout.Type != RoomType.Shop && _layout.Type != RoomType.Puzzle ? Ui.Good : Ui.Alpha(Ui.TextDim, 0.95f);
                if (_layout.Type == RoomType.Puzzle && _puzzleSolved) goalColor = Ui.Good;
                float width = Mathf.Min(Ui.U(460f), Screen.width - Ui.U(40f));
                GUIStyle wrap = Ui.Style(9f, false, TextAnchor.UpperCenter, true);
                float height = Ui.WrappedHeight(goal, wrap, width);
                Ui.Label(new Rect(cx - width * 0.5f, y, width, height), goal, wrap, goalColor);
                y += height + Ui.U(4f);
            }

            if (hub)
            {
                if (_lastRunSummary != null)
                {
                    float width = Mathf.Min(Ui.U(460f), Screen.width - Ui.U(40f));
                    GUIStyle wrap = Ui.Style(9f, false, TextAnchor.UpperCenter, true);
                    float height = Ui.WrappedHeight(_lastRunSummary, wrap, width);
                    Ui.Label(new Rect(cx - width * 0.5f, y, width, height), _lastRunSummary, wrap, Ui.Shard);
                    y += height + Ui.U(4f);
                }
                string test = "테스트 키  2 다음 스테이지 · 3 중간 보스 · 6 보스" + (_layout.HasEventDoor ? " · delete 두 번 저장 지우기" : "");
                Ui.Text(cx, y, test, 8f, Ui.Alpha(Ui.TextDim, 0.55f), false, TextAnchor.UpperCenter);
            }
        }

        /// <summary>스테이지의 방 여섯 개를 점으로 잇는다. 지나온 방은 밝고, 지금 방은 크게 빛나고, 남은 방은 흐리다.</summary>
        private void DrawRoomPips(float cx, float cy)
        {
            float spacing = Ui.U(24f);
            float left = cx - spacing * (RoomsPerStage - 1) * 0.5f;
            float line = Mathf.Max(1f, Mathf.Round(Ui.U(1f)));
            for (int i = 1; i < RoomsPerStage; i++)
            {
                float x0 = left + (i - 1) * spacing;
                bool passed = i < _room;
                Ui.Fill(new Rect(x0, cy - line * 0.5f, spacing, line), passed ? Ui.Alpha(Ui.Accent, 0.8f) : new Color(1f, 1f, 1f, 0.15f));
            }

            int px = Ui.IconPx;
            for (int i = 1; i <= RoomsPerStage; i++)
            {
                float x = left + (i - 1) * spacing;
                string icon = PipIcon(i);
                bool current = i == _room;
                bool passed = i < _room;
                Color tint;
                if (current)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
                    Ui.Glow(new Rect(x - Ui.U(13f), cy - Ui.U(13f), Ui.U(26f), Ui.U(26f)), Ui.Alpha(Ui.Accent, 0.35f + 0.25f * pulse));
                    tint = i == RoomsPerStage ? Ui.Danger : Color.white;
                }
                else if (passed)
                {
                    tint = Ui.Alpha(Ui.Accent, 0.95f);
                }
                else
                {
                    tint = i == RoomsPerStage ? new Color(0.75f, 0.3f, 0.35f, 0.9f) : new Color(0.45f, 0.42f, 0.55f, 0.9f);
                }
                Ui.IconCentered(icon, x, cy, current ? px + 1 : px, tint);
            }
        }

        private string PipIcon(int room)
        {
            switch (room)
            {
                case 3: return "Diamond";
                case 4: return "Coin";
                case 5:
                    if (room == _room) return _layout.Type == RoomType.Puzzle ? "Question" : "Dot";
                    return room < _room ? "Dot" : "Question";
                case 6: return "Skull";
                default: return "Dot";
            }
        }

        /// <summary>상호작용 키 이름. 키 설정에서 바꾼 키를 따른다.</summary>
        private static string InteractKey => KeyBindings.PrimaryName(GameAction.Interact);

        /// <summary>지금 방에서 할 일. 보스방에서는 보스 막대가 대신하므로 비워 둔다.</summary>
        private string ObjectiveText()
        {
            switch (_layout.Type)
            {
                case RoomType.Hub:
                    return _layout.HasExit
                        ? "강화의 제단에서 " + InteractKey + " 키를 누르면 " + MetaProgress.CurrencyName + " " + AltarPrice + "개로 강화를 하나 고른다. 오른쪽 출발의 문으로 나가면 " + _stage + "-1 시작"
                        : "난이도의 문 앞에서 " + InteractKey + " 키를 누르면 그 난이도로 1-1을 시작한다";
                case RoomType.Shop:
                    return "물건 앞에서 " + InteractKey + " 키로 산다. 오른쪽 문으로 나간다";
                case RoomType.Puzzle:
                    return _puzzleSolved
                        ? "퍼즐 해결! 오른쪽 문으로 나간다"
                        : "발판 두 개를 동시에 밟으면 보상. 한 발판에 서 있다가 다른 발판으로 가면 분신이 대신 밟아 준다. " + KeyBindings.PrimaryName(GameAction.Anchor) + " 키로 분신을 고정해도 된다";
                case RoomType.MidBoss:
                case RoomType.Boss:
                    return _enemies.Count > 0 ? null : "문이 열렸다. 오른쪽 끝의 문으로";
                default:
                    return _enemies.Count > 0 ? "남은 적 " + _enemies.Count : "문이 열렸다. 오른쪽 끝의 문으로";
            }
        }

        /// <summary>오른쪽 위: 골드, 그림자 조각, 처치와 협공, 난이도.</summary>
        private void DrawResources()
        {
            float now = Time.unscaledTime;
            if (_hudRepaint)
            {
                if (_hudGold >= 0 && _gold > _hudGold) _hudGoldAt = now;
                if (_hudShards >= 0 && _shards > _hudShards) _hudShardsAt = now;
                _hudGold = _gold;
                _hudShards = _shards;
            }

            float right = Screen.width - Ui.U(14f);
            float y = Ui.U(10f);
            DrawCounter(right, y, "Coin", _gold.ToString(), Ui.Gold, now - _hudGoldAt);
            y += Ui.U(20f);
            DrawCounter(right, y, "Shard", _shards.ToString(), Ui.Shard, now - _hudShardsAt);
            y += Ui.U(21f);
            Ui.Text(right, y, "처치 " + _kills + "   협공 " + _pincers, 9f, Ui.Alpha(Ui.TextDim, 0.9f), false, TextAnchor.UpperRight);
            y += Ui.U(15f);

            if (_layout.HasExit || _room >= 1)
            {
                string title = "난이도 " + _difficulty.Title;
                GUIStyle style = Ui.Style(8f, true, TextAnchor.MiddleCenter);
                float width = Ui.Measure(title, style).x + Ui.U(14f);
                var pill = new Rect(right - width, y, width, Ui.U(14f));
                Ui.Pill(pill, Ui.Alpha(_difficulty.DoorColor, 0.18f), Ui.Alpha(_difficulty.DoorColor, 0.8f));
                Ui.Label(pill, title, style, _difficulty.DoorColor, 0f);
            }
        }

        /// <summary>오른쪽 정렬된 아이콘과 숫자. 늘어나면 잠깐 하얗게 튄다.</summary>
        private static void DrawCounter(float right, float y, string icon, string value, Color color, float sinceGain)
        {
            float pop = sinceGain < 0.3f ? 1f - sinceGain / 0.3f : 0f;
            GUIStyle style = Ui.Style(13f, true, TextAnchor.UpperLeft);
            Vector2 size = Ui.Measure(value, style);
            float textLeft = right - size.x;
            Ui.Text(textLeft, y - pop * Ui.U(2f), value, 13f, Color.Lerp(color, Color.white, pop * 0.8f), true);
            int px = Ui.IconPx;
            Vector2 iconSize = Ui.IconSize(icon, px);
            Ui.Icon(icon, textLeft - iconSize.x - Ui.U(5f), y + (size.y - iconSize.y) * 0.5f - pop * Ui.U(2f), px, color);
        }

        /// <summary>
        /// 왼쪽 아래: Esc 설정 안내 한 줄. 조작 키 전체는 Esc 설정 화면의 키 설정에서 본다.
        /// 거점, 상점, 퍼즐방과 첫 방에서는 또렷하게, 싸우는 방에서는 흐리게 그린다.
        /// </summary>
        private void DrawMenuHint()
        {
            bool calm = _layout.Type == RoomType.Hub || _layout.Type == RoomType.Shop || _layout.Type == RoomType.Puzzle || (_stage == 1 && _room == 1);
            float alpha = calm ? 0.95f : 0.45f;
            float keyHeight = Ui.U(13f);
            float y = Screen.height - Ui.U(14f) - keyHeight;
            DrawControl(Ui.U(14f), y, new[] { "Esc" }, calm ? "설정 · 조작 키 보기와 바꾸기" : "설정", keyHeight, alpha);
        }

        private static float ControlWidth(string[] keys, string label, float keyHeight)
        {
            float width = 0f;
            for (int i = 0; i < keys.Length; i++)
            {
                width += keys[i] == "+" ? Ui.Measure("+", Ui.Style(9f, true)).x + Ui.U(4f) : KeycapWidth(keys[i], keyHeight) + Ui.U(2f);
            }
            return width + Ui.U(4f) + Ui.Measure(label, Ui.Style(9f)).x;
        }

        private static void DrawControl(float x, float y, string[] keys, string label, float keyHeight, float alpha)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i] == "+")
                {
                    x += Ui.Text(x + Ui.U(1f), y + keyHeight * 0.5f, "+", 9f, Ui.Alpha(Ui.TextDim, alpha), true, TextAnchor.MiddleLeft) + Ui.U(4f);
                    continue;
                }
                x += Ui.Keycap(x, y, keys[i], keyHeight, alpha) + Ui.U(2f);
            }
            Ui.Text(x + Ui.U(2f), y + keyHeight * 0.5f, label, 9f, Ui.Alpha(Ui.TextColor, alpha * 0.9f), false, TextAnchor.MiddleLeft);
        }

        /// <summary>방에 들어오면 가운데 위에 지역 이름과 방 이름이 잠깐 떴다가 사라진다.</summary>
        private void DrawRoomBanner()
        {
            float age = Time.unscaledTime - _roomBannerAt;
            if (age < 0f || age > RoomBannerTime || _layout == null) return;

            float alpha = Mathf.Clamp01(age / 0.25f) * Mathf.Clamp01((RoomBannerTime - age) / 0.5f);
            float slide = 1f - Mathf.Pow(1f - Mathf.Clamp01(age / 0.45f), 3f);
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.3f;

            Ui.Band(new Rect(0f, cy - Ui.U(30f), Screen.width, Ui.U(62f)), Ui.Alpha(Ui.Ink, 0.55f * alpha));

            string area = Spaced(StageBuilder.AreaNameFor(_stage));
            string title = _layout.Type == RoomType.Hub ? _stage + "스테이지 거점" : _stage + "-" + _room + "  " + RoomName(_layout.Type);
            Ui.Text(cx, cy - Ui.U(14f), area, 9f, Ui.Alpha(Ui.Accent, alpha), true, TextAnchor.MiddleCenter);
            float width = Ui.Text(cx, cy + Ui.U(6f), title, 20f, Ui.Alpha(Ui.TextColor, alpha), true, TextAnchor.MiddleCenter);

            // 제목 양옆으로 선이 뻗어 나간다.
            float lineLength = Ui.U(70f) * slide;
            float thickness = Mathf.Max(1f, Mathf.Round(Ui.U(1f)));
            float lineY = cy + Ui.U(6f);
            Ui.Fill(new Rect(cx - width * 0.5f - Ui.U(12f) - lineLength, lineY, lineLength, thickness), Ui.Alpha(Ui.Accent, 0.8f * alpha));
            Ui.Fill(new Rect(cx + width * 0.5f + Ui.U(12f), lineY, lineLength, thickness), Ui.Alpha(Ui.Accent, 0.8f * alpha));
        }

        /// <summary>방이 바뀔 때 검은 화면에서 밝아진다.</summary>
        private void DrawRoomFade()
        {
            float age = Time.unscaledTime - _roomFadeAt;
            if (age < 0f || age > RoomFadeTime) return;
            Ui.Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 1f - age / RoomFadeTime));
        }

        /// <summary>글자 사이를 벌린다. 제목을 넓게 쓸 때 쓴다.</summary>
        private static string Spaced(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var builder = new System.Text.StringBuilder(text.Length * 2);
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == ' ')
                {
                    builder.Append("  ");
                    continue;
                }
                builder.Append(text[i]);
                if (i < text.Length - 1) builder.Append(' ');
            }
            return builder.ToString();
        }

        // ---- 떠오르는 글자 ----

        private bool WorldToGui(Vector2 world, out Vector2 gui)
        {
            Vector3 screen = _camera.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));
            gui = new Vector2(screen.x, Screen.height - screen.y);
            return screen.z >= 0f;
        }

        private void DrawPopups()
        {
            for (int i = 0; i < _popups.Count; i++)
            {
                Popup popup = _popups[i];
                float life = popup.Life > 0f ? popup.Life : PopupLifetime;
                float t = Mathf.Clamp01(popup.Age / life);
                if (popup.Callout)
                {
                    DrawCallout(popup, t);
                    continue;
                }

                // 튀어나와 위로 떠오르다가 끝에서 흐려진다.
                float rise = (1f - (1f - t) * (1f - t)) * 0.9f;
                Vector2 gui;
                if (!WorldToGui(popup.Position + Vector2.up * rise, out gui)) continue;
                float alpha = t < 0.65f ? 1f : 1f - (t - 0.65f) / 0.35f;
                float pop = 1f + 0.4f * (1f - Mathf.Clamp01(popup.Age / 0.12f));

                Matrix4x4 previous = GUI.matrix;
                if (pop > 1.001f) GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), gui);
                GUIStyle style = Ui.Style(11f, true, TextAnchor.MiddleCenter);
                var rect = new Rect(gui.x - Ui.U(200f), gui.y - Ui.U(12f), Ui.U(400f), Ui.U(24f));
                Ui.Label(rect, popup.Text, style, Ui.Alpha(popup.Color, alpha));
                GUI.matrix = previous;
            }
        }

        /// <summary>보스와 적의 공격 이름. 붉은 판이 튀어나와 잠깐 머문다.</summary>
        private void DrawCallout(Popup popup, float t)
        {
            Vector2 gui;
            if (!WorldToGui(popup.Position + Vector2.up * (t * 0.35f), out gui)) return;
            float alpha = t < 0.75f ? 1f : 1f - (t - 0.75f) / 0.25f;
            float pop = 1f + 0.35f * (1f - Mathf.Clamp01(popup.Age / 0.1f));

            GUIStyle style = Ui.Style(11f, true, TextAnchor.MiddleLeft);
            Vector2 size = Ui.Measure(popup.Text, style);
            float bang = Ui.U(12f);
            float width = size.x + bang + Ui.U(16f);
            float height = Ui.U(19f);
            var rect = new Rect(Mathf.Round(gui.x - width * 0.5f), Mathf.Round(gui.y - height * 0.5f), Mathf.Round(width), Mathf.Round(height));

            Matrix4x4 previous = GUI.matrix;
            if (pop > 1.001f) GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), gui);
            Ui.Glow(Grow(rect, Ui.U(8f)), Ui.Alpha(popup.Color, 0.25f * alpha));
            Ui.Fill(new Rect(rect.x, rect.y + Ui.U(2f), rect.width, rect.height), Ui.Alpha(Ui.Ink, 0.6f * alpha), rect.height * 0.5f);
            Ui.Pill(rect, Ui.Alpha(CalloutFill, alpha), Ui.Alpha(popup.Color, alpha));
            Ui.Text(rect.x + Ui.U(9f), rect.center.y, "!", 11f, Ui.Alpha(popup.Color, alpha), true, TextAnchor.MiddleLeft);
            Ui.Label(new Rect(rect.x + Ui.U(8f) + bang, rect.y, size.x + 2f, rect.height), popup.Text, style, Ui.Alpha(Ui.TextColor, alpha), 0f);
            GUI.matrix = previous;
        }

        /// <summary>월드 좌표 위에 띄우는 작은 이름표. 아래쪽 가운데가 world에 온다. 그린 판을 돌려준다.</summary>
        private Rect DrawWorldTag(Vector2 world, string title, string sub, Color subColor, string subIcon, bool highlight, float alpha = 1f)
        {
            Vector2 gui;
            if (!WorldToGui(world, out gui)) return new Rect();

            GUIStyle titleStyle = Ui.Style(10f, true, TextAnchor.UpperCenter);
            GUIStyle subStyle = Ui.Style(9f, true, TextAnchor.UpperLeft);
            Vector2 titleSize = Ui.Measure(title, titleStyle);
            Vector2 subSize = string.IsNullOrEmpty(sub) ? Vector2.zero : Ui.Measure(sub, subStyle);
            int px = Mathf.Max(1, Ui.IconPx - 1);
            Vector2 iconSize = string.IsNullOrEmpty(subIcon) ? Vector2.zero : Ui.IconSize(subIcon, px);
            float subWidth = subSize.x + (iconSize.x > 0f ? iconSize.x + Ui.U(4f) : 0f);
            float width = Mathf.Max(titleSize.x, subWidth) + Ui.U(16f);
            float height = Ui.U(8f) + titleSize.y + (subSize.y > 0f ? subSize.y + Ui.U(2f) : 0f);
            var rect = new Rect(Mathf.Round(gui.x - width * 0.5f), Mathf.Round(gui.y - height), Mathf.Round(width), Mathf.Round(height));

            if (highlight) Ui.Glow(Grow(rect, Ui.U(8f)), Ui.Alpha(Ui.Accent, 0.3f * alpha));
            Ui.Panel(rect, alpha * 0.95f, highlight ? Ui.Alpha(Ui.Accent, 0.9f) : (Color?)null);
            // 판 아래 가운데에 작은 꼬리를 달아 무엇의 이름표인지 가리킨다.
            float tail = Ui.U(3f);
            Ui.Fill(new Rect(rect.center.x - tail, rect.yMax, tail * 2f, tail), Ui.Alpha(Ui.PanelFill, alpha));

            Ui.Label(new Rect(rect.x, rect.y + Ui.U(4f), rect.width, titleSize.y), title, titleStyle, Ui.Alpha(Ui.TextColor, alpha));
            if (subSize.y > 0f)
            {
                float subLeft = rect.center.x - subWidth * 0.5f;
                float subTop = rect.y + Ui.U(4f) + titleSize.y + Ui.U(2f);
                if (iconSize.x > 0f)
                {
                    Ui.Icon(subIcon, subLeft, subTop + (subSize.y - iconSize.y) * 0.5f, px, Ui.Alpha(subColor, alpha));
                    subLeft += iconSize.x + Ui.U(4f);
                }
                Ui.Label(new Rect(subLeft, subTop, subSize.x + 2f, subSize.y), sub, subStyle, Ui.Alpha(subColor, alpha));
            }
            return rect;
        }

        /// <summary>판 위에 띄우는 설명과 키 안내. "E 구매"처럼 그린다.</summary>
        private void DrawPrompt(Rect anchor, string description, string key, string action)
        {
            float width = Mathf.Min(Ui.U(240f), Screen.width - Ui.U(40f));
            GUIStyle wrap = Ui.Style(9f, false, TextAnchor.UpperCenter, true);
            float textHeight = string.IsNullOrEmpty(description) ? 0f : Ui.WrappedHeight(description, wrap, width - Ui.U(16f));
            float keyHeight = Ui.U(13f);
            float height = Ui.U(10f) + textHeight + (textHeight > 0f ? Ui.U(6f) : 0f) + keyHeight;
            var rect = new Rect(Mathf.Round(anchor.center.x - width * 0.5f), Mathf.Round(anchor.y - Ui.U(8f) - height), Mathf.Round(width), Mathf.Round(height));
            Ui.Panel(rect, 1f, Ui.Alpha(Ui.Good, 0.5f));
            if (textHeight > 0f) Ui.Label(new Rect(rect.x + Ui.U(8f), rect.y + Ui.U(5f), rect.width - Ui.U(16f), textHeight), description, wrap, Ui.TextColor);

            GUIStyle actionStyle = Ui.Style(9f, true);
            float actionWidth = KeycapWidth(key, keyHeight) + Ui.U(5f) + Ui.Measure(action, actionStyle).x;
            float x = rect.center.x - actionWidth * 0.5f;
            float y = rect.yMax - Ui.U(5f) - keyHeight;
            x += Ui.Keycap(x, y, key, keyHeight, 1f, true) + Ui.U(5f);
            Ui.Text(x, y + keyHeight * 0.5f, action, 9f, Ui.Good, true, TextAnchor.MiddleLeft);
        }

        private void DrawShopLabels()
        {
            for (int i = 0; i < _shopItems.Count; i++)
            {
                ShopItem item = _shopItems[i];
                Vector2 above = item.Position + Vector2.up * 1.55f;
                if (item.Sold)
                {
                    DrawWorldTag(above, "판매 완료", null, Color.white, null, false, 0.5f);
                    continue;
                }

                bool affordable = (item.PaysShards ? _shards : _gold) >= item.Price;
                Color priceColor = affordable ? (item.PaysShards ? Ui.Shard : Ui.Gold) : Ui.Danger;
                bool near = item == _nearItem;
                Rect tag = DrawWorldTag(above, item.Title, item.Price.ToString(), priceColor, item.PaysShards ? "Shard" : "Coin", near);
                if (near) DrawPrompt(tag, item.Description, InteractKey, affordable ? "구매" : (item.PaysShards ? MetaProgress.CurrencyName + "이 부족하다" : "골드가 부족하다"));
            }
        }

        private void DrawHubLabels()
        {
            if (_layout == null || _layout.Type != RoomType.Hub) return;

            for (int i = 0; i < _layout.DifficultyDoors.Count && i < Difficulty.Levels.Length; i++)
            {
                DifficultyLevel level = Difficulty.Levels[i];
                bool near = i == _nearDifficultyDoor;
                Rect tag = DrawWorldTag(_layout.DifficultyDoors[i] + Vector2.up * 3f, level.Title, "난이도", level.DoorColor, null, near);
                if (near) DrawPrompt(tag, level.Description, InteractKey, "이 난이도로 시작");
            }

            if (_layout.HasEventDoor)
            {
                DrawWorldTag(_layout.EventDoor + Vector2.up * 3f, "이벤트의 문", "잠겨 있다", Ui.TextDim, null, false, 0.75f);
            }

            if (_layout.HasAltar)
            {
                Vector2 above = _layout.Altar + Vector2.up * 1.75f;
                bool near = _nearAltar && !_altarUsed;
                if (_altarUsed)
                {
                    DrawWorldTag(above, "강화의 제단", "사용함", Ui.TextDim, null, false, 0.7f);
                }
                else
                {
                    Rect tag = DrawWorldTag(above, "강화의 제단", AltarPrice.ToString(), _shards >= AltarPrice ? Ui.Shard : Ui.Danger, "Shard", near);
                    if (near) DrawPrompt(tag, null, InteractKey, "강화 셋 중 하나 고르기");
                }
            }

            if (_layout.HasExit)
            {
                DrawWorldTag(_layout.ExitPosition + Vector2.up * 3f, "출발의 문", "닿으면 " + _stage + "-1 시작", Ui.Good, null, false);
            }
        }

        // ---- 보스 ----

        /// <summary>보스 체력 막대. 화면 위 가운데에 그린다. 쌍둥이처럼 보스가 둘이면 나란히 그린다.</summary>
        private void DrawBossBars()
        {
            if (_bosses.Count == 0) return;

            // 화면 위쪽 가운데. 왼쪽 위 체력과 오른쪽 위 골드를 가리지 않게 양옆을 비운다.
            int count = Mathf.Min(_bosses.Count, _bossTrail.Length);
            float gap = Ui.U(30f);
            float total = Mathf.Min(Ui.U(580f), Screen.width - Ui.U(300f));
            float width = (total - gap * (count - 1)) / count;
            float left = (Screen.width - total) * 0.5f;
            float barHeight = Mathf.Round(Ui.U(9f));
            float top = Mathf.Round(Ui.U(32f));

            GUI.color = Color.white;
            for (int i = 0; i < count; i++)
            {
                if (_bosses[i] != null) DrawOneBossBar(i, _bosses[i], left + i * (width + gap), top, width, barHeight);
            }
        }

        private void DrawOneBossBar(int index, Enemy boss, float left, float top, float width, float height)
        {
            float now = Time.unscaledTime;
            float ratio = Mathf.Clamp01(boss.HealthRatio * _bossBarFill);
            if (_hudRepaint)
            {
                // 맞으면 깎인 만큼 밝은 자국이 잠깐 남았다가 따라 내려온다.
                if (ratio < _bossShown[index] - 0.0001f)
                {
                    _bossTrailHold[index] = 0.45f;
                    _bossHitAt[index] = now;
                }
                _bossShown[index] = ratio;
                if (ratio > _bossTrail[index]) _bossTrail[index] = ratio;
                if (_bossTrailHold[index] > 0f) _bossTrailHold[index] -= Time.unscaledDeltaTime;
                else _bossTrail[index] = Mathf.MoveTowards(_bossTrail[index], ratio, Time.unscaledDeltaTime * 0.7f);
            }

            Color fill = new Color(0.9f, 0.18f, 0.26f, 1f);
            Color frame = new Color(1f, 1f, 1f, 0.28f);
            float glow = 0f;
            Color glowColor = Ui.Danger;
            if (boss.IsDowned)
            {
                fill = new Color(0.45f, 0.44f, 0.52f, 1f);
            }
            else if (boss.IsStaggered)
            {
                float flash = 0.5f + 0.5f * Mathf.Sin(now * 14f);
                fill = Color.Lerp(Ui.Gold, Color.white, flash);
                frame = Ui.Gold;
                glow = 0.4f;
                glowColor = Ui.Gold;
            }
            else if (_bossEnraged)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(now * 9f);
                fill = Color.Lerp(new Color(0.95f, 0.14f, 0.1f, 1f), new Color(1f, 0.55f, 0.18f, 1f), pulse);
                frame = Color.Lerp(new Color(1f, 0.3f, 0.2f, 0.7f), new Color(1f, 0.7f, 0.3f, 1f), pulse);
                glow = 0.2f + 0.2f * pulse;
                glowColor = new Color(1f, 0.3f, 0.15f, 1f);
            }

            var bar = new Rect(Mathf.Round(left), Mathf.Round(top), Mathf.Round(width), height);
            float pad = Mathf.Round(Ui.U(3f));
            Rect outer = Grow(bar, pad);
            float edge = Mathf.Max(1f, Mathf.Round(Ui.U(1f)));
            if (glow > 0f) Ui.Glow(Grow(outer, Ui.U(14f)), Ui.Alpha(glowColor, glow));
            Ui.Fill(new Rect(outer.x, outer.y + edge * 2f, outer.width, outer.height), Ui.Alpha(Ui.Ink, 0.7f), Ui.U(4f));
            Ui.Fill(outer, new Color(0.04f, 0.02f, 0.06f, 0.92f), Ui.U(4f));
            Ui.Border(outer, frame, edge, Ui.U(4f));

            Ui.Fill(bar, new Color(0.2f, 0.05f, 0.09f, 1f), Ui.U(2f));
            float trail = Mathf.Max(_bossTrail[index], ratio);
            if (trail > ratio) Ui.Fill(new Rect(bar.x, bar.y, bar.width * trail, bar.height), new Color(1f, 0.93f, 0.78f, 0.9f), Ui.U(2f));
            if (ratio > 0f)
            {
                var filled = new Rect(bar.x, bar.y, Mathf.Max(edge * 2f, bar.width * ratio), bar.height);
                // 방금 맞았으면 막대가 하얗게 번쩍인다.
                float hit = 1f - Mathf.Clamp01((now - _bossHitAt[index]) / 0.12f);
                Ui.Fill(filled, Color.Lerp(fill, Color.white, hit * 0.7f), Ui.U(2f));
                Ui.Fill(new Rect(filled.x + Ui.U(2f), filled.y, filled.width - Ui.U(4f), Mathf.Max(1f, Mathf.Round(height * 0.3f))), new Color(1f, 1f, 1f, 0.28f));
                Ui.Fill(new Rect(filled.x + Ui.U(2f), filled.yMax - Mathf.Max(1f, Mathf.Round(height * 0.25f)), filled.width - Ui.U(4f), Mathf.Max(1f, Mathf.Round(height * 0.25f))), new Color(0f, 0f, 0f, 0.22f));
            }

            // 10% 눈금.
            for (int i = 1; i < 10; i++)
            {
                Ui.Fill(new Rect(Mathf.Round(bar.x + bar.width * i * 0.1f), bar.y, edge, bar.height), new Color(0f, 0f, 0f, 0.3f));
            }

            // 단계의 경계: 여기 아래로 내려가면 보스가 달라진다.
            float[] marks = boss.PhaseMarks;
            if (marks != null)
            {
                int px = Mathf.Max(1, Ui.IconPx - 1);
                for (int i = 0; i < marks.Length; i++)
                {
                    float x = Mathf.Round(bar.x + bar.width * marks[i]);
                    bool passed = ratio < marks[i];
                    Ui.Fill(new Rect(x - edge, outer.y, edge * 2f, outer.height), passed ? new Color(1f, 1f, 1f, 0.25f) : new Color(1f, 1f, 1f, 0.85f));
                    Ui.IconCentered("Diamond", x, outer.y - Ui.U(1f), px, passed ? new Color(0.5f, 0.45f, 0.55f, 1f) : Ui.Gold);
                }
            }

            // 이름은 막대 왼쪽 위, 상태는 오른쪽 위.
            string title = boss.BossName;
            float nameY = outer.y - Ui.U(5f);
            float nameWidth = Ui.Text(bar.x, nameY, title, 12f, boss.IsFinalBoss ? new Color(1f, 0.88f, 0.8f, 1f) : Ui.TextColor, true, TextAnchor.LowerLeft);
            string kind = boss.IsFinalBoss ? (_stage >= FinalStage ? "마지막 보스" : "보스") : "중간 보스";
            Ui.Text(bar.x + nameWidth + Ui.U(8f), nameY - Ui.U(1f), kind, 8f, Ui.Alpha(Ui.TextDim, 0.85f), true, TextAnchor.LowerLeft);

            string status = boss.BarStatus;
            if (!string.IsNullOrEmpty(status))
            {
                Color statusColor = boss.IsStaggered || boss.IsDowned ? Ui.Gold : new Color(1f, 0.62f, 0.42f, 1f);
                GUIStyle style = Ui.Style(9f, true, TextAnchor.MiddleCenter);
                float pillWidth = Ui.Measure(status, style).x + Ui.U(14f);
                float pillHeight = Ui.U(14f);
                var pill = new Rect(Mathf.Round(bar.xMax - pillWidth), Mathf.Round(nameY - pillHeight), Mathf.Round(pillWidth), Mathf.Round(pillHeight));
                Ui.Pill(pill, Ui.Alpha(statusColor, 0.2f), statusColor);
                Ui.Label(pill, status, style, statusColor, 0f);
            }
        }

        private void DrawBossIntro()
        {
            float now = _introAge;
            // 검은 띠가 위아래에서 들어왔다가 끝날 때 다시 나간다.
            float slide = Mathf.Clamp01(now / 0.35f) * Mathf.Clamp01((IntroDuration - now) / 0.35f);
            float eased = 1f - (1f - slide) * (1f - slide);
            float bar = Mathf.Round(Screen.height * 0.14f * eased);
            float edge = Mathf.Max(1f, Mathf.Round(Ui.U(1f)));

            // 심장 박동: 박동마다 화면이 붉게 조였다가 풀린다. 이름이 뜨기 전까지만.
            float pulse = 0f;
            for (int i = 0; i < IntroHeartbeats.Length; i++)
            {
                float since = now - IntroHeartbeats[i];
                if (since >= 0f) pulse = Mathf.Max(pulse, Mathf.Exp(-since * 7f));
            }
            if (now >= IntroTitleAt) pulse = 0f;
            float build = Mathf.Clamp01(now / IntroTitleAt);
            if (now < IntroTitleAt) Ui.Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.35f, 0f, 0.02f, 0.18f * pulse + 0.1f * build));

            Ui.Fill(new Rect(0f, 0f, Screen.width, bar), Color.black);
            Ui.Fill(new Rect(0f, Screen.height - bar, Screen.width, bar), Color.black);
            float bandAlpha = (0.55f + 0.45f * pulse) * slide;
            Ui.Band(new Rect(0f, bar, Screen.width, edge * (1f + 2f * pulse)), Ui.Alpha(_introColor, bandAlpha));
            Ui.Band(new Rect(0f, Screen.height - bar - edge * (1f + 2f * pulse), Screen.width, edge * (1f + 2f * pulse)), Ui.Alpha(_introColor, bandAlpha));

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.42f;

            if (_introTitleShown)
            {
                float since = now - IntroTitleAt;
                float alpha = Mathf.Clamp01((IntroDuration - now) / 0.3f);
                float appear = 1f - Mathf.Pow(1f - Mathf.Clamp01(since / 0.35f), 3f);

                // 타격 순간 화면 가운데에서 빛줄기가 사방으로 뻗는다.
                DrawImpactLines(cx, cy, since);

                Ui.Band(new Rect(0f, cy - Ui.U(52f), Screen.width, Ui.U(112f)), Ui.Alpha(Ui.Ink, 0.75f * alpha));
                Ui.Glow(new Rect(cx - Ui.U(240f), cy - Ui.U(48f), Ui.U(480f), Ui.U(96f)), Ui.Alpha(_introColor, (0.22f + 0.25f * (1f - appear)) * alpha));

                string title = Spaced(_introTitle);
                GUIStyle titleStyle = Ui.Style(34f, true, TextAnchor.MiddleCenter);
                Vector2 titleSize = Ui.Measure(title, titleStyle);
                var titleRect = new Rect(cx - titleSize.x * 0.5f - Ui.U(10f), cy - titleSize.y * 0.5f, titleSize.x + Ui.U(20f), titleSize.y);

                // 이름의 큰 잔상이 퍼지며 사라진다.
                float echo = Mathf.Clamp01(since / 0.6f);
                if (echo < 1f)
                {
                    Matrix4x4 before = GUI.matrix;
                    float echoScale = 1f + 0.9f * echo;
                    GUIUtility.ScaleAroundPivot(new Vector2(echoScale, echoScale), new Vector2(cx, cy));
                    Ui.Label(titleRect, title, titleStyle, Ui.Alpha(_introColor, 0.35f * (1f - echo) * alpha), 0f);
                    GUI.matrix = before;
                }

                // 이름이 크게 나타났다가 제 크기로 줄어든다.
                float pop = 1f + 0.35f * (1f - Mathf.Clamp01(since / 0.15f));
                Matrix4x4 previous = GUI.matrix;
                if (pop > 1.001f) GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), new Vector2(cx, cy));
                Ui.Label(titleRect, title, titleStyle, Ui.Alpha(_introColor, alpha), 1.5f);
                GUI.matrix = previous;

                // 이름 양옆으로 선이 뻗는다.
                float lineLength = Ui.U(140f) * appear;
                float lineY = Mathf.Round(cy);
                Ui.Fill(new Rect(cx - titleSize.x * 0.5f - Ui.U(18f) - lineLength, lineY, lineLength, edge * 2f), Ui.Alpha(_introColor, 0.85f * alpha));
                Ui.Fill(new Rect(cx + titleSize.x * 0.5f + Ui.U(18f), lineY, lineLength, edge * 2f), Ui.Alpha(_introColor, 0.85f * alpha));

                Ui.Text(cx, cy - titleSize.y * 0.5f - Ui.U(8f), Spaced(_introSubtitle), 10f, Ui.Alpha(Ui.TextColor, 0.85f * alpha * appear), true, TextAnchor.LowerCenter);

                // 이름 아래 한 줄이 한 글자씩 써진다.
                if (!string.IsNullOrEmpty(_introEpithet) && since > 0.35f)
                {
                    int shown = Mathf.Min(_introEpithet.Length, Mathf.FloorToInt((since - 0.35f) * 22f) + 1);
                    Ui.Text(cx, cy + titleSize.y * 0.5f + Ui.U(10f), _introEpithet.Substring(0, shown), 11f, Ui.Alpha(Ui.TextDim, 0.95f * alpha), false, TextAnchor.UpperCenter);
                }
            }

            if (now >= IntroSkipAfter)
            {
                float keyHeight = Ui.U(13f);
                float alpha = 0.7f * slide;
                GUIStyle style = Ui.Style(9f);
                string label = "건너뛰기";
                float width = KeycapWidth("Space", keyHeight) + Ui.U(5f) + Ui.Measure(label, style).x;
                float x = Screen.width - Ui.U(20f) - width;
                float y = Screen.height - bar * 0.5f - keyHeight * 0.5f;
                x += Ui.Keycap(x, y, "Space", keyHeight, alpha) + Ui.U(5f);
                Ui.Text(x, y + keyHeight * 0.5f, label, 9f, Ui.Alpha(Ui.TextColor, alpha), false, TextAnchor.MiddleLeft);
            }
        }

        /// <summary>보스 이름이 뜨는 순간 화면 가운데에서 사방으로 뻗어 나가는 빛줄기. since는 타격 뒤 지난 초.</summary>
        private void DrawImpactLines(float cx, float cy, float since)
        {
            const float Life = 0.55f;
            if (since < 0f || since > Life) return;

            float t = since / Life;
            float fadeOut = 1f - t;
            float reach = Mathf.Max(Screen.width, Screen.height) * 0.75f;
            float thick = Mathf.Max(2f, Mathf.Round(Ui.U(3f)));
            Matrix4x4 previous = GUI.matrix;
            for (int i = 0; i < 28; i++)
            {
                // 줄마다 각도와 길이를 조금씩 다르게 해서 고르지 않게 보이게 한다.
                float angle = i * (360f / 28f) + Mathf.Sin(i * 12.9898f) * 6f;
                float length = reach * (0.35f + 0.25f * Mathf.Abs(Mathf.Sin(i * 78.233f)));
                float inner = Ui.U(60f) + reach * 0.5f * t;
                GUI.matrix = previous;
                GUIUtility.RotateAroundPivot(angle, new Vector2(cx, cy));
                Ui.Fill(new Rect(cx + inner, cy - thick * 0.5f, length * fadeOut, thick), Ui.Alpha(i % 3 == 0 ? Color.white : _introColor, 0.55f * fadeOut));
            }
            GUI.matrix = previous;
        }

        /// <summary>보스 기믹의 표시: 표식의 남은 초, 시계 감옥과 심판과 역행까지의 카운트다운.</summary>
        private void DrawRaidLabels()
        {
            float judgment = 0f;
            float cage = 0f;
            float reversal = 0f;
            for (int i = 0; i < _bosses.Count; i++)
            {
                Enemy boss = _bosses[i];
                if (boss == null) continue;
                if (boss.HasMark)
                {
                    Vector2 gui;
                    if (WorldToGui(boss.MarkPosition + Vector2.up * 1.35f, out gui))
                    {
                        float size = Ui.U(20f);
                        var circle = new Rect(gui.x - size * 0.5f, gui.y - size * 0.5f, size, size);
                        Ui.Fill(circle, new Color(0.25f, 0.18f, 0.02f, 0.85f), size * 0.5f);
                        Ui.Border(circle, new Color(1f, 0.9f, 0.55f, 1f), Mathf.Max(1f, Mathf.Round(Ui.U(1.5f))), size * 0.5f);
                        Ui.Label(circle, Mathf.CeilToInt(boss.MarkRemaining).ToString(), Ui.Style(11f, true, TextAnchor.MiddleCenter), new Color(1f, 0.94f, 0.7f, 1f), 0f);
                    }
                }
                judgment = Mathf.Max(judgment, boss.JudgmentRemaining);
                cage = Mathf.Max(cage, boss.CageRemaining);
                reversal = Mathf.Max(reversal, boss.ReversalRemaining);
            }

            if (reversal > 0f)
            {
                DrawCountdown("역행", reversal, Enemy.ReversalCastTime, new Color(0.7f, 0.95f, 1f, 1f));
            }
            if (cage > 0f)
            {
                // 시간이 멈출 때가 다가올수록 화면이 푸르게 차오른다.
                float c = 1f - Mathf.Clamp01(cage / Enemy.CageTime);
                Ui.Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.55f, 0.8f, 1f, 0.22f * c * c));
                DrawCountdown("정지", cage, Enemy.CageTime, new Color(0.65f, 0.88f, 1f, 1f));
            }
            if (judgment > 0f)
            {
                // 심판이 다가올수록 화면이 하얗게 차오른다.
                float t = 1f - Mathf.Clamp01(judgment / Enemy.JudgmentCastTime);
                Ui.Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(1f, 0.97f, 0.85f, 0.35f * t * t));
                DrawCountdown("심판", judgment, Enemy.JudgmentCastTime, new Color(1f, 0.93f, 0.62f, 1f));
            }
        }

        /// <summary>화면 위쪽 가운데의 큰 카운트다운. 초가 바뀔 때마다 숫자가 튄다.</summary>
        private void DrawCountdown(string label, float seconds, float total, Color color)
        {
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.27f;
            int whole = Mathf.CeilToInt(seconds);
            float intoSecond = whole - seconds;
            float pop = 1f + 0.3f * (1f - Mathf.Clamp01(intoSecond / 0.15f));

            Ui.Band(new Rect(0f, cy - Ui.U(40f), Screen.width, Ui.U(84f)), Ui.Alpha(Ui.Ink, 0.55f));
            Ui.Glow(new Rect(cx - Ui.U(80f), cy - Ui.U(36f), Ui.U(160f), Ui.U(72f)), Ui.Alpha(color, 0.18f + 0.15f * (pop - 1f) / 0.3f));
            Ui.Text(cx, cy - Ui.U(24f), Spaced(label), 11f, color, true, TextAnchor.MiddleCenter);

            Matrix4x4 previous = GUI.matrix;
            if (pop > 1.001f) GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), new Vector2(cx, cy + Ui.U(4f)));
            Ui.Text(cx, cy + Ui.U(4f), whole.ToString(), 36f, Color.Lerp(color, Color.white, (pop - 1f) / 0.3f), true, TextAnchor.MiddleCenter);
            GUI.matrix = previous;

            float barWidth = Ui.U(150f);
            Ui.Bar(new Rect(cx - barWidth * 0.5f, cy + Ui.U(30f), barWidth, Ui.U(3f)), seconds / Mathf.Max(0.01f, total), color, Ui.Alpha(Ui.Ink, 0.85f));
        }

        // ---- 클리어 화면 ----

        /// <summary>한 판 클리어 화면. 걸린 시간과 이번 판의 기록을 보여 준다.</summary>
        private void DrawClearScreen()
        {
            float now = Time.unscaledTime;
            float age = now - (_clearInputAt - 1f);
            float appear = 1f - Mathf.Pow(1f - Mathf.Clamp01(age / 0.45f), 3f);

            Ui.Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.75f * appear));

            float width = Mathf.Min(Ui.U(380f), Screen.width - Ui.U(40f));
            float height = Ui.U(262f);
            var panel = new Rect(Mathf.Round((Screen.width - width) * 0.5f), Mathf.Round((Screen.height - height) * 0.5f + (1f - appear) * Ui.U(24f)), Mathf.Round(width), Mathf.Round(height));
            Ui.Glow(Grow(panel, Ui.U(30f)), Ui.Alpha(Ui.Gold, 0.12f * appear));
            Ui.Panel(panel, appear, Ui.Alpha(Ui.Gold, 0.6f));
            Ui.Fill(new Rect(panel.x + Ui.U(20f), panel.y, panel.width - Ui.U(40f), Mathf.Max(2f, Mathf.Round(Ui.U(2f)))), Ui.Alpha(Ui.Gold, appear));

            float cx = panel.center.x;
            float y = panel.y + Ui.U(16f);
            Ui.Text(cx, y, Spaced("한 판 클리어"), 9f, Ui.Alpha(Ui.Gold, appear), true, TextAnchor.UpperCenter);
            y += Ui.U(16f);
            Ui.Text(cx, y, "시계탑을 넘었다", 24f, Ui.Alpha(Ui.TextColor, appear), true, TextAnchor.UpperCenter);
            y += Ui.U(38f);
            Ui.Fill(new Rect(panel.x + Ui.U(24f), y, panel.width - Ui.U(48f), Mathf.Max(1f, Mathf.Round(Ui.U(1f)))), Ui.Alpha(Ui.PanelEdge, appear * 2f));
            y += Ui.U(10f);

            string[] labels = { "난이도", "걸린 시간", "처치", "협공", "이번 판 " + MetaProgress.CurrencyName, "지금까지 클리어" };
            string[] values =
            {
                _difficulty.Title,
                FormatTime(_clearSeconds),
                _kills.ToString(),
                _pincers.ToString(),
                _runShards + "개 (보너스 " + _clearBonus + ")",
                MetaProgress.Clears + "번",
            };
            float rowHeight = Ui.U(19f);
            float rowLeft = panel.x + Ui.U(30f);
            float rowRight = panel.xMax - Ui.U(30f);
            for (int i = 0; i < labels.Length; i++)
            {
                // 줄이 하나씩 차례로 나타난다.
                float rowAppear = Mathf.Clamp01((age - 0.25f - i * 0.07f) / 0.2f);
                float rowY = y + i * rowHeight;
                Ui.Text(rowLeft, rowY, labels[i], 10f, Ui.Alpha(Ui.TextDim, rowAppear), false);
                Color valueColor = i == 0 ? _difficulty.DoorColor : (i == 4 ? Ui.Shard : Ui.TextColor);
                if (i == 1 && _clearNewBest) valueColor = Ui.Gold;
                Ui.Text(rowRight, rowY, values[i], 10f, Ui.Alpha(valueColor, rowAppear), true, TextAnchor.UpperRight);
            }

            // 걸린 시간 옆에 최고 기록 표시.
            float bestAppear = Mathf.Clamp01((age - 0.6f) / 0.2f);
            string best = _clearNewBest ? "최고 기록!" : "최고 " + FormatTime(MetaProgress.BestClearSeconds);
            Color bestColor = _clearNewBest ? Ui.Gold : Ui.TextDim;
            GUIStyle bestStyle = Ui.Style(8f, true, TextAnchor.MiddleCenter);
            float bestWidth = Ui.Measure(best, bestStyle).x + Ui.U(12f);
            float timeWidth = Ui.Measure(values[1], Ui.Style(10f, true)).x;
            var bestPill = new Rect(Mathf.Round(rowRight - timeWidth - Ui.U(8f) - bestWidth), Mathf.Round(y + rowHeight + Ui.U(1f)), Mathf.Round(bestWidth), Mathf.Round(Ui.U(13f)));
            Ui.Pill(bestPill, Ui.Alpha(bestColor, 0.18f * bestAppear), Ui.Alpha(bestColor, 0.8f * bestAppear));
            Ui.Label(bestPill, best, bestStyle, Ui.Alpha(bestColor, bestAppear), 0f);

            if (now >= _clearInputAt)
            {
                float blink = 0.65f + 0.35f * Mathf.Sin(now * 4f);
                float keyHeight = Ui.U(13f);
                string tail = " 처음 거점으로";
                GUIStyle style = Ui.Style(9f, true);
                // 상호작용 키와 Space 둘 다 받는다. 상호작용 키를 Space로 바꿨거나 비웠으면 Space만 보여 준다.
                string interact = InteractKey;
                bool showInteract = interact != "Space" && interact != "-";
                float total = (showInteract ? KeycapWidth(interact, keyHeight) + Ui.U(4f) : 0f) + KeycapWidth("Space", keyHeight) + Ui.Measure(tail, style).x;
                float x = cx - total * 0.5f;
                float keyY = panel.yMax - Ui.U(16f) - keyHeight;
                if (showInteract) x += Ui.Keycap(x, keyY, interact, keyHeight, blink, true) + Ui.U(4f);
                x += Ui.Keycap(x, keyY, "Space", keyHeight, blink, true);
                Ui.Text(x, keyY + keyHeight * 0.5f, tail, 9f, Ui.Alpha(Ui.Good, blink), true, TextAnchor.MiddleLeft);
            }
        }

        // ---- 강화 선택 ----

        private void DrawChoices()
        {
            var titles = new string[_choices.Count];
            var bodies = new string[_choices.Count];
            for (int i = 0; i < _choices.Count; i++)
            {
                titles[i] = _choices[i].Title;
                bodies[i] = _choices[i].Description;
            }

            int clicked = DrawCards(titles, bodies);
            if (clicked >= 0) Choose(clicked);
        }

        /// <summary>화면을 어둡게 하고 강화 카드를 나란히 그린다. 클릭한 카드의 번호를 돌려주고, 없으면 -1이다.</summary>
        private int DrawCards(string[] titles, string[] bodies)
        {
            float now = Time.unscaledTime;
            float age = now - _choicesOpenedAt;
            float dim = Mathf.Clamp01(age / 0.2f);
            Ui.Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.01f, 0f, 0.03f, 0.7f * dim));

            int count = titles.Length;
            float gap = Ui.U(14f);
            float cardWidth = Mathf.Min(Ui.U(196f), (Screen.width - Ui.U(40f) - gap * (count - 1)) / Mathf.Max(1, count));
            float cardHeight = Ui.U(156f);
            float totalWidth = count * cardWidth + (count - 1) * gap;
            float left = (Screen.width - totalWidth) * 0.5f;
            float top = (Screen.height - cardHeight) * 0.5f + Ui.U(8f);
            float cx = Screen.width * 0.5f;

            Ui.Text(cx, top - Ui.U(52f), "강화 선택", 20f, Ui.Alpha(Ui.TextColor, dim), true, TextAnchor.MiddleCenter);
            string cost = MetaProgress.CurrencyName + " " + AltarPrice + "개를 쓴다";
            GUIStyle costStyle = Ui.Style(9f);
            int costPx = Mathf.Max(1, Ui.IconPx - 1);
            Vector2 costIcon = Ui.IconSize("Shard", costPx);
            float costWidth = costIcon.x + Ui.U(5f) + Ui.Measure(cost, costStyle).x;
            float costX = cx - costWidth * 0.5f;
            float costY = top - Ui.U(28f);
            Ui.Icon("Shard", costX, costY - costIcon.y * 0.5f, costPx, Ui.Alpha(Ui.Shard, dim));
            Ui.Text(costX + costIcon.x + Ui.U(5f), costY, cost, 9f, Ui.Alpha(Ui.TextDim, dim), false, TextAnchor.MiddleLeft);

            int clicked = -1;
            Vector2 mouse = Event.current.mousePosition;
            for (int i = 0; i < count; i++)
            {
                // 카드가 차례로 아래에서 올라온다.
                float t = Mathf.Clamp01((age - 0.05f - i * 0.07f) / 0.28f);
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                var card = new Rect(Mathf.Round(left + i * (cardWidth + gap)), Mathf.Round(top + (1f - ease) * Ui.U(26f)), Mathf.Round(cardWidth), Mathf.Round(cardHeight));
                bool hover = ease >= 1f && card.Contains(mouse);
                if (hover) card.y -= Mathf.Round(Ui.U(4f));
                if (GUI.Button(card, GUIContent.none, GUIStyle.none)) clicked = i;
                if (!_hudRepaint) continue;

                float alpha = ease;
                if (hover) Ui.Glow(Grow(card, Ui.U(16f)), Ui.Alpha(Ui.Accent, 0.35f));
                Ui.Panel(card, alpha, hover ? Ui.Alpha(Ui.Accent, 0.95f) : Ui.Alpha(Ui.PanelEdge, 1.6f));
                Ui.Fill(new Rect(card.x + Ui.U(12f), card.y, card.width - Ui.U(24f), Mathf.Max(2f, Mathf.Round(Ui.U(2f)))), Ui.Alpha(Ui.Accent, alpha * (hover ? 1f : 0.55f)));

                Ui.Keycap(card.x + Ui.U(8f), card.y + Ui.U(8f), (i + 1).ToString(), Ui.U(14f), alpha, hover);

                int px = Ui.IconPx;
                float bob = hover ? Mathf.Sin(now * 5f) * Ui.U(1.5f) : 0f;
                Ui.Glow(new Rect(card.center.x - Ui.U(18f), card.y + Ui.U(10f), Ui.U(36f), Ui.U(36f)), Ui.Alpha(Ui.Shard, 0.3f * alpha));
                Ui.IconCentered("Shard", card.center.x, card.y + Ui.U(28f) + bob, px, Ui.Alpha(Ui.Shard, alpha));

                GUIStyle titleStyle = Ui.Style(12f, true, TextAnchor.UpperCenter, true);
                float titleHeight = Ui.WrappedHeight(titles[i], titleStyle, card.width - Ui.U(20f));
                Ui.Label(new Rect(card.x + Ui.U(10f), card.y + Ui.U(48f), card.width - Ui.U(20f), titleHeight), titles[i], titleStyle, Ui.Alpha(hover ? Color.white : Ui.TextColor, alpha));
                float divider = card.y + Ui.U(52f) + titleHeight;
                Ui.Fill(new Rect(card.center.x - Ui.U(24f), divider, Ui.U(48f), Mathf.Max(1f, Mathf.Round(Ui.U(1f)))), Ui.Alpha(Ui.Accent, 0.6f * alpha));

                GUIStyle bodyStyle = Ui.Style(9f, false, TextAnchor.UpperCenter, true);
                Ui.Label(new Rect(card.x + Ui.U(12f), divider + Ui.U(7f), card.width - Ui.U(24f), card.yMax - divider - Ui.U(12f)), bodies[i], bodyStyle, Ui.Alpha(Ui.TextDim, alpha));
            }

            if (_hudRepaint)
            {
                // 아래쪽 안내: 1 2 3 고르기, Esc 닫기.
                float keyHeight = Ui.U(13f);
                GUIStyle style = Ui.Style(9f);
                string pick = "고르기";
                string close = "닫기";
                float width = 0f;
                for (int i = 0; i < count; i++) width += KeycapWidth((i + 1).ToString(), keyHeight) + Ui.U(3f);
                width += Ui.U(3f) + Ui.Measure(pick, style).x + Ui.U(18f) + KeycapWidth("Esc", keyHeight) + Ui.U(5f) + Ui.Measure(close, style).x;
                float x = cx - width * 0.5f;
                float y = top + cardHeight + Ui.U(20f);
                for (int i = 0; i < count; i++) x += Ui.Keycap(x, y, (i + 1).ToString(), keyHeight, dim) + Ui.U(3f);
                x += Ui.U(3f) + Ui.Text(x + Ui.U(3f), y + keyHeight * 0.5f, pick, 9f, Ui.Alpha(Ui.TextColor, dim), false, TextAnchor.MiddleLeft) + Ui.U(18f);
                x += Ui.Keycap(x, y, "Esc", keyHeight, dim) + Ui.U(5f);
                Ui.Text(x, y + keyHeight * 0.5f, close, 9f, Ui.Alpha(Ui.TextColor, dim), false, TextAnchor.MiddleLeft);
            }

            return clicked;
        }
    }
}

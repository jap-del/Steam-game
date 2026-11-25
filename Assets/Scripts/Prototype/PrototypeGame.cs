using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 프로토타입 진행 담당. SampleScene에서 Play를 누르면 씬을 따로 손대지 않아도
    /// 방, 플레이어, 분신, 적을 코드로 만들어 준다.
    /// 한 스테이지는 방 여섯 개(전투, 전투, 중간 보스, 상점, 퍼즐 또는 전투, 보스)이고,
    /// 스테이지마다 앞에 거점이 있다. 1-6 다음에 2스테이지 거점, 그다음에 2-1이 온다.
    /// 첫 거점에서는 난이도 문으로 들어가 판을 시작하고, 그 뒤의 거점에서는 그림자 조각으로 강화를 하나 산다.
    /// 적을 잡아 모은 그림자 조각은 죽어도 남고 상점에서도 쓴다.
    /// 중간 보스방과 보스방에 들어가면 짧은 등장 연출이 나온 뒤에 싸움이 시작된다.
    /// 보스는 풀어야 할 기믹이 있다(EnemyCitadel.cs, EnemySanctum.cs, EnemyClock.cs). 기믹의 풀이나 힌트는 화면에 띄우지 않는다.
    /// 한 판은 세 스테이지다. 3-6의 시계탑의 주인을 쓰러뜨리고 문으로 나가면 클리어 화면이 나오고 첫 거점으로 돌아간다.
    /// </summary>
    public sealed partial class PrototypeGame : MonoBehaviour
    {
        /// <summary>이 이름의 씬에서만 프로토타입이 자동으로 켜진다.</summary>
        public const string PrototypeSceneName = "SampleScene";

        public const int RoomsPerStage = 6;
        /// <summary>마지막 스테이지. 이 스테이지의 보스방을 나가면 한 판이 끝난다.</summary>
        public const int FinalStage = 3;

        private const float PopupLifetime = 0.8f;
        private const int HealOnStageClear = 2;
        private const int PuzzleRewardGold = 30;
        private const int HealPrice = 15;
        private const int UpgradePrice = 40;
        private const float PlateHalfWidth = 0.9f;
        private const int ShardsPerEnemy = 1;
        private const int ShardsPerMidBoss = 5;
        private const int ShardsPerBoss = 10;
        /// <summary>쌍둥이 수문장은 둘이라서 하나당 조금 적게 준다.</summary>
        private const int ShardsPerTwin = 3;
        /// <summary>한 판을 클리어하면 더 주는 그림자 조각.</summary>
        private const int ShardsPerClear = 20;
        private const float ResetConfirmWindow = 2f;
        /// <summary>그림자 조각으로 내는 값: 거점의 제단에서 강화 하나, 상점의 마지막 물건.</summary>
        private const int AltarPrice = 12;
        private const int ShopShardPrice = 8;
        /// <summary>보스 등장 연출의 길이(초)와 그 안의 순서. 실제 시간 기준이다.</summary>
        private const float IntroDuration = 2.4f;
        private const float IntroSkipAfter = 0.3f;
        private const float IntroTitleAt = 1f;
        private const float IntroReturnAt = 1.8f;
        private const float IntroPanSmoothing = 3.5f;
        private const float BossBarFillTime = 0.7f;

        private static readonly Color DoorLockedColor = new Color(0.45f, 0.2f, 0.2f, 1f);
        private static readonly Color DoorOpenColor = new Color(0.3f, 0.9f, 0.5f, 1f);
        private static readonly Color GoldColor = new Color(1f, 0.85f, 0.3f, 1f);
        private static readonly Color PlateIdleColor = new Color(0.2f, 0.4f, 0.5f, 1f);
        private static readonly Color PlatePressedColor = new Color(0.4f, 0.95f, 1f, 1f);
        private static readonly Color ShardColor = new Color(0.72f, 0.6f, 1f, 1f);
        private static readonly Color EventDoorLockedColor = new Color(0.3f, 0.3f, 0.36f, 1f);
        private static readonly Vector2 HubDoorSize = new Vector2(1.6f, 2.6f);
        private static readonly Vector2 AltarSize = new Vector2(1.2f, 1.2f);
        private static readonly Color DenyColor = new Color(1f, 0.4f, 0.4f, 1f);
        private static readonly Color BossVignetteColor = new Color(0f, 0f, 0f, 1f);
        private static readonly Color RageVignetteColor = new Color(0.45f, 0f, 0f, 1f);

        private static PrototypeGame _instance;

        private struct Popup
        {
            public Vector2 Position;
            public string Text;
            public Color Color;
            public float Age;
            /// <summary>떠 있는 시간(초).</summary>
            public float Life;
            /// <summary>공격 이름처럼 판에 담아 띄우는 글자인지.</summary>
            public bool Callout;
        }

        /// <summary>상점에 놓인 물건 하나.</summary>
        private sealed class ShopItem
        {
            public Vector2 Position;
            public string Title;
            public string Description;
            public int Price;
            /// <summary>true이면 골드가 아니라 그림자 조각으로 산다.</summary>
            public bool PaysShards;
            /// <summary>null이면 체력 회복 물약이다.</summary>
            public Upgrade Upgrade;
            public bool Sold;
            public SpriteRenderer Pedestal;
        }

        /// <summary>퍼즐방의 발판 하나.</summary>
        private sealed class Plate
        {
            public Vector2 Position;
            public SpriteRenderer Renderer;
            public bool Pressed;
        }

        private readonly List<Enemy> _enemies = new List<Enemy>();
        private readonly List<Popup> _popups = new List<Popup>();
        private readonly List<ShopItem> _shopItems = new List<ShopItem>();
        private readonly List<Plate> _plates = new List<Plate>();
        private GameObject _root;
        private StageLayout _layout;
        private PlayerController _player;
        private SpriteRenderer _door;
        /// <summary>지금 방의 보스들. 쌍둥이 수문장은 둘이다.</summary>
        private readonly List<Enemy> _bosses = new List<Enemy>();
        /// <summary>보스 기믹의 힌트. 실패할수록 구체적으로 바뀐다.</summary>
        private ShopItem _nearItem;
        private DifficultyLevel _difficulty;
        private int _nearDifficultyDoor = -1;
        private int _pendingDifficulty = -1;
        /// <summary>테스트용 건너뛰기. 0은 다음 스테이지 거점, 3은 중간 보스방, 6은 보스방. -1이면 없음.</summary>
        private int _testJump = -1;
        private bool _nearEventDoor;
        private bool _nearAltar;
        private bool _altarUsed;
        private SpriteRenderer _altar;
        private List<Upgrade> _altarChoices;
        private string _lastRunSummary;
        private int _shards;
        private int _runShards;
        private float _resetArmedUntil;
        private Camera _camera;
        private CameraFollow _follow;
        private float _followSmoothing;
        private bool _introActive;
        private bool _introTitleShown;
        private bool _introReturned;
        private float _introAge;
        private int _introEndFrame = -1;
        private string _introTitle;
        private string _introSubtitle;
        private Color _introColor;
        private bool _bossEnraged;
        private float _bossBarFill;
        private List<Upgrade> _choices;
        private int _stage;
        private int _room;
        private int _gold;
        private int _pincers;
        private int _kills;
        private bool _puzzleSolved;
        private bool _roomCleared;
        private bool _restartRequested;
        /// <summary>한 판을 클리어해서 클리어 화면을 보여 주는 중인지.</summary>
        private bool _runCleared;
        private float _runStartTime;
        private float _clearSeconds;
        private float _clearInputAt;
        private int _clearBonus;
        private bool _clearNewBest;

        /// <summary>이번 판의 수치. 강화를 고르면 바뀌고, 죽으면 처음으로 돌아간다.</summary>
        public RunStats Stats { get; private set; }

        /// <summary>타격감과 연출 담당.</summary>
        public GameFeel Feel { get; private set; }

        /// <summary>지금 방의 물건들이 달려 있는 곳. 방이 바뀌면 함께 사라진다.</summary>
        public Transform RoomRoot => _root != null ? _root.transform : null;

        /// <summary>지금 켜져 있는 게임. 유리 발판이나 봉인석처럼 게임을 직접 받지 못한 물건이 쓴다.</summary>
        public static PrototypeGame Current => _instance;

        /// <summary>지금 방의 플레이어. 바닥 가시처럼 게임을 직접 받지 못한 물건이 쓴다.</summary>
        public PlayerController Player => _player;

        /// <summary>지금 방의 가로 길이.</summary>
        public float RoomWidth => _layout != null ? _layout.Width : 0f;

        /// <summary>지금 방의 보스 기믹 자리. 1스테이지 보스방의 봉인석 자리 같은 것이다.</summary>
        public IList<Vector2> GimmickPoints => _layout != null ? _layout.GimmickPoints : null;

        /// <summary>
        /// 강화를 고르는 동안, 보스 등장 연출 동안, Esc 설정 화면이 열려 있는 동안에는 게임이 멈춘다.
        /// 연출을 건너뛰거나 메뉴를 닫은 키가 점프나 공격으로 이어지지 않도록, 끝난 프레임까지 멈춘 것으로 친다.
        /// </summary>
        public bool IsPaused => _choices != null || _introActive || Time.frameCount == _introEndFrame || _runCleared
            || _menu != MenuScreen.None || Time.frameCount == _menuClosedFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (_instance != null) return;
            if (SceneManager.GetActiveScene().name != PrototypeSceneName) return;

            Time.timeScale = 1f;
            var go = new GameObject("PrototypeGame");
            _instance = go.AddComponent<PrototypeGame>();
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            MetaProgress.Save();
        }

        private void Start()
        {
            Sfx.Attach(gameObject);
            Feel = gameObject.AddComponent<GameFeel>();
            Feel.Init(this);
            StartRun();
        }

        private void Update()
        {
            // Esc 설정 화면이 열려 있으면 입력은 메뉴가 받는다.
            if (UpdateMenu()) return;

            Keyboard keyboard = Keyboard.current;
            if (KeyBindings.Pressed(GameAction.Restart))
            {
                _restartRequested = true;
            }

            if (KeyBindings.Pressed(GameAction.Mute))
            {
                Sfx.Muted = !Sfx.Muted;
            }

            if (_restartRequested)
            {
                _restartRequested = false;
                StartRun();
                return;
            }

            if (_runCleared)
            {
                // 클리어 화면. 잘못 눌러 바로 넘어가지 않도록 1초 뒤부터 받는다.
                if (Time.unscaledTime >= _clearInputAt && ContinuePressed()) _restartRequested = true;
                return;
            }

            if (_introActive)
            {
                UpdateBossIntro();
                return;
            }
            if (_bosses.Count > 0 && _bossBarFill < 1f)
            {
                _bossBarFill = Mathf.Min(1f, _bossBarFill + Time.unscaledDeltaTime / BossBarFillTime);
            }

            if (_pendingDifficulty >= 0)
            {
                int index = _pendingDifficulty;
                _pendingDifficulty = -1;
                EnterDifficultyDoor(index);
                return;
            }

            if (_testJump >= 0)
            {
                int target = _testJump;
                _testJump = -1;
                TestJump(target);
                return;
            }

            if (_choices != null)
            {
                int picked = ReadChoiceInput();
                if (picked >= 0 && picked < _choices.Count)
                {
                    Choose(picked);
                }
                else if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                {
                    // 고르지 않고 닫는다. 그림자 조각은 쓰지 않는다.
                    _choices = null;
                    Time.timeScale = 1f;
                }
                return;
            }

            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                Popup popup = _popups[i];
                popup.Age += Time.deltaTime;
                if (popup.Age >= (popup.Life > 0f ? popup.Life : PopupLifetime)) _popups.RemoveAt(i);
                else _popups[i] = popup;
            }

            if (_player == null || _door == null || _roomCleared) return;

            UpdatePlates();
            UpdateShop();
            UpdateHub();

            bool doorOpen = IsDoorOpen();
            _door.color = doorOpen ? DoorOpenColor : DoorLockedColor;
            Feel.SetVignette(_bossEnraged ? RageVignetteColor : BossVignetteColor, _bosses.Count == 0 ? 0f : (_bossEnraged ? 0.75f : 0.55f));

            Vector2 toDoor = _player.Position - (Vector2)_door.transform.position;
            if (doorOpen && Mathf.Abs(toDoor.x) < 1.1f && Mathf.Abs(toDoor.y) < 2f)
            {
                _roomCleared = true;
                LeaveRoom();
            }
        }

        // ---- 판, 스테이지, 방 ----

        /// <summary>
        /// 새 판을 거점에서 시작한다. 판 안의 강화, 골드, 진행은 처음으로 돌아가고
        /// 그림자 조각과 고른 난이도만 남는다.
        /// </summary>
        private void StartRun()
        {
            // 방 번호 0은 거점이다. 첫 거점을 떠난 뒤라면 판을 하던 중에 죽었거나 다시 시작한 것이다.
            if (_runCleared)
            {
                _lastRunSummary = "지난 판: 클리어! " + _difficulty.Title + " 난이도, " + FormatTime(_clearSeconds) + ", " + MetaProgress.CurrencyName + " " + _runShards + "개를 모았다";
            }
            else if (_stage > 1 || _room >= 1)
            {
                string reached = _room >= 1 ? _stage + "-" + _room : _stage + "스테이지 거점";
                _lastRunSummary = "지난 판: " + reached + "까지 갔고 " + MetaProgress.CurrencyName + " " + _runShards + "개를 모았다";
            }

            MetaProgress.Save();
            _shards = MetaProgress.Shards;
            _runShards = 0;

            // 난이도는 첫 거점에서 문으로 들어갈 때 정해진다. 그때까지는 지난번에 고른 것을 들고 있는다.
            Stats = new RunStats();
            _difficulty = Difficulty.Selected;
            _pendingDifficulty = -1;
            _runCleared = false;
            _runStartTime = Time.time;
            _stage = 1;
            _room = 0;
            _gold = 0;
            _pincers = 0;
            _kills = 0;
            BuildRoom(Stats.MaxHealth);
        }

        /// <summary>방 번호에 따른 방 종류. 다섯 번째 방은 퍼즐방이거나 전투방이다.</summary>
        private static RoomType RoomTypeFor(int room)
        {
            switch (room)
            {
                case 0: return RoomType.Hub;
                case 3: return RoomType.MidBoss;
                case 4: return RoomType.Shop;
                case 5: return Random.value < 0.5f ? RoomType.Puzzle : RoomType.Mob;
                case 6: return RoomType.Boss;
                default: return RoomType.Mob;
            }
        }

        private static string RoomName(RoomType type)
        {
            switch (type)
            {
                case RoomType.MidBoss: return "중간 보스방";
                case RoomType.Shop: return "상점";
                case RoomType.Puzzle: return "퍼즐방";
                case RoomType.Boss: return "보스방";
                case RoomType.Hub: return "거점";
                default: return "전투방";
            }
        }

        private void BuildRoom(int playerHealth)
        {
            if (_root != null) Destroy(_root);
            _enemies.Clear();
            _popups.Clear();
            _shopItems.Clear();
            _plates.Clear();
            _choices = null;
            _bosses.Clear();
            _nearItem = null;
            _nearDifficultyDoor = -1;
            _nearEventDoor = false;
            _nearAltar = false;
            _altarUsed = false;
            _altar = null;
            _altarChoices = null;
            _puzzleSolved = false;
            _roomCleared = false;
            _introActive = false;
            _bossEnraged = false;
            _bossBarFill = 0f;
            Time.timeScale = 1f;
            Feel.CancelFreeze();
            Feel.SetVignette(BossVignetteColor, 0f);
            Sfx.StopBossMusic();

            _root = new GameObject("PrototypeRoom");
            Transform parent = _root.transform;
            bool withDummy = _stage == 1 && _room == 1;
            _layout = StageBuilder.Build(parent, RoomTypeFor(_room), _stage, withDummy);

            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(parent, false);
            playerGo.transform.position = FeetToCenter(_layout.PlayerSpawn, PlayerController.BodySize);
            _player = playerGo.AddComponent<PlayerController>();
            _player.Build(this, Stats, playerHealth);

            if (_layout.HasDummy)
            {
                // 협공을 연습할 수 있는 허수아비. 움직이지 않고 체력이 다시 찬다.
                Enemy dummy = CreateEnemy(EnemyKind.Dummy, _layout.DummyFeet);
                Physics2D.IgnoreCollision(dummy.Collider, _player.Collider);
            }

            for (int i = 0; i < _layout.Enemies.Count; i++)
            {
                EnemySpawn spawn = _layout.Enemies[i];
                AddEnemy(VaryKind(spawn.Kind), spawn.Feet);
            }
            AddFlyers();

            BuildShop(parent);
            BuildPlates(parent);

            Vector2 doorSize = new Vector2(1.6f, 2.6f);
            _door = PrototypeArt.CreateBox("ExitDoor", parent, FeetToCenter(_layout.ExitPosition, doorSize), doorSize, DoorLockedColor, 1);
            PixelSprites.Dress(_door, "Door", doorSize);

            SetUpCamera(playerGo.transform);
            BuildHub(parent);
            LinkTwinBosses();
            // 보스방은 등장 연출이 방 이름을 대신한다.
            ResetHudForRoom(_bosses.Count == 0);
            if (_bosses.Count > 0) BeginBossIntro();
        }

        // ---- 보스 등장 연출 ----

        /// <summary>
        /// 보스방에 들어왔다. 게임을 멈추고, 화면 위아래에 검은 띠를 치고, 카메라가 보스에게 갔다가 돌아온다.
        /// 끝나면 싸움과 보스전 음악이 시작된다. 점프, 공격, 상호작용 키로 건너뛸 수 있다.
        /// </summary>
        private void BeginBossIntro()
        {
            _introActive = true;
            _introTitleShown = false;
            _introReturned = false;
            _introAge = 0f;
            Enemy boss = _bosses[0];
            bool twins = _bosses.Count > 1;
            _introTitle = twins ? "쌍둥이 수문장" : boss.BossName;
            _introSubtitle = _stage + "-" + _room + (boss.IsFinalBoss ? (_stage >= FinalStage ? " 마지막 보스" : " 보스") : " 중간 보스");
            _introColor = boss.IsFinalBoss ? new Color(1f, 0.3f, 0.3f, 1f) : new Color(0.95f, 0.5f, 0.85f, 1f);
            if (boss.Kind == EnemyKind.LightLord) _introColor = new Color(1f, 0.9f, 0.55f, 1f);
            else if (boss.Kind == EnemyKind.ClockKnight) _introColor = new Color(0.6f, 0.85f, 1f, 1f);
            else if (boss.Kind == EnemyKind.ClockMaster) _introColor = new Color(1f, 0.7f, 0.35f, 1f);
            Time.timeScale = 0f;

            if (_follow != null)
            {
                _follow.Smoothing = IntroPanSmoothing;
                _follow.Target = boss.transform;
            }
            Feel.SetVignette(BossVignetteColor, 0.55f);
            Sfx.Play(Sound.BossIntro);
        }

        private void UpdateBossIntro()
        {
            _introAge += Time.unscaledDeltaTime;

            if (!_introTitleShown && _introAge >= IntroTitleAt)
            {
                // 이름이 뜨는 순간에 "쿵" 하고 화면이 흔들린다.
                _introTitleShown = true;
                Sfx.Play(Sound.BossSlam);
                Feel.Shake(0.35f, 0.4f);
            }

            if (!_introReturned && _introAge >= IntroReturnAt)
            {
                _introReturned = true;
                if (_follow != null && _player != null) _follow.Target = _player.transform;
            }

            if (_introAge >= IntroDuration || (_introAge >= IntroSkipAfter && IntroSkipPressed()))
            {
                EndBossIntro();
            }
        }

        private static bool IntroSkipPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
            {
                return true;
            }
            if (KeyBindings.Pressed(GameAction.Jump) || KeyBindings.Pressed(GameAction.Attack) || KeyBindings.Pressed(GameAction.Interact)) return true;

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;

            Gamepad gamepad = Gamepad.current;
            return gamepad != null && (gamepad.buttonSouth.wasPressedThisFrame || gamepad.buttonWest.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame);
        }

        private void EndBossIntro()
        {
            _introActive = false;
            _introEndFrame = Time.frameCount;
            Time.timeScale = 1f;

            if (_follow != null)
            {
                _follow.Smoothing = _followSmoothing;
                if (_player != null) _follow.Target = _player.transform;
            }
            // 건너뛰었을 때 이름이 뜨는 "쿵"이 빠지지 않게 한다.
            if (!_introTitleShown) Sfx.Play(Sound.BossSlam);
            Sfx.StartBossMusic();
        }

        /// <summary>보스가 다음 단계로 넘어가거나 기믹을 시작했다. 화면이 붉게 번쩍이고 음악이 빨라진다. 풀이는 알려 주지 않는다.</summary>
        public void OnBossPhase(Enemy boss, string text)
        {
            _bossEnraged = true;
            Vector2 position = boss.transform.position;
            ShowPopup(position + Vector2.up * (Enemy.SizeOf(boss.Kind).y * 0.5f + 1.5f), text, new Color(1f, 0.85f, 0.4f, 1f));
            Feel.BossEnraged(position);
        }

        /// <summary>보스의 기믹을 풀었다. 크게 알린다.</summary>
        public void OnGimmickSolved(Enemy boss, string text)
        {
            Vector2 position = boss.transform.position;
            ShowPopup(position + Vector2.up * (Enemy.SizeOf(boss.Kind).y * 0.5f + 1.5f), text, GoldColor);
            Sfx.Play(Sound.Solve);
            Feel.Flash(Color.white, 0.45f, 0.4f);
            Feel.Shake(0.35f, 0.3f);
            Feel.Sparks(position, GoldColor, 16, 8f);
        }

        /// <summary>플레이어가 분신과 자리를 바꿨다. 보스에게 알린다(백광의 군주의 표식이 옮겨 붙는다).</summary>
        public void OnPlayerSwapped()
        {
            for (int i = 0; i < _bosses.Count; i++)
            {
                if (_bosses[i] != null) _bosses[i].OnPlayerSwapped();
            }
        }

        /// <summary>쌍둥이 하나가 짝 덕분에 다시 일어났다.</summary>
        public void OnTwinRevived(Enemy twin)
        {
            ShowPopup((Vector2)twin.transform.position + Vector2.up * (Enemy.SizeOf(twin.Kind).y * 0.5f + 1.2f), "다시 일어났다!", new Color(1f, 0.4f, 0.35f, 1f));
            Sfx.Play(Sound.BossRage);
            Feel.Shake(0.3f, 0.3f);
        }

        /// <summary>쌍둥이 수문장 둘을 서로의 짝으로 묶는다.</summary>
        private void LinkTwinBosses()
        {
            Enemy sun = null;
            Enemy moon = null;
            for (int i = 0; i < _bosses.Count; i++)
            {
                if (_bosses[i].Kind == EnemyKind.TwinSun) sun = _bosses[i];
                else if (_bosses[i].Kind == EnemyKind.TwinMoon) moon = _bosses[i];
            }
            if (sun != null && moon != null) Enemy.LinkTwins(sun, moon);
        }

        /// <summary>적 하나를 만들어 방에 넣는다. 적은 플레이어나 다른 적과 몸으로 부딪히지 않는다.</summary>
        private void AddEnemy(EnemyKind kind, Vector2 feet)
        {
            Enemy enemy = CreateEnemy(kind, feet);
            Physics2D.IgnoreCollision(enemy.Collider, _player.Collider);
            for (int j = 0; j < _enemies.Count; j++)
            {
                Physics2D.IgnoreCollision(enemy.Collider, _enemies[j].Collider);
            }
            _enemies.Add(enemy);
            if (enemy.IsBoss) _bosses.Add(enemy);
        }

        /// <summary>전투방에서 기본 적의 일부를 방패병이나 폭탄병으로 바꾼다. 맨 첫 방은 기본 적만 나온다.</summary>
        private EnemyKind VaryKind(EnemyKind kind)
        {
            if (_layout.Type != RoomType.Mob || (_stage == 1 && _room == 1)) return kind;

            if (kind == EnemyKind.Brute && Random.value < 0.35f) return EnemyKind.Shield;
            if (kind == EnemyKind.Shooter && Random.value < 0.4f) return EnemyKind.Bomber;
            return kind;
        }

        /// <summary>전투방에 날아다니는 적을 몇 마리 더한다. 스테이지가 오를수록 늘어난다.</summary>
        private void AddFlyers()
        {
            if (_layout.Type != RoomType.Mob || (_stage == 1 && _room == 1)) return;

            int count = Mathf.Min(_stage, 3);
            Vector2 size = Enemy.SizeOf(EnemyKind.Flyer);
            for (int i = 0; i < count; i++)
            {
                float x = Random.Range(14f, Mathf.Max(15f, _layout.Width - 12f));
                AddEnemy(EnemyKind.Flyer, new Vector2(x, StageBuilder.GroundTop + 4.5f - size.y * 0.5f));
            }
        }

        /// <summary>
        /// 적의 체력(보통 난이도 기준). 스테이지가 오를수록 늘어난다.
        /// 플레이어는 한 번에 10, 분신은 5, 협공은 두 배를 준다.
        /// </summary>
        private float HealthFor(EnemyKind kind)
        {
            int extra = _stage - 1;
            switch (kind)
            {
                case EnemyKind.Dummy: return 100f;
                case EnemyKind.MidBoss: return 320f + 120f * extra;
                case EnemyKind.Boss: return 520f + 200f * extra;
                // 2스테이지부터 나오는 보스들. 3스테이지 이후에는 같은 보스가 더 단단해진다.
                case EnemyKind.TwinSun:
                case EnemyKind.TwinMoon: return 280f + 100f * Mathf.Max(0, extra - 1);
                case EnemyKind.LightLord: return 1000f + 300f * Mathf.Max(0, extra - 1);
                // 3스테이지 보스들. 마지막 스테이지라 더 강해지지 않는다.
                case EnemyKind.ClockKnight: return 700f;
                case EnemyKind.ClockMaster: return 1500f;
                case EnemyKind.Shield: return 40f + 12f * extra;
                case EnemyKind.Flyer: return 20f + 6f * extra;
                default: return 30f + 10f * extra;
            }
        }

        private Enemy CreateEnemy(EnemyKind kind, Vector2 feet)
        {
            var go = new GameObject(kind.ToString());
            go.transform.SetParent(_root.transform, false);
            go.transform.position = FeetToCenter(feet, Enemy.SizeOf(kind));
            var enemy = go.AddComponent<Enemy>();
            // 연습용 허수아비는 난이도와 상관없다.
            bool scaled = kind != EnemyKind.Dummy;
            enemy.MaxHealth = HealthFor(kind) * (scaled ? _difficulty.EnemyHealthScale : 1f);
            enemy.MoveSpeed = Random.Range(2.2f, 3.2f) * (scaled ? _difficulty.EnemySpeedScale : 1f);
            enemy.Build(this, _player, kind);
            return enemy;
        }

        private static Vector3 FeetToCenter(Vector2 feet, Vector2 size)
        {
            return new Vector3(feet.x, feet.y + size.y * 0.5f + 0.05f, 0f);
        }

        private void SetUpCamera(Transform target)
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var cameraGo = new GameObject("Main Camera");
                cameraGo.tag = "MainCamera";
                cameraGo.transform.position = new Vector3(0f, 0f, -10f);
                _camera = cameraGo.AddComponent<Camera>();
                // 소리를 듣는 귀. 씬에 원래 있던 카메라에는 이미 달려 있다.
                cameraGo.AddComponent<AudioListener>();
            }

            _camera.orthographic = true;
            _camera.orthographicSize = 6.5f;
            _camera.backgroundColor = StageBuilder.BackgroundFor(_stage);

            var follow = _camera.GetComponent<CameraFollow>();
            if (follow == null) follow = _camera.gameObject.AddComponent<CameraFollow>();
            if (_follow != follow)
            {
                _follow = follow;
                _followSmoothing = follow.Smoothing;
            }
            // 보스 등장 연출 도중에 방이 바뀌었으면 느려져 있던 카메라를 되돌린다.
            follow.Smoothing = _followSmoothing;
            Feel.SetCamera(follow);
            float halfViewWidth = _camera.orthographicSize * _camera.aspect;
            float minX = halfViewWidth - 1f;
            float maxX = _layout.Width + 1f - halfViewWidth;
            if (minX > maxX)
            {
                minX = _layout.Width * 0.5f;
                maxX = minX;
            }
            follow.MinX = minX;
            follow.MaxX = maxX;
            follow.Target = target;
            follow.SnapToTarget();
        }

        private bool IsDoorOpen()
        {
            // 첫 거점에는 닿아서 나가는 문이 없다.
            if (!_layout.HasExit) return false;
            // 거점, 상점, 퍼즐방은 언제든 나갈 수 있다. 나머지는 적을 모두 잡아야 열린다.
            if (_layout.Type == RoomType.Hub || _layout.Type == RoomType.Shop || _layout.Type == RoomType.Puzzle) return true;
            return _enemies.Count == 0;
        }

        private void LeaveRoom()
        {
            MetaProgress.Save();
            if (_room < RoomsPerStage)
            {
                _room++;
                BuildRoom(_player.Health);
                return;
            }

            // 마지막 스테이지의 보스방까지 깼다: 한 판 클리어.
            if (_stage >= FinalStage)
            {
                ClearRun();
                return;
            }

            // 보스방까지 깼다: 스테이지 클리어. 체력을 조금 회복하고 다음 스테이지의 거점으로 간다.
            // 강화는 그 거점의 제단에서 하나 고른다.
            _player.Heal(HealOnStageClear);
            int health = _player.Health;
            _stage++;
            _room = 0;
            BuildRoom(health);
        }

        /// <summary>한 판을 클리어했다. 기록을 남기고 클리어 화면을 띄운다.</summary>
        private void ClearRun()
        {
            _clearSeconds = Time.time - _runStartTime;
            _clearBonus = Mathf.CeilToInt(ShardsPerClear * _difficulty.ShardScale);
            _shards += _clearBonus;
            _runShards += _clearBonus;
            MetaProgress.Shards = _shards;
            _clearNewBest = MetaProgress.RecordClear(_clearSeconds);
            MetaProgress.Save();

            _runCleared = true;
            _clearInputAt = Time.unscaledTime + 1f;
            Feel.CancelFreeze();
            Feel.SetVignette(BossVignetteColor, 0f);
            Time.timeScale = 0f;
            Sfx.StopBossMusic();
            Sfx.Play(Sound.Clear);
        }

        private static bool ContinuePressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)) return true;
            if (KeyBindings.Pressed(GameAction.Interact)) return true;

            Gamepad gamepad = Gamepad.current;
            return gamepad != null && (gamepad.buttonSouth.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame);
        }

        /// <summary>초를 "12분 05초"처럼 쓴다.</summary>
        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return total / 60 + "분 " + (total % 60).ToString("00") + "초";
        }

        // ---- 강화 선택 ----

        private static int ReadChoiceInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) return 0;
                if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) return 1;
                if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) return 2;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                if (gamepad.dpad.left.wasPressedThisFrame) return 0;
                if (gamepad.dpad.up.wasPressedThisFrame) return 1;
                if (gamepad.dpad.right.wasPressedThisFrame) return 2;
            }
            return -1;
        }

        private void Choose(int index)
        {
            Upgrade upgrade = _choices[index];
            _choices = null;
            Time.timeScale = 1f;

            // 제단에서 고른 강화: 그림자 조각을 내고, 이 거점에서는 더 고를 수 없다.
            SpendShards(AltarPrice);
            _altarUsed = true;
            if (_altar != null) _altar.color = new Color(0.25f, 0.25f, 0.3f, 1f);
            if (_player != null)
            {
                upgrade.Apply(Stats, _player);
                ShowPopup(_player.Position + Vector2.up * 2.2f, upgrade.Title, new Color(0.6f, 1f, 0.7f, 1f));
            }
            Sfx.Play(Sound.Buy);
        }

        // ---- 상점 ----

        private void BuildShop(Transform parent)
        {
            if (_layout.Type != RoomType.Shop || _layout.ShopSlots.Count == 0) return;

            // 첫 자리는 항상 체력 회복, 나머지는 무작위 강화. 마지막 강화는 그림자 조각으로 산다.
            List<Upgrade> upgrades = UpgradePool.Pick(Stats, _layout.ShopSlots.Count - 1);
            for (int i = 0; i < _layout.ShopSlots.Count; i++)
            {
                var item = new ShopItem();
                item.Position = _layout.ShopSlots[i];
                if (i == 0)
                {
                    item.Title = "회복 물약";
                    item.Description = "체력을 2 회복한다.";
                    item.Price = HealPrice;
                }
                else if (i - 1 < upgrades.Count)
                {
                    item.Upgrade = upgrades[i - 1];
                    item.Title = item.Upgrade.Title;
                    item.Description = item.Upgrade.Description;
                    item.PaysShards = i == _layout.ShopSlots.Count - 1;
                    item.Price = item.PaysShards ? ShopShardPrice : UpgradePrice;
                }
                else
                {
                    continue;
                }

                Vector2 size = new Vector2(1f, 1f);
                Color color = item.Upgrade == null ? new Color(0.9f, 0.35f, 0.4f, 1f) : new Color(0.5f, 0.75f, 1f, 1f);
                item.Pedestal = PrototypeArt.CreateBox("ShopItem", parent, FeetToCenter(item.Position, size), size, color, 2);
                PixelSprites.Dress(item.Pedestal, item.Upgrade == null ? "Potion" : "Crystal", size);
                _shopItems.Add(item);
            }
        }

        private void SpendShards(int amount)
        {
            _shards = Mathf.Max(0, _shards - amount);
            MetaProgress.Shards = _shards;
            MetaProgress.Save();
        }

        private void UpdateShop()
        {
            _nearItem = null;
            for (int i = 0; i < _shopItems.Count; i++)
            {
                ShopItem item = _shopItems[i];
                if (!item.Sold && Mathf.Abs(_player.Position.x - item.Position.x) < 1.2f && Mathf.Abs(_player.Feet.y - item.Position.y) < 1.5f)
                {
                    _nearItem = item;
                }
            }
        }

        /// <summary>플레이어가 상호작용 키를 눌렀다. 상점 물건을 사거나 거점의 문을 쓴다.</summary>
        public void OnInteract()
        {
            if (_player == null) return;

            if (_nearDifficultyDoor >= 0)
            {
                // 플레이어의 Update 안에서 불리므로, 방을 다시 만드는 일은 다음 Update로 미룬다.
                _pendingDifficulty = _nearDifficultyDoor;
                return;
            }
            if (_nearEventDoor)
            {
                ShowPopup(_player.Position + Vector2.up * 1.6f, "아직 열리지 않았다", Color.white);
                Sfx.Play(Sound.Deny);
                return;
            }
            if (_nearAltar)
            {
                UseAltar();
                return;
            }
            if (_nearItem == null || _nearItem.Sold) return;

            Vector2 above = _player.Position + Vector2.up * 1.6f;
            if ((_nearItem.PaysShards ? _shards : _gold) < _nearItem.Price)
            {
                ShowPopup(above, (_nearItem.PaysShards ? MetaProgress.CurrencyName + "이" : "골드가") + " 부족하다", DenyColor);
                Sfx.Play(Sound.Deny);
                return;
            }

            if (_nearItem.PaysShards) SpendShards(_nearItem.Price);
            else _gold -= _nearItem.Price;
            _nearItem.Sold = true;
            _nearItem.Pedestal.color = new Color(0.25f, 0.25f, 0.3f, 1f);
            if (_nearItem.Upgrade == null) _player.Heal(2);
            else _nearItem.Upgrade.Apply(Stats, _player);
            ShowPopup(above, _nearItem.Title + " 구매", new Color(0.6f, 1f, 0.7f, 1f));
            Sfx.Play(Sound.Buy);
            _nearItem = null;
        }

        // ---- 거점 ----

        private void BuildHub(Transform parent)
        {
            if (_layout.Type != RoomType.Hub) return;

            // 첫 거점에는 닿아서 나가는 문이 없다. 난이도 문으로 들어가야 판이 시작된다.
            if (!_layout.HasExit) _door.enabled = false;

            for (int i = 0; i < _layout.DifficultyDoors.Count && i < Difficulty.Levels.Length; i++)
            {
                SpriteRenderer door = PrototypeArt.CreateBox("DifficultyDoor", parent, FeetToCenter(_layout.DifficultyDoors[i], HubDoorSize), HubDoorSize, Difficulty.Levels[i].DoorColor, 1);
                PixelSprites.Dress(door, "Door", HubDoorSize);
            }

            if (_layout.HasEventDoor)
            {
                // 이벤트나 DLC가 생기면 여는 문. 지금은 잠겨 있다.
                SpriteRenderer eventDoor = PrototypeArt.CreateBox("EventDoor", parent, FeetToCenter(_layout.EventDoor, HubDoorSize), HubDoorSize, EventDoorLockedColor, 1);
                PixelSprites.Dress(eventDoor, "Door", HubDoorSize);
            }

            if (_layout.HasAltar)
            {
                // 고를 강화 셋은 거점에 들어올 때 정해 둔다. 닫았다 다시 열어도 바뀌지 않는다.
                _altarChoices = UpgradePool.Pick(Stats, 3);
                _altar = PrototypeArt.CreateBox("Altar", parent, FeetToCenter(_layout.Altar, AltarSize), AltarSize, ShardColor, 2);
                PixelSprites.Dress(_altar, "Altar", AltarSize);
            }
        }

        private bool IsNear(Vector2 feet)
        {
            return Mathf.Abs(_player.Position.x - feet.x) < 1.2f && Mathf.Abs(_player.Feet.y - feet.y) < 1.5f;
        }

        private void UpdateHub()
        {
            _nearDifficultyDoor = -1;
            _nearEventDoor = false;
            _nearAltar = false;
            if (_layout.Type != RoomType.Hub) return;

            for (int i = 0; i < _layout.DifficultyDoors.Count; i++)
            {
                if (IsNear(_layout.DifficultyDoors[i])) _nearDifficultyDoor = i;
            }
            _nearEventDoor = _layout.HasEventDoor && IsNear(_layout.EventDoor);
            _nearAltar = _layout.HasAltar && IsNear(_layout.Altar);

            // 테스트용 건너뛰기: 숫자 2는 다음 스테이지 거점, 3은 이 스테이지의 중간 보스방, 6은 보스방.
            // 키 설정에서 그 숫자를 게임 키로 쓰고 있으면 건너뛰지 않는다.
            Keyboard testKeys = Keyboard.current;
            if (testKeys != null)
            {
                if (testKeys.digit2Key.wasPressedThisFrame && !KeyBindings.IsBound(Key.Digit2)) _testJump = 0;
                else if (testKeys.digit3Key.wasPressedThisFrame && !KeyBindings.IsBound(Key.Digit3)) _testJump = 3;
                else if (testKeys.digit6Key.wasPressedThisFrame && !KeyBindings.IsBound(Key.Digit6)) _testJump = 6;
            }

            // 테스트용: 첫 거점에서 Delete(맥에서는 delete 키)를 두 번 누르면 저장을 지운다. 게임 키로 쓰고 있으면 무시한다.
            Keyboard keyboard = Keyboard.current;
            bool resetKey = keyboard != null
                && ((keyboard.deleteKey.wasPressedThisFrame && !KeyBindings.IsBound(Key.Delete))
                    || (keyboard.backspaceKey.wasPressedThisFrame && !KeyBindings.IsBound(Key.Backspace)));
            if (_layout.HasEventDoor && resetKey)
            {
                if (Time.unscaledTime <= _resetArmedUntil)
                {
                    _resetArmedUntil = 0f;
                    MetaProgress.ResetAll();
                    _lastRunSummary = "저장을 지웠다";
                    _restartRequested = true;
                }
                else
                {
                    _resetArmedUntil = Time.unscaledTime + ResetConfirmWindow;
                    ShowPopup(_player.Position + Vector2.up * 1.6f, "한 번 더 누르면 저장이 지워진다", new Color(1f, 0.4f, 0.4f, 1f));
                }
            }
        }

        /// <summary>난이도 문으로 들어갔다. 그 난이도로 1-1을 바로 시작한다.</summary>
        private void EnterDifficultyDoor(int index)
        {
            Difficulty.SelectedIndex = index;
            MetaProgress.Save();
            _difficulty = Difficulty.Selected;

            // 첫 거점에서는 아직 강화가 없으므로 최대 체력을 기본값에서 다시 계산한다.
            Stats.MaxHealth = new RunStats().MaxHealth + _difficulty.MaxHealthBonus;
            _room = 1;
            _runStartTime = Time.time;
            BuildRoom(Stats.MaxHealth);
        }

        /// <summary>테스트용으로 거점에서 바로 다른 방으로 간다. 보스 기믹을 여러 번 시험해 볼 때 쓴다.</summary>
        private void TestJump(int target)
        {
            if (target == 0 && _stage >= FinalStage)
            {
                ShowPopup(_player.Position + Vector2.up * 1.6f, "여기가 마지막 스테이지다", DenyColor);
                Sfx.Play(Sound.Deny);
                return;
            }

            // 첫 거점에서 건너뛰면 난이도 문으로 들어간 것처럼 최대 체력을 맞춘다.
            if (!_layout.HasExit)
            {
                Stats.MaxHealth = new RunStats().MaxHealth + _difficulty.MaxHealthBonus;
                _runStartTime = Time.time;
            }
            if (target == 0)
            {
                _stage++;
                _room = 0;
            }
            else
            {
                _room = target;
            }
            BuildRoom(Stats.MaxHealth);
        }

        /// <summary>강화의 제단 앞에서 상호작용했다. 그림자 조각이 충분하면 강화 셋 중 하나를 고르는 화면을 연다.</summary>
        private void UseAltar()
        {
            Vector2 above = _player.Position + Vector2.up * 1.6f;
            if (_altarUsed)
            {
                ShowPopup(above, "이 거점에서는 이미 강화를 골랐다", Color.white);
                Sfx.Play(Sound.Deny);
                return;
            }
            if (_altarChoices == null || _altarChoices.Count == 0)
            {
                ShowPopup(above, "더 고를 강화가 없다", Color.white);
                Sfx.Play(Sound.Deny);
                return;
            }
            if (_shards < AltarPrice)
            {
                ShowPopup(above, MetaProgress.CurrencyName + "이 부족하다", DenyColor);
                Sfx.Play(Sound.Deny);
                return;
            }

            _choices = _altarChoices;
            _choicesOpenedAt = Time.unscaledTime;
            Time.timeScale = 0f;
            Feel.CancelFreeze();
            Sfx.Play(Sound.Anchor);
        }

        // ---- 퍼즐 ----

        private void BuildPlates(Transform parent)
        {
            for (int i = 0; i < _layout.Plates.Count; i++)
            {
                var plate = new Plate();
                plate.Position = _layout.Plates[i];
                Vector2 size = new Vector2(PlateHalfWidth * 2f, 0.2f);
                plate.Renderer = PrototypeArt.CreateBox("Plate", parent, plate.Position + new Vector2(0f, 0.1f), size, PlateIdleColor, 2);
                _plates.Add(plate);
            }
        }

        /// <summary>발판은 플레이어나 분신이 올라서면 눌린다. 모든 발판이 동시에 눌리면 퍼즐이 풀린다.</summary>
        private void UpdatePlates()
        {
            if (_plates.Count == 0) return;

            bool allPressed = true;
            for (int i = 0; i < _plates.Count; i++)
            {
                Plate plate = _plates[i];
                Vector2 feet = _player.Feet;
                bool playerOn = Mathf.Abs(feet.x - plate.Position.x) <= PlateHalfWidth && Mathf.Abs(feet.y - plate.Position.y) <= 0.4f;
                bool shadowOn = _player.IsAnyShadowStandingAt(plate.Position, PlateHalfWidth, 0.4f);
                plate.Pressed = playerOn || shadowOn;
                plate.Renderer.color = plate.Pressed || _puzzleSolved ? PlatePressedColor : PlateIdleColor;
                if (!plate.Pressed) allPressed = false;
            }

            if (allPressed && !_puzzleSolved)
            {
                _puzzleSolved = true;
                _gold += PuzzleRewardGold;
                _player.Heal(1);
                ShowPopup(_player.Position + Vector2.up * 2.2f, "퍼즐 해결! 골드 +" + PuzzleRewardGold + ", 체력 +1", GoldColor);
                Sfx.Play(Sound.Solve);
            }
        }

        // ---- 적과 전투 ----

        public void OnEnemyDied(Enemy enemy)
        {
            if (!_enemies.Remove(enemy)) return;

            _kills++;
            _gold += enemy.GoldValue;
            ShowPopup((Vector2)enemy.transform.position + Vector2.up * 1.6f, "+" + enemy.GoldValue + " 골드", GoldColor);

            // 그림자 조각은 죽어도 남는다. 바로 저장 값에 더해 둔다.
            int baseShards = ShardsPerEnemy;
            if (enemy.IsFinalBoss) baseShards = ShardsPerBoss;
            else if (enemy.Kind == EnemyKind.TwinSun || enemy.Kind == EnemyKind.TwinMoon) baseShards = ShardsPerTwin;
            else if (enemy.IsBoss) baseShards = ShardsPerMidBoss;
            int shards = Mathf.CeilToInt(baseShards * _difficulty.ShardScale);
            _shards += shards;
            _runShards += shards;
            MetaProgress.Shards = _shards;
            ShowPopup((Vector2)enemy.transform.position + Vector2.up * 2.2f, "+" + shards + " " + MetaProgress.CurrencyName, ShardColor);
            if (_bosses.Remove(enemy) && _bosses.Count == 0)
            {
                _bossEnraged = false;
            }

            if (enemy.IsBoss) Feel.BossDied(enemy.transform.position, enemy.BaseColor);
            else Feel.EnemyDied(enemy.transform.position, enemy.BaseColor);

            if (_enemies.Count == 0 && _player != null)
            {
                ShowPopup(_player.Position + Vector2.up * 2.2f, "문이 열렸다", DoorOpenColor);
                // 보스를 잡았을 때는 처치 소리가 그 자리를 맡는다.
                if (!enemy.IsBoss) Sfx.Play(Sound.DoorOpen);
            }
        }

        public void CountPincer()
        {
            _pincers++;
        }

        public void RequestRestart()
        {
            _restartRequested = true;
        }

        public void SpawnProjectile(Vector2 origin, Vector2 velocity, float size)
        {
            if (_root == null) return;
            SpriteRenderer renderer = PrototypeArt.CreateBox("Projectile", _root.transform, origin, new Vector2(size, size), new Color(1f, 0.8f, 0.2f, 1f), 15);
            PixelSprites.Dress(renderer, "Orb", new Vector2(size, size));
            renderer.gameObject.AddComponent<Projectile>().Launch(_player, velocity);
            Sfx.Play(Sound.Shoot, 0.7f);
        }

        /// <summary>
        /// 보스가 쏘는 구슬. 소리는 내지 않으니 부르는 쪽에서 한 번만 낸다.
        /// 돌려받은 탄에 되돌아오기(SetAcceleration)나 멈췄다 날아가기(HoldThenAim)를 정할 수 있다. 방이 없으면 null.
        /// </summary>
        public Projectile SpawnShot(Vector2 origin, Vector2 velocity, float size, Color color)
        {
            if (_root == null) return null;
            var shotSize = new Vector2(size, size);
            SpriteRenderer renderer = PrototypeArt.CreateBox("Shot", _root.transform, origin, shotSize, color, 15);
            PixelSprites.Dress(renderer, "Orb", shotSize);
            Projectile projectile = renderer.gameObject.AddComponent<Projectile>();
            projectile.Launch(_player, velocity);
            return projectile;
        }

        /// <summary>바닥을 따라 굴러오는 톱니바퀴. 탄처럼 벽에 닿으면 사라지고 대시로 지나갈 수 있다.</summary>
        public void SpawnGear(Vector2 origin, Vector2 velocity, float size)
        {
            if (_root == null) return;
            var gearSize = new Vector2(size, size);
            SpriteRenderer renderer = PrototypeArt.CreateBox("Gear", _root.transform, origin, gearSize, new Color(0.95f, 0.68f, 0.32f, 1f), 15);
            PixelSprites.Dress(renderer, "Gear", gearSize);
            renderer.gameObject.AddComponent<Projectile>().Launch(_player, velocity);
            renderer.gameObject.AddComponent<GearSpin>().Speed = velocity.x > 0f ? -540f : 540f;
            Sfx.Play(Sound.Shoot, 0.6f);
        }

        public void SpawnBomb(Vector2 origin, Vector2 target, float flightTime, float radius)
        {
            if (_root == null) return;
            // 떨어질 자리에 붉은 원을 그리고, 폭탄이 닿을 때까지 고리가 좁혀 온다.
            GameObject marker = LandingCircle.Spawn(_root.transform, target, radius, flightTime, new Color(1f, 0.32f, 0.24f, 1f));
            // 그림에 명암이 있으므로 사각형일 때보다 밝은 색을 입힌다.
            var bombSize = new Vector2(0.5f, 0.5f);
            SpriteRenderer renderer = PrototypeArt.CreateBox("Bomb", _root.transform, origin, bombSize, new Color(0.5f, 0.5f, 0.56f, 1f), 15);
            PixelSprites.Dress(renderer, "Bomb", bombSize);
            renderer.gameObject.AddComponent<Bomb>().Launch(this, _player, marker, origin, target, flightTime, radius);
            Sfx.Play(Sound.Shoot, 0.7f);
        }

        // ---- 화면 표시 ----

        public void ShowPopup(Vector2 position, string text, Color color)
        {
            _popups.Add(new Popup { Position = position, Text = text, Color = color, Age = 0f, Life = PopupLifetime });
        }

        /// <summary>충격파 같은 범위 공격을 잠깐 보여 준다. 번쩍인 원 위로 고리가 퍼져 나간다.</summary>
        public void ShowBlast(Vector2 center, float radius, Color color)
        {
            if (_root == null) return;
            BlastRing.Spawn(_root.transform, center, radius, color);
        }
    }
}

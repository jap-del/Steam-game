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
    /// </summary>
    public sealed class PrototypeGame : MonoBehaviour
    {
        /// <summary>이 이름의 씬에서만 프로토타입이 자동으로 켜진다.</summary>
        public const string PrototypeSceneName = "SampleScene";

        public const int RoomsPerStage = 6;

        private const float PopupLifetime = 0.8f;
        private const int HealOnStageClear = 2;
        private const int PuzzleRewardGold = 30;
        private const int HealPrice = 15;
        private const int UpgradePrice = 40;
        private const float PlateHalfWidth = 0.9f;
        private const int ShardsPerEnemy = 1;
        private const int ShardsPerMidBoss = 5;
        private const int ShardsPerBoss = 10;
        private const float ResetConfirmWindow = 2f;
        /// <summary>그림자 조각으로 내는 값: 거점의 제단에서 강화 하나, 상점의 마지막 물건.</summary>
        private const int AltarPrice = 12;
        private const int ShopShardPrice = 8;

        private static readonly Color DoorLockedColor = new Color(0.45f, 0.2f, 0.2f, 1f);
        private static readonly Color DoorOpenColor = new Color(0.3f, 0.9f, 0.5f, 1f);
        private static readonly Color GoldColor = new Color(1f, 0.85f, 0.3f, 1f);
        private static readonly Color PlateIdleColor = new Color(0.2f, 0.4f, 0.5f, 1f);
        private static readonly Color PlatePressedColor = new Color(0.4f, 0.95f, 1f, 1f);
        private static readonly Color ShardColor = new Color(0.72f, 0.6f, 1f, 1f);
        private static readonly Color EventDoorLockedColor = new Color(0.3f, 0.3f, 0.36f, 1f);
        private static readonly Vector2 HubDoorSize = new Vector2(1.6f, 2.6f);
        private static readonly Vector2 AltarSize = new Vector2(1.2f, 1.2f);

        private static PrototypeGame _instance;

        private struct Popup
        {
            public Vector2 Position;
            public string Text;
            public Color Color;
            public float Age;
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
        private Enemy _boss;
        private ShopItem _nearItem;
        private DifficultyLevel _difficulty;
        private int _nearDifficultyDoor = -1;
        private int _pendingDifficulty = -1;
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
        private GUIStyle _hudStyle;
        private GUIStyle _popupStyle;
        private GUIStyle _cardTitleStyle;
        private GUIStyle _cardBodyStyle;
        private List<Upgrade> _choices;
        private int _stage;
        private int _room;
        private int _gold;
        private int _pincers;
        private int _kills;
        private bool _puzzleSolved;
        private bool _roomCleared;
        private bool _restartRequested;

        /// <summary>이번 판의 수치. 강화를 고르면 바뀌고, 죽으면 처음으로 돌아간다.</summary>
        public RunStats Stats { get; private set; }

        /// <summary>강화를 고르는 동안에는 게임이 멈춘다.</summary>
        public bool IsPaused => _choices != null;

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
            StartRun();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                _restartRequested = true;
            }

            if (_restartRequested)
            {
                _restartRequested = false;
                StartRun();
                return;
            }

            if (_pendingDifficulty >= 0)
            {
                int index = _pendingDifficulty;
                _pendingDifficulty = -1;
                EnterDifficultyDoor(index);
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
                if (popup.Age >= PopupLifetime) _popups.RemoveAt(i);
                else _popups[i] = popup;
            }

            if (_player == null || _door == null || _roomCleared) return;

            UpdatePlates();
            UpdateShop();
            UpdateHub();

            bool doorOpen = IsDoorOpen();
            _door.color = doorOpen ? DoorOpenColor : DoorLockedColor;

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
            if (_stage > 1 || _room >= 1)
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

        /// <summary>화면에 보여 줄 방 이름. 거점은 "2스테이지 거점"처럼 적는다.</summary>
        private string RoomLabel()
        {
            if (_layout.Type == RoomType.Hub) return _stage + "스테이지 " + RoomName(RoomType.Hub);
            return _stage + "-" + _room + " " + RoomName(_layout.Type);
        }

        private void BuildRoom(int playerHealth)
        {
            if (_root != null) Destroy(_root);
            _enemies.Clear();
            _popups.Clear();
            _shopItems.Clear();
            _plates.Clear();
            _choices = null;
            _boss = null;
            _nearItem = null;
            _nearDifficultyDoor = -1;
            _nearEventDoor = false;
            _nearAltar = false;
            _altarUsed = false;
            _altar = null;
            _altarChoices = null;
            _puzzleSolved = false;
            _roomCleared = false;
            Time.timeScale = 1f;

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
            ShowPopup(_player.Position + Vector2.up * 2.2f, RoomLabel(), Color.white);
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
            if (enemy.IsBoss) _boss = enemy;
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

        private float HealthFor(EnemyKind kind)
        {
            int extra = _stage - 1;
            switch (kind)
            {
                case EnemyKind.Dummy: return 100f;
                case EnemyKind.MidBoss: return 160f + 70f * extra;
                case EnemyKind.Boss: return 260f + 120f * extra;
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
            }

            _camera.orthographic = true;
            _camera.orthographicSize = 6.5f;
            _camera.backgroundColor = new Color(0.09f, 0.1f, 0.14f, 1f);

            var follow = _camera.GetComponent<CameraFollow>();
            if (follow == null) follow = _camera.gameObject.AddComponent<CameraFollow>();
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

            // 보스방까지 깼다: 스테이지 클리어. 체력을 조금 회복하고 다음 스테이지의 거점으로 간다.
            // 강화는 그 거점의 제단에서 하나 고른다.
            _player.Heal(HealOnStageClear);
            int health = _player.Health;
            _stage++;
            _room = 0;
            BuildRoom(health);
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
                ShowPopup(above, (_nearItem.PaysShards ? MetaProgress.CurrencyName + "이" : "골드가") + " 부족하다", new Color(1f, 0.4f, 0.4f, 1f));
                return;
            }

            if (_nearItem.PaysShards) SpendShards(_nearItem.Price);
            else _gold -= _nearItem.Price;
            _nearItem.Sold = true;
            _nearItem.Pedestal.color = new Color(0.25f, 0.25f, 0.3f, 1f);
            if (_nearItem.Upgrade == null) _player.Heal(2);
            else _nearItem.Upgrade.Apply(Stats, _player);
            ShowPopup(above, _nearItem.Title + " 구매", new Color(0.6f, 1f, 0.7f, 1f));
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
                PrototypeArt.CreateBox("DifficultyDoor", parent, FeetToCenter(_layout.DifficultyDoors[i], HubDoorSize), HubDoorSize, Difficulty.Levels[i].DoorColor, 1);
            }

            if (_layout.HasEventDoor)
            {
                // 이벤트나 DLC가 생기면 여는 문. 지금은 잠겨 있다.
                PrototypeArt.CreateBox("EventDoor", parent, FeetToCenter(_layout.EventDoor, HubDoorSize), HubDoorSize, EventDoorLockedColor, 1);
            }

            if (_layout.HasAltar)
            {
                // 고를 강화 셋은 거점에 들어올 때 정해 둔다. 닫았다 다시 열어도 바뀌지 않는다.
                _altarChoices = UpgradePool.Pick(Stats, 3);
                _altar = PrototypeArt.CreateBox("Altar", parent, FeetToCenter(_layout.Altar, AltarSize), AltarSize, ShardColor, 2);
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

            // 테스트용: 첫 거점에서 Delete(맥에서는 delete 키)를 두 번 누르면 저장을 지운다.
            Keyboard keyboard = Keyboard.current;
            if (_layout.HasEventDoor && keyboard != null && (keyboard.deleteKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame))
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
            BuildRoom(Stats.MaxHealth);
        }

        /// <summary>강화의 제단 앞에서 상호작용했다. 그림자 조각이 충분하면 강화 셋 중 하나를 고르는 화면을 연다.</summary>
        private void UseAltar()
        {
            Vector2 above = _player.Position + Vector2.up * 1.6f;
            if (_altarUsed)
            {
                ShowPopup(above, "이 거점에서는 이미 강화를 골랐다", Color.white);
                return;
            }
            if (_altarChoices == null || _altarChoices.Count == 0)
            {
                ShowPopup(above, "더 고를 강화가 없다", Color.white);
                return;
            }
            if (_shards < AltarPrice)
            {
                ShowPopup(above, MetaProgress.CurrencyName + "이 부족하다", new Color(1f, 0.4f, 0.4f, 1f));
                return;
            }

            _choices = _altarChoices;
            Time.timeScale = 0f;
        }

        private void DrawHubLabels()
        {
            if (_layout == null || _layout.Type != RoomType.Hub) return;

            Color hint = new Color(0.8f, 1f, 0.85f, 1f);
            for (int i = 0; i < _layout.DifficultyDoors.Count && i < Difficulty.Levels.Length; i++)
            {
                DifficultyLevel level = Difficulty.Levels[i];
                Vector2 aboveDoor = _layout.DifficultyDoors[i] + Vector2.up * 3.1f;
                DrawWorldLabel(aboveDoor, level.Title, level.DoorColor, 300f);
                if (i == _nearDifficultyDoor)
                {
                    DrawWorldLabel(aboveDoor + Vector2.up * 0.7f, level.Description + "  (E: 이 난이도로 시작)", hint, 1100f);
                }
            }

            if (_layout.HasEventDoor)
            {
                Vector2 aboveDoor = _layout.EventDoor + Vector2.up * 3.1f;
                DrawWorldLabel(aboveDoor + Vector2.up * 0.6f, "이벤트의 문", Color.white, 300f);
                DrawWorldLabel(aboveDoor, "잠겨 있다", new Color(1f, 1f, 1f, 0.5f), 300f);
            }

            if (_layout.HasAltar)
            {
                Vector2 aboveAltar = _layout.Altar + Vector2.up * 2.2f;
                DrawWorldLabel(aboveAltar + Vector2.up * 0.6f, "강화의 제단", Color.white, 300f);
                if (_altarUsed)
                {
                    DrawWorldLabel(aboveAltar, "사용함", new Color(1f, 1f, 1f, 0.5f), 300f);
                }
                else
                {
                    DrawWorldLabel(aboveAltar, MetaProgress.CurrencyName + " " + AltarPrice, _shards >= AltarPrice ? ShardColor : new Color(1f, 0.4f, 0.4f, 1f), 300f);
                    if (_nearAltar)
                    {
                        DrawWorldLabel(aboveAltar + Vector2.up * 1.3f, "E: 강화 셋 중 하나 고르기", hint, 600f);
                    }
                }
            }

            if (_layout.HasExit)
            {
                Vector2 aboveDoor = _layout.ExitPosition + Vector2.up * 3.1f;
                DrawWorldLabel(aboveDoor + Vector2.up * 0.6f, "출발의 문", Color.white, 300f);
                DrawWorldLabel(aboveDoor, "닿으면 " + _stage + "-1 시작", DoorOpenColor, 300f);
            }
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
            int baseShards = enemy.Kind == EnemyKind.Boss ? ShardsPerBoss : (enemy.Kind == EnemyKind.MidBoss ? ShardsPerMidBoss : ShardsPerEnemy);
            int shards = Mathf.CeilToInt(baseShards * _difficulty.ShardScale);
            _shards += shards;
            _runShards += shards;
            MetaProgress.Shards = _shards;
            ShowPopup((Vector2)enemy.transform.position + Vector2.up * 2.2f, "+" + shards + " " + MetaProgress.CurrencyName, ShardColor);
            if (enemy == _boss) _boss = null;

            if (_enemies.Count == 0 && _player != null)
            {
                ShowPopup(_player.Position + Vector2.up * 2.2f, "문이 열렸다", DoorOpenColor);
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
        }

        public void SpawnBomb(Vector2 origin, Vector2 target, float flightTime, float radius)
        {
            if (_root == null) return;
            // 떨어질 자리를 붉게 표시한다.
            SpriteRenderer marker = PrototypeArt.CreateBox("BombMarker", _root.transform, target, new Vector2(radius * 2f, radius * 2f), new Color(1f, 0.25f, 0.2f, 0.2f), 5);
            SpriteRenderer renderer = PrototypeArt.CreateBox("Bomb", _root.transform, origin, new Vector2(0.45f, 0.45f), new Color(0.2f, 0.2f, 0.22f, 1f), 15);
            renderer.gameObject.AddComponent<Bomb>().Launch(this, _player, marker.gameObject, origin, target, flightTime, radius);
        }

        // ---- 화면 표시 ----

        public void ShowPopup(Vector2 position, string text, Color color)
        {
            _popups.Add(new Popup { Position = position, Text = text, Color = color, Age = 0f });
        }

        /// <summary>충격파 같은 범위 공격을 잠깐 보여 준다.</summary>
        public void ShowBlast(Vector2 center, float radius, Color color)
        {
            if (_root == null) return;
            SpriteRenderer blast = PrototypeArt.CreateBox("Blast", _root.transform, center, new Vector2(radius * 2f, radius * 2f), color, 7);
            Destroy(blast.gameObject, 0.15f);
        }

        private string GoalText()
        {
            switch (_layout.Type)
            {
                case RoomType.Hub:
                    return _layout.HasExit
                        ? "강화의 제단 앞에서 E를 누르면 " + MetaProgress.CurrencyName + " " + AltarPrice + "개로 강화를 하나 고른다. 오른쪽 문으로 나가면 " + _stage + "-1 시작"
                        : "난이도의 문 앞에서 E를 누르면 그 난이도로 1-1을 시작한다. (delete 두 번: 저장 지우기)";
                case RoomType.Shop:
                    return "물건 앞에서 E로 구매. 오른쪽 문으로 나간다";
                case RoomType.Puzzle:
                    return _puzzleSolved
                        ? "퍼즐 해결. 오른쪽 문으로 나간다"
                        : "발판 두 개를 동시에 밟으면 보상. 한 발판에 잠깐 서 있다가 다른 발판으로 가면 분신이 대신 밟아 준다. S로 분신을 3초간 고정해도 된다";
                default:
                    return _enemies.Count > 0 ? "남은 적 " + _enemies.Count : "문이 열렸다. 오른쪽 끝의 초록 문으로 가자";
            }
        }

        private void OnGUI()
        {
            if (_hudStyle == null)
            {
                _hudStyle = new GUIStyle(GUI.skin.label);
                _hudStyle.fontSize = 18;
                _hudStyle.normal.textColor = Color.white;

                _popupStyle = new GUIStyle(GUI.skin.label);
                _popupStyle.fontSize = 20;
                _popupStyle.fontStyle = FontStyle.Bold;
                _popupStyle.alignment = TextAnchor.MiddleCenter;

                _cardTitleStyle = new GUIStyle(GUI.skin.label);
                _cardTitleStyle.fontSize = 22;
                _cardTitleStyle.fontStyle = FontStyle.Bold;
                _cardTitleStyle.alignment = TextAnchor.UpperCenter;
                _cardTitleStyle.normal.textColor = Color.white;

                _cardBodyStyle = new GUIStyle(GUI.skin.label);
                _cardBodyStyle.fontSize = 16;
                _cardBodyStyle.wordWrap = true;
                _cardBodyStyle.alignment = TextAnchor.UpperCenter;
                _cardBodyStyle.normal.textColor = Color.white;
            }

            if (_player != null && _layout != null)
            {
                string shadowState = _player.ShadowFill < 1f
                    ? "분신 준비 중 " + Mathf.RoundToInt(_player.ShadowFill * 100f) + "%"
                    : (_player.SwapCooldownRemaining > 0f
                        ? "자리 바꾸기 " + _player.SwapCooldownRemaining.ToString("0.0") + "초"
                        : "자리 바꾸기 가능");
                if (_player.ShadowFill >= 1f)
                {
                    if (_player.AreShadowsAnchored) shadowState += "    분신 고정 중 " + _player.AnchorRemaining.ToString("0.0") + "초";
                    else if (_player.AnchorCooldownRemaining > 0f) shadowState += "    분신 고정 " + _player.AnchorCooldownRemaining.ToString("0.0") + "초";
                    else shadowState += "    분신 고정 가능";
                }
                string dashState = _player.DashCooldownRemaining > 0f
                    ? "대시 " + _player.DashCooldownRemaining.ToString("0.0") + "초"
                    : "대시 가능";

                GUI.color = Color.white;
                GUI.Label(new Rect(16f, 12f, 1400f, 28f), RoomLabel() + (_layout.HasExit ? "    난이도 " + _difficulty.Title : "") + "    체력 " + _player.Health + " / " + Stats.MaxHealth + "    골드 " + _gold + "    " + MetaProgress.CurrencyName + " " + _shards, _hudStyle);
                GUI.Label(new Rect(16f, 40f, 1000f, 28f), shadowState + "    " + dashState + "    처치 " + _kills + "    협공 " + _pincers, _hudStyle);
                GUI.Label(new Rect(16f, 68f, 1400f, 28f), GoalText(), _hudStyle);
                if (_layout.Type == RoomType.Hub && _lastRunSummary != null)
                {
                    GUI.color = ShardColor;
                    GUI.Label(new Rect(16f, 96f, 1400f, 28f), _lastRunSummary, _hudStyle);
                    GUI.color = Color.white;
                }
            }

            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.Label(new Rect(16f, Screen.height - 40f, 1600f, 28f), "이동 A/D 또는 방향키    점프 Space    공격 J 또는 마우스 왼쪽    대시 Shift    자리 바꾸기 K 또는 마우스 오른쪽    분신 고정 S    구매 E    다시 시작 R", _hudStyle);
            GUI.color = Color.white;

            if (_choices != null)
            {
                DrawChoices();
                return;
            }

            DrawBossBar();
            if (_camera == null) return;
            DrawShopLabels();
            DrawHubLabels();

            for (int i = 0; i < _popups.Count; i++)
            {
                Popup popup = _popups[i];
                float t = popup.Age / PopupLifetime;
                Color color = popup.Color;
                color.a = 1f - t * t;
                DrawWorldLabel(new Vector2(popup.Position.x, popup.Position.y + t * 0.8f), popup.Text, color, 600f);
            }
            GUI.color = Color.white;
        }

        private void DrawWorldLabel(Vector2 worldPosition, string text, Color color, float width)
        {
            Vector3 screen = _camera.WorldToScreenPoint(new Vector3(worldPosition.x, worldPosition.y, 0f));
            GUI.color = color;
            GUI.Label(new Rect(screen.x - width * 0.5f, Screen.height - screen.y - 15f, width, 30f), text, _popupStyle);
        }

        private void DrawBossBar()
        {
            if (_boss == null) return;

            float width = Mathf.Min(600f, Screen.width - 80f);
            float left = (Screen.width - width) * 0.5f;
            const float top = 104f;

            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(left - 3f, top - 3f, width + 6f, 22f), Texture2D.whiteTexture);
            GUI.color = new Color(0.85f, 0.2f, 0.25f, 1f);
            GUI.DrawTexture(new Rect(left, top, width * _boss.HealthRatio, 16f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(left, top + 20f, width, 28f), _boss.Kind == EnemyKind.Boss ? "보스" : "중간 보스", _cardBodyStyle);
        }

        private void DrawShopLabels()
        {
            for (int i = 0; i < _shopItems.Count; i++)
            {
                ShopItem item = _shopItems[i];
                Vector2 above = item.Position + Vector2.up * 2f;
                if (item.Sold)
                {
                    DrawWorldLabel(above, "판매 완료", new Color(1f, 1f, 1f, 0.5f), 300f);
                    continue;
                }

                DrawWorldLabel(above + Vector2.up * 0.6f, item.Title, Color.white, 300f);
                bool affordable = (item.PaysShards ? _shards : _gold) >= item.Price;
                string price = item.PaysShards ? MetaProgress.CurrencyName + " " + item.Price : item.Price + " 골드";
                DrawWorldLabel(above, price, affordable ? (item.PaysShards ? ShardColor : GoldColor) : new Color(1f, 0.4f, 0.4f, 1f), 300f);
                if (item == _nearItem)
                {
                    DrawWorldLabel(above + Vector2.up * 1.3f, item.Description + "  (E: 구매)", new Color(0.8f, 1f, 0.85f, 1f), 900f);
                }
            }
        }

        private void DrawChoices()
        {
            var titles = new string[_choices.Count];
            var bodies = new string[_choices.Count];
            for (int i = 0; i < _choices.Count; i++)
            {
                titles[i] = _choices[i].Title;
                bodies[i] = _choices[i].Description;
            }

            int clicked = DrawCards("강화를 하나 고르세요. " + MetaProgress.CurrencyName + " " + AltarPrice + "개를 쓴다 (숫자 키 1, 2, 3 또는 클릭, Esc로 닫기)", titles, bodies);
            if (clicked >= 0) Choose(clicked);
        }

        /// <summary>화면을 어둡게 하고 카드를 나란히 그린다. 클릭한 카드의 번호를 돌려주고, 없으면 -1이다.</summary>
        private int DrawCards(string header, string[] titles, string[] bodies)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            const float cardWidth = 300f;
            const float cardHeight = 190f;
            const float gap = 24f;
            float totalWidth = titles.Length * cardWidth + (titles.Length - 1) * gap;
            float left = (Screen.width - totalWidth) * 0.5f;
            float top = (Screen.height - cardHeight) * 0.5f;

            GUI.Label(new Rect(0f, top - 60f, Screen.width, 36f), header, _cardTitleStyle);

            int clicked = -1;
            for (int i = 0; i < titles.Length; i++)
            {
                var card = new Rect(left + i * (cardWidth + gap), top, cardWidth, cardHeight);
                if (GUI.Button(card, GUIContent.none)) clicked = i;
                GUI.Label(new Rect(card.x + 12f, card.y + 14f, cardWidth - 24f, 32f), (i + 1) + ". " + titles[i], _cardTitleStyle);
                GUI.Label(new Rect(card.x + 16f, card.y + 64f, cardWidth - 32f, cardHeight - 76f), bodies[i], _cardBodyStyle);
            }

            return clicked;
        }
    }
}

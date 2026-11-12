using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 프로토타입 진행 담당. SampleScene에서 Play를 누르면 씬을 따로 손대지 않아도
    /// 스테이지, 플레이어, 분신, 적을 코드로 만들어 준다.
    /// 스테이지의 적을 모두 잡으면 오른쪽 끝의 문이 열리고, 문에 닿으면 다음 스테이지로 간다.
    /// </summary>
    public sealed class PrototypeGame : MonoBehaviour
    {
        /// <summary>이 이름의 씬에서만 프로토타입이 자동으로 켜진다.</summary>
        public const string PrototypeSceneName = "SampleScene";

        private const float PopupLifetime = 0.8f;
        private const int HealOnStageClear = 2;

        private static readonly Color DoorLockedColor = new Color(0.45f, 0.2f, 0.2f, 1f);
        private static readonly Color DoorOpenColor = new Color(0.3f, 0.9f, 0.5f, 1f);

        private static PrototypeGame _instance;

        private struct Popup
        {
            public Vector2 Position;
            public string Text;
            public Color Color;
            public float Age;
        }

        private readonly List<Enemy> _enemies = new List<Enemy>();
        private readonly List<Popup> _popups = new List<Popup>();
        private GameObject _root;
        private StageLayout _layout;
        private PlayerController _player;
        private SpriteRenderer _door;
        private Camera _camera;
        private GUIStyle _hudStyle;
        private GUIStyle _popupStyle;
        private GUIStyle _cardTitleStyle;
        private GUIStyle _cardBodyStyle;
        private List<Upgrade> _choices;
        private int _stage;
        private int _pincers;
        private int _kills;
        private bool _stageCleared;
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

            if (_choices != null)
            {
                int picked = ReadChoiceInput();
                if (picked >= 0 && picked < _choices.Count) Choose(picked);
                return;
            }

            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                Popup popup = _popups[i];
                popup.Age += Time.deltaTime;
                if (popup.Age >= PopupLifetime) _popups.RemoveAt(i);
                else _popups[i] = popup;
            }

            if (_player == null || _door == null || _stageCleared) return;

            bool doorOpen = _enemies.Count == 0;
            _door.color = doorOpen ? DoorOpenColor : DoorLockedColor;

            Vector2 toDoor = _player.Position - (Vector2)_door.transform.position;
            if (doorOpen && Mathf.Abs(toDoor.x) < 1.1f && Mathf.Abs(toDoor.y) < 2f)
            {
                _stageCleared = true;
                ClearStage();
            }
        }

        // ---- 판과 스테이지 ----

        /// <summary>새 판을 시작한다. 강화와 진행이 모두 처음으로 돌아간다.</summary>
        private void StartRun()
        {
            Stats = new RunStats();
            _stage = 1;
            _pincers = 0;
            _kills = 0;
            BuildStage(Stats.MaxHealth);
        }

        private void BuildStage(int playerHealth)
        {
            if (_root != null) Destroy(_root);
            _enemies.Clear();
            _popups.Clear();
            _choices = null;
            _stageCleared = false;
            Time.timeScale = 1f;

            _root = new GameObject("PrototypeStage");
            Transform parent = _root.transform;
            _layout = StageBuilder.Build(parent, _stage);

            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(parent, false);
            playerGo.transform.position = FeetToCenter(_layout.PlayerSpawn, PlayerController.BodySize);
            _player = playerGo.AddComponent<PlayerController>();
            _player.Build(this, Stats, playerHealth);

            if (_layout.HasDummy)
            {
                // 협공을 연습할 수 있는 허수아비. 움직이지 않고 체력이 다시 찬다.
                Enemy dummy = CreateEnemy(EnemyKind.Dummy, _layout.DummyFeet, 100f);
                Physics2D.IgnoreCollision(dummy.Collider, _player.Collider);
            }

            float enemyHealth = 30f + 8f * (_stage - 1);
            for (int i = 0; i < _layout.Enemies.Count; i++)
            {
                EnemySpawn spawn = _layout.Enemies[i];
                Enemy enemy = CreateEnemy(spawn.Kind, spawn.Feet, enemyHealth);

                // 적은 플레이어나 다른 적과 몸으로 부딪히지 않는다.
                Physics2D.IgnoreCollision(enemy.Collider, _player.Collider);
                for (int j = 0; j < _enemies.Count; j++)
                {
                    Physics2D.IgnoreCollision(enemy.Collider, _enemies[j].Collider);
                }
                _enemies.Add(enemy);
            }

            Vector2 doorSize = new Vector2(1.6f, 2.6f);
            _door = PrototypeArt.CreateBox("ExitDoor", parent, FeetToCenter(_layout.ExitPosition, doorSize), doorSize, DoorLockedColor, 1);

            SetUpCamera(playerGo.transform);
            ShowPopup(_player.Position + Vector2.up * 2.2f, "스테이지 " + _stage, Color.white);
        }

        private Enemy CreateEnemy(EnemyKind kind, Vector2 feet, float health)
        {
            var go = new GameObject(kind.ToString());
            go.transform.SetParent(_root.transform, false);
            go.transform.position = FeetToCenter(feet, Enemy.SizeOf(kind));
            var enemy = go.AddComponent<Enemy>();
            enemy.MaxHealth = health;
            enemy.MoveSpeed = Random.Range(2.2f, 3.2f);
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

        private void ClearStage()
        {
            _player.Heal(HealOnStageClear);

            List<Upgrade> choices = UpgradePool.Pick(Stats, 3);
            if (choices.Count == 0)
            {
                GoToNextStage();
                return;
            }
            _choices = choices;
            Time.timeScale = 0f;
        }

        private void GoToNextStage()
        {
            int health = _player != null ? _player.Health : Stats.MaxHealth;
            _stage++;
            BuildStage(health);
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

            if (_player != null) upgrade.Apply(Stats, _player);
            GoToNextStage();
            if (_player != null)
            {
                ShowPopup(_player.Position + Vector2.up * 3f, upgrade.Title, new Color(0.6f, 1f, 0.7f, 1f));
            }
        }

        // ---- 적과 전투 ----

        public void OnEnemyDied(Enemy enemy)
        {
            if (_enemies.Remove(enemy))
            {
                _kills++;
                if (_enemies.Count == 0 && _player != null)
                {
                    ShowPopup(_player.Position + Vector2.up * 2.2f, "문이 열렸다", DoorOpenColor);
                }
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

        public void SpawnProjectile(Vector2 origin, Vector2 velocity)
        {
            if (_root == null) return;
            SpriteRenderer renderer = PrototypeArt.CreateBox("Projectile", _root.transform, origin, new Vector2(0.35f, 0.35f), new Color(1f, 0.8f, 0.2f, 1f), 15);
            renderer.gameObject.AddComponent<Projectile>().Launch(_player, velocity);
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

            if (_player != null)
            {
                string shadowState = _player.ShadowFill < 1f
                    ? "분신 준비 중 " + Mathf.RoundToInt(_player.ShadowFill * 100f) + "%"
                    : (_player.SwapCooldownRemaining > 0f
                        ? "자리 바꾸기 " + _player.SwapCooldownRemaining.ToString("0.0") + "초"
                        : "자리 바꾸기 가능");
                string dashState = _player.DashCooldownRemaining > 0f
                    ? "대시 " + _player.DashCooldownRemaining.ToString("0.0") + "초"
                    : "대시 가능";
                string goal = _enemies.Count > 0
                    ? "남은 적 " + _enemies.Count
                    : "문이 열렸다. 오른쪽 끝의 초록 문으로 가자";

                GUI.color = Color.white;
                GUI.Label(new Rect(16f, 12f, 900f, 28f), "체력 " + _player.Health + " / " + Stats.MaxHealth + "    스테이지 " + _stage + "    " + goal, _hudStyle);
                GUI.Label(new Rect(16f, 40f, 900f, 28f), shadowState + "    " + dashState + "    처치 " + _kills + "    협공 " + _pincers, _hudStyle);
            }

            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.Label(new Rect(16f, Screen.height - 40f, 1400f, 28f), "이동 A/D 또는 방향키    점프 Space    공격 J 또는 마우스 왼쪽    대시 Shift    자리 바꾸기 K 또는 마우스 오른쪽    다시 시작 R", _hudStyle);

            if (_choices != null)
            {
                DrawChoices();
                return;
            }

            if (_camera == null) return;
            for (int i = 0; i < _popups.Count; i++)
            {
                Popup popup = _popups[i];
                float t = popup.Age / PopupLifetime;
                Vector3 screen = _camera.WorldToScreenPoint(new Vector3(popup.Position.x, popup.Position.y + t * 0.8f, 0f));
                Color color = popup.Color;
                color.a = 1f - t * t;
                GUI.color = color;
                GUI.Label(new Rect(screen.x - 100f, Screen.height - screen.y - 15f, 200f, 30f), popup.Text, _popupStyle);
            }
            GUI.color = Color.white;
        }

        private void DrawChoices()
        {
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            const float cardWidth = 300f;
            const float cardHeight = 190f;
            const float gap = 24f;
            float totalWidth = _choices.Count * cardWidth + (_choices.Count - 1) * gap;
            float left = (Screen.width - totalWidth) * 0.5f;
            float top = (Screen.height - cardHeight) * 0.5f;

            GUI.Label(new Rect(0f, top - 60f, Screen.width, 36f), "스테이지 클리어! 강화를 하나 고르세요 (숫자 키 1, 2, 3 또는 클릭)", _cardTitleStyle);

            int clicked = -1;
            for (int i = 0; i < _choices.Count; i++)
            {
                var card = new Rect(left + i * (cardWidth + gap), top, cardWidth, cardHeight);
                if (GUI.Button(card, GUIContent.none)) clicked = i;
                GUI.Label(new Rect(card.x + 12f, card.y + 14f, cardWidth - 24f, 32f), (i + 1) + ". " + _choices[i].Title, _cardTitleStyle);
                GUI.Label(new Rect(card.x + 16f, card.y + 64f, cardWidth - 32f, cardHeight - 76f), _choices[i].Description, _cardBodyStyle);
            }

            if (clicked >= 0) Choose(clicked);
        }
    }
}

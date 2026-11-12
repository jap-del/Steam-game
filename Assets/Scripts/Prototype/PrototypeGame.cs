using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 프로토타입 전투장. SampleScene에서 Play를 누르면 씬을 따로 손대지 않아도
    /// 바닥, 발판, 플레이어, 분신, 적을 코드로 만들어 준다.
    /// </summary>
    public sealed class PrototypeGame : MonoBehaviour
    {
        /// <summary>이 이름의 씬에서만 프로토타입이 자동으로 켜진다.</summary>
        public const string PrototypeSceneName = "SampleScene";

        private const float GroundTop = -2.5f;
        private const float ArenaHalfWidth = 22f;

        private static PrototypeGame _instance;

        private struct Popup
        {
            public Vector2 Position;
            public string Text;
            public Color Color;
            public float Age;
        }

        private const float PopupLifetime = 0.8f;

        private readonly List<Enemy> _enemies = new List<Enemy>();
        private readonly List<Popup> _popups = new List<Popup>();
        private GameObject _root;
        private PlayerController _player;
        private Camera _camera;
        private GUIStyle _hudStyle;
        private GUIStyle _popupStyle;
        private GUIStyle _cardTitleStyle;
        private GUIStyle _cardBodyStyle;
        private List<Upgrade> _choices;
        private int _wave;
        private int _pincers;
        private int _kills;
        private float _nextWaveTimer;
        private float _rewardTimer;
        private bool _rewardPending;
        private bool _restartRequested;

        /// <summary>이번 판의 수치. 강화를 고르면 바뀌고, 다시 시작하면 처음으로 돌아간다.</summary>
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
            BuildArena();
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
                BuildArena();
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

            if (_enemies.Count > 0) return;

            if (_rewardPending)
            {
                // 마지막 적을 잡은 직후 잠깐 뜸을 들였다가 강화 선택 화면을 띄운다.
                _rewardTimer -= Time.deltaTime;
                if (_rewardTimer <= 0f) OpenChoices();
                return;
            }

            _nextWaveTimer -= Time.deltaTime;
            if (_nextWaveTimer <= 0f) SpawnWave();
        }

        // ---- 강화 선택 ----

        private void OpenChoices()
        {
            _rewardPending = false;
            List<Upgrade> choices = UpgradePool.Pick(Stats, 3);
            if (choices.Count == 0)
            {
                _nextWaveTimer = 1f;
                return;
            }
            _choices = choices;
            Time.timeScale = 0f;
        }

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
            _nextWaveTimer = 1.2f;

            if (_player != null)
            {
                upgrade.Apply(Stats, _player);
                ShowPopup(_player.Position + Vector2.up * 2.2f, upgrade.Title, new Color(0.6f, 1f, 0.7f, 1f));
            }
        }

        // ---- 전투장 만들기 ----

        private void BuildArena()
        {
            if (_root != null) Destroy(_root);
            _enemies.Clear();
            _popups.Clear();
            _choices = null;
            _rewardPending = false;
            Time.timeScale = 1f;
            Stats = new RunStats();
            _wave = 0;
            _pincers = 0;
            _kills = 0;
            _nextWaveTimer = 2f;

            _root = new GameObject("PrototypeArena");
            Transform parent = _root.transform;

            Color groundColor = new Color(0.32f, 0.34f, 0.42f, 1f);
            CreateSolid("Ground", parent, new Vector2(0f, GroundTop - 0.5f), new Vector2(ArenaHalfWidth * 2f, 1f), groundColor);
            CreateSolid("WallLeft", parent, new Vector2(-ArenaHalfWidth - 0.5f, 3f), new Vector2(1f, 14f), groundColor);
            CreateSolid("WallRight", parent, new Vector2(ArenaHalfWidth + 0.5f, 3f), new Vector2(1f, 14f), groundColor);
            CreateSolid("PlatformLeft", parent, new Vector2(-9f, -0.6f), new Vector2(5f, 0.5f), groundColor);
            CreateSolid("PlatformMid", parent, new Vector2(0f, 1.2f), new Vector2(6f, 0.5f), groundColor);
            CreateSolid("PlatformRight", parent, new Vector2(9f, -0.6f), new Vector2(5f, 0.5f), groundColor);

            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(parent, false);
            playerGo.transform.position = new Vector3(0f, GroundTop + PlayerController.BodySize.y * 0.5f + 0.05f, 0f);
            _player = playerGo.AddComponent<PlayerController>();
            _player.Build(this, Stats);

            // 협공을 연습할 수 있는 허수아비. 움직이지 않고 체력이 다시 찬다.
            Vector2 dummySize = new Vector2(1f, 1.8f);
            var dummyGo = new GameObject("TrainingDummy");
            dummyGo.transform.SetParent(parent, false);
            dummyGo.transform.position = new Vector3(-15f, GroundTop + dummySize.y * 0.5f, 0f);
            var dummy = dummyGo.AddComponent<Enemy>();
            dummy.MaxHealth = 100f;
            dummy.Build(this, _player, dummySize, new Color(0.8f, 0.65f, 0.35f, 1f), true);
            Physics2D.IgnoreCollision(dummy.Collider, _player.Collider);

            SetUpCamera(playerGo.transform);
        }

        private static void CreateSolid(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            SpriteRenderer renderer = PrototypeArt.CreateBox(name, parent, position, size, color, 0);
            renderer.gameObject.AddComponent<BoxCollider2D>();
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
            float limit = Mathf.Max(0f, ArenaHalfWidth + 1f - halfViewWidth);
            follow.MinX = -limit;
            follow.MaxX = limit;
            follow.Target = target;
            follow.SnapToTarget();
        }

        // ---- 적 웨이브 ----

        private void SpawnWave()
        {
            _wave++;
            int count = Mathf.Min(1 + _wave, 8);
            Vector2 size = new Vector2(0.9f, 1.2f);

            for (int i = 0; i < count; i++)
            {
                // 플레이어 바로 위에 떨어지지 않도록 반대편이나 먼 곳에 만든다.
                float side = i % 2 == 0 ? 1f : -1f;
                float x = _player.Position.x + side * Random.Range(8f, 14f);
                if (Mathf.Abs(x) > ArenaHalfWidth - 1.5f)
                {
                    x = _player.Position.x - side * Random.Range(8f, 14f);
                }
                x = Mathf.Clamp(x, -ArenaHalfWidth + 1.5f, ArenaHalfWidth - 1.5f);

                var go = new GameObject("Enemy");
                go.transform.SetParent(_root.transform, false);
                go.transform.position = new Vector3(x, GroundTop + size.y * 0.5f + 0.05f, 0f);
                var enemy = go.AddComponent<Enemy>();
                enemy.MaxHealth = 30f + 6f * (_wave - 1);
                enemy.MoveSpeed = Random.Range(1.8f, 3.2f);
                enemy.Build(this, _player, size, new Color(0.85f, 0.3f, 0.35f, 1f), false);

                Physics2D.IgnoreCollision(enemy.Collider, _player.Collider);
                for (int j = 0; j < _enemies.Count; j++)
                {
                    Physics2D.IgnoreCollision(enemy.Collider, _enemies[j].Collider);
                }
                _enemies.Add(enemy);
            }

            ShowPopup(_player.Position + Vector2.up * 2.2f, "웨이브 " + _wave, Color.white);
        }

        public void OnEnemyDied(Enemy enemy)
        {
            if (_enemies.Remove(enemy))
            {
                _kills++;
                if (_enemies.Count == 0)
                {
                    _rewardPending = true;
                    _rewardTimer = 0.6f;
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

                GUI.color = Color.white;
                GUI.Label(new Rect(16f, 12f, 600f, 28f), "체력 " + _player.Health + " / " + Stats.MaxHealth + "    웨이브 " + _wave + "    처치 " + _kills + "    협공 " + _pincers, _hudStyle);
                string dashState = _player.DashCooldownRemaining > 0f
                    ? "대시 " + _player.DashCooldownRemaining.ToString("0.0") + "초"
                    : "대시 가능";
                GUI.Label(new Rect(16f, 40f, 600f, 28f), shadowState + "    " + dashState, _hudStyle);
            }

            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.Label(new Rect(16f, Screen.height - 40f, 1200f, 28f), "이동 A/D 또는 방향키    점프 Space    공격 J 또는 마우스 왼쪽    자리 바꾸기 K 또는 마우스 오른쪽    대시 Shift    다시 시작 R", _hudStyle);

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

            GUI.Label(new Rect(0f, top - 60f, Screen.width, 36f), "강화를 하나 고르세요 (숫자 키 1, 2, 3 또는 클릭)", _cardTitleStyle);

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

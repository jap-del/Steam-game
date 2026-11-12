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
        private int _wave;
        private int _pincers;
        private int _kills;
        private float _nextWaveTimer;
        private bool _restartRequested;

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

            var go = new GameObject("PrototypeGame");
            _instance = go.AddComponent<PrototypeGame>();
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

            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                Popup popup = _popups[i];
                popup.Age += Time.deltaTime;
                if (popup.Age >= PopupLifetime) _popups.RemoveAt(i);
                else _popups[i] = popup;
            }

            if (_enemies.Count == 0)
            {
                _nextWaveTimer -= Time.deltaTime;
                if (_nextWaveTimer <= 0f) SpawnWave();
            }
        }

        // ---- 전투장 만들기 ----

        private void BuildArena()
        {
            if (_root != null) Destroy(_root);
            _enemies.Clear();
            _popups.Clear();
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

            var shadowGo = new GameObject("ShadowClone");
            shadowGo.transform.SetParent(parent, false);
            var shadow = shadowGo.AddComponent<ShadowClone>();
            shadow.Build(PlayerController.BodySize);

            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(parent, false);
            playerGo.transform.position = new Vector3(0f, GroundTop + PlayerController.BodySize.y * 0.5f + 0.05f, 0f);
            _player = playerGo.AddComponent<PlayerController>();
            _player.Build(this, shadow);

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
                if (_enemies.Count == 0) _nextWaveTimer = 1.5f;
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
            }

            if (_player != null)
            {
                string shadowState = _player.ShadowFill < 1f
                    ? "분신 준비 중 " + Mathf.RoundToInt(_player.ShadowFill * 100f) + "%"
                    : (_player.SwapCooldownRemaining > 0f
                        ? "자리 바꾸기 " + _player.SwapCooldownRemaining.ToString("0.0") + "초"
                        : "자리 바꾸기 가능");

                GUI.color = Color.white;
                GUI.Label(new Rect(16f, 12f, 600f, 28f), "체력 " + _player.Health + " / " + _player.MaxHealth + "    웨이브 " + _wave + "    처치 " + _kills + "    협공 " + _pincers, _hudStyle);
                GUI.Label(new Rect(16f, 40f, 600f, 28f), shadowState, _hudStyle);
            }

            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.Label(new Rect(16f, Screen.height - 40f, 1200f, 28f), "이동 A/D 또는 방향키    점프 Space    공격 J 또는 마우스 왼쪽    자리 바꾸기 K 또는 마우스 오른쪽    다시 시작 R", _hudStyle);

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
    }
}

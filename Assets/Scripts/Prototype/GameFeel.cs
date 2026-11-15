using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 타격감 담당. 맞는 순간의 잠깐 멈춤, 화면 흔들림, 튀는 조각, 화면 번쩍임을 맡고
    /// 그때 나는 효과음도 여기서 낸다. 세기는 아래 함수들의 숫자로 조절한다.
    /// </summary>
    public sealed class GameFeel : MonoBehaviour
    {
        /// <summary>잠깐 멈출 때의 시간 흐름. 0에 가까울수록 완전히 멈춘 것처럼 보인다.</summary>
        private const float HitStopScale = 0.05f;

        private PrototypeGame _game;
        private CameraFollow _camera;
        private float _freezeUntil;
        private Color _flashColor;
        private float _flashAlpha;
        private float _flashDuration;
        private float _flashAge;
        private Texture2D _vignette;
        private Color _vignetteColor = Color.black;
        private float _vignetteAlpha;
        private float _vignetteTarget;

        public void Init(PrototypeGame game)
        {
            _game = game;
        }

        public void SetCamera(CameraFollow camera)
        {
            _camera = camera;
        }

        // ---- 재료 ----

        /// <summary>seconds(실제 시간) 동안 게임 시간을 scale배로 늦춘다. 타격 순간의 멈춤과 보스 처치의 느린 화면에 쓴다.</summary>
        public void Freeze(float seconds, float scale)
        {
            if (_game == null || _game.IsPaused) return;

            _freezeUntil = Mathf.Max(_freezeUntil, Time.unscaledTime + seconds);
            Time.timeScale = Mathf.Min(Time.timeScale, scale);
        }

        /// <summary>
        /// seconds(실제 시간) 동안 느린 화면으로 바꾼다. Freeze와 달리 직전의 타격 멈춤을 덮어쓰므로,
        /// 마지막 일격의 멈춤이 느린 화면 내내 이어지지 않는다.
        /// </summary>
        private void SlowMotion(float seconds, float scale)
        {
            if (_game == null || _game.IsPaused) return;

            _freezeUntil = Time.unscaledTime + seconds;
            Time.timeScale = scale;
        }

        /// <summary>방을 새로 만들 때 부른다. 남아 있던 멈춤을 없앤다.</summary>
        public void CancelFreeze()
        {
            _freezeUntil = 0f;
        }

        public void Shake(float amplitude, float duration)
        {
            if (_camera != null) _camera.Shake(amplitude, duration);
        }

        /// <summary>화면 전체를 한 가지 색으로 번쩍였다가 서서히 걷어 낸다.</summary>
        public void Flash(Color color, float alpha, float seconds)
        {
            _flashColor = color;
            _flashAlpha = alpha;
            _flashDuration = Mathf.Max(0.01f, seconds);
            _flashAge = 0f;
        }

        /// <summary>
        /// 화면 가장자리를 어둡게 한다. 보스전에서 긴장감을 주는 데 쓴다. alpha가 0이면 서서히 걷힌다.
        /// </summary>
        public void SetVignette(Color color, float alpha)
        {
            if (alpha > 0f) _vignetteColor = color;
            _vignetteTarget = alpha;
        }

        /// <summary>화면 가장자리의 어둠을 그린다. 글자에 덮이도록 게임의 화면 표시보다 먼저 불러야 한다.</summary>
        public void DrawVignette()
        {
            if (_vignetteAlpha <= 0.01f) return;

            if (_vignette == null) _vignette = BuildVignette();
            Color color = _vignetteColor;
            color.a = _vignetteAlpha;
            GUI.color = color;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _vignette, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;
        }

        /// <summary>가운데는 투명하고 가장자리로 갈수록 진해지는 그림.</summary>
        private static Texture2D BuildVignette()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float edge = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.55f) / 0.75f);
                    byte alpha = (byte)Mathf.RoundToInt(255f * edge * edge * (3f - 2f * edge));
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        public void Sparks(Vector2 position, Color color, int count, float speed)
        {
            Transform parent = _game != null ? _game.RoomRoot : null;
            if (parent == null) return;

            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.8f + 0.35f);
                float size = Random.Range(0.08f, 0.16f);
                SpriteRenderer box = PrototypeArt.CreateBox("Spark", parent, position, new Vector2(size, size), color, 16);
                box.gameObject.AddComponent<Spark>().Launch(box, direction * speed * Random.Range(0.5f, 1f), Random.Range(0.18f, 0.36f));
            }
        }

        // ---- 상황별 연출 ----

        /// <summary>적이 맞았다. 협공이 가장 세고, 분신 혼자 때린 것은 흐름이 끊기지 않게 멈춤 없이 가볍게 한다.</summary>
        public void HitEnemy(Vector2 position, bool pincer, bool fromShadow)
        {
            if (pincer)
            {
                Sfx.Play(Sound.Pincer);
                Freeze(0.09f, HitStopScale);
                Shake(0.18f, 0.16f);
                Sparks(position, new Color(1f, 0.9f, 0.3f, 1f), 9, 7f);
            }
            else if (fromShadow)
            {
                Sfx.Play(Sound.Hit, 0.6f);
                Shake(0.04f, 0.06f);
                Sparks(position, new Color(0.75f, 0.6f, 1f, 1f), 3, 4f);
            }
            else
            {
                Sfx.Play(Sound.Hit);
                Freeze(0.045f, HitStopScale);
                Shake(0.08f, 0.09f);
                Sparks(position, Color.white, 4, 5f);
            }
        }

        /// <summary>방패에 막혔다.</summary>
        public void BlockedHit(Vector2 position)
        {
            Sfx.Play(Sound.Block);
            Shake(0.05f, 0.06f);
            Sparks(position, new Color(0.8f, 0.9f, 1f, 1f), 3, 4f);
        }

        public void EnemyDied(Vector2 position, Color color)
        {
            Sfx.Play(Sound.EnemyDie);
            Shake(0.12f, 0.12f);
            Sparks(position, color, 10, 6f);
        }

        public void PlayerHurt()
        {
            Sfx.Play(Sound.Hurt);
            Freeze(0.1f, HitStopScale);
            Shake(0.28f, 0.22f);
            Flash(new Color(1f, 0.15f, 0.15f, 1f), 0.3f, 0.3f);
        }

        public void Explosion(Vector2 position)
        {
            Sfx.Play(Sound.Explosion);
            Shake(0.22f, 0.2f);
            Sparks(position, new Color(1f, 0.6f, 0.25f, 1f), 8, 6f);
        }

        /// <summary>보스가 내려찍으며 착지했다.</summary>
        public void BossSlam(Vector2 position)
        {
            Sfx.Play(Sound.BossSlam);
            Shake(0.45f, 0.3f);
            Sparks(position, new Color(1f, 0.95f, 0.3f, 1f), 10, 7f);
        }

        /// <summary>보스의 체력이 절반 아래로 내려가 분노했다.</summary>
        public void BossEnraged(Vector2 position)
        {
            Sfx.Play(Sound.BossRage);
            Sfx.SetBossMusicEnraged(true);
            Freeze(0.35f, HitStopScale);
            Shake(0.4f, 0.5f);
            Flash(new Color(1f, 0.2f, 0.15f, 1f), 0.35f, 0.5f);
            Sparks(position, new Color(1f, 0.35f, 0.3f, 1f), 14, 8f);
        }

        /// <summary>보스를 잡았다. 화면이 하얗게 번쩍이고 잠깐 느리게 흐른다.</summary>
        public void BossDied(Vector2 position, Color color)
        {
            Sfx.StopBossMusic();
            Sfx.Play(Sound.BossDie);
            SlowMotion(1f, 0.2f);
            Shake(0.6f, 0.7f);
            Flash(Color.white, 0.7f, 0.6f);
            Sparks(position, color, 20, 9f);
            Sparks(position, Color.white, 10, 6f);
        }

        // ---- 내부 ----

        private void Update()
        {
            if (_freezeUntil > 0f && Time.unscaledTime >= _freezeUntil)
            {
                _freezeUntil = 0f;
                // 강화 고르기나 보스 등장처럼 게임이 멈춰 있는 동안에는 시간을 되돌리지 않는다.
                if (_game != null && !_game.IsPaused) Time.timeScale = 1f;
            }

            if (_flashAge < _flashDuration) _flashAge += Time.unscaledDeltaTime;
            _vignetteAlpha = Mathf.MoveTowards(_vignetteAlpha, _vignetteTarget, Time.unscaledDeltaTime * 1.2f);
        }

        private void OnDestroy()
        {
            if (_vignette != null) Destroy(_vignette);
        }

        private void OnGUI()
        {
            if (_flashAge >= _flashDuration || _flashAlpha <= 0f) return;

            // 숫자가 작을수록 다른 화면 표시 위에 그려진다.
            GUI.depth = -10;
            Color color = _flashColor;
            color.a = _flashAlpha * (1f - _flashAge / _flashDuration);
            GUI.color = color;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}

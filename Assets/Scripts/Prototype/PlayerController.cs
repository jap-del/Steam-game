using UnityEngine;
using UnityEngine.InputSystem;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 플레이어 조작: 좌우 이동, 점프, 근접 공격, 분신과 자리 바꾸기.
    /// 매 물리 틱마다 자신의 상태를 기록해 분신에게 넘긴다.
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        public static readonly Vector2 BodySize = new Vector2(0.8f, 1.4f);

        public float MoveSpeed = 7f;
        public float JumpSpeed = 14f;
        public float GravityScale = 3.5f;
        public float AttackDamage = 10f;
        public float AttackCooldown = 0.3f;
        public float ShadowDelaySeconds = 3f;
        public float SwapCooldown = 4f;
        public int MaxHealth = 5;

        private const float CoyoteTime = 0.1f;
        private const float JumpBufferTime = 0.12f;
        private const float InvulnerableTime = 0.8f;

        private PrototypeGame _game;
        private ShadowClone _shadow;
        private ShadowRecorder _recorder;
        private Rigidbody2D _body;
        private BoxCollider2D _collider;
        private SpriteRenderer _sprite;
        private SpriteRenderer _slash;
        private readonly ContactPoint2D[] _contacts = new ContactPoint2D[8];

        private float _moveInput;
        private int _facing = 1;
        private bool _jumpHeld;
        private bool _jumpCut;
        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private float _attackTimer;
        private float _slashTimer;
        private float _swapTimer;
        private float _invulnerableTimer;
        private float _knockbackTimer;
        private bool _attackedThisTick;
        private int _health;

        public int Health => _health;
        public Collider2D Collider => _collider;
        public Vector2 Position => _body != null ? _body.position : (Vector2)transform.position;
        public float SwapCooldownRemaining => Mathf.Max(0f, _swapTimer);
        public float ShadowFill => _recorder != null ? _recorder.Fill : 0f;

        public void Build(PrototypeGame game, ShadowClone shadow)
        {
            _game = game;
            _shadow = shadow;
            _health = MaxHealth;
            _recorder = new ShadowRecorder(Mathf.RoundToInt(ShadowDelaySeconds / Time.fixedDeltaTime));

            _sprite = PrototypeArt.CreateBox("Body", transform, Vector2.zero, BodySize, new Color(0.95f, 0.95f, 1f, 1f), 10);
            _slash = PrototypeArt.CreateBox("Slash", transform, Vector2.zero, Combat.SlashSize, new Color(1f, 1f, 1f, 0.7f), 12);
            _slash.enabled = false;

            _collider = gameObject.AddComponent<BoxCollider2D>();
            _collider.size = BodySize;
            var slippery = new PhysicsMaterial2D("PlayerNoFriction");
            slippery.friction = 0f;
            slippery.bounciness = 0f;
            _collider.sharedMaterial = slippery;

            _body = gameObject.AddComponent<Rigidbody2D>();
            _body.gravityScale = GravityScale;
            _body.freezeRotation = true;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        private void Update()
        {
            ReadInput();

            if (_attackTimer > 0f) _attackTimer -= Time.deltaTime;
            if (_swapTimer > 0f) _swapTimer -= Time.deltaTime;
            if (_jumpBufferTimer > 0f) _jumpBufferTimer -= Time.deltaTime;

            if (_slashTimer > 0f)
            {
                _slashTimer -= Time.deltaTime;
                if (_slashTimer <= 0f) _slash.enabled = false;
            }

            if (_invulnerableTimer > 0f)
            {
                _invulnerableTimer -= Time.deltaTime;
                // 무적 시간 동안 깜빡인다.
                bool visible = _invulnerableTimer <= 0f || Mathf.FloorToInt(_invulnerableTimer * 20f) % 2 == 0;
                _sprite.enabled = visible;
            }
        }

        private void ReadInput()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            Mouse mouse = Mouse.current;

            float move = 0f;
            bool jumpPressed = false;
            bool jumpHeld = false;
            bool attackPressed = false;
            bool swapPressed = false;

            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move += 1f;
                jumpPressed |= keyboard.spaceKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame;
                jumpHeld |= keyboard.spaceKey.isPressed || keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;
                attackPressed |= keyboard.jKey.wasPressedThisFrame || keyboard.zKey.wasPressedThisFrame;
                swapPressed |= keyboard.kKey.wasPressedThisFrame || keyboard.xKey.wasPressedThisFrame;
            }

            if (mouse != null)
            {
                attackPressed |= mouse.leftButton.wasPressedThisFrame;
                swapPressed |= mouse.rightButton.wasPressedThisFrame;
            }

            if (gamepad != null)
            {
                float stick = gamepad.leftStick.ReadValue().x;
                if (Mathf.Abs(stick) > 0.3f) move = Mathf.Sign(stick);
                if (gamepad.dpad.left.isPressed) move = -1f;
                if (gamepad.dpad.right.isPressed) move = 1f;
                jumpPressed |= gamepad.buttonSouth.wasPressedThisFrame;
                jumpHeld |= gamepad.buttonSouth.isPressed;
                attackPressed |= gamepad.buttonWest.wasPressedThisFrame;
                swapPressed |= gamepad.buttonNorth.wasPressedThisFrame;
            }

            _moveInput = Mathf.Clamp(move, -1f, 1f);
            _jumpHeld = jumpHeld;
            if (_moveInput > 0.01f) _facing = 1;
            else if (_moveInput < -0.01f) _facing = -1;

            if (jumpPressed) _jumpBufferTimer = JumpBufferTime;
            if (attackPressed) TryAttack();
            if (swapPressed) TrySwap();
        }

        private void TryAttack()
        {
            if (_attackTimer > 0f) return;

            _attackTimer = AttackCooldown;
            _attackedThisTick = true;
            Combat.Slash(_body.position, _facing, AttackDamage, HitSource.Player);

            _slash.transform.localPosition = new Vector3(_facing * Combat.SlashReach, 0f, 0f);
            _slash.enabled = true;
            _slashTimer = 0.12f;
        }

        private void TrySwap()
        {
            if (_swapTimer > 0f || _shadow == null || !_shadow.IsActive) return;

            _swapTimer = SwapCooldown;
            Vector2 destination = _shadow.Position;
            _body.position = destination;
            transform.position = new Vector3(destination.x, destination.y, 0f);
            _body.linearVelocity = Vector2.zero;
            _game.ShowPopup(destination + Vector2.up * 1.2f, "자리 바꾸기", new Color(0.75f, 0.6f, 1f, 1f));
        }

        private void FixedUpdate()
        {
            bool grounded = IsGrounded();
            if (grounded) _coyoteTimer = CoyoteTime;
            else if (_coyoteTimer > 0f) _coyoteTimer -= Time.fixedDeltaTime;

            Vector2 velocity = _body.linearVelocity;

            if (_knockbackTimer > 0f)
            {
                _knockbackTimer -= Time.fixedDeltaTime;
            }
            else
            {
                velocity.x = _moveInput * MoveSpeed;
            }

            if (_jumpBufferTimer > 0f && _coyoteTimer > 0f)
            {
                velocity.y = JumpSpeed;
                _jumpBufferTimer = 0f;
                _coyoteTimer = 0f;
                _jumpCut = false;
            }
            else if (!_jumpHeld && !_jumpCut && velocity.y > 0f)
            {
                // 점프 버튼을 일찍 떼면 낮게 뛴다.
                velocity.y *= 0.5f;
                _jumpCut = true;
            }

            _body.linearVelocity = velocity;

            // 지금 상태를 기록하고, 지연 시간만큼 과거의 상태를 분신에게 재현시킨다.
            var current = new ShadowFrame(_body.position, _facing, _attackedThisTick);
            _attackedThisTick = false;
            if (_recorder.Push(current, out ShadowFrame delayed) && _shadow != null)
            {
                _shadow.Replay(delayed, AttackDamage);
            }
        }

        /// <summary>발 아래쪽에 위아래 방향으로 맞닿은 면이 있으면 땅에 서 있는 것으로 본다.</summary>
        private bool IsGrounded()
        {
            int count = _body.GetContacts(_contacts);
            float feetLine = _body.position.y - BodySize.y * 0.25f;
            for (int i = 0; i < count; i++)
            {
                if (Mathf.Abs(_contacts[i].normal.y) > 0.5f && _contacts[i].point.y < feetLine)
                {
                    return true;
                }
            }
            return false;
        }

        public void TakeDamage(int amount, float sourceX)
        {
            if (_invulnerableTimer > 0f || _health <= 0) return;

            _health -= amount;
            _invulnerableTimer = InvulnerableTime;
            _knockbackTimer = 0.2f;
            float direction = _body.position.x >= sourceX ? 1f : -1f;
            _body.linearVelocity = new Vector2(direction * 6f, 7f);
            _game.ShowPopup(_body.position + Vector2.up * 1.2f, "-" + amount, new Color(1f, 0.4f, 0.4f, 1f));

            if (_health <= 0)
            {
                _game.RequestRestart();
            }
        }
    }
}

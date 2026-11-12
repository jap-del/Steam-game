using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 플레이어 조작: 좌우 이동, 점프, 대시, 근접 공격, 분신과 자리 바꾸기.
    /// 매 물리 틱마다 자신의 상태를 기록해 분신들에게 넘긴다.
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        public static readonly Vector2 BodySize = new Vector2(0.8f, 1.4f);

        public float MoveSpeed = 7f;
        public float JumpSpeed = 14f;
        public float GravityScale = 3.5f;
        public float AttackCooldown = 0.3f;
        public float DashSpeed = 18f;
        public float DashDuration = 0.15f;

        private const float CoyoteTime = 0.1f;
        private const float JumpBufferTime = 0.12f;
        private const float InvulnerableTime = 0.8f;
        private const float ShockwaveRadius = 2.2f;

        /// <summary>분신 하나와, 그 분신이 따라 할 과거 기록.</summary>
        private sealed class ShadowSlot
        {
            public ShadowRecorder Recorder;
            public ShadowClone Clone;
        }

        private static readonly Color BodyColor = new Color(0.95f, 0.95f, 1f, 1f);
        private static readonly Color DashColor = new Color(0.5f, 0.85f, 1f, 1f);

        private readonly List<ShadowSlot> _shadows = new List<ShadowSlot>();
        private readonly ContactPoint2D[] _contacts = new ContactPoint2D[8];
        private PrototypeGame _game;
        private RunStats _stats;
        private Rigidbody2D _body;
        private BoxCollider2D _collider;
        private SpriteRenderer _sprite;
        private SpriteRenderer _slash;

        private float _moveInput;
        private int _facing = 1;
        private bool _jumpHeld;
        private bool _jumpCut;
        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private float _attackTimer;
        private float _slashTimer;
        private float _swapTimer;
        private float _dashTimer;
        private float _dashCooldownTimer;
        private int _dashDirection = 1;
        private float _invulnerableTimer;
        private float _knockbackTimer;
        private bool _attackedThisTick;
        private int _health;

        public int Health => _health;
        public Collider2D Collider => _collider;
        public Vector2 Position => _body != null ? _body.position : (Vector2)transform.position;
        public float SwapCooldownRemaining => Mathf.Max(0f, _swapTimer);
        public float DashCooldownRemaining => Mathf.Max(0f, _dashCooldownTimer);
        public float ShadowFill => _shadows.Count > 0 ? _shadows[0].Recorder.Fill : 0f;

        public void Build(PrototypeGame game, RunStats stats)
        {
            _game = game;
            _stats = stats;
            _health = stats.MaxHealth;

            _sprite = PrototypeArt.CreateBox("Body", transform, Vector2.zero, BodySize, BodyColor, 10);
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

            RebuildShadows();
        }

        /// <summary>
        /// 분신을 현재 수치(개수, 지연 시간, 공격 범위)에 맞게 다시 만든다.
        /// 기록이 비워지므로 분신은 지연 시간이 지난 뒤 다시 나타난다.
        /// </summary>
        public void RebuildShadows()
        {
            for (int i = 0; i < _shadows.Count; i++)
            {
                if (_shadows[i].Clone != null) Destroy(_shadows[i].Clone.gameObject);
            }
            _shadows.Clear();

            int baseTicks = Mathf.Max(1, Mathf.RoundToInt(_stats.ShadowDelaySeconds / Time.fixedDeltaTime));
            for (int i = 0; i < _stats.ShadowCount; i++)
            {
                var go = new GameObject("ShadowClone" + (i + 1));
                go.transform.SetParent(transform.parent, false);
                var clone = go.AddComponent<ShadowClone>();
                clone.Build(BodySize, _stats.ShadowSlashScale, i == 0 ? 0.55f : 0.35f);

                var slot = new ShadowSlot();
                slot.Recorder = new ShadowRecorder(baseTicks * (i + 1));
                slot.Clone = clone;
                _shadows.Add(slot);
            }
        }

        public void Heal(int amount)
        {
            _health = Mathf.Min(_stats.MaxHealth, _health + amount);
        }

        private void Update()
        {
            if (_game.IsPaused) return;

            ReadInput();

            if (_attackTimer > 0f) _attackTimer -= Time.deltaTime;
            if (_swapTimer > 0f) _swapTimer -= Time.deltaTime;
            if (_dashCooldownTimer > 0f) _dashCooldownTimer -= Time.deltaTime;
            if (_jumpBufferTimer > 0f) _jumpBufferTimer -= Time.deltaTime;

            if (_slashTimer > 0f)
            {
                _slashTimer -= Time.deltaTime;
                if (_slashTimer <= 0f) _slash.enabled = false;
            }

            _sprite.color = _dashTimer > 0f ? DashColor : BodyColor;

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
            bool dashPressed = false;

            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move += 1f;
                jumpPressed |= keyboard.spaceKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame;
                jumpHeld |= keyboard.spaceKey.isPressed || keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;
                attackPressed |= keyboard.jKey.wasPressedThisFrame || keyboard.zKey.wasPressedThisFrame;
                swapPressed |= keyboard.kKey.wasPressedThisFrame || keyboard.xKey.wasPressedThisFrame;
                dashPressed |= keyboard.leftShiftKey.wasPressedThisFrame || keyboard.lKey.wasPressedThisFrame || keyboard.cKey.wasPressedThisFrame;
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
                dashPressed |= gamepad.buttonEast.wasPressedThisFrame || gamepad.rightShoulder.wasPressedThisFrame;
            }

            _moveInput = Mathf.Clamp(move, -1f, 1f);
            _jumpHeld = jumpHeld;
            if (_dashTimer <= 0f)
            {
                if (_moveInput > 0.01f) _facing = 1;
                else if (_moveInput < -0.01f) _facing = -1;
            }

            if (jumpPressed) _jumpBufferTimer = JumpBufferTime;
            if (dashPressed) TryDash();
            if (attackPressed) TryAttack();
            if (swapPressed) TrySwap();
        }

        private void TryDash()
        {
            if (_dashCooldownTimer > 0f || _dashTimer > 0f) return;

            _dashTimer = DashDuration;
            _dashCooldownTimer = _stats.DashCooldown;
            _dashDirection = _facing;
        }

        private void TryAttack()
        {
            if (_attackTimer > 0f) return;

            _attackTimer = AttackCooldown;
            _attackedThisTick = true;
            Combat.Slash(_body.position, _facing, _stats.PlayerDamage, HitSource.Player);

            _slash.transform.localPosition = new Vector3(_facing * Combat.SlashReach, 0f, 0f);
            _slash.enabled = true;
            _slashTimer = 0.12f;
        }

        private void TrySwap()
        {
            if (_swapTimer > 0f || _shadows.Count == 0 || !_shadows[0].Clone.IsActive) return;

            _swapTimer = _stats.SwapCooldown;
            Vector2 origin = _body.position;
            Vector2 destination = _shadows[0].Clone.Position;
            _body.position = destination;
            transform.position = new Vector3(destination.x, destination.y, 0f);
            _body.linearVelocity = Vector2.zero;
            _dashTimer = 0f;
            _game.ShowPopup(destination + Vector2.up * 1.2f, "자리 바꾸기", new Color(0.75f, 0.6f, 1f, 1f));

            if (_stats.SwapShockwaveDamage > 0f)
            {
                // 떠난 자리는 분신의 공격, 도착한 자리는 본체의 공격으로 쳐서 둘이 겹치면 협공이 된다.
                Combat.Burst(origin, ShockwaveRadius, _stats.SwapShockwaveDamage, HitSource.Shadow);
                Combat.Burst(destination, ShockwaveRadius, _stats.SwapShockwaveDamage, HitSource.Player);
                _game.ShowBlast(origin, ShockwaveRadius, new Color(0.7f, 0.5f, 1f, 0.5f));
                _game.ShowBlast(destination, ShockwaveRadius, new Color(1f, 1f, 1f, 0.5f));
            }
        }

        private void FixedUpdate()
        {
            bool grounded = IsGrounded();
            if (grounded) _coyoteTimer = CoyoteTime;
            else if (_coyoteTimer > 0f) _coyoteTimer -= Time.fixedDeltaTime;

            Vector2 velocity = _body.linearVelocity;

            if (_dashTimer > 0f)
            {
                // 대시 중에는 수평으로만 빠르게 움직이고 중력을 무시한다.
                _dashTimer -= Time.fixedDeltaTime;
                velocity = new Vector2(_dashDirection * DashSpeed, 0f);
            }
            else
            {
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
            }

            _body.linearVelocity = velocity;

            // 지금 상태를 기록하고, 지연 시간만큼 과거의 상태를 분신에게 재현시킨다.
            var current = new ShadowFrame(_body.position, _facing, _attackedThisTick);
            _attackedThisTick = false;
            float shadowDamage = _stats.PlayerDamage * _stats.ShadowDamageMultiplier;
            for (int i = 0; i < _shadows.Count; i++)
            {
                if (_shadows[i].Recorder.Push(current, out ShadowFrame delayed))
                {
                    _shadows[i].Clone.Replay(delayed, shadowDamage);
                }
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
            // 대시 중에는 피해를 받지 않는다.
            if (_invulnerableTimer > 0f || _dashTimer > 0f || _health <= 0) return;

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

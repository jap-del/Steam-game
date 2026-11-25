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
        private const int TrailDotCount = 10;
        /// <summary>발판을 뚫고 내려갈 때 아래로 밀어 주는 속도.</summary>
        private const float DropSpeed = 4f;

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
        private readonly List<SpriteRenderer> _trailDots = new List<SpriteRenderer>();
        /// <summary>지금 뚫고 내려가는 중인 발판들. 몸이 발판에서 완전히 벗어나면 다시 밟을 수 있게 된다.</summary>
        private readonly List<Collider2D> _dropThrough = new List<Collider2D>();
        private bool _dropRequested;
        private float _anchorTimer;
        private float _anchorCooldownTimer;
        private bool _shadowsAnchored;
        /// <summary>보스가 분신을 그 자리에 묶어 둔 남은 시간. 묶인 분신은 고정된 분신처럼 움직이지 않는다.</summary>
        private float _holdTimer;
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
        private Vector2 _lastSafePosition;

        public int Health => _health;
        public Collider2D Collider => _collider;
        public Vector2 Position => _body != null ? _body.position : (Vector2)transform.position;
        public float SwapCooldownRemaining => Mathf.Max(0f, _swapTimer);
        public float DashCooldownRemaining => Mathf.Max(0f, _dashCooldownTimer);
        public float ShadowFill => _shadows.Count > 0 ? _shadows[0].Recorder.Fill : 0f;
        public bool AreShadowsAnchored => _shadowsAnchored;
        /// <summary>보고 있는 쪽. 오른쪽이 1, 왼쪽이 -1.</summary>
        public int Facing => _facing;
        public float AnchorRemaining => Mathf.Max(0f, _anchorTimer);
        public float AnchorCooldownRemaining => Mathf.Max(0f, _anchorCooldownTimer);

        public void Build(PrototypeGame game, RunStats stats, int startHealth)
        {
            _game = game;
            _stats = stats;
            _health = Mathf.Clamp(startHealth, 1, stats.MaxHealth);
            _lastSafePosition = transform.position;

            _sprite = PrototypeArt.CreateBox("Body", transform, Vector2.zero, BodySize, BodyColor, 10);
            PixelSprites.Dress(_sprite, "Player", BodySize);
            _slash = PrototypeArt.CreateBox("Slash", transform, Vector2.zero, Combat.SlashSize, new Color(1f, 1f, 1f, 0.7f), 12);
            PixelSprites.Dress(_slash, "Slash", Combat.SlashSize);
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
            _shadowsAnchored = false;
            _anchorTimer = 0f;
            _holdTimer = 0f;

            for (int i = 0; i < _trailDots.Count; i++)
            {
                if (_trailDots[i] != null) Destroy(_trailDots[i].gameObject);
            }
            _trailDots.Clear();
            for (int i = 0; i < TrailDotCount; i++)
            {
                // 분신이 앞으로 지나갈 길을 점으로 보여 준다.
                float alpha = Mathf.Lerp(0.45f, 0.12f, i / (float)(TrailDotCount - 1));
                SpriteRenderer dot = PrototypeArt.CreateBox("ShadowTrail", transform.parent, Vector2.zero, new Vector2(0.14f, 0.14f), new Color(0.7f, 0.5f, 1f, alpha), 6);
                dot.enabled = false;
                _trailDots.Add(dot);
            }

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

        /// <summary>플레이어의 발 위치.</summary>
        public Vector2 Feet => Position + new Vector2(0f, -BodySize.y * 0.5f);

        /// <summary>첫 번째 분신이 나타나 있으면 그 자리를 알려 준다. 보스의 표식이 분신에게 붙는다.</summary>
        public bool TryGetShadow(out Vector2 position)
        {
            position = Vector2.zero;
            if (_shadows.Count == 0 || _shadows[0].Clone == null || !_shadows[0].Clone.IsActive) return false;
            position = _shadows[0].Clone.Position;
            return true;
        }

        /// <summary>내가 분신과 겹쳐 서 있는지. 백광의 군주가 내리는 심판의 빛은 분신의 그림자 속까지는 닿지 않는다.</summary>
        public bool IsInShadow()
        {
            for (int i = 0; i < _shadows.Count; i++)
            {
                ShadowClone clone = _shadows[i].Clone;
                if (clone == null || !clone.IsActive) continue;

                Vector2 offset = clone.Position - Position;
                if (Mathf.Abs(offset.x) <= 0.75f && Mathf.Abs(offset.y) <= 0.9f) return true;
            }
            return false;
        }

        /// <summary>나타나 있는 분신 중 하나라도 발이 그 범위 안에 있는지 확인한다. 퍼즐 발판에 쓴다.</summary>
        public bool IsAnyShadowStandingAt(Vector2 point, float halfWidth, float tolerance)
        {
            for (int i = 0; i < _shadows.Count; i++)
            {
                ShadowClone clone = _shadows[i].Clone;
                if (clone == null || !clone.IsActive) continue;

                Vector2 feet = clone.Position + new Vector2(0f, -BodySize.y * 0.5f);
                if (Mathf.Abs(feet.x - point.x) <= halfWidth && Mathf.Abs(feet.y - point.y) <= tolerance)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 분신을 seconds 동안 지금 자리에 묶어 둔다. 태엽 기사의 감옥이 쓴다.
        /// 묶인 분신은 따라 하지 않고 그 자리에 있으며, 자리를 바꾸면 고정된 분신처럼 내가 있던 자리로 옮겨 간다.
        /// </summary>
        public void HoldShadows(float seconds)
        {
            if (_shadows.Count == 0 || !_shadows[0].Clone.IsActive) return;
            _holdTimer = Mathf.Max(_holdTimer, seconds);
            for (int i = 0; i < _shadows.Count; i++)
            {
                _shadows[i].Clone.SetAnchored(true);
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
            if (_anchorCooldownTimer > 0f) _anchorCooldownTimer -= Time.deltaTime;

            if (_shadowsAnchored)
            {
                _anchorTimer -= Time.deltaTime;
                if (_anchorTimer <= 0f) ReleaseAnchor();
            }

            if (_holdTimer > 0f)
            {
                _holdTimer -= Time.deltaTime;
                if (_holdTimer <= 0f && !_shadowsAnchored)
                {
                    for (int i = 0; i < _shadows.Count; i++)
                    {
                        _shadows[i].Clone.SetAnchored(false);
                    }
                }
            }

            if (_slashTimer > 0f)
            {
                _slashTimer -= Time.deltaTime;
                if (_slashTimer <= 0f) _slash.enabled = false;
            }

            _sprite.color = _dashTimer > 0f ? DashColor : BodyColor;
            PixelSprites.Face(_sprite, _facing);

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
            bool interactPressed = false;
            bool anchorPressed = false;
            bool downHeld = false;

            if (keyboard != null)
            {
                // 키보드 키는 Esc 설정 화면의 키 설정에서 바꿀 수 있다(KeyBindings).
                if (KeyBindings.Held(GameAction.MoveLeft)) move -= 1f;
                if (KeyBindings.Held(GameAction.MoveRight)) move += 1f;
                jumpPressed |= KeyBindings.Pressed(GameAction.Jump);
                jumpHeld |= KeyBindings.Held(GameAction.Jump);
                attackPressed |= KeyBindings.Pressed(GameAction.Attack);
                swapPressed |= KeyBindings.Pressed(GameAction.Swap);
                dashPressed |= KeyBindings.Pressed(GameAction.Dash);
                interactPressed |= KeyBindings.Pressed(GameAction.Interact);
                anchorPressed |= KeyBindings.Pressed(GameAction.Anchor);
                downHeld |= KeyBindings.Held(GameAction.Down);
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
                interactPressed |= gamepad.leftShoulder.wasPressedThisFrame;
                anchorPressed |= gamepad.leftTrigger.wasPressedThisFrame;
                downHeld |= gamepad.leftStick.ReadValue().y < -0.5f || gamepad.dpad.down.isPressed;
            }

            _moveInput = Mathf.Clamp(move, -1f, 1f);
            _jumpHeld = jumpHeld;
            if (_dashTimer <= 0f)
            {
                if (_moveInput > 0.01f) _facing = 1;
                else if (_moveInput < -0.01f) _facing = -1;
            }

            // 아래를 누른 채 점프하면 밟고 있는 발판 아래로 내려간다. 발판이 아닌 땅 위라면 그냥 점프한다.
            if (jumpPressed && downHeld) _dropRequested = true;
            else if (jumpPressed) _jumpBufferTimer = JumpBufferTime;
            if (dashPressed) TryDash();
            if (attackPressed) TryAttack();
            if (anchorPressed) TryAnchor();
            if (swapPressed) TrySwap();
            if (interactPressed) _game.OnInteract();
        }

        /// <summary>
        /// 분신을 지금 자리에 잠깐 고정한다. 정해진 시간이 지나면 저절로 풀리고, 그 뒤로 대기시간이 돈다.
        /// 고정된 분신은 내가 공격할 때 함께 공격하므로, 적을 사이에 두면 바로 협공이 된다.
        /// </summary>
        private void TryAnchor()
        {
            if (_shadowsAnchored || _anchorCooldownTimer > 0f) return;
            if (_shadows.Count == 0 || !_shadows[0].Clone.IsActive) return;

            _shadowsAnchored = true;
            _anchorTimer = _stats.AnchorDuration;
            for (int i = 0; i < _shadows.Count; i++)
            {
                _shadows[i].Clone.SetAnchored(true);
            }
            _game.ShowPopup(_shadows[0].Clone.Position + Vector2.up * 1.2f, "분신 고정", new Color(0.75f, 0.6f, 1f, 1f));
            Sfx.Play(Sound.Anchor);
        }

        private void ReleaseAnchor()
        {
            _shadowsAnchored = false;
            _anchorTimer = 0f;
            _anchorCooldownTimer = _stats.AnchorCooldown;
            for (int i = 0; i < _shadows.Count; i++)
            {
                // 보스가 묶어 둔 분신은 묶인 시간이 끝날 때까지 그대로 있다.
                _shadows[i].Clone.SetAnchored(_holdTimer > 0f);
            }
        }

        private void TryDash()
        {
            if (_dashCooldownTimer > 0f || _dashTimer > 0f) return;

            _dashTimer = DashDuration;
            _dashCooldownTimer = _stats.DashCooldown;
            _dashDirection = _facing;
            Sfx.Play(Sound.Dash);
        }

        private void TryAttack()
        {
            if (_attackTimer > 0f) return;

            _attackTimer = AttackCooldown;
            _attackedThisTick = true;
            Sfx.Play(Sound.Slash, 0.8f);
            Combat.Slash(_body.position, _facing, _stats.PlayerDamage, HitSource.Player);

            if (_shadowsAnchored)
            {
                float shadowDamage = _stats.PlayerDamage * _stats.ShadowDamageMultiplier;
                for (int i = 0; i < _shadows.Count; i++)
                {
                    _shadows[i].Clone.AttackNow(_facing, shadowDamage);
                }
            }

            _slash.transform.localPosition = new Vector3(_facing * Combat.SlashReach, 0f, 0f);
            PixelSprites.Face(_slash, _facing);
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
            // 고정되거나 묶인 분신은 내가 있던 자리로 옮겨 가서 정말로 자리가 바뀐다.
            if (_shadowsAnchored || _holdTimer > 0f) _shadows[0].Clone.MoveTo(origin);
            _game.ShowPopup(destination + Vector2.up * 1.2f, "자리 바꾸기", new Color(0.75f, 0.6f, 1f, 1f));
            Sfx.Play(Sound.Swap);
            _game.Feel.Sparks(origin, new Color(0.75f, 0.6f, 1f, 1f), 5, 4f);
            _game.Feel.Sparks(destination, new Color(0.75f, 0.6f, 1f, 1f), 5, 4f);
            _game.OnPlayerSwapped();

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
            if (_health <= 0) return;

            if (_body.position.y < StageBuilder.KillY)
            {
                FallIntoPit();
                return;
            }

            RestoreDroppedPlatforms();

            bool onGlass;
            bool grounded = IsGrounded(out onGlass);
            if (_dropRequested)
            {
                _dropRequested = false;
                if (grounded && _dashTimer <= 0f && DropThroughPlatform())
                {
                    grounded = false;
                }
                else
                {
                    _jumpBufferTimer = JumpBufferTime;
                }
            }
            if (grounded)
            {
                _coyoteTimer = CoyoteTime;
                // 유리 발판은 곧 깨지므로 돌아올 자리로 기억하지 않는다.
                if (_dashTimer <= 0f && _knockbackTimer <= 0f && !onGlass) _lastSafePosition = _body.position;
            }
            else if (_coyoteTimer > 0f)
            {
                _coyoteTimer -= Time.fixedDeltaTime;
            }

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
                    Sfx.Play(Sound.Jump, 0.7f);
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
            UpdateTrail();
        }

        /// <summary>
        /// 발밑의 한쪽 방향 발판(아래에서 뚫고 올라갈 수 있는 발판)을 뚫고 내려간다. 내려갈 발판이 없으면 false.
        /// 발판과의 충돌만 잠시 끄므로, 바로 아래에 다른 발판이나 땅이 있으면 거기에 내려선다.
        /// </summary>
        private bool DropThroughPlatform()
        {
            // 발판 끝에 걸쳐 서 있어도 찾도록 발바닥 너비만큼 살핀다.
            Vector2 below = _body.position + new Vector2(0f, -BodySize.y * 0.5f - 0.1f);
            Collider2D[] hits = Physics2D.OverlapBoxAll(below, new Vector2(BodySize.x * 0.95f, 0.2f), 0f);
            int found = 0;
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null || hit == _collider || hit.isTrigger) continue;
                if (hit.usedByEffector && hit.GetComponent<PlatformEffector2D>() != null)
                {
                    found++;
                }
                else if (hit.attachedRigidbody == null)
                {
                    // 보통 땅을 함께 밟고 있으면 내려갈 수 없다.
                    return false;
                }
            }
            if (found == 0) return false;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null || hit.isTrigger || !hit.usedByEffector || hit.GetComponent<PlatformEffector2D>() == null) continue;
                if (_dropThrough.Contains(hit)) continue;
                Physics2D.IgnoreCollision(_collider, hit, true);
                _dropThrough.Add(hit);
            }

            // 가만히 서 있던 몸은 물리 엔진이 재워 둘 수 있다. 깨우고 아래로 살짝 밀어 준다.
            _body.WakeUp();
            _body.linearVelocity = new Vector2(_body.linearVelocity.x, -DropSpeed);
            _coyoteTimer = 0f;
            _jumpBufferTimer = 0f;
            return true;
        }

        /// <summary>
        /// 뚫고 내려간 발판과 몸이 완전히 떨어졌으면 다시 밟을 수 있게 한다.
        /// 몸이 발판에 걸친 채로 되돌리면 물리 엔진이 몸을 발판 위로 밀어 올리므로 기다린다.
        /// </summary>
        private void RestoreDroppedPlatforms()
        {
            for (int i = _dropThrough.Count - 1; i >= 0; i--)
            {
                Collider2D platform = _dropThrough[i];
                if (platform == null)
                {
                    _dropThrough.RemoveAt(i);
                    continue;
                }
                Bounds platformBounds = platform.bounds;
                platformBounds.Expand(new Vector3(0.1f, 0.4f, 0f));
                if (_collider.bounds.Intersects(platformBounds)) continue;
                // 그사이 깨진 유리 발판이면 다시 생길 때까지 기다렸다가 되돌린다.
                if (!platform.enabled) continue;
                Physics2D.IgnoreCollision(_collider, platform, false);
                _dropThrough.RemoveAt(i);
            }
        }

        /// <summary>첫 번째 분신이 앞으로 지나갈 길에 점을 찍는다. 고정 중이거나 분신이 아직 없으면 숨긴다.</summary>
        private void UpdateTrail()
        {
            bool show = _shadows.Count > 0 && !_shadowsAnchored && _holdTimer <= 0f && _shadows[0].Clone.IsActive;
            ShadowRecorder recorder = show ? _shadows[0].Recorder : null;
            for (int i = 0; i < _trailDots.Count; i++)
            {
                SpriteRenderer dot = _trailDots[i];
                if (dot == null) continue;

                ShadowFrame frame = default;
                bool visible = show && recorder.TryPeek((i + 1) * recorder.DelayTicks / (TrailDotCount + 1), out frame);
                dot.enabled = visible;
                if (visible) dot.transform.position = new Vector3(frame.Position.x, frame.Position.y, 0f);
            }
        }

        /// <summary>발 아래쪽에 위아래 방향으로 맞닿은 면이 있으면 땅에 서 있는 것으로 본다.</summary>
        private bool IsGrounded(out bool onGlass)
        {
            onGlass = false;
            bool grounded = false;
            int count = _body.GetContacts(_contacts);
            float feetLine = _body.position.y - BodySize.y * 0.25f;
            for (int i = 0; i < count; i++)
            {
                if (Mathf.Abs(_contacts[i].normal.y) > 0.5f && _contacts[i].point.y < feetLine)
                {
                    grounded = true;
                    Collider2D ground = _contacts[i].collider;
                    if (ground != null && ground.GetComponent<GlassPlatform>() != null) onGlass = true;
                }
            }
            return grounded;
        }

        /// <summary>구덩이에 빠지면 체력을 1 잃고 마지막으로 서 있던 땅으로 돌아간다.</summary>
        private void FallIntoPit()
        {
            _health -= 1;
            _game.Feel.PlayerHurt();
            if (_health <= 0)
            {
                _game.RequestRestart();
                return;
            }

            Vector2 destination = _lastSafePosition + Vector2.up * 0.1f;
            _body.position = destination;
            transform.position = new Vector3(destination.x, destination.y, 0f);
            _body.linearVelocity = Vector2.zero;
            _dashTimer = 0f;
            _knockbackTimer = 0f;
            _invulnerableTimer = InvulnerableTime;
            _game.ShowPopup(destination + Vector2.up * 1.2f, "-1", new Color(1f, 0.4f, 0.4f, 1f));
        }

        /// <summary>
        /// 피해를 실제로 받았으면 true. 대시 중이거나 방금 맞아서 무적이면 false.
        /// unavoidable이면 대시나 무적 시간으로 피할 수 없다. 보스의 심판처럼 방 전체를 덮는 공격에 쓴다.
        /// </summary>
        public bool TakeDamage(int amount, float sourceX, bool unavoidable = false)
        {
            if (_health <= 0) return false;
            if (!unavoidable && (_invulnerableTimer > 0f || _dashTimer > 0f)) return false;

            _health -= amount;
            _invulnerableTimer = InvulnerableTime;
            _knockbackTimer = 0.2f;
            float direction = _body.position.x >= sourceX ? 1f : -1f;
            _body.linearVelocity = new Vector2(direction * 6f, 7f);
            _game.ShowPopup(_body.position + Vector2.up * 1.2f, "-" + amount, new Color(1f, 0.4f, 0.4f, 1f));
            _game.Feel.PlayerHurt();

            if (_health <= 0)
            {
                _game.RequestRestart();
            }
            return true;
        }
    }
}

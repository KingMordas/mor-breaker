using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MorBreaker
{
    /// <summary>
    /// Bouncing ball for morBreaker.
    /// Sits on the paddle until launched, then keeps a speed that ramps up
    /// each time it bounces off a wall or chips (without breaking) a brick.
    /// The per-hit ramp grows with the level; the speed is capped and is
    /// reset to the level base whenever the ball is lost. Hitting the paddle
    /// applies "english" based on where it lands (and never accelerates).
    /// Input is read via the Input System package. Stores no data of any kind.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class BallController : MonoBehaviour
    {
        /// <summary>Raised when the ball is launched off the paddle.</summary>
        public static event Action Launched;

        /// <summary>Raised when the ball falls past the bottom (a life is lost).</summary>
        public static event Action Lost;

        [Header("Speed")]
        [Tooltip("Base travel speed (world units/sec) at the start of a life, before any bounce acceleration.")]
        [SerializeField] private float speed = 9f;

        [Tooltip("Fraction added to the speed on every qualifying bounce (wall or survived brick). Set per level by the GameManager.")]
        [SerializeField] private float accelPerHit = 0.004f;

        [Tooltip("Hard ceiling on bounce acceleration, as a multiple of the level base speed.")]
        [SerializeField] private float maxSpeedMultiplier = 2.0f;

        [Tooltip("Minimum absolute vertical direction (0..1). Stops the ball from getting stuck moving sideways.")]
        [SerializeField] private float minVerticalFraction = 0.25f;

        [Header("Launch")]
        [Tooltip("Max horizontal tilt of the initial launch, in degrees from straight up.")]
        [SerializeField] private float launchSpreadDeg = 35f;

        [Tooltip("Vertical offset above the paddle while the ball is resting on it.")]
        [SerializeField] private float restOffsetY = 0.45f;

        [Header("Paddle control")]
        [Tooltip("How strongly the paddle hit position steers the bounce (0 = pure reflection, 1 = full steer).")]
        [Range(0f, 1f)]
        [SerializeField] private float paddleInfluence = 0.6f;

        [Header("Bounds safety net")]
        [Tooltip("Below this Y the ball counts as lost (a backstop in case it slips past the paddle).")]
        [SerializeField] private float deathY = -5.6f;

        [Tooltip("Hard horizontal limit; the ball is reflected back if a physics pop pushes it past this.")]
        [SerializeField] private float boundX = 3.25f;

        [Tooltip("Hard ceiling; the ball is reflected back if a physics pop pushes it past this.")]
        [SerializeField] private float ceilingY = 5.6f;

        private Rigidbody2D _rb;
        private Transform _paddle;
        private Collider2D _paddleCol;
        private bool _launched;
        private bool _canLaunch = true;
        private float _levelBaseSpeed = 9f;

        /// <summary>Current live travel speed; clamped to a sane minimum.</summary>
        public float Speed
        {
            get => speed;
            set => speed = Mathf.Max(1f, value);
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.linearDamping = 0f;
            _rb.angularDamping = 0f;
            _rb.freezeRotation = true;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            _levelBaseSpeed = speed;

            var paddleGo = GameObject.FindGameObjectWithTag("Player");
            if (paddleGo != null)
            {
                _paddle = paddleGo.transform;
                _paddleCol = paddleGo.GetComponent<Collider2D>();
            }
        }

        private void OnEnable() => BrickController.Damaged += OnBrickDamaged;
        private void OnDisable() => BrickController.Damaged -= OnBrickDamaged;

        private void Start() => ResetToPaddle();

        /// <summary>
        /// Set the per-level base speed and the per-bounce acceleration.
        /// Resets the live speed to the new base. Difficulty (point 2) comes
        /// from raising <paramref name="perHitAccel"/> as the level climbs.
        /// </summary>
        public void ConfigureLevel(float baseSpeed, float perHitAccel)
        {
            _levelBaseSpeed = Mathf.Max(1f, baseSpeed);
            accelPerHit = Mathf.Max(0f, perHitAccel);
            speed = _levelBaseSpeed;
        }

        /// <summary>Speed up by one bounce step, capped at the per-level maximum.</summary>
        private void Accelerate()
        {
            if (!_launched) return;
            float max = _levelBaseSpeed * Mathf.Max(1f, maxSpeedMultiplier);
            speed = Mathf.Min(speed * (1f + accelPerHit), max);
        }

        private void OnBrickDamaged(BrickController brick) => Accelerate();

        private void Update()
        {
            if (_launched) return;
            StickToPaddle();
            if (_canLaunch && LaunchPressed())
                Launch();
        }

        private void FixedUpdate()
        {
            if (!_launched) return;
            Vector2 v = _rb.linearVelocity;
            if (v.sqrMagnitude < 0.0001f) v = Vector2.up;

            Vector2 dir = v.normalized;
            // Prevent near-horizontal stalling.
            if (Mathf.Abs(dir.y) < minVerticalFraction)
            {
                dir.y = Mathf.Sign(dir.y == 0f ? 1f : dir.y) * minVerticalFraction;
                dir.x = Mathf.Sign(dir.x == 0f ? 1f : dir.x) * (1f - minVerticalFraction);
                dir.Normalize();
            }
            _rb.linearVelocity = dir * speed;

            ContainWithinPlayfield();
        }

        /// <summary>
        /// Safety net: guarantees the ball can never leave the playfield or be
        /// lost forever, even if the physics solver pops it out of the brick grid
        /// (continuous collision does not protect against trigger tunnelling).
        /// </summary>
        private void ContainWithinPlayfield()
        {
            Vector2 p = _rb.position;
            if (p.y < deathY)
            {
                LoseBall();
                return;
            }

            Vector2 vel = _rb.linearVelocity;
            bool corrected = false;

            if (p.x < -boundX) { p.x = -boundX; if (vel.x < 0f) vel.x = -vel.x; corrected = true; }
            else if (p.x > boundX) { p.x = boundX; if (vel.x > 0f) vel.x = -vel.x; corrected = true; }
            if (p.y > ceilingY) { p.y = ceilingY; if (vel.y > 0f) vel.y = -vel.y; corrected = true; }

            if (corrected)
            {
                _rb.position = p;
                _rb.linearVelocity = vel.normalized * speed;
            }
        }

        private void LoseBall()
        {
            Lost?.Invoke();
            ResetToPaddle();
        }

        /// <summary>Place the ball on the paddle and allow the player to launch it.</summary>
        public void Arm()
        {
            ResetToPaddle();
            _canLaunch = true;
        }

        /// <summary>Park the ball on the paddle and block launching (game over / between levels).</summary>
        public void Hold()
        {
            ResetToPaddle();
            _canLaunch = false;
        }

        /// <summary>Place the ball on the paddle and stop it. Resets bounce speed to the level base.</summary>
        public void ResetToPaddle()
        {
            _launched = false;
            speed = _levelBaseSpeed;
            _rb.linearVelocity = Vector2.zero;
            _rb.bodyType = RigidbodyType2D.Kinematic;
            StickToPaddle();
        }

        private void StickToPaddle()
        {
            if (_paddle == null) return;
            transform.position = new Vector2(_paddle.position.x, _paddle.position.y + restOffsetY);
        }

        private static bool LaunchPressed()
        {
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame) return true;

            var k = Keyboard.current;
            if (k != null && (k.spaceKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame)) return true;

            return false;
        }

        private void Launch()
        {
            _launched = true;
            _rb.bodyType = RigidbodyType2D.Dynamic;
            float tilt = UnityEngine.Random.Range(-launchSpreadDeg, launchSpreadDeg);
            Vector2 dir = Quaternion.Euler(0f, 0f, tilt) * Vector2.up;
            _rb.linearVelocity = dir.normalized * speed;
            Launched?.Invoke();
        }

        private void OnCollisionEnter2D(Collision2D col)
        {
            if (_paddleCol != null && col.collider == _paddleCol)
            {
                float halfWidth = Mathf.Max(0.01f, _paddleCol.bounds.extents.x);
                float offset = Mathf.Clamp((transform.position.x - _paddle.position.x) / halfWidth, -1f, 1f);

                Vector2 reflected = _rb.linearVelocity.normalized;
                Vector2 steered = new Vector2(offset, 1f).normalized;
                Vector2 dir = Vector2.Lerp(reflected, steered, paddleInfluence).normalized;
                dir.y = Mathf.Max(dir.y, minVerticalFraction); // always send it upward
                _rb.linearVelocity = dir.normalized * speed;
                return;
            }

            // Bricks handle their own damage and raise Damaged -> Accelerate, so skip them here
            // (their break-vs-survive outcome isn't known at this point). Everything else that is
            // not the paddle is a wall/ceiling, which accelerates the ball.
            if (col.gameObject.GetComponent<BrickController>() != null) return;
            Accelerate();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Backstop for the position-based check in ContainWithinPlayfield.
            // Guarded by _launched so it can only fire once per life.
            if (_launched && other.gameObject.name == "DeathZone")
                LoseBall();
        }
    }
}

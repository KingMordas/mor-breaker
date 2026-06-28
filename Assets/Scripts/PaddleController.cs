using UnityEngine;
using UnityEngine.InputSystem;

namespace MorBreaker
{
    /// <summary>
    /// Horizontal paddle movement for morBreaker.
    /// Supports keyboard (A/D or arrows) and pointer (mouse / touch) control
    /// via the Input System package. Clamped to the playfield bounds.
    /// Stores no data of any kind.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PaddleController : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Keyboard movement speed in world units per second.")]
        [SerializeField] private float keyboardSpeed = 12f;

        [Tooltip("How quickly the paddle eases toward the pointer position.")]
        [SerializeField] private float pointerLerp = 25f;

        [Tooltip("If true, the paddle follows the mouse / touch horizontal position.")]
        [SerializeField] private bool followPointer = true;

        [Header("Bounds")]
        [Tooltip("Half-width of the paddle's reachable area, measured from x = 0.")]
        [SerializeField] private float playHalfWidth = 3.15f;

        private Rigidbody2D _rb;
        private Camera _camera;
        private float _halfPaddleWidth;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.gravityScale = 0f;
            _camera = Camera.main;

            var col = GetComponent<Collider2D>();
            _halfPaddleWidth = col != null ? col.bounds.extents.x : transform.localScale.x * 0.5f;
        }

        private void FixedUpdate()
        {
            float targetX = transform.position.x;

            // Pointer control takes priority when the user is actively pointing.
            if (followPointer && _camera != null && PointerHeld(out Vector2 screenPos))
            {
                Vector3 world = _camera.ScreenToWorldPoint(screenPos);
                targetX = Mathf.Lerp(transform.position.x, world.x, pointerLerp * Time.fixedDeltaTime);
            }
            else
            {
                targetX += KeyboardAxis() * keyboardSpeed * Time.fixedDeltaTime;
            }

            float limit = Mathf.Max(0f, playHalfWidth - _halfPaddleWidth);
            targetX = Mathf.Clamp(targetX, -limit, limit);

            _rb.MovePosition(new Vector2(targetX, transform.position.y));
        }

        /// <summary>True while a mouse button / touch is held; outputs the screen position.</summary>
        private static bool PointerHeld(out Vector2 screenPos)
        {
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.isPressed)
            {
                screenPos = pointer.position.ReadValue();
                return true;
            }
            screenPos = default;
            return false;
        }

        /// <summary>-1 / 0 / +1 horizontal input from A/D or arrow keys.</summary>
        private static float KeyboardAxis()
        {
            var k = Keyboard.current;
            if (k == null) return 0f;
            float axis = 0f;
            if (k.leftArrowKey.isPressed || k.aKey.isPressed) axis -= 1f;
            if (k.rightArrowKey.isPressed || k.dKey.isPressed) axis += 1f;
            return axis;
        }
    }
}

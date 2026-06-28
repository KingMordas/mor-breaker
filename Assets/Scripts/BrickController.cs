using System;
using UnityEngine;

namespace MorBreaker
{
    /// <summary>
    /// A single destructible brick for morBreaker.
    /// Takes a fixed number of hits, changing colour as it weakens,
    /// then removes itself and reports the score it was worth.
    /// Stores no data of any kind.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    public class BrickController : MonoBehaviour
    {
        /// <summary>Raised when a brick is destroyed, carrying its point value. In-memory only.</summary>
        public static event Action<int> Destroyed;

        /// <summary>Raised whenever a brick is hit (before it may be destroyed).</summary>
        public static event Action<BrickController> Hit;

        /// <summary>Raised when a brick is hit but survives (did not break). Used to accelerate the ball.</summary>
        public static event Action<BrickController> Damaged;

        [Header("Durability")]
        [Tooltip("How many ball hits this brick can take before breaking.")]
        [SerializeField] private int hitPoints = 1;

        [Tooltip("Score awarded when the brick is destroyed (multiplied by starting hit points).")]
        [SerializeField] private int basePoints = 50;

        [Header("Appearance")]
        [Tooltip("Colour per remaining hit point. Index 0 = 1 hit left, last index used for anything higher.")]
        [SerializeField] private Color[] hitColors =
        {
            new Color(0.40f, 0.85f, 1.00f), // 1 hit
            new Color(0.45f, 0.95f, 0.55f), // 2 hits
            new Color(1.00f, 0.80f, 0.30f), // 3 hits
            new Color(1.00f, 0.45f, 0.45f), // 4+ hits
        };

        private SpriteRenderer _renderer;
        private int _startingHitPoints;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _startingHitPoints = Mathf.Max(1, hitPoints);
            ApplyColor();
        }

        /// <summary>Configure durability at spawn time (used by the grid spawner).</summary>
        public void Init(int hits)
        {
            hitPoints = Mathf.Max(1, hits);
            _startingHitPoints = hitPoints;
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            ApplyColor();
        }

        private void OnCollisionEnter2D(Collision2D col)
        {
            // Only the ball damages bricks.
            if (col.gameObject.GetComponent<BallController>() == null) return;
            TakeHit();
        }

        /// <summary>Apply one hit; weaken or destroy the brick.</summary>
        public void TakeHit()
        {
            hitPoints--;
            Hit?.Invoke(this);

            if (hitPoints <= 0)
            {
                Destroyed?.Invoke(basePoints * _startingHitPoints);
                Destroy(gameObject);
                return;
            }

            // Survived the hit: weaken, recolour, and signal so the ball can speed up.
            ApplyColor();
            Damaged?.Invoke(this);
        }

        private void ApplyColor()
        {
            if (_renderer == null || hitColors == null || hitColors.Length == 0) return;
            int idx = Mathf.Clamp(hitPoints - 1, 0, hitColors.Length - 1);
            _renderer.color = hitColors[idx];
        }
    }
}

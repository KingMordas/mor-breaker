using System;
using UnityEngine;

namespace MorBreaker
{
    /// <summary>
    /// Lays out a grid of bricks at runtime for morBreaker.
    /// Keeps the scene lean (no baked grid) and tracks how many bricks
    /// remain so a level-clear can be detected. Stores no data of any kind.
    /// </summary>
    public class BrickSpawner : MonoBehaviour
    {
        /// <summary>Raised when every brick in the current layout has been destroyed.</summary>
        public static event Action LevelCleared;

        [Header("Prefab")]
        [Tooltip("Brick prefab to instantiate. Must carry a BrickController.")]
        [SerializeField] private BrickController brickPrefab;

        [Header("Grid")]
        [SerializeField] private int columns = 7;
        [SerializeField] private int rows = 5;

        [Tooltip("World size of a single brick (must match the prefab's scale).")]
        [SerializeField] private Vector2 brickSize = new Vector2(0.8f, 0.32f);

        [Tooltip("Empty space between adjacent bricks.")]
        [SerializeField] private Vector2 spacing = new Vector2(0.06f, 0.06f);

        [Tooltip("Y position of the centre of the top row.")]
        [SerializeField] private float topY = 4.6f;

        [Header("Durability pattern")]
        [Tooltip("Hit points per row, from the top row down. Reused cyclically if shorter than the row count.")]
        [SerializeField] private int[] hitPointsPerRow = { 3, 2, 2, 1, 1 };

        [Tooltip("Spawn the grid automatically on Start.")]
        [SerializeField] private bool spawnOnStart = true;

        private Transform _container;
        private int _aliveCount;

        private void OnEnable() => BrickController.Destroyed += OnBrickDestroyed;
        private void OnDisable() => BrickController.Destroyed -= OnBrickDestroyed;

        private void Start()
        {
            if (spawnOnStart) Spawn();
        }

        /// <summary>Apply a level layout (columns + per-row hit points) and build it.</summary>
        public void Build(LevelDefinition def)
        {
            if (def.HitPointsPerRow != null && def.HitPointsPerRow.Length > 0)
            {
                columns = Mathf.Max(1, def.Columns);
                rows = def.HitPointsPerRow.Length;
                hitPointsPerRow = def.HitPointsPerRow;
            }
            Spawn();
        }

        /// <summary>Clear any existing bricks and build a fresh grid.</summary>
        public void Spawn()
        {
            if (brickPrefab == null)
            {
                Debug.LogWarning("BrickSpawner: no brick prefab assigned.");
                return;
            }

            ClearContainer();

            _container = new GameObject("Bricks").transform;
            _container.SetParent(transform, false);

            float stepX = brickSize.x + spacing.x;
            float stepY = brickSize.y + spacing.y;
            float totalWidth = columns * brickSize.x + (columns - 1) * spacing.x;
            float startX = -totalWidth * 0.5f + brickSize.x * 0.5f;

            _aliveCount = 0;
            for (int r = 0; r < rows; r++)
            {
                int hits = HitsForRow(r);
                float y = topY - r * stepY;
                for (int c = 0; c < columns; c++)
                {
                    float x = startX + c * stepX;
                    var brick = Instantiate(brickPrefab, new Vector3(x, y, 0f), Quaternion.identity, _container);
                    brick.name = $"Brick_{r}_{c}";
                    brick.Init(hits);
                    _aliveCount++;
                }
            }
        }

        private int HitsForRow(int row)
        {
            if (hitPointsPerRow == null || hitPointsPerRow.Length == 0) return 1;
            return Mathf.Max(1, hitPointsPerRow[row % hitPointsPerRow.Length]);
        }

        private void OnBrickDestroyed(int points)
        {
            _aliveCount = Mathf.Max(0, _aliveCount - 1);
            if (_aliveCount == 0)
                LevelCleared?.Invoke();
        }

        private void ClearContainer()
        {
            if (_container != null)
            {
                Destroy(_container.gameObject);
                _container = null;
            }
        }
    }
}

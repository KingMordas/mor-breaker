using UnityEngine;

namespace MorBreaker
{
    /// <summary>
    /// Layout for a single level: a fixed column count and the hit points of
    /// each row from the top down. Pure design data. Stores no data of any kind.
    /// </summary>
    public readonly struct LevelDefinition
    {
        public readonly int Columns;
        /// <summary>Hit points per row, top row first. Length is the row count.</summary>
        public readonly int[] HitPointsPerRow;

        public int Rows => HitPointsPerRow == null ? 0 : HitPointsPerRow.Length;

        public LevelDefinition(int columns, int[] hitPointsPerRow)
        {
            Columns = columns;
            HitPointsPerRow = hitPointsPerRow;
        }
    }

    /// <summary>
    /// The hand-tuned 10-level difficulty curve for morBreaker. Tougher bricks
    /// sit on top; 2-hit bricks arrive at level 2, 3-hit at level 5, and the
    /// grid grows from 5 to 7 rows. The per-bounce speed ramp (point 2) also
    /// steepens with the level. All values are compile-time design constants —
    /// this type stores no player data of any kind.
    /// </summary>
    public static class LevelTable
    {
        /// <summary>Number of levels; clearing the last one wins the game.</summary>
        public const int Count = 10;

        // 7 columns throughout; rows grow 5 -> 7. Top row first.
        private static readonly LevelDefinition[] _levels =
        {
            new LevelDefinition(7, new[] { 1, 1, 1, 1, 1 }),          // L1  - all single-hit
            new LevelDefinition(7, new[] { 2, 1, 1, 1, 1 }),          // L2
            new LevelDefinition(7, new[] { 2, 2, 1, 1, 1, 1 }),       // L3
            new LevelDefinition(7, new[] { 2, 2, 2, 1, 1, 1 }),       // L4
            new LevelDefinition(7, new[] { 3, 2, 2, 1, 1, 1 }),       // L5  - first 3-hit
            new LevelDefinition(7, new[] { 3, 2, 2, 2, 1, 1 }),       // L6
            new LevelDefinition(7, new[] { 3, 3, 2, 2, 1, 1, 1 }),    // L7
            new LevelDefinition(7, new[] { 3, 3, 2, 2, 2, 1, 1 }),    // L8
            new LevelDefinition(7, new[] { 3, 3, 3, 2, 2, 1, 1 }),    // L9
            new LevelDefinition(7, new[] { 3, 3, 3, 2, 2, 2, 1 }),    // L10 - toughest mix
        };

        /// <summary>Layout for a 1-based level number, clamped to the valid range.</summary>
        public static LevelDefinition Get(int level)
        {
            int idx = Mathf.Clamp(level, 1, Count) - 1;
            return _levels[idx];
        }

        /// <summary>
        /// Per-bounce speed increase fraction for a level (point 2): 0.4% at
        /// level 1, rising 0.15% per level to 1.75% at level 10.
        /// </summary>
        public static float AccelPerHit(int level)
        {
            int l = Mathf.Clamp(level, 1, Count);
            return 0.004f + (l - 1) * 0.0015f;
        }

        /// <summary>True when this level is the final one.</summary>
        public static bool IsFinal(int level) => level >= Count;
    }
}

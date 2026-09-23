using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MyGame.Board;
using MyGame.Interaction;

namespace MyGame.Match
{
    /// <summary>
    /// Resolves matches asynchronously: animate merge → destroy → grow → cleanup → re-check.
    ///
    /// Growth rule: surviving sub-cubes grow to fill freed space, preferring to
    /// reach Whole as fast as possible. A Small can jump straight to Whole if
    /// the surrounding 2×2 sub-cells are free. A Half can grow to Whole if the
    /// adjacent row/column is free.
    /// </summary>
    public class SubCubeGrowthResolver
    {
        private readonly SubCubeMatchDetector _detector = new();
        private readonly List<MatchResult> _scratch = new();

        private readonly HashSet<DraggableCube> _affectedCubes = new();
        private readonly List<DraggableCube> _affectedList = new();

        private const int MaxIterations = 64;
        private const int MaxGrowthSteps = 4;   // Small → Half → Whole is 2 steps; 4 is safety headroom

        /// <summary>Fires whenever a cube is destroyed because it ran out of sub-cubes.</summary>
        public event System.Action<DraggableCube> CubeDestroyed;

        /// <summary>The animator used for merge visuals. Set by MatchManager.</summary>
        public MergeAnimator Animator { get; set; }

        #region Resolve Loop

        public IEnumerator ResolveAll(GridManager grid, int minMatchSize = 2, System.Action<int> onComplete = null)
        {
            int totalRemoved = 0;

            if (grid == null)
            {
                onComplete?.Invoke(0);
                yield break;
            }

            int iterations = 0;
            while (iterations++ < MaxIterations)
            {
                // 1. Detect
                _detector.FindMatches(grid.SubGrid, _scratch, minMatchSize);
                if (_scratch.Count == 0) break;

                // 2. Gather affected cubes, unregister from sub-grid, remove from shapes
                var pending = PrepareMatches(grid, _scratch);

                // 3. Animate merge for each group
                if (Animator != null && pending.totalSubCubes > 0)
                {
                    yield return Animator.AnimateBatch(pending.groups, null);
                    totalRemoved += pending.totalSubCubes;
                }
                else
                {
                    totalRemoved += DestroyPending(pending);
                }

                // 4. Grow survivors
                GrowAffectedCells(grid);

                // 5. Cleanup empty cubes
                CleanupEmptyCubes(grid);
            }

            CleanupEmptyCubes(grid);

            if (iterations >= MaxIterations)
                Debug.LogWarning("[SubCubeGrowthResolver] Hit iteration cap.");

            onComplete?.Invoke(totalRemoved);
        }

        #endregion

        #region Preparation

        private struct Pending
        {
            public List<MatchResult> groups;
            public List<SubCube> subCubes;
            public int totalSubCubes;
        }

        private Pending PrepareMatches(GridManager grid, List<MatchResult> matches)
        {
            var pending = new Pending
            {
                groups = new List<MatchResult>(),
                subCubes = new List<SubCube>(),
            };

            var seen = new HashSet<SubCube>();
            _affectedCubes.Clear();

            foreach (var m in matches)
            {
                var filtered = new List<SubCube>();

                foreach (var sub in m.Blocks)
                {
                    if (sub == null) continue;
                    if (!seen.Add(sub)) continue;

                    // Unregister from the board's sub-grid immediately
                    grid.SubGrid?.Unregister(sub);

                    // Remove from its shape
                    var owner = ResolveShape(sub);
                    if (owner != null) owner.RemoveBlock(sub);

                    // Track affected cube for the growth pass
                    if (owner != null)
                    {
                        var draggable = owner.GetComponentInParent<DraggableCube>();
                        if (draggable != null) _affectedCubes.Add(draggable);
                    }

                    filtered.Add(sub);
                    pending.subCubes.Add(sub);
                }

                if (filtered.Count > 0)
                    pending.groups.Add(new MatchResult(m.Color, filtered, m.IsHorizontal, m.IsVertical));
            }

            pending.totalSubCubes = pending.subCubes.Count;

            _affectedList.Clear();
            foreach (var c in _affectedCubes) _affectedList.Add(c);

            return pending;
        }

        private int DestroyPending(Pending pending)
        {
            int count = 0;
            for (int i = 0; i < pending.subCubes.Count; i++)
            {
                var sub = pending.subCubes[i];
                if (sub == null) continue;
                sub.enabled = false;
                Object.Destroy(sub.gameObject);
                count++;
            }
            return count;
        }

        #endregion

        #region Growth

        private void GrowAffectedCells(GridManager grid)
        {
            for (int i = 0; i < _affectedList.Count; i++)
            {
                var cube = _affectedList[i];
                if (cube == null) continue;
                if (!cube.OccupiedCell.HasValue) continue;
                if (cube.Shape == null) continue;
                if (cube.Shape.IsBuilding) continue;
                if (cube.Shape.Blocks.Count == 0) continue;

                GrowCell(grid, cube, cube.OccupiedCell.Value);
            }
        }

        private void GrowCell(GridManager grid, DraggableCube cube, Vector2Int cell)
        {
            var shape = cube.Shape;
            if (shape == null) return;

            var subs = new List<SubCube>();
            var blocks = shape.Blocks;
            for (int i = 0; i < blocks.Count; i++)
                if (blocks[i] != null) subs.Add(blocks[i]);

            if (subs.Count == 0) return;

            // Sort by footprint area, largest first — bigger blocks grow first and claim space
            subs.Sort((a, b) => SlotArea(b).CompareTo(SlotArea(a)));

            var occupied = new bool[2, 2];
            var footprints = new List<(SubCube sub, int xMin, int xMax, int yMin, int yMax)>();

            // Record current footprints and mark the grid
            foreach (var sub in subs)
            {
                var fp = SlotFootprint(sub.SlotPosition, sub.SlotSize);
                footprints.Add((sub, fp.xMin, fp.xMax, fp.yMin, fp.yMax));

                for (int x = fp.xMin; x < fp.xMax; x++)
                    for (int y = fp.yMin; y < fp.yMax; y++)
                        occupied[x, y] = true;
            }

            var newSlots = new List<(SubCube sub, Vector2 center, Vector2 size, CubeShape.BlockKind kind)>();

            foreach (var (sub, xMin, xMax, yMin, yMax) in footprints)
            {
                // Temporarily free this sub-cube's own cells so it can grow into them
                for (int x = xMin; x < xMax; x++)
                    for (int y = yMin; y < yMax; y++)
                        occupied[x, y] = false;

                // Loop growth until it stabilizes
                int gxMin = xMin, gxMax = xMax, gyMin = yMin, gyMax = yMax;
                CubeShape.BlockKind finalKind = CubeShape.KindFromSlotSize(sub.SlotSize);

                for (int step = 0; step < MaxGrowthSteps; step++)
                {
                    var grown = TryGrow(gxMin, gxMax, gyMin, gyMax, occupied);

                    bool changed =
                        grown.xMin != gxMin || grown.xMax != gxMax ||
                        grown.yMin != gyMin || grown.yMax != gyMax;

                    gxMin = grown.xMin; gxMax = grown.xMax;
                    gyMin = grown.yMin; gyMax = grown.yMax;
                    finalKind = grown.kind;

                    if (!changed) break;

                    // Free the grown region so the next iteration can consider extending further
                    for (int x = gxMin; x < gxMax; x++)
                        for (int y = gyMin; y < gyMax; y++)
                            occupied[x, y] = false;
                }

                // Re-occupy the final grown region
                for (int x = gxMin; x < gxMax; x++)
                    for (int y = gyMin; y < gyMax; y++)
                        occupied[x, y] = true;

                Vector2 center = new Vector2(
                    (gxMin + gxMax) * 0.25f,
                    (gyMin + gyMax) * 0.25f
                );
                Vector2 size = new Vector2(
                    (gxMax - gxMin) * 0.5f,
                    (gyMax - gyMin) * 0.5f
                );

                newSlots.Add((sub, center, size, finalKind));
            }

            foreach (var (sub, center, size, kind) in newSlots)
                ApplySlot(grid, cube, cell, sub, center, size, kind);
        }

        /// <summary>
        /// Given a sub-cube's current footprint (in sub-cell coords), return the largest
        /// footprint it can grow into, given current occupancy.
        /// Whole is preferred over Half, and Half over Small.
        /// </summary>
        private (int xMin, int xMax, int yMin, int yMax, CubeShape.BlockKind kind) TryGrow(
            int xMin, int xMax, int yMin, int yMax, bool[,] occupied)
        {
            int w = xMax - xMin;
            int h = yMax - yMin;

            // Already whole — max size, no further growth.
            if (w == 2 && h == 2)
                return (xMin, xMax, yMin, yMax, CubeShape.BlockKind.Whole);

            // ---- Try to reach WHOLE directly ----

            // Vertical Half (w=1, h=2): grow horizontally
            if (w == 1 && h == 2)
            {
                if (xMax < 2 && IsColumnFree(occupied, xMax, yMin, yMax))
                    return (xMin, xMax + 1, yMin, yMax, CubeShape.BlockKind.Whole);

                if (xMin > 0 && IsColumnFree(occupied, xMin - 1, yMin, yMax))
                    return (xMin - 1, xMax, yMin, yMax, CubeShape.BlockKind.Whole);

                return (xMin, xMax, yMin, yMax, CubeShape.BlockKind.Half);
            }

            // Horizontal Half (w=2, h=1): grow vertically
            if (w == 2 && h == 1)
            {
                if (yMax < 2 && IsRowFree(occupied, yMax, xMin, xMax))
                    return (xMin, xMax, yMin, yMax + 1, CubeShape.BlockKind.Whole);

                if (yMin > 0 && IsRowFree(occupied, yMin - 1, xMin, xMax))
                    return (xMin, xMax, yMin - 1, yMax, CubeShape.BlockKind.Whole);

                return (xMin, xMax, yMin, yMax, CubeShape.BlockKind.Half);
            }

            // Small (w=1, h=1): try Whole first, then Half
            if (w == 1 && h == 1)
            {
                // Try all 4 possible 2×2 windows that contain this cell
                if (CanClaim2x2(occupied, xMin,     yMin,     out var wholeA)) return wholeA;
                if (CanClaim2x2(occupied, xMin - 1, yMin,     out var wholeB)) return wholeB;
                if (CanClaim2x2(occupied, xMin,     yMin - 1, out var wholeC)) return wholeC;
                if (CanClaim2x2(occupied, xMin - 1, yMin - 1, out var wholeD)) return wholeD;

                // Otherwise try to become a Half in any direction
                if (xMax < 2 && !occupied[xMax, yMin])
                    return (xMin, xMax + 1, yMin, yMax, CubeShape.BlockKind.Half);
                if (xMin > 0 && !occupied[xMin - 1, yMin])
                    return (xMin - 1, xMax, yMin, yMax, CubeShape.BlockKind.Half);
                if (yMax < 2 && !occupied[xMin, yMax])
                    return (xMin, xMax, yMin, yMax + 1, CubeShape.BlockKind.Half);
                if (yMin > 0 && !occupied[xMin, yMin - 1])
                    return (xMin, xMax, yMin - 1, yMax, CubeShape.BlockKind.Half);

                return (xMin, xMax, yMin, yMax, CubeShape.BlockKind.Small);
            }

            // Fallback (shouldn't normally reach here)
            return (xMin, xMax, yMin, yMax,
                CubeShape.KindFromSlotSize(new Vector2(w * 0.5f, h * 0.5f)));
        }

        /// <summary>All cells in column x, rows yMin..yMax-1 are free?</summary>
        private static bool IsColumnFree(bool[,] occupied, int x, int yMin, int yMax)
        {
            if (x < 0 || x >= 2) return false;
            for (int y = yMin; y < yMax; y++)
                if (occupied[x, y]) return false;
            return true;
        }

        /// <summary>All cells in row y, columns xMin..xMax-1 are free?</summary>
        private static bool IsRowFree(bool[,] occupied, int y, int xMin, int xMax)
        {
            if (y < 0 || y >= 2) return false;
            for (int x = xMin; x < xMax; x++)
                if (occupied[x, y]) return false;
            return true;
        }

        /// <summary>
        /// Can a 2×2 block anchored at (ax, ay) be claimed?
        /// The anchor is the bottom-left of the 2×2 window.
        /// </summary>
        private static bool CanClaim2x2(bool[,] occupied, int ax, int ay,
            out (int xMin, int xMax, int yMin, int yMax, CubeShape.BlockKind kind) result)
        {
            result = default;

            if (ax < 0 || ay < 0) return false;
            if (ax + 2 > 2 || ay + 2 > 2) return false;

            for (int x = ax; x < ax + 2; x++)
                for (int y = ay; y < ay + 2; y++)
                    if (occupied[x, y]) return false;

            result = (ax, ax + 2, ay, ay + 2, CubeShape.BlockKind.Whole);
            return true;
        }

        #endregion

        #region Slot Application

        private void ApplySlot(GridManager grid, DraggableCube cube, Vector2Int cell,
                              SubCube sub, Vector2 center, Vector2 size, CubeShape.BlockKind targetKind)
        {
            if (sub == null) return;
            if (cube == null || cube.Shape == null) return;

            var currentKind = CubeShape.KindFromSlotSize(sub.SlotSize);
            SubCube finalSub = sub;

            if (currentKind != targetKind)
            {
                int yaw = 0;
                if (targetKind == CubeShape.BlockKind.Half)
                {
                    bool spansZ = size.x < size.y;
                    yaw = spansZ ? 0 : 1;
                }

                finalSub = cube.Shape.ReplaceSubCube(sub, targetKind, center, size, yaw);
                if (finalSub == null) return;
            }
            else
            {
                finalSub.SlotPosition = center;
                finalSub.SlotSize = size;

                if (finalSub.SlotPivot != null)
                {
                    float cs = cube.Shape.CellSize;
                    finalSub.SlotPivot.localPosition = new Vector3(
                        (center.x - 0.5f) * cs,
                        0f,
                        (center.y - 0.5f) * cs
                    );
                }
                else
                {
                    float cs = cube.Shape.CellSize;
                    finalSub.transform.localPosition = new Vector3(
                        (center.x - 0.5f) * cs,
                        0f,
                        (center.y - 0.5f) * cs
                    );
                }
            }

            grid.SubGrid?.Register(finalSub, cell, center, size);
        }

        #endregion

        #region Empty-Cube Cleanup

        private void CleanupEmptyCubes(GridManager grid)
        {
            var emptyCubes = new List<DraggableCube>();

            foreach (var kvp in grid.Occupied)
            {
                var cubeGO = kvp.Value;
                if (cubeGO == null) continue;
                if (!cubeGO.TryGetComponent(out DraggableCube cube)) continue;
                if (!ShouldCleanup(cube)) continue;
                if (!emptyCubes.Contains(cube)) emptyCubes.Add(cube);
            }

            var allCubes = Object.FindObjectsByType<DraggableCube>(FindObjectsSortMode.None);
            for (int i = 0; i < allCubes.Length; i++)
            {
                var cube = allCubes[i];
                if (cube == null) continue;
                if (!ShouldCleanup(cube)) continue;
                if (!emptyCubes.Contains(cube)) emptyCubes.Add(cube);
            }

            for (int i = 0; i < emptyCubes.Count; i++)
            {
                var cube = emptyCubes[i];
                if (cube == null) continue;

                var cell = cube.OccupiedCell;
                if (cell.HasValue)
                {
                    var shape = cube.Shape;
                    if (shape != null)
                    {
                        var blocks = shape.Blocks;
                        for (int b = 0; b < blocks.Count; b++)
                            if (blocks[b] != null)
                                grid.SubGrid?.Unregister(blocks[b]);
                    }

                    grid.Release(cell.Value, cube.gameObject);
                }

                CubeDestroyed?.Invoke(cube);
                Object.Destroy(cube.gameObject);
            }
        }

        private bool ShouldCleanup(DraggableCube cube)
        {
            if (cube == null) return false;
            if (!cube.IsPlaced) return false;
            if (!cube.OccupiedCell.HasValue) return false;
            if (cube.Shape == null) return false;
            if (cube.Shape.IsBuilding) return false;
            if (cube.Shape.Blocks.Count != 0) return false;
            return true;
        }

        #endregion

        #region Helpers

        private static CubeShape ResolveShape(SubCube sub)
        {
            if (sub == null) return null;
            return sub.Owner != null ? sub.Owner : sub.GetComponentInParent<CubeShape>();
        }

        private static (int xMin, int xMax, int yMin, int yMax) SlotFootprint(Vector2 center, Vector2 size)
        {
            Vector2 half = size * 0.5f;
            Vector2 lower = center - half;
            Vector2 upper = center + half;

            return (
                Mathf.RoundToInt(lower.x * 2),
                Mathf.RoundToInt(upper.x * 2),
                Mathf.RoundToInt(lower.y * 2),
                Mathf.RoundToInt(upper.y * 2)
            );
        }

        private static float SlotArea(SubCube sub)
            => sub.SlotSize.x * sub.SlotSize.y;

        #endregion
    }
}
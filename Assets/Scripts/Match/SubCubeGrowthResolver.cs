using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MyGame.Board;
using MyGame.Interaction;

namespace MyGame.Match
{
    /// <summary>
    /// Resolves matches asynchronously: animate merge → destroy → grow → cleanup → re-check.
    /// </summary>
    public class SubCubeGrowthResolver
    {
        private readonly SubCubeMatchDetector _detector = new();
        private readonly List<MatchResult> _scratch = new();

        private readonly HashSet<DraggableCube> _affectedCubes = new();
        private readonly List<DraggableCube> _affectedList = new();

        private const int MaxIterations = 64;

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

                // 2. Gather affected cubes, unregister from sub-grid, remove from shapes,
                //    but DO NOT destroy — animation handles that.
                var pending = PrepareMatches(grid, _scratch);

                // 3. Animate merge for each group (parallel across groups)
                if (Animator != null && pending.totalSubCubes > 0)
                {
                    bool done = false;
                    yield return Animator.AnimateBatch(pending.groups, () => done = true);
                    // AnimateBatch calls onComplete synchronously before yielding; safe.
                    totalRemoved += pending.totalSubCubes;
                }
                else
                {
                    // No animator — destroy instantly
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

        /// <summary>
        /// Unregisters matched sub-cubes from the sub-grid and removes them from
        /// their shapes, but does not destroy them. The animation takes over.
        /// </summary>
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

                    // Unregister now, so detection and other systems treat the board as updated
                    grid.SubGrid?.Unregister(sub);

                    // Remove from shape immediately
                    var owner = ResolveShape(sub);
                    if (owner != null) owner.RemoveBlock(sub);
                    else Debug.LogWarning($"[PrepareMatches] Sub-cube {sub.name} has no CubeShape.");

                    // Track for growth pass
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

        /// <summary>Fallback for when there's no animator — destroy immediately.</summary>
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

            subs.Sort((a, b) => SlotArea(b).CompareTo(SlotArea(a)));

            var occupied = new bool[2, 2];
            var footprints = new List<(SubCube sub, int xMin, int xMax, int yMin, int yMax)>();

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
                for (int x = xMin; x < xMax; x++)
                    for (int y = yMin; y < yMax; y++)
                        occupied[x, y] = false;

                var grown = TryGrow(xMin, xMax, yMin, yMax, occupied);

                for (int x = grown.xMin; x < grown.xMax; x++)
                    for (int y = grown.yMin; y < grown.yMax; y++)
                        occupied[x, y] = true;

                Vector2 center = new Vector2(
                    (grown.xMin + grown.xMax) * 0.25f,
                    (grown.yMin + grown.yMax) * 0.25f
                );
                Vector2 size = new Vector2(
                    (grown.xMax - grown.xMin) * 0.5f,
                    (grown.yMax - grown.yMin) * 0.5f
                );

                newSlots.Add((sub, center, size, grown.kind));
            }

            foreach (var (sub, center, size, kind) in newSlots)
                ApplySlot(grid, cube, cell, sub, center, size, kind);
        }

        private (int xMin, int xMax, int yMin, int yMax, CubeShape.BlockKind kind) TryGrow(
            int xMin, int xMax, int yMin, int yMax, bool[,] occupied)
        {
            int w = xMax - xMin;
            int h = yMax - yMin;

            if (w == 2 && h == 2)
                return (xMin, xMax, yMin, yMax, CubeShape.BlockKind.Whole);

            if (w == 1 && h == 1)
            {
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

            if (w == 2 && h == 1)
            {
                if (yMax < 2 && !occupied[xMin, yMax] && !occupied[xMax - 1, yMax])
                    return (xMin, xMax, yMin, yMax + 1, CubeShape.BlockKind.Whole);
                if (yMin > 0 && !occupied[xMin, yMin - 1] && !occupied[xMax - 1, yMin - 1])
                    return (xMin, xMax, yMin - 1, yMax, CubeShape.BlockKind.Whole);
                return (xMin, xMax, yMin, yMax, CubeShape.BlockKind.Half);
            }

            if (w == 1 && h == 2)
            {
                if (xMax < 2 && !occupied[xMax, yMin] && !occupied[xMax, yMax - 1])
                    return (xMin, xMax + 1, yMin, yMax, CubeShape.BlockKind.Whole);
                if (xMin > 0 && !occupied[xMin - 1, yMin] && !occupied[xMin - 1, yMax - 1])
                    return (xMin - 1, xMax, yMin, yMax, CubeShape.BlockKind.Whole);
                return (xMin, xMax, yMin, yMax, CubeShape.BlockKind.Half);
            }

            return (xMin, xMax, yMin, yMax, CubeShape.KindFromSlotSize(new Vector2(w * 0.5f, h * 0.5f)));
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
                    yaw = spansZ ? 1 : 0;
                }

                finalSub = cube.Shape.ReplaceSubCube(sub, targetKind, center, size, yaw);
                if (finalSub == null) return;
            }
            else
            {
                finalSub.SlotPosition = center;
                finalSub.SlotSize = size;

                float cellSize = cube.Shape.CellSize;
                finalSub.transform.localPosition = new Vector3(
                    (center.x - 0.5f) * cellSize,
                    0f,
                    (center.y - 0.5f) * cellSize
                );
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
using System.Collections.Generic;
using UnityEngine;
using MyGame.Interaction;

namespace MyGame.Board
{
    /// <summary>
    /// The finest-resolution grid of the board. Each big-grid cell is subdivided
    /// into a 2×2 sub-grid (smallest sub-cube is 0.5 cells).
    /// </summary>
    public class BoardSubGrid
    {
        public const int SubCellsPerCell = 2;

        public int CellWidth  { get; private set; }
        public int CellHeight { get; private set; }

        public int SubWidth  => CellWidth  * SubCellsPerCell;
        public int SubHeight => CellHeight * SubCellsPerCell;

        private SubCube[,] _grid;
        private readonly Dictionary<SubCube, List<Vector2Int>> _registeredCells = new();

        public BoardSubGrid(int cellWidth, int cellHeight)
        {
            Resize(cellWidth, cellHeight);
        }

        public void Resize(int cellWidth, int cellHeight)
        {
            CellWidth  = Mathf.Max(1, cellWidth);
            CellHeight = Mathf.Max(1, cellHeight);

            _grid = new SubCube[SubWidth, SubHeight];
            _registeredCells.Clear();
        }

        public void Clear()
        {
            if (_grid != null)
            {
                for (int x = 0; x < SubWidth; x++)
                    for (int y = 0; y < SubHeight; y++)
                        _grid[x, y] = null;
            }
            _registeredCells.Clear();
        }

        public void Register(SubCube sub, Vector2Int cell, Vector2 slotPosition, Vector2 slotSize)
        {
            if (sub == null || _grid == null) return;

            Vector2 half = slotSize * 0.5f;
            Vector2 lowerLeft = slotPosition - half;
            Vector2 upperRight = slotPosition + half;

            if (lowerLeft.x < -0.001f || lowerLeft.y < -0.001f ||
                upperRight.x > 1.001f || upperRight.y > 1.001f)
            {
                Debug.LogWarning(
                    $"[BoardSubGrid] Slot out of cell bounds: pos={slotPosition} size={slotSize}",
                    sub
                );
                return;
            }

            int xMin = Mathf.RoundToInt(lowerLeft.x * SubCellsPerCell);
            int xMax = Mathf.RoundToInt(upperRight.x * SubCellsPerCell);
            int yMin = Mathf.RoundToInt(lowerLeft.y * SubCellsPerCell);
            int yMax = Mathf.RoundToInt(upperRight.y * SubCellsPerCell);

            int baseX = cell.x * SubCellsPerCell;
            int baseY = cell.y * SubCellsPerCell;

            var list = new List<Vector2Int>(4);

            for (int x = xMin; x < xMax; x++)
            {
                for (int y = yMin; y < yMax; y++)
                {
                    int gx = baseX + x;
                    int gy = baseY + y;

                    if (gx < 0 || gx >= SubWidth) continue;
                    if (gy < 0 || gy >= SubHeight) continue;

                    _grid[gx, gy] = sub;
                    list.Add(new Vector2Int(gx, gy));
                }
            }

            _registeredCells[sub] = list;
        }

        public void Unregister(SubCube sub)
        {
            if (sub == null) return;
            if (!_registeredCells.TryGetValue(sub, out var cells)) return;

            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                if (c.x >= 0 && c.x < SubWidth && c.y >= 0 && c.y < SubHeight)
                    if (_grid[c.x, c.y] == sub)
                        _grid[c.x, c.y] = null;
            }

            _registeredCells.Remove(sub);
        }

        public SubCube Get(int x, int y)
        {
            if (_grid == null) return null;
            if (x < 0 || x >= SubWidth) return null;
            if (y < 0 || y >= SubHeight) return null;
            return _grid[x, y];
        }

        public IEnumerable<SubCube> AllSubCubes()
        {
            foreach (var kvp in _registeredCells)
                if (kvp.Key != null) yield return kvp.Key;
        }
    }
}
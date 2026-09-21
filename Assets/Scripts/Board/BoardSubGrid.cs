using System.Collections.Generic;
using UnityEngine;
using MyGame.Interaction;

namespace MyGame.Board
{
    /// <summary>
    /// The board's sub-grid. Each big-grid cell has a 2×2 sub-grid.
    /// Each sub-slot carries an optional SubCube and that sub-cube's CubeColor.
    /// </summary>
    public class BoardSubGrid
    {
        public const int SubCellsPerCell = 2;

        public int CellWidth  { get; private set; }
        public int CellHeight { get; private set; }

        public int SubWidth  => CellWidth  * SubCellsPerCell;
        public int SubHeight => CellHeight * SubCellsPerCell;

        private SubCube[,] _subCubes;
        private CubeColor[,] _colors;

        private readonly Dictionary<SubCube, List<Vector2Int>> _occupancy = new();

        public BoardSubGrid(int cellWidth, int cellHeight)
        {
            Resize(cellWidth, cellHeight);
        }

        public void Resize(int cellWidth, int cellHeight)
        {
            CellWidth  = Mathf.Max(1, cellWidth);
            CellHeight = Mathf.Max(1, cellHeight);

            _subCubes = new SubCube[SubWidth, SubHeight];
            _colors   = new CubeColor[SubWidth, SubHeight];
            _occupancy.Clear();
        }

        public void Clear()
        {
            if (_subCubes != null)
            {
                for (int x = 0; x < SubWidth; x++)
                    for (int y = 0; y < SubHeight; y++)
                    {
                        _subCubes[x, y] = null;
                        _colors[x, y] = CubeColor.None;
                    }
            }
            _occupancy.Clear();
        }

        #region Registration

        /// <summary>
        /// Registers a sub-cube at a cell, occupying sub-slots based on its
        /// cell-local slot position and size. Writes the sub-cube's color into
        /// each occupied sub-slot's color data.
        /// </summary>
        public void Register(SubCube sub, Vector2Int cell, Vector2 slotPos, Vector2 slotSize)
        {
            if (sub == null) return;
            if (_subCubes == null) return;

            Vector2 half = slotSize * 0.5f;
            Vector2 ll = slotPos - half;
            Vector2 ur = slotPos + half;

            if (ll.x < -0.001f || ll.y < -0.001f || ur.x > 1.001f || ur.y > 1.001f)
            {
                Debug.LogWarning($"[BoardSubGrid] Slot out of cell bounds: pos={slotPos} size={slotSize}", sub);
                return;
            }

            int xMin = Mathf.RoundToInt(ll.x * SubCellsPerCell);
            int xMax = Mathf.RoundToInt(ur.x * SubCellsPerCell);
            int yMin = Mathf.RoundToInt(ll.y * SubCellsPerCell);
            int yMax = Mathf.RoundToInt(ur.y * SubCellsPerCell);

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

                    _subCubes[gx, gy] = sub;
                    _colors[gx, gy] = sub.CurrentColor;   // write the color data
                    list.Add(new Vector2Int(gx, gy));
                }
            }

            _occupancy[sub] = list;
        }

        public void Unregister(SubCube sub)
        {
            if (sub == null) return;
            if (!_occupancy.TryGetValue(sub, out var cells)) return;

            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                if (c.x < 0 || c.x >= SubWidth || c.y < 0 || c.y >= SubHeight) continue;
                if (_subCubes[c.x, c.y] == sub)
                {
                    _subCubes[c.x, c.y] = null;
                    _colors[c.x, c.y] = CubeColor.None;
                }
            }

            _occupancy.Remove(sub);
        }

        #endregion

        #region Queries

        public SubCube GetSubCube(int x, int y)
        {
            if (_subCubes == null) return null;
            if (x < 0 || x >= SubWidth) return null;
            if (y < 0 || y >= SubHeight) return null;
            return _subCubes[x, y];
        }

        public CubeColor GetColor(int x, int y)
        {
            if (_colors == null) return CubeColor.None;
            if (x < 0 || x >= SubWidth) return CubeColor.None;
            if (y < 0 || y >= SubHeight) return CubeColor.None;
            return _colors[x, y];
        }

        public IEnumerable<SubCube> AllSubCubes()
        {
            foreach (var kvp in _occupancy)
                if (kvp.Key != null) yield return kvp.Key;
        }

        #endregion
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Board
{
    [CreateAssetMenu(fileName = "Board_", menuName = "Levels/Board Definition", order = 0)]
    public class BoardDefinition : ScriptableObject
    {
        public enum BoardPlane { XZ_3D, XY_2D }

        [Header("Identity")]
        public string boardName = "New Board";

        [Header("Shape (Row 0 = bottom)")]
        [TextArea(5, 20)]
        public string shape =
            "11111\n" +
            "10101\n" +
            "11111";

        [Header("Layout")]
        public BoardPlane plane = BoardPlane.XZ_3D;
        public float cellSize = 1f;
        public Vector3 originPosition = Vector3.zero;

        [Header("Visuals")]
        public Color validCellColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        [NonSerialized] private List<string> _rows;
        [NonSerialized] private int _width = -1;
        [NonSerialized] private int _height = -1;

        public int Width  { get { EnsureRebuilt(); return _width; } }
        public int Height { get { EnsureRebuilt(); return _height; } }

        public void Rebuild()
        {
            _rows = new List<string>();
            string[] split = shape.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
            _height = split.Length;
            _width = 0;
            foreach (var raw in split)
            {
                string t = raw.TrimEnd();
                _rows.Add(t);
                if (t.Length > _width) _width = t.Length;
            }
        }

        private void EnsureRebuilt() { if (_rows == null) Rebuild(); }

        public bool IsValidCell(int x, int y)
        {
            EnsureRebuilt();
            if (y < 0 || y >= _height) return false;
            var row = _rows[y];
            if (x < 0 || x >= row.Length) return false;
            char c = row[x];
            return c == '1' || c == '#' || c == 'X' || c == 'x';
        }

        public IEnumerable<Vector2Int> EnumerateValidCells()
        {
            EnsureRebuilt();
            for (int y = 0; y < _height; y++)
                for (int x = 0; x < _rows[y].Length; x++)
                    if (IsValidCell(x, y)) yield return new Vector2Int(x, y);
        }

        public Vector3 GridToWorld(Vector2Int grid)
        {
            Vector3 p = (plane == BoardPlane.XZ_3D)
                ? new Vector3(grid.x * cellSize, 0f, grid.y * cellSize)
                : new Vector3(grid.x * cellSize, grid.y * cellSize, 0f);
            return p + originPosition;
        }

        public Vector2Int WorldToGrid(Vector3 world)
        {
            Vector3 local = world - originPosition;
            if (plane == BoardPlane.XZ_3D)
                return new Vector2Int(Mathf.RoundToInt(local.x / cellSize), Mathf.RoundToInt(local.z / cellSize));
            return new Vector2Int(Mathf.RoundToInt(local.x / cellSize), Mathf.RoundToInt(local.y / cellSize));
        }

        public Vector3 ComputeLocalCenter()
        {
            EnsureRebuilt();
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;
            bool any = false;
            foreach (var g in EnumerateValidCells())
            {
                any = true;
                if (g.x < minX) minX = g.x;
                if (g.y < minY) minY = g.y;
                if (g.x > maxX) maxX = g.x;
                if (g.y > maxY) maxY = g.y;
            }
            if (!any) return Vector3.zero;

            float cx = (minX + maxX) * 0.5f * cellSize;
            float cy = (minY + maxY) * 0.5f * cellSize;
            return (plane == BoardPlane.XZ_3D) ? new Vector3(cx, 0f, cy) : new Vector3(cx, cy, 0f);
        }

#if UNITY_EDITOR
        private void OnValidate() { Rebuild(); }
#endif
    }
}
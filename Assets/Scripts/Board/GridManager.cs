using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Board
{
    public class GridManager : MonoBehaviour
    {
        [Header("Current Level")]
        public BoardDefinition board;

        [Header("Prefabs")]
        public GameObject cellPrefab;
        public Material validCellMat;

        private readonly Dictionary<Vector2Int, GameObject> _cells = new();
        private readonly Dictionary<Vector2Int, GameObject> _occupied = new();

        public BoardSubGrid SubGrid { get; private set; }

        public BoardDefinition Board => board;
        public IReadOnlyDictionary<Vector2Int, GameObject> Occupied => _occupied;
        public IReadOnlyDictionary<Vector2Int, GameObject> Cells => _cells;

        public event System.Action<Vector2Int, GameObject> OnCubePlaced;
        public event System.Action<Vector2Int, GameObject> OnCubeRemoved;
        public event System.Action<BoardDefinition> OnBoardBuilt;

        private void Start()
        {
            if (board != null) Build(board);
        }

        public void Build(BoardDefinition definition)
        {
            Clear();
            board = definition;
            if (board == null) return;
            board.Rebuild();

            if (SubGrid == null) SubGrid = new BoardSubGrid(board.Width, board.Height);
            else SubGrid.Resize(board.Width, board.Height);

            board.originPosition = -board.ComputeLocalCenter() + transform.position;

            foreach (var grid in board.EnumerateValidCells())
            {
                Vector3 world = board.GridToWorld(grid);
                GameObject cell = Instantiate(cellPrefab, world, cellPrefab.transform.rotation, transform);
                cell.name = $"Cell_{grid.x}_{grid.y}";
                cell.transform.localScale = Vector3.one * (board.cellSize * 0.95f);

                if (cell.TryGetComponent(out Renderer r))
                {
                    if (validCellMat != null) r.sharedMaterial = validCellMat;
                    else r.material.color = board.validCellColor;
                }

                _cells[grid] = cell;
            }

            OnBoardBuilt?.Invoke(board);
        }

        public void Clear()
        {
            foreach (var c in _cells.Values) if (c) Destroy(c);
            _cells.Clear();
            _occupied.Clear();
            SubGrid?.Clear();
        }

        public bool IsValidGridPosition(Vector2Int grid)
            => board != null && board.IsValidCell(grid.x, grid.y);

        public bool IsOccupied(Vector2Int grid, GameObject ignore = null)
        {
            if (!_occupied.TryGetValue(grid, out var occupant)) return false;
            return occupant != ignore;
        }

        public bool CanPlaceAt(Vector2Int grid, GameObject ignore = null)
            => IsValidGridPosition(grid) && !IsOccupied(grid, ignore);

        public GameObject GetOccupant(Vector2Int grid)
            => _occupied.TryGetValue(grid, out var go) ? go : null;

        public bool Occupy(Vector2Int grid, GameObject cube)
        {
            if (!CanPlaceAt(grid, cube)) return false;
            _occupied[grid] = cube;
            OnCubePlaced?.Invoke(grid, cube);
            return true;
        }

        public void Release(Vector2Int grid, GameObject cube)
        {
            if (_occupied.TryGetValue(grid, out var current) && current == cube)
            {
                _occupied.Remove(grid);
                OnCubeRemoved?.Invoke(grid, cube);
            }
        }

        public Vector3 GetWorldPosition(Vector2Int grid) => board.GridToWorld(grid);
        public Vector2Int GetGridPosition(Vector3 world) => board.WorldToGrid(world);
    }
}
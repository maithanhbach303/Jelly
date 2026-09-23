using System.Collections.Generic;
using UnityEngine;
using MyGame.Interaction;

namespace MyGame.Board
{
    public class GridManager : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Current Level")]
        public BoardDefinition board;

        [Header("Prefabs")]
        public GameObject cellPrefab;
        public Material validCellMat;
        public Material holeMat;

        [Header("Auto-Center")]
        [SerializeField] private bool autoCenterOnBuild = true;
        [SerializeField] private bool centerInFrontOfCamera = false;

        [Header("Auto-Fit")]
        [SerializeField] private bool autoFitToScreen = true;
        [Range(0f, 0.5f)]
        [SerializeField] private float fitPadding = 0.15f;
        [SerializeField] private bool shrinkOnly = true;
        [SerializeField] private FitStrategy fitStrategy = FitStrategy.ScaleBoard;
        [SerializeField] private Camera targetCamera;

        #endregion

        #region Nested Types

        public enum FitStrategy { ScaleBoard, ScaleCamera }

        #endregion

        #region Runtime State

        private readonly Dictionary<Vector2Int, GameObject> _cells = new();
        private readonly Dictionary<Vector2Int, GameObject> _occupied = new();

        private bool _isBuilding;
        private bool _isFitting;
        private bool _initialized;
        private int _lastScreenW = -1;
        private int _lastScreenH = -1;
        private float _baseOrthoSize = -1f;

        public BoardSubGrid SubGrid { get; private set; }

        #endregion

        #region Public Accessors

        public BoardDefinition Board => board;
        public IReadOnlyDictionary<Vector2Int, GameObject> Cells => _cells;
        public IReadOnlyDictionary<Vector2Int, GameObject> Occupied => _occupied;

        #endregion

        #region Events

        public event System.Action<Vector2Int, GameObject> OnCubePlaced;
        public event System.Action<Vector2Int, GameObject> OnCubeRemoved;
        public event System.Action<BoardDefinition> OnBoardBuilt;

        #endregion

        #region Lifecycle

        private void Start()
        {
            if (_initialized) return;
            if (board == null) return;

            var loader = FindFirstObjectByType<MyGame.Levels.LevelLoader>();
            if (loader != null) return;

            Build(board);
        }

        private void LateUpdate()
        {
            if (Screen.width == _lastScreenW && Screen.height == _lastScreenH) return;
            _lastScreenW = Screen.width;
            _lastScreenH = Screen.height;

            if (!autoCenterOnBuild && !autoFitToScreen) return;
            if (board == null) return;

            RecenterAndFit();
        }

        #endregion

        #region Build / Clear

        public void Build(BoardDefinition definition, bool autoCenter = true)
        {
            if (_isBuilding) return;
            _isBuilding = true;
            _initialized = true;
            try
            {
                Clear();
                board = definition;
                if (board == null) return;
                board.Rebuild();

                if (SubGrid == null)
                    SubGrid = new BoardSubGrid(board.Width, board.Height);
                else
                    SubGrid.Resize(board.Width, board.Height);

                var cam = targetCamera != null ? targetCamera : Camera.main;
                if (cam != null && cam.orthographic) _baseOrthoSize = cam.orthographicSize;
                else _baseOrthoSize = -1f;

                if (autoCenter && autoCenterOnBuild && !centerInFrontOfCamera)
                    board.originPosition = -board.ComputeLocalCenter() + transform.position;

                foreach (Vector2Int grid in board.EnumerateValidCells())
                {
                    Vector3 world = board.GridToWorld(grid);
                    GameObject cell = Instantiate(cellPrefab, world, cellPrefab.transform.rotation, transform);
                    cell.name = $"Cell_{grid.x}_{grid.y}";
                    cell.transform.localScale = Vector3.one * (board.cellSize * 0.95f);

                    if (cell.TryGetComponent(out Renderer rend))
                    {
                        if (validCellMat != null) rend.sharedMaterial = validCellMat;
                        else rend.material.color = board.validCellColor;
                    }

                    _cells[grid] = cell;
                }

                if (autoCenter && autoCenterOnBuild)
                {
                    if (centerInFrontOfCamera) CenterInFrontOfCamera();
                    if (autoFitToScreen) ApplyFit();
                }

                OnBoardBuilt?.Invoke(board);
            }
            finally
            {
                _isBuilding = false;
            }
        }

        /// <summary>
        /// Full clear: destroys cells and non-tray cubes. Prefills are skipped
        /// because PrefilledCubeSpawner owns them and clears them explicitly.
        /// </summary>
        public void Clear()
        {
            var dragController = FindFirstObjectByType<DragController>();
            dragController?.CancelDrag();

            foreach (var c in _cells.Values)
                if (c) Destroy(c);
            _cells.Clear();

            var occupiedSnapshot = new List<GameObject>(_occupied.Values);
            _occupied.Clear();

            for (int i = 0; i < occupiedSnapshot.Count; i++)
            {
                var cubeGO = occupiedSnapshot[i];
                if (cubeGO == null) continue;

                if (cubeGO.TryGetComponent(out DraggableCube cube))
                    cube.CleanupForDestroy();

                Destroy(cubeGO);
            }

            var leftovers = FindObjectsByType<DraggableCube>(FindObjectsSortMode.None);
            for (int i = 0; i < leftovers.Length; i++)
            {
                var cube = leftovers[i];
                if (cube == null) continue;
                if (cube.IsTrayOwned) continue;
                if (cube.GetComponentInParent<MyGame.Tray.CubeTray>() != null) continue;

                cube.CleanupForDestroy();
                Destroy(cube.gameObject);
            }

            SubGrid?.Clear();
        }

        /// <summary>
        /// Destroys every cube currently on the board without touching cells or
        /// board dimensions. Used by LevelLoader.RemoveAll for retry.
        /// </summary>
        public void ClearAllCubes()
        {
            var snapshot = new List<GameObject>(_occupied.Values);
            _occupied.Clear();

            for (int i = 0; i < snapshot.Count; i++)
            {
                var cubeGO = snapshot[i];
                if (cubeGO == null) continue;

                if (cubeGO.TryGetComponent(out DraggableCube cube))
                    cube.CleanupForDestroy();

                Destroy(cubeGO);
            }

            // Sweep orphans, skip tray-owned
            var leftovers = FindObjectsByType<DraggableCube>(FindObjectsSortMode.None);
            for (int i = 0; i < leftovers.Length; i++)
            {
                var cube = leftovers[i];
                if (cube == null) continue;
                if (cube.IsTrayOwned) continue;
                if (cube.GetComponentInParent<MyGame.Tray.CubeTray>() != null) continue;

                cube.CleanupForDestroy();
                Destroy(cube.gameObject);
            }

            SubGrid?.Clear();
        }

        #endregion

        #region Queries

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

        public int GetPlacedCubeCount() => _occupied.Count;

        #endregion

        #region Mutations

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

        #endregion

        #region Centering

        public void RecenterAndFit()
        {
            if (board == null) return;

            if (autoCenterOnBuild && !centerInFrontOfCamera) CenterOnTransform();
            else if (centerInFrontOfCamera) CenterInFrontOfCamera();

            if (autoFitToScreen) ApplyFit();
        }

        public void CenterOnTransform()
        {
            if (board == null) return;

            Vector3 localCenter = board.ComputeLocalCenter();
            Vector3 desiredOrigin = transform.position - localCenter;
            Vector3 delta = desiredOrigin - board.originPosition;
            if (delta.sqrMagnitude <= 0f) return;

            board.originPosition = desiredOrigin;

            foreach (var kvp in _cells)
                if (kvp.Value != null) kvp.Value.transform.position = board.GridToWorld(kvp.Key);

            foreach (var kvp in _occupied)
                if (kvp.Value != null) kvp.Value.transform.position = board.GridToWorld(kvp.Key);
        }

        public void CenterInFrontOfCamera()
        {
            if (board == null) return;

            var cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;

            Bounds b = ComputeBoardBounds();
            float distance = Mathf.Max(b.size.magnitude, 5f) * 1.2f;
            Vector3 focusPoint = cam.transform.position + cam.transform.forward * distance;
            Vector3 delta = focusPoint - b.center;
            board.originPosition += delta;

            foreach (var kvp in _cells)
                if (kvp.Value != null) kvp.Value.transform.position = board.GridToWorld(kvp.Key);

            foreach (var kvp in _occupied)
                if (kvp.Value != null) kvp.Value.transform.position = board.GridToWorld(kvp.Key);
        }

        public Bounds ComputeBoardBounds()
        {
            if (board == null) return new Bounds(transform.position, Vector3.zero);

            bool any = false;
            Bounds b = new Bounds();

            foreach (Vector2Int grid in board.EnumerateValidCells())
            {
                Vector3 c = board.GridToWorld(grid);
                Bounds cb = new Bounds(c, new Vector3(board.cellSize, 0.1f, board.cellSize));
                if (!any) { b = cb; any = true; }
                else b.Encapsulate(cb);
            }

            return any ? b : new Bounds(transform.position, Vector3.zero);
        }

        #endregion

        #region Fitting

        public void ApplyFit()
        {
            switch (fitStrategy)
            {
                case FitStrategy.ScaleBoard: FitByScalingBoard(); break;
                case FitStrategy.ScaleCamera: FitByScalingCamera(); break;
            }
        }

        public void FitByScalingBoard()
        {
            if (_isFitting) return;
            if (board == null) return;

            var cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;

            _isFitting = true;
            try
            {
                const int iterations = 6;
                for (int i = 0; i < iterations; i++)
                {
                    Bounds bounds = ComputeBoardBounds();
                    Vector3 center = bounds.center;
                    Vector3 extents = bounds.extents;

                    float maxX = 0f, maxY = 0f;
                    for (int s = 0; s < 8; s++)
                    {
                        Vector3 corner = center + new Vector3(
                            (s & 1) == 0 ? -extents.x : extents.x,
                            (s & 2) == 0 ? -extents.y : extents.y,
                            (s & 4) == 0 ? -extents.z : extents.z);
                        Vector3 vp = cam.WorldToViewportPoint(corner);
                        maxX = Mathf.Max(maxX, Mathf.Abs(vp.x - 0.5f));
                        maxY = Mathf.Max(maxY, Mathf.Abs(vp.y - 0.5f));
                    }

                    float limitX = 0.5f - fitPadding;
                    float limitY = 0.5f - fitPadding;

                    float scale = Mathf.Min(
                        limitX / Mathf.Max(maxX, 0.001f),
                        limitY / Mathf.Max(maxY, 0.001f));

                    if (shrinkOnly) scale = Mathf.Min(scale, 1f);
                    if (Mathf.Abs(scale - 1f) < 0.01f) break;

                    board.cellSize *= scale;
                    RebuildCellsOnly();
                    CenterOnTransform();
                }
            }
            finally
            {
                _isFitting = false;
            }
        }

        public void FitByScalingCamera()
        {
            if (board == null) return;

            var cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null || !cam.orthographic) return;

            if (_baseOrthoSize < 0f) _baseOrthoSize = cam.orthographicSize;

            Bounds b = ComputeBoardBounds();

            float halfH, halfV;
            if (board.plane == BoardDefinition.BoardPlane.XZ_3D)
            {
                halfH = b.extents.x;
                halfV = Mathf.Max(b.extents.y, b.extents.z);
            }
            else
            {
                halfH = b.extents.x;
                halfV = b.extents.y;
            }

            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float requiredVertical = halfV / Mathf.Max(0.001f, 1f - fitPadding);
            float requiredHorizontal = halfH / Mathf.Max(0.001f, aspect * (1f - fitPadding));

            float targetSize = Mathf.Max(requiredVertical, requiredHorizontal);
            cam.orthographicSize = shrinkOnly ? Mathf.Max(_baseOrthoSize, targetSize) : targetSize;
        }

        private void RebuildCellsOnly()
        {
            foreach (var c in _cells.Values) if (c) Destroy(c);
            _cells.Clear();

            foreach (Vector2Int grid in board.EnumerateValidCells())
            {
                Vector3 world = board.GridToWorld(grid);
                GameObject cell = Instantiate(cellPrefab, world, cellPrefab.transform.rotation, transform);
                cell.name = $"Cell_{grid.x}_{grid.y}";
                cell.transform.localScale = Vector3.one * (board.cellSize * 0.95f);

                if (cell.TryGetComponent(out Renderer rend))
                {
                    if (validCellMat != null) rend.sharedMaterial = validCellMat;
                    else rend.material.color = board.validCellColor;
                }

                _cells[grid] = cell;
            }
        }

        #endregion

        #region Conversions

        public Vector3 GetWorldPosition(Vector2Int grid) => board.GridToWorld(grid);
        public Vector2Int GetGridPosition(Vector3 world) => board.WorldToGrid(world);

        #endregion
    }
}
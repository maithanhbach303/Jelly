using System.Collections.Generic;
using UnityEngine;

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
        [Tooltip("Recenter the board so its geometric center sits on this transform when built.")]
        [SerializeField] private bool autoCenterOnBuild = true;

        [Tooltip("Recenter in front of the camera instead of this transform.")]
        [SerializeField] private bool centerInFrontOfCamera = false;

        [Header("Auto-Fit")]
        [Tooltip("Fit the board to the screen when built. Uses the fit strategy below.")]
        [SerializeField] private bool autoFitToScreen = true;

        [Tooltip("Padding (0..1) around the board when fitting. 0.15 = 15% margin.")]
        [Range(0f, 0.5f)][SerializeField] private float fitPadding = 0.15f;

        [Tooltip("If true, only shrink when the board is bigger than the screen. Small boards keep their authored cell size.")]
        [SerializeField] private bool shrinkOnly = true;

        [Tooltip("Which fit strategy to use. Perspective 3D usually scales the board; 2D/ortho scales the camera.")]
        [SerializeField] private FitStrategy fitStrategy = FitStrategy.ScaleBoard;

        [Tooltip("Camera used for centering/fitting. Defaults to Camera.main.")]
        [SerializeField] private Camera targetCamera;

        #endregion

        #region Nested Types

        public enum FitStrategy
        {
            /// <summary>Iteratively scale board.cellSize so the projected board fits the viewport.</summary>
            ScaleBoard,

            /// <summary>Adjust Camera.orthographicSize so the board fits. Only valid for orthographic cameras.</summary>
            ScaleCamera,
        }

        #endregion

        #region Runtime State

        private readonly Dictionary<Vector2Int, GameObject> _cells = new();
        private readonly Dictionary<Vector2Int, GameObject> _occupied = new();

        private bool _isBuilding;
        private bool _isFitting;

        // Screen resize detection
        private int _lastScreenW = -1;
        private int _lastScreenH = -1;

        // Baseline ortho size, captured once per build, so we never zoom in past the authored intent
        private float _baseOrthoSize = -1f;

        #endregion

        #region Public Accessors

        public BoardDefinition Board => board;
        public IReadOnlyDictionary<Vector2Int, GameObject> Cells => _cells;

        #endregion

        #region Events

        public event System.Action<Vector2Int, GameObject> OnCubePlaced;
        public event System.Action<Vector2Int, GameObject> OnCubeRemoved;
        public event System.Action<BoardDefinition> OnBoardBuilt;

        #endregion

        #region Lifecycle

        private void Start()
        {
            if (board != null) Build(board);
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
            try
            {
                Clear();
                board = definition;
                if (board == null) return;
                board.Rebuild();

                // Capture the author's ortho size once per build
                var cam = targetCamera != null ? targetCamera : Camera.main;
                if (cam != null && cam.orthographic)
                    _baseOrthoSize = cam.orthographicSize;
                else
                    _baseOrthoSize = -1f;

                // Center the board on this transform by adjusting originPosition
                if (autoCenter && autoCenterOnBuild && !centerInFrontOfCamera)
                    board.originPosition = -board.ComputeLocalCenter() + transform.position;

                // Instantiate cells
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

                // Centering / fitting
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

        public void Clear()
        {
            foreach (var c in _cells.Values) if (c) Destroy(c);
            _cells.Clear();
            _occupied.Clear();
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
        public int GetOccupiedCellCount() => _occupied.Count;

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

            if (autoCenterOnBuild && !centerInFrontOfCamera)
                CenterOnTransform();
            else if (centerInFrontOfCamera)
                CenterInFrontOfCamera();

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
            {
                if (kvp.Value != null)
                    kvp.Value.transform.position = board.GridToWorld(kvp.Key);
            }
        }

        public void CenterInFrontOfCamera()
        {
            if (board == null) return;

            var cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;

            Bounds b = ComputeBoardBounds();
            float distance = Mathf.Max(b.size.magnitude, 5f) * 1.2f;
            Vector3 focusPoint = cam.transform.position + cam.transform.forward * distance;

            Vector3 currentCenter = b.center;
            Vector3 delta = focusPoint - currentCenter;

            board.originPosition += delta;

            foreach (var kvp in _cells)
            {
                if (kvp.Value != null)
                    kvp.Value.transform.position = board.GridToWorld(kvp.Key);
            }
        }

        public Bounds ComputeBoardBounds()
        {
            if (board == null) return new Bounds(transform.position, Vector3.zero);

            bool any = false;
            Bounds b = new Bounds();

            foreach (Vector2Int grid in board.EnumerateValidCells())
            {
                Vector3 c = board.GridToWorld(grid);
                Bounds cellBounds = new Bounds(c, new Vector3(board.cellSize, 0.1f, board.cellSize));
                if (!any) { b = cellBounds; any = true; }
                else b.Encapsulate(cellBounds);
            }

            return any ? b : new Bounds(transform.position, Vector3.zero);
        }

        #endregion

        #region Fitting

        public void ApplyFit()
        {
            switch (fitStrategy)
            {
                case FitStrategy.ScaleBoard:  FitByScalingBoard();  break;
                case FitStrategy.ScaleCamera: FitByScalingCamera(); break;
            }
        }

        /// <summary>
        /// Iteratively scale board.cellSize so the projected board fits the viewport.
        /// When shrinkOnly is true, never grows past the authored cell size — a board
        /// that already fits is left untouched.
        /// </summary>
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

                    float maxX = 0f;
                    float maxY = 0f;

                    for (int s = 0; s < 8; s++)
                    {
                        Vector3 corner = center + new Vector3(
                            (s & 1) == 0 ? -extents.x : extents.x,
                            (s & 2) == 0 ? -extents.y : extents.y,
                            (s & 4) == 0 ? -extents.z : extents.z
                        );

                        Vector3 vp = cam.WorldToViewportPoint(corner);
                        maxX = Mathf.Max(maxX, Mathf.Abs(vp.x - 0.5f));
                        maxY = Mathf.Max(maxY, Mathf.Abs(vp.y - 0.5f));
                    }

                    float limitX = 0.5f - fitPadding;
                    float limitY = 0.5f - fitPadding;

                    float scale = Mathf.Min(
                        limitX / Mathf.Max(maxX, 0.001f),
                        limitY / Mathf.Max(maxY, 0.001f)
                    );

                    // 🆕 Only shrink when the board is too big. Never grow.
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

        /// <summary>
        /// Adjust the orthographic camera so the board fits with padding.
        /// When shrinkOnly is true, never zooms in past the author's baseline
        /// ortho size — only zooms out when the board is too big.
        /// </summary>
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

            float requiredVertical   = halfV / Mathf.Max(0.001f, 1f - fitPadding);
            float requiredHorizontal = halfH / Mathf.Max(0.001f, aspect * (1f - fitPadding));

            float targetSize = Mathf.Max(requiredVertical, requiredHorizontal);

            // 🆕 Only zoom out. Never zoom in past the authored baseline.
            cam.orthographicSize = shrinkOnly
                ? Mathf.Max(_baseOrthoSize, targetSize)
                : targetSize;
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

        #region Debug

        private void OnDrawGizmosSelected()
        {
            if (board == null) return;

            Bounds b = ComputeBoardBounds();
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireCube(b.center, b.size);

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(board.originPosition, board.cellSize * 0.1f);

            Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.6f);
            foreach (var g in board.EnumerateValidCells())
            {
                Vector3 w = board.GridToWorld(g);
                Gizmos.DrawWireCube(w, new Vector3(board.cellSize * 0.9f, 0.02f, board.cellSize * 0.9f));
            }
        }

        #endregion
    }
}
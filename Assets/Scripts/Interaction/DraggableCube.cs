using UnityEngine;
using MyGame.Board;
using MyGame.Match;

namespace MyGame.Interaction
{
    [RequireComponent(typeof(BoxCollider))]
    public class DraggableCube : MonoBehaviour, IDraggable
    {
        [Header("Shape")]
        [SerializeField] private CubeShape shape;

        [Header("Drag Feel")]
        [SerializeField] private float liftHeight = 0.4f;
        [SerializeField] private float dragSmoothTime = 0.06f;
        [SerializeField] private float snapSmoothTime = 0.14f;

        [Header("Ghost Preview")]
        [SerializeField] private GameObject ghostPrefab;
        [SerializeField] private Color validTint   = new(0.3f, 1f, 0.4f, 0.45f);
        [SerializeField] private Color invalidTint = new(1f, 0.3f, 0.3f, 0.45f);

        [Header("Match Resolution")]
        [SerializeField] private MatchResolver matchResolver;

        private GridManager _grid;
        private GameObject _ghost;
        private Renderer _ghostRenderer;

        private Vector3 _homePosition;
        private Vector3 _homeScale;
        private Vector2Int? _occupiedCell;

        private bool _dragging;
        private bool _snapping;

        private Vector3 _dragTarget;
        private Vector3 _snapTargetPos;
        private Vector3 _snapTargetScale;
        private Vector3 _dragVel;
        private Vector3 _snapVel;
        private Vector3 _scaleVel;

        public bool CanDrag => !_dragging && !_snapping;
        public bool IsPlaced => _occupiedCell.HasValue;
        public Vector2Int? OccupiedCell => _occupiedCell;
        public CubeShape Shape => shape;

        public event System.Action<DraggableCube> OnPlacedOnBoard;
        public event System.Action<DraggableCube> OnRemovedFromBoard;

        private void Awake()
        {
            if (shape == null) shape = GetComponentInChildren<CubeShape>();
        }

        private void Start()
        {
            _grid = FindFirstObjectByType<GridManager>();
            if (matchResolver == null) matchResolver = FindFirstObjectByType<MatchResolver>();

            _homePosition = transform.position;
            _homeScale = transform.localScale;

            if (shape != null && _grid != null && _grid.Board != null)
            {
                shape.SetCellSize(_grid.Board.cellSize);
                shape.Build();
            }

            ResizeCollider();
            SpawnGhost();
        }

        private void OnDestroy()
        {
            if (_ghost != null) Destroy(_ghost);
        }

        private void Update()
        {
            if (_dragging) UpdateDrag();
            else if (_snapping) UpdateSnap();
        }

        private void ResizeCollider()
        {
            if (shape == null) return;
            if (!TryGetComponent(out BoxCollider box)) return;

            var size = shape.WorldSize;
            box.size = size;
            box.center = new Vector3(0f, size.y * 0.5f, 0f);
        }

        private void SpawnGhost()
        {
            if (ghostPrefab == null) return;

            _ghost = Instantiate(ghostPrefab, transform.position, Quaternion.identity);
            _ghost.SetActive(false);
            _ghostRenderer = _ghost.GetComponentInChildren<Renderer>();

            if (_grid != null && _grid.Board != null)
            {
                float s = _grid.Board.cellSize * 0.9f;
                _ghost.transform.localScale = new Vector3(s, s, s);
                _ghost.transform.rotation = (_grid.Board.plane == BoardDefinition.BoardPlane.XZ_3D)
                    ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            }
        }

        private void UpdateDrag()
        {
            Vector3 target = _dragTarget + Vector3.up * liftHeight;
            transform.position = Vector3.SmoothDamp(
                transform.position, target, ref _dragVel, dragSmoothTime, Mathf.Infinity, Time.deltaTime);

            UpdateGhost();
        }

        private void UpdateSnap()
        {
            transform.position = Vector3.SmoothDamp(
                transform.position, _snapTargetPos, ref _snapVel, snapSmoothTime, Mathf.Infinity, Time.deltaTime);

            float d = Vector3.SqrMagnitude(transform.position - _snapTargetPos);
            if (d < 0.0001f && _snapVel.sqrMagnitude < 0.0001f)
            {
                transform.position = _snapTargetPos;
                _snapVel = Vector3.zero;
                _snapping = false;
            }
        }

        private void UpdateGhost()
        {
            if (_ghost == null || _grid == null) return;

            Vector2Int grid = _grid.GetGridPosition(transform.position);
            bool canPlace = _grid.CanPlaceAt(grid, gameObject);

            _ghost.transform.position = _grid.GetWorldPosition(grid) + Vector3.up * 0.02f;
            if (_ghostRenderer != null)
                _ghostRenderer.material.color = canPlace ? validTint : invalidTint;
        }

        public void OnPickup(Vector3 worldHit)
        {
            _dragging = true;
            _snapping = false;
            _dragVel = Vector3.zero;
            _snapVel = Vector3.zero;

            _dragTarget = transform.position;
            if (_ghost != null) _ghost.SetActive(true);

            if (_occupiedCell.HasValue)
            {
                UnregisterSubCubes();
                OnRemovedFromBoard?.Invoke(this);
            }
        }

        public void OnDrag(Vector3 worldGroundPoint)
        {
            _dragTarget = worldGroundPoint;
        }

        public void OnDrop()
        {
            _dragging = false;
            if (_ghost != null) _ghost.SetActive(false);

            Vector2Int grid = _grid.GetGridPosition(transform.position);
            bool canPlace = _grid.CanPlaceAt(grid, gameObject);

            if (canPlace)
            {
                if (_occupiedCell.HasValue)
                    _grid.Release(_occupiedCell.Value, gameObject);

                if (_grid.Occupy(grid, gameObject))
                {
                    _occupiedCell = grid;
                    _homePosition = _grid.GetWorldPosition(grid);
                    _homeScale = Vector3.one * _grid.Board.cellSize;

                    // Write sub-cube colors into the board's sub-grid
                    RegisterSubCubes(grid);

                    OnPlacedOnBoard?.Invoke(this);

                    // ⚡ Immediately resolve matches — same frame.
                    if (matchResolver != null)
                        matchResolver.ResolveAt(grid);

                    if (this == null) return;   // resolver may have destroyed us

                    StartSnap(_homePosition);
                    return;
                }
            }

            StartSnap(_homePosition);
        }

        private void StartSnap(Vector3 target)
        {
            _snapTargetPos = target;
            _snapVel = Vector3.zero;
            _snapping = true;
        }

        private void RegisterSubCubes(Vector2Int cell)
        {
            if (_grid == null || _grid.SubGrid == null) return;
            if (shape == null) return;

            foreach (var sub in shape.Blocks)
            {
                if (sub == null) continue;
                _grid.SubGrid.Register(sub, cell, sub.SlotPosition, sub.SlotSize);
            }
        }

        private void UnregisterSubCubes()
        {
            if (_grid == null || _grid.SubGrid == null) return;
            if (shape == null) return;

            foreach (var sub in shape.Blocks)
            {
                if (sub == null) continue;
                _grid.SubGrid.Unregister(sub);
            }
        }
    }
}
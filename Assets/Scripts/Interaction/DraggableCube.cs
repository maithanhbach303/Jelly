using UnityEngine;
using MyGame.Board;

namespace MyGame.Interaction
{
    [RequireComponent(typeof(BoxCollider))]
    public class DraggableCube : MonoBehaviour, IDraggable
    {
        #region Inspector Fields

        [Header("Shape")]
        [SerializeField] private CubeShape shape;

        [Header("Drag Feel")]
        [SerializeField] private float liftHeight = 0.4f;
        [SerializeField] private float dragSmoothTime = 0.06f;
        [SerializeField] private float snapSmoothTime = 0.14f;

        [Header("Placement Scale")]
        [Range(0.5f, 1.0f)]
        [SerializeField] private float placedFitRatio = 1.0f;

        [Header("Landing Pulse")]
        [SerializeField] private float landingPulseDuration = 0.18f;
        [Range(0f, 0.6f)]
        [SerializeField] private float landingSquash = 0.25f;

        [Header("Ghost Preview")]
        [SerializeField] private GameObject ghostPrefab;
        [SerializeField] private Color validTint   = new(0.3f, 1f, 0.4f, 0.45f);
        [SerializeField] private Color invalidTint = new(1f, 0.3f, 0.3f, 0.45f);

        #endregion

        #region Runtime State

        private GridManager _grid;
        private GameObject _ghost;
        private Renderer _ghostRenderer;

        private Vector3 _homePosition;
        private Vector3 _homeScale;
        private Vector2Int? _occupiedCell;

        private bool _dragging;
        private bool _snapping;
        private bool _pulseActive;

        private Vector3 _dragTarget;
        private Vector3 _snapTargetPosition;
        private Vector3 _snapTargetScale;
        private float _snapStartTime;

        private Vector3 _dragVelocity;
        private Vector3 _snapVelocity;
        private Vector3 _scaleVelocity;

        // Safety: force-finish a snap if it takes longer than this (in case a coroutine stalls).
        private const float SnapTimeoutSeconds = 2f;

        #endregion

        #region Public Accessors

        /// <summary>
        /// False while dragging, while snapping, or when this cube is already placed on the board.
        /// Placed cubes are locked in place — they leave the board only via match resolution.
        /// </summary>
        public bool CanDrag => !_dragging && !_snapping && !IsPlaced;

        public bool IsPlaced => _occupiedCell.HasValue;
        public Vector2Int? OccupiedCell => _occupiedCell;
        public CubeShape Shape => shape;

        /// <summary>Frame when this cube was last placed. Used by cleanup guards.</summary>
        public int PlacedFrame { get; private set; } = -1;

        #endregion

        #region Events

        /// <summary>Fires after a successful placement (sub-cubes registered, snap started).</summary>
        public event System.Action<DraggableCube> OnPlacedOnBoard;

        /// <summary>Fires if this cube is ever removed from the board (currently only via external code).</summary>
        public event System.Action<DraggableCube> OnRemovedFromBoard;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            if (shape == null) shape = GetComponentInChildren<CubeShape>();
        }

        private void Start()
        {
            _grid = FindFirstObjectByType<GridManager>();
            _homePosition = transform.position;
            _homeScale = transform.localScale;

            if (shape != null && _grid != null && _grid.Board != null)
            {
                shape.SetCellSize(_grid.Board.cellSize);
                shape.Build();
            }

            ResizeColliderToShape();
            SpawnGhost();

            if (_grid != null) _grid.OnBoardBuilt += HandleBoardRebuilt;
        }

        private void OnEnable()
        {
            if (_grid != null)
            {
                _grid.OnBoardBuilt -= HandleBoardRebuilt;
                _grid.OnBoardBuilt += HandleBoardRebuilt;
            }
        }

        private void OnDisable()
        {
            if (_grid != null) _grid.OnBoardBuilt -= HandleBoardRebuilt;
        }

        private void OnDestroy()
        {
            if (_grid != null) _grid.OnBoardBuilt -= HandleBoardRebuilt;
            if (_ghost != null) Destroy(_ghost);
        }

        private void Update()
        {
            if (_dragging) UpdateDrag();
            else if (_snapping) UpdateSnap();
        }

        private void LateUpdate()
        {
            // Safety net — if a snap is stuck (interrupted coroutine, timescale weirdness),
            // force-complete it so CanDrag can return true on tray cubes and the placed
            // cube doesn't sit half-animated forever.
            if (_snapping && Time.time - _snapStartTime > SnapTimeoutSeconds)
            {
                FinishSnap();
            }
        }

        #endregion

        #region Shape Helpers

        private void ResizeColliderToShape()
        {
            if (shape == null) return;
            if (!TryGetComponent(out BoxCollider box)) return;

            Vector3 worldSize = shape.WorldSize;
            box.size = worldSize;
            box.center = new Vector3(0f, worldSize.y * 0.5f, 0f);
        }

        private float CurrentCellSize()
        {
            if (_grid != null && _grid.Board != null) return _grid.Board.cellSize;
            return _homeScale.x / Mathf.Max(0.0001f, placedFitRatio);
        }

        private Vector3 GetSnapWorldPosition(Vector2Int grid, Vector3 cubeScale)
            => _grid.GetWorldPosition(grid);

        #endregion

        #region Ghost Preview

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
                    ? Quaternion.Euler(90f, 0f, 0f)
                    : Quaternion.identity;
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

        #endregion

        #region Drag

        private void UpdateDrag()
        {
            Vector3 target = _dragTarget + Vector3.up * liftHeight;

            transform.position = Vector3.SmoothDamp(
                transform.position, target, ref _dragVelocity,
                dragSmoothTime, Mathf.Infinity, Time.deltaTime
            );

            UpdateGhost();
        }

        #endregion

        #region Snap

        private void StartSnap(Vector3 targetPosition, Vector3 targetScale, bool playLandingPulse)
        {
            _snapTargetPosition = targetPosition;
            _snapTargetScale = targetScale;
            _snapStartTime = Time.time;
            _snapping = true;

            _snapVelocity = Vector3.zero;
            _scaleVelocity = Vector3.zero;

            if (playLandingPulse) StartCoroutine(LandingPulseRoutine());
        }

        private void UpdateSnap()
        {
            transform.position = Vector3.SmoothDamp(
                transform.position, _snapTargetPosition, ref _snapVelocity,
                snapSmoothTime, Mathf.Infinity, Time.deltaTime
            );

            if (!_pulseActive)
            {
                transform.localScale = Vector3.SmoothDamp(
                    transform.localScale, _snapTargetScale, ref _scaleVelocity,
                    snapSmoothTime * 0.7f, Mathf.Infinity, Time.deltaTime
                );
            }

            float distSqr  = Vector3.SqrMagnitude(transform.position - _snapTargetPosition);
            float speedSqr = _snapVelocity.sqrMagnitude;

            if (distSqr < 0.0001f && speedSqr < 0.0001f && !_pulseActive)
            {
                FinishSnap();
            }
        }

        private void FinishSnap()
        {
            transform.position = _snapTargetPosition;
            transform.localScale = _snapTargetScale;
            _snapVelocity = Vector3.zero;
            _scaleVelocity = Vector3.zero;

            _pulseActive = false;
            _snapping = false;
        }

        #endregion

        #region Landing Pulse

        private System.Collections.IEnumerator LandingPulseRoutine()
        {
            _pulseActive = true;
            yield return new WaitForSeconds(snapSmoothTime * 0.8f);

            Vector3 baseScale = _snapTargetScale;
            float t = 0f;

            while (t < landingPulseDuration)
            {
                if (_dragging)
                {
                    _pulseActive = false;
                    yield break;
                }

                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / landingPulseDuration);
                float bell = 1f - Mathf.Abs(k * 2f - 1f);
                float squash = 1f - landingSquash * bell;
                float bulge  = 1f + (1f - squash) * 0.5f;

                transform.localScale = new Vector3(
                    baseScale.x * bulge,
                    baseScale.y * squash,
                    baseScale.z * bulge
                );

                yield return null;
            }

            FinishSnap();
        }

        #endregion

        #region IDraggable

        /// <summary>
        /// Called only for tray cubes. Placed cubes return CanDrag = false, so DragController never calls this on them.
        /// </summary>
        public void OnPickup(Vector3 worldHit)
        {
            StopAllCoroutines();
            _pulseActive = false;
            _scaleVelocity = Vector3.zero;
            _snapVelocity = Vector3.zero;

            _dragging = true;
            _snapping = false;
            _dragVelocity = Vector3.zero;
            _dragTarget = transform.position;

            if (_ghost != null) _ghost.SetActive(true);
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

            if (canPlace && _grid.Occupy(grid, gameObject))
            {
                _occupiedCell = grid;
                PlacedFrame = Time.frameCount;

                _homeScale = Vector3.one * (CurrentCellSize() * placedFitRatio);
                _homePosition = GetSnapWorldPosition(grid, _homeScale);

                RegisterSubCubes(grid);

                OnPlacedOnBoard?.Invoke(this);
                StartSnap(_homePosition, _homeScale, playLandingPulse: true);
                return;
            }

            // Illegal placement — return to tray spot
            StartSnap(_homePosition, _homeScale, playLandingPulse: false);
        }

        #endregion

        #region Sub-Grid Registration

        private void RegisterSubCubes(Vector2Int cell)
        {
            if (_grid == null || _grid.SubGrid == null) return;
            if (shape == null) return;

            var blocks = shape.Blocks;
            for (int i = 0; i < blocks.Count; i++)
            {
                var sub = blocks[i];
                if (sub == null) continue;
                _grid.SubGrid.Register(sub, cell, sub.SlotPosition, sub.SlotSize);
            }
        }

        private void UnregisterSubCubes()
        {
            if (_grid == null || _grid.SubGrid == null) return;
            if (shape == null) return;

            var blocks = shape.Blocks;
            for (int i = 0; i < blocks.Count; i++)
            {
                var sub = blocks[i];
                if (sub == null) continue;
                _grid.SubGrid.Unregister(sub);
            }
        }

        #endregion

        #region Board Rebuild

        private void HandleBoardRebuilt(BoardDefinition def)
        {
            if (def == null) return;

            if (shape != null)
            {
                shape.SetCellSize(def.cellSize);
                shape.Build();
                ResizeColliderToShape();
            }

            if (_occupiedCell.HasValue && !_dragging)
            {
                UnregisterSubCubes();
                RegisterSubCubes(_occupiedCell.Value);

                _homeScale = Vector3.one * (def.cellSize * placedFitRatio);
                _homePosition = GetSnapWorldPosition(_occupiedCell.Value, _homeScale);

                if (!_snapping && !_pulseActive)
                    StartSnap(_homePosition, _homeScale, playLandingPulse: false);
            }
        }

        #endregion
    }
}
using UnityEngine;
using MyGame.Board;

namespace MyGame.Interaction
{
    [RequireComponent(typeof(Collider))]
    public class DraggableCube : MonoBehaviour, IDraggable
    {
        #region Inspector Fields

        [Header("Drag Feel")]
        [Tooltip("How high the cube lifts while being dragged.")]
        [SerializeField] private float liftHeight = 0.5f;

        [Tooltip("Time (seconds) to smoothly reach the pointer.")]
        [SerializeField] private float dragSmoothTime = 0.06f;

        [Tooltip("Time (seconds) to snap to home (or a cell) after release.")]
        [SerializeField] private float snapSmoothTime = 0.14f;

        [Header("Placement Scale")]
        [Tooltip("Scale relative to cell size. 1.0 = flush with the cell, 0.95 = slight gap.")]
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

        // The cube's "home" — where and how big it rests when not being dragged.
        // After a successful placement, this becomes the new cell's position + size.
        // After a failed drop, the cube snaps back here unchanged.
        private Vector3 _homePosition;
        private Vector3 _homeScale;

        private Vector2Int? _occupiedCell;

        private bool _dragging;
        private bool _snapping;
        private bool _pulseActive;

        // Drag / snap targets
        private Vector3 _dragTarget;
        private Vector3 _snapTargetPosition;
        private Vector3 _snapTargetScale;

        // SmoothDamp caches
        private Vector3 _dragVelocity;
        private Vector3 _snapVelocity;
        private Vector3 _scaleVelocity;

        #endregion

        #region Public Accessors

        public bool CanDrag => !_dragging && !_snapping;
        public bool IsPlaced => _occupiedCell.HasValue;
        public Vector2Int? OccupiedCell => _occupiedCell;

        #endregion

        #region Events

        public event System.Action<DraggableCube> OnPlacedOnBoard;
        public event System.Action<DraggableCube> OnRemovedFromBoard;

        #endregion

        #region Lifecycle

        private void Start()
        {
            _grid = FindFirstObjectByType<GridManager>();

            // Initial home = wherever we spawned. Tray spawns us at tray scale.
            _homePosition = transform.position;
            _homeScale = transform.localScale;

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

        #endregion

        #region Setup Helpers

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

        /// <summary>Cell size of the board right now, safe against missing grid/board.</summary>
        private float CurrentCellSize()
        {
            if (_grid != null && _grid.Board != null)
                return _grid.Board.cellSize;
            return _homeScale.x / Mathf.Max(0.0001f, placedFitRatio);
        }

        #endregion

        #region Drag

        private void UpdateDrag()
        {
            Vector3 target = _dragTarget + Vector3.up * liftHeight;

            transform.position = Vector3.SmoothDamp(
                transform.position,
                target,
                ref _dragVelocity,
                dragSmoothTime,
                Mathf.Infinity,
                Time.deltaTime
            );

            UpdateGhost();
        }

        #endregion

        #region Snap

        private void StartSnap(Vector3 targetPosition, Vector3 targetScale, bool playLandingPulse)
        {
            _snapTargetPosition = targetPosition;
            _snapTargetScale = targetScale;
            _snapping = true;

            _snapVelocity = Vector3.zero;
            _scaleVelocity = Vector3.zero;

            if (playLandingPulse)
                StartCoroutine(LandingPulseRoutine());
        }

        private void UpdateSnap()
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                _snapTargetPosition,
                ref _snapVelocity,
                snapSmoothTime,
                Mathf.Infinity,
                Time.deltaTime
            );

            if (!_pulseActive)
            {
                transform.localScale = Vector3.SmoothDamp(
                    transform.localScale,
                    _snapTargetScale,
                    ref _scaleVelocity,
                    snapSmoothTime * 0.7f,
                    Mathf.Infinity,
                    Time.deltaTime
                );
            }

            float distSqr  = Vector3.SqrMagnitude(transform.position - _snapTargetPosition);
            float speedSqr = _snapVelocity.sqrMagnitude;

            if (distSqr < 0.0001f && speedSqr < 0.0001f)
            {
                transform.position = _snapTargetPosition;
                _snapVelocity = Vector3.zero;

                if (!_pulseActive)
                {
                    transform.localScale = _snapTargetScale;
                    _snapping = false;
                }
            }
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
                if (_dragging) { _pulseActive = false; yield break; }

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

            transform.localScale = baseScale;
            _scaleVelocity = Vector3.zero;

            _pulseActive = false;
            _snapping = false;
        }

        #endregion

        #region IDraggable

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

            if (_occupiedCell.HasValue)
                OnRemovedFromBoard?.Invoke(this);
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
                    // ✅ Successful placement — this becomes the new home.
                    _occupiedCell = grid;

                    _homePosition = _grid.GetWorldPosition(grid);
                    _homeScale = Vector3.one * (CurrentCellSize() * placedFitRatio);

                    OnPlacedOnBoard?.Invoke(this);

                    StartSnap(_homePosition, _homeScale, playLandingPulse: true);
                    return;
                }
            }

            // ❌ Failed drop — return to the previous slot's position AND size.
            StartSnap(_homePosition, _homeScale, playLandingPulse: false);
        }

        #endregion

        #region Ghost Preview

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

        #region Board Rebuild

        private void HandleBoardRebuilt(BoardDefinition def)
        {
            if (def == null) return;

            // If we're placed, our home becomes a cell in the (possibly resized) grid.
            // Recompute home position/scale to match the new layout.
            if (_occupiedCell.HasValue && !_dragging)
            {
                _homePosition = _grid.GetWorldPosition(_occupiedCell.Value);
                _homeScale = Vector3.one * (def.cellSize * placedFitRatio);

                if (!_snapping && !_pulseActive)
                    StartSnap(_homePosition, _homeScale, playLandingPulse: false);
            }
        }

        #endregion
    }
}
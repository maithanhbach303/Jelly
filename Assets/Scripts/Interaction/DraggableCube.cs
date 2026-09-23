using UnityEngine;
using MyGame.Board;
using MyGame.Levels;

namespace MyGame.Interaction
{
    [RequireComponent(typeof(BoxCollider))]
    public class DraggableCube : MonoBehaviour, IDraggable
    {
        #region Inspector

        [Header("Shape")]
        [SerializeField] private CubeShape shape;

        [Header("Drag Feel")]
        [SerializeField] private float liftHeight = 0.4f;
        [SerializeField] private float dragSmoothTime = 0.06f;
        [SerializeField] private float snapSmoothTime = 0.14f;

        [Range(0.5f, 1.0f)]
        [SerializeField] private float placedFitRatio = 1.0f;

        [Header("Landing Pulse")]
        [SerializeField] private float landingPulseDuration = 0.18f;
        [Range(0f, 0.6f)] [SerializeField] private float landingSquash = 0.25f;

        [Header("Ghost Preview")]
        [SerializeField] private GameObject ghostPrefab;
        [SerializeField] private Color validTint = new(0.3f, 1f, 0.4f, 0.45f);
        [SerializeField] private Color invalidTint = new(1f, 0.3f, 0.3f, 0.45f);

        [Header("Rotation")]
        [Tooltip("0 = 0°, 1 = 90°, 2 = 180°, 3 = 270°")]
        [Range(0, 3)]
        [SerializeField] private int yawQuarterTurns = 0;

        [Header("Debug")]
        [SerializeField] private bool logRotation = false;

        #endregion

        #region Ownership Flags

        [HideInInspector] public bool IsPrefilled = false;
        [HideInInspector] public bool IsTrayOwned = false;

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
        private bool _canDrag = true;
        private bool _initialized;

        private Vector3 _dragTarget;
        private Vector3 _snapTargetPosition;
        private Vector3 _snapTargetScale;
        private Vector3 _dragVelocity;
        private Vector3 _snapVelocity;
        private Vector3 _scaleVelocity;

        // Pending sequence spec
        private bool _hasPendingSpec;
        private TraySequence.CubeSpec _pendingSpec;
        private CubePalette _pendingSpecPalette;

        #endregion

        #region Accessors

        public bool CanDrag => _canDrag && !_dragging && !_snapping;
        public bool IsPlaced => _occupiedCell.HasValue;
        public Vector2Int? OccupiedCell => _occupiedCell;
        public CubeShape Shape => shape;
        public bool HasPendingSpec => _hasPendingSpec;

        public int YawQuarterTurns => yawQuarterTurns;
        public Quaternion HomeRotation => Quaternion.Euler(0f, yawQuarterTurns * 90f, 0f);

        #endregion

        #region Events

        public event System.Action<DraggableCube> OnPlacedOnBoard;
        public event System.Action<DraggableCube> OnRemovedFromBoard;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            if (shape == null)
                shape = GetComponentInChildren<CubeShape>();
        }

        private void Start()
        {
            if (_initialized) return;
            Initialize();
        }

        private void Initialize()
        {
            _grid = FindFirstObjectByType<GridManager>();
            _homePosition = transform.position;
            _homeScale = transform.localScale;

            if (shape != null)
            {
                if (_grid != null && _grid.Board != null)
                    shape.SetCellSize(_grid.Board.cellSize);

                shape.Build();
            }

            // Reassert pending spec (shape type + palette + colors) and rotation
            // AFTER Build() so nothing gets wiped.
            ReapplyPendingSpec();
            ApplyHomeRotation();

            ResizeColliderToShape();
            SpawnGhost();

            if (_grid != null) _grid.OnBoardBuilt += HandleBoardRebuilt;

            _initialized = true;

            if (logRotation)
                Debug.Log($"[DraggableCube '{name}'] Initialized rotation={yawQuarterTurns * 90f}°", this);
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

        #region Public API

        public void SetYawQuarterTurns(int turns)
        {
            yawQuarterTurns = ((turns % 4) + 4) % 4;
            ApplyHomeRotation();

            if (_occupiedCell.HasValue && _grid != null && _grid.SubGrid != null)
            {
                UnregisterSubCubes();
                RegisterSubCubes(_occupiedCell.Value);
            }

            if (logRotation)
                Debug.Log($"[DraggableCube '{name}'] SetYawQuarterTurns -> {yawQuarterTurns} ({yawQuarterTurns * 90f}°)", this);
        }

        public void InitializeForPrefill(GridManager grid)
        {
            if (_initialized)
            {
                ApplyHomeRotation();
                return;
            }

            _grid = grid;
            _homePosition = transform.position;
            _homeScale = transform.localScale;

            if (shape != null)
            {
                if (_grid != null && _grid.Board != null)
                    shape.SetCellSize(_grid.Board.cellSize);

                shape.Build();
            }

            ReapplyPendingSpec();
            ApplyHomeRotation();

            ResizeColliderToShape();
            _initialized = true;

            if (logRotation)
                Debug.Log($"[DraggableCube '{name}'] InitializeForPrefill rotation={yawQuarterTurns * 90f}°", this);
        }

        public void SetPendingSpec(TraySequence.CubeSpec spec, CubePalette resolvedPalette)
        {
            _pendingSpec = spec;
            _pendingSpecPalette = resolvedPalette;
            _hasPendingSpec = true;

            SetYawQuarterTurns(spec.rotationQuarterTurns);
            ReapplyPendingSpec();

            if (logRotation)
                Debug.Log($"[DraggableCube '{name}'] Sequence rotation = {spec.rotationQuarterTurns} ({spec.rotationQuarterTurns * 90f}°)", this);
        }

        public void ClearPendingSpec()
        {
            _hasPendingSpec = false;
            _pendingSpec = default;
            _pendingSpecPalette = null;
        }

        public void PlaceImmediate(bool fireEvents = true, bool playPulse = false)
        {
            _dragging = false;
            _snapping = false;
            _pulseActive = false;

            if (_ghost != null) _ghost.SetActive(false);

            TryPlace(playPulse, fireEvents);
        }

        public void SetDraggable(bool value) => _canDrag = value;

        public void CleanupForDestroy()
        {
            if (_occupiedCell.HasValue && _grid != null)
            {
                UnregisterSubCubes();
                _grid.Release(_occupiedCell.Value, gameObject);
                _occupiedCell = null;

                OnRemovedFromBoard?.Invoke(this);
            }

            StopAllCoroutines();

            if (_ghost != null)
            {
                Destroy(_ghost);
                _ghost = null;
            }
        }

        #endregion

        #region Rotation

        private void ApplyHomeRotation()
        {
            transform.localRotation = HomeRotation;

            if (logRotation)
                Debug.Log($"[DraggableCube '{name}'] ApplyHomeRotation -> {yawQuarterTurns * 90f}°", this);
        }

        private static Vector2 RotateSlotCenter(Vector2 c, int yaw)
        {
            switch (((yaw % 4) + 4) % 4)
            {
                case 1:  return new Vector2(c.y, 1f - c.x);
                case 2:  return new Vector2(1f - c.x, 1f - c.y);
                case 3:  return new Vector2(1f - c.y, c.x);
                default: return c;
            }
        }

        private static Vector2 RotateSlotSize(Vector2 size, int yaw)
        {
            int k = ((yaw % 4) + 4) % 4;
            return (k == 1 || k == 3) ? new Vector2(size.y, size.x) : size;
        }

        #endregion

        #region Spec Application

        /// <summary>
        /// Re-asserts the pending spec: shape type, palette, and colors.
        /// Safe to call after shape.Build(); it rebuilds blocks if the shape
        /// type changed, then re-colors them.
        /// </summary>
        private void ReapplyPendingSpec()
        {
            if (!_hasPendingSpec) return;
            if (shape == null) return;

            // Reassert shape type FIRST — this may rebuild blocks.
            shape.SetShape(_pendingSpec.shapeType);

            // Then palette + colors on the freshly built blocks.
            shape.SetPalette(_pendingSpecPalette);
            ApplySpecColors(shape, _pendingSpec.colors);
        }

        private static void ApplySpecColors(CubeShape targetShape, CubeColor[] colors)
        {
            if (targetShape == null) return;
            if (colors == null || colors.Length == 0) return;

            var blocks = targetShape.Blocks;
            for (int i = 0; i < blocks.Count; i++)
            {
                var sub = blocks[i];
                if (sub == null) continue;

                int idx = Mathf.Min(i, colors.Length - 1);
                sub.SetColor(colors[idx]);
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

            _ghost = Instantiate(ghostPrefab, transform.position, HomeRotation);
            _ghost.SetActive(false);
            _ghostRenderer = _ghost.GetComponentInChildren<Renderer>();

            if (_grid != null && _grid.Board != null)
            {
                float s = _grid.Board.cellSize * 0.9f;
                _ghost.transform.localScale = new Vector3(s, s, s);
                _ghost.transform.localRotation = HomeRotation;
            }
        }

        private void UpdateGhost()
        {
            if (_ghost == null || _grid == null) return;

            Vector2Int grid = _grid.GetGridPosition(transform.position);
            bool canPlace = _grid.CanPlaceAt(grid, gameObject);

            _ghost.transform.position = _grid.GetWorldPosition(grid) + Vector3.up * 0.02f;
            _ghost.transform.localRotation = HomeRotation;

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
                dragSmoothTime, Mathf.Infinity, Time.deltaTime);

            transform.localRotation = HomeRotation;
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

            if (playLandingPulse) StartCoroutine(LandingPulseRoutine());
        }

        private void UpdateSnap()
        {
            transform.position = Vector3.SmoothDamp(
                transform.position, _snapTargetPosition, ref _snapVelocity,
                snapSmoothTime, Mathf.Infinity, Time.deltaTime);

            transform.localRotation = Quaternion.RotateTowards(
                transform.localRotation, HomeRotation, 720f * Time.deltaTime);

            if (Quaternion.Angle(transform.localRotation, HomeRotation) < 0.01f)
                transform.localRotation = HomeRotation;

            if (!_pulseActive)
            {
                transform.localScale = Vector3.SmoothDamp(
                    transform.localScale, _snapTargetScale, ref _scaleVelocity,
                    snapSmoothTime * 0.7f, Mathf.Infinity, Time.deltaTime);
            }

            float distSqr = Vector3.SqrMagnitude(transform.position - _snapTargetPosition);
            float speedSqr = _snapVelocity.sqrMagnitude;

            if (distSqr < 0.0001f && speedSqr < 0.0001f)
            {
                transform.position = _snapTargetPosition;
                _snapVelocity = Vector3.zero;

                if (!_pulseActive)
                {
                    transform.localScale = _snapTargetScale;
                    transform.localRotation = HomeRotation;
                    _scaleVelocity = Vector3.zero;
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
                float bulge = 1f + (1f - squash) * 0.5f;

                transform.localScale = new Vector3(
                    baseScale.x * bulge,
                    baseScale.y * squash,
                    baseScale.z * bulge);

                yield return null;
            }

            transform.position = _snapTargetPosition;
            transform.localScale = baseScale;
            transform.localRotation = HomeRotation;
            _scaleVelocity = Vector3.zero;
            _snapVelocity = Vector3.zero;
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

        public void OnDrag(Vector3 worldGroundPoint) => _dragTarget = worldGroundPoint;

        public void OnDrop()
        {
            _dragging = false;
            if (_ghost != null) _ghost.SetActive(false);

            TryPlace(playPulse: true, fireEvents: true);
        }

        #endregion

        #region Core Placement

        private void TryPlace(bool playPulse, bool fireEvents)
        {
            if (_grid == null) { ReturnHome(); return; }

            Vector2Int grid = _grid.GetGridPosition(transform.position);
            bool canPlace = _grid.CanPlaceAt(grid, gameObject);

            if (canPlace)
            {
                if (_occupiedCell.HasValue)
                {
                    UnregisterSubCubes();
                    _grid.Release(_occupiedCell.Value, gameObject);
                }

                if (_grid.Occupy(grid, gameObject))
                {
                    _occupiedCell = grid;
                    _homeScale = Vector3.one * (CurrentCellSize() * placedFitRatio);
                    _homePosition = GetSnapWorldPosition(grid, _homeScale);

                    RegisterSubCubes(grid);

                    if (fireEvents) OnPlacedOnBoard?.Invoke(this);

                    if (playPulse)
                    {
                        StartSnap(_homePosition, _homeScale, true);
                    }
                    else
                    {
                        transform.position = _homePosition;
                        transform.localScale = _homeScale;
                        transform.localRotation = HomeRotation;
                        _snapping = false;
                        _pulseActive = false;
                    }

                    return;
                }
            }

            ReturnHome();
        }

        private void ReturnHome()
        {
            if (_snapping || _pulseActive) return;

            transform.position = _homePosition;
            transform.localScale = _homeScale;
            transform.localRotation = HomeRotation;
            _snapping = false;
            _pulseActive = false;
        }

        #endregion

        #region Sub-Grid Registration

        private void RegisterSubCubes(Vector2Int cell)
        {
            if (_grid == null || _grid.SubGrid == null) return;
            if (shape == null) return;

            int yaw = yawQuarterTurns;
            var blocks = shape.Blocks;

            for (int i = 0; i < blocks.Count; i++)
            {
                var sub = blocks[i];
                if (sub == null) continue;

                Vector2 rotatedCenter = RotateSlotCenter(sub.SlotPosition, yaw);
                Vector2 rotatedSize   = RotateSlotSize(sub.SlotSize, yaw);

                _grid.SubGrid.Register(sub, cell, rotatedCenter, rotatedSize);
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
            if (IsPrefilled) return;

            if (shape != null)
            {
                shape.SetCellSize(def.cellSize);
                shape.Build();

                ReapplyPendingSpec();
                ApplyHomeRotation();

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
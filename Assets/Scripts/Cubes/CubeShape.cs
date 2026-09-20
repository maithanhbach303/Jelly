using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Interaction
{
    /// <summary>
    /// Which fixed layout a CubeShape should build.
    /// Every shape fits inside a single 1×1 board cell.
    /// </summary>
    public enum CubeShapeType
    {
        /// <summary>1 whole block filling the cell.</summary>
        Whole,

        /// <summary>2×2 grid of small (quarter-cell) blocks.</summary>
        FourSmall,

        /// <summary>2 half blocks side by side (spanning X).</summary>
        TwoHalf,


        /// <summary>1 half block at the bottom + 2 small blocks on top.</summary>
        HalfAndTwoSmall,
    }

    /// <summary>
    /// Builds a single-cell cube shape from pre-sized sub-cube prefabs.
    ///
    /// Conventions:
    ///  - Shape pivot is at the BOTTOM-CENTER of the cell footprint.
    ///  - Sub-cube prefabs are authored at their FINAL size (no runtime scaling).
    ///  - All layout math is in cell units, with (0,0) at the cell's bottom-left
    ///    and (1,1) at its top-right, then offset by -shapeCenter to center on the pivot.
    /// </summary>
    public class CubeShape : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Sub-Cube Prefabs (authored at final size)")]
        [Tooltip("Whole cube: 1 × 1 × 1, pivot at bottom-center.")]
        [SerializeField] private GameObject wholeCube;

        [Tooltip("Half cube: 0.5 × 1 × 1 (spans X), pivot at bottom-center.")]
        [SerializeField] private GameObject halfCube;

        [Tooltip("Small cube: 0.5 × 1 × 0.5, pivot at bottom-center.")]
        [SerializeField] private GameObject smallCube;

        [Header("Shape")]
        [Tooltip("Which layout to build. The tray can override this before Start().")]
        [SerializeField] private CubeShapeType shapeType = CubeShapeType.Whole;

        [Tooltip("Rotate the entire layout around Y. 0-3 quarter-turns.")]
        [Range(0, 3)]
        [SerializeField] private int rotationQuarterTurns = 0;

        [Tooltip("Cell size in world units. Set by the tray / DraggableCube at runtime.")]
        [SerializeField] private float cellSize = 1f;

        #endregion

        #region Runtime State

        private readonly List<SubCube> _spawned = new();
        private bool _built;

        public CubeShapeType ShapeType => shapeType;
        public int RotationQuarterTurns => rotationQuarterTurns;
        public float CellSize => cellSize;

        /// <summary>Footprint in cell units. All shapes fit a single cell.</summary>
        public Vector2 ShapeSize => new Vector2(1f, 1f);

        /// <summary>Footprint in world units.</summary>
        public Vector3 WorldSize => new Vector3(cellSize, cellSize, cellSize);

        /// <summary>Local center offset from the pivot (pivot is at bottom-center).</summary>
        public Vector3 LocalCenter => new Vector3(0f, cellSize * 0.5f, 0f);

        public IReadOnlyList<SubCube> Blocks => _spawned;

        /// <summary>Fired whenever the shape is rebuilt or a sub-cube is removed.</summary>
        public event System.Action<CubeShape> OnShapeChanged;

        #endregion

        #region Public API

        /// <summary>Set the cell size used for layout. Call before Build() (or the tray sets it at spawn).</summary>
        public void SetCellSize(float size)
        {
            cellSize = size;
        }

        /// <summary>Swap the shape type and optional rotation, then rebuild immediately.</summary>
        public void SetShape(CubeShapeType type, int quarterTurns = 0)
        {
            shapeType = type;
            rotationQuarterTurns = Mathf.Clamp(quarterTurns, 0, 3);
            Build();
        }

        /// <summary>Instantiate the sub-cube prefabs according to the current shape type.</summary>
        public void Build()
        {
            Clear();

            if (wholeCube == null || halfCube == null || smallCube == null)
            {
                Debug.LogError("CubeShape: one or more sub-cube prefabs are missing.", this);
                return;
            }

            var slots = GetSlots(shapeType, rotationQuarterTurns);

            // The cell's geometric center is (0.5, 0.5) in cell units.
            // Pivot is bottom-center → offset each slot's position by -shapeCenter.
            Vector2 shapeCenter = ShapeSize * 0.5f;   // (0.5, 0.5)

            foreach (var slot in slots)
            {
                GameObject prefab = PickPrefab(slot.kind);
                if (prefab == null) continue;

                GameObject go = Instantiate(prefab, transform);

                // Position: horizontal offset from shape center, Y at pivot level (0)
                go.transform.localPosition = new Vector3(
                    (slot.center.x - shapeCenter.x) * cellSize,
                    0f,
                    (slot.center.y - shapeCenter.y) * cellSize
                );

                // Yaw: rotate half cubes so a single prefab can span X or Z
                go.transform.localRotation = Quaternion.Euler(0f, slot.yaw * 90f, 0f);

                // NOTE: no localScale change — prefabs are authored at final size.

                // Optional uniform scale if the board's cell size differs from 1
                if (!Mathf.Approximately(cellSize, 1f))
                    go.transform.localScale = Vector3.one * cellSize;

                // Wire up sub-cube logic
                if (go.TryGetComponent(out SubCube sub))
                {
                    sub.Initialize(this, slot.center, slot.size);
                    _spawned.Add(sub);
                }
                else
                {
                    Debug.LogWarning(
                        $"CubeShape: prefab '{prefab.name}' has no SubCube component.",
                        prefab
                    );
                }
            }

            _built = true;
            OnShapeChanged?.Invoke(this);
        }

        /// <summary>Destroy all spawned sub-cubes.</summary>
        public void Clear()
        {
            foreach (var s in _spawned)
                if (s != null) Destroy(s.gameObject);
            _spawned.Clear();

            // Catch anything parented but untracked (e.g. editor preview leftovers)
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            _built = false;
        }

        /// <summary>Remove a specific sub-cube from the shape (e.g. a bomb that exploded).</summary>
        public void RemoveBlock(SubCube block)
        {
            if (block == null) return;
            _spawned.Remove(block);
            OnShapeChanged?.Invoke(this);
        }

        #endregion

        #region Lifecycle

        private void Start()
        {
            // The tray may have already called SetShape() before we ran.
            // If not, build from the inspector-authored defaults.
            if (!_built) Build();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Live preview while in Play Mode and tweaking the shape in the inspector.
            if (Application.isPlaying && isActiveAndEnabled && _built)
                Build();
        }
#endif

        #endregion

        #region Slot Layout

        private enum BlockKind { Whole, Half, Small }

        private struct Slot
        {
            public Vector2 center;   // center of the slot, cell units, relative to cell bottom-left
            public Vector2 size;     // footprint, cell units
            public BlockKind kind;
            public int yaw;          // 0..3 quarter-turns around Y (for half cubes)

            public Slot(float cx, float cy, float sx, float sy, BlockKind kind, int yaw = 0)
            {
                center = new Vector2(cx, cy);
                size = new Vector2(sx, sy);
                this.kind = kind;
                this.yaw = yaw;
            }
        }

        /// <summary>
        /// Slot definitions for each shape, in cell units (bottom-left origin, cell spans 0..1).
        /// Half cubes are authored spanning X; use yaw = 1 to make them span Z.
        /// </summary>
        private static List<Slot> GetSlots(CubeShapeType type, int quarterTurns)
        {
            List<Slot> slots = type switch
            {
                CubeShapeType.Whole => new List<Slot>
                {
                    new Slot(0.5f, 0.5f, 1f, 1f, BlockKind.Whole),
                },

                CubeShapeType.FourSmall => new List<Slot>
                {
                    new Slot(0.25f, 0.25f, 0.5f, 0.5f, BlockKind.Small),
                    new Slot(0.75f, 0.25f, 0.5f, 0.5f, BlockKind.Small),
                    new Slot(0.25f, 0.75f, 0.5f, 0.5f, BlockKind.Small),
                    new Slot(0.75f, 0.75f, 0.5f, 0.5f, BlockKind.Small),
                },

                CubeShapeType.TwoHalf => new List<Slot>
                {
                    new Slot( 0.5f, 0.25f, 0.5f, 1f, BlockKind.Half),
                    new Slot( 0.5f, 0.75f, 0.5f, 1f, BlockKind.Half),
                },

                CubeShapeType.HalfAndTwoSmall => new List<Slot>
                {
                    // Bottom half (spans X) + two small cubes on top
                    new Slot(0.5f, 0.25f, 1f, 0.5f, BlockKind.Half),
                    new Slot(0.25f, 0.75f, 0.5f, 0.5f, BlockKind.Small),
                    new Slot(0.75f, 0.75f, 0.5f, 0.5f, BlockKind.Small),
                },

                _ => new List<Slot>()
            };

            // Optional global rotation of the whole layout
            if (quarterTurns != 0)
            {
                for (int i = 0; i < slots.Count; i++)
                    slots[i] = RotateSlot(slots[i], quarterTurns);
            }

            return slots;
        }

        /// <summary>Rotate a slot's position and yaw around the cell center (0.5, 0.5).</summary>
        private static Slot RotateSlot(Slot s, int quarterTurns)
        {
            Vector2 c = s.center - new Vector2(0.5f, 0.5f);
            Vector2 sz = s.size;
            int yaw = s.yaw;

            for (int i = 0; i < quarterTurns; i++)
            {
                c = new Vector2(c.y, -c.x);       // rotate 90° clockwise
                sz = new Vector2(sz.y, sz.x);     // swap axes
                yaw = (yaw + 1) % 4;
            }

            return new Slot(c.x + 0.5f, c.y + 0.5f, sz.x, sz.y, s.kind, yaw);
        }

        private GameObject PickPrefab(BlockKind kind) => kind switch
        {
            BlockKind.Whole => wholeCube,
            BlockKind.Half  => halfCube,
            BlockKind.Small => smallCube,
            _ => null
        };

        #endregion
    }
}
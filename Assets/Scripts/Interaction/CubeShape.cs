using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Interaction
{
    public enum CubeShapeType
    {
        Whole,
        FourSmall,
        TwoHalf,
        HalfAndTwoSmall,
    }

    /// <summary>
    /// Builds a 2×2 sub-cube layout inside one board cell.
    ///
    /// Slot footprints:
    ///   Whole → (1, 1)      → occupies all 4 sub-slots
    ///   Half  → (1, 0.5) or (0.5, 1) → occupies 2 sub-slots
    ///   Small → (0.5, 0.5)  → occupies 1 sub-slot
    ///
    /// Slot positions are in cell units with (0,0) at the cell's bottom-left and
    /// (1,1) at the top-right. A shape's pivot is at the bottom-center of its
    /// cell footprint (i.e. the cell's center in XZ, at ground level in Y).
    ///
    /// Build assigns a CubeColor to each sub-cube by dealing from the palette.
    /// This class knows nothing about the board, the tray, or match resolution.
    /// </summary>
    public class CubeShape : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Sub-Cube Prefabs")]
        [SerializeField] private GameObject wholeCube;
        [SerializeField] private GameObject halfCube;
        [SerializeField] private GameObject smallCube;

        [Header("Shape")]
        [SerializeField] private CubeShapeType shapeType = CubeShapeType.Whole;
        [Range(0, 3)]
        [SerializeField] private int rotationQuarterTurns = 0;
        [SerializeField] private float cellSize = 1f;

        [Header("Color")]
        [SerializeField] private CubePalette palette;
        [SerializeField] private CubeColor defaultColor = CubeColor.None;

        #endregion

        #region Runtime State

        private readonly List<SubCube> _spawned = new();
        private bool _built;
        private bool _isBuilding;
        private bool _isFiringShapeChanged;

        public CubeShapeType ShapeType => shapeType;
        public int RotationQuarterTurns => rotationQuarterTurns;
        public float CellSize => cellSize;
        public Vector2 ShapeSize => new Vector2(1f, 1f);
        public Vector3 WorldSize => new Vector3(cellSize, cellSize, cellSize);
        public Vector3 LocalCenter => new Vector3(0f, cellSize * 0.5f, 0f);
        public IReadOnlyList<SubCube> Blocks => _spawned;
        public CubePalette Palette => palette;

        public event System.Action<CubeShape> OnShapeChanged;

        #endregion

        #region Public API

        public void SetCellSize(float size) => cellSize = size;

        public void SetPalette(CubePalette p) => palette = p;

        public void SetShape(CubeShapeType type, int quarterTurns = 0)
        {
            shapeType = type;
            rotationQuarterTurns = Mathf.Clamp(quarterTurns, 0, 3);
            Build();
        }

        public void RemoveBlock(SubCube block)
        {
            if (block == null) return;
            _spawned.Remove(block);
            FireShapeChanged();
        }

        #endregion

        #region Build / Clear

        public void Build()
        {
            if (_isBuilding) return;
            _isBuilding = true;

            try
            {
                Clear();

                if (wholeCube == null || halfCube == null || smallCube == null)
                {
                    Debug.LogError("CubeShape: missing prefab references.", this);
                    return;
                }

                var slots = GetSlots(shapeType, rotationQuarterTurns);
                Vector2 shapeCenter = ShapeSize * 0.5f;

                var dealt = DealUniqueColors(slots.Count);
                int dealtIndex = 0;

                foreach (var slot in slots)
                {
                    GameObject prefab = PickPrefab(slot.kind);
                    if (prefab == null) continue;

                    GameObject go = Instantiate(prefab, transform);

                    // Slot centers are in cell units, with (0.5, 0.5) being the cell's
                    // geometric center. Pivot is at the cell's horizontal center, so
                    // subtract shapeCenter to convert to pivot-relative offsets.
                    go.transform.localPosition = new Vector3(
                        (slot.center.x - shapeCenter.x) * cellSize,
                        0f,
                        (slot.center.y - shapeCenter.y) * cellSize
                    );
                    go.transform.localRotation = Quaternion.Euler(0f, slot.yaw * 90f, 0f);

                    if (go.TryGetComponent(out SubCube sub))
                    {
                        sub.Initialize(this, slot.center, slot.size, palette);
                        sub.SetColor(dealt[dealtIndex++]);
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
            }
            finally
            {
                _isBuilding = false;
            }

            // Fire after the guard releases so a subscriber can rebuild safely.
            FireShapeChanged();
        }

        public void Clear()
        {
            // Detach all children first so a subsequent Build() sees an empty
            // transform even though Destroy() defers to the end of the frame.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (child == null) continue;
                child.transform.SetParent(null, false);
                Destroy(child);
            }

            _spawned.Clear();
            _built = false;
        }

        #endregion

        #region Events

        private void FireShapeChanged()
        {
            if (_isFiringShapeChanged) return;
            _isFiringShapeChanged = true;
            try { OnShapeChanged?.Invoke(this); }
            finally { _isFiringShapeChanged = false; }
        }

        #endregion

        #region Color Dealing

        /// <summary>
        /// Shuffle-and-deal unique colors from the palette's entries.
        /// Refills on exhaustion so a shape with more slots than palette entries
        /// still completes (with repeats).
        /// </summary>
        private CubeColor[] DealUniqueColors(int count)
        {
            var result = new CubeColor[count];
            if (count == 0) return result;

            List<CubeColor> pool = new();
            if (palette != null)
            {
                foreach (var e in palette.Entries)
                    if (e.color != CubeColor.None) pool.Add(e.color);
            }

            if (pool.Count == 0) pool.Add(defaultColor);

            var bag = new List<CubeColor>(pool);
            for (int i = 0; i < count; i++)
            {
                if (bag.Count == 0) bag.AddRange(pool);

                int pick = Random.Range(0, bag.Count);
                result[i] = bag[pick];
                bag.RemoveAt(pick);
            }

            return result;
        }

        #endregion

        #region Lifecycle

        private void Start()
        {
            if (!_built) Build();
        }

        #endregion

        #region Slot Layout

        private enum BlockKind { Whole, Half, Small }

        private struct Slot
        {
            public Vector2 center;   // cell units, relative to cell bottom-left
            public Vector2 size;     // footprint, cell units
            public BlockKind kind;
            public int yaw;          // 0..3 quarter-turns around Y

            public Slot(float cx, float cy, float sx, float sy, BlockKind kind, int yaw = 0)
            {
                center = new Vector2(cx, cy);
                size = new Vector2(sx, sy);
                this.kind = kind;
                this.yaw = yaw;
            }
        }

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
                    new Slot(0.5f, 0.25f, 1f, 0.5f, BlockKind.Half, yaw: 1),
                    new Slot(0.5f, 0.75f, 1f, 0.5f, BlockKind.Half, yaw: 1),
                },

                CubeShapeType.HalfAndTwoSmall => new List<Slot>
                {
                    new Slot(0.5f, 0.25f, 1f, 0.5f, BlockKind.Half),
                    new Slot(0.25f, 0.75f, 0.5f, 0.5f, BlockKind.Small),
                    new Slot(0.75f, 0.75f, 0.5f, 0.5f, BlockKind.Small),
                },

                _ => new List<Slot>()
            };

            if (quarterTurns != 0)
            {
                for (int i = 0; i < slots.Count; i++)
                    slots[i] = RotateSlot(slots[i], quarterTurns);
            }

            return slots;
        }

        private static Slot RotateSlot(Slot s, int q)
        {
            Vector2 c = s.center - new Vector2(0.5f, 0.5f);
            Vector2 sz = s.size;
            int yaw = s.yaw;

            for (int i = 0; i < q; i++)
            {
                c = new Vector2(c.y, -c.x);       // 90° CW
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
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
    /// Builds a single-cell cube layout from pre-sized sub-cube prefabs.
    /// Pivot is at the BOTTOM-CENTER of the cell footprint.
    /// Slot layouts are in cell units (0,0 = cell bottom-left, 1,1 = top-right).
    /// </summary>
    public class CubeShape : MonoBehaviour
    {
        public enum BlockKind { Whole, Half, Small }

        #region Inspector Fields

        [Header("Sub-Cube Prefabs (authored at final size)")]
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
        [SerializeField] private CubeColor shapeColor = CubeColor.None;
        [SerializeField] private bool randomizePerSlotColor = false;

        [SerializeField]
        private CubeColor[] colorPool =
        {
            CubeColor.Red,
            CubeColor.Blue,
            CubeColor.Green,
            CubeColor.Yellow,
            CubeColor.Purple,
        };

        #endregion

        #region Runtime State

        private readonly List<SubCube> _spawned = new();
        private bool _built;

        public bool IsBuilding { get; private set; }

        public CubeShapeType ShapeType => shapeType;
        public int RotationQuarterTurns => rotationQuarterTurns;
        public float CellSize => cellSize;
        public Vector2 ShapeSize => new Vector2(1f, 1f);
        public Vector3 WorldSize => new Vector3(cellSize, cellSize, cellSize);
        public Vector3 LocalCenter => new Vector3(0f, cellSize * 0.5f, 0f);
        public IReadOnlyList<SubCube> Blocks => _spawned;
        public CubeColor ShapeColor => shapeColor;
        public CubePalette Palette => palette;

        public event System.Action<CubeShape> OnShapeChanged;

        #endregion

        #region Public API

        public void SetCellSize(float size) => cellSize = size;

        public void SetPalette(CubePalette p)
        {
            palette = p;
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) _spawned[i].SetPalette(p);
        }

        public void SetColor(CubeColor color)
        {
            shapeColor = color;
            randomizePerSlotColor = false;
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) _spawned[i].SetColor(color);
        }

        public void SetRandomPerSlotColors(CubeColor[] pool)
        {
            if (pool != null && pool.Length > 0) colorPool = pool;

            // Strip None from the pool as a safety net
            if (colorPool != null)
            {
                var clean = new List<CubeColor>();
                foreach (var c in colorPool) if (c != CubeColor.None) clean.Add(c);
                if (clean.Count > 0) colorPool = clean.ToArray();
            }

            randomizePerSlotColor = true;
        }

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
            OnShapeChanged?.Invoke(this);
        }

        /// <summary>
        /// Replaces a sub-cube with a fresh instance of the correct prefab for the given kind.
        /// yaw = 0 → half spans X (wide, short). yaw = 1 → half spans Z (narrow, tall).
        /// </summary>
        public SubCube ReplaceSubCube(SubCube oldSub, BlockKind newKind, Vector2 center, Vector2 size, int yaw = 0)
        {
            if (oldSub == null) return null;

            CubeColor preserveColor = oldSub.CurrentColor;

            if (_spawned.Contains(oldSub))
                _spawned.Remove(oldSub);

            // Prevent the deferred-destroy object from running any logic this frame
            oldSub.enabled = false;
            Destroy(oldSub.gameObject);

            GameObject prefab = PickPrefab(newKind);
            if (prefab == null) return null;

            GameObject go = Instantiate(prefab, transform);

            go.transform.localPosition = new Vector3(
                (center.x - 0.5f) * cellSize,
                0f,
                (center.y - 0.5f) * cellSize
            );
            go.transform.localRotation = Quaternion.Euler(0f, yaw * 90f, 0f);

            if (!Mathf.Approximately(cellSize, 1f))
                go.transform.localScale = Vector3.one * cellSize;

            if (go.TryGetComponent(out SubCube newSub))
            {
                newSub.Initialize(this, center, size, palette);
                newSub.SetColor(preserveColor);
                _spawned.Add(newSub);
                return newSub;
            }

            return null;
        }

        public static BlockKind KindFromSlotSize(Vector2 size)
        {
            bool oneX = Mathf.Approximately(size.x, 1f);
            bool oneY = Mathf.Approximately(size.y, 1f);
            bool halfX = Mathf.Approximately(size.x, 0.5f);
            bool halfY = Mathf.Approximately(size.y, 0.5f);

            if (oneX && oneY) return BlockKind.Whole;
            if (halfX && halfY) return BlockKind.Small;
            return BlockKind.Half;
        }

        #endregion

        #region Build / Clear

        public void Build()
        {
            if (IsBuilding) return;
            IsBuilding = true;
            try
            {
                Clear();

                if (wholeCube == null || halfCube == null || smallCube == null)
                {
                    Debug.LogError("CubeShape: one or more sub-cube prefabs are missing.", this);
                    return;
                }

                var slots = GetSlots(shapeType, rotationQuarterTurns);
                Vector2 shapeCenter = ShapeSize * 0.5f;

                CubeColor[] dealt = null;
                if (randomizePerSlotColor) dealt = DealUniqueColors(slots.Count);
                int dealtIndex = 0;

                foreach (var slot in slots)
                {
                    GameObject prefab = PickPrefab(slot.kind);
                    if (prefab == null) continue;

                    GameObject go = Instantiate(prefab, transform);
                    go.transform.localPosition = new Vector3(
                        (slot.center.x - shapeCenter.x) * cellSize,
                        0f,
                        (slot.center.y - shapeCenter.y) * cellSize
                    );
                    go.transform.localRotation = Quaternion.Euler(0f, slot.yaw * 90f, 0f);

                    if (!Mathf.Approximately(cellSize, 1f))
                        go.transform.localScale = Vector3.one * cellSize;

                    if (go.TryGetComponent(out SubCube sub))
                    {
                        sub.Initialize(this, slot.center, slot.size, palette);

                        CubeColor assigned = randomizePerSlotColor && dealt != null
                            ? dealt[dealtIndex++]
                            : shapeColor;

                        if (assigned == CubeColor.None) assigned = CubeColor.None;

                        sub.SetColor(assigned);
                        _spawned.Add(sub);
                    }
                }

                _built = true;
                OnShapeChanged?.Invoke(this);
            }
            finally
            {
                IsBuilding = false;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                var s = _spawned[i];
                if (s == null) continue;
                s.enabled = false;
                Destroy(s.gameObject);
            }
            _spawned.Clear();

            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            _built = false;
        }

        #endregion

        #region Color Dealing

        private CubeColor[] DealUniqueColors(int count)
        {
            var result = new CubeColor[count];
            if (count == 0) return result;

            var pool = (colorPool != null && colorPool.Length > 0)
                ? colorPool
                : new[] { shapeColor };

            var clean = new List<CubeColor>();
            foreach (var c in pool) if (c != CubeColor.None) clean.Add(c);
            if (clean.Count == 0) clean.Add(CubeColor.None);

            var bag = new List<CubeColor>(clean);

            for (int i = 0; i < count; i++)
            {
                if (bag.Count == 0) bag.AddRange(clean);
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying && isActiveAndEnabled && _built) Build();
        }
#endif

        #endregion

        #region Slot Layout

        private struct Slot
        {
            public Vector2 center;
            public Vector2 size;
            public BlockKind kind;
            public int yaw;

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

        private static Slot RotateSlot(Slot s, int quarterTurns)
        {
            Vector2 c = s.center - new Vector2(0.5f, 0.5f);
            Vector2 sz = s.size;
            int yaw = s.yaw;

            for (int i = 0; i < quarterTurns; i++)
            {
                c = new Vector2(c.y, -c.x);
                sz = new Vector2(sz.y, sz.x);
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
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

    public class CubeShape : MonoBehaviour
    {
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
        [SerializeField] private CubeColor shapeColor = CubeColor.White;

        [Tooltip("If true, every sub-cube gets a UNIQUE color from colorPool on each build.")]
        [SerializeField] private bool randomizePerSlotColor = false;

        [Tooltip("Colors that can be dealt to sub-cubes. Needs at least as many entries as the shape has sub-cubes for strict uniqueness.")]
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
        private readonly Dictionary<SubCube, CubeColor> _blockColors = new();
        private bool _built;

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
            ApplyColorsToBlocks();
        }

        public void SetColor(CubeColor color)
        {
            shapeColor = color;
            randomizePerSlotColor = false;
            ApplyColorsToBlocks();
        }

        public void SetRandomPerSlotColors(CubeColor[] pool)
        {
            if (pool != null && pool.Length > 0) colorPool = pool;
            randomizePerSlotColor = true;
            Build();   // re-deal unique colors
        }

        public void SetBlockColor(SubCube block, CubeColor color)
        {
            if (block == null) return;
            _blockColors[block] = color;
            block.SetColor(color);
        }

        public CubeColor GetBlockColor(SubCube block)
        {
            if (block == null) return shapeColor;
            return _blockColors.TryGetValue(block, out var c) ? c : shapeColor;
        }

        public void SetShape(CubeShapeType type, int quarterTurns = 0)
        {
            shapeType = type;
            rotationQuarterTurns = Mathf.Clamp(quarterTurns, 0, 3);
            Build();
        }

        #endregion

        #region Build / Clear

        public void Build()
        {
            Clear();

            if (wholeCube == null || halfCube == null || smallCube == null)
            {
                Debug.LogError("CubeShape: one or more sub-cube prefabs are missing.", this);
                return;
            }

            var slots = GetSlots(shapeType, rotationQuarterTurns);
            Vector2 shapeCenter = ShapeSize * 0.5f;

            // Deal unique colors for the slots that don't have explicit overrides.
            CubeColor[] dealt = null;
            if (randomizePerSlotColor)
            {
                int freeSlots = 0;
                foreach (var s in slots) if (!s.overrideColor.HasValue) freeSlots++;

                dealt = DealUniqueColors(freeSlots);

                int distinct = DistinctColorCount();
                if (freeSlots > distinct)
                {
                    Debug.LogWarning(
                        $"[CubeShape] '{shapeType}' has {freeSlots} sub-cubes but pool has " +
                        $"{distinct} distinct colors. Repeats will appear.",
                        this
                    );
                }
            }

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

                    CubeColor finalColor;
                    if (slot.overrideColor.HasValue)
                        finalColor = slot.overrideColor.Value;
                    else if (randomizePerSlotColor && dealt != null)
                        finalColor = dealt[dealtIndex++];
                    else
                        finalColor = shapeColor;

                    sub.SetColor(finalColor);
                    _blockColors[sub] = finalColor;
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

        public void Clear()
        {
            foreach (var s in _spawned) if (s != null) Destroy(s.gameObject);
            _spawned.Clear();
            _blockColors.Clear();

            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            _built = false;
        }

        public void RemoveBlock(SubCube block)
        {
            if (block == null) return;
            _spawned.Remove(block);
            _blockColors.Remove(block);
            OnShapeChanged?.Invoke(this);
        }

        #endregion

        #region Color Handling

        private void ApplyColorsToBlocks()
        {
            if (randomizePerSlotColor)
            {
                // Re-deal unique colors to existing sub-cubes
                var dealt = DealUniqueColors(_spawned.Count);
                for (int i = 0; i < _spawned.Count; i++)
                {
                    var sub = _spawned[i];
                    if (sub == null) continue;

                    CubeColor c = _blockColors.TryGetValue(sub, out var existing)
                        ? existing
                        : dealt[i];

                    sub.SetColor(c);
                }
            }
            else
            {
                foreach (var sub in _spawned)
                    if (sub != null) sub.SetColor(shapeColor);
            }
        }

        /// <summary>
        /// Deals unique colors from the pool to the given number of slots.
        /// Shuffles with a Fisher–Yates-style bag; refills when the bag empties.
        /// </summary>
        private CubeColor[] DealUniqueColors(int count)
        {
            var result = new CubeColor[count];
            if (count == 0) return result;

            var pool = (colorPool != null && colorPool.Length > 0)
                ? colorPool
                : new[] { shapeColor };

            var bag = new List<CubeColor>(pool);

            for (int i = 0; i < count; i++)
            {
                if (bag.Count == 0)
                    bag.AddRange(pool);    // refill on exhaustion

                int pick = Random.Range(0, bag.Count);
                result[i] = bag[pick];
                bag.RemoveAt(pick);
            }

            return result;
        }

        public int DistinctColorCount()
        {
            if (colorPool == null) return 0;
            var set = new HashSet<CubeColor>(colorPool);
            return set.Count;
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
            if (Application.isPlaying && isActiveAndEnabled && _built)
                Build();
        }
#endif

        #endregion

        #region Slot Layout

        private enum BlockKind { Whole, Half, Small }

        private struct Slot
        {
            public Vector2 center;
            public Vector2 size;
            public BlockKind kind;
            public int yaw;
            public CubeColor? overrideColor;

            public Slot(float cx, float cy, float sx, float sy, BlockKind kind,
                        int yaw = 0, CubeColor? overrideColor = null)
            {
                center = new Vector2(cx, cy);
                size = new Vector2(sx, sy);
                this.kind = kind;
                this.yaw = yaw;
                this.overrideColor = overrideColor;
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
                    new Slot(0.5f, 0.25f, 0.5f, 1f, BlockKind.Half),
                    new Slot(0.5f, 0.75f, 0.5f, 1f, BlockKind.Half),
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

            return new Slot(c.x + 0.5f, c.y + 0.5f, sz.x, sz.y, s.kind, yaw, s.overrideColor);
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
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
        #region Nested Types

        public enum BlockKind { Whole, Half, Small }

        #endregion

        #region Inspector Fields

        [Header("Sub-Cube Prefabs (authored at final size)")]
        [SerializeField] private GameObject wholeCube;
        [SerializeField] private GameObject halfCube;
        [SerializeField] private GameObject smallCube;

        [Header("Shape")]
        [SerializeField] private CubeShapeType shapeType = CubeShapeType.Whole;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private bool randomizeHalfOrientation = true;
        [SerializeField] private bool randomizePlacement = true;

        [Header("Color")]
        [SerializeField] private CubePalette palette;

        #endregion

        #region Runtime State

        private readonly List<SubCube> _spawned = new();
        private bool _built;
        private bool _isBuilding;
        private bool _initialized;

        public CubeShapeType ShapeType => shapeType;
        public float CellSize => cellSize;
        public Vector2 ShapeSize => new Vector2(1f, 1f);
        public Vector3 WorldSize => new Vector3(cellSize, cellSize, cellSize);
        public Vector3 LocalCenter => new Vector3(0f, cellSize * 0.5f, 0f);
        public IReadOnlyList<SubCube> Blocks => _spawned;
        public CubePalette Palette => palette;
        public bool IsBuilding => _isBuilding;

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

        public void SetRandomizePlacement(bool value) => randomizePlacement = value;

        public void SetColor(CubeColor color)
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) _spawned[i].SetColor(color);
        }

        public void SetShape(CubeShapeType type)
        {
            _initialized = true;
            shapeType = type;
            Build();
        }

        public void RemoveBlock(SubCube block)
        {
            if (block == null) return;
            _spawned.Remove(block);
            OnShapeChanged?.Invoke(this);
        }

        public static BlockKind KindFromSlotSize(Vector2 size)
        {
            bool fullX = Mathf.Approximately(size.x, 1f);
            bool fullY = Mathf.Approximately(size.y, 1f);
            if (fullX && fullY) return BlockKind.Whole;
            if (!fullX && !fullY) return BlockKind.Small;
            return BlockKind.Half;
        }

        public SubCube ReplaceSubCube(SubCube oldSub, BlockKind newKind, Vector2 newCenter, Vector2 newSize, int yaw)
        {
            if (oldSub == null) return null;

            int index = _spawned.IndexOf(oldSub);
            if (index < 0) return null;

            Transform oldPivot = oldSub.SlotPivot != null ? oldSub.SlotPivot : oldSub.transform.parent;
            CubeColor oldColor = oldSub.CurrentColor;

            Object.Destroy(oldSub.gameObject);
            _spawned.RemoveAt(index);

            GameObject prefab = PickPrefab(newKind);
            if (prefab == null)
            {
                Debug.LogWarning($"[CubeShape] ReplaceSubCube: no prefab for {newKind}");
                return null;
            }

            Transform pivot = oldPivot;
            if (pivot == null)
            {
                var pivotGO = new GameObject($"Slot_{newKind}_{newCenter.x}_{newCenter.y}");
                pivotGO.transform.SetParent(transform, worldPositionStays: false);
                pivot = pivotGO.transform;
            }

            float cs = cellSize;
            pivot.localPosition = new Vector3((newCenter.x - 0.5f) * cs, 0f, (newCenter.y - 0.5f) * cs);
            pivot.localRotation = Quaternion.identity;
            pivot.localScale = Vector3.one;

            GameObject go = Instantiate(prefab, pivot);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0f, yaw * 90f, 0f);
            go.transform.localScale = Vector3.one;

            if (!go.TryGetComponent(out SubCube newSub))
            {
                Debug.LogWarning($"[CubeShape] ReplaceSubCube: prefab '{prefab.name}' has no SubCube.");
                Object.Destroy(go);
                return null;
            }

            newSub.Initialize(this, newCenter, newSize, palette);
            newSub.SetColor(oldColor);
            _spawned.Add(newSub);

            OnShapeChanged?.Invoke(this);
            return newSub;
        }

        #endregion

        #region Build / Clear

        private void Awake()
        {
            transform.localRotation = Quaternion.identity;
        }

        public void Build()
        {
            if (_isBuilding) return;
            _isBuilding = true;
            _initialized = true;
            try
            {
                transform.localRotation = Quaternion.identity;
                Clear();

                if (wholeCube == null || halfCube == null || smallCube == null)
                {
                    Debug.LogError("CubeShape: one or more sub-cube prefabs are missing.", this);
                    return;
                }

                var slots = BuildSlotLayout(shapeType);
                CubeColor[] dealt = DealColorsFromPalette(slots.Count);

                int dealtIndex = 0;
                Vector2 shapeCenter = ShapeSize * 0.5f;

                foreach (var slot in slots)
                {
                    GameObject prefab = PickPrefab(slot.kind);
                    if (prefab == null) continue;

                    var pivot = new GameObject($"Slot_{slot.kind}_{slot.center.x}_{slot.center.y}");
                    pivot.transform.SetParent(transform, worldPositionStays: false);
                    pivot.transform.localPosition = new Vector3(
                        (slot.center.x - shapeCenter.x) * cellSize,
                        0f,
                        (slot.center.y - shapeCenter.y) * cellSize
                    );
                    pivot.transform.localRotation = Quaternion.identity;
                    pivot.transform.localScale = Vector3.one;

                    GameObject go = Instantiate(prefab, pivot.transform);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localRotation = Quaternion.Euler(0f, slot.yaw * 90f, 0f);
                    go.transform.localScale = Vector3.one;

                    if (go.TryGetComponent(out SubCube sub))
                    {
                        sub.Initialize(this, slot.center, slot.size, palette);
                        sub.SetColor(dealt[dealtIndex++]);
                        _spawned.Add(sub);
                    }
                    else
                    {
                        Debug.LogWarning($"CubeShape: prefab '{prefab.name}' has no SubCube component.", prefab);
                    }
                }

                _built = true;
                OnShapeChanged?.Invoke(this);
            }
            finally
            {
                _isBuilding = false;
            }
        }

        public void Clear()
        {
            foreach (var s in _spawned) if (s != null) Destroy(s.gameObject);
            _spawned.Clear();

            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            _built = false;
        }

        #endregion

        #region Slot Layout

        private struct Slot
        {
            public Vector2 center;
            public Vector2 size;
            public BlockKind kind;
            public int yaw;

            public Slot(Vector2 center, Vector2 size, BlockKind kind, int yaw = 0)
            {
                this.center = center;
                this.size = size;
                this.kind = kind;
                this.yaw = yaw;
            }
        }

        private List<Slot> BuildSlotLayout(CubeShapeType type)
        {
            return randomizePlacement ? BuildRandomLayout(type) : BuildDefaultLayout(type);
        }

        private static List<Slot> BuildDefaultLayout(CubeShapeType type)
        {
            switch (type)
            {
                case CubeShapeType.Whole:
                    return new List<Slot>
                    {
                        new Slot(new Vector2(0.5f, 0.5f), new Vector2(1f, 1f), BlockKind.Whole),
                    };

                case CubeShapeType.FourSmall:
                    return new List<Slot>
                    {
                        new Slot(new Vector2(0.25f, 0.25f), new Vector2(0.5f, 0.5f), BlockKind.Small),
                        new Slot(new Vector2(0.75f, 0.25f), new Vector2(0.5f, 0.5f), BlockKind.Small),
                        new Slot(new Vector2(0.25f, 0.75f), new Vector2(0.5f, 0.5f), BlockKind.Small),
                        new Slot(new Vector2(0.75f, 0.75f), new Vector2(0.5f, 0.5f), BlockKind.Small),
                    };

                case CubeShapeType.TwoHalf:
                    return new List<Slot>
                    {
                        new Slot(new Vector2(0.5f, 0.25f), new Vector2(1f, 0.5f), BlockKind.Half, yaw: 1),
                        new Slot(new Vector2(0.5f, 0.75f), new Vector2(1f, 0.5f), BlockKind.Half, yaw: 1),
                    };

                case CubeShapeType.HalfAndTwoSmall:
                    return new List<Slot>
                    {
                        new Slot(new Vector2(0.5f, 0.25f), new Vector2(1f, 0.5f), BlockKind.Half, yaw: 1),
                        new Slot(new Vector2(0.25f, 0.75f), new Vector2(0.5f, 0.5f), BlockKind.Small),
                        new Slot(new Vector2(0.75f, 0.75f), new Vector2(0.5f, 0.5f), BlockKind.Small),
                    };

                default:
                    return new List<Slot>();
            }
        }

        private List<Slot> BuildRandomLayout(CubeShapeType type)
        {
            var result = new List<Slot>();
            var taken = new bool[2, 2];

            switch (type)
            {
                case CubeShapeType.Whole:
                    result.Add(new Slot(new Vector2(0.5f, 0.5f), new Vector2(1f, 1f), BlockKind.Whole));
                    return result;

                case CubeShapeType.FourSmall:
                    for (int i = 0; i < 4; i++)
                        if (TryPlaceSmall(taken, out var s)) result.Add(s);
                    return result;

                case CubeShapeType.TwoHalf:
                    for (int i = 0; i < 2; i++)
                        if (TryPlaceHalf(taken, out var s)) result.Add(s);
                    return result;

                case CubeShapeType.HalfAndTwoSmall:
                    if (TryPlaceHalf(taken, out var h)) result.Add(h);
                    for (int i = 0; i < 2; i++)
                        if (TryPlaceSmall(taken, out var s)) result.Add(s);
                    return result;

                default:
                    return result;
            }
        }

        private static bool TryPlaceSmall(bool[,] taken, out Slot slot)
        {
            slot = default;

            var free = new List<Vector2Int>(4);
            for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                    if (!taken[x, y]) free.Add(new Vector2Int(x, y));

            if (free.Count == 0) return false;

            var pick = free[Random.Range(0, free.Count)];
            taken[pick.x, pick.y] = true;

            slot = new Slot(
                center: new Vector2(0.25f + pick.x * 0.5f, 0.25f + pick.y * 0.5f),
                size: new Vector2(0.5f, 0.5f),
                kind: BlockKind.Small
            );
            return true;
        }

        private bool TryPlaceHalf(bool[,] taken, out Slot slot)
        {
            slot = default;

            var options = new List<Slot>(4);

            for (int y = 0; y < 2; y++)
            {
                if (!taken[0, y] && !taken[1, y])
                {
                    options.Add(new Slot(
                        center: new Vector2(0.5f, 0.25f + y * 0.5f),
                        size: new Vector2(1f, 0.5f),
                        kind: BlockKind.Half,
                        yaw: 1
                    ));
                }
            }

            for (int x = 0; x < 2; x++)
            {
                if (!taken[x, 0] && !taken[x, 1])
                {
                    options.Add(new Slot(
                        center: new Vector2(0.25f + x * 0.5f, 0.5f),
                        size: new Vector2(0.5f, 1f),
                        kind: BlockKind.Half,
                        yaw: 0
                    ));
                }
            }

            if (options.Count == 0) return false;

            if (!randomizeHalfOrientation)
            {
                var preferred = options.FindAll(o => Mathf.Approximately(o.size.x, 1f));
                if (preferred.Count > 0) options = preferred;
            }

            slot = options[Random.Range(0, options.Count)];

            if (Mathf.Approximately(slot.size.x, 1f))
            {
                int y = Mathf.RoundToInt((slot.center.y - 0.25f) / 0.5f);
                taken[0, y] = true;
                taken[1, y] = true;
            }
            else
            {
                int x = Mathf.RoundToInt((slot.center.x - 0.25f) / 0.5f);
                taken[x, 0] = true;
                taken[x, 1] = true;
            }

            return true;
        }

        #endregion

        #region Color Dealing

        private CubeColor[] DealColorsFromPalette(int count)
        {
            var result = new CubeColor[count];
            if (count == 0) return result;

            var source = new List<CubeColor>(8);
            if (palette != null && palette.Entries != null)
            {
                for (int i = 0; i < palette.Entries.Count; i++)
                {
                    var c = palette.Entries[i].color;
                    if (c != CubeColor.None) source.Add(c);
                }
            }

            if (source.Count == 0)
            {
                for (int i = 0; i < count; i++) result[i] = CubeColor.None;
                return result;
            }

            var bag = new List<CubeColor>(source);
            for (int i = 0; i < count; i++)
            {
                if (bag.Count == 0) bag.AddRange(source);
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
            if (_initialized) return;
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

        #region Prefab Picking

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
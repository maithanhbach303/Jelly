using UnityEngine;

namespace MyGame.Interaction
{
    /// <summary>
    /// Base behavior for a sub-cube inside a CubeShape.
    /// Stores a CubeColor (identity used by match detection) and applies RGB via a palette.
    /// Grows only along world axes because the slot pivot is always rotation-free.
    /// </summary>
    public class SubCube : MonoBehaviour
    {
        #region Inspector

        [Header("Color")]
        [Tooltip("Optional per-prefab palette. Overridden by the shape's palette at spawn.")]
        [SerializeField] private CubePalette palette;

        [Tooltip("Fallback color used if nothing else assigns one.")]
        [SerializeField] private CubeColor startColor = CubeColor.None;

        [Header("Debug")]
        [SerializeField] private bool logColorChanges = false;

        #endregion

        #region Runtime State

        [HideInInspector] public Vector2 SlotPosition;
        [HideInInspector] public Vector2 SlotSize;

        public CubeShape Owner { get; private set; }

        /// <summary>Current color identity — this is what match detection compares.</summary>
        public CubeColor CurrentColor { get; private set; } = CubeColor.None;

        /// <summary>Rotation-free wrapper that carries this sub-cube's position and world-aligned scale.</summary>
        public Transform SlotPivot { get; private set; }

        public CubePalette Palette => palette;
        public CubeColor StartColor => startColor;

        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId     = Shader.PropertyToID("_Color");

        #endregion

        #region Lifecycle

        protected virtual void Awake()
        {
            CacheRenderers();
            _mpb = new MaterialPropertyBlock();

            // Ensure the slot pivot is rotation-free
            if (transform.parent != null)
                transform.parent.localRotation = Quaternion.identity;
        }

        private void Start()
        {
            if (CurrentColor == CubeColor.None)
                SetColor(startColor);
        }

        private void CacheRenderers()
        {
            _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);

            if (logColorChanges && _renderers.Length == 0)
                Debug.LogWarning($"[SubCube {name}] No renderers found.", this);
        }

        #endregion

        #region Public API

        public void Initialize(CubeShape owner, Vector2 slotPosition, Vector2 slotSize, CubePalette shapePalette)
        {
            Owner = owner;
            SlotPosition = slotPosition;
            SlotSize = slotSize;
            SlotPivot = transform.parent;

            if (shapePalette != null)
                palette = shapePalette;

            OnInitialized();
        }

        public void SetColor(CubeColor color)
        {
            CurrentColor = color;
            ApplyColor(color);

            if (logColorChanges)
                Debug.Log($"[SubCube {name}] SetColor({color})", this);
        }

        public void SetPalette(CubePalette p)
        {
            palette = p;
            if (CurrentColor != CubeColor.None) ApplyColor(CurrentColor);
        }

        #endregion

        #region World-Aligned Growth

        /// <summary>
        /// Grow the sub-cube along a world-space direction by the given amount.
        /// Only XZ components are used; Y is ignored (sub-cubes are flat on the board).
        /// </summary>
        public void GrowAlongWorld(Vector3 worldDir, float delta)
        {
            if (SlotPivot == null) return;

            worldDir.y = 0f;
            if (worldDir.sqrMagnitude < 0.0001f) return;
            worldDir.Normalize();

            Vector3 localDir = SlotPivot.InverseTransformDirection(worldDir);

            var s = SlotPivot.localScale;
            s.x += localDir.x * delta;
            s.z += localDir.z * delta;
            s.y = 1f;
            SlotPivot.localScale = s;
        }

        public void GrowWorldRight(float delta)   => GrowAlongWorld(Vector3.right,   delta);
        public void GrowWorldLeft(float delta)    => GrowAlongWorld(Vector3.left,    delta);
        public void GrowWorldForward(float delta) => GrowAlongWorld(Vector3.forward, delta);
        public void GrowWorldBack(float delta)    => GrowAlongWorld(Vector3.back,    delta);

        public void ResetGrowth()
        {
            if (SlotPivot == null || Owner == null) return;

            SlotPivot.localScale = Vector3.one;
            float cs = Owner.CellSize;
            SlotPivot.localPosition = new Vector3(
                (SlotPosition.x - 0.5f) * cs,
                0f,
                (SlotPosition.y - 0.5f) * cs
            );
        }

        #endregion

        #region Rendering

        private void ApplyColor(CubeColor color)
        {
            if (_renderers == null || _renderers.Length == 0) CacheRenderers();
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            Color rgb = palette != null ? palette.GetRGB(color) : FallbackColorFor(color);
            rgb.a = 1f;

            foreach (var r in _renderers)
            {
                if (r == null) continue;

                if (palette != null)
                {
                    var mat = palette.GetMaterial(color);
                    if (mat != null && r.sharedMaterial != mat)
                        r.sharedMaterial = mat;
                }

                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, rgb);
                _mpb.SetColor(ColorId, rgb);
                r.SetPropertyBlock(_mpb);
            }
        }

        private static Color FallbackColorFor(CubeColor color) => color switch
        {
            CubeColor.Red    => new Color(0.95f, 0.30f, 0.30f),
            CubeColor.Blue   => new Color(0.30f, 0.55f, 0.95f),
            CubeColor.Green  => new Color(0.40f, 0.85f, 0.45f),
            CubeColor.Yellow => new Color(0.98f, 0.80f, 0.25f),
            CubeColor.Purple => new Color(0.80f, 0.50f, 0.95f),
            CubeColor.Orange => new Color(0.98f, 0.60f, 0.20f),
            CubeColor.Cyan   => new Color(0.35f, 0.90f, 0.95f),
            CubeColor.Pink   => new Color(0.98f, 0.55f, 0.75f),
        };

        #endregion

        #region Overridables

        protected virtual void OnInitialized() { }

        #endregion
    }
}
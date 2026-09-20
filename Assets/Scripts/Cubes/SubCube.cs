using UnityEngine;

namespace MyGame.Interaction
{
    /// <summary>
    /// Base behavior for a sub-cube inside a CubeShape.
    /// Carries a CubeColor and applies it to all child renderers.
    /// Subclass to add logic (bombs, animations, etc.).
    /// </summary>
    public class SubCube : MonoBehaviour
    {
        #region Inspector

        [Header("Color")]
        [Tooltip("If true, this sub-cube's color is driven by the parent CubeShape.")]
        [SerializeField] private bool colorDrivenByShape = true;

        [Tooltip("Local color override when NOT driven by the shape.")]
        [SerializeField] private CubeColor localColor = CubeColor.White;

        [Tooltip("Optional per-prefab palette. Falls back to the shape's palette.")]
        [SerializeField] private CubePalette palette;

        #endregion

        #region Runtime State

        [HideInInspector] public Vector2 SlotPosition;
        [HideInInspector] public Vector2 SlotSize;

        public CubeShape Owner { get; private set; }
        public CubeColor CurrentColor { get; private set; } = CubeColor.White;

        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP
        private static readonly int ColorId     = Shader.PropertyToID("_Color");     // Built-in

        #endregion

        #region Lifecycle

        protected virtual void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            _mpb = new MaterialPropertyBlock();
        }

        /// <summary>Called by CubeShape right after instantiation.</summary>
        public void Initialize(CubeShape owner, Vector2 slotPosition, Vector2 slotSize, CubePalette shapePalette)
        {
            Owner = owner;
            SlotPosition = slotPosition;
            SlotSize = slotSize;

            if (palette == null) palette = shapePalette;

            OnInitialized();
        }

        #endregion

        #region Public API

        public void SetColor(CubeColor color)
        {
            CurrentColor = colorDrivenByShape ? color : localColor;
            ApplyColor(CurrentColor);
        }

        public CubePalette Palette => palette;

        #endregion

        #region Rendering

        private void ApplyColor(CubeColor color)
        {
            if (_renderers == null || _renderers.Length == 0)
                _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);

            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            Color rgb = palette != null ? palette.GetRGB(color) : Color.white;

            foreach (var r in _renderers)
            {
                if (r == null) continue;

                // Optional shared material swap if the palette provides one
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

        #endregion

        #region Overridables

        /// <summary>Override in subclasses for custom setup.</summary>
        protected virtual void OnInitialized() { }

        #endregion
    }
}
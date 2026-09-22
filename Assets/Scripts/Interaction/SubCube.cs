using UnityEngine;

namespace MyGame.Interaction
{
    /// <summary>
    /// Base behavior for a sub-cube. Stores a CubeColor and renders via a palette.
    /// Slot metadata (SlotPosition, SlotSize) is written by the owning CubeShape.
    /// </summary>
    public class SubCube : MonoBehaviour
    {
        #region Inspector

        [Header("Color")]
        [SerializeField] private CubePalette palette;
        [SerializeField] private CubeColor startColor = CubeColor.None;

        [Header("Debug")]
        [SerializeField] private bool logColorChanges = false;

        #endregion

        #region Runtime State

        [HideInInspector] public Vector2 SlotPosition;
        [HideInInspector] public Vector2 SlotSize;

        public CubeShape Owner { get; private set; }
        public CubeColor CurrentColor { get; private set; } = CubeColor.None;
        public CubeColor StartColor => startColor;
        public CubePalette Palette => palette;

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

            if (shapePalette != null) palette = shapePalette;

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
            CubeColor.None   => Color.clear,
        };

        #endregion

        #region Overridables

        protected virtual void OnInitialized() { }

        #endregion
    }
}
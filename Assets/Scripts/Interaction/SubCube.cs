using UnityEngine;

namespace MyGame.Interaction
{
    /// <summary>
    /// Base behavior for a sub-cube. Carries a CubeColor (identity + render tint).
    /// </summary>
    public class SubCube : MonoBehaviour
    {
        [Header("Color")]
        [SerializeField] private CubePalette palette;

        public CubeShape Owner { get; private set; }
        public Vector2 SlotPosition { get; private set; }
        public Vector2 SlotSize { get; private set; }

        /// <summary>Color identity — read by match detection.</summary>
        public CubeColor CurrentColor { get; private set; } = CubeColor.None;

        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId     = Shader.PropertyToID("_Color");

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            _mpb = new MaterialPropertyBlock();
        }

        public void Initialize(CubeShape owner, Vector2 slotPos, Vector2 slotSize, CubePalette shapePalette)
        {
            Owner = owner;
            SlotPosition = slotPos;
            SlotSize = slotSize;
            if (shapePalette != null) palette = shapePalette;
        }

        public void SetColor(CubeColor color)
        {
            CurrentColor = color;
            ApplyColor(color);
        }

        private void ApplyColor(CubeColor color)
        {
            if (_renderers == null || _renderers.Length == 0)
                _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            Color rgb = palette != null ? palette.GetRGB(color) : Fallback(color);
            rgb.a = 1f;

            foreach (var r in _renderers)
            {
                if (r == null) continue;
                if (palette != null)
                {
                    var mat = palette.GetMaterial(color);
                    if (mat != null && r.sharedMaterial != mat) r.sharedMaterial = mat;
                }
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, rgb);
                _mpb.SetColor(ColorId, rgb);
                r.SetPropertyBlock(_mpb);
            }
        }

        private static Color Fallback(CubeColor c) => c switch
        {
            CubeColor.Red    => new Color(0.95f, 0.30f, 0.30f),
            CubeColor.Blue   => new Color(0.30f, 0.55f, 0.95f),
            CubeColor.Green  => new Color(0.40f, 0.85f, 0.45f),
            CubeColor.Yellow => new Color(0.98f, 0.80f, 0.25f),
            CubeColor.Purple => new Color(0.80f, 0.50f, 0.95f),
            CubeColor.Orange => new Color(0.98f, 0.60f, 0.20f),
            CubeColor.Cyan   => new Color(0.35f, 0.90f, 0.95f),
            CubeColor.Pink   => new Color(0.98f, 0.55f, 0.75f),
            _                => Color.white,
        };
    }
}
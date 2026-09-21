using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Interaction
{
    [CreateAssetMenu(fileName = "CubePalette_", menuName = "Cubes/Cube Palette", order = 1)]
    public class CubePalette : ScriptableObject
    {
        [SerializeField]
        private List<CubeColorEntry> entries = new()
        {
            new CubeColorEntry { color = CubeColor.Red,    rgb = new Color(0.95f, 0.30f, 0.30f) },
            new CubeColorEntry { color = CubeColor.Blue,   rgb = new Color(0.30f, 0.55f, 0.95f) },
            new CubeColorEntry { color = CubeColor.Green,  rgb = new Color(0.40f, 0.85f, 0.45f) },
            new CubeColorEntry { color = CubeColor.Yellow, rgb = new Color(0.98f, 0.80f, 0.25f) },
            new CubeColorEntry { color = CubeColor.Purple, rgb = new Color(0.80f, 0.50f, 0.95f) },
            new CubeColorEntry { color = CubeColor.Orange, rgb = new Color(0.98f, 0.60f, 0.20f) },
            new CubeColorEntry { color = CubeColor.Cyan,   rgb = new Color(0.35f, 0.90f, 0.95f) },
            new CubeColorEntry { color = CubeColor.Pink,   rgb = new Color(0.98f, 0.55f, 0.75f) },
        };

        private Dictionary<CubeColor, CubeColorEntry> _lookup;

        private void BuildLookup()
        {
            _lookup = new Dictionary<CubeColor, CubeColorEntry>();
            foreach (var e in entries) _lookup[e.color] = e;
        }

        private void EnsureLookup()
        {
            if (_lookup == null || _lookup.Count != entries.Count) BuildLookup();
        }

        public Color GetRGB(CubeColor color)
        {
            EnsureLookup();
            return _lookup.TryGetValue(color, out var e) ? e.rgb : Color.magenta;
        }

        public Material GetMaterial(CubeColor color)
        {
            EnsureLookup();
            return _lookup.TryGetValue(color, out var e) ? e.material : null;
        }

        public IReadOnlyList<CubeColorEntry> Entries => entries;

#if UNITY_EDITOR
        private void OnValidate() { _lookup = null; }
#endif
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Interaction
{
    /// <summary>
    /// Named cube colors. Add new entries as needed — the palette maps each to a concrete Color.
    /// </summary>
    public enum CubeColor
    {
        None = 0,
        Red,
        Blue,
        Green,
        Yellow,
        Purple,
        Orange,
        Cyan,
        Pink,
        White,
        Black,
    }

    [Serializable]
    public struct CubeColorEntry
    {
        public CubeColor color;
        public Color rgb;

        [Tooltip("Optional shared material for this color. If empty, only the RGB tint is applied.")]
        public Material material;

        [Tooltip("Optional label for UI, scoring, etc.")]
        public string displayName;
    }

    [CreateAssetMenu(fileName = "CubePalette_", menuName = "Cubes/Cube Palette", order = 1)]
    public class CubePalette : ScriptableObject
    {
        [SerializeField]
        private List<CubeColorEntry> entries = new()
        {
            new CubeColorEntry { color = CubeColor.Red,    rgb = new Color(0.95f, 0.30f, 0.30f), displayName = "Red" },
            new CubeColorEntry { color = CubeColor.Blue,   rgb = new Color(0.30f, 0.55f, 0.95f), displayName = "Blue" },
            new CubeColorEntry { color = CubeColor.Green,  rgb = new Color(0.40f, 0.85f, 0.45f), displayName = "Green" },
            new CubeColorEntry { color = CubeColor.Yellow, rgb = new Color(0.98f, 0.80f, 0.25f), displayName = "Yellow" },
            new CubeColorEntry { color = CubeColor.Purple, rgb = new Color(0.80f, 0.50f, 0.95f), displayName = "Purple" },
            new CubeColorEntry { color = CubeColor.Orange, rgb = new Color(0.98f, 0.60f, 0.20f), displayName = "Orange" },
            new CubeColorEntry { color = CubeColor.Cyan,   rgb = new Color(0.35f, 0.90f, 0.95f), displayName = "Cyan" },
            new CubeColorEntry { color = CubeColor.Pink,   rgb = new Color(0.98f, 0.55f, 0.75f), displayName = "Pink" },
            new CubeColorEntry { color = CubeColor.White,  rgb = Color.white,                     displayName = "White" },
            new CubeColorEntry { color = CubeColor.Black,  rgb = new Color(0.15f, 0.15f, 0.15f),  displayName = "Black" },
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

        public bool TryGet(CubeColor color, out CubeColorEntry entry)
        {
            EnsureLookup();
            return _lookup.TryGetValue(color, out entry);
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

        public string GetDisplayName(CubeColor color)
        {
            EnsureLookup();
            if (_lookup.TryGetValue(color, out var e) && !string.IsNullOrEmpty(e.displayName))
                return e.displayName;
            return color.ToString();
        }

        public IReadOnlyList<CubeColorEntry> Entries => entries;

#if UNITY_EDITOR
        private void OnValidate() { _lookup = null; }
#endif
    }
}
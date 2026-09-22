using System;
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
            new CubeColorEntry { color = CubeColor.Red,    rgb = new Color(0.95f, 0.30f, 0.30f), displayName = "Red" },
            new CubeColorEntry { color = CubeColor.Blue,   rgb = new Color(0.30f, 0.55f, 0.95f), displayName = "Blue" },
            new CubeColorEntry { color = CubeColor.Green,  rgb = new Color(0.40f, 0.85f, 0.45f), displayName = "Green" },
            new CubeColorEntry { color = CubeColor.Yellow, rgb = new Color(0.98f, 0.80f, 0.25f), displayName = "Yellow" },
            new CubeColorEntry { color = CubeColor.Purple, rgb = new Color(0.80f, 0.50f, 0.95f), displayName = "Purple" },
            new CubeColorEntry { color = CubeColor.Orange, rgb = new Color(0.98f, 0.60f, 0.20f), displayName = "Orange" },
            new CubeColorEntry { color = CubeColor.Cyan,   rgb = new Color(0.35f, 0.90f, 0.95f), displayName = "Cyan" },
            new CubeColorEntry { color = CubeColor.Pink,   rgb = new Color(0.98f, 0.55f, 0.75f), displayName = "Pink" },
        };

        [NonSerialized] private Dictionary<CubeColor, CubeColorEntry> _lookup;

        private void BuildLookup()
        {
            _lookup = new Dictionary<CubeColor, CubeColorEntry>();
            if (entries != null)
            {
                foreach (var e in entries)
                {
                    // Skip None — it's a placeholder, not a real color
                    if (e.color == CubeColor.None) continue;
                    _lookup[e.color] = e;
                }
            }
        }

        private void EnsureLookup()
        {
            if (_lookup == null) BuildLookup();
        }

        public Color GetRGB(CubeColor color)
        {
            // None means "no color assigned" — return a neutral gray rather than magenta,
            // so a missing assignment is obvious but not mistaken for a shader error.
            if (color == CubeColor.None)
                return new Color(0.5f, 0.5f, 0.5f);

            EnsureLookup();

            if (_lookup.TryGetValue(color, out var e))
                return e.rgb;

            Debug.LogWarning($"[CubePalette {name}] No entry for color '{color}'. Using gray.");
            return new Color(0.5f, 0.5f, 0.5f);
        }

        public Material GetMaterial(CubeColor color)
        {
            if (color == CubeColor.None) return null;

            EnsureLookup();
            return _lookup.TryGetValue(color, out var e) ? e.material : null;
        }

        public string GetDisplayName(CubeColor color)
        {
            if (color == CubeColor.None) return "None";

            EnsureLookup();
            if (_lookup.TryGetValue(color, out var e) && !string.IsNullOrEmpty(e.displayName))
                return e.displayName;

            return color.ToString();
        }

        public IReadOnlyList<CubeColorEntry> Entries => entries;

#if UNITY_EDITOR
        private void OnValidate()
        {
            _lookup = null;
        }

        /// <summary>
        /// Inspector-side helper: if the entries list is empty, fill it with defaults.
        /// Right-click the asset in the Project window → "Populate Defaults".
        /// </summary>
        [ContextMenu("Populate Defaults")]
        private void PopulateDefaults()
        {
            entries = new List<CubeColorEntry>
            {
                new CubeColorEntry { color = CubeColor.Red,    rgb = new Color(0.95f, 0.30f, 0.30f), displayName = "Red" },
                new CubeColorEntry { color = CubeColor.Blue,   rgb = new Color(0.30f, 0.55f, 0.95f), displayName = "Blue" },
                new CubeColorEntry { color = CubeColor.Green,  rgb = new Color(0.40f, 0.85f, 0.45f), displayName = "Green" },
                new CubeColorEntry { color = CubeColor.Yellow, rgb = new Color(0.98f, 0.80f, 0.25f), displayName = "Yellow" },
                new CubeColorEntry { color = CubeColor.Purple, rgb = new Color(0.80f, 0.50f, 0.95f), displayName = "Purple" },
                new CubeColorEntry { color = CubeColor.Orange, rgb = new Color(0.98f, 0.60f, 0.20f), displayName = "Orange" },
                new CubeColorEntry { color = CubeColor.Cyan,   rgb = new Color(0.35f, 0.90f, 0.95f), displayName = "Cyan" },
                new CubeColorEntry { color = CubeColor.Pink,   rgb = new Color(0.98f, 0.55f, 0.75f), displayName = "Pink" },
            };

            _lookup = null;
            Debug.Log($"[CubePalette {name}] Populated {entries.Count} default entries.");
        }
#endif
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Interaction
{
    /// <summary>
    /// One entry in a shape pool: which shape, how often, and any restrictions.
    /// </summary>
    [Serializable]
    public struct ShapeEntry
    {
        [Tooltip("The shape this entry refers to.")]
        public CubeShapeType shape;

        [Tooltip("If false, this shape is skipped when picking.")]
        public bool enabled;

        [Tooltip("Relative weight. Higher = more frequent. 0 = never picked while enabled is false.")]
        [Min(0f)]
        public float weight;

        [Tooltip("Allowed rotations (0..3 quarter-turns). If empty, all rotations are allowed.")]
        public int[] allowedRotations;

        public ShapeEntry(CubeShapeType shape, float weight = 1f, bool enabled = true)
        {
            this.shape = shape;
            this.weight = weight;
            this.enabled = enabled;
            this.allowedRotations = null;
        }
    }

    /// <summary>
    /// Data asset describing which shapes a tray can generate and how often.
    /// Replaces a plain CubeShapeType[] on the tray.
    /// </summary>
    [CreateAssetMenu(fileName = "ShapePool_", menuName = "Cubes/Cube Shape Pool", order = 2)]
    public class CubeShapeDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string poolName = "Default Pool";
        [TextArea] public string description;

        [Header("Shape Entries")]
        [Tooltip("Each entry is a shape the tray may spawn, with its own weight and options.")]
        [SerializeField]
        private List<ShapeEntry> entries = new()
        {
            new ShapeEntry(CubeShapeType.Whole,           weight: 3f),
            new ShapeEntry(CubeShapeType.FourSmall,       weight: 2f),
            new ShapeEntry(CubeShapeType.TwoHalf,         weight: 2f),
            new ShapeEntry(CubeShapeType.HalfAndTwoSmall, weight: 1f),
        };

        [Header("Picking Rules")]
        [Tooltip("If true, the picker avoids picking the same shape twice in a row.")]
        [SerializeField] private bool avoidImmediateRepeat = true;

        // -------- Runtime caches --------
        [NonSerialized] private int _lastPickedIndex = -1;

        #region Public API

        public IReadOnlyList<ShapeEntry> Entries => entries;

        /// <summary>Number of enabled entries with weight > 0.</summary>
        public int EnabledCount
        {
            get
            {
                int n = 0;
                foreach (var e in entries)
                    if (e.enabled && e.weight > 0f) n++;
                return n;
            }
        }

        /// <summary>Sum of all enabled weights.</summary>
        public float TotalWeight
        {
            get
            {
                float sum = 0f;
                foreach (var e in entries)
                    if (e.enabled && e.weight > 0f) sum += e.weight;
                return sum;
            }
        }

        /// <summary>
        /// Picks a random shape based on the entry weights and options.
        /// Returns false if no entry is eligible.
        /// </summary>
        public bool TryPick(out CubeShapeType shape, out int rotation, out int entryIndex)
        {
            shape = default;
            rotation = 0;
            entryIndex = -1;

            if (entries == null || entries.Count == 0) return false;

            float total = TotalWeight;
            if (total <= 0f) return false;

            // Build a working set to allow repeat-avoidance without permanent mutation
            List<int> candidates = null;
            if (avoidImmediateRepeat && EnabledCount > 1)
            {
                candidates = new List<int>(entries.Count);
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (!e.enabled || e.weight <= 0f) continue;
                    if (i == _lastPickedIndex) continue;
                    candidates.Add(i);
                }

                // If avoiding repeat wiped out everything, fall back to full set
                if (candidates.Count == 0) candidates = null;
            }

            // Roll a random index in the eligible set
            int picked;
            if (candidates != null)
            {
                float subTotal = 0f;
                foreach (int i in candidates) subTotal += entries[i].weight;

                float r = UnityEngine.Random.value * subTotal;
                float acc = 0f;
                picked = candidates[candidates.Count - 1];
                foreach (int i in candidates)
                {
                    acc += entries[i].weight;
                    if (r <= acc) { picked = i; break; }
                }
            }
            else
            {
                float r = UnityEngine.Random.value * total;
                float acc = 0f;
                picked = entries.Count - 1;
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (!e.enabled || e.weight <= 0f) continue;
                    acc += e.weight;
                    if (r <= acc) { picked = i; break; }
                }
            }

            var chosen = entries[picked];
            shape = chosen.shape;
            rotation = PickRotation(chosen);
            entryIndex = picked;

            _lastPickedIndex = picked;
            return true;
        }

        /// <summary>Force a pick of a specific shape (bypasses weights). Useful for tests or scripted levels.</summary>
        public bool TryPickSpecific(CubeShapeType shape, out int rotation, out int entryIndex)
        {
            rotation = 0;
            entryIndex = -1;

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].shape != shape) continue;
                entryIndex = i;
                rotation = PickRotation(entries[i]);
                _lastPickedIndex = i;
                return true;
            }
            return false;
        }

        /// <summary>Reset the "last picked" memory — useful when re-entering a level.</summary>
        public void ResetPicker()
        {
            _lastPickedIndex = -1;
        }

        #endregion

        #region Rotation

        private static int PickRotation(ShapeEntry entry)
        {
            if (entry.allowedRotations != null && entry.allowedRotations.Length > 0)
            {
                int idx = UnityEngine.Random.Range(0, entry.allowedRotations.Length);
                return Mathf.Clamp(entry.allowedRotations[idx], 0, 3);
            }

            // No restriction — pick any rotation, but skip it for symmetric shapes
            if (IsRotationallyDistinct(entry.shape))
                return UnityEngine.Random.Range(0, 4);

            return 0;
        }

        private static bool IsRotationallyDistinct(CubeShapeType type)
        {
            return type switch
            {
                CubeShapeType.Whole => false,
                CubeShapeType.FourSmall => false,
                _ => true,
            };
        }

        #endregion

        #region Editor Helpers

        /// <summary>Total probability of a specific shape (0..1) — for inspector tooltips and tests.</summary>
        public float GetProbability(CubeShapeType shape)
        {
            float total = TotalWeight;
            if (total <= 0f) return 0f;

            float match = 0f;
            foreach (var e in entries)
                if (e.enabled && e.shape == shape)
                    match += e.weight;

            return match / total;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Keep weights sane
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.weight < 0f) e.weight = 0f;
                if (e.allowedRotations != null)
                {
                    for (int j = 0; j < e.allowedRotations.Length; j++)
                        e.allowedRotations[j] = Mathf.Clamp(e.allowedRotations[j], 0, 3);
                }
                entries[i] = e;
            }
        }
#endif

        #endregion
    }
}
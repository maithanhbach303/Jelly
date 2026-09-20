using UnityEngine;

namespace MyGame.Interaction
{
    /// <summary>
    /// Base behavior for a sub-cube inside a CubeShape.
    /// Gives the parent a stable API, and holds its own slot data.
    /// Extend this for special behaviors (color, animation, abilities, etc.).
    /// </summary>
    public class SubCube : MonoBehaviour
    {
        // Slot data assigned by CubeShape on spawn
        [HideInInspector] public Vector2 SlotPosition;   // in cell units, relative to shape origin
        [HideInInspector] public Vector2 SlotSize;       // in cell units

        // Back-reference to the shape that owns us
        public CubeShape Owner { get; private set; }

        /// <summary>Called by CubeShape right after instantiation.</summary>
        public void Initialize(CubeShape owner, Vector2 slotPosition, Vector2 slotSize)
        {
            Owner = owner;
            SlotPosition = slotPosition;
            SlotSize = slotSize;
            OnInitialized();
        }

        /// <summary>Override this in subclasses for custom setup (colors, anims, etc.).</summary>
        protected virtual void OnInitialized() { }
    }
}
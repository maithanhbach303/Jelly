using UnityEngine;

namespace MyGame.Interaction
{
    public interface IDraggable
    {
        /// <summary>Can this object currently be picked up?</summary>
        bool CanDrag { get; }

        /// <summary>Called once when the drag starts. worldHit = 3D point hit on the object.</summary>
        void OnPickup(Vector3 worldHit);

        /// <summary>Called every frame while dragging. worldGroundPoint = pointer position on the ground plane.</summary>
        void OnDrag(Vector3 worldGroundPoint);

        /// <summary>Called once when the drag is released (button up / touch lifted).</summary>
        void OnDrop();
    }
}
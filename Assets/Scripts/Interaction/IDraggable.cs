using UnityEngine;

namespace MyGame.Interaction
{
    public interface IDraggable
    {
        bool CanDrag { get; }
        void OnPickup(Vector3 worldHit);
        void OnDrag(Vector3 worldGroundPoint);
        void OnDrop();
    }
}
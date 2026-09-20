using UnityEngine;
using UnityEngine.InputSystem;

namespace MyGame.Interaction
{
    /// <summary>
    /// Centralized player input for drag-and-drop. Owns the Input System wiring,
    /// does 3D raycasting, and delegates to IDraggable objects.
    /// Nothing else in the game should read pointer input directly.
    /// </summary>
    public class DragController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private GameplayInput input;   // auto-generated wrapper
        [SerializeField] private Camera rayCamera;      // assign Main Camera

        [Header("Raycast")]
        [SerializeField] private LayerMask draggableMask;   // layer of draggable cubes
        [SerializeField] private LayerMask groundMask;      // layer of the ground plane
        [SerializeField] private float maxRayDistance = 200f;

        private IDraggable _active;

        // ---------- Lifecycle ----------

        private void Awake()
        {
            input ??= new GameplayInput();
            rayCamera ??= Camera.main;
        }

        private void OnEnable()
        {
            input.Gameplay.Enable();
            input.Gameplay.Press.started  += OnPressStarted;
            input.Gameplay.Press.canceled += OnPressCanceled;
        }

        private void OnDisable()
        {
            input.Gameplay.Press.started  -= OnPressStarted;
            input.Gameplay.Press.canceled -= OnPressCanceled;
            input.Gameplay.Disable();
        }

        // ---------- Input callbacks ----------

        private void OnPressStarted(InputAction.CallbackContext _)
        {
            Vector2 screen = input.Gameplay.Point.ReadValue<Vector2>();
            Ray ray = rayCamera.ScreenPointToRay(screen);

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, draggableMask))
            {
                if (hit.collider.TryGetComponent(out IDraggable draggable))
                {
                    _active = draggable;
                    _active.OnPickup(hit.point);
                }
            }
        }

        private void OnPressCanceled(InputAction.CallbackContext _)
        {
            if (_active == null) return;
            _active.OnDrop();
            _active = null;
        }

        // ---------- Continuous drag ----------

        private void Update()
        {
            if (_active == null) return;

            Vector2 screen = input.Gameplay.Point.ReadValue<Vector2>();
            Ray ray = rayCamera.ScreenPointToRay(screen);

            // Raycast against ground plane to find where the pointer is in world space
            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, groundMask))
            {
                _active.OnDrag(hit.point);
            }
        }
    }
}
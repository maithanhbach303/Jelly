using UnityEngine;
using UnityEngine.InputSystem;

namespace MyGame.Interaction
{
    public class DragController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private GameplayInput input;
        [SerializeField] private Camera rayCamera;

        [Header("Raycast")]
        [SerializeField] private LayerMask draggableMask;
        [SerializeField] private LayerMask groundMask;
        [SerializeField] private float maxRayDistance = 200f;

        private IDraggable _active;

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

        private void OnPressStarted(InputAction.CallbackContext _)
        {
            Vector2 screen = input.Gameplay.Point.ReadValue<Vector2>();
            Ray ray = rayCamera.ScreenPointToRay(screen);

            if (Physics.Raycast(ray, out var hit, maxRayDistance, draggableMask))
            {
                if (hit.collider.TryGetComponent(out IDraggable draggable) && draggable.CanDrag)
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

        private void Update()
        {
            if (_active == null) return;

            Vector2 screen = input.Gameplay.Point.ReadValue<Vector2>();
            Ray ray = rayCamera.ScreenPointToRay(screen);

            if (Physics.Raycast(ray, out var hit, maxRayDistance, groundMask))
                _active.OnDrag(hit.point);
        }
    }
}
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Moba
{
    // Moves the rig that the Cinemachine camera follows.
    // Y toggles hero lock, hold Space to centre on the hero, edge-pan / arrow keys when unlocked, scroll to zoom.
    [DefaultExecutionOrder(-100)]
    public class MobaCamera : MonoBehaviour
    {
        public HeroController hero;
        public CinemachineFollow follow;
        public bool locked = true;
        public float panSpeed = 35f;
        [Tooltip("Pixels from the screen edge that trigger edge panning.")]
        public float edgeSize = 12f;
        public float minZoom = 0.6f;
        public float maxZoom = 1.4f;
        public float zoomStep = 0.1f;
        public Vector2 boundsMin = new(-65f, -65f);
        public Vector2 boundsMax = new(65f, 65f);

        Vector3 _baseOffset;
        float _zoom = 1f;

        void Start()
        {
            if (follow != null) _baseOffset = follow.FollowOffset;
            if (hero != null) transform.position = hero.transform.position;
        }

        void LateUpdate()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard != null && keyboard.yKey.wasPressedThisFrame) locked = !locked;
            bool centering = keyboard != null && keyboard.spaceKey.isPressed;

            if ((locked || centering) && hero != null)
                transform.position = hero.transform.position;
            else
                Pan(keyboard, mouse);

            if (mouse != null && follow != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0f)
                {
                    _zoom = Mathf.Clamp(_zoom - Mathf.Sign(scroll) * zoomStep, minZoom, maxZoom);
                    follow.FollowOffset = _baseOffset * _zoom;
                }
            }
        }

        void Pan(Keyboard keyboard, Mouse mouse)
        {
            Vector2 dir = Vector2.zero;

            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.isPressed) dir.x -= 1f;
                if (keyboard.rightArrowKey.isPressed) dir.x += 1f;
                if (keyboard.downArrowKey.isPressed) dir.y -= 1f;
                if (keyboard.upArrowKey.isPressed) dir.y += 1f;
            }

            if (mouse != null && Application.isFocused)
            {
                Vector2 p = mouse.position.ReadValue();
                bool inside = p.x >= 0f && p.y >= 0f && p.x <= Screen.width && p.y <= Screen.height;
                if (inside)
                {
                    if (p.x <= edgeSize) dir.x -= 1f;
                    else if (p.x >= Screen.width - edgeSize) dir.x += 1f;
                    if (p.y <= edgeSize) dir.y -= 1f;
                    else if (p.y >= Screen.height - edgeSize) dir.y += 1f;
                }
            }

            if (dir == Vector2.zero) return;

            Vector3 pos = transform.position + new Vector3(dir.x, 0f, dir.y).normalized * (panSpeed * Time.unscaledDeltaTime);
            pos.x = Mathf.Clamp(pos.x, boundsMin.x, boundsMax.x);
            pos.z = Mathf.Clamp(pos.z, boundsMin.y, boundsMax.y);
            transform.position = pos;
        }
    }
}

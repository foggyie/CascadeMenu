using UnityEngine;
using UnityEngine.InputSystem;

namespace CascadeMenu.Mods.Flight
{
    /// <summary>
    /// Desktop free-flight, toggled from the menu.
    ///
    /// Reads input through the Input System package (the game has the legacy
    /// UnityEngine.Input class disabled, so Input.GetKey would throw).
    ///
    /// Movement is applied in LateUpdate (after the game's physics step) by
    /// moving the rig transform directly, so it is not overwritten by Gorilla
    /// Tag's locomotion every frame. Velocity is cleared on disable.
    ///
    /// Controls while enabled: WASD move, Space up, Ctrl down, Shift faster,
    /// Alt slower, arrow keys turn, hold right mouse to look.
    /// </summary>
    public class WASDFly : Mod
    {
        private const float BaseSpeed = 8f;

        private Vector3 _move;
        private float _multiplier = 1f;

        public WASDFly()
        {
            Name = "WASD Fly";
            isToggle = true;
        }

        public override void OnEnable()
        {
            _move = Vector3.zero;
            Plugin.Log?.LogInfo("Cascade mod: " + Name + " enabled.");
        }

        public override void OnDisable()
        {
            _move = Vector3.zero;
            Stop();
            Plugin.Log?.LogInfo("Cascade mod: " + Name + " disabled.");
        }

        public override void Update()
        {
            _move = Vector3.zero;
            _multiplier = 1f;

            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (GorillaTagger.Instance == null || GorillaTagger.Instance.rigidbody == null) return;

            if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) _multiplier *= 2f;
            if (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed) _multiplier *= 0.5f;

            Transform view = GetView();
            Vector3 forward = view != null ? Vector3.ProjectOnPlane(view.forward, Vector3.up) : Vector3.forward;
            Vector3 right = view != null ? Vector3.ProjectOnPlane(view.right, Vector3.up) : Vector3.right;
            forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
            right = right.sqrMagnitude > 0.001f ? right.normalized : Vector3.right;

            Vector3 move = Vector3.zero;
            if (kb.wKey.isPressed) move += forward;
            if (kb.sKey.isPressed) move -= forward;
            if (kb.dKey.isPressed) move += right;
            if (kb.aKey.isPressed) move -= right;
            if (kb.spaceKey.isPressed) move += Vector3.up;
            if (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed) move -= Vector3.up;
            _move = move;

            if (view != null && kb.rightArrowKey.isPressed)
                view.Rotate(Vector3.up, 250f * Time.deltaTime, Space.World);
            if (view != null && kb.leftArrowKey.isPressed)
                view.Rotate(Vector3.up, -250f * Time.deltaTime, Space.World);

            // Hold the right mouse button to look around.
            Mouse mouse = Mouse.current;
            if (view != null && (kb.upArrowKey.isPressed || kb.downArrowKey.isPressed))
            {
                float pitch = (kb.upArrowKey.isPressed ? -1f : 0f) + (kb.downArrowKey.isPressed ? 1f : 0f);
                view.Rotate(Vector3.right, pitch * 120f * Time.deltaTime, Space.World);
            }
            else if (view != null && mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                view.Rotate(Vector3.up, delta.x * 0.2f, Space.World);
                view.Rotate(Vector3.right, -delta.y * 0.2f, Space.World);
            }
        }

        public override void LateUpdate()
        {
            if (_move.sqrMagnitude < 0.0001f) return;
            if (GorillaTagger.Instance == null || GorillaTagger.Instance.rigidbody == null) return;

            Rigidbody body = GorillaTagger.Instance.rigidbody;

            // Cancel gravity/momentum so flight is steady, then move the
            // transform directly. LateUpdate runs after the game has had its
            // physics step, so our move sticks.
            body.linearVelocity = Vector3.zero;
            body.transform.position += _move.normalized * (BaseSpeed * _multiplier * Time.deltaTime);
        }

        private void Stop()
        {
            if (GorillaTagger.Instance != null && GorillaTagger.Instance.rigidbody != null)
                GorillaTagger.Instance.rigidbody.linearVelocity = Vector3.zero;
        }

        private static Transform GetView()
        {
            if (GorillaTagger.Instance == null) return null;
            GameObject cam = GorillaTagger.Instance.mainCamera;
            if (cam == null) return null;
            return cam.transform.parent != null ? cam.transform.parent : cam.transform;
        }
    }
}

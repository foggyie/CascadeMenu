using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;

namespace CascadeMenu
{
    /// <summary>One physical controller button that can be used as a bind.</summary>
    internal enum VrButton
    {
        None = 0,
        LeftPrimary,     // X
        LeftSecondary,   // Y
        LeftTrigger,
        LeftGrip,
        LeftStick,       // joystick click
        RightPrimary,    // A
        RightSecondary,  // B
        RightTrigger,
        RightGrip,
        RightStick       // joystick click
    }

    /// <summary>
    /// One place for VR controller reads.
    ///
    /// Gorilla Tag drives its own input through the Input System, so the most
    /// reliable source is the XR device that XRController.leftHand/rightHand
    /// points at. Controls are looked up by name because different Input System
    /// builds expose different typed properties.
    ///
    /// Fallback chain: XRController (Input System) -> ControllerInputPoller ->
    /// legacy UnityEngine.XR.InputDevices. That keeps sticks and buttons alive
    /// on SteamVR, OpenXR, Oculus and the legacy XR runtime.
    /// </summary>
    internal static class VrInput
    {
        /// <summary>Dropdown labels, in <see cref="VrButton"/> order.</summary>
        internal static readonly string[] ButtonNames =
        {
            "None",
            "Left A/X",
            "Left B/Y",
            "Left Trigger",
            "Left Grip",
            "Left Stick",
            "Right A/X",
            "Right B/Y",
            "Right Trigger",
            "Right Grip",
            "Right Stick"
        };

        private const int ButtonCount = (int)VrButton.RightStick + 1;

        private static readonly bool[] LastHeld = new bool[ButtonCount];

        private static readonly bool[] PressedThisFrame = new bool[ButtonCount];

        private static int _cachedFrame = -1;

        internal static bool VrActive
        {
            get { return UnityEngine.XR.XRSettings.isDeviceActive; }
        }

        // -----------------------------------------------------------------
        // Parsing / formatting
        // -----------------------------------------------------------------
        internal static VrButton Parse(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                VrButton parsed;
                if (Enum.TryParse(value.Trim(), true, out parsed))
                {
                    return parsed;
                }
            }
            return VrButton.None;
        }

        internal static string Format(VrButton button)
        {
            return button.ToString();
        }

        internal static int IndexOf(string value)
        {
            return Mathf.Clamp((int)Parse(value), 0, ButtonNames.Length - 1);
        }

        internal static VrButton FromIndex(int index)
        {
            return (VrButton)Mathf.Clamp(index, 0, ButtonNames.Length - 1);
        }

        internal static string Name(VrButton button)
        {
            int index = Mathf.Clamp((int)button, 0, ButtonNames.Length - 1);
            return ButtonNames[index];
        }

        // -----------------------------------------------------------------
        // Sticks
        // -----------------------------------------------------------------
        /// <summary>Right stick, preferring the Input System XR device.</summary>
        internal static Vector2 RightStick()
        {
            XRController right = XRController.rightHand;
            if (right != null)
            {
                Vector2Control axis = right.TryGetChildControl<Vector2Control>("primary2DAxis");
                if (axis != null)
                {
                    Vector2 value = axis.ReadValue();
                    if (value.sqrMagnitude > 0.0001f)
                    {
                        return value;
                    }
                }
            }

            ControllerInputPoller poller = ControllerInputPoller.instance;
            if (poller != null)
            {
                return poller.rightControllerPrimary2DAxis;
            }
            return Vector2.zero;
        }

        /// <summary>Left stick, preferring the Input System XR device.</summary>
        internal static Vector2 LeftStick()
        {
            XRController left = XRController.leftHand;
            if (left != null)
            {
                Vector2Control axis = left.TryGetChildControl<Vector2Control>("primary2DAxis");
                if (axis != null)
                {
                    Vector2 value = axis.ReadValue();
                    if (value.sqrMagnitude > 0.0001f)
                    {
                        return value;
                    }
                }
            }

            ControllerInputPoller poller = ControllerInputPoller.instance;
            if (poller != null)
            {
                return poller.leftControllerPrimary2DAxis;
            }
            return Vector2.zero;
        }

        // -----------------------------------------------------------------
        // Buttons
        // -----------------------------------------------------------------
        /// <summary>True while the button is held down.</summary>
        internal static bool Held(VrButton button)
        {
            if (button == VrButton.None)
            {
                return false;
            }

            bool? value = ReadInputSystem(button);
            if (value.HasValue) return value.Value;

            value = ReadPoller(button);
            if (value.HasValue) return value.Value;

            value = ReadLegacy(button);
            return value.HasValue && value.Value;
        }

        /// <summary>True on the frame the button went down.</summary>
        internal static bool Pressed(VrButton button)
        {
            if (button == VrButton.None)
            {
                return false;
            }

            EnsureFrame();
            return PressedThisFrame[(int)button];
        }

        /// <summary>Right stick click, kept for existing callers.</summary>
        internal static bool RightStickClicked()
        {
            return Pressed(VrButton.RightStick);
        }

        /// <summary>First button that went down this frame, or None.</summary>
        internal static bool CapturePressed(out VrButton button)
        {
            EnsureFrame();
            for (int i = 1; i < ButtonCount; i++)
            {
                if (PressedThisFrame[i])
                {
                    button = (VrButton)i;
                    return true;
                }
            }
            button = VrButton.None;
            return false;
        }

        /// <summary>
        /// Marks everything that is already held as "not new". Called when a
        /// rebind starts so the trigger/grip used to press the Rebind button is
        /// not captured as the new binding.
        /// </summary>
        internal static void PrimeHeld()
        {
            EnsureFrame();
            for (int i = 0; i < ButtonCount; i++)
            {
                PressedThisFrame[i] = false;
            }
        }

        private static void EnsureFrame()
        {
            int frame = Time.frameCount;
            if (frame == _cachedFrame)
            {
                return;
            }
            _cachedFrame = frame;

            for (int i = 1; i < ButtonCount; i++)
            {
                bool now = Held((VrButton)i);
                PressedThisFrame[i] = now && !LastHeld[i];
                LastHeld[i] = now;
            }
        }

        // -----------------------------------------------------------------
        // Sources
        // -----------------------------------------------------------------
        private static bool? ReadInputSystem(VrButton button)
        {
            XRController controller = Hand(button);
            if (controller == null)
            {
                return null;
            }

            if (IsAxis(button))
            {
                AxisControl axis = controller.TryGetChildControl<AxisControl>(ControlName(button));
                if (axis != null)
                {
                    return axis.ReadValue() > 0.5f;
                }
                return null;
            }

            if (IsStickClick(button))
            {
                ButtonControl primary = controller.TryGetChildControl<ButtonControl>("primary2DAxisClick");
                if (primary != null) return primary.isPressed;
                ButtonControl secondary = controller.TryGetChildControl<ButtonControl>("secondary2DAxisClick");
                if (secondary != null) return secondary.isPressed;
                return null;
            }

            ButtonControl control = controller.TryGetChildControl<ButtonControl>(ControlName(button));
            return control != null ? (bool?)control.isPressed : null;
        }

        private static bool? ReadPoller(VrButton button)
        {
            ControllerInputPoller poller = ControllerInputPoller.instance;
            if (poller == null)
            {
                return null;
            }

            switch (button)
            {
                case VrButton.LeftPrimary:
                    return poller.leftControllerPrimaryButton;
                case VrButton.LeftSecondary:
                    return poller.leftControllerSecondaryButton;
                case VrButton.LeftTrigger:
                    return poller.leftControllerTriggerButton || poller.leftControllerIndexFloat > 0.5f;
                case VrButton.LeftGrip:
                    return poller.leftControllerGripFloat > 0.5f;
                case VrButton.RightPrimary:
                    return poller.rightControllerPrimaryButton;
                case VrButton.RightSecondary:
                    return poller.rightControllerSecondaryButton;
                case VrButton.RightTrigger:
                    return poller.rightControllerTriggerButton || poller.rightControllerIndexFloat > 0.5f;
                case VrButton.RightGrip:
                    return poller.rightControllerGripFloat > 0.5f;
                default:
                    // The poller does not expose the joystick click.
                    return null;
            }
        }

        private static bool? ReadLegacy(VrButton button)
        {
            bool left = IsLeft(button);
            UnityEngine.XR.InputDevice device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(
                left ? UnityEngine.XR.XRNode.LeftHand : UnityEngine.XR.XRNode.RightHand);
            if (!device.isValid)
            {
                return null;
            }

            switch (button)
            {
                case VrButton.LeftPrimary:
                case VrButton.RightPrimary:
                    if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool primary))
                    {
                        return primary;
                    }
                    break;

                case VrButton.LeftSecondary:
                case VrButton.RightSecondary:
                    if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool secondary))
                    {
                        return secondary;
                    }
                    break;

                case VrButton.LeftTrigger:
                case VrButton.RightTrigger:
                    if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float trigger))
                    {
                        return trigger > 0.5f;
                    }
                    if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool triggerButton))
                    {
                        return triggerButton;
                    }
                    break;

                case VrButton.LeftGrip:
                case VrButton.RightGrip:
                    if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out float grip))
                    {
                        return grip > 0.5f;
                    }
                    if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out bool gripButton))
                    {
                        return gripButton;
                    }
                    break;

                case VrButton.LeftStick:
                case VrButton.RightStick:
                    if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxisClick, out bool primaryClick))
                    {
                        return primaryClick;
                    }
                    if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondary2DAxisClick, out bool secondaryClick))
                    {
                        return secondaryClick;
                    }
                    break;
            }
            return null;
        }

        private static XRController Hand(VrButton button)
        {
            return IsLeft(button) ? XRController.leftHand : XRController.rightHand;
        }

        private static bool IsLeft(VrButton button)
        {
            switch (button)
            {
                case VrButton.LeftPrimary:
                case VrButton.LeftSecondary:
                case VrButton.LeftTrigger:
                case VrButton.LeftGrip:
                case VrButton.LeftStick:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsAxis(VrButton button)
        {
            switch (button)
            {
                case VrButton.LeftTrigger:
                case VrButton.RightTrigger:
                case VrButton.LeftGrip:
                case VrButton.RightGrip:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsStickClick(VrButton button)
        {
            return button == VrButton.LeftStick || button == VrButton.RightStick;
        }

        private static string ControlName(VrButton button)
        {
            switch (button)
            {
                case VrButton.LeftPrimary:
                case VrButton.RightPrimary:
                    return "primaryButton";
                case VrButton.LeftSecondary:
                case VrButton.RightSecondary:
                    return "secondaryButton";
                case VrButton.LeftTrigger:
                case VrButton.RightTrigger:
                    return "trigger";
                case VrButton.LeftGrip:
                case VrButton.RightGrip:
                    return "grip";
                default:
                    return "primary2DAxisClick";
            }
        }
    }
}

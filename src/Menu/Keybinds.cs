using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace CascadeMenu
{
    /// <summary>
    /// Small helper around the game's Input System keyboard.
    ///
    /// Keybinds are stored in the config file as Input System key names ("F1",
    /// "Delete", "LeftAlt", ...). This class parses those names, draws a friendly
    /// label for the menu, and captures the next key press for rebinding.
    ///
    /// The game disables legacy UnityEngine.Input, so everything here goes
    /// through Keyboard.current only.
    /// </summary>
    internal static class Keybinds
    {
        /// <summary>Parse a stored key name, returning a fallback when invalid.</summary>
        internal static Key Parse(string value, Key fallback = Key.None)
        {
            if (!string.IsNullOrEmpty(value))
            {
                Key parsed;
                if (Enum.TryParse(value.Trim(), true, out parsed) && parsed != Key.None)
                {
                    return parsed;
                }
            }
            return fallback;
        }

        /// <summary>Short label shown in the settings rows.</summary>
        internal static string Name(Key key)
        {
            switch (key)
            {
                case Key.None: return "unbound";
                case Key.Escape: return "Esc";
                case Key.Space: return "Space";
                case Key.Enter: return "Enter";
                case Key.Backspace: return "Backspace";
                case Key.LeftShift: return "Left Shift";
                case Key.RightShift: return "Right Shift";
                case Key.LeftCtrl: return "Left Ctrl";
                case Key.RightCtrl: return "Right Ctrl";
                case Key.LeftAlt: return "Left Alt";
                case Key.RightAlt: return "Right Alt";
                case Key.UpArrow: return "Up Arrow";
                case Key.DownArrow: return "Down Arrow";
                case Key.LeftArrow: return "Left Arrow";
                case Key.RightArrow: return "Right Arrow";
                case Key.Digit1: return "1";
                case Key.Digit2: return "2";
                case Key.Digit3: return "3";
                case Key.Digit4: return "4";
                case Key.Digit5: return "5";
                case Key.Digit6: return "6";
                case Key.Digit7: return "7";
                case Key.Digit8: return "8";
                case Key.Digit9: return "9";
                case Key.Digit0: return "0";
                default: return key.ToString();
            }
        }

        /// <summary>True on the frame the bind was pressed.</summary>
        internal static bool Pressed(Key key)
        {
            try
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard == null || key == Key.None)
                {
                    return false;
                }
                KeyControl control = keyboard[key];
                return control != null && control.wasPressedThisFrame;
            }
            catch (Exception)
            {
                // A keyboard device that is still initialising must not break Update.
                return false;
            }
        }

        /// <summary>First key pressed this frame, or Key.None.</summary>
        internal static Key CapturePressed()
        {
            try
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard == null)
                {
                    return Key.None;
                }

                var keys = keyboard.allKeys;
                for (int i = 0; i < keys.Count; i++)
                {
                    KeyControl control = keys[i];
                    if (control != null && control.wasPressedThisFrame)
                    {
                        return control.keyCode;
                    }
                }
            }
            catch (Exception)
            {
                // Ignore transient device errors while the keyboard reconnects.
            }
            return Key.None;
        }
    }
}

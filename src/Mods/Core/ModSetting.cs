using System;
using System.Globalization;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Tools;
using CascadeMenu.Mods.Cosmetics;
using CascadeMenu.Mods.Visual;
using CascadeMenu.Mods.Movement;
using CascadeMenu.Mods.Overlays;

namespace CascadeMenu.Mods.Core
{
    /// <summary>How a mod-authored option is rendered and stored.</summary>
    public enum ModSettingKind
    {
        Toggle = 0,
        Slider = 1,
        Stepper = 2,
        Dropdown = 3,
        Text = 4
    }

    /// <summary>
    /// One extra option a mod can show on its settings page.
    ///
    /// The mod owns the value (a field or property) and supplies a getter and a
    /// setter. <see cref="ModRegistry"/> binds one config entry per setting using
    /// the mod's class name and <see cref="Key"/>, so persistence needs no code
    /// in the mod itself.
    /// </summary>
    public sealed class ModSetting
    {
        public readonly string Key;
        public readonly string Title;
        public readonly string Description;
        public readonly ModSettingKind Kind;

        public readonly float Min;
        public readonly float Max;
        public readonly int MinInt;
        public readonly int MaxInt;
        public readonly string[] Options;
        public readonly Func<float, string> Format;

        private readonly Func<bool> _getBool;
        private readonly Action<bool> _setBool;
        private readonly Func<float> _getFloat;
        private readonly Action<float> _setFloat;
        private readonly Func<int> _getInt;
        private readonly Action<int> _setInt;
        private readonly Func<string> _getText;
        private readonly Action<string> _setText;

        private ModSetting(
            string key,
            string title,
            string description,
            ModSettingKind kind,
            float min,
            float max,
            int minInt,
            int maxInt,
            string[] options,
            Func<float, string> format,
            Func<bool> getBool,
            Action<bool> setBool,
            Func<float> getFloat,
            Action<float> setFloat,
            Func<int> getInt,
            Action<int> setInt,
            Func<string> getText,
            Action<string> setText)
        {
            Key = key;
            Title = title;
            Description = description ?? string.Empty;
            Kind = kind;
            Min = min;
            Max = max;
            MinInt = minInt;
            MaxInt = maxInt;
            Options = options ?? new string[0];
            Format = format;
            _getBool = getBool;
            _setBool = setBool;
            _getFloat = getFloat;
            _setFloat = setFloat;
            _getInt = getInt;
            _setInt = setInt;
            _getText = getText;
            _setText = setText;
        }

        public static ModSetting Toggle(
            string key,
            string title,
            string description,
            Func<bool> get,
            Action<bool> set)
        {
            return new ModSetting(key, title, description, ModSettingKind.Toggle,
                0f, 1f, 0, 1, null, null, get, set, null, null, null, null, null, null);
        }

        public static ModSetting Slider(
            string key,
            string title,
            string description,
            float min,
            float max,
            Func<float> get,
            Action<float> set,
            Func<float, string> format = null)
        {
            return new ModSetting(key, title, description, ModSettingKind.Slider,
                min, max, 0, 0, null, format, null, null, get, set, null, null, null, null);
        }

        public static ModSetting Stepper(
            string key,
            string title,
            string description,
            int min,
            int max,
            Func<int> get,
            Action<int> set,
            Func<int, string> format = null)
        {
            Func<float, string> formatted = format == null
                ? (Func<float, string>)null
                : value => format(MathfRound(value));

            return new ModSetting(key, title, description, ModSettingKind.Stepper,
                min, max, min, max, null, formatted, null, null, null, null, get, set, null, null);
        }

        public static ModSetting Dropdown(
            string key,
            string title,
            string description,
            string[] options,
            Func<int> get,
            Action<int> set)
        {
            int max = options != null && options.Length > 0 ? options.Length - 1 : 0;
            return new ModSetting(key, title, description, ModSettingKind.Dropdown,
                0f, max, 0, max, options, null, null, null, null, null, get, set, null, null);
        }

        /// <summary>
        /// A short free-text option (for example the custom-property key filter).
        /// The registry renders it as the same keyboard text field used by the
        /// theme hex editor.
        /// </summary>
        public static ModSetting Text(
            string key,
            string title,
            string description,
            Func<string> get,
            Action<string> set)
        {
            return new ModSetting(key, title, description, ModSettingKind.Text,
                0f, 0f, 0, 0, null, null, null, null, null, null, null, null, get, set);
        }

        public bool GetBool()
        {
            return _getBool != null && _getBool();
        }

        public void SetBool(bool value)
        {
            _setBool?.Invoke(value);
        }

        public float GetFloat()
        {
            if (_getFloat != null) return _getFloat();
            if (_getInt != null) return _getInt();
            return GetBool() ? 1f : 0f;
        }

        public void SetFloat(float value)
        {
            if (_setFloat != null)
            {
                _setFloat(value);
                return;
            }
            if (_setInt != null)
            {
                _setInt(MathfRound(value));
            }
        }

        public int GetInt()
        {
            if (_getInt != null) return _getInt();
            return MathfRound(GetFloat());
        }

        public void SetInt(int value)
        {
            if (_setInt != null)
            {
                _setInt(value);
                return;
            }
            SetFloat(value);
        }

        public string GetText()
        {
            return _getText != null ? (_getText() ?? string.Empty) : string.Empty;
        }

        public void SetText(string value)
        {
            _setText?.Invoke(value ?? string.Empty);
        }

        /// <summary>The current value as text, used as the config default.</summary>
        public string Serialize()
        {
            switch (Kind)
            {
                case ModSettingKind.Toggle:
                    return GetBool() ? "true" : "false";
                case ModSettingKind.Dropdown:
                case ModSettingKind.Stepper:
                    return GetInt().ToString(CultureInfo.InvariantCulture);
                case ModSettingKind.Text:
                    return GetText();
                default:
                    return GetFloat().ToString("R", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Restores a saved value, silently ignoring a damaged entry.</summary>
        public void Deserialize(string value)
        {
            if (string.IsNullOrEmpty(value)) return;

            switch (Kind)
            {
                case ModSettingKind.Toggle:
                    bool flag;
                    if (bool.TryParse(value.Trim(), out flag)) SetBool(flag);
                    break;
                case ModSettingKind.Text:
                    SetText(value);
                    break;
                case ModSettingKind.Dropdown:
                case ModSettingKind.Stepper:
                    int number;
                    if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                    {
                        SetInt(Clamp(number));
                    }
                    break;
                default:
                    float amount;
                    if (float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out amount))
                    {
                        SetFloat(UnityEngine.Mathf.Clamp(amount, Min, Max));
                    }
                    break;
            }
        }

        private int Clamp(int value)
        {
            return value < MinInt ? MinInt : (value > MaxInt ? MaxInt : value);
        }

        private static int MathfRound(float value)
        {
            return UnityEngine.Mathf.RoundToInt(value);
        }
    }
}

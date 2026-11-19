using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using CascadeMenu.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Discovers every <see cref="Mod"/> subclass in this assembly and turns it
    /// into menu rows automatically.
    ///
    /// Adding a new mod is one class:
    ///
    ///   [PluginToggleable]
    ///   public class MyMod : Mod
    ///   {
    ///       public MyMod() { Name = "My mod"; isToggle = true; }
    ///       public override void Update() { /* runs while enabled and active */ }
    ///   }
    ///
    /// ...and it appears on its namespace's page with an Enabled toggle, a
    /// keybind, a settings gear and an Active indicator. No registration call
    /// and no per-mod keybind code is needed.
    ///
    /// Categories come from the namespace: CascadeMenu.Mods.Halloween -> "Halloween",
    /// CascadeMenu.Mods.Super -> "Super", CascadeMenu.Mods -> "General".
    /// </summary>
    internal static class ModRegistry
    {
        private const string BindSection = "Binds";
        private const string SettingsSection = "Mod settings";
        private const float FallbackDoublePressWindow = 0.3f;

        /// <summary>Tag prefix for a generated mod settings page.</summary>
        internal const string SettingsTagPrefix = "cascade.modsettings.";

        private static readonly List<Mod> All = new List<Mod>();
        private static readonly List<string> CategoryList = new List<string>();

        // Sidebar order. Categories that are not listed here follow, sorted
        // alphabetically, so a namespace-only mod still gets a stable page.
        private static readonly string[] PreferredCategories =
        {
            "Overlays", "Movement", "Visual", "Fun", "Snake", "Tools", "Flight", "Desktop",
            "Performance", "Super"
        };

        // Persistence for the generated per-mod rows.
        private static readonly Dictionary<Mod, ConfigEntry<string>> KeyEntries =
            new Dictionary<Mod, ConfigEntry<string>>();
        private static readonly Dictionary<Mod, ConfigEntry<string>> PressEntries =
            new Dictionary<Mod, ConfigEntry<string>>();

        // Mod option changes are coalesced so dragging a slider does not write
        // the config file every frame.
        private static readonly HashSet<Mod> DirtySettings = new HashSet<Mod>();
        private static float _nextSettingsFlush;

        // Reused every frame so the input path does not allocate.
        private static readonly HashSet<Key> DoubleKeys = new HashSet<Key>();
        private static readonly HashSet<Key> PressedKeys = new HashSet<Key>();
        private static readonly Dictionary<Key, float> LastDoublePress = new Dictionary<Key, float>();

        private static MenuConfig _config;
        private static bool _loaded;
        private static bool _bindsApplied;

        /// <summary>
        /// Loads every mod and binds its saved key / press type. Called once by
        /// the plugin after the config file exists.
        /// </summary>
        internal static void Initialize(MenuConfig config)
        {
            _config = config;
            EnsureLoaded();
            ApplyBinds();
        }

        /// <summary>Creates one instance of every concrete Mod subclass (once).</summary>
        internal static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            Type[] types;
            try
            {
                types = typeof(Mod).Assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types;
            }

            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                if (type == null || type.IsAbstract || !typeof(Mod).IsAssignableFrom(type)) continue;
                if (type.GetConstructor(Type.EmptyTypes) == null) continue;

                try
                {
                    var mod = (Mod)Activator.CreateInstance(type);
                    if (mod == null) continue;
                    All.Add(mod);
                }
                catch (Exception exception)
                {
                    Plugin.Log?.LogWarning("Cascade mod load failed for " + type.Name + ": " + exception.Message);
                }
            }

            All.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            for (int i = 0; i < All.Count; i++)
            {
                string category = CategoryOf(All[i]);
                if (!CategoryList.Contains(category)) CategoryList.Add(category);
            }
            CategoryList.Sort(CompareCategories);

            Plugin.Log?.LogInfo("Cascade mod registry: " + All.Count + " mod(s) in " +
                                CategoryList.Count + " categor" + (CategoryList.Count == 1 ? "y" : "ies") + ".");
        }

        /// <summary>Binds saved keys and press types, then logs one line per mod.</summary>
        private static void ApplyBinds()
        {
            if (_bindsApplied || _config == null) return;
            _bindsApplied = true;

            for (int i = 0; i < All.Count; i++)
            {
                Mod mod = All[i];
                string id = mod.GetType().Name;

                ConfigEntry<string> keyEntry = _config.File.Bind(
                    BindSection,
                    id,
                    "None",
                    "Keyboard key for \"" + mod.Name + "\" (Input System name, e.g. F6). None = unbound.");

                ConfigEntry<string> pressEntry = _config.File.Bind(
                    BindSection,
                    id + " Press",
                    "Single",
                    "Press type for \"" + mod.Name + "\": Single or Double.");

                mod.Key = Keybinds.Parse(keyEntry.Value, Key.None);
                mod.PressType = ParsePressType(pressEntry.Value);

                KeyEntries[mod] = keyEntry;
                PressEntries[mod] = pressEntry;

                ApplySettings(mod);

                Plugin.Log?.LogInfo("Cascade bind: " + mod.Name + " = " +
                                    Keybinds.Name(mod.Key) + " (" + PressName(mod.PressType) + ").");
            }
        }

        private static ModPressType ParsePressType(string value)
        {
            ModPressType parsed;
            if (!string.IsNullOrEmpty(value) && Enum.TryParse(value.Trim(), true, out parsed))
            {
                return parsed;
            }
            return ModPressType.Single;
        }

        /// <summary>
        /// Restores every extra option a mod declared. The config section is
        /// shared; the key is "ClassName.SettingKey", so renaming a mod class
        /// starts a fresh entry instead of reading someone else's value.
        /// </summary>
        private static void ApplySettings(Mod mod)
        {
            if (_config == null || mod.Settings == null) return;

            string id = mod.GetType().Name;
            for (int i = 0; i < mod.Settings.Count; i++)
            {
                ModSetting setting = mod.Settings[i];
                if (setting == null) continue;

                try
                {
                    ConfigEntry<string> entry = _config.File.Bind(
                        SettingsSection,
                        id + "." + setting.Key,
                        setting.Serialize(),
                        setting.Title + " for \"" + mod.Name + "\".");

                    setting.Deserialize(entry.Value);
                }
                catch (Exception exception)
                {
                    Plugin.Log?.LogWarning("Cascade could not load a setting for \"" + mod.Name + "\": " + exception.Message);
                }
            }
        }

        /// <summary>Writes every setting of one mod to the config file.</summary>
        internal static void SaveSettings(Mod mod)
        {
            if (_config == null || mod == null || mod.Settings == null) return;

            string id = mod.GetType().Name;
            bool changed = false;
            for (int i = 0; i < mod.Settings.Count; i++)
            {
                ModSetting setting = mod.Settings[i];
                if (setting == null) continue;

                try
                {
                    ConfigEntry<string> entry = _config.File.Bind(
                        SettingsSection,
                        id + "." + setting.Key,
                        setting.Serialize(),
                        setting.Title + " for \"" + mod.Name + "\".");

                    entry.Value = setting.Serialize();
                    changed = true;
                }
                catch (Exception exception)
                {
                    Plugin.Log?.LogWarning("Cascade could not save a setting for \"" + mod.Name + "\": " + exception.Message);
                }
            }

            if (changed)
            {
                try
                {
                    _config.File.Save();
                }
                catch (Exception exception)
                {
                    Plugin.Log?.LogWarning("Cascade could not save the settings file: " + exception.Message);
                }
            }
        }

        private static string PressName(ModPressType type)
        {
            return type == ModPressType.Double ? "double press" : "single press";
        }

        private static void MarkSettingsDirty(Mod mod)
        {
            if (mod != null) DirtySettings.Add(mod);
        }

        /// <summary>Writes pending option changes at most once a second.</summary>
        private static void FlushSettings()
        {
            if (DirtySettings.Count == 0 || Time.unscaledTime < _nextSettingsFlush) return;
            _nextSettingsFlush = Time.unscaledTime + 1f;

            foreach (Mod mod in DirtySettings)
            {
                SaveSettings(mod);
            }
            DirtySettings.Clear();
        }

        /// <summary>Page name for a mod, from its attribute or its namespace.</summary>
        internal static string CategoryOf(Mod mod)
        {
            if (mod == null) return "General";

            ModCategoryAttribute category = (ModCategoryAttribute)Attribute.GetCustomAttribute(
                mod.GetType(), typeof(ModCategoryAttribute));
            if (category != null && !string.IsNullOrEmpty(category.Name))
            {
                return category.Name;
            }

            string space = mod.GetType().Namespace ?? string.Empty;
            const string prefix = "CascadeMenu.Mods";
            if (space.Length <= prefix.Length) return "General";
            if (!space.StartsWith(prefix, StringComparison.Ordinal)) return "General";

            string rest = space.Substring(prefix.Length).TrimStart('.');
            return string.IsNullOrEmpty(rest) ? "General" : rest;
        }

        private static int CompareCategories(string a, string b)
        {
            int orderA = Array.IndexOf(PreferredCategories, a);
            int orderB = Array.IndexOf(PreferredCategories, b);
            if (orderA < 0) orderA = PreferredCategories.Length;
            if (orderB < 0) orderB = PreferredCategories.Length;
            if (orderA != orderB) return orderA.CompareTo(orderB);
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Every discovered mod, for pages and overlays that list them.</summary>
        internal static IReadOnlyList<Mod> DiscoveredMods
        {
            get
            {
                EnsureLoaded();
                return All;
            }
        }

        /// <summary>Category pages, in discovery order (usually alphabetical).</summary>
        internal static List<string> Categories()
        {
            EnsureLoaded();
            return CategoryList;
        }

        internal static int CountIn(string category)
        {
            EnsureLoaded();
            int count = 0;
            for (int i = 0; i < All.Count; i++)
            {
                if (CategoryOf(All[i]) == category) count++;
            }
            return count;
        }

        internal static int EnabledCount
        {
            get
            {
                EnsureLoaded();
                int count = 0;
                for (int i = 0; i < All.Count; i++)
                {
                    if (All[i].Enabled) count++;
                }
                return count;
            }
        }

        internal static int ActiveCount
        {
            get
            {
                EnsureLoaded();
                int count = 0;
                for (int i = 0; i < All.Count; i++)
                {
                    if (All[i].IsActive) count++;
                }
                return count;
            }
        }

        /// <summary>Tag used by the sidebar tab for one module page.</summary>
        internal static string CategoryTag(string category)
        {
            return "cascade.category." + (string.IsNullOrEmpty(category) ? "General" : category);
        }

        private static CascadeIcon CategoryIcon(string category)
        {
            switch (category)
            {
                case "Overlays": return CascadeIcon.Eye;
                case "Movement": return CascadeIcon.Person;
                case "Visual": return CascadeIcon.Eye;
                case "Fun": return CascadeIcon.Sparkle;
                case "Snake": return CascadeIcon.Sparkle;
                case "Tools": return CascadeIcon.Gear;
                case "Flight": return CascadeIcon.Recenter;
                case "Desktop": return CascadeIcon.Sidebar;
                case "Performance": return CascadeIcon.Sliders;
                case "Super": return CascadeIcon.Layers;
                default: return CascadeIcon.Layers;
            }
        }

        /// <summary>
        /// Builds one sidebar page for a single module category. Every mod in
        /// that category gets the usual one-switch row; the shared footer can
        /// disarm the whole registry.
        /// </summary>
        internal static CascadePage BuildCategoryPage(CascadeMenuBehaviour menu, string category)
        {
            EnsureLoaded();

            int count = CountIn(category);
            var page = new CascadePage(
                category,
                count + (count == 1 ? " mod in this module" : " mods in this module"),
                CategoryIcon(category));
            page.Tag = CategoryTag(category);

            CascadeSection section = page.Section(
                category,
                "Flip one switch to start or stop a mod");

            for (int i = 0; i < All.Count; i++)
            {
                Mod mod = All[i];
                if (CategoryOf(mod) != category) continue;
                AppendModRow(section, menu, mod);
            }

            if (section.Rows.Count == 0)
            {
                page.Section("Empty").Label("Nothing here", "No mods were discovered", "-");
            }

            page.Section("Module", "Applies to every mod in the registry")
                .Button("Disable all", "Disarm every mod and stop its effect", "Disable",
                    DisableAll, false)
                .Label("Enabled", "Effect is on", EnabledCount + " / " + All.Count)
                .Label("Active", "Effects currently running", ActiveCount + " / " + All.Count);

            return page;
        }

        /// <summary>
        /// Appends one compact mod row (state line, Enabled switch and optional
        /// settings gear) to an existing section. The Mods page and the Visual
        /// tab both use this so a mod has exactly one row implementation.
        /// </summary>
        internal static void AppendModRow(CascadeSection section, CascadeMenuBehaviour menu, Mod mod)
        {
            if (section == null || mod == null) return;

            Mod captured = mod;
            section.Mod(
                captured.Name,
                captured.isToggle ? "Toggle mod" : "One-shot action",
                () => captured.Enabled,
                value => SetEffect(captured, value),
                () => captured.IsActive,
                () => Keybinds.Name(captured.Key),
                () =>
                {
                    if (menu != null) menu.BeginRebind(ModTarget(captured));
                },
                () => PressLabel(captured.PressType),
                () => CyclePress(captured),
                () => ActionLabel(captured),
                () => Trigger(captured),
                captured.isToggle,
                captured.HasSettings
                    ? (Action)(() =>
                    {
                        if (menu != null) menu.OpenModSettings(captured);
                    })
                    : null);
        }

        /// <summary>
        /// Turns a mod on or off from a plain feature toggle. On arms the mod
        /// and starts the effect in the same click; off stops the effect and
        /// disarms it, matching the other feature rows.
        /// </summary>
        internal static void SetEffect(Mod mod, bool value)
        {
            if (mod == null) return;

            try
            {
                if (value)
                {
                    mod.SetEnabled(true);
                    if (mod.isToggle) mod.SetActive(true);
                    else mod.ToggleActive();
                }
                else
                {
                    if (mod.isToggle) mod.SetActive(false);
                    mod.SetEnabled(false);
                }
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogError("Cascade mod \"" + mod.Name + "\" failed to switch: " + exception);
            }
        }

        /// <summary>Rebind target used by the menu for a mod's key row.</summary>
        private static string ModTarget(Mod mod)
        {
            return "mod:" + mod.GetType().Name;
        }

        internal static string SettingsTag(Mod mod)
        {
            return SettingsTagPrefix + (mod != null ? mod.GetType().Name : string.Empty);
        }

        internal static bool IsSettingsTag(string tag)
        {
            return !string.IsNullOrEmpty(tag) &&
                   tag.StartsWith(SettingsTagPrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Builds the settings page behind a mod's gear. It contains the
        /// Enabled switch, the live Active state and any options the mod
        /// declares. The page is added to the existing window, so only one
        /// settings panel can be open and its scroll position survives value
        /// changes.
        /// </summary>
        internal static CascadePage BuildSettingsPage(CascadeMenuBehaviour menu, Mod mod)
        {
            if (mod == null) return null;

            var page = new CascadePage(mod.Name + " settings",
                "Mod options", CascadeIcon.Sliders);
            page.Tag = SettingsTag(mod);
            page.HiddenFromSidebar = true;

            Mod captured = mod;

            page.Section("State", "The row switch starts and stops this mod")
                .Toggle("Enabled", "Turn the mod on or off",
                    () => captured.Enabled,
                    value => SetEffect(captured, value))
                .LiveLabel("Active", captured.isToggle
                        ? "Effect currently running"
                        : "One-shot mods do not stay active",
                    () => captured.IsActive ? "ON" : "OFF",
                    () => captured.IsActive);

            if (captured.Settings != null && captured.Settings.Count > 0)
            {
                CascadeSection options = page.Section("Options", "Settings declared by this mod");
                for (int i = 0; i < captured.Settings.Count; i++)
                {
                    AddSettingRow(options, captured, captured.Settings[i]);
                }
            }

            string category = CategoryOf(captured);
            page.Section("Module", "Back to this mod's tab")
                .Button("Back to " + category, "Return to the " + category + " module tab", "Back",
                    () => menu.OpenPage(CategoryTag(category), category), false);

            return page;
        }

        private static void AddSettingRow(CascadeSection section, Mod mod, ModSetting setting)
        {
            if (setting == null) return;

            switch (setting.Kind)
            {
                case ModSettingKind.Toggle:
                    section.Toggle(setting.Title, setting.Description,
                        () => setting.GetBool(),
                        value =>
                        {
                            setting.SetBool(value);
                            MarkSettingsDirty(mod);
                        });
                    break;

                case ModSettingKind.Slider:
                    section.Slider(setting.Title, setting.Description,
                        setting.Min, setting.Max,
                        () => setting.GetFloat(),
                        value =>
                        {
                            setting.SetFloat(value);
                            MarkSettingsDirty(mod);
                        },
                        setting.Format ?? (value => value.ToString("0.##")));
                    break;

                case ModSettingKind.Stepper:
                    section.Stepper(setting.Title, setting.Description,
                        setting.MinInt, setting.MaxInt,
                        () => setting.GetInt(),
                        value =>
                        {
                            setting.SetInt(value);
                            MarkSettingsDirty(mod);
                        },
                        setting.Format == null ? null : (Func<int, string>)(value => setting.Format(value)));
                    break;

                case ModSettingKind.Dropdown:
                    section.Dropdown(setting.Title, setting.Description,
                        setting.Options,
                        () => Mathf.Clamp(setting.GetInt(), 0, Mathf.Max(0, setting.Options.Length - 1)),
                        value =>
                        {
                            setting.SetInt(value);
                            MarkSettingsDirty(mod);
                        });
                    break;

                case ModSettingKind.Text:
                    section.TextField(setting.Title, setting.Description,
                        "all", 96,
                        () => setting.GetText(),
                        value =>
                        {
                            setting.SetText(value);
                            MarkSettingsDirty(mod);
                        });
                    break;
            }
        }

        private static string PressLabel(ModPressType type)
        {
            return type == ModPressType.Double ? "Double" : "Single";
        }

        private static string ActionLabel(Mod mod)
        {
            if (!mod.isToggle) return "Run";
            if (!mod.Enabled) return "Locked";
            return mod.IsActive ? "Stop" : "Start";
        }

        /// <summary>Runs a one-shot mod with error reporting.</summary>
        private static void Run(Mod mod)
        {
            try
            {
                mod.Execute();
                Plugin.Log?.LogInfo("Cascade mod executed: " + mod.Name);
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogError("Cascade mod \"" + mod.Name + "\" failed: " + exception);
            }
        }

        /// <summary>Menu action button: toggles Active, or runs a one-shot.</summary>
        private static void Trigger(Mod mod)
        {
            try
            {
                if (mod.isToggle)
                {
                    if (!mod.Enabled)
                    {
                        CascadeMenuBehaviour menu = CascadeMenuBehaviour.Instance;
                        if (menu != null)
                        {
                            menu.ShowToast("Mod disabled", "Enable \"" + mod.Name + "\" before activating it.");
                        }
                        return;
                    }

                    mod.ToggleActive();
                }
                else
                {
                    // The menu's Run button works even before the mod is armed.
                    Run(mod);
                }
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogError("Cascade mod \"" + mod.Name + "\" action failed: " + exception);
            }
        }

        private static void CyclePress(Mod mod)
        {
            SetPressType(mod, mod.PressType == ModPressType.Double
                ? ModPressType.Single
                : ModPressType.Double);
        }

        /// <summary>Look up a discovered mod by its display name (case-insensitive).</summary>
        internal static Mod Find(string name)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(name)) return null;
            for (int i = 0; i < All.Count; i++)
            {
                if (string.Equals(All[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return All[i];
            }
            return null;
        }

        /// <summary>Look up a mod by its class name, used by "mod:TypeName" rebinds.</summary>
        internal static Mod FindByTypeName(string typeName)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(typeName)) return null;
            for (int i = 0; i < All.Count; i++)
            {
                if (string.Equals(All[i].GetType().Name, typeName, StringComparison.Ordinal))
                    return All[i];
            }
            return null;
        }

        internal static bool IsEnabledByName(string name)
        {
            Mod mod = Find(name);
            return mod != null && mod.Enabled;
        }

        internal static void SetEnabledByName(string name, bool value)
        {
            Mod mod = Find(name);
            if (mod == null) return;
            SetEnabled(mod, value);
        }

        /// <summary>Stores a new key for a mod and writes it to the config file.</summary>
        internal static void SetKey(Mod mod, Key key)
        {
            if (mod == null) return;
            mod.Key = key;

            ConfigEntry<string> entry;
            if (KeyEntries.TryGetValue(mod, out entry))
            {
                try
                {
                    entry.Value = key.ToString();
                    if (_config != null) _config.File.Save();
                }
                catch (Exception exception)
                {
                    Plugin.Log?.LogWarning("Cascade could not save the bind for \"" + mod.Name + "\": " + exception.Message);
                }
            }

            // A double-press sequence started on the old key must not carry over.
            LastDoublePress.Remove(key);
        }

        /// <summary>Stores the Single/Double choice for a mod and saves it.</summary>
        internal static void SetPressType(Mod mod, ModPressType type)
        {
            if (mod == null) return;
            mod.PressType = type;

            ConfigEntry<string> entry;
            if (PressEntries.TryGetValue(mod, out entry))
            {
                try
                {
                    entry.Value = type.ToString();
                    if (_config != null) _config.File.Save();
                }
                catch (Exception exception)
                {
                    Plugin.Log?.LogWarning("Cascade could not save the press type for \"" + mod.Name + "\": " + exception.Message);
                }
            }
        }

        /// <summary>Turns a mod on or off, reporting failures instead of throwing into the UI.</summary>
        private static void SetEnabled(Mod mod, bool value)
        {
            try
            {
                mod.SetEnabled(value);
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogError("Cascade mod \"" + mod.Name + "\" failed to switch: " + exception);
            }
        }

        internal static void DisableAll()
        {
            EnsureLoaded();
            for (int i = 0; i < All.Count; i++)
            {
                try
                {
                    All[i].SetEnabled(false);
                }
                catch (Exception exception)
                {
                    Plugin.Log?.LogWarning("Cascade mod disable failed: " + exception.Message);
                }
            }
        }

        /// <summary>
        /// Handles every mod keybind. Only key-down edges are used, so holding a
        /// key never repeats. Input is ignored while the player is typing in the
        /// menu search field or while the menu is waiting for a new bind.
        /// </summary>
        internal static void UpdateInput()
        {
            EnsureLoaded();
            FlushSettings();
            if (All.Count == 0 || Keyboard.current == null) return;

            CascadeMenuBehaviour menu = CascadeMenuBehaviour.Instance;
            if (menu != null && (menu.IsTyping || menu.IsRebinding)) return;

            DoubleKeys.Clear();
            PressedKeys.Clear();

            // Keys claimed by an enabled Double-press mod suppress single-press
            // mods that share the same key, so one press can only mean one thing.
            for (int i = 0; i < All.Count; i++)
            {
                Mod mod = All[i];
                if (!mod.Enabled || mod.Key == Key.None) continue;
                if (mod.PressType == ModPressType.Double) DoubleKeys.Add(mod.Key);
            }

            for (int i = 0; i < All.Count; i++)
            {
                Mod mod = All[i];
                if (!mod.Enabled || mod.Key == Key.None) continue;

                Key key = mod.Key;
                if (PressedKeys.Contains(key)) continue;
                if (!Keybinds.Pressed(key)) continue;

                PressedKeys.Add(key);
                HandleKeyDown(key);
            }
        }

        private static void HandleKeyDown(Key key)
        {
            float now = Time.unscaledTime;

            if (DoubleKeys.Contains(key))
            {
                float last;
                bool isDouble = LastDoublePress.TryGetValue(key, out last) &&
                                now - last <= CurrentDoublePressWindow();

                if (!isDouble)
                {
                    // First tap: remember it, but do nothing yet.
                    LastDoublePress[key] = now;
                    return;
                }

                // Consume the pair so a third tap starts a new sequence.
                LastDoublePress[key] = -1000f;

                for (int i = 0; i < All.Count; i++)
                {
                    Mod mod = All[i];
                    if (mod.Enabled && mod.Key == key && mod.PressType == ModPressType.Double)
                    {
                        ActivateFromBind(mod);
                    }
                }
                return;
            }

            for (int i = 0; i < All.Count; i++)
            {
                Mod mod = All[i];
                if (mod.Enabled && mod.Key == key && mod.PressType == ModPressType.Single)
                {
                    ActivateFromBind(mod);
                }
            }
        }

        private static float CurrentDoublePressWindow()
        {
            return _config != null ? _config.DoublePressWindow.Value : FallbackDoublePressWindow;
        }

        private static void ActivateFromBind(Mod mod)
        {
            try
            {
                mod.ToggleActive();
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogError("Cascade mod \"" + mod.Name + "\" keybind failed: " + exception);
            }
        }

        /// <summary>Called by <see cref="ModHost"/> every frame.</summary>
        internal static void Tick(bool late)
        {
            for (int i = 0; i < All.Count; i++)
            {
                Mod mod = All[i];
                if (!mod.Enabled) continue;

                // A toggle mod runs only while its effect is Active. One-shot
                // mods keep their per-frame hooks available once armed.
                if (mod.isToggle && !mod.IsActive) continue;

                try
                {
                    if (late) mod.LateUpdate();
                    else mod.Update();

                    // Success: reset failure tracking
                    mod._failureCount = 0;
                    mod._firstFailureTime = 0f;
                }
                catch (Exception exception)
                {
                    float now = UnityEngine.Time.time;
                    const float GracePeriod = 2f;

                    // Track first failure
                    if (mod._failureCount == 0)
                    {
                        mod._firstFailureTime = now;
                        Plugin.Log?.LogWarning($"Cascade mod \"{mod.Name}\" threw: {exception}");
                    }

                    mod._failureCount++;

                    // Check if grace period expired
                    if (now - mod._firstFailureTime >= GracePeriod)
                    {
                        Plugin.Log?.LogError($"Cascade mod \"{mod.Name}\" failed {mod._failureCount} times, disabling: {exception}");
                        
                        try 
                        { 
                            mod.SetEnabled(false); 
                        }
                        catch (Exception disableEx)
                        {
                            Plugin.Log?.LogError($"Failed to disable mod \"{mod.Name}\": {disableEx}");
                        }

                        try
                        {
                            ModKit.Notify(mod.Name, "Turned off after repeated errors. See the BepInEx log.");
                        }
                        catch (Exception notifyEx)
                        {
                            Plugin.Log?.LogError($"Failed to notify about mod \"{mod.Name}\" disable: {notifyEx}");
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Drives enabled mods. Lives on the plugin root so it survives map loads.
    /// </summary>
    internal sealed class ModHost : MonoBehaviour
    {
        private void Update()
        {
            ModRegistry.UpdateInput();
            ModRegistry.Tick(false);
            GameApi.VerifyWhenReady();
        }

        private void LateUpdate()
        {
            ModRegistry.Tick(true);
        }
    }
}

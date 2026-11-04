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
    /// keybind, a Single/Double press selector and an Active indicator. No
    /// registration call and no per-mod keybind code is needed.
    ///
    /// Categories come from the namespace: CascadeMenu.Mods.Halloween -> "Halloween",
    /// CascadeMenu.Mods.Super -> "Super", CascadeMenu.Mods -> "General".
    /// </summary>
    internal static class ModRegistry
    {
        private const string BindSection = "Binds";
        private const float FallbackDoublePressWindow = 0.3f;

        private static readonly List<Mod> All = new List<Mod>();
        private static readonly List<string> CategoryList = new List<string>();

        // Persistence for the generated per-mod rows.
        private static readonly Dictionary<Mod, ConfigEntry<string>> KeyEntries =
            new Dictionary<Mod, ConfigEntry<string>>();
        private static readonly Dictionary<Mod, ConfigEntry<string>> PressEntries =
            new Dictionary<Mod, ConfigEntry<string>>();

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

        private static string PressName(ModPressType type)
        {
            return type == ModPressType.Double ? "double press" : "single press";
        }

        /// <summary>Page name for a mod, derived from its namespace.</summary>
        internal static string CategoryOf(Mod mod)
        {
            string space = mod.GetType().Namespace ?? string.Empty;
            const string prefix = "CascadeMenu.Mods";
            if (space.Length <= prefix.Length) return "General";
            if (!space.StartsWith(prefix, StringComparison.Ordinal)) return "General";

            string rest = space.Substring(prefix.Length).TrimStart('.');
            return string.IsNullOrEmpty(rest) ? "General" : rest;
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

        /// <summary>Tag of the single page listing every discovered mod.</summary>
        internal const string AllModsTag = "cascade.mods";

        /// <summary>
        /// Builds one page with a section per module, instead of a sidebar tab per
        /// namespace. Keeps the navigation rail short and predictable.
        /// </summary>
        internal static CascadePage BuildAllPage(CascadeMenuBehaviour menu)
        {
            EnsureLoaded();

            var page = new CascadePage("Mods", "Enable to arm a mod, then press its key or Start", CascadeIcon.Layers);
            page.Tag = AllModsTag;

            page.Section("How this works",
                    "Enabled only arms the keybind. Active is the effect actually running.")
                .Label("Enabled", "Menu toggle; the keybind is ignored while off", "Toggle")
                .Label("Active", "Changed by the keybind or the Start/Stop button", "Dot")
                .Label("Keybind", "Press Escape while rebinding to clear it", "Bind");

            for (int c = 0; c < CategoryList.Count; c++)
            {
                string category = CategoryList[c];
                int count = CountIn(category);
                CascadeSection section = page.Section(
                    category,
                    count + (count == 1 ? " mod in this module" : " mods in this module"));

                for (int i = 0; i < All.Count; i++)
                {
                    Mod mod = All[i];
                    if (CategoryOf(mod) != category) continue;

                    Mod captured = mod;
                    section.Mod(
                        captured.Name,
                        captured.isToggle ? "Toggle mod" : "One-shot action",
                        () => captured.Enabled,
                        value => SetEnabled(captured, value),
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
                        captured.isToggle);
                }
            }

            if (page.Sections.Count == 0)
            {
                page.Section("Empty").Label("Nothing here", "No mods were discovered", "-");
            }

            page.Section("Module", "Applies to every mod in the registry")
                .Button("Disable all", "Disarm every mod and stop its effect", "Disable",
                    DisableAll, false)
                .Label("Enabled", "Armed, so the keybind works", EnabledCount + " / " + All.Count)
                .Label("Active", "Effects currently running", ActiveCount + " / " + All.Count);

            return page;
        }

        /// <summary>Rebind target used by the menu for a mod's key row.</summary>
        private static string ModTarget(Mod mod)
        {
            return "mod:" + mod.GetType().Name;
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
                }
                catch (Exception exception)
                {
                    // One broken mod should not spam the log or the frame loop.
                    Plugin.Log?.LogError("Cascade mod \"" + mod.Name + "\" threw, disabling: " + exception);
                    try { mod.SetEnabled(false); } catch { }
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
        }

        private void LateUpdate()
        {
            ModRegistry.Tick(true);
        }
    }
}

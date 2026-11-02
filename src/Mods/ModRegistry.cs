using System;
using System.Collections.Generic;
using System.Reflection;
using CascadeMenu.UI;
using UnityEngine;

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
    ///       public override void Update() { /* runs while enabled */ }
    ///   }
    ///
    /// ...and it appears on its namespace's page. No registration call needed.
    ///
    /// Categories come from the namespace: CascadeMenu.Mods.Halloween -> "Halloween",
    /// CascadeMenu.Mods.Super -> "Super", CascadeMenu.Mods -> "General".
    /// </summary>
    internal static class ModRegistry
    {
        private static readonly List<Mod> All = new List<Mod>();
        private static readonly List<string> CategoryList = new List<string>();
        private static bool _loaded;

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

        /// <summary>Tag of the single page listing every discovered mod.</summary>
        internal const string AllModsTag = "cascade.mods";

        /// <summary>
        /// Builds one page with a section per module, instead of a sidebar tab per
        /// namespace. Keeps the navigation rail short and predictable.
        /// </summary>
        internal static CascadePage BuildAllPage()
        {
            EnsureLoaded();

            var page = new CascadePage("Mods", "Every discovered mod, grouped by module", CascadeIcon.Layers);
            page.Tag = AllModsTag;

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
                    if (mod.isToggle)
                    {
                        section.Toggle(captured.Name, "Runs while switched on",
                            () => captured.Enabled,
                            value => SetEnabled(captured, value));
                    }
                    else
                    {
                        section.Button(captured.Name, "Runs immediately", "Run",
                            () => Run(captured), false);
                    }
                }
            }

            if (page.Sections.Count == 0)
            {
                page.Section("Empty").Label("Nothing here", "No mods were discovered", "-");
            }

            page.Section("Module", "Applies to every mod in the registry")
                .Button("Disable all", "Turn off every running mod in Cascade", "Disable",
                    DisableAll, false)
                .Label("Enabled", "Currently running", EnabledCount + " / " + All.Count);

            return page;
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
        {            EnsureLoaded();
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

        /// <summary>Called by <see cref="ModHost"/> every frame.</summary>
        internal static void Tick(bool late)
        {
            for (int i = 0; i < All.Count; i++)
            {
                Mod mod = All[i];
                if (!mod.Enabled) continue;

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

        private static CascadeIcon IconOf(string category)
        {
            switch (category)
            {
                case "Halloween": return CascadeIcon.Sparkle;
                case "Super": return CascadeIcon.Layers;
                case "Visual": return CascadeIcon.Eye;
                default: return CascadeIcon.Sliders;
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
            ModRegistry.Tick(false);
        }

        private void LateUpdate()
        {
            ModRegistry.Tick(true);
        }
    }
}

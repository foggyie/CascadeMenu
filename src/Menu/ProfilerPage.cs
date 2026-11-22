using CascadeMenu.UI;
using UnityEngine;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Overlays;
using CascadeMenu.Mods.Cosmetics;
using CascadeMenu.Mods.Desktop;
using CascadeMenu.Mods.Flight;
using CascadeMenu.Mods.Fun;
using CascadeMenu.Mods.Game;
using CascadeMenu.Mods.Movement;
using CascadeMenu.Mods.Performance;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Snake;
using CascadeMenu.Mods.Tools;
using CascadeMenu.Mods.Visual;

namespace CascadeMenu.Menu
{
    /// <summary>
    /// Live frame-cost view for the features that do the most per-frame work.
    /// The page polls <see cref="ProfilerStats"/> through live labels, so the
    /// numbers update while it is open without rebuilding the window.
    /// </summary>
    internal static class ProfilerPage
    {
        internal const string Tag = "cascade.profiler";

        internal static CascadePage Build(CascadeMenuBehaviour menu, MenuConfig config)
        {
            var page = new CascadePage("Profiler", "Per-frame cost of the heavy features", CascadeIcon.Sliders);
            page.Tag = Tag;

            // Make sure every entry exists before the first frame it runs.
            ProfilerStats.Register(ProfilerStats.PlayerHudKey, "Player HUD",
                "Pooled per-player world-space cards");
            ProfilerStats.Register(ProfilerStats.NameTagsKey, "Name Tags",
                "Pooled name labels above every remote rig");
            ProfilerStats.Register(ProfilerStats.GunLibKey, "GunLib",
                "Aim ray, visibility-filtered raycast, beam and ring");
            ProfilerStats.Register(ProfilerStats.ConstructionKey, "Construction",
                "Aim ray, preview, weld bookkeeping and demolition");

            page.Section("Frame cost", "A few tenths of a millisecond per active feature is normal")
                .LiveLabel("Player HUD",
                    "Roster refresh, billboards, distance fade and cached text",
                    () => ProfilerStats.Describe(ProfilerStats.PlayerHudKey),
                    () => ProfilerStats.Get(ProfilerStats.PlayerHudKey).Active)
                .LiveLabel("Name Tags",
                    "Pooled name labels, billboards and distance fade",
                    () => ProfilerStats.Describe(ProfilerStats.NameTagsKey),
                    () => ProfilerStats.Get(ProfilerStats.NameTagsKey).Active)
                .LiveLabel("GunLib",
                    "Grip check, aim ray, RaycastNonAlloc, beam and ring",
                    () => ProfilerStats.Describe(ProfilerStats.GunLibKey),
                    () => ProfilerStats.Get(ProfilerStats.GunLibKey).Active)
                .LiveLabel("Construction",
                    "Aim, preview, piece and weld bookkeeping, blast queries",
                    () => ProfilerStats.Describe(ProfilerStats.ConstructionKey),
                    () => ProfilerStats.Get(ProfilerStats.ConstructionKey).Active);

            page.Section("Rules", "What disabled or idle features cost")
                .Label("Disabled mods", "No Update, no objects, no strings", "0 ms")
                .Label("HUD when off", "Every pooled card is destroyed", "0 objects")
                .Label("Gun when grip is up", "Returns before the aim ray and raycast", "0 casts")
                .Label("Construction when idle", "No grip means no raycast, preview or tool action", "0 casts")
                .Label("Allocations", "No LINQ, no FindObjectsOfType, no per-frame lists", "reused buffers");

            page.Section("Actions", "Reset the peak or copy the report to the log")
                .Button("Reset peaks", "Clear every average and peak", "Reset",
                    () =>
                    {
                        ProfilerStats.ResetPeaks();
                        if (menu != null) menu.ShowToast("Profiler", "Averages and peaks cleared.");
                    }, false)
                .Button("Write to log", "Print one line per tracked feature", "Log",
                    ProfilerStats.LogAll, false)
                .Label("Tip", "Open this tab, then close the menu to see idle costs", "live");

            return page;
        }
    }
}

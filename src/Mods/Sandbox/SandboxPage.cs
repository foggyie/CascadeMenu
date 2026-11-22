using CascadeMenu.Menu;
using CascadeMenu.UI;
using UnityEngine;

namespace CascadeMenu.Mods.Sandbox
{
    /// <summary>
    /// All-in-one Cascade UI tab ("PropForge") for Construction blocks,
    /// Sandbox physics props, quick builds, solid climbable colliders, and tools.
    /// </summary>
    internal static class SandboxPage
    {
        private static readonly string[] ToolLabels =
        {
            "None (Hands Only)",
            "Physics Gun (Grab & Freeze)",
            "Construction Placer (Snap & Build)",
            "Thruster Tool",
            "Balloon Tool",
            "Weld Tool",
            "Remover Tool"
        };

        private static readonly string[] BuildBlockLabels =
        {
            "Wall Block",
            "Floor Slab",
            "Pillar",
            "Stairs",
            "Bridge Plank"
        };

        private static readonly SandboxPropType[] BuildBlockTypes =
        {
            SandboxPropType.WallBlock,
            SandboxPropType.FloorSlab,
            SandboxPropType.Pillar,
            SandboxPropType.Stairs,
            SandboxPropType.BridgePlank
        };

        private static readonly string[] ColorLabels =
        {
            "Default",
            "Neon Cyan",
            "Hot Pink",
            "Sunset Gold",
            "Mint Green",
            "Random"
        };

        private static readonly string[] SpawnOriginLabels =
        {
            "In Front of View",
            "At Right Hand"
        };

        internal static CascadePage Build(CascadeMenuBehaviour menu)
        {
            SandboxManager.Ensure();

            var page = new CascadePage("PropForge", "Construction blocks, sandbox props & physics tools", CascadeIcon.Cube);
            page.Tag = "cascade.propforge";

            page.Section("Construction Blocks", "Climbable Gorilla-collider structures (spawn frozen for building)")
                .Button("Wall Block", "Solid climbable vertical wall panel", "Build", () =>
                {
                    SandboxManager.SpawnProp(SandboxPropType.WallBlock);
                    menu.ShowToast("PropForge", "Built Wall Block.");
                }, true)
                .Button("Floor Slab", "Solid flat walkable platform slab", "Build", () =>
                {
                    SandboxManager.SpawnProp(SandboxPropType.FloorSlab);
                    menu.ShowToast("PropForge", "Built Floor Slab.");
                }, false)
                .Button("Pillar", "Tall climbable column", "Build", () =>
                {
                    SandboxManager.SpawnProp(SandboxPropType.Pillar);
                    menu.ShowToast("PropForge", "Built Pillar.");
                }, false)
                .Button("Stairs / Slope", "Angled climbing ramp", "Build", () =>
                {
                    SandboxManager.SpawnProp(SandboxPropType.Stairs);
                    menu.ShowToast("PropForge", "Built Stairs.");
                }, false)
                .Button("Bridge Plank", "Long solid bridge beam", "Build", () =>
                {
                    SandboxManager.SpawnProp(SandboxPropType.BridgePlank);
                    menu.ShowToast("PropForge", "Built Bridge Plank.");
                }, false);

            page.Section("Sandbox Objects", "Interactive physics props with Gorilla Tag hand collision")
                .Button("Crate", "Wooden physics cube", "Spawn", () =>
                {
                    SandboxManager.SpawnProp(SandboxPropType.Crate);
                    menu.ShowToast("PropForge", "Spawned Crate.");
                }, false)
                .Button("Barrel", "Heavy metal physics barrel", "Spawn", () =>
                {
                    SandboxManager.SpawnProp(SandboxPropType.Barrel);
                    menu.ShowToast("PropForge", "Spawned Barrel.");
                }, false)
                .Button("Bouncy Ball", "High-bounce physics sphere", "Spawn", () =>
                {
                    SandboxManager.SpawnProp(SandboxPropType.BouncyBall);
                    menu.ShowToast("PropForge", "Spawned Bouncy Ball.");
                }, false)
                .Button("Launch Ramp", "Angled jump ramp", "Spawn", () =>
                {
                    SandboxManager.SpawnProp(SandboxPropType.Ramp);
                    menu.ShowToast("PropForge", "Spawned Launch Ramp.");
                }, false)
                .Button("Trampoline", "Green bounce pad that launches your Gorilla upward", "Spawn", () =>
                {
                    SandboxManager.SpawnProp(SandboxPropType.Trampoline);
                    menu.ShowToast("PropForge", "Spawned Trampoline.");
                }, false)
                .Button("Explosive Barrel", "Detonates on hard impact or via Detonate button", "Spawn", () =>
                {
                    SandboxManager.SpawnProp(SandboxPropType.ExplosiveBarrel);
                    menu.ShowToast("PropForge", "Spawned Explosive Barrel.");
                }, false)
                .Button("Ragdoll Dummy", "Multi-limb articulated physics ragdoll", "Spawn", () =>
                {
                    SandboxManager.SpawnProp(SandboxPropType.Ragdoll);
                    menu.ShowToast("PropForge", "Spawned Ragdoll Dummy.");
                }, false)
                .Button("Hover Balloon", "Buoyant floating balloon", "Spawn", () =>
                {
                    SandboxManager.SpawnProp(SandboxPropType.HoverBalloon);
                    menu.ShowToast("PropForge", "Spawned Hover Balloon.");
                }, false);

            page.Section("Quick Build Presets", "One-tap multi-block structures")
                .Button("Construction Fort", "Walkable floor with 3 climbable walls", "Build", () =>
                {
                    SandboxManager.SpawnPreset(3);
                    menu.ShowToast("PropForge", "Built Construction Fort.");
                }, true)
                .Button("Crate Tower", "4-high stack of physics crates", "Build", () =>
                {
                    SandboxManager.SpawnPreset(0);
                    menu.ShowToast("PropForge", "Built Crate Tower.");
                }, false)
                .Button("Ramp + Bounce Pad", "Launch ramp lined up with a trampoline", "Build", () =>
                {
                    SandboxManager.SpawnPreset(1);
                    menu.ShowToast("PropForge", "Built Ramp + Bounce Pad.");
                }, false)
                .Button("Bounce Ring", "Trampoline surrounded by 3 high-bounce balls", "Build", () =>
                {
                    SandboxManager.SpawnPreset(2);
                    menu.ShowToast("PropForge", "Built Bounce Ring.");
                }, false);

            page.Section("Tools", "Right Trigger (VR) or Right Click (PC) to use active tool")
                .Dropdown("Active tool", "Physics Gun: Right Trigger to grab, Primary/F to freeze",
                    ToolLabels,
                    () => (int)SandboxManager.ActiveTool,
                    idx =>
                    {
                        SandboxManager.ActiveTool = (SandboxToolMode)Mathf.Clamp(idx, 0, ToolLabels.Length - 1);
                        menu.ShowToast("PropForge Tool", ToolLabels[(int)SandboxManager.ActiveTool]);
                    })
                .Dropdown("Construction block", "Piece placed when Construction Placer tool is active",
                    BuildBlockLabels,
                    () =>
                    {
                        int i = System.Array.IndexOf(BuildBlockTypes, SandboxManager.SelectedBuildBlock);
                        return i >= 0 ? i : 0;
                    },
                    idx => SandboxManager.SelectedBuildBlock = BuildBlockTypes[Mathf.Clamp(idx, 0, BuildBlockTypes.Length - 1)])
                .Toggle("Snap to grid", "Align Construction Placer blocks neatly to a 0.25m grid & 45° angles",
                    () => SandboxManager.SnapToGrid,
                    v => SandboxManager.SnapToGrid = v);

            page.Section("Colliders, Style & Cleanup", "Solid Gorilla colliders, styling, and bulk actions")
                .LiveLabel("Active objects", "Currently spawned PropForge objects in the world",
                    () => SandboxManager.PropCount + " / 60 objects",
                    () => SandboxManager.PropCount > 0)
                .Toggle("Solid Gorilla colliders", "Put props on Gorilla Tag's climbable locomotion layer (off = pass-through)",
                    () => SandboxManager.SolidColliders,
                    v => SandboxManager.SolidColliders = v)
                .Dropdown("Prop color", "Color theme applied to newly spawned props",
                    ColorLabels,
                    () => SandboxManager.ColorPreset,
                    idx => SandboxManager.ColorPreset = Mathf.Clamp(idx, 0, ColorLabels.Length - 1))
                .Dropdown("Spawn origin", "Choose whether props appear in front of your view or at your right hand",
                    SpawnOriginLabels,
                    () => SandboxManager.SpawnAtRightHand ? 1 : 0,
                    idx => SandboxManager.SpawnAtRightHand = idx == 1)
                .Slider("Prop scale", "Size multiplier applied to newly spawned props",
                    0.25f, 3.0f,
                    () => SandboxManager.PropScale,
                    v => SandboxManager.PropScale = v,
                    v => v.ToString("0.00") + "x")
                .Slider("Bounciness", "Extra restitution applied to spawned props",
                    0.0f, 1.0f,
                    () => SandboxManager.CustomBounciness,
                    v => SandboxManager.CustomBounciness = v,
                    v => Mathf.RoundToInt(v * 100f) + "%")
                .Toggle("Spawn sandbox props frozen", "Sandbox props start frozen in mid-air (Construction always starts frozen)",
                    () => SandboxManager.SpawnFrozen,
                    v => SandboxManager.SpawnFrozen = v)
                .Button("Unfreeze all", "Release all frozen props back to physics", "Unfreeze", () =>
                {
                    SandboxManager.UnfreezeAll();
                    menu.ShowToast("PropForge", "All objects unfrozen.");
                }, false)
                .Button("Detonate explosives", "Trigger every spawned explosive barrel", "Detonate", () =>
                {
                    SandboxManager.DetonateAllExplosives();
                    menu.ShowToast("PropForge", "Detonated all explosives!");
                }, false)
                .Button("Clear all objects", "Delete every spawned PropForge object", "Clear All", () =>
                {
                    SandboxManager.ClearAll();
                    menu.ShowToast("PropForge", "Cleared all objects.");
                }, false);

            return page;
        }
    }
}

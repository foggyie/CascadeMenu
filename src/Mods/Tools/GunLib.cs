using System;
using UnityEngine;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Compatibility adapter for mods expecting Resurgence's GunLib API.
    /// Bridges all gun raycasting, visual beams, and trigger actions to Cascade's GunLibController.
    /// </summary>
    public static class GunLib
    {
        public static void Update(Action<RaycastHit, VRRig, NetPlayer> onFire, float cooldown = 0.25f)
        {
            GunLibController.SetActiveGun(onFire, cooldown);
        }

        public static void Cleanup()
        {
            GunLibController.ClearActiveGun();
        }

        public static void CycleGunColor()
        {
            GunLibController.CycleColor();
        }

        public static void ToggleSmoothness()
        {
            // Cascade's direct LineRenderer is already smooth and performant
        }
    }
}


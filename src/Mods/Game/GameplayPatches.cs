using GorillaLocomotion;
using GorillaTag.Gravity;
using HarmonyLib;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Stops <see cref="GTPlayer.TakeMyHand_ProcessMovement"/> from moving the local
    /// player while another player has grabbed a hand link.
    /// </summary>
    [HarmonyPatch(typeof(GTPlayer), "TakeMyHand_ProcessMovement")]
    internal static class HandLinkBlockPatch
    {
        internal static bool Enabled;

        private static bool Prefix()
        {
            return !Enabled;
        }
    }

    /// <summary>
    /// Gorilla Tag applies its gravity through MonkeGravityController components.
    /// Skip that force for the local player while joystick fly is active so the
    /// player floats instead of slowly being pulled out of the air.
    /// </summary>
    [HarmonyPatch(typeof(MonkeGravityController), "ApplyGravityForce")]
    internal static class FlyGravityPatch
    {
        private static bool Prefix(MonkeGravityController __instance)
        {
            if (!JoystickFlyController.IsFlying)
            {
                return true;
            }

            GTPlayer player = GTPlayer.Instance;
            return player == null ||
                   player.playerRigidBody == null ||
                   __instance.TargetRigidBody != player.playerRigidBody;
        }
    }
}

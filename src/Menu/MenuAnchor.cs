using UnityEngine;

namespace CascadeMenu
{
    /// <summary>
    /// Works out where the menu should sit in the world.
    ///
    /// Rules (matching the requested behaviour):
    /// - Spawn in world space in front of the head, centred on the view.
    /// - Ignore head roll: the pose is built from a flattened forward vector, so
    ///   tilting the head sideways never tilts the menu.
    /// - Keep a fixed height. The menu does not bob with the head; it only spawns
    ///   higher once the head passes the configured threshold.
    /// - After spawning it is a static world object. It is only re-spawned when
    ///   the player walks further away than the configured distance.
    ///
    /// All the maths lives here so <see cref="CascadeMenuBehaviour"/> stays about
    /// input and rebuilding.
    /// </summary>
    internal static class MenuAnchor
    {
        /// <summary>Compute a level, roll-free pose in front of the view camera.</summary>
        internal static void GetPose(MenuConfig config, Camera camera, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (camera == null || config == null) return;

            Transform head = camera.transform;

            // Flatten the view direction so pitch/roll cannot rotate the menu.
            Vector3 flatForward = new Vector3(head.forward.x, 0f, head.forward.z);
            if (flatForward.sqrMagnitude < 0.0001f)
            {
                // Looking straight up or down: fall back to the camera yaw.
                flatForward = Quaternion.Euler(0f, head.eulerAngles.y, 0f) * Vector3.forward;
            }
            flatForward.Normalize();

            // LookRotation(forward, worldUp) is always upright -> no roll.
            rotation = Quaternion.LookRotation(flatForward, Vector3.up);

            float distance = Mathf.Max(0.3f, config.Distance.Value);
            float headHeight = head.position.y;
            float threshold = config.MenuRaiseAbove.Value;

            // Fixed height, except when the head is above the threshold: then the
            // menu is lifted by exactly how much the head exceeds it.
            float height = config.MenuHeight.Value;
            if (headHeight > threshold)
            {
                height += headHeight - threshold;
            }

            // Use the horizontal head position so leaning forward does not push
            // the menu further away.
            position = new Vector3(head.position.x, 0f, head.position.z) + flatForward * distance;
            position.y = height;
        }

        /// <summary>True when the menu is too far from the player to be usable.</summary>
        internal static bool NeedsReanchor(MenuConfig config, Vector3 menuPosition, Vector3 headPosition)
        {
            if (config == null) return false;

            float limit = config.MenuReanchorDistance.Value;
            if (limit <= 0.01f) return false;

            Vector3 a = new Vector3(menuPosition.x, 0f, menuPosition.z);
            Vector3 b = new Vector3(headPosition.x, 0f, headPosition.z);
            return Vector3.Distance(a, b) > limit;
        }
    }
}

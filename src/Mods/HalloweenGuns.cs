using GorillaLocomotion;
using UnityEngine;

namespace CascadeMenu.Mods.Halloween
{
    /// <summary>
    /// Aim at a player and shoot to command the Lurker Ghost to hunt and attack them (Master Client required).
    /// </summary>
    [PluginPreferenced]
    [PluginToggleable]
    public class LurkerAttackGun : Mod
    {
        public LurkerAttackGun()
        {
            Name = "Lurker Attack (Gun)";
            isToggle = true;
        }

        public override void Update()
        {
            GunLib.Update((hit, rig, player) =>
            {
                if (!HalloweenHelper.IsMasterClient)
                {
                    HalloweenHelper.Log.LogWarning("[Halloween] Lurker Attack Gun: Master Client required.");
                    return;
                }

                if (player != null)
                {
                    HalloweenHelper.Log.LogInfo($"[Halloween] Lurker Gun targeting player: {player.NickName}");
                    HalloweenHelper.LurkerAttack(player);
                }
                else
                {
                    HalloweenHelper.Log.LogWarning("[Halloween] Lurker Gun: No player under crosshair. Aim directly at a player.");
                }
            }, cooldown: 0.5f);
        }

        public override void OnDisable()
        {
            GunLib.Cleanup();
        }
    }

    /// <summary>
    /// Aim anywhere and shoot to teleport the Wandering Ghost to that spot (Master Client required).
    /// </summary>
    [PluginPreferenced]
    [PluginToggleable]
    public class SpawnGhostGun : Mod
    {
        public SpawnGhostGun()
        {
            Name = "Spawn Ghost (Gun)";
            isToggle = true;
        }

        public override void Update()
        {
            GunLib.Update((hit, rig, player) =>
            {
                if (!HalloweenHelper.IsMasterClient)
                {
                    HalloweenHelper.Log.LogWarning("[Halloween] Spawn Ghost Gun: Master Client required.");
                    return;
                }

                if (hit.collider != null)
                {
                    Vector3 targetPos = hit.point + hit.normal * 0.3f;
                    HalloweenHelper.Log.LogInfo($"[Halloween] Teleporting Wandering Ghost to {targetPos}");
                    HalloweenHelper.SpawnWanderingGhost(targetPos);
                }
            }, cooldown: 0.4f);
        }

        public override void OnDisable()
        {
            GunLib.Cleanup();
        }
    }

    /// <summary>
    /// Aim at any player and shoot to make the Wandering Ghost orbit around THEM (Master Client required).
    /// </summary>
    [PluginPreferenced]
    [PluginToggleable]
    [DefaultExecutionOrder(32000)]
    public class OrbitGhostGun : Mod
    {
        private VRRig _targetRig;
        private float _angle;
        public float radius = 2.5f;
        public float speed = 90f;
        public float height = 0.2f;

        public OrbitGhostGun()
        {
            Name = "Orbit Ghost (Gun)";
            isToggle = true;
        }

        public override void Update()
        {
            GunLib.Update((hit, rig, player) =>
            {
                if (rig != null)
                {
                    _targetRig = rig;
                    HalloweenHelper.Log.LogInfo($"[Halloween] Ghost now orbiting player: {player?.NickName ?? "Target"}");
                }
            }, cooldown: 0.3f);
        }

        public override void LateUpdate()
        {
            if (!HalloweenHelper.IsMasterClient) return;
            var ghost = HalloweenHelper.GetWanderingGhost();
            if (ghost == null) return;

            Transform centerTransform = _targetRig != null ? _targetRig.transform : null;
            if (centerTransform == null) return;

            GhostHold.Hold(ghost);
            _angle += speed * Time.deltaTime;
            float rad = _angle * Mathf.Deg2Rad;
            Vector3 center = centerTransform.position + Vector3.up * 0.5f;
            Vector3 pos = center + new Vector3(Mathf.Cos(rad) * radius, height, Mathf.Sin(rad) * radius);

            ghost.transform.position = pos;
            Vector3 tangent = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            ghost.transform.rotation = Quaternion.LookRotation(tangent);
        }

        public override void OnDisable()
        {
            GunLib.Cleanup();
            _targetRig = null;
            if (HalloweenHelper.IsMasterClient)
            {
                var ghost = HalloweenHelper.GetWanderingGhost();
                if (ghost != null)
                {
                    GhostHold.Release(ghost);
                    HalloweenHelper.ChangeWanderingGhostState(ghost, "patrol");
                }
            }
        }
    }

    /// <summary>
    /// Aim and shoot to send the Wandering Ghost back to patrol mode (Master Client required).
    /// </summary>
    [PluginPreferenced]
    [PluginToggleable]
    public class SendGhostToPatrolGun : Mod
    {
        public SendGhostToPatrolGun()
        {
            Name = "Send Ghost To Patrol (Gun)";
            isToggle = true;
        }

        public override void Update()
        {
            GunLib.Update((hit, rig, player) =>
            {
                if (!HalloweenHelper.IsMasterClient) return;
                var ghost = HalloweenHelper.GetWanderingGhost();
                if (ghost != null)
                {
                    GhostHold.Release(ghost);
                    HalloweenHelper.ChangeWanderingGhostState(ghost, "patrol");
                    HalloweenHelper.Log.LogInfo("[Halloween] Wandering Ghost sent back to patrol.");
                }
            }, cooldown: 0.5f);
        }

        public override void OnDisable()
        {
            GunLib.Cleanup();
        }
    }

    /// <summary>
    /// Aim anywhere and shoot to reveal and hold the Wandering Ghost visible to all players at that spot (Master Client required).
    /// </summary>
    [PluginPreferenced]
    [PluginToggleable]
    [DefaultExecutionOrder(32000)]
    public class ShowGhostGun : Mod
    {
        public ShowGhostGun()
        {
            Name = "Show Ghost (Gun)";
            isToggle = true;
        }

        public override void Update()
        {
            GunLib.Update((hit, rig, player) =>
            {
                if (!HalloweenHelper.IsMasterClient) return;
                var ghost = HalloweenHelper.GetWanderingGhost();
                if (ghost != null && hit.collider != null)
                {
                    ghost.transform.position = hit.point + Vector3.up * 0.3f;
                    GhostHold.Hold(ghost);
                    HalloweenHelper.Log.LogInfo("[Halloween] Ghost shown and held at target position.");
                }
            }, cooldown: 0.4f);
        }

        public override void LateUpdate()
        {
            if (HalloweenHelper.IsMasterClient)
            {
                GhostHold.Hold(HalloweenHelper.GetWanderingGhost());
            }
        }

        public override void OnDisable()
        {
            GunLib.Cleanup();
            if (HalloweenHelper.IsMasterClient)
            {
                var g = HalloweenHelper.GetWanderingGhost();
                if (g != null)
                {
                    GhostHold.Release(g);
                    HalloweenHelper.ChangeWanderingGhostState(g, "patrol");
                }
            }
        }
    }

    /// <summary>
    /// Aim anywhere and shoot to spawn a flower at the beam location (Master Client required).
    /// </summary>
    [PluginPreferenced]
    [PluginToggleable]
    public class SpawnFlowerGun : Mod
    {
        public SpawnFlowerGun()
        {
            Name = "Spawn Flower (Gun)";
            isToggle = true;
        }

        public override void Update()
        {
            GunLib.Update((hit, rig, player) =>
            {
                if (!HalloweenHelper.IsMasterClient)
                {
                    HalloweenHelper.Log.LogWarning("[Halloween] Spawn Flower Gun: Master Client required.");
                    return;
                }

                if (hit.collider != null)
                {
                    Vector3 spawnPos = hit.point + Vector3.up * 0.1f;
                    HalloweenHelper.Log.LogInfo($"[Halloween] Spawning flower at {spawnPos}");
                    HalloweenHelper.SpawnFlowers(spawnPos, spawnAll: false);
                }
            }, cooldown: 0.35f);
        }

        public override void OnDisable()
        {
            GunLib.Cleanup();
        }
    }

    /// <summary>
    /// Aim at a player and shoot to teleport all unheld flowers right to them (Master Client required).
    /// </summary>
    [PluginPreferenced]
    [PluginToggleable]
    public class GiveFlowersGun : Mod
    {
        public GiveFlowersGun()
        {
            Name = "Give Flowers (Gun)";
            isToggle = true;
        }

        public override void Update()
        {
            GunLib.Update((hit, rig, player) =>
            {
                if (!HalloweenHelper.IsMasterClient)
                {
                    HalloweenHelper.Log.LogWarning("[Halloween] Give Flowers Gun: Master Client required.");
                    return;
                }

                Vector3 targetLocation = (rig != null)
                    ? rig.transform.position + Vector3.up * 0.2f
                    : hit.point + Vector3.up * 0.1f;

                HalloweenHelper.SpawnFlowers(targetLocation, spawnAll: true);
                HalloweenHelper.Log.LogInfo($"[Halloween] Teleported flowers to target at {targetLocation}");
            }, cooldown: 0.4f);
        }

        public override void OnDisable()
        {
            GunLib.Cleanup();
        }
    }

    /// <summary>
    /// Aim at any surface or object and shoot to trigger spooky haunted object effects near that point.
    /// </summary>
    [PluginPreferenced]
    [PluginToggleable]
    public class HauntGun : Mod
    {
        public HauntGun()
        {
            Name = "Haunt (Gun)";
            isToggle = true;
        }

        public override void Update()
        {
            GunLib.Update((hit, rig, player) =>
            {
                if (hit.collider == null) return;

                var hauntedObjects = HalloweenHelper.GetHauntedObjects();
                int triggered = 0;

                foreach (var obj in hauntedObjects)
                {
                    if (obj != null && obj.gameObject.activeInHierarchy)
                    {
                        float distance = Vector3.Distance(obj.transform.position, hit.point);
                        if (distance <= 8f)
                        {
                            try
                            {
                                HalloweenHelper.TriggerHaunted(obj);
                                triggered++;
                            }
                            catch { }
                        }
                    }
                }

                HalloweenHelper.Log.LogInfo($"[Halloween] Haunt Gun triggered {triggered} object(s) near hit point.");
            }, cooldown: 0.3f);
        }

        public override void OnDisable()
        {
            GunLib.Cleanup();
        }
    }

    /// <summary>
    /// Aim anywhere and shoot to trigger ALL haunted objects across the entire map at once.
    /// </summary>
    [PluginPreferenced]
    [PluginToggleable]
    public class HauntAllGun : Mod
    {
        public HauntAllGun()
        {
            Name = "Haunt All (Gun)";
            isToggle = true;
        }

        public override void Update()
        {
            GunLib.Update((hit, rig, player) =>
            {
                HalloweenHelper.TriggerAllHauntedObjects();
            }, cooldown: 1.0f);
        }

        public override void OnDisable()
        {
            GunLib.Cleanup();
        }
    }

    /// <summary>
    /// Aim anywhere and shoot to teleport yourself to that location.
    /// </summary>
    [PluginPreferenced]
    [PluginToggleable]
    public class TeleportGun : Mod
    {
        public TeleportGun()
        {
            Name = "Teleport (Gun)";
            isToggle = true;
        }

        public override void Update()
        {
            GunLib.Update((hit, rig, player) =>
            {
                if (hit.collider != null)
                {
                    Vector3 targetPos = hit.point + Vector3.up * 0.35f;
                    PlayerActions.TeleportToPosition(targetPos);
                }
            }, cooldown: 0.25f);
        }

        public override void OnDisable()
        {
            GunLib.Cleanup();
        }
    }

    /// <summary>
    /// Cycle through gun beam and pointer colors using Cascade's theme accent palette.
    /// </summary>
    public class CycleGunColor : Mod
    {
        public CycleGunColor()
        {
            Name = "Cycle Gun Color";
            isToggle = false;
        }

        public override void Execute()
        {
            GunLib.CycleGunColor();
        }
    }

    /// <summary>
    /// Toggle tracer beam smoothness.
    /// </summary>
    public class ToggleGunSmoothness : Mod
    {
        public ToggleGunSmoothness()
        {
            Name = "Toggle Gun Smoothness";
            isToggle = false;
        }

        public override void Execute()
        {
            GunLib.ToggleSmoothness();
        }
    }
}


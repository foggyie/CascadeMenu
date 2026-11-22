using UnityEngine;
using System.Collections.Generic;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Overlays;
using CascadeMenu.Mods.Tools;
using CascadeMenu.Mods.Cosmetics;
using CascadeMenu.Mods.Visual;
using CascadeMenu.Mods.Movement;

namespace CascadeMenu.Mods.Player
{
    /// <summary>Teleport gun with configurable distance from target.</summary>
    [PluginToggleable]
    [ModCategory("Player")]
    public sealed class TeleportGunEnhanced : Mod
    {
        private float _cooldown = 0.5f;
        private float _distanceFromTarget = 0f;
        private bool _teleportNearPlayer = false;

        public TeleportGunEnhanced()
        {
            Name = "Teleport Gun Enhanced";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "cooldown",
                "Teleport Cooldown",
                "Seconds between teleports",
                0.1f,
                2f,
                () => _cooldown,
                value => _cooldown = value,
                value => value.ToString("0.0") + " s"));

            AddSetting(ModSetting.Slider(
                "offset",
                "Distance Offset",
                "How far from target to teleport (meters)",
                0f,
                20f,
                () => _distanceFromTarget,
                value => _distanceFromTarget = value,
                value => value.ToString("0.0") + " m"));

            AddSetting(ModSetting.Toggle(
                "nearPlayer",
                "Near Player Mode",
                "Teleport near players instead of exact hit point",
                () => _teleportNearPlayer,
                value => _teleportNearPlayer = value));
        }

        public override void Update()
        {
            GunLib.Update((hit, rig, player) =>
            {
                Vector3 targetPos;
                Vector3 hitNormal = hit.normal.sqrMagnitude > 0.01f ? hit.normal.normalized : Vector3.up;

                if (_teleportNearPlayer && rig != null)
                {
                    // Teleport near the player with safe vertical & horizontal clearance
                    Vector3 toPlayer = rig.transform.position - hit.point;
                    toPlayer.y = 0f;
                    if (toPlayer.sqrMagnitude < 0.01f) toPlayer = Vector3.forward;
                    float dist = Mathf.Max(1.1f, _distanceFromTarget);
                    targetPos = rig.transform.position - toPlayer.normalized * dist + Vector3.up * 0.45f;
                }
                else
                {
                    // Keep the player capsule cleanly outside walls and above floors
                    // so GTPlayer collision never blocks or snaps the teleport back.
                    float clearance = Mathf.Max(0.55f, _distanceFromTarget);
                    targetPos = hit.point + hitNormal * clearance;
                    if (hitNormal.y > 0.5f)
                    {
                        targetPos.y = Mathf.Max(targetPos.y, hit.point.y + 0.75f);
                    }
                    else
                    {
                        targetPos.y += 0.35f;
                    }
                }

                TeleportPlayer(targetPos);
            }, cooldown: _cooldown);
        }

        public override void OnDisable()
        {
            GunLib.Cleanup();
        }

        internal static void TeleportPlayer(Vector3 position)
        {
            if (PlayerActions.TeleportToPosition(position))
                return;

            if (GorillaTagger.Instance == null) return;

            Transform player = GorillaTagger.Instance.transform;
            player.position = position;

            if (GorillaTagger.Instance.offlineVRRig != null)
            {
                GorillaTagger.Instance.offlineVRRig.transform.position = position;
            }

            if (GorillaTagger.Instance.rigidbody != null)
            {
                GorillaTagger.Instance.rigidbody.linearVelocity = Vector3.zero;
            }
            Physics.SyncTransforms();
        }
    }

    /// <summary>Teleport to nearest player with one click.</summary>
    [PluginToggleable]
    [ModCategory("Player")]
    public sealed class TeleportToNearestMod : Mod
    {
        private float _offset = 2f;

        public TeleportToNearestMod()
        {
            Name = "Teleport To Nearest";
            isToggle = false;

            AddSetting(ModSetting.Slider(
                "offset",
                "Distance Offset",
                "How far from player to appear",
                0.5f,
                10f,
                () => _offset,
                value => _offset = value,
                value => value.ToString("0.0") + " m"));
        }

        public override void Execute()
        {
            Transform localPlayer = GorillaTagger.Instance?.transform;
            if (localPlayer == null)
            {
                ModKit.Notify("Teleport", "Player not found");
                return;
            }

            VRRig nearest = null;
            float nearestDist = float.MaxValue;

            var players = PlayerActions.GetPlayers();
            foreach (var player in players)
            {
                VRRig rig = PlayerActions.FindRigObject(player);
                if (rig == null) continue;

                float dist = Vector3.Distance(localPlayer.position, rig.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = rig;
                }
            }

            if (nearest != null)
            {
                Vector3 direction = (localPlayer.position - nearest.transform.position).normalized;
                if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
                Vector3 targetPos = nearest.transform.position + direction * _offset + Vector3.up * 0.45f;
                
                TeleportGunEnhanced.TeleportPlayer(targetPos);

                ModKit.Notify("Teleport", $"Teleported to nearest player ({nearestDist:0.0}m away)");
            }
            else
            {
                ModKit.Notify("Teleport", "No players found");
            }
        }
    }

    /// <summary>Teleport to random player.</summary>
    [PluginToggleable]
    [ModCategory("Player")]
    public sealed class TeleportToRandomMod : Mod
    {
        private float _offset = 2f;

        public TeleportToRandomMod()
        {
            Name = "Teleport To Random";
            isToggle = false;

            AddSetting(ModSetting.Slider(
                "offset",
                "Distance Offset",
                "How far from player to appear",
                0.5f,
                10f,
                () => _offset,
                value => _offset = value,
                value => value.ToString("0.0") + " m"));
        }

        public override void Execute()
        {
            Transform localPlayer = GorillaTagger.Instance?.transform;
            if (localPlayer == null)
            {
                ModKit.Notify("Teleport", "Player not found");
                return;
            }

            var players = PlayerActions.GetPlayers();
            if (players == null || players.Count == 0)
            {
                ModKit.Notify("Teleport", "No players found");
                return;
            }

            // Pick random player
            var randomPlayer = players[Random.Range(0, players.Count)];
            VRRig rig = PlayerActions.FindRigObject(randomPlayer);

            if (rig != null)
            {
                Vector3 randomOffset = Random.onUnitSphere * _offset;
                randomOffset.y = Mathf.Abs(randomOffset.y) + 0.4f; // Keep above ground
                Vector3 targetPos = rig.transform.position + randomOffset;

                TeleportGunEnhanced.TeleportPlayer(targetPos);

                string playerName = PlayerActions.SafeName(randomPlayer);
                ModKit.Notify("Teleport", $"Teleported to {playerName}");
            }
            else
            {
                ModKit.Notify("Teleport", "Failed to find player");
            }
        }
    }

    /// <summary>Cycle through players by teleporting to each one.</summary>
    [PluginToggleable]
    [ModCategory("Player")]
    public sealed class TeleportCycleMod : Mod
    {
        private int _currentIndex = 0;
        private float _offset = 2f;

        public TeleportCycleMod()
        {
            Name = "Cycle Through Players";
            isToggle = false;

            AddSetting(ModSetting.Slider(
                "offset",
                "Distance Offset",
                "How far from player to appear",
                0.5f,
                10f,
                () => _offset,
                value => _offset = value,
                value => value.ToString("0.0") + " m"));
        }

        public override void Execute()
        {
            Transform localPlayer = GorillaTagger.Instance?.transform;
            if (localPlayer == null)
            {
                ModKit.Notify("Teleport", "Player not found");
                return;
            }

            var players = PlayerActions.GetPlayers();
            if (players == null || players.Count == 0)
            {
                ModKit.Notify("Teleport", "No players found");
                return;
            }

            // Cycle to next player
            _currentIndex = (_currentIndex + 1) % players.Count;
            var targetPlayer = players[_currentIndex];
            VRRig rig = PlayerActions.FindRigObject(targetPlayer);

            if (rig != null)
            {
                Vector3 direction = (localPlayer.position - rig.transform.position).normalized;
                if (direction.magnitude < 0.1f) direction = Vector3.forward;
                
                Vector3 targetPos = rig.transform.position + direction * _offset + Vector3.up * 0.45f;

                TeleportGunEnhanced.TeleportPlayer(targetPos);

                string playerName = PlayerActions.SafeName(targetPlayer);
                ModKit.Notify("Teleport", $"→ {playerName} ({_currentIndex + 1}/{players.Count})");
            }
        }
    }

    /// <summary>Auto-follow nearest player by continuously teleporting.</summary>
    [PluginToggleable]
    [ModCategory("Player")]
    public sealed class FollowPlayerMod : Mod
    {
        private float _followDistance = 3f;
        private float _updateRate = 0.2f;
        private float _nextUpdate;
        private VRRig _targetRig;

        public FollowPlayerMod()
        {
            Name = "Follow Nearest Player";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "distance",
                "Follow Distance",
                "Stay this far from target",
                1f,
                10f,
                () => _followDistance,
                value => _followDistance = value,
                value => value.ToString("0.0") + " m"));

            AddSetting(ModSetting.Slider(
                "rate",
                "Update Rate",
                "How often to update position",
                0.05f,
                1f,
                () => _updateRate,
                value => _updateRate = value,
                value => value.ToString("0.00") + " s"));
        }

        public override void OnEnable()
        {
            _targetRig = null;
            ModKit.Notify("Follow", "Following nearest player");
        }

        public override void Update()
        {
            if (Time.time < _nextUpdate) return;
            _nextUpdate = Time.time + _updateRate;

            Transform localPlayer = GorillaTagger.Instance?.transform;
            if (localPlayer == null) return;

            // Find nearest player
            _targetRig = null;
            float nearestDist = float.MaxValue;

            var players = PlayerActions.GetPlayers();
            foreach (var player in players)
            {
                VRRig rig = PlayerActions.FindRigObject(player);
                if (rig == null) continue;

                float dist = Vector3.Distance(localPlayer.position, rig.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    _targetRig = rig;
                }
            }

            if (_targetRig != null)
            {
                Vector3 direction = (localPlayer.position - _targetRig.transform.position).normalized;
                if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
                Vector3 targetPos = _targetRig.transform.position + direction * _followDistance + Vector3.up * 0.4f;

                TeleportGunEnhanced.TeleportPlayer(targetPos);
            }
        }
    }
}

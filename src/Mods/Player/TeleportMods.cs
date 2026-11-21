using UnityEngine;
using System.Collections.Generic;

namespace CascadeMenu.Mods
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

                if (_teleportNearPlayer && rig != null)
                {
                    // Teleport near the player
                    Vector3 toPlayer = (rig.transform.position - hit.point).normalized;
                    targetPos = rig.transform.position - toPlayer * _distanceFromTarget;
                }
                else
                {
                    // Teleport to hit point with offset
                    targetPos = hit.point + hit.normal * _distanceFromTarget;
                }

                TeleportPlayer(targetPos);
            }, cooldown: _cooldown);
        }

        public override void OnDisable()
        {
            GunLib.Cleanup();
        }

        private void TeleportPlayer(Vector3 position)
        {
            if (GorillaTagger.Instance == null) return;

            Transform player = GorillaTagger.Instance.transform;
            player.position = position;

            if (GorillaTagger.Instance.offlineVRRig != null)
            {
                GorillaTagger.Instance.offlineVRRig.transform.position = position;
            }

            // Reset velocity
            if (GorillaTagger.Instance.rigidbody != null)
            {
                GorillaTagger.Instance.rigidbody.linearVelocity = Vector3.zero;
            }
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
                Vector3 targetPos = nearest.transform.position + direction * _offset;
                
                localPlayer.position = targetPos;
                if (GorillaTagger.Instance.offlineVRRig != null)
                {
                    GorillaTagger.Instance.offlineVRRig.transform.position = targetPos;
                }

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
                randomOffset.y = Mathf.Abs(randomOffset.y); // Keep above ground
                Vector3 targetPos = rig.transform.position + randomOffset;

                localPlayer.position = targetPos;
                if (GorillaTagger.Instance.offlineVRRig != null)
                {
                    GorillaTagger.Instance.offlineVRRig.transform.position = targetPos;
                }

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
                
                Vector3 targetPos = rig.transform.position + direction * _offset;

                localPlayer.position = targetPos;
                if (GorillaTagger.Instance.offlineVRRig != null)
                {
                    GorillaTagger.Instance.offlineVRRig.transform.position = targetPos;
                }

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
                Vector3 targetPos = _targetRig.transform.position + direction * _followDistance;

                localPlayer.position = targetPos;
                if (GorillaTagger.Instance.offlineVRRig != null)
                {
                    GorillaTagger.Instance.offlineVRRig.transform.position = targetPos;
                }
            }
        }
    }
}

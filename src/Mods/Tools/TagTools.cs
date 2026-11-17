using UnityEngine;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Aim at a gorilla and pull the trigger to send a real tag through the
    /// game mode. Enhanced with distance-based spam tagging and better VR support.
    /// </summary>
    [PluginPreferenced]
    [PluginToggleable]
    [ModCategory("Tools")]
    public class TagGun : Mod
    {
        private float _cooldown = 0.35f;
        private float _maxDistance = 100f;
        private bool _spamMode = false;

        public TagGun()
        {
            Name = "Tag Gun";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "cooldown",
                "Tag cooldown",
                "Seconds between tags",
                0.05f,
                2f,
                () => _cooldown,
                value => _cooldown = value,
                value => value.ToString("0.00") + " s"));

            AddSetting(ModSetting.Slider(
                "distance",
                "Max Distance",
                "Maximum tag distance in meters",
                5f,
                200f,
                () => _maxDistance,
                value => _maxDistance = value,
                value => value.ToString("0") + " m"));

            AddSetting(ModSetting.Toggle(
                "spam",
                "Spam Mode",
                "Hold trigger to continuously tag",
                () => _spamMode,
                value => _spamMode = value));
        }

        public override void Update()
        {
            // Enhanced gun update with distance check
            GunLib.Update((hit, rig, player) =>
            {
                if (player == null || rig == null) return;

                // Check distance
                Transform localPlayer = GorillaTagger.Instance?.transform;
                if (localPlayer != null)
                {
                    float distance = Vector3.Distance(localPlayer.position, rig.transform.position);
                    if (distance > _maxDistance) return;
                }

                bool tagged = PlayerActions.TagPlayer(player);
                if (tagged)
                {
                    Plugin.Log?.LogInfo($"Tagged {PlayerActions.SafeName(player)} at {hit.distance:0.1}m");
                }
            }, cooldown: _spamMode ? 0.05f : _cooldown);
        }

        public override void OnDisable()
        {
            GunLib.Cleanup();
        }
    }

    /// <summary>Tags all players within a specified distance.</summary>
    [PluginToggleable]
    [ModCategory("Tools")]
    public class TagNearbyPlayersMod : Mod
    {
        private float _tagDistance = 10f;
        private float _lastTagTime;

        public TagNearbyPlayersMod()
        {
            Name = "Tag Nearby Players";
            isToggle = false;

            AddSetting(ModSetting.Slider(
                "distance",
                "Tag Distance",
                "Tag all players within this distance",
                1f,
                50f,
                () => _tagDistance,
                value => _tagDistance = value,
                value => value.ToString("0") + " m"));
        }

        public override void Execute()
        {
            if (Time.time - _lastTagTime < 1f)
            {
                ModKit.Notify("Tag", "Wait 1 second between uses");
                return;
            }

            _lastTagTime = Time.time;

            Transform localPlayer = GorillaTagger.Instance?.transform;
            if (localPlayer == null)
            {
                ModKit.Notify("Tag", "Player not found");
                return;
            }

            int taggedCount = 0;
            var players = PlayerActions.GetPlayers();

            foreach (var player in players)
            {
                VRRig rig = PlayerActions.FindRigObject(player);
                if (rig == null) continue;

                float distance = Vector3.Distance(localPlayer.position, rig.transform.position);
                if (distance <= _tagDistance)
                {
                    if (PlayerActions.TagPlayer(player))
                    {
                        taggedCount++;
                    }
                }
            }

            ModKit.Notify("Tag", $"Tagged {taggedCount} players within {_tagDistance:0}m");
            Plugin.Log?.LogInfo($"Tagged {taggedCount} nearby players");
        }
    }

    /// <summary>One-shot action that tags everyone in the room.</summary>
    [PluginToggleable]
    [ModCategory("Tools")]
    public class TagAllPlayers : Mod
    {
        public TagAllPlayers()
        {
            Name = "Tag All";
            isToggle = false;
        }

        public override void Execute()
        {
            int tagged = PlayerActions.TagAll();
            ModKit.Notify("Tag", $"Tagged {tagged} players");
        }
    }

    /// <summary>Continuous tag spam on all players.</summary>
    [PluginToggleable]
    [ModCategory("Tools")]
    public class TagSpamMod : Mod
    {
        private float _spamRate = 0.1f;
        private float _nextSpam;

        public TagSpamMod()
        {
            Name = "Tag Spam All";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "rate",
                "Spam Rate",
                "Seconds between spam waves",
                0.05f,
                1f,
                () => _spamRate,
                value => _spamRate = value,
                value => value.ToString("0.00") + " s"));
        }

        public override void Update()
        {
            if (Time.time < _nextSpam) return;
            _nextSpam = Time.time + _spamRate;

            PlayerActions.TagAll();
        }
    }
}

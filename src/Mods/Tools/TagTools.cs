using UnityEngine;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Aim at a gorilla and pull the trigger to send a real tag through the
    /// game mode. Uses the same targeting as every other Cascade gun, so it
    /// connects with rigs even when their colliders are triggers.
    ///
    /// When the local player is master, the tag is applied directly. Other
    /// clients send the game's normal tag RPC, which the master validates.
    /// </summary>
    [PluginPreferenced]
    [PluginToggleable]
    public class TagGun : Mod
    {
        private float _cooldown = 0.35f;

        public TagGun()
        {
            Name = "Tag gun";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "cooldown",
                "Tag cooldown",
                "Seconds between tags, so a held trigger does not spam the lobby",
                0.1f,
                2f,
                () => _cooldown,
                value => _cooldown = value,
                value => value.ToString("0.00") + " s"));
        }

        public override void Update()
        {
            GunLib.Update((hit, rig, player) =>
            {
                if (player == null) return;
                PlayerActions.TagPlayer(player);
            }, cooldown: _cooldown);
        }

        public override void OnDisable()
        {
            GunLib.Cleanup();
        }
    }

    /// <summary>One-shot Mods-page action that tags everyone in the room.</summary>
    public class TagAllPlayers : Mod
    {
        public TagAllPlayers()
        {
            Name = "Tag all";
            isToggle = false;
        }

        public override void Execute()
        {
            PlayerActions.TagAll();
        }
    }
}

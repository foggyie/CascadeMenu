using System;
using System.Collections.Generic;
using GorillaNetworking;
using UnityEngine;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// "Fun" page actions that operate on the whole lobby. Everything here is
    /// client-side and reuses the same APIs as the player pages.
    /// </summary>
    internal static class FunActions
    {
        internal static int MuteAll(bool muted)
        {
            List<NetPlayer> players = PlayerActions.GetPlayers();
            int changed = 0;
            for (int i = 0; i < players.Count; i++)
            {
                NetPlayer player = players[i];
                if (player == null) continue;
                if (PlayerActions.IsMuted(player.UserId) == muted) continue;
                if (PlayerActions.SetMute(player, muted)) changed++;
            }
            return changed;
        }

        internal static string RoomName()
        {
            NetworkSystem system = NetworkSystem.Instance;
            string room = system != null ? system.RoomName : null;
            return string.IsNullOrEmpty(room) ? "unknown" : room;
        }

        internal static string AllNames()
        {
            List<NetPlayer> players = PlayerActions.GetPlayers();
            var names = new string[players.Count];
            for (int i = 0; i < players.Count; i++) names[i] = PlayerActions.SafeName(players[i]);
            return string.Join(", ", names);
        }

        internal static NetPlayer RandomPlayer()
        {
            List<NetPlayer> players = PlayerActions.GetPlayers();
            if (players.Count == 0) return null;
            return players[UnityEngine.Random.Range(0, players.Count)];
        }

        internal static bool MarkRandom(out string name)
        {
            name = "nobody";
            NetPlayer player = RandomPlayer();
            if (player == null) return false;

            VRRig rig = PlayerActions.FindRigObject(player);
            if (rig == null) return false;

            name = PlayerActions.SafeName(player);
            GunLibController.MarkTarget(rig);
            PlayerActions.Select(player.UserId);
            return true;
        }

        internal static void ClearSelection()
        {
            PlayerActions.Select(string.Empty);
        }
    }
}

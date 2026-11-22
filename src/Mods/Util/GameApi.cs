using System;
using System.Collections.Generic;
using System.Reflection;
using GorillaNetworking;
using UnityEngine;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Tools;
using CascadeMenu.Mods.Cosmetics;
using CascadeMenu.Mods.Visual;
using CascadeMenu.Mods.Movement;
using CascadeMenu.Mods.Overlays;

namespace CascadeMenu.Mods.Util
{
    /// <summary>
    /// Owns the reflection lookups into game and Photon members used by the
    /// read-only UI features (Player HUD, player list, room info). A member that
    /// a game update renames becomes a MISSING entry in the log and in the
    /// About page summary instead of a silent failure in each consumer.
    /// </summary>
    internal static class GameApi
    {
        private static bool _resolved;
        private static bool _verified;
        private static readonly List<string> Status = new List<string>();

        internal static PropertyInfo GameManagerInstance { get; private set; }
        internal static MethodInfo StaticFindRigForPlayer { get; private set; }

        internal static FieldInfo RigColorField { get; private set; }
        internal static PropertyInfo RigColorProperty { get; private set; }

        internal static Type PhotonNetworkType { get; private set; }
        internal static PropertyInfo PhotonServerAddress { get; private set; }
        internal static MethodInfo PhotonGetPing { get; private set; }
        internal static PropertyInfo PhotonPlayerList { get; private set; }
        internal static Type PhotonPlayerType { get; private set; }
        internal static PropertyInfo PhotonActorNumber { get; private set; }
        internal static PropertyInfo PhotonCustomProperties { get; private set; }

        internal static PropertyInfo TagManagerInstance { get; private set; }
        internal static MethodInfo IsTaggedMethod { get; private set; }
        internal static FieldInfo CurrentTaggedField { get; private set; }
        internal static PropertyInfo CurrentTaggedProperty { get; private set; }
        internal static PropertyInfo InfectionManagerInstance { get; private set; }
        internal static FieldInfo InfectedPlayersField { get; private set; }
        internal static PropertyInfo InfectedPlayersProperty { get; private set; }

        internal static PropertyInfo RoomCustomProps { get; private set; }

        /// <summary>Short result shown on the About page.</summary>
        internal static string Summary { get; private set; } = "not checked yet";

        /// <summary>Runs the check once, as soon as the local player has spawned.</summary>
        internal static void VerifyWhenReady()
        {
            if (_verified) return;
            if (PlayerActions.LocalPlayer() == null) return;
            Verify();
        }

        /// <summary>Logs one line per member and stores the short summary.</summary>
        internal static void Verify()
        {
            if (_verified) return;
            _verified = true;
            Resolve();

            Status.Clear();
            Add("game manager instance", GameManagerInstance != null);
            Add("game manager rig lookup", StaticFindRigForPlayer != null);
            Add("rig player color", RigColorField != null || RigColorProperty != null);
            Add("Photon network", PhotonNetworkType != null);
            Add("Photon server address", PhotonServerAddress != null);
            Add("Photon ping", PhotonGetPing != null);
            Add("Photon player list", PhotonPlayerList != null);
            Add("Photon actor number", PhotonActorNumber != null);
            Add("Photon custom properties", PhotonCustomProperties != null);
            Add("tag manager instance", TagManagerInstance != null);
            Add("tag state getter",
                IsTaggedMethod != null || CurrentTaggedField != null || CurrentTaggedProperty != null);
            Add("infection manager instance", InfectionManagerInstance != null);
            Add("infected players", InfectedPlayersField != null || InfectedPlayersProperty != null);
            Add("room custom properties", RoomCustomProps != null);

            int missing = 0;
            for (int i = 0; i < Status.Count; i++)
            {
                string line = Status[i];
                if (line.EndsWith("MISSING", StringComparison.Ordinal)) missing++;
                Plugin.Log?.LogInfo("Cascade game API: " + line);
            }

            Summary = missing == 0
                ? Status.Count + "/" + Status.Count + " resolved"
                : (Status.Count - missing) + "/" + Status.Count + " resolved, " + missing + " missing";
            Plugin.Log?.LogInfo("Cascade game API: " + Summary);
        }

        private static void Add(string name, bool resolved)
        {
            Status.Add(name + (resolved ? ": resolved" : ": MISSING"));
        }

        /// <summary>Resolves every member once. Safe to call from any feature.</summary>
        internal static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            try
            {
                Assembly game = typeof(GorillaTagger).Assembly;

                Type managerType = game.GetType("GorillaGameManager");
                if (managerType != null)
                {
                    GameManagerInstance = managerType.GetProperty(
                        "instance", BindingFlags.Public | BindingFlags.Static);
                    StaticFindRigForPlayer = managerType.GetMethod(
                        "StaticFindRigForPlayer",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(NetPlayer) },
                        null);
                }

                Type rigType = typeof(VRRig);
                RigColorField = rigType.GetField(
                    "playerColor",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (RigColorField != null && RigColorField.FieldType != typeof(Color))
                    RigColorField = null;

                RigColorProperty = rigType.GetProperty(
                    "playerColor",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (RigColorProperty != null && RigColorProperty.PropertyType != typeof(Color))
                    RigColorProperty = null;

                Type tagType = game.GetType("GorillaTagManager");
                if (tagType != null)
                {
                    TagManagerInstance = tagType.GetProperty("instance",
                        BindingFlags.Public | BindingFlags.Static) ??
                        tagType.GetProperty("Instance",
                            BindingFlags.Public | BindingFlags.Static);
                    IsTaggedMethod = tagType.GetMethod("IsTagged",
                        BindingFlags.Public | BindingFlags.Instance,
                        null, new[] { typeof(NetPlayer) }, null);
                    CurrentTaggedField = tagType.GetField("currentTaggedPlayer",
                        BindingFlags.Public | BindingFlags.Instance);
                    CurrentTaggedProperty = tagType.GetProperty("currentTaggedPlayer",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                Type infectionType = game.GetType("GorillaInfectionManager");
                if (infectionType != null)
                {
                    InfectionManagerInstance = infectionType.GetProperty("instance",
                        BindingFlags.Public | BindingFlags.Static) ??
                        infectionType.GetProperty("Instance",
                            BindingFlags.Public | BindingFlags.Static);
                    InfectedPlayersField = infectionType.GetField("infectedPlayers",
                        BindingFlags.Public | BindingFlags.Instance);
                    InfectedPlayersProperty = infectionType.GetProperty("infectedPlayers",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                RoomCustomProps = typeof(RoomConfig).GetProperty(
                    "CustomProps", BindingFlags.Public | BindingFlags.Instance);

                PhotonNetworkType = FindType("Photon.Pun.PhotonNetwork");
                if (PhotonNetworkType != null)
                {
                    PhotonServerAddress = PhotonNetworkType.GetProperty(
                        "ServerAddress", BindingFlags.Public | BindingFlags.Static);
                    PhotonGetPing = PhotonNetworkType.GetMethod(
                        "GetPing",
                        BindingFlags.Public | BindingFlags.Static,
                        null, Type.EmptyTypes, null);
                    PhotonPlayerList = PhotonNetworkType.GetProperty(
                        "PlayerList", BindingFlags.Public | BindingFlags.Static);
                }

                PhotonPlayerType = FindType("Photon.Realtime.Player");
                if (PhotonPlayerType != null)
                {
                    PhotonActorNumber = PhotonPlayerType.GetProperty(
                        "ActorNumber", BindingFlags.Public | BindingFlags.Instance);
                    PhotonCustomProperties = PhotonPlayerType.GetProperty(
                        "CustomProperties", BindingFlags.Public | BindingFlags.Instance);
                }
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogWarning("Cascade game API lookup failed: " + exception.Message);
            }
        }

        /// <summary>Finds a type in any loaded assembly without a hard reference.</summary>
        internal static Type FindType(string name)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    Type type = assemblies[i].GetType(name, false);
                    if (type != null) return type;
                }
                catch (Exception exception)
                {
                    LogOnce.Warn("Game API type lookup", exception);
                }
            }
            return null;
        }
    }
}

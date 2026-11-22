using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GorillaLocomotion;
using GorillaNetworking;
using UnityEngine;
using CascadeMenu.UI;
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
    /// <summary>
    /// Live room player list and the client-side actions attached to each player.
    /// Everything here is local: mutes and reports use the same APIs as the
    /// in-game scoreboard, the rest is visual/utility only.
    /// </summary>
    internal static class PlayerActions
    {
        internal const string PlayersTag = "cascade.players";
        internal const string PlayerTagPrefix = "cascade.player.";
        internal const string ReportTagPrefix = "cascade.report.";
        internal const string CustomPropsTagPrefix = "cascade.props.";

        internal static string PlayerTag(string userId)
        {
            return PlayerTagPrefix + (userId ?? "unknown");
        }

        internal static string ReportTag(string userId)
        {
            return ReportTagPrefix + (userId ?? "unknown");
        }

        internal static string CustomPropsTag(string userId)
        {
            return CustomPropsTagPrefix + (userId ?? "unknown");
        }

        internal static bool IsPlayerListPage(CascadePage page)
        {
            return page != null && (page.Tag as string) == PlayersTag;
        }

        internal static bool IsPlayerPage(CascadePage page, out string userId)
        {
            userId = null;
            string tag = page != null ? page.Tag as string : null;
            if (string.IsNullOrEmpty(tag) || !tag.StartsWith(PlayerTagPrefix, StringComparison.Ordinal)) return false;
            userId = tag.Substring(PlayerTagPrefix.Length);
            return true;
        }

        internal static bool PlayerExists(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return false;
            List<NetPlayer> players = GetPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].UserId == userId) return true;
            }
            return false;
        }

        /// <summary>Finds a player in the current room by user ID (null when gone).</summary>
        internal static NetPlayer FindPlayer(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return null;
            List<NetPlayer> players = GetPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].UserId == userId) return players[i];
            }
            return null;
        }

        /// <summary>Changes whenever the room roster changes; used to refresh the list.</summary>
        internal static string PlayerSignature()
        {
            List<NetPlayer> players = GetPlayers();
            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < players.Count; i++)
            {
                NetPlayer player = players[i];
                builder.Append(player.UserId);
                builder.Append(';');
            }
            builder.Append('|');
            return builder.ToString();
        }

        // -----------------------------------------------------------------
        // Selection
        // -----------------------------------------------------------------

        /// <summary>The player highlighted in the list (opened or gun-selected).</summary>
        internal static string SelectedUserId { get; private set; } = string.Empty;

        internal static void Select(string userId)
        {
            SelectedUserId = userId ?? string.Empty;
        }

        internal static bool IsSelected(NetPlayer player)
        {
            if (player == null) return false;
            if (!string.IsNullOrEmpty(SelectedUserId) && SelectedUserId == player.UserId) return true;

            VRRig selected = GunLibController.SelectedRig;
            if (selected == null) return false;
            RigContainer container = FindRig(player.UserId);
            return container != null && container.Rig == selected;
        }

        // -----------------------------------------------------------------
        // Hand taps
        // -----------------------------------------------------------------

        /// <summary>
        /// True when the player's hands are touching. Gorilla Tag plays its tap
        /// sound at that moment, so the list can show a live TAP badge.
        /// </summary>
        internal static bool IsHandTapping(NetPlayer player)
        {
            VRRig rig = FindRigObject(player);
            if (rig == null || rig.leftHandTransform == null || rig.rightHandTransform == null) return false;
            return Vector3.Distance(rig.leftHandTransform.position, rig.rightHandTransform.position) < 0.14f;
        }

        // -----------------------------------------------------------------
        // Room data
        // -----------------------------------------------------------------

        internal static List<NetPlayer> GetPlayers()
        {
            var result = new List<NetPlayer>();
            NetworkSystem system = NetworkSystem.Instance;
            if (system == null || !system.InRoom) return result;

            NetPlayer[] players;
            try
            {
                players = system.PlayerListOthers;
            }
            catch (Exception)
            {
                // Roster reads can race with a room teardown.
                return result;
            }
            if (players == null) return result;

            for (int i = 0; i < players.Length; i++)
            {
                NetPlayer player = players[i];
                if (player == null || player.IsNull || !player.IsValid || player.IsLocal) continue;
                result.Add(player);
            }

            result.Sort((a, b) =>
            {
                int cmp = a.ActorNumber.CompareTo(b.ActorNumber);
                return cmp != 0 ? cmp : string.Compare(SafeName(a), SafeName(b), StringComparison.OrdinalIgnoreCase);
            });
            return result;
        }

        internal static NetPlayer LocalPlayer()
        {
            NetworkSystem system = NetworkSystem.Instance;
            return system != null ? system.LocalPlayer : null;
        }

        internal static string SafeName(NetPlayer player)
        {
            if (player == null) return "Unknown";
            string name = player.NickName;
            if (string.IsNullOrEmpty(name)) name = player.DefaultName;
            if (string.IsNullOrEmpty(name)) name = "Player " + player.ActorNumber;
            return name;
        }

        internal static string SafePlatform(NetPlayer player)
        {
            if (player == null) return "Unknown";
            string platform = player.Platform;
            return string.IsNullOrEmpty(platform) ? "Unknown" : platform;
        }

        internal static RigContainer FindRig(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return null;

            IReadOnlyList<RigContainer> containers = VRRigCache.ActiveRigContainers;
            if (containers != null)
            {
                for (int i = 0; i < containers.Count; i++)
                {
                    RigContainer container = containers[i];
                    if (container == null || container.Creator == null) continue;
                    if (container.Creator.UserId == userId) return container;
                }
            }

            containers = VRRigCache.AllRigContainers;
            if (containers != null)
            {
                for (int i = 0; i < containers.Count; i++)
                {
                    RigContainer container = containers[i];
                    if (container == null || container.Creator == null) continue;
                    if (container.Creator.UserId == userId) return container;
                }
            }

            return null;
        }

        internal static VRRig FindRigObject(NetPlayer player)
        {
            RigContainer container = FindRig(player != null ? player.UserId : null);
            if (container != null && container.Rig != null) return container.Rig;

            if (player != null)
            {
                IReadOnlyList<RigContainer> active = VRRigCache.ActiveRigContainers;
                if (active != null)
                {
                    for (int i = 0; i < active.Count; i++)
                    {
                        RigContainer c = active[i];
                        if (c == null || c.Rig == null) continue;
                        NetPlayer creator = c.Creator ?? c.Rig.Creator;
                        if (creator == null) continue;
                        if (creator == player ||
                            (player.ActorNumber != 0 && creator.ActorNumber == player.ActorNumber) ||
                            (!string.IsNullOrEmpty(player.UserId) && creator.UserId == player.UserId))
                        {
                            return c.Rig;
                        }
                    }
                }
            }

            // Fallback for the short window where the cache has not caught up
            // with the game manager's roster entry. Called through reflection
            // because GorillaGameManager inherits a Photon type that this project
            // deliberately does not reference.
            if (player != null)
            {
                try
                {
                    GameApi.Resolve();
                    MethodInfo findRig = GameApi.StaticFindRigForPlayer;
                    if (findRig != null)
                    {
                        VRRig rig = findRig.Invoke(null, new object[] { player }) as VRRig;
                        if (rig != null) return rig;
                    }
                }
                catch (Exception)
                {
                    // The game manager can be torn down with the room.
                }
            }

            return null;
        }

        private static readonly HashSet<VRRig> _remoteRigsSet = new HashSet<VRRig>();
        private static readonly List<VRRig> _remoteRigsBuffer = new List<VRRig>(16);
        private static int _remoteRigsFrame = -1;

        /// <summary>
        /// Collects every active remote VRRig in the scene. Cached per frame so
        /// multiple visual and HUD mods calling it in the same frame share one pass.
        /// </summary>
        internal static List<VRRig> GetRemoteRigs()
        {
            if (_remoteRigsFrame == Time.frameCount)
            {
                return _remoteRigsBuffer;
            }
            _remoteRigsFrame = Time.frameCount;
            _remoteRigsBuffer.Clear();
            _remoteRigsSet.Clear();

            IReadOnlyList<RigContainer> active = VRRigCache.ActiveRigContainers;
            if (active != null)
            {
                for (int i = 0; i < active.Count; i++)
                {
                    RigContainer c = active[i];
                    if (c == null || c.Rig == null) continue;
                    VRRig rig = c.Rig;
                    if (rig == VRRig.LocalRig || rig.isOfflineVRRig) continue;
                    if (rig.gameObject == null || !rig.gameObject.activeInHierarchy) continue;
                    if (_remoteRigsSet.Add(rig)) _remoteRigsBuffer.Add(rig);
                }
            }

            List<NetPlayer> players = GetPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                VRRig rig = FindRigObject(players[i]);
                if (rig == null || rig == VRRig.LocalRig || rig.isOfflineVRRig) continue;
                if (rig.gameObject == null || !rig.gameObject.activeInHierarchy) continue;
                if (_remoteRigsSet.Add(rig)) _remoteRigsBuffer.Add(rig);
            }

            return _remoteRigsBuffer;
        }

        // -----------------------------------------------------------------
        // Appearance
        // -----------------------------------------------------------------

        /// <summary>Live gorilla color for a remote rig, with safe fallbacks.</summary>
        internal static Color GetRigColor(VRRig rig)
        {
            if (rig == null) return Color.white;

            GameApi.Resolve();
            try
            {
                FieldInfo colorField = GameApi.RigColorField;
                if (colorField != null)
                    return (Color)colorField.GetValue(rig);
                PropertyInfo colorProperty = GameApi.RigColorProperty;
                if (colorProperty != null)
                    return (Color)colorProperty.GetValue(rig, null);
            }
            catch (Exception)
            {
                // Fall through to the material color below.
            }

            try
            {
                if (rig.mainSkin != null && rig.mainSkin.material != null)
                    return rig.mainSkin.material.color;
            }
            catch (Exception)
            {
                // A rig can be destroyed between the null check and the read.
            }

            return Color.white;
        }

        internal static Color GetPlayerColor(NetPlayer player)
        {
            return GetRigColor(FindRigObject(player));
        }

        // -----------------------------------------------------------------
        // Infected / Tagged Detection
        // -----------------------------------------------------------------

        /// <summary>
        /// Checks whether a remote gorilla rig / player is currently infected or tagged.
        /// Combines fast material / setMatIndex checks on the VRRig with GorillaTagManager
        /// and GorillaInfectionManager reflection fallbacks.
        /// </summary>
        internal static bool IsInfected(VRRig rig, NetPlayer player = null)
        {
            if (rig != null)
            {
                try
                {
                    if (rig.setMatIndex > 0) return true;

                    if (rig.mainSkin != null && rig.mainSkin.sharedMaterial != null)
                    {
                        string matName = rig.mainSkin.sharedMaterial.name;
                        if (!string.IsNullOrEmpty(matName))
                        {
                            string lower = matName.ToLowerInvariant();
                            if (lower.Contains("infected") || lower.Contains("it") ||
                                lower.Contains("lava") || lower.Contains("rock") ||
                                lower.Contains("ice") || lower.Contains("tag") ||
                                lower.Contains("hunter"))
                            {
                                return true;
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Fall through to manager checks.
                }
            }

            if (player == null && rig != null)
            {
                player = GunLibController.GetPlayerFromRig(rig);
            }
            if (player == null) return false;

            GameApi.Resolve();
            try
            {
                PropertyInfo tagManagerInstance = GameApi.TagManagerInstance;
                if (tagManagerInstance != null)
                {
                    object manager = tagManagerInstance.GetValue(null, null);
                    if (manager != null)
                    {
                        MethodInfo isTaggedMethod = GameApi.IsTaggedMethod;
                        if (isTaggedMethod != null)
                        {
                            object result = isTaggedMethod.Invoke(manager, new object[] { player });
                            if (result is bool b) return b;
                        }

                        object current = GameApi.CurrentTaggedField != null
                            ? GameApi.CurrentTaggedField.GetValue(manager)
                            : (GameApi.CurrentTaggedProperty != null ? GameApi.CurrentTaggedProperty.GetValue(manager, null) : null);
                        if (current is NetPlayer taggedPlayer)
                        {
                            if (taggedPlayer.UserId == player.UserId || taggedPlayer.ActorNumber == player.ActorNumber)
                                return true;
                        }
                    }
                }

                PropertyInfo infectionManagerInstance = GameApi.InfectionManagerInstance;
                if (infectionManagerInstance != null)
                {
                    object manager = infectionManagerInstance.GetValue(null, null);
                    if (manager != null)
                    {
                        object collection = GameApi.InfectedPlayersField != null
                            ? GameApi.InfectedPlayersField.GetValue(manager)
                            : (GameApi.InfectedPlayersProperty != null ? GameApi.InfectedPlayersProperty.GetValue(manager, null) : null);
                        if (collection is IEnumerable list)
                        {
                            foreach (object item in list)
                            {
                                if (item is NetPlayer infected &&
                                    (infected.UserId == player.UserId || infected.ActorNumber == player.ActorNumber))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Ignore room teardown races.
            }

            return false;
        }

        internal static string ColorHex(Color color)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        private static float DistanceTo(NetPlayer player)
        {
            RigContainer container = FindRig(player != null ? player.UserId : null);
            GTPlayer local = GTPlayer.Instance;
            if (container == null || local == null) return -1f;
            return Vector3.Distance(container.transform.position, local.transform.position);
        }

        // -----------------------------------------------------------------
        // Room information
        // -----------------------------------------------------------------

        /// <summary>
        /// Photon server address (usually IP:port), read through reflection so the
        /// project does not need a hard reference to PhotonUnityNetworking.
        /// </summary>
        internal static string ServerAddress()
        {
            try
            {
                GameApi.Resolve();
                if (GameApi.PhotonNetworkType == null) return "unknown";
                PropertyInfo property = GameApi.PhotonServerAddress;
                string address = property != null ? property.GetValue(null, null) as string : null;
                return string.IsNullOrEmpty(address) ? "offline" : address;
            }
            catch (Exception)
            {
                return "unknown";
            }
        }

        /// <summary>Name of the current master client, with "(you)" for the local host.</summary>
        internal static string MasterName(NetworkSystem system)
        {
            if (system == null || !system.InRoom) return "unknown";
            NetPlayer master;
            bool isMaster;
            try
            {
                master = system.MasterClient;
                isMaster = system.IsMasterClient;
            }
            catch (Exception)
            {
                return "unknown";
            }

            if (master == null)
            {
                NetPlayer local = LocalPlayer();
                if (isMaster && local != null) return SafeName(local) + " (you)";
                return "unknown";
            }
            string name = SafeName(master);
            NetPlayer me = LocalPlayer();
            if (me != null && master.UserId == me.UserId) name += " (you)";
            return name;
        }

        /// <summary>Local device ID. Other players' HWIDs are never exposed by the game.</summary>
        internal static string LocalHardwareId()
        {
            string id = SystemInfo.deviceUniqueIdentifier;
            return string.IsNullOrEmpty(id) ? "unavailable" : id;
        }

        /// <summary>Custom properties attached to the current room (join filters, etc).</summary>
        internal static List<string> RoomCustomProps()
        {
            var result = new List<string>();
            NetworkSystem system = NetworkSystem.Instance;
            if (system == null || system.CurrentRoom == null) return result;

            // RoomConfig.CustomProps is a Photon Hashtable. Read it through
            // reflection so the plugin does not need a Photon assembly reference.
            IDictionary props = null;
            try
            {
                GameApi.Resolve();
                PropertyInfo property = GameApi.RoomCustomProps;
                props = property != null ? property.GetValue(system.CurrentRoom, null) as IDictionary : null;
            }
            catch (Exception)
            {
                return result;
            }
            if (props == null) return result;

            try
            {
                foreach (DictionaryEntry entry in props)
                {
                    string key = entry.Key != null ? entry.Key.ToString() : "?";
                    string value = entry.Value != null ? entry.Value.ToString() : string.Empty;
                    result.Add(string.IsNullOrEmpty(value) ? key : key + "=" + value);
                }
            }
            catch (Exception)
            {
                // A room can rewrite its properties while we are reading them.
            }
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        internal static string RoomCustomPropSummary()
        {
            List<string> props = RoomCustomProps();
            if (props.Count == 0) return "None";
            int shown = Mathf.Min(3, props.Count);
            return props.Count + ": " + string.Join(", ", props.GetRange(0, shown));
        }

        /// <summary>
        /// Local cosmetic-based hint, not proof. "Modder" here means the worn
        /// cosmetics look unknown, duplicated or out of their slot.
        /// </summary>
        internal static bool IsFlaggedModder(NetPlayer player, out string summary)
        {
            summary = string.Empty;
            CosmeticChecker.Report report = CosmeticChecker.Inspect(FindRigObject(player));
            if (!report.Available) return false;
            int flags = report.UnknownCount + report.MismatchCount + report.DuplicateCount;
            if (flags == 0) return false;
            summary = flags + (flags == 1 ? " flag" : " flags");
            return true;
        }

        internal static string FlaggedPlayersSummary(out int count)
        {
            count = 0;
            var names = new List<string>();
            List<NetPlayer> players = GetPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                string ignored;
                if (!IsFlaggedModder(players[i], out ignored)) continue;
                count++;
                if (names.Count < 3) names.Add(SafeName(players[i]));
            }
            if (count == 0) return "None";
            string text = string.Join(", ", names);
            if (count > names.Count) text += ", ...";
            return count + ": " + text;
        }

        /// <summary>
        /// Room-wide special-cosmetic summary. "Verified" means the exact special
        /// item ID was found in the correct slot; "fake" means someone claimed an
        /// admin/creator name without a valid special item. Ownership cannot be
        /// checked client-side.
        /// </summary>
        internal static void SpecialPlayersSummary(out string verifiedText, out string fakeText)
        {
            var verified = new List<string>();
            var fakes = new List<string>();
            var verifiedRoles = new List<string>(4);
            var fakeRoles = new List<string>(4);
            List<NetPlayer> players = GetPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                NetPlayer player = players[i];
                CosmeticChecker.GetSpecialRoles(FindRigObject(player), verifiedRoles, fakeRoles);
                if (verifiedRoles.Count > 0)
                {
                    verified.Add(SafeName(player) + " (" +
                                 string.Join(", ", verifiedRoles) + ")");
                }
                if (fakeRoles.Count > 0)
                {
                    fakes.Add(SafeName(player) + " (" +
                              string.Join(", ", fakeRoles) + ")");
                }
            }

            verifiedText = verified.Count == 0
                ? "None"
                : verified.Count + ": " + string.Join(", ", TruncateList(verified, 3));
            fakeText = fakes.Count == 0
                ? "None"
                : fakes.Count + ": " + string.Join(", ", TruncateList(fakes, 3));
        }

        private static List<string> TruncateList(List<string> items, int max)
        {
            int shown = Mathf.Min(max, items.Count);
            var result = items.GetRange(0, shown);
            if (items.Count > shown) result.Add("...");
            return result;
        }

        // -----------------------------------------------------------------
        // Per-player toy actions
        // -----------------------------------------------------------------

        private static readonly string[] CustomPropNames = { "Cube", "Sphere", "Cylinder", "Capsule" };

        private static readonly Dictionary<string, GameObject> CustomPropObjects =
            new Dictionary<string, GameObject>();

        private static readonly Dictionary<string, int> CustomPropIndexes =
            new Dictionary<string, int>();

        internal static string CustomPropStatus(NetPlayer player)
        {
            string id = player != null ? player.UserId : null;
            if (string.IsNullOrEmpty(id)) return "Off";
            GameObject prop;
            if (!CustomPropObjects.TryGetValue(id, out prop) || prop == null)
            {
                return "Off";
            }
            int index;
            CustomPropIndexes.TryGetValue(id, out index);
            return CustomPropNames[Mathf.Clamp(index, 0, CustomPropNames.Length - 1)];
        }

        /// <summary>
        /// Cycles a local-only prop in the player's right hand. Nothing is sent
        /// over the network, so only you can see it.
        /// </summary>
        internal static string CycleCustomProp(NetPlayer player)
        {
            if (player == null) return "Off";
            string id = player.UserId;
            VRRig rig = FindRigObject(player);
            Transform hand = rig != null ? rig.rightHandTransform : null;
            if (hand == null && rig != null) hand = rig.leftHandTransform;
            if (hand == null) return "Not ready";

            GameObject existing;
            if (CustomPropObjects.TryGetValue(id, out existing) && existing != null)
            {
                UnityEngine.Object.Destroy(existing);
            }

            int index;
            CustomPropIndexes.TryGetValue(id, out index);
            index = (index + 1) % CustomPropNames.Length;
            CustomPropIndexes[id] = index;

            PrimitiveType shape = PrimitiveType.Cube;
            switch (index)
            {
                case 1:
                    shape = PrimitiveType.Sphere;
                    break;
                case 2:
                    shape = PrimitiveType.Cylinder;
                    break;
                case 3:
                    shape = PrimitiveType.Capsule;
                    break;
            }

            GameObject prop = GameObject.CreatePrimitive(shape);
            prop.name = "CascadeCustomProp";
            prop.transform.SetParent(hand, false);
            prop.transform.localPosition = new Vector3(0f, 0.07f, 0.05f);
            prop.transform.localRotation = Quaternion.identity;
            prop.transform.localScale = Vector3.one * 0.11f;

            Collider collider = prop.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);

            Renderer renderer = prop.GetComponent<Renderer>();
            if (renderer != null && renderer.material != null)
            {
                renderer.material.color = new Color(0.24f, 0.86f, 1f, 1f);
            }

            CustomPropObjects[id] = prop;
            return CustomPropNames[index];
        }

        internal static bool RemoveCustomProp(NetPlayer player)
        {
            string id = player != null ? player.UserId : null;
            if (string.IsNullOrEmpty(id)) return false;
            GameObject prop;
            if (CustomPropObjects.TryGetValue(id, out prop) && prop != null)
            {
                UnityEngine.Object.Destroy(prop);
            }
            CustomPropObjects.Remove(id);
            CustomPropIndexes.Remove(id);
            return true;
        }

        /// <summary>
        /// Ghost-teleports next to the player. Collision is disabled only for the
        /// teleport frame, so this can pass through walls without leaving noclip on.
        /// </summary>
        internal static bool NoclipTo(NetPlayer player)
        {
            VRRig rig = FindRigObject(player);
            GTPlayer local = GTPlayer.Instance;
            if (rig == null || local == null) return false;

            Vector3 target;
            if (!FindStandPosition(rig, local, out target)) return false;

            Collider body = local.bodyCollider;
            Collider head = local.headCollider;
            Rigidbody rigidbody = local.playerRigidBody;
            bool bodyWasEnabled = body != null && body.enabled;
            bool headWasEnabled = head != null && head.enabled;
            bool detectWasEnabled = rigidbody != null && rigidbody.detectCollisions;
            try
            {
                if (body != null) body.enabled = false;
                if (head != null) head.enabled = false;
                if (rigidbody != null) rigidbody.detectCollisions = false;

                TeleportLocal(local, target);
            }
            finally
            {
                if (body != null) body.enabled = bodyWasEnabled;
                if (head != null) head.enabled = headWasEnabled;
                if (rigidbody != null) rigidbody.detectCollisions = detectWasEnabled;
            }
            return true;
        }

        // -----------------------------------------------------------------
        // Moderation
        // -----------------------------------------------------------------

        internal static bool IsMuted(string userId)
        {
            RigContainer container = FindRig(userId);
            return container != null && container.IsMutedFor(RigContainer.MuteReason.Manual);
        }

        internal static bool ToggleMute(NetPlayer player)
        {
            if (player == null) return false;
            bool muted = !IsMuted(player.UserId);
            return SetMute(player, muted);
        }

        /// <summary>Force a mute state (used by toggle and by Mute all).</summary>
        internal static bool SetMute(NetPlayer player, bool muted)
        {
            if (player == null) return false;

            RigContainer container = FindRig(player.UserId);
            if (container == null) return false;

            container.hasManualMute = muted;
            container.SetMuted(RigContainer.MuteReason.Manual, muted);

            PlayerPrefs.SetInt(player.UserId, muted ? 1 : 0);
            PlayerPrefs.Save();

            if (GorillaScoreboardTotalUpdater.instance != null)
                GorillaScoreboardTotalUpdater.ReportMute(player, muted ? 1 : 0);

            return true;
        }

        internal static bool Report(NetPlayer player, GorillaPlayerLineButton.ButtonType reason)
        {
            if (player == null) return false;

            GorillaPlayerScoreboardLine.ReportPlayer(player.UserId, reason, SafeName(player));
            return true;
        }

        // -----------------------------------------------------------------
        // Utility
        // -----------------------------------------------------------------

        internal static void Copy(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            GUIUtility.systemCopyBuffer = text;
        }

        internal static bool TeleportTo(NetPlayer player)
        {
            VRRig rig = FindRigObject(player);
            GTPlayer local = GTPlayer.Instance;
            if (rig == null || local == null) return false;

            Vector3 target;
            if (!FindStandPosition(rig, local, out target)) return false;

            TeleportLocal(local, target);
            return true;
        }

        /// <summary>Teleports the local player to a world position (used by guns).</summary>
        internal static bool TeleportToPosition(Vector3 position)
        {
            GTPlayer local = GTPlayer.Instance;
            if (local == null) return false;
            TeleportLocal(local, position);
            return true;
        }

        /// <summary>
        /// Picks a walkable spot next to a remote rig: one body-width toward the
        /// local player, then snapped down to the floor.
        /// </summary>
        private static bool FindStandPosition(VRRig rig, GTPlayer local, out Vector3 target)
        {
            target = Vector3.zero;
            if (rig == null || local == null) return false;

            Vector3 origin = rig.transform.position;
            Vector3 offset = local.transform.position - origin;
            offset.y = 0f;

            // Standing exactly on the target would push both bodies apart; if we
            // are already close, use the local forward direction instead.
            if (offset.sqrMagnitude < 0.04f) offset = local.transform.forward;

            target = origin + offset.normalized * 1.35f;
            target.y = origin.y;

            // Snap to the floor so the teleport does not leave us hovering.
            RaycastHit hit;
            if (Physics.Raycast(target + Vector3.up * 2f, Vector3.down, out hit, 6f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                target.y = hit.point.y + 0.02f;
            }

            return true;
        }

        /// <summary>
        /// Applies a teleport with velocity cleared and verifies that the player
        /// actually moved. Some game states ignore <c>TeleportTo</c> (attached to
        /// a train, mid-respawn), so a direct transform fallback is included.
        /// </summary>
        private static void TeleportLocal(GTPlayer local, Vector3 target)
        {
            if (local == null) return;

            Rigidbody rigidbody = local.playerRigidBody;
            if (rigidbody != null)
            {
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
            }

            // Keep the heading but drop any acrobatics roll/pitch so the body
            // arrives upright and the physics update cannot snap it back.
            Quaternion rotation = Quaternion.Euler(0f, local.transform.eulerAngles.y, 0f);
            local.TeleportTo(target, rotation, false, false);

            if (rigidbody != null)
            {
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
            }

            // Verify the move. If the game refused it, write the transform
            // directly and sync the visual rig as well.
            if ((local.transform.position - target).sqrMagnitude > 4f)
            {
                local.transform.position = target;
                if (rigidbody != null) rigidbody.position = target;

                if (GorillaTagger.Instance != null)
                {
                    GorillaTagger.Instance.transform.position = target;
                    if (GorillaTagger.Instance.offlineVRRig != null)
                        GorillaTagger.Instance.offlineVRRig.transform.position = target;
                }

                Physics.SyncTransforms();
            }
        }

        // -----------------------------------------------------------------
        // Pages
        // -----------------------------------------------------------------

        // -----------------------------------------------------------------
        // Player Telemetry Helpers (FPS, Speaking, Color Code, Custom Props)
        // -----------------------------------------------------------------
        private static FieldInfo _rigFpsField;
        private static PropertyInfo _rigFpsProp;
        private static bool _rigFpsResolved;
        private static MethodInfo _netPlayerGetRef;
        private static PropertyInfo _photonCustomProps;
        private static bool _customPropsResolved;

        internal static int LocalFps()
        {
            float dt = Time.smoothDeltaTime;
            if (dt <= 0.0001f) dt = Time.unscaledDeltaTime;
            return dt > 0.0001f ? Mathf.Clamp(Mathf.RoundToInt(1f / dt), 0, 999) : 0;
        }

        internal static int GetRigFps(VRRig rig)
        {
            if (rig == null) return -1;
            if (rig.isLocal) return LocalFps();

            if (!_rigFpsResolved)
            {
                _rigFpsResolved = true;
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                _rigFpsField = typeof(VRRig).GetField("fps", flags);
                if (_rigFpsField == null) _rigFpsProp = typeof(VRRig).GetProperty("fps", flags);
            }

            try
            {
                object val = _rigFpsField != null
                    ? _rigFpsField.GetValue(rig)
                    : (_rigFpsProp != null ? _rigFpsProp.GetValue(rig, null) : null);
                if (val is int iv) return iv;
                if (val is float fv) return Mathf.RoundToInt(fv);
                if (val is byte bv) return bv;
                if (val is short sv) return sv;
            }
            catch { }
            return -1;
        }

        internal static int GetPlayerFps(NetPlayer player)
        {
            if (player == null) return -1;
            if (player.IsLocal) return LocalFps();
            return GetRigFps(FindRigObject(player));
        }

        internal static bool IsRigSpeaking(VRRig rig)
        {
            if (rig == null) return false;
            try
            {
                GorillaSpeakerLoudness speaker = rig.GetComponent<GorillaSpeakerLoudness>();
                if (speaker != null) return speaker.IsSpeaking || speaker.Loudness > 0.04f;
            }
            catch { }
            return false;
        }

        internal static bool IsPlayerSpeaking(NetPlayer player)
        {
            if (player == null) return false;
            return IsRigSpeaking(FindRigObject(player));
        }

        internal static string ColorCode9(Color c)
        {
            int r = Mathf.Clamp(Mathf.RoundToInt(c.r * 9f), 0, 9);
            int g = Mathf.Clamp(Mathf.RoundToInt(c.g * 9f), 0, 9);
            int b = Mathf.Clamp(Mathf.RoundToInt(c.b * 9f), 0, 9);
            return r + " " + g + " " + b;
        }

        // Illegal / cheat menu custom property keys vs known legal mod keys
        private static readonly HashSet<string> IllegalModKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "void", "void_menu_open", "ORBIT", "ØƦƁƖƬ", "Vivid", "Atlas",
            "ObsidianMC", "genesis", "violetpaiduser", "violetfree",
            "Violet On Top", "dark", "oblivionuser", "hidden menu", "elux",
            "Mango", "bvunt menu", "y u lookin in here weirdo", "cokecosmetics"
        };

        private static readonly Dictionary<string, string> KnownModPropKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Illegal / cheat menus
            { "void", "Void" },
            { "void_menu_open", "Void" },
            { "ORBIT", "Orbit" },
            { "ØƦƁƖƬ", "Orbit" },
            { "Vivid", "Vivid" },
            { "Atlas", "Atlas" },
            { "ObsidianMC", "Obsidian" },
            { "genesis", "Genesis" },
            { "violetpaiduser", "Violet Paid" },
            { "violetfree", "Violet Free" },
            { "Violet On Top", "Violet" },
            { "dark", "Shiba GT Dark" },
            { "oblivionuser", "Oblivion" },
            { "hidden menu", "Hidden" },
            { "elux", "Elux" },
            { "Mango", "Mango Mod Menu" },
            { "bvunt menu", "Bvunt Menu" },
            { "y u lookin in here weirdo", "Malachi Menu Reborn" },
            { "cokecosmetics", "Coke Cosmetx" },

            // Known legal mods
            { "GrateVersion", "Grate" },
            { "BANANAOS", "Banana OS" },
            { "GC", "Gorilla Craft" },
            { "CarName", "Gorilla Vehicles" },
            { "GorillaWatch", "Gorilla Watch" },
            { "InfoWatch", "Info Watch" },
            { "BananaPhone", "Banana Phone" },
            { "MonkePhone", "Monke Phone" },
            { "MP25", "Monke Phone" },
            { "GorillaShirts", "Gorilla Shirts" },
            { "ShirtProperties", "Gorilla Shirts" },
            { "shirtversion", "Gorilla Shirts" },
            { "GPronouns", "Gorilla Pronouns" },
            { "GFaces", "Gorilla Faces" },
            { "GorillaStats", "Gorilla Stats" },
            { "GorillaCinema", "Gorilla Cinema" },
            { "MediaPad", "Media Pad" },
            { "EmoteWheel", "Emote Wheel" },
            { "ChainedTogether", "Chained Together" },
            { "ChainedTogetherActive", "Chained Together" },
            { "GTrials", "Gorilla Trials" },
            { "using gorilladrift", "Gorilla Drift" },
            { "HP_Left", "Holdable Pad" },
            { "CSVersion", "Custom Skin" },
            { "pmversion", "Player Models" },
            { "spectapeversion", "Spec Tape" },
            { "goofywalkversion", "Goofy Walk" },
            { "FPS-Nametags for Zlothy", "FPS Tags" },
            { "github.com/maroon-shadow/SimpleBoards", "Simple Boards" },
            { "github.com/ZlothY29IQ/GorillaMediaDisplay", "Gorilla Media Display" },
            { "github.com/ZlothY29IQ/TooMuchInfo", "Too Much Info" }
        };

        private static bool _modJsonLoaded;

        private static string KnownModsJsonPath()
        {
            return System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "CascadeMenu_KnownMods.json");
        }

        private static void EnsureKnownModsJsonLoaded()
        {
            if (_modJsonLoaded) return;
            _modJsonLoaded = true;
            try
            {
                string path = KnownModsJsonPath();
                if (!System.IO.File.Exists(path))
                {
                    SaveKnownModsJson();
                    return;
                }

                string json = System.IO.File.ReadAllText(path);
                if (string.IsNullOrEmpty(json)) return;

                // Lightweight parser for {"key":"Name","key2":"[ILLEGAL] Name"} without external JSON deps
                var matches = System.Text.RegularExpressions.Regex.Matches(json, "\"([^\"]+)\"\\s*:\\s*\"([^\"]+)\"");
                for (int i = 0; i < matches.Count; i++)
                {
                    string k = matches[i].Groups[1].Value;
                    string v = matches[i].Groups[2].Value;
                    if (string.IsNullOrEmpty(k) || string.IsNullOrEmpty(v)) continue;
                    if (v.StartsWith("[ILLEGAL] ", StringComparison.OrdinalIgnoreCase))
                    {
                        IllegalModKeys.Add(k);
                        KnownModPropKeys[k] = v.Substring(10).Trim();
                    }
                    else
                    {
                        KnownModPropKeys[k] = v;
                    }
                }
            }
            catch { }
        }

        internal static void SaveKnownModsJson()
        {
            try
            {
                string path = KnownModsJsonPath();
                string dir = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("{");
                var keys = new List<string>(KnownModPropKeys.Keys);
                keys.Sort(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < keys.Count; i++)
                {
                    string k = keys[i];
                    string v = KnownModPropKeys[k];
                    if (IllegalModKeys.Contains(k)) v = "[ILLEGAL] " + v;
                    string ek = k.Replace("\\", "\\\\").Replace("\"", "\\\"");
                    string ev = v.Replace("\\", "\\\\").Replace("\"", "\\\"");
                    sb.Append("  \"").Append(ek).Append("\": \"").Append(ev).Append("\"");
                    sb.AppendLine(i < keys.Count - 1 ? "," : "");
                }
                sb.AppendLine("}");
                System.IO.File.WriteAllText(path, sb.ToString());
            }
            catch { }
        }

        internal static void SaveCustomPropToJson(string key, string displayName, bool markIllegal)
        {
            if (string.IsNullOrEmpty(key)) return;
            EnsureKnownModsJsonLoaded();
            KnownModPropKeys[key] = string.IsNullOrEmpty(displayName) ? key : displayName;
            if (markIllegal) IllegalModKeys.Add(key);
            else IllegalModKeys.Remove(key);
            SaveKnownModsJson();
            _propsCache.Clear();
        }

        internal readonly struct CustomPropEntry
        {
            public readonly string Key;
            public readonly string Value;
            public readonly string DetectedMod;
            public readonly bool IsIllegal;

            public CustomPropEntry(string key, string value, string detectedMod, bool isIllegal)
            {
                Key = key;
                Value = value;
                DetectedMod = detectedMod;
                IsIllegal = isIllegal;
            }
        }

        private sealed class CachedPropsSnapshot
        {
            public float ExpiresAt;
            public readonly List<CustomPropEntry> Entries = new List<CustomPropEntry>();
            public readonly List<string> IllegalMods = new List<string>();
            public readonly List<string> LegalMods = new List<string>();
        }

        private static readonly Dictionary<string, CachedPropsSnapshot> _propsCache =
            new Dictionary<string, CachedPropsSnapshot>(StringComparer.Ordinal);

        internal static List<CustomPropEntry> GetPlayerCustomPropEntries(NetPlayer player, bool forceRefresh = false)
        {
            EnsureKnownModsJsonLoaded();
            if (player == null) return new List<CustomPropEntry>();
            string cacheKey = !string.IsNullOrEmpty(player.UserId) ? player.UserId : ("actor_" + player.ActorNumber);
            float now = Time.unscaledTime;

            if (!forceRefresh && _propsCache.TryGetValue(cacheKey, out CachedPropsSnapshot cached) && now < cached.ExpiresAt)
            {
                return cached.Entries;
            }

            if (!_propsCache.TryGetValue(cacheKey, out cached))
            {
                cached = new CachedPropsSnapshot();
                _propsCache[cacheKey] = cached;
            }
            cached.Entries.Clear();
            cached.IllegalMods.Clear();
            cached.LegalMods.Clear();
            cached.ExpiresAt = now + 0.5f;

            try
            {
                if (!_customPropsResolved)
                {
                    _customPropsResolved = true;
                    _netPlayerGetRef = player.GetType().GetMethod("GetPlayerRef",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }

                object photonPlayer = _netPlayerGetRef != null ? _netPlayerGetRef.Invoke(player, null) : null;
                if (photonPlayer == null) return cached.Entries;

                if (_photonCustomProps == null)
                {
                    _photonCustomProps = photonPlayer.GetType().GetProperty("CustomProperties",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }

                IDictionary dict = _photonCustomProps != null ? _photonCustomProps.GetValue(photonPlayer, null) as IDictionary : null;
                if (dict == null) return cached.Entries;

                var seenIllegal = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var seenLegal = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (DictionaryEntry entry in dict)
                {
                    if (entry.Key == null) continue;
                    string k = entry.Key.ToString();
                    if (string.IsNullOrEmpty(k)) continue;
                    string v = entry.Value != null ? entry.Value.ToString() : string.Empty;

                    string modName = null;
                    bool illegal = IllegalModKeys.Contains(k);
                    if (KnownModPropKeys.TryGetValue(k, out string mapped))
                    {
                        modName = mapped;
                        if (illegal)
                        {
                            if (seenIllegal.Add(mapped)) cached.IllegalMods.Add(mapped);
                        }
                        else
                        {
                            if (seenLegal.Add(mapped)) cached.LegalMods.Add(mapped);
                        }
                    }

                    cached.Entries.Add(new CustomPropEntry(k, v, modName, illegal));
                }
            }
            catch { }

            cached.Entries.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase));
            cached.IllegalMods.Sort(StringComparer.OrdinalIgnoreCase);
            cached.LegalMods.Sort(StringComparer.OrdinalIgnoreCase);
            return cached.Entries;
        }

        internal static List<string> GetDetectedModsForPlayer(NetPlayer player, bool forceRefresh = false)
        {
            var combined = new List<string>();
            if (player == null) return combined;
            GetPlayerCustomPropEntries(player, forceRefresh);
            string cacheKey = !string.IsNullOrEmpty(player.UserId) ? player.UserId : ("actor_" + player.ActorNumber);
            if (_propsCache.TryGetValue(cacheKey, out CachedPropsSnapshot cached))
            {
                for (int i = 0; i < cached.IllegalMods.Count; i++) combined.Add("[ILLEGAL] " + cached.IllegalMods[i]);
                for (int i = 0; i < cached.LegalMods.Count; i++) combined.Add(cached.LegalMods[i]);
            }
            return combined;
        }

        internal static List<string> GetIllegalModsForPlayer(NetPlayer player, bool forceRefresh = false)
        {
            if (player == null) return new List<string>();
            GetPlayerCustomPropEntries(player, forceRefresh);
            string cacheKey = !string.IsNullOrEmpty(player.UserId) ? player.UserId : ("actor_" + player.ActorNumber);
            return _propsCache.TryGetValue(cacheKey, out CachedPropsSnapshot cached) ? cached.IllegalMods : new List<string>();
        }

        internal static List<string> GetLegalModsForPlayer(NetPlayer player, bool forceRefresh = false)
        {
            if (player == null) return new List<string>();
            GetPlayerCustomPropEntries(player, forceRefresh);
            string cacheKey = !string.IsNullOrEmpty(player.UserId) ? player.UserId : ("actor_" + player.ActorNumber);
            return _propsCache.TryGetValue(cacheKey, out CachedPropsSnapshot cached) ? cached.LegalMods : new List<string>();
        }

        internal static string PlayerCustomPropsSummary(NetPlayer player, int maxItems = 3)
        {
            List<CustomPropEntry> entries = GetPlayerCustomPropEntries(player);
            if (entries.Count == 0) return "None";

            var items = new List<string>(maxItems);
            int totalNonTutorial = 0;
            // Prioritize illegal mods first in summary!
            for (int i = 0; i < entries.Count; i++)
            {
                CustomPropEntry e = entries[i];
                if (e.Key == "didTutorial") continue;
                totalNonTutorial++;
                if (e.IsIllegal && items.Count < maxItems)
                    items.Add("[ILLEGAL] " + (e.DetectedMod ?? e.Key));
            }
            for (int i = 0; i < entries.Count; i++)
            {
                CustomPropEntry e = entries[i];
                if (e.Key == "didTutorial" || e.IsIllegal) continue;
                if (items.Count < maxItems)
                {
                    if (!string.IsNullOrEmpty(e.DetectedMod))
                        items.Add(e.DetectedMod);
                    else if (!string.IsNullOrEmpty(e.Value) && e.Value.Length <= 16)
                        items.Add(e.Key + " : " + e.Value);
                    else
                        items.Add(e.Key);
                }
            }

            if (totalNonTutorial == 0) return "None";
            string joined = string.Join(", ", items);
            if (totalNonTutorial > items.Count) joined += " +" + (totalNonTutorial - items.Count);
            return joined;
        }

        /// <summary>
        private static System.Reflection.FieldInfo _zoneEntityField;
        private static System.Reflection.PropertyInfo _zoneEntityProp;
        private static System.Reflection.FieldInfo _currentZoneField;
        private static System.Reflection.PropertyInfo _currentZoneProp;
        private static bool _zoneReflectionInit;

        /// <summary>
        /// Reads the Gorilla Tag map/zone a player's VRRig is currently inside
        /// (shown only in Cascade's Player List, never modifying the game leaderboard).
        /// </summary>
        internal static string GetPlayerZone(NetPlayer player)
        {
            if (player == null) return "Unknown";
            VRRig rig = player.IsLocal ? VRRig.LocalRig : FindRigObject(player);
            return GetRigZone(rig);
        }

        internal static string GetRigZone(VRRig rig)
        {
            if (rig == null) return "Unknown";
            try
            {
                if (!_zoneReflectionInit)
                {
                    _zoneReflectionInit = true;
                    const System.Reflection.BindingFlags flags =
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                    _zoneEntityField = typeof(VRRig).GetField("zoneEntity", flags);
                    _zoneEntityProp = typeof(VRRig).GetProperty("zoneEntity", flags);
                }

                object entity = _zoneEntityField != null ? _zoneEntityField.GetValue(rig) : _zoneEntityProp?.GetValue(rig, null);
                if (entity == null) return "Unknown";

                if (_currentZoneField == null && _currentZoneProp == null)
                {
                    const System.Reflection.BindingFlags flags =
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                    System.Type et = entity.GetType();
                    _currentZoneField = et.GetField("currentZone", flags);
                    _currentZoneProp = et.GetProperty("currentZone", flags);
                }

                object zoneVal = _currentZoneField != null ? _currentZoneField.GetValue(entity) : _currentZoneProp?.GetValue(entity, null);
                if (zoneVal == null) return "Unknown";
                return FormatZoneName(zoneVal.ToString());
            }
            catch
            {
                return "Unknown";
            }
        }

        private static string FormatZoneName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "Unknown";
            string lower = raw.Trim();
            if (string.Equals(lower, "none", StringComparison.OrdinalIgnoreCase)) return "Stump";

            var sb = new System.Text.StringBuilder(lower.Length + 4);
            for (int i = 0; i < lower.Length; i++)
            {
                char c = lower[i];
                if (c == '_' || c == '-')
                {
                    sb.Append(' ');
                    continue;
                }
                if (i > 0 && char.IsUpper(c) && char.IsLower(lower[i - 1]))
                {
                    sb.Append(' ');
                }
                sb.Append(i == 0 || sb[sb.Length - 1] == ' ' ? char.ToUpperInvariant(c) : c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Compact live summary shown under each player's name in Player List:
        /// Actor #, Zone, FPS, Color Code (0-9), Speaking, Custom Props, Master & Platform.
        /// </summary>
        private static string RowSubtitle(CascadeMenuBehaviour menu, NetPlayer player)
        {
            var parts = new List<string>(7);
            if (player != null) parts.Add("#" + player.ActorNumber);

            string zone = GetPlayerZone(player);
            if (!string.IsNullOrEmpty(zone) && zone != "Unknown") parts.Add(zone);

            int fps = GetPlayerFps(player);
            if (fps >= 0) parts.Add(fps + " FPS");

            parts.Add("Col " + ColorCode9(GetPlayerColor(player)));

            if (IsPlayerSpeaking(player)) parts.Add("SPEAKING");
            if (player != null && player.IsMasterClient) parts.Add("MASTER");

            string props = PlayerCustomPropsSummary(player, 2);
            if (props != "None") parts.Add("Props: " + props);
            else parts.Add(SafePlatform(player));

            if (menu != null && menu.Config != null && menu.Config.CosmeticCheck.Value)
            {
                CosmeticChecker.Report report = CosmeticChecker.Inspect(FindRigObject(player));
                string status = report.ShortStatus;
                if (!string.IsNullOrEmpty(status)) parts.Add(status);
            }

            return string.Join(" · ", parts);
        }

        /// <summary>Live lobby data shared by the Home room card and the Players tools.</summary>
        internal sealed class RoomSnapshot
        {
            public string Name = "home";
            public int PlayerCount;
            public string Region = "unknown";
            public string MasterName = "unknown";
            public bool LocalIsMaster;
            public string HardwareId = "unknown";
            public string VerifiedSpecials = "None";
            public string FakeSpecials = "None";
            public string FlaggedNames = "None";
            public int FlaggedCount;
            public string CustomProps = "None";
        }

        /// <summary>
        /// Reads the current Photon room. RoomName, RoomPlayerCount and
        /// CurrentRegion throw while connecting or after leaving, so only touch
        /// them once NetworkSystem says we are in and keep offline fallbacks.
        /// </summary>
        internal static RoomSnapshot ReadRoomSnapshot()
        {
            var room = new RoomSnapshot();
            NetworkSystem system = NetworkSystem.Instance;
            List<NetPlayer> players = GetPlayers();
            bool inRoom = system != null && system.InRoom;

            if (inRoom)
            {
                try
                {
                    if (!string.IsNullOrEmpty(system.RoomName)) room.Name = system.RoomName;
                    room.PlayerCount = system.RoomPlayerCount;
                    if (!string.IsNullOrEmpty(system.CurrentRegion)) room.Region = system.CurrentRegion;
                    room.MasterName = MasterName(system);
                    room.LocalIsMaster = system.IsMasterClient;
                }
                catch (Exception)
                {
                    // The room can be torn down between the InRoom check and the
                    // reads above; keep the offline fallbacks instead of crashing.
                    room = new RoomSnapshot();
                }

                if (room.PlayerCount < players.Count + 1) room.PlayerCount = players.Count + 1;
            }

            room.HardwareId = LocalHardwareId();
            room.FlaggedNames = FlaggedPlayersSummary(out room.FlaggedCount);
            room.CustomProps = RoomCustomPropSummary();
            SpecialPlayersSummary(out room.VerifiedSpecials, out room.FakeSpecials);
            return room;
        }

        /// <summary>Adds the live room card to a page (the Home tab).</summary>
        internal static void AppendRoomSection(CascadePage page)
        {
            RoomSnapshot room = ReadRoomSnapshot();
            page.Section("Room", "Live lobby information")
                .Label("Home / room", "Current Photon room", room.Name)
                .Label("Players", "In this room, including you", room.PlayerCount.ToString())
                .Label("Master client", room.LocalIsMaster ? "You are hosting this room" : "Current host", room.MasterName)
                .Label("Region", "Photon cloud region", room.Region)
                .Label("Server IP", "Photon server address", ServerAddress())
                .Label("Your HWID", "Local device ID (others are hidden)",
                    room.HardwareId.Length > 28 ? room.HardwareId.Substring(0, 24) + "..." : room.HardwareId)
                .Label("Special cosmetics", "Slot-verified staff/creator items (not server ownership)", room.VerifiedSpecials)
                .Label("Fake specials", "Claims without a matching special item", room.FakeSpecials)
                .Label("Modders flagged", "Cosmetic spoof flags (local hint)", room.FlaggedNames)
                .Label("Custom props", "Room custom properties", room.CustomProps);
        }

        internal static CascadePage BuildPlayersPage(CascadeMenuBehaviour menu)
        {
            var page = new CascadePage("Player List", "Sorted by Actor # (lowest first) · FPS, Color, Speaking & Props", CascadeIcon.Person);
            page.Tag = PlayersTag;

            NetPlayer local = LocalPlayer();
            string localName = local != null ? SafeName(local) : "Local";
            if (local == null && VRRig.LocalRig != null && !string.IsNullOrEmpty(VRRig.LocalRig.playerNameVisible))
                localName = VRRig.LocalRig.playerNameVisible;
            string localId = local != null && !string.IsNullOrEmpty(local.UserId) ? local.UserId : "unknown";
            int localActor = local != null ? local.ActorNumber : 0;
            Color localColor = VRRig.LocalRig != null ? VRRig.LocalRig.playerColor : Color.white;

            List<NetPlayer> players = GetPlayers();
            RoomSnapshot room = ReadRoomSnapshot();

            page.Section("Your identity", "Live local telemetry")
                .Label("Player name", "From your local VRRig", localName + (localActor > 0 ? "  (#" + localActor + ")" : ""))
                .LiveLabel("FPS & Color", "Your live FPS and 0-9 Gorilla color code",
                    () => LocalFps() + " FPS · Col " + ColorCode9(VRRig.LocalRig != null ? VRRig.LocalRig.playerColor : localColor),
                    () => true)
                .Label("Current zone", "Map zone your local rig is currently in", GetRigZone(VRRig.LocalRig))
                .Label("Room", "Current room and player count", room.Name + " (" + room.PlayerCount + ")")
                .Button("Your Custom Properties", "Open the full key : value viewer for your own player", "Open",
                    () =>
                    {
                        if (local != null) menu.OpenPlayerCustomPropsPage(local);
                        else menu.ShowToast("Custom Properties", "Join a room first to inspect your properties.");
                    }, false)
                .Label("Player ID", "Your unique UserId", ShortId(localId));

            string description = players.Count == 0
                ? "No other players detected. Join a public room first."
                : players.Count + (players.Count == 1
                    ? " player in room (lowest Actor # at top). Tap to open actions."
                    : " players in room (lowest Actor # at top). Tap to open actions.");

            CascadeSection list = page.Section("Player List", description);
            if (players.Count == 0)
            {
                list.Label("No players", "Nothing to list yet", "0");
            }
            else
            {
                for (int i = 0; i < players.Count; i++)
                {
                    NetPlayer player = players[i];
                    string name = SafeName(player);
                    string id = player != null && !string.IsNullOrEmpty(player.UserId) ? player.UserId : "unknown";
                    int actor = player != null ? player.ActorNumber : 0;

                    list.Player(new CascadePlayerRow(
                        "#" + actor + "  " + name,
                        RowSubtitle(menu, player),
                        id,
                        "Actor #" + actor + " · " + ShortId(id),
                        () => IsSelected(player),
                        () => IsPlayerSpeaking(player) || (menu.Config != null && menu.Config.HandTapIndicator.Value && IsHandTapping(player)),
                        () => IsMuted(id),
                        () => menu.OpenPlayerPage(player),
                        () =>
                        {
                            bool muted = ToggleMute(player);
                            menu.ShowToast(muted ? "Player muted" : "Player unmuted", name);
                        },
                        () => menu.OpenReportPage(player),
                        () => Copy(id),
                        () => Copy(name),
                        () => GetPlayerColor(player),
                        () => FindRigObject(player)));
                }
            }

            page.Section("Tools", "Refresh the list or copy session data")
                .Button("Refresh player list", "Re-scan the current room", "Refresh",
                    menu.RefreshPlayerList, false)
                .Button("Copy room info", "Clipboard: room, master, region, server and HWID", "Copy",
                    () => Copy(
                        "Home / room: " + room.Name + "\n" +
                        "Players: " + room.PlayerCount + "\n" +
                        "Master client: " + room.MasterName + "\n" +
                        "Region: " + room.Region + "\n" +
                        "Server IP: " + ServerAddress() + "\n" +
                        "HWID: " + room.HardwareId + "\n" +
                        "Special cosmetics: " + room.VerifiedSpecials + "\n" +
                        "Fake specials: " + room.FakeSpecials + "\n" +
                        "Modders flagged: " + room.FlaggedNames + "\n" +
                        "Custom props: " + room.CustomProps), false)
                .Button("Copy your player ID", "Useful when reporting", "Copy",
                    () => Copy(localId), false)
                .Button("Copy your HWID", "Local device identifier", "Copy",
                    () => Copy(room.HardwareId), false);

            return page;
        }

        /// <summary>Short form of a UserId for the list (full ID is on the player page).</summary>
        internal static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "unknown";
            return id.Length <= 12 ? id : id.Substring(0, 8) + "..." + id.Substring(id.Length - 3);
        }

        internal static CascadePage BuildPlayerPage(CascadeMenuBehaviour menu, NetPlayer player)
        {
            string name = SafeName(player);
            string id = player != null && !string.IsNullOrEmpty(player.UserId) ? player.UserId : "unknown";
            RigContainer container = FindRig(id);
            bool muted = container != null && container.IsMutedFor(RigContainer.MuteReason.Manual);
            float distance = DistanceTo(player);
            bool master = player != null && player.IsMasterClient;
            string modderSummary;
            bool flagged = IsFlaggedModder(player, out modderSummary);
            string propStatus = CustomPropStatus(player);
            Color rigColor = GetPlayerColor(player);

            var page = new CascadePage(name, "Player actions", CascadeIcon.Person);
            page.Tag = PlayerTag(id);
            page.HiddenFromSidebar = true;

            page.Section("Navigation", string.Empty)
                .Button("Close Player View", "Return to the player list", "Close", () => menu.GoBack(), true);

            page.Section("Live preview", "Their actual gorilla, rendered from the live rig")
                .Portrait(name, SafePlatform(player), () => FindRigObject(player), () => GetPlayerColor(player));

            int playerFps = GetPlayerFps(player);
            int actorNum = player != null ? player.ActorNumber : 0;
            bool speaking = IsPlayerSpeaking(player);
            List<CustomPropEntry> allProps = GetPlayerCustomPropEntries(player);
            List<string> detectedMods = GetDetectedModsForPlayer(player);
            string customPropsText = PlayerCustomPropsSummary(player, 4);

            page.Section("Session & Telemetry", "Live player stats and identity")
                .Label("Name", "Nickname", name)
                .Label("Actor number", "Lower actor # joined earlier", "#" + actorNum)
                .Label("Zone", "Current Gorilla Tag map zone", GetPlayerZone(player))
                .Label("FPS", "Reported frame rate from VRRig", playerFps >= 0 ? playerFps + " FPS" : "unknown")
                .Label("Speaking", "Live voice activity", speaking ? "Speaking" : "Quiet")
                .Label("Color code", "0-9 Gorilla color code & hex", ColorCode9(rigColor) + "  (" + ColorHex(rigColor) + ")")
                .Label("Platform", "Reported platform", SafePlatform(player))
                .Label("Distance", "Distance from your position", distance >= 0f ? distance.ToString("0.0") + " m" : "unknown")
                .Label("Master client", "Current room host", master ? "Yes" : "No")
                .Label("Player ID", "Tap Copy below for full UserId", ShortId(id));

            page.Section("Custom Properties & Mods", "Inspect all Photon custom properties on this player")
                .Label("Detected mods", "Matched from known Gorilla Tag mod keys",
                    detectedMods.Count > 0 ? string.Join(", ", detectedMods) : "None detected")
                .Label("Summary (" + allProps.Count + ")", "Preview of custom properties", customPropsText)
                .Button("Open Custom Properties", "Open the full key : value property viewer for " + name, "Open",
                    () => menu.OpenPlayerCustomPropsPage(player), true);

            page.Section("Status & Checks", "Local client state")
                .Label("Modder check", "Cosmetic spoof flags (local hint)", flagged ? modderSummary : "Clean / unknown")
                .Label("Muted", "Local manual mute", muted ? "Yes" : "No")
                .Label("Hands", "Live hand-tap state", IsHandTapping(player) ? "Tapping" : "Idle")
                .Label("Selected", "Highlighted in Player List", IsSelected(player) ? "Yes" : "No");

            CascadeSection actions = page.Section("Player actions", "Client-side tools aimed at this player");
            actions.Button("Spectate player", "Live first- or third-person view of their gorilla", "Spectate",
                () => menu.StartSpectating(player), false);
            actions.Button("Teleport to player", "Move next to them without disabling collisions", "Teleport",
                () =>
                {
                    bool ok = TeleportTo(player);
                    menu.ShowToast("Teleport", ok ? "Moved next to " + name : "That player's rig is not ready.");
                }, false);
            actions.Button("Noclip to player", "Ghost-teleport through walls to their position", "Noclip",
                () =>
                {
                    bool ok = NoclipTo(player);
                    menu.ShowToast("Noclip", ok ? "Moved next to " + name : "That player's rig is not ready.");
                }, false);
            actions.Button("Custom prop (" + propStatus + ")", "Local-only prop in their hand; tap to cycle", "Cycle",
                () =>
                {
                    string now = CycleCustomProp(player);
                    menu.ShowToast("Custom prop", now == "Not ready"
                        ? "That player's rig is not ready."
                        : now + " on " + name + " (local only).");
                    menu.RefreshPlayerPage(player);
                }, false);
            if (propStatus != "Off")
            {
                actions.Button("Remove custom prop", "Delete the local prop from their hand", "Remove",
                    () =>
                    {
                        RemoveCustomProp(player);
                        menu.ShowToast("Custom prop", "Removed from " + name + ".");
                        menu.RefreshPlayerPage(player);
                    }, false);
            }

            if (menu.Config == null || menu.Config.CosmeticCheck.Value)
            {
                BuildCosmeticSection(menu, page, player);
            }

            page.Section("Moderation", "Uses the same backend as the scoreboard; actions stay on this page")
                .Button(muted ? "Unmute player" : "Mute player", "Voice and hand-tap audio",
                    muted ? "Unmute" : "Mute",
                    () =>
                    {
                        bool nowMuted = ToggleMute(player);
                        menu.ShowToast(nowMuted ? "Player muted" : "Player unmuted", name);
                        menu.RefreshPlayerPage(player);
                    }, true)
                .Button("Report: hate speech", "Submit through Gorilla Tag's report API", "Report",
                    () => SubmitReport(menu, player, GorillaPlayerLineButton.ButtonType.HateSpeech), false)
                .Button("Report: cheating", "Submit through Gorilla Tag's report API", "Report",
                    () => SubmitReport(menu, player, GorillaPlayerLineButton.ButtonType.Cheating), false)
                .Button("Report: toxicity", "Submit through Gorilla Tag's report API", "Report",
                    () => SubmitReport(menu, player, GorillaPlayerLineButton.ButtonType.Toxicity), false);

            page.Section("Visual", "Gun library integration")
                .Button("Mark with gun", "Draw the target ring on this player", "Mark",
                    () =>
                    {
                        VRRig rig = FindRigObject(player);
                        if (rig == null)
                        {
                            menu.ShowToast("Mark", "Player rig is not ready yet.");
                            return;
                        }
                        GunLibController.MarkTarget(rig);
                        Select(id);
                        menu.ShowToast("Marked", name + " is now marked.");
                    }, false)
                .Button("Select in gun", "Make this the gun's selected target", "Select",
                    () =>
                    {
                        VRRig rig = FindRigObject(player);
                        if (rig == null)
                        {
                            menu.ShowToast("Select", "Player rig is not ready yet.");
                            return;
                        }
                        GunLibController.SelectTarget(rig);
                        Select(id);
                        menu.ShowToast("Selected", name);
                    }, false);

            page.Section("Utility", "Clipboard and movement")
                .Button("Copy name", "Copy the nickname", "Copy", () => Copy(name), false)
                .Button("Copy player ID", "Copy the UserId", "Copy", () => Copy(id), false)
                .Button("Teleport to player", "Move next to them (client-side)", "Go",
                    () =>
                    {
                        bool moved = TeleportTo(player);
                        menu.ShowToast("Teleport", moved ? "Moved next to " + name : "Player rig is not ready");
                    }, false);

            return page;
        }

        /// <summary>
        /// Dedicated Custom Properties viewer UI for any player.
        /// Shows detected mods and every custom property in clean "key : value" format.
        /// </summary>
        internal static CascadePage BuildCustomPropsPage(CascadeMenuBehaviour menu, NetPlayer player)
        {
            string name = SafeName(player);
            string id = player != null && !string.IsNullOrEmpty(player.UserId) ? player.UserId : "unknown";
            int actor = player != null ? player.ActorNumber : 0;

            List<CustomPropEntry> entries = GetPlayerCustomPropEntries(player, true);
            List<string> illegalMods = GetIllegalModsForPlayer(player, false);
            List<string> legalMods = GetLegalModsForPlayer(player, false);

            var page = new CascadePage("Custom Properties", name + " (#" + actor + ") · " + entries.Count + " properties", CascadeIcon.Sliders);
            page.Tag = CustomPropsTag(id);
            page.HiddenFromSidebar = true;

            page.Section("Navigation", "Saved mod database: BepInEx/config/CascadeMenu_KnownMods.json")
                .Button("Back", "Return to previous page", "Back", () => menu.GoBack(), false)
                .Button("Refresh properties", "Re-read live Photon custom properties", "Refresh",
                    () => menu.OpenPlayerCustomPropsPage(player), true)
                .Button("Save database to JSON", "Write known & illegal mod keys to CascadeMenu_KnownMods.json", "Save JSON", () =>
                {
                    SaveKnownModsJson();
                    menu.ShowToast("Saved", "BepInEx/config/CascadeMenu_KnownMods.json updated.");
                }, false)
                .Button("Copy all raw properties", "Copy every key : value line to clipboard", "Copy All", () =>
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine(name + " (#" + actor + ") Custom Properties:");
                    for (int i = 0; i < entries.Count; i++)
                    {
                        CustomPropEntry e = entries[i];
                        string modTag = !string.IsNullOrEmpty(e.DetectedMod)
                            ? (e.IsIllegal ? " [ILLEGAL: " + e.DetectedMod + "]" : " [" + e.DetectedMod + "]")
                            : "";
                        sb.AppendLine(e.Key + " : " + (string.IsNullOrEmpty(e.Value) ? "(empty)" : e.Value) + modTag);
                    }
                    Copy(sb.ToString());
                    menu.ShowToast("Custom Properties", "Copied " + entries.Count + " entries.");
                }, false);

            CascadeSection illegalSec = page.Section("Illegal / Cheat Menus (" + illegalMods.Count + ")",
                "Flagged menus: Void, Orbit, Vivid, Atlas, Obsidian, Genesis, etc.");
            if (illegalMods.Count == 0)
            {
                illegalSec.Label("None detected", "No illegal menu keys found on this player", "Clean");
            }
            else
            {
                for (int i = 0; i < illegalMods.Count; i++)
                {
                    illegalSec.Label(illegalMods[i], "Flagged illegal mod menu key", "ILLEGAL");
                }
            }

            CascadeSection legalSec = page.Section("Known Mods (" + legalMods.Count + ")",
                "Recognized Gorilla Tag community mods");
            if (legalMods.Count == 0)
            {
                legalSec.Label("None detected", "No recognized legal mod keys found", "0");
            }
            else
            {
                for (int i = 0; i < legalMods.Count; i++)
                {
                    legalSec.Label(legalMods[i], "Recognized mod key", "Mod");
                }
            }

            CascadeSection allSec = page.Section("All Raw Custom Properties (" + entries.Count + ")",
                "Every raw key : value pair (unknown keys can be saved to JSON)");
            if (entries.Count == 0)
            {
                allSec.Label("None", "This player has no custom properties set", "0");
            }
            else
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    CustomPropEntry e = entries[i];
                    string val = string.IsNullOrEmpty(e.Value) ? "(empty)" : e.Value;
                    string tag = e.IsIllegal
                        ? "[ILLEGAL: " + (e.DetectedMod ?? e.Key) + "]  "
                        : (!string.IsNullOrEmpty(e.DetectedMod) ? "[Mod: " + e.DetectedMod + "]  " : "[Raw / Unknown]  ");
                    string desc = tag + e.Key + " : " + val;
                    string shortVal = val.Length > 24 ? val.Substring(0, 21) + "..." : val;

                    if (string.IsNullOrEmpty(e.DetectedMod) && e.Key != "didTutorial")
                    {
                        allSec.Button(e.Key + " : " + shortVal, desc + " — tap Save to add to JSON", "Save", () =>
                        {
                            SaveCustomPropToJson(e.Key, e.Key, false);
                            menu.ShowToast("Saved to JSON", e.Key + " added to CascadeMenu_KnownMods.json");
                            menu.OpenPlayerCustomPropsPage(player);
                        }, false);
                    }
                    else
                    {
                        allSec.Button(e.Key + " : " + shortVal, desc, "Copy", () =>
                        {
                            Copy(e.Key + " : " + val);
                            menu.ShowToast("Copied", e.Key + " : " + val);
                        }, false);
                    }
                }
            }

            return page;
        }

        /// <summary>
        /// Worn cosmetics with their category and a local spoof verdict. This is
        /// read-only: the game only tells us the item names in each slot, so
        /// "spoof" means unknown, duplicated or in the wrong slot.
        /// </summary>
        private static void BuildCosmeticSection(CascadeMenuBehaviour menu, CascadePage page, NetPlayer player)
        {
            VRRig rig = FindRigObject(player);
            CosmeticChecker.Report report = CosmeticChecker.Inspect(rig);

            CascadeSection section = page.Section("Cosmetics", "Worn items, categories and local spoof check");
            section.Label("Spoof check", "Unknown, duplicated or misplaced cosmetics", report.Summary);

            if (!report.Available)
            {
                section.Label("Not synced", "That player's cosmetics have not arrived yet", "-");
                return;
            }

            if (report.Total == 0)
            {
                section.Label("No cosmetics", "This rig is wearing nothing detectable", "0");
                return;
            }

            const int maxRows = 24;
            int shown = Mathf.Min(report.Items.Count, maxRows);
            for (int i = 0; i < shown; i++)
            {
                CosmeticChecker.WornItem item = report.Items[i];
                section.Label(
                    item.DisplayName,
                    item.SlotName + " - " + CosmeticChecker.CategoryName(item.Category),
                    item.FlagText);
            }

            if (report.Items.Count > shown)
            {
                section.Label("More cosmetics", "Copy the full list for everything", "+" + (report.Items.Count - shown));
            }

            section.Button("Copy cosmetic list", "Clipboard: slot, name and flags", "Copy",
                () =>
                {
                    Copy(CosmeticChecker.FormatForClipboard(report));
                    menu.ShowToast("Cosmetics copied", report.Total + " item(s).");
                }, false);
        }

        internal static CascadePage BuildReportPage(CascadeMenuBehaviour menu, NetPlayer player)
        {
            string name = SafeName(player);
            string id = player != null && !string.IsNullOrEmpty(player.UserId) ? player.UserId : "unknown";

            var page = new CascadePage("Report " + name, "Choose a reason", CascadeIcon.Info);
            page.Tag = ReportTag(id);
            page.HiddenFromSidebar = true;

            page.Section("Report reason", "Sent through Gorilla Tag's scoreboard")
                .Button("Hate speech", "Slurs or hateful language", "Report",
                    () => Submit(menu, player, GorillaPlayerLineButton.ButtonType.HateSpeech), false)
                .Button("Cheating", "Exploits, mods or cheating", "Report",
                    () => Submit(menu, player, GorillaPlayerLineButton.ButtonType.Cheating), false)
                .Button("Toxicity", "Harassment or toxic behaviour", "Report",
                    () => Submit(menu, player, GorillaPlayerLineButton.ButtonType.Toxicity), false);

            page.Section("Player", "Who you are reporting")
                .Label("Name", "Nickname", name)
                .Label("Player ID", "Unique UserId", id)
                .Button("Back to player", "Return to the action page", "Back",
                    () => menu.OpenPlayerPage(player), false);

            return page;
        }

        private static void Submit(
            CascadeMenuBehaviour menu,
            NetPlayer player,
            GorillaPlayerLineButton.ButtonType reason)
        {
            bool sent = Report(player, reason);
            menu.ShowToast(
                sent ? "Report sent" : "Report unavailable",
                sent ? SafeName(player) + " was reported." : "Open the in-game scoreboard, then try again.");
        }

        private static void SubmitReport(
            CascadeMenuBehaviour menu,
            NetPlayer player,
            GorillaPlayerLineButton.ButtonType reason)
        {
            bool sent = Report(player, reason);
            menu.ShowToast(sent ? "Report sent" : "Report unavailable",
                sent ? SafeName(player) + " was reported." : "Open the in-game scoreboard, then try again.");
        }
    }
}

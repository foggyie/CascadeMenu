using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GorillaLocomotion;
using GorillaNetworking;
using UnityEngine;
using CascadeMenu.UI;

namespace CascadeMenu.Mods
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

        internal static string PlayerTag(string userId)
        {
            return PlayerTagPrefix + (userId ?? "unknown");
        }

        internal static string ReportTag(string userId)
        {
            return ReportTagPrefix + (userId ?? "unknown");
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

            result.Sort((a, b) => string.Compare(SafeName(a), SafeName(b), StringComparison.OrdinalIgnoreCase));
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

            // Fallback for the short window where the cache has not caught up
            // with the game manager's roster entry. Called through reflection
            // because GorillaGameManager inherits a Photon type that this project
            // deliberately does not reference.
            if (player != null)
            {
                try
                {
                    ResolveGameModeApi();
                    if (_staticFindRigMethod != null)
                    {
                        VRRig rig = _staticFindRigMethod.Invoke(null, new object[] { player }) as VRRig;
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

        // -----------------------------------------------------------------
        // Appearance
        // -----------------------------------------------------------------

        private static FieldInfo _playerColorField;
        private static PropertyInfo _playerColorProperty;
        private static bool _playerColorResolved;

        private static void ResolvePlayerColor()
        {
            if (_playerColorResolved) return;
            _playerColorResolved = true;

            Type type = typeof(VRRig);
            _playerColorField = type.GetField(
                "playerColor",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (_playerColorField != null && _playerColorField.FieldType != typeof(Color))
                _playerColorField = null;

            _playerColorProperty = type.GetProperty(
                "playerColor",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (_playerColorProperty != null && _playerColorProperty.PropertyType != typeof(Color))
                _playerColorProperty = null;
        }

        /// <summary>Live gorilla color for a remote rig, with safe fallbacks.</summary>
        internal static Color GetRigColor(VRRig rig)
        {
            if (rig == null) return Color.white;

            ResolvePlayerColor();
            try
            {
                if (_playerColorField != null)
                    return (Color)_playerColorField.GetValue(rig);
                if (_playerColorProperty != null)
                    return (Color)_playerColorProperty.GetValue(rig, null);
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

        private static Type _photonType;

        private static bool _photonTypeSearched;

        /// <summary>
        /// Photon server address (usually IP:port), read through reflection so the
        /// project does not need a hard reference to PhotonUnityNetworking.
        /// </summary>
        internal static string ServerAddress()
        {
            try
            {
                Type type = PhotonType();
                if (type == null) return "unknown";
                PropertyInfo property = type.GetProperty("ServerAddress", BindingFlags.Public | BindingFlags.Static);
                string address = property != null ? property.GetValue(null, null) as string : null;
                return string.IsNullOrEmpty(address) ? "offline" : address;
            }
            catch (Exception)
            {
                return "unknown";
            }
        }

        private static Type PhotonType()
        {
            if (_photonType != null || _photonTypeSearched) return _photonType;
            _photonTypeSearched = true;
            try
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    Type type = assemblies[i].GetType("Photon.Pun.PhotonNetwork", false);
                    if (type != null)
                    {
                        _photonType = type;
                        break;
                    }
                }
            }
            catch (Exception)
            {
                // Reflection is only a convenience here; the UI falls back to
                // "unknown" when the assembly is not loaded yet.
            }
            return _photonType;
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
                PropertyInfo property = typeof(RoomConfig).GetProperty(
                    "CustomProps", BindingFlags.Public | BindingFlags.Instance);
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
            return props.Count + ": " + string.Join(", ", props.GetRange(0, shown).ToArray());
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
            string text = string.Join(", ", names.ToArray());
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
                                 string.Join(", ", verifiedRoles.ToArray()) + ")");
                }
                if (fakeRoles.Count > 0)
                {
                    fakes.Add(SafeName(player) + " (" +
                              string.Join(", ", fakeRoles.ToArray()) + ")");
                }
            }

            verifiedText = verified.Count == 0
                ? "None"
                : verified.Count + ": " + string.Join(", ", TruncateList(verified, 3).ToArray());
            fakeText = fakes.Count == 0
                ? "None"
                : fakes.Count + ": " + string.Join(", ", TruncateList(fakes, 3).ToArray());
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
        /// Sends a real tag through the game mode. The master client applies it,
        /// so the tag survives after the menu closes (unlike the old local-only
        /// <c>PlayTaggedEffect</c> call).
        ///
        /// A master client can call <c>HitPlayer</c> directly, which tags without
        /// a distance check. Other clients use the same RPC a hand tag uses, so
        /// the server still validates range.
        ///
        /// The game-mode API is reached through reflection because
        /// GorillaGameManager inherits a Photon type that this project does not
        /// reference directly.
        /// </summary>
        internal static bool TagPlayer(NetPlayer player)
        {
            if (player == null) return false;

            NetworkSystem system = NetworkSystem.Instance;
            NetPlayer local = system != null ? system.LocalPlayer : null;
            if (system == null || !system.InRoom || local == null || player.UserId == local.UserId)
                return false;

            ResolveGameModeApi();
            if (_gameManagerInstanceProperty == null) return false;

            object manager;
            try
            {
                manager = _gameManagerInstanceProperty.GetValue(null, null);
            }
            catch (Exception)
            {
                return false;
            }
            if (manager == null) return false;

            VRRig targetRig = FindRigObject(player);

            // Non-master tags go through the game's RPC, and the master's
            // anti-cheat check reports tags sent from far away. Only send when
            // the server will accept it; the master path is unrestricted.
            if (!system.IsMasterClient)
            {
                VRRig localRig = VRRig.LocalRig;
                if (localRig == null || targetRig == null) return false;
                if (Vector3.Distance(localRig.transform.position, targetRig.transform.position) > 5.5f)
                {
                    Plugin.Log?.LogInfo("Cascade tag skipped: " + SafeName(player) + " is outside the server tag range.");
                    return false;
                }
            }

            try
            {
                // Keep the local game-mode event/cooldown in sync with a hand tag.
                if (_localTagMethod != null)
                    _localTagMethod.Invoke(manager, new object[] { player, local, false, false });

                if (system.IsMasterClient)
                {
                    // Direct, server-authoritative tag that ignores range.
                    if (_hitPlayerMethod != null)
                        _hitPlayerMethod.Invoke(manager, new object[] { player });
                }
                else
                {
                    // Same RPC the game sends for a hand tag. The master validates
                    // that the tagger is actually near the target.
                    if (_reportTagMethod != null)
                        _reportTagMethod.Invoke(null, new object[] { player });
                }
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogWarning("Cascade tag failed for " + SafeName(player) + ": " + exception.Message);
                return false;
            }

            if (targetRig != null) GunLibController.MarkTarget(targetRig);
            Select(player.UserId);
            return true;
        }

        /// <summary>
        /// Tags everyone else in the room through the same real game-mode path.
        /// Returns how many players the request was sent for.
        /// </summary>
        internal static int TagAll()
        {
            List<NetPlayer> players = GetPlayers();
            int tagged = 0;
            for (int i = 0; i < players.Count; i++)
            {
                if (TagPlayer(players[i])) tagged++;
            }
            return tagged;
        }

        // -----------------------------------------------------------------
        // Game-mode API (reflection)
        // -----------------------------------------------------------------

        private static PropertyInfo _gameManagerInstanceProperty;
        private static MethodInfo _localTagMethod;
        private static MethodInfo _hitPlayerMethod;
        private static MethodInfo _reportTagMethod;
        private static MethodInfo _staticFindRigMethod;
        private static bool _gameModeApiResolved;

        private static void ResolveGameModeApi()
        {
            if (_gameModeApiResolved) return;
            _gameModeApiResolved = true;

            try
            {
                Assembly assembly = typeof(GorillaTagger).Assembly;
                Type managerType = assembly.GetType("GorillaGameManager");
                if (managerType != null)
                {
                    _gameManagerInstanceProperty = managerType.GetProperty(
                        "instance",
                        BindingFlags.Public | BindingFlags.Static);
                    _localTagMethod = managerType.GetMethod(
                        "LocalTag",
                        BindingFlags.Public | BindingFlags.Instance,
                        null,
                        new[] { typeof(NetPlayer), typeof(NetPlayer), typeof(bool), typeof(bool) },
                        null);
                    _hitPlayerMethod = managerType.GetMethod(
                        "HitPlayer",
                        BindingFlags.Public | BindingFlags.Instance,
                        null,
                        new[] { typeof(NetPlayer) },
                        null);
                    _staticFindRigMethod = managerType.GetMethod(
                        "StaticFindRigForPlayer",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(NetPlayer) },
                        null);
                }

                Type gameModeType = assembly.GetType("GorillaGameModes.GameMode");
                if (gameModeType != null)
                {
                    _reportTagMethod = gameModeType.GetMethod(
                        "ReportTag",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(NetPlayer) },
                        null);
                }
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogWarning("Cascade could not resolve the game-mode tag API: " + exception.Message);
            }
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

            local.TeleportTo(target, local.transform.rotation, false, false);

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

        /// <summary>
        /// Platform plus a compact cosmetic check marker. The full report lives on
        /// the player's page; this is only a hint that opens with the row.
        /// </summary>
        private static string RowSubtitle(CascadeMenuBehaviour menu, NetPlayer player)
        {
            string subtitle = SafePlatform(player);
            if (player != null && player.IsMasterClient) subtitle += " - MASTER";
            if (menu == null || menu.Config == null || !menu.Config.CosmeticCheck.Value) return subtitle;

            CosmeticChecker.Report report = CosmeticChecker.Inspect(FindRigObject(player));
            string status = report.ShortStatus;
            return string.IsNullOrEmpty(status) ? subtitle : subtitle + " - " + status;
        }

        internal static CascadePage BuildPlayersPage(CascadeMenuBehaviour menu)
        {
            var page = new CascadePage("Players", "Live room list and player actions", CascadeIcon.Person);
            page.Tag = PlayersTag;

            NetPlayer local = LocalPlayer();
            string localName = local != null ? SafeName(local) : "Local";
            if (local == null && VRRig.LocalRig != null && !string.IsNullOrEmpty(VRRig.LocalRig.playerNameVisible))
                localName = VRRig.LocalRig.playerNameVisible;
            string localId = local != null && !string.IsNullOrEmpty(local.UserId) ? local.UserId : "unknown";

            List<NetPlayer> players = GetPlayers();
            NetworkSystem system = NetworkSystem.Instance;
            bool inRoom = system != null && system.InRoom;

            // RoomPlayerCount/RoomName/CurrentRegion read Photon's current room
            // internally and throw while the game is still connecting or after
            // leaving a room. Only touch them once NetworkSystem says we are in.
            string roomName = "home";
            int roomCount = 0;
            string region = "unknown";
            string masterName = "unknown";
            bool localMaster = false;
            if (inRoom)
            {
                try
                {
                    if (!string.IsNullOrEmpty(system.RoomName)) roomName = system.RoomName;
                    roomCount = system.RoomPlayerCount;
                    if (!string.IsNullOrEmpty(system.CurrentRegion)) region = system.CurrentRegion;
                    masterName = MasterName(system);
                    localMaster = system.IsMasterClient;
                }
                catch (Exception)
                {
                    // The room can be torn down between the InRoom check and the
                    // reads above; keep the offline fallbacks instead of crashing.
                    roomName = "home";
                    roomCount = 0;
                    region = "unknown";
                    masterName = "unknown";
                    localMaster = false;
                }

                if (roomCount < players.Count + 1) roomCount = players.Count + 1;
            }
            string hwid = LocalHardwareId();
            int flaggedCount;
            string flaggedNames = FlaggedPlayersSummary(out flaggedCount);
            string customProps = RoomCustomPropSummary();

            string verifiedSpecials;
            string fakeSpecials;
            SpecialPlayersSummary(out verifiedSpecials, out fakeSpecials);

            page.Section("Room", "Live lobby information")
                .Label("Home / room", "Current Photon room", roomName)
                .Label("Players", "In this room, including you", roomCount.ToString())
                .Label("Master client", localMaster ? "You are hosting this room" : "Current host", masterName)
                .Label("Region", "Photon cloud region", region)
                .Label("Server IP", "Photon server address", ServerAddress())
                .Label("Your HWID", "Local device ID (others are hidden)", hwid.Length > 28 ? hwid.Substring(0, 24) + "..." : hwid)
                .Label("Special cosmetics", "Slot-verified staff/creator items (not server ownership)", verifiedSpecials)
                .Label("Fake specials", "Claims without a matching special item", fakeSpecials)
                .Label("Modders flagged", "Cosmetic spoof flags (local hint)", flaggedNames)
                .Label("Custom props", "Room custom properties", customProps);

            page.Section("Your identity", "Copied with the room summary")
                .Label("Player name", "From the local VRRig", localName)
                .Label("Player ID", "Your unique UserId", localId);

            Plugin.Log?.LogInfo("Cascade player list: " + players.Count + " other player(s) in room \"" +
                                roomName + "\", master " + masterName + ", " + flaggedCount + " flagged.");
            string description = players.Count == 0
                ? "No other players detected. Join a public room first."
                : players.Count + (players.Count == 1
                    ? " player in this room. Tap a name to open actions."
                    : " players in this room. Tap a name to open actions.");

            CascadeSection list = page.Section("Players in room", description);
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

                    list.Player(new CascadePlayerRow(
                        name,
                        RowSubtitle(menu, player),
                        id,
                        "ID " + ShortId(id),
                        () => IsSelected(player),
                        () => menu.Config != null && menu.Config.HandTapIndicator.Value && IsHandTapping(player),
                        () => IsMuted(id),
                        () => menu.OpenPlayerPage(player),
                        () =>
                        {
                            // The row polls IsMuted, so no page rebuild is needed here:
                            // the list keeps its scroll position and the button relabels.
                            bool muted = ToggleMute(player);
                            menu.ShowToast(muted ? "Player muted" : "Player unmuted", name);
                        },
                        () => menu.OpenReportPage(player),
                        () => Copy(id),
                        () => Copy(name),
                        () => GetPlayerColor(player)));
                }
            }

            page.Section("Tools", "Refresh the list or copy session data")
                .Button("Refresh player list", "Re-scan the current room", "Refresh",
                    menu.RefreshPlayerList, false)
                .Button("Tag all players", "Send a real tag for everyone in the room", "Tag all",
                    () =>
                    {
                        int tagged = TagAll();
                        menu.ShowToast("Tag all", tagged == 0
                            ? "No tags sent - no players or outside tag range."
                            : "Sent tags for " + tagged + " player(s).");
                    }, false)
                .Button("Copy room info", "Clipboard: room, master, region, server and HWID", "Copy",
                    () => Copy(
                        "Home / room: " + roomName + "\n" +
                        "Players: " + roomCount + "\n" +
                        "Master client: " + masterName + "\n" +
                        "Region: " + region + "\n" +
                        "Server IP: " + ServerAddress() + "\n" +
                        "HWID: " + hwid + "\n" +
                        "Special cosmetics: " + verifiedSpecials + "\n" +
                        "Fake specials: " + fakeSpecials + "\n" +
                        "Modders flagged: " + flaggedNames + "\n" +
                        "Custom props: " + customProps), false)
                .Button("Copy your player ID", "Useful when reporting", "Copy",
                    () => Copy(localId), false)
                .Button("Copy your HWID", "Local device identifier", "Copy",
                    () => Copy(hwid), false);

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

            page.Section("Player", "Session information")
                .Label("Name", "Nickname", name)
                .Label("Player ID", "Tap Copy for the full UserId", id)
                .Label("Platform", "Report platform", SafePlatform(player))
                .Label("Distance", "From your position", distance >= 0f ? distance.ToString("0.0") + " m" : "unknown")
                .Label("Master client", "Current room host", master ? "Yes" : "No")
                .Label("Gorilla color", "Live tint shown next to them in the list", ColorHex(rigColor))
                .Label("Modder check", "Cosmetic spoof flags (local hint)", flagged ? modderSummary : "Clean / unknown")
                .Label("HWID", "Only your own device ID is exposed", "hidden")
                .Label("Muted", "Local manual mute", muted ? "Yes" : "No")
                .Label("Hands", "Live hand-tap state", IsHandTapping(player) ? "Tapping" : "Idle")
                .Label("Selected", "Highlighted in the player list", IsSelected(player) ? "Yes" : "No");

            CascadeSection actions = page.Section("Player actions", "Client-side tools aimed at this player");
            actions.Button("Tag with tag gun", "Send a real tag through the game mode", "Tag",
                () =>
                {
                    bool ok = TagPlayer(player);
                    menu.ShowToast("Tag gun", ok
                        ? name + " tagged."
                        : "Rig not ready, or they are outside tag range.");
                }, true);
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

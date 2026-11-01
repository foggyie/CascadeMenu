using System;
using System.Collections.Generic;
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
            if (system == null) return result;

            NetPlayer[] players = system.PlayerListOthers;
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
            return container != null ? container.Rig : null;
        }

        private static float DistanceTo(NetPlayer player)
        {
            RigContainer container = FindRig(player != null ? player.UserId : null);
            GTPlayer local = GTPlayer.Instance;
            if (container == null || local == null) return -1f;
            return Vector3.Distance(container.transform.position, local.transform.position);
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
            RigContainer container = FindRig(player != null ? player.UserId : null);
            GTPlayer local = GTPlayer.Instance;
            if (container == null || container.Rig == null || local == null) return false;

            Vector3 target = container.transform.position;
            Vector3 offset = local.transform.position - target;
            offset.y = 0f;
            if (offset.sqrMagnitude < 0.01f) offset = -local.transform.forward;
            target += offset.normalized * 1.2f;

            local.TeleportTo(target, local.transform.rotation, false, false);
            return true;
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

            page.Section("Local player", "Your identity in this session")
                .Label("Player name", "From the local VRRig", localName)
                .Label("Player ID", "Your unique UserId", localId);

            List<NetPlayer> players = GetPlayers();
            Plugin.Log?.LogInfo("Cascade player list: " + players.Count + " other player(s) in room \"" +
                                (NetworkSystem.Instance != null ? NetworkSystem.Instance.RoomName : "?") + "\".");
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
                        () => Copy(name)));
                }
            }

            page.Section("Tools", "Refresh the list or copy your own ID")
                .Button("Refresh player list", "Re-scan the current room", "Refresh",
                    menu.RefreshPlayerList, false)
                .Button("Copy your player ID", "Useful when reporting", "Copy",
                    () => Copy(localId), false);

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

            var page = new CascadePage(name, "Player actions", CascadeIcon.Person);
            page.Tag = PlayerTag(id);
            page.HiddenFromSidebar = true;

            page.Section("Player", "Session information")
                .Label("Name", "Nickname", name)
                .Label("Player ID", "Tap Copy for the full UserId", id)
                .Label("Platform", "Report platform", SafePlatform(player))
                .Label("Distance", "From your position", distance >= 0f ? distance.ToString("0.0") + " m" : "unknown")
                .Label("Muted", "Local manual mute", muted ? "Yes" : "No")
                .Label("Hands", "Live hand-tap state", IsHandTapping(player) ? "Tapping" : "Idle")
                .Label("Selected", "Highlighted in the player list", IsSelected(player) ? "Yes" : "No");

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

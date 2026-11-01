using System;
using System.Collections.Generic;
using System.Text;
using GorillaNetworking;
using UnityEngine;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Reads the cosmetics a remote rig is currently wearing and turns them into
    /// a readable report:
    ///
    /// - every slot with its category (Hat, Face, Paw, TagEffect, ...)
    /// - flags for items that are not in the local catalog (removed/spoofed),
    ///   items that sit in a slot their category does not belong to,
    ///   duplicate copies of the same item and known special/creator cosmetics.
    ///
    /// Everything is read-only. Nothing is sent to the server.
    /// </summary>
    internal static class CosmeticChecker
    {
        internal sealed class WornItem
        {
            public int SlotIndex;
            public string SlotName;
            public string ItemName;
            public string DisplayName;
            public CosmeticsController.CosmeticCategory Category;
            public Sprite Icon;
            public bool Known;
            public bool ClaimedSpecial;
            public bool Special;
            public bool SpecialVerified;
            public bool FakeSpecial;
            public string Role;
            public bool SlotMismatch;
            public bool Duplicate;
            public bool Holdable;

            public bool Flagged
            {
                get { return !Known || FakeSpecial || SlotMismatch || Duplicate; }
            }

            public string FlagText
            {
                get
                {
                    if (FakeSpecial) return "fake special";
                    if (!Known) return "unknown";
                    if (SlotMismatch) return "wrong slot";
                    if (Duplicate) return "duplicate";
                    if (Special) return "special (slot)";
                    return "ok";
                }
            }
        }

        internal sealed class Report
        {
            public readonly List<WornItem> Items = new List<WornItem>(35);
            public bool Available;
            public int UnknownCount;
            public int SpecialCount;
            public int FakeSpecialCount;
            public int MismatchCount;
            public int DuplicateCount;

            public int Total
            {
                get { return Items.Count; }
            }

            public bool Clean
            {
                get
                {
                    return UnknownCount == 0 && MismatchCount == 0 &&
                           DuplicateCount == 0 && FakeSpecialCount == 0;
                }
            }

            public string Summary
            {
                get
                {
                    if (!Available) return "Cosmetics not synced";
                    if (Total == 0) return "No cosmetics";
                    if (!Clean)
                    {
                        int flags = UnknownCount + MismatchCount + DuplicateCount + FakeSpecialCount;
                        return flags + (flags == 1 ? " spoof flag" : " spoof flags");
                    }
                    return SpecialCount > 0 ? "Special cosmetics (slot verified)" : "Clean";
                }
            }

            /// <summary>Short marker used in the player list.</summary>
            public string ShortStatus
            {
                get
                {
                    if (!Available || Total == 0) return string.Empty;
                    if (!Clean) return "spoof?";
                    if (SpecialCount > 0) return "special";
                    return string.Empty;
                }
            }
        }

        // Known creator / staff cosmetics. This mirrors the ids VisualsController
        // already tags in the ESP overlay.
        private static readonly KeyValuePair<string, string>[] SpecialIds =
        {
            new KeyValuePair<string, string>("LBAAD.", "Administrator"),
            new KeyValuePair<string, string>("LBAAK.", "Forest Guide"),
            new KeyValuePair<string, string>("LBADE.", "Finger Painter"),
            new KeyValuePair<string, string>("LBAGS.", "Illustrator"),
            new KeyValuePair<string, string>("LMAPY.", "Forest Guide"),
            new KeyValuePair<string, string>("LBANI.", "AA Creator")
        };

        private static readonly string[] SpecialWords =
        {
            "administrator", "admin", "forest guide", "finger painter", "illustrator",
            "aa creator", "moderator", "founder"
        };

        internal static Report Inspect(VRRig rig)
        {
            var report = new Report();

            if (rig == null || rig.cosmeticSet == null || rig.cosmeticSet.items == null)
                return report;

            report.Available = true;

            CosmeticsController controller = CosmeticsController.hasInstance
                ? CosmeticsController.instance
                : null;
            Dictionary<string, CosmeticsController.CosmeticItem> catalog =
                controller != null && controller.allCosmeticsDict_isInitialized
                    ? controller.allCosmeticsDict
                    : null;

            CosmeticsController.CosmeticItem[] items = rig.cosmeticSet.items;
            for (int i = 0; i < items.Length; i++)
            {
                CosmeticsController.CosmeticItem item = items[i];
                if (item.isNullItem) continue;

                string itemName = item.itemName;
                if (string.IsNullOrEmpty(itemName) || itemName == "null" || itemName == "NOTHING")
                    continue;

                CosmeticsController.CosmeticItem resolved = item;
                bool known = true;

                if (catalog != null)
                {
                    if (catalog.TryGetValue(itemName, out CosmeticsController.CosmeticItem found) &&
                        !found.isNullItem)
                    {
                        resolved = found;
                    }
                    else
                    {
                        known = false;
                    }
                }

                if (resolved.itemCategory == CosmeticsController.CosmeticCategory.None)
                {
                    // Remote items occasionally arrive without their category;
                    // fall back to the catalog entry when we have one.
                    if (catalog != null &&
                        catalog.TryGetValue(itemName, out CosmeticsController.CosmeticItem byName))
                    {
                        resolved = byName;
                    }
                }

                var entry = new WornItem
                {
                    SlotIndex = i,
                    SlotName = SlotName((CosmeticsController.CosmeticSlots)i),
                    ItemName = itemName,
                    DisplayName = DisplayName(resolved),
                    Category = resolved.itemCategory,
                    Known = known,
                    Holdable = resolved.isHoldable,
                    Icon = resolved.itemPicture
                };

                string role;
                entry.Special = TryGetSpecialRole(itemName, out role);
                entry.Role = role;
                entry.ClaimedSpecial = entry.Special ||
                                       IsSpecialClaim(itemName, entry.DisplayName);
                if (entry.Special && !string.IsNullOrEmpty(role))
                    entry.DisplayName = entry.DisplayName + " (" + role + ")";

                if (known && !CosmeticsController.CosmeticSet.IsHoldable(resolved) &&
                    !SlotMatchesCategory((CosmeticsController.CosmeticSlots)i, resolved.itemCategory))
                {
                    entry.SlotMismatch = true;
                }

                if (!entry.Known) report.UnknownCount++;
                if (entry.SlotMismatch) report.MismatchCount++;

                report.Items.Add(entry);
            }

            // Duplicate non-holdable items in two slots are a common spoof pattern.
            for (int i = 0; i < report.Items.Count; i++)
            {
                WornItem entry = report.Items[i];
                int copies = 0;
                for (int j = 0; j < report.Items.Count; j++)
                {
                    if (string.Equals(report.Items[j].ItemName, entry.ItemName, StringComparison.Ordinal))
                        copies++;
                }

                if (copies > 1 && !entry.Holdable)
                {
                    entry.Duplicate = true;
                    report.DuplicateCount++;
                }
            }

            // A special ID only counts when it is a real catalog item in its real
            // slot and not duplicated. A display name that merely claims to be an
            // admin/creator badge is flagged as a fake special instead.
            for (int i = 0; i < report.Items.Count; i++)
            {
                WornItem entry = report.Items[i];
                entry.SpecialVerified = entry.Special && entry.Known &&
                                        !entry.SlotMismatch && !entry.Duplicate;
                entry.FakeSpecial = entry.Special
                    ? !entry.SpecialVerified
                    : entry.ClaimedSpecial;
                if (entry.SpecialVerified) report.SpecialCount++;
                if (entry.FakeSpecial) report.FakeSpecialCount++;
            }

            report.Items.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));
            return report;
        }

        /// <summary>Human-readable category for a worn item (used by the player page).</summary>
        internal static string CategoryName(CosmeticsController.CosmeticCategory category)
        {
            return category == CosmeticsController.CosmeticCategory.None
                ? "Unknown"
                : category.ToString();
        }

        /// <summary>Copies a readable cosmetic list to the clipboard format.</summary>
        internal static string FormatForClipboard(Report report)
        {
            if (report == null || report.Items.Count == 0) return "No cosmetics";

            var builder = new StringBuilder();
            for (int i = 0; i < report.Items.Count; i++)
            {
                WornItem item = report.Items[i];
                builder.Append(item.SlotName)
                    .Append(": ")
                    .Append(item.DisplayName)
                    .Append(" [")
                    .Append(CategoryName(item.Category))
                    .Append("]");
                if (item.Flagged) builder.Append(" (").Append(item.FlagText).Append(')');
                if (i < report.Items.Count - 1) builder.Append('\n');
            }
            return builder.ToString();
        }

        private static string DisplayName(CosmeticsController.CosmeticItem item)
        {
            if (!string.IsNullOrEmpty(item.overrideDisplayName)) return item.overrideDisplayName;
            if (!string.IsNullOrEmpty(item.displayName)) return item.displayName;
            return string.IsNullOrEmpty(item.itemName) ? "Unknown" : item.itemName;
        }

        private static bool TryGetSpecialRole(string itemName, out string role)
        {
            role = null;
            if (string.IsNullOrEmpty(itemName)) return false;

            for (int i = 0; i < SpecialIds.Length; i++)
            {
                if (itemName.StartsWith(SpecialIds[i].Key, StringComparison.OrdinalIgnoreCase))
                {
                    role = SpecialIds[i].Value;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// True when a name/display claims to be a creator or staff cosmetic.
        /// This alone never grants special status; it only marks a fake attempt
        /// when no matching special item ID is present.
        /// </summary>
        private static bool IsSpecialClaim(string itemName, string displayName)
        {
            string haystack = ((itemName ?? string.Empty) + " " + (displayName ?? string.Empty)).ToLowerInvariant();
            for (int i = 0; i < SpecialWords.Length; i++)
            {
                if (haystack.Contains(SpecialWords[i])) return true;
            }
            return false;
        }

        /// <summary>
        /// Verified special roles for a rig. Only exact special item IDs in the
        /// correct slot are returned; fake admin/creator claims go to
        /// <paramref name="fake"/> instead.
        /// </summary>
        internal static void GetSpecialRoles(VRRig rig, List<string> verified, List<string> fake)
        {
            if (verified == null || fake == null) return;
            verified.Clear();
            fake.Clear();

            Report report = Inspect(rig);
            for (int i = 0; i < report.Items.Count; i++)
            {
                WornItem item = report.Items[i];
                if (item.SpecialVerified)
                {
                    string role = string.IsNullOrEmpty(item.Role) ? "Special" : item.Role;
                    if (!verified.Contains(role)) verified.Add(role);
                }
                else if (item.FakeSpecial)
                {
                    string label = item.Special && !string.IsNullOrEmpty(item.Role)
                        ? "fake " + item.Role
                        : "fake special";
                    if (!fake.Contains(label)) fake.Add(label);
                }
            }
        }

        internal static Sprite VerifiedSpecialIcon(VRRig rig)
        {
            Report report = Inspect(rig);
            for (int i = 0; i < report.Items.Count; i++)
            {
                WornItem item = report.Items[i];
                if (item.SpecialVerified && item.Icon != null) return item.Icon;
            }
            return null;
        }

        private static bool SlotMatchesCategory(CosmeticsController.CosmeticSlots slot, CosmeticsController.CosmeticCategory category)
        {
            if (category == CosmeticsController.CosmeticCategory.None ||
                category == CosmeticsController.CosmeticCategory.Collectable ||
                category == CosmeticsController.CosmeticCategory.Set)
                return true;

            // Paired categories legitimately occupy left/right/both slots.
            switch (category)
            {
                case CosmeticsController.CosmeticCategory.Paw:
                    return slot == CosmeticsController.CosmeticSlots.HandLeft || slot == CosmeticsController.CosmeticSlots.HandRight;
                case CosmeticsController.CosmeticCategory.Arms:
                    return slot == CosmeticsController.CosmeticSlots.Arms ||
                           slot == CosmeticsController.CosmeticSlots.ArmLeft ||
                           slot == CosmeticsController.CosmeticSlots.ArmRight;
                case CosmeticsController.CosmeticCategory.Back:
                    return slot == CosmeticsController.CosmeticSlots.Back ||
                           slot == CosmeticsController.CosmeticSlots.BackLeft ||
                           slot == CosmeticsController.CosmeticSlots.BackRight;
                case CosmeticsController.CosmeticCategory.Ears:
                    return slot == CosmeticsController.CosmeticSlots.EarLeft || slot == CosmeticsController.CosmeticSlots.EarRight ||
                           slot == CosmeticsController.CosmeticSlots.EarBoth;
                case CosmeticsController.CosmeticCategory.Nose:
                    return slot == CosmeticsController.CosmeticSlots.NoseLeft || slot == CosmeticsController.CosmeticSlots.NoseRight ||
                           slot == CosmeticsController.CosmeticSlots.NoseBoth;
                case CosmeticsController.CosmeticCategory.Glasses:
                    return slot == CosmeticsController.CosmeticSlots.GlassesLeft || slot == CosmeticsController.CosmeticSlots.GlassesRight ||
                           slot == CosmeticsController.CosmeticSlots.GlassesBoth;
                case CosmeticsController.CosmeticCategory.Eyes:
                    return slot == CosmeticsController.CosmeticSlots.EyeLeft || slot == CosmeticsController.CosmeticSlots.EyeRight ||
                           slot == CosmeticsController.CosmeticSlots.EyeBoth;
            }

            CosmeticsController.CosmeticSlots expected = CosmeticsController.CategoryToNonTransferrableSlot(category);
            return expected == CosmeticsController.CosmeticSlots.Count || expected == slot;
        }

        private static string SlotName(CosmeticsController.CosmeticSlots slot)
        {
            switch (slot)
            {
                case CosmeticsController.CosmeticSlots.Hat: return "Hat";
                case CosmeticsController.CosmeticSlots.Badge: return "Badge";
                case CosmeticsController.CosmeticSlots.Face: return "Face";
                case CosmeticsController.CosmeticSlots.ArmLeft: return "Left arm";
                case CosmeticsController.CosmeticSlots.ArmRight: return "Right arm";
                case CosmeticsController.CosmeticSlots.BackLeft: return "Left back";
                case CosmeticsController.CosmeticSlots.BackRight: return "Right back";
                case CosmeticsController.CosmeticSlots.HandLeft: return "Left hand";
                case CosmeticsController.CosmeticSlots.HandRight: return "Right hand";
                case CosmeticsController.CosmeticSlots.Chest: return "Chest";
                case CosmeticsController.CosmeticSlots.Fur: return "Fur";
                case CosmeticsController.CosmeticSlots.Shirt: return "Shirt";
                case CosmeticsController.CosmeticSlots.Pants: return "Pants";
                case CosmeticsController.CosmeticSlots.Back: return "Back";
                case CosmeticsController.CosmeticSlots.Arms: return "Arms";
                case CosmeticsController.CosmeticSlots.TagEffect: return "Tag effect";
                case CosmeticsController.CosmeticSlots.EarLeft: return "Left ear";
                case CosmeticsController.CosmeticSlots.EarRight: return "Right ear";
                case CosmeticsController.CosmeticSlots.EarBoth: return "Ears";
                case CosmeticsController.CosmeticSlots.NoseLeft: return "Left nose";
                case CosmeticsController.CosmeticSlots.NoseRight: return "Right nose";
                case CosmeticsController.CosmeticSlots.NoseBoth: return "Nose";
                case CosmeticsController.CosmeticSlots.GlassesLeft: return "Left glasses";
                case CosmeticsController.CosmeticSlots.GlassesRight: return "Right glasses";
                case CosmeticsController.CosmeticSlots.GlassesBoth: return "Glasses";
                case CosmeticsController.CosmeticSlots.EyeLeft: return "Left eye";
                case CosmeticsController.CosmeticSlots.EyeRight: return "Right eye";
                case CosmeticsController.CosmeticSlots.EyeBoth: return "Eyes";
                case CosmeticsController.CosmeticSlots.Mouth: return "Mouth";
                case CosmeticsController.CosmeticSlots.Hair: return "Hair";
                case CosmeticsController.CosmeticSlots.FacialHair: return "Facial hair";
                case CosmeticsController.CosmeticSlots.Neck: return "Neck";
                case CosmeticsController.CosmeticSlots.Hoverboard: return "Hoverboard";
                case CosmeticsController.CosmeticSlots.Glider: return "Glider";
                case CosmeticsController.CosmeticSlots.Tattoo: return "Tattoo";
                default: return "Slot " + (int)slot;
            }
        }
    }
}

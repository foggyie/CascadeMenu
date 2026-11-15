using System;
using System.Collections.Generic;
using GorillaNetworking;
using CascadeMenu.UI;
using UnityEngine;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// A cosmetics browser built from the game's real cosmetic data.
    ///
    /// The "Cosmetics" tab lists one button per CosmeticCategory (Hats, Badges,
    /// Face, Holdables, Gloves, Slingshots, Sets) with the number of items in
    /// it. Pressing a button opens a separate tab that lists every item in that
    /// section, each with a Try on and Cart action.
    ///
    /// Everything is read from CosmeticsController.instance.allCosmetics, so new
    /// categories and items added by the game appear automatically.
    /// </summary>
    internal static class CosmeticBrowser
    {
        internal const string CosmeticsTag = "cascade.cosmetics";

        internal const string CategoryTagPrefix = "cascade.cosmetics.cat.";

        private const int PageSize = 25;

        /// <summary>Remembered page per category, so Prev/Next keeps position.</summary>
        private static readonly Dictionary<string, int> PageIndex = new Dictionary<string, int>();

        private static readonly CosmeticsController.CosmeticCategory[] Categories = BuildCategories();

        private static CosmeticsController.CosmeticCategory[] BuildCategories()
        {
            var list = new List<CosmeticsController.CosmeticCategory>();
            Array values = Enum.GetValues(typeof(CosmeticsController.CosmeticCategory));
            for (int i = 0; i < values.Length; i++)
            {
                var category = (CosmeticsController.CosmeticCategory)values.GetValue(i);
                if (category == CosmeticsController.CosmeticCategory.None) continue;
                if (category == CosmeticsController.CosmeticCategory.Count) continue;
                list.Add(category);
            }
            return list.ToArray();
        }

        internal static string CategoryTag(string key)
        {
            return CategoryTagPrefix + (key ?? "All");
        }

        // -----------------------------------------------------------------
        // The Cosmetics tab: one button per category.
        // -----------------------------------------------------------------
        internal static CascadePage BuildCategoriesPage(CascadeMenuBehaviour menu)
        {
            var page = new CascadePage("Cosmetics", "Pick a wardrobe section", CascadeIcon.Sparkle);
            page.Tag = CosmeticsTag;

            CosmeticsController controller = GetController();

            var section = page.Section("Sections", "Built from the game's own cosmetic categories");

            section.Button("All cosmetics (" + TotalCount(controller) + ")",
                "Open every cosmetic in one list", "Open",
                () => menu.OpenCosmeticsCategory("All"), false);

            for (int i = 0; i < Categories.Length; i++)
            {
                CosmeticsController.CosmeticCategory category = Categories[i];
                string key = category.ToString();

                section.Button(Friendly(category) + " (" + CountIn(controller, category) + ")",
                    "Show every " + Friendly(category) + " cosmetic", "Open",
                    () => menu.OpenCosmeticsCategory(key), true);
            }

            page.Section("Note", "Try on uses the game's temporary wardrobe set")
                .Label("Safe", "Cart edits stay local; nothing is bought here", "no auto-buy");

            return page;
        }

        // -----------------------------------------------------------------
        // The per-category tab: every item, with Try on + Cart.
        // -----------------------------------------------------------------
        internal static CascadePage BuildCategoryPage(CascadeMenuBehaviour menu, string key)
        {
            bool all = string.IsNullOrEmpty(key) || key == "All";

            CosmeticsController.CosmeticCategory category = CosmeticsController.CosmeticCategory.None;
            if (!all) Enum.TryParse(key, out category);

            string title = all ? "All cosmetics" : Friendly(category);
            var page = new CascadePage(title, "Every item in this section", CascadeIcon.Sparkle);
            page.Tag = CategoryTag(key);
            page.HiddenFromSidebar = true;

            CosmeticsController controller = GetController();
            if (controller == null || controller.allCosmetics == null)
            {
                page.Section("Cosmetics", "The cosmetic list is not ready yet")
                    .Label("Not loaded", "Open the in-game wardrobe once, then come back", "-");
                AddBack(page, menu);
                return page;
            }

            List<CosmeticsController.CosmeticItem> items = Collect(controller, category, all);

            string pkey = key ?? "All";
            int pageNum = 0;
            PageIndex.TryGetValue(pkey, out pageNum);
            int pageCount = Mathf.Max(1, (items.Count + PageSize - 1) / PageSize);
            pageNum = Mathf.Clamp(pageNum, 0, pageCount - 1);
            PageIndex[pkey] = pageNum;
            int start = pageNum * PageSize;
            int end = Mathf.Min(start + PageSize, items.Count);

            string description = items.Count + (items.Count == 1 ? " item" : " items") +
                                 " - page " + (pageNum + 1) + " / " + pageCount;
            var section = page.Section(title, description);

            if (items.Count == 0)
            {
                section.Label("Nothing here", "No cosmetics in this section yet", "0");
            }
            else
            {
                int currentPage = pageNum;

                // Pager at the top so a long list can be flipped without scrolling.
                AddPager(section, menu, key, currentPage, pageCount);

                for (int i = start; i < end; i++)
                {
                    CosmeticsController.CosmeticItem item = items[i];
                    string name = DisplayName(item);
                    bool owned = IsOwned(controller, item);
                    bool inCart = InCart(controller, item);

                    bool available = item.canTryOn;
                    string subtitle = available
                        ? CategoryName(item.itemCategory) + " - " +
                          (item.cost <= 0 ? "free" : item.cost + " rocks") +
                          (owned ? " - owned" : string.Empty)
                        : CategoryName(item.itemCategory) + " - not for sale";

                    section.Cosmetic(name, subtitle, item.itemPicture, owned, inCart, available,
                        () =>
                        {
                            bool ok = TryOn(item);
                            menu.ShowToast(ok ? "Try on" : "Not available",
                                ok ? name : "This item cannot be equipped right now.");
                        },
                        () =>
                        {
                            bool added = ToggleCart(controller, item);
                            menu.ShowToast(added ? "Added to cart" : "Removed from cart", name);
                        });
                }

                // Pager at the bottom as well, so both directions are reachable.
                AddPager(section, menu, key, currentPage, pageCount);
            }

            AddBack(page, menu);
            return page;
        }

        /// <summary>
        /// Prev + Next in one row group. Added twice (above and below the item
        /// list) so paging never requires scrolling to the other end.
        /// </summary>
        private static void AddPager(
            CascadeSection section,
            CascadeMenuBehaviour menu,
            string key,
            int currentPage,
            int pageCount)
        {
            if (pageCount <= 1)
            {
                return;
            }

            string pkey = key ?? "All";
            string where = "page " + (currentPage + 1) + " / " + pageCount;

            section.Button("Previous page", "Back one page (" + where + ")", "Prev",
                () =>
                {
                    if (currentPage <= 0) return;
                    PageIndex[pkey] = currentPage - 1;
                    menu.OpenCosmeticsCategory(key);
                }, false);

            section.Button("Next page", "Forward one page (" + where + ")", "Next",
                () =>
                {
                    if (currentPage >= pageCount - 1) return;
                    PageIndex[pkey] = currentPage + 1;
                    menu.OpenCosmeticsCategory(key);
                }, true);
        }

        private static void AddBack(CascadePage page, CascadeMenuBehaviour menu)
        {
            page.Section("Navigate", "Return to the section list")
                .Button("Back", "Back to the cosmetics sections", "Back",
                    menu.OpenCosmeticsPage, false)
                .Button("Open the shop", "The full shop with cart and filters", "Shop",
                    menu.OpenCosmeticsShop, false);
        }

        // -----------------------------------------------------------------
        // Data helpers
        // -----------------------------------------------------------------

        private static List<CosmeticsController.CosmeticItem> Collect(
            CosmeticsController controller,
            CosmeticsController.CosmeticCategory category,
            bool all)
        {
            var result = new List<CosmeticsController.CosmeticItem>();
            List<CosmeticsController.CosmeticItem> source = controller.allCosmetics;

            for (int i = 0; i < source.Count; i++)
            {
                CosmeticsController.CosmeticItem item = source[i];
                if (item.isNullItem) continue;
                if (string.IsNullOrEmpty(item.itemName)) continue;
                if (!all && item.itemCategory != category) continue;

                bool seen = false;
                for (int r = 0; r < result.Count; r++)
                {
                    if (string.Equals(result[r].itemName, item.itemName, StringComparison.Ordinal))
                    {
                        seen = true;
                        break;
                    }
                }
                if (!seen) result.Add(item);
            }

            // Deliberately not sorted: the list stays in the game's own order,
            // so the first cosmetic is first and the newest is last.
            return result;
        }

        private static int TotalCount(CosmeticsController controller)
        {
            if (controller == null || controller.allCosmetics == null) return 0;

            int count = 0;
            for (int i = 0; i < controller.allCosmetics.Count; i++)
            {
                CosmeticsController.CosmeticItem item = controller.allCosmetics[i];
                if (!item.isNullItem && !string.IsNullOrEmpty(item.itemName)) count++;
            }
            return count;
        }

        private static int CountIn(CosmeticsController controller, CosmeticsController.CosmeticCategory category)
        {
            if (controller == null || controller.allCosmetics == null) return 0;

            int count = 0;
            for (int i = 0; i < controller.allCosmetics.Count; i++)
            {
                CosmeticsController.CosmeticItem item = controller.allCosmetics[i];
                if (!item.isNullItem && !string.IsNullOrEmpty(item.itemName) && item.itemCategory == category)
                    count++;
            }
            return count;
        }

        private static CosmeticsController GetController()
        {
            return CosmeticsController.hasInstance ? CosmeticsController.instance : null;
        }

        internal static string DisplayName(CosmeticsController.CosmeticItem item)
        {
            if (!string.IsNullOrEmpty(item.overrideDisplayName)) return item.overrideDisplayName;
            if (!string.IsNullOrEmpty(item.displayName)) return item.displayName;
            return string.IsNullOrEmpty(item.itemName) ? "Unknown" : item.itemName;
        }

        internal static string CategoryName(CosmeticsController.CosmeticCategory category)
        {
            return category.ToString();
        }

        /// <summary>
        /// Human label for a category. Matched by name so it keeps compiling
        /// even when the game adds or removes enum members.
        /// </summary>
        internal static string Friendly(CosmeticsController.CosmeticCategory category)
        {
            switch (category.ToString())
            {
                case "Hat": return "Hats";
                case "Badge": return "Badges";
                case "Face": return "Face";
                case "Holdable": return "Holdables";
                case "Gloves": return "Gloves";
                case "Slingshot": return "Slingshots";
                case "Set": return "Sets";
                case "Fur": return "Fur";
                case "Paw": return "Paws";
                case "Shirt": return "Shirts";
                case "Pants": return "Bottoms";
                case "Back": return "Back";
                case "Arms": return "Arms";
                case "TagEffect": return "Tag effects";
                case "Ears": return "Ears";
                case "Nose": return "Nose";
                case "Glasses": return "Glasses";
                case "Eyes": return "Eyes";
                case "Mouth": return "Mouth";
                case "Hair": return "Hair";
                case "FacialHair": return "Facial hair";
                case "Neck": return "Neck";
                case "Hoverboard": return "Hoverboard";
                case "Glider": return "Gliders";
                case "Tattoo": return "Tattoos";
                case "Collectable": return "Collectables";
                default: return category.ToString();
            }
        }

        internal static bool IsOwned(CosmeticsController controller, CosmeticsController.CosmeticItem item)
        {
            if (controller == null || controller.unlockedCosmetics == null) return false;

            for (int i = 0; i < controller.unlockedCosmetics.Count; i++)
            {
                if (string.Equals(controller.unlockedCosmetics[i].itemName, item.itemName, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool InCart(CosmeticsController controller, CosmeticsController.CosmeticItem item)
        {
            if (controller == null || controller.currentCart == null) return false;

            for (int i = 0; i < controller.currentCart.Count; i++)
            {
                if (string.Equals(controller.currentCart[i].itemName, item.itemName, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        internal static bool TryOn(CosmeticsController.CosmeticItem item)
        {
            CosmeticsController controller = GetController();
            if (controller == null) return false;

            try
            {
                // Some catalog entries exist but have no prefab loaded, so the
                // game cannot actually wear them. Treat that as unavailable
                // instead of silently doing nothing.
                VRRig local = VRRig.LocalRig;
                if (local != null && local.cosmeticsObjectRegistry != null &&
                    local.cosmeticsObjectRegistry.Cosmetic(item.itemName) == null)
                {
                    return false;
                }

                controller.PressWardrobeItemButton(item, false, true);
                return true;
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogError("Cascade cosmetic try-on failed: " + exception);
                return false;
            }
        }

        private static bool ToggleCart(CosmeticsController controller, CosmeticsController.CosmeticItem item)
        {
            if (controller == null || controller.currentCart == null) return false;

            int index = -1;
            for (int i = 0; i < controller.currentCart.Count; i++)
            {
                if (string.Equals(controller.currentCart[i].itemName, item.itemName, StringComparison.Ordinal))
                {
                    index = i;
                    break;
                }
            }

            bool added = index < 0;
            try
            {
                if (added)
                {
                    if (controller.currentCart.Count >= 12) return false;
                    controller.currentCart.Add(item);
                }
                else
                {
                    controller.currentCart.RemoveAt(index);
                }
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogError("Cascade cart edit failed: " + exception);
                return false;
            }

            return added;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using CascadeMenu.UI;
using GorillaNetworking;
using UnityEngine;
using UnityEngine.UI;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// In-menu cosmetic catalog, modelled on GorillaShop but integrated into the
    /// Cascade window.
    ///
    /// Safety: nothing here buys an item automatically. "Try on" uses Gorilla
    /// Tag's temporary wardrobe set, "Cart" only edits the local shopping cart,
    /// and checkout is left to the real in-game shop.
    /// </summary>
    internal static class CosmeticShop
    {
        internal const string ShopTag = "cascade.shop";

        private const int PageSize = 25;

        /// <summary>Remembered catalog page; reset when the filter changes.</summary>
        private static int CatalogPage;
        // MaxRows removed – the catalog now shows every matching item.
        // private const int MaxRows = 60;

        /// <summary>Current search text. The menu keeps this in sync with the titlebar field.</summary>
        internal static string Query = string.Empty;

        /// <summary>0 = all categories, otherwise one of <see cref="CategoryNames"/> after the first entry.</summary>
        internal static int CategoryIndex;

        internal static bool IncludeOwned = true;

        internal static readonly string[] CategoryNames = BuildCategoryNames();

        private static string[] BuildCategoryNames()
        {
            var names = new List<string> { "All categories" };
            Array values = Enum.GetValues(typeof(CosmeticsController.CosmeticCategory));
            for (int i = 0; i < values.Length; i++)
            {
                CosmeticsController.CosmeticCategory category = (CosmeticsController.CosmeticCategory)values.GetValue(i);
                if (category == CosmeticsController.CosmeticCategory.None ||
                    category == CosmeticsController.CosmeticCategory.Count ||
                    category == CosmeticsController.CosmeticCategory.Set ||
                    category == CosmeticsController.CosmeticCategory.Collectable)
                    continue;

                names.Add(category.ToString());
            }
            return names.ToArray();
        }

        internal static void Reset()
        {
            Query = string.Empty;
            CategoryIndex = 0;
            IncludeOwned = true;
        }

        internal static CascadePage BuildPage(CascadeMenuBehaviour menu)
        {
            var page = new CascadePage("Shop", "Browse, try on and build a cart", CascadeIcon.Sparkle);
            page.Tag = ShopTag;

            page.Section("Shop", "Try-on is temporary; Cascade never presses Buy")
                .Dropdown("Category", "Only show one wardrobe section",
                    CategoryNames,
                    () => CategoryIndex,
                    index =>
                    {
                        CategoryIndex = index;
                        CatalogPage = 0;
                        menu.QueueRebuild();
                    })
                .Toggle("Include owned", "Show cosmetics you already own as well",
                    () => IncludeOwned,
                    value =>
                    {
                        IncludeOwned = value;
                        CatalogPage = 0;
                        menu.QueueRebuild();
                    })
                .Label("Safety", "Cart edits are local; checkout stays in the real shop", "no auto-buy");

            BuildCatalog(page, menu);
            BuildCart(page, menu);
            return page;
        }

        private static void BuildCatalog(CascadePage page, CascadeMenuBehaviour menu)
        {
            CosmeticsController controller = GetController();
            if (controller == null || !controller.allCosmeticsDict_isInitialized)
            {
                page.Section("Catalog", "Waiting for the cosmetics service")
                    .Label("Not ready", "Cosmetics have not loaded yet. Open the in-game wardrobe once, then return here.", "-");
                return;
            }

            List<CosmeticCatalogItem> items = CollectItems(controller);
            if (items.Count == 0)
            {
                page.Section("Catalog", "Nothing matches this filter")
                    .Label("Empty", "Try another category or clear the search", "0");
                return;
            }

            int pageCount = Mathf.Max(1, (items.Count + PageSize - 1) / PageSize);
            CatalogPage = Mathf.Clamp(CatalogPage, 0, pageCount - 1);
            int start = CatalogPage * PageSize;
            int end = Mathf.Min(start + PageSize, items.Count);

            string description = items.Count + (items.Count == 1 ? " item" : " items") +
                                 " - page " + (CatalogPage + 1) + " / " + pageCount;

            CascadeSection section = page.Section("Catalog", description);
            int currentPage = CatalogPage;

            // Pager at the top: both directions available without scrolling.
            AddPager(section, menu, currentPage, pageCount);

            for (int i = start; i < end; i++)
            {
                CosmeticCatalogItem entry = items[i];
                CosmeticCatalogItem captured = entry;
                bool inCart = InCart(controller, captured.Item);

                section.AddDual(
                    captured.Name,
                    captured.Subtitle,
                    "Try on",
                    inCart ? "Remove" : "Cart",
                    () =>
                    {
                        bool ok = TryOn(captured.Item);
                        menu.ShowToast(ok ? "Try on" : "Try on unavailable",
                            ok ? captured.Name : "Cosmetics service is not ready.");
                    },
                    () =>
                    {
                        bool added = ToggleCart(controller, captured.Item);
                        menu.ShowToast(added ? "Added to cart" : "Removed from cart", captured.Name);
                        menu.QueueRebuild();
                    });
            }

            // Pager at the bottom too.
            AddPager(section, menu, currentPage, pageCount);
        }

        /// <summary>Prev + Next, added both above and below the catalog list.</summary>
        private static void AddPager(CascadeSection section, CascadeMenuBehaviour menu,
            int currentPage, int pageCount)
        {
            if (pageCount <= 1)
            {
                return;
            }

            string where = "page " + (currentPage + 1) + " / " + pageCount;

            section.Button("Previous page", "Back one page (" + where + ")", "Prev",
                () =>
                {
                    if (currentPage <= 0) return;
                    CatalogPage = currentPage - 1;
                    menu.QueueRebuildTop();
                }, false);

            section.Button("Next page", "Forward one page (" + where + ")", "Next",
                () =>
                {
                    if (currentPage >= pageCount - 1) return;
                    CatalogPage = currentPage + 1;
                    menu.QueueRebuildTop();
                }, true);
        }

        private static void BuildCart(CascadePage page, CascadeMenuBehaviour menu)
        {
            CosmeticsController controller = GetController();
            if (controller == null || controller.currentCart == null)
            {
                page.Section("Cart", "Local cart preview")
                    .Label("Cart unavailable", "Waiting for the cosmetics service", "-");
                return;
            }

            List<CosmeticsController.CosmeticItem> cart = controller.currentCart;
            CascadeSection section = page.Section("Cart", "Up to 12 items; use the real shop to check out");

            if (cart.Count == 0)
            {
                section.Label("Cart is empty", "Add items with the Cart button above", "0");
            }
            else
            {
                for (int i = 0; i < cart.Count; i++)
                {
                    CosmeticsController.CosmeticItem item = cart[i];
                    section.Label(DisplayName(item), CosmeticChecker.CategoryName(item.itemCategory),
                        item.cost <= 0 ? "free" : item.cost + " rocks");
                }
            }

            section.Button("Copy cart", "Clipboard: one line per item", "Copy",
                    () =>
                    {
                        var builder = new StringBuilder();
                        for (int i = 0; i < cart.Count; i++)
                        {
                            if (i > 0) builder.Append('\n');
                            builder.Append(DisplayName(cart[i]));
                        }
                        PlayerActions.Copy(builder.ToString());
                        menu.ShowToast("Cart copied", cart.Count + " item(s).");
                    }, false)
                .Button("Clear cart", "Empties only the local cart", "Clear",
                    () =>
                    {
                        cart.Clear();
                        if (CosmeticsController.hasInstance) controller.UpdateShoppingCart();
                        menu.ShowToast("Cart cleared", "Nothing was bought.");
                        menu.QueueRebuild();
                    }, false)
                .Button("Unlock free items", "Only 0-rock items; costs no currency", "Claim",
                    () => menu.ShowConfirm(
                        "Unlock free cosmetics",
                        "Cascade will call the game's purchase API for items that cost 0 rocks only. Nothing else in your cart is bought.",
                        "Unlock free",
                        () =>
                        {
                            int count = ClaimFreeItems(controller);
                            menu.ShowToast("Free cosmetics", count == 1 ? "1 item unlocked." : count + " items unlocked.");
                            menu.QueueRebuild();
                        }), false);
        }

        // -----------------------------------------------------------------
        // Catalog helpers
        // -----------------------------------------------------------------

        private sealed class CosmeticCatalogItem
        {
            public CosmeticsController.CosmeticItem Item;
            public string Name;
            public string Subtitle;
        }

        private static List<CosmeticCatalogItem> CollectItems(CosmeticsController controller)
        {
            var result = new List<CosmeticCatalogItem>();
            List<CosmeticsController.CosmeticItem> all = controller.allCosmetics;
            if (all == null) return result;

            string query = (Query ?? string.Empty).Trim().ToLowerInvariant();
            CosmeticsController.CosmeticCategory category = CategoryIndex <= 0 || CategoryIndex >= CategoryNames.Length
                ? CosmeticsController.CosmeticCategory.None
                : (CosmeticsController.CosmeticCategory)Enum.Parse(typeof(CosmeticsController.CosmeticCategory), CategoryNames[CategoryIndex]);

            for (int i = 0; i < all.Count; i++)
            {
                CosmeticsController.CosmeticItem item = all[i];
                if (item.isNullItem || !item.canTryOn) continue;
                if (category != CosmeticsController.CosmeticCategory.None && item.itemCategory != category) continue;
                if (!IncludeOwned && IsOwned(controller, item)) continue;

                string name = DisplayName(item);
                if (query.Length > 0 &&
                    !(name ?? string.Empty).ToLowerInvariant().Contains(query) &&
                    !(item.itemName ?? string.Empty).ToLowerInvariant().Contains(query))
                    continue;

                result.Add(new CosmeticCatalogItem
                {
                    Item = item,
                    Name = name,
                    Subtitle = BuildSubtitle(controller, item)
                });
            }

            result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        private static string BuildSubtitle(CosmeticsController controller, CosmeticsController.CosmeticItem item)
        {
            string cost = item.cost <= 0 ? "free" : item.cost + " rocks";
            string owned = IsOwned(controller, item) ? " - owned" : string.Empty;
            return CosmeticChecker.CategoryName(item.itemCategory) + " - " + cost + owned;
        }

        private static string DisplayName(CosmeticsController.CosmeticItem item)
        {
            if (!string.IsNullOrEmpty(item.overrideDisplayName)) return item.overrideDisplayName;
            if (!string.IsNullOrEmpty(item.displayName)) return item.displayName;
            return string.IsNullOrEmpty(item.itemName) ? "Unknown" : item.itemName;
        }

        private static bool IsOwned(CosmeticsController controller, CosmeticsController.CosmeticItem item)
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

        // -----------------------------------------------------------------
        // Actions
        // -----------------------------------------------------------------

        internal static bool TryOn(CosmeticsController.CosmeticItem item)
        {
            CosmeticsController controller = GetController();
            if (controller == null || !controller.allCosmeticsDict_isInitialized) return false;

            try
            {
                controller.PressWardrobeItemButton(item, false, true);
                return true;
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogWarning("Cascade shop try-on failed: " + exception.Message);
                return false;
            }
        }

        /// <summary>Adds or removes an item; returns true when it was added.</summary>
        internal static bool ToggleCart(CosmeticsController controller, CosmeticsController.CosmeticItem item)
        {
            if (controller == null || controller.currentCart == null) return false;

            int index = controller.currentCart.FindIndex(x =>
                string.Equals(x.itemName, item.itemName, StringComparison.Ordinal));

            bool added = index < 0;
            if (added)
            {
                if (controller.currentCart.Count >= 12) return false;
                controller.currentCart.Add(item);
            }
            else
            {
                controller.currentCart.RemoveAt(index);
            }

            if (CosmeticsController.hasInstance) controller.UpdateShoppingCart();
            return added;
        }

        internal static int ClaimFreeItems(CosmeticsController controller)
        {
            if (controller == null || controller.allCosmetics == null) return 0;

            int claimed = 0;
            for (int i = 0; i < controller.allCosmetics.Count; i++)
            {
                CosmeticsController.CosmeticItem item = controller.allCosmetics[i];
                if (item.isNullItem || !item.canTryOn || item.cost != 0) continue;
                if (IsOwned(controller, item)) continue;

                try
                {
                    controller.itemToBuy = item;
                    controller.PurchaseItem();
                    claimed++;
                }
                catch (Exception exception)
                {
                    Plugin.Log?.LogWarning("Cascade shop free unlock failed for " + item.itemName + ": " + exception.Message);
                }
            }

            return claimed;
        }

        private static CosmeticsController GetController()
        {
            return CosmeticsController.hasInstance ? CosmeticsController.instance : null;
        }
    }
}

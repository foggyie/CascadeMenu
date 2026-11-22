using System;
using System.Collections;
using System.Collections.Generic;
using GorillaNetworking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Overlays;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Tools;

namespace CascadeMenu.UI
{
    internal sealed partial class CascadeWindow
    {
        // -----------------------------------------------------------------
        // Toasts
        // -----------------------------------------------------------------

        private void BuildToasts()
        {
            _toastContainer = CascadeUI.Rect("Toasts", _root);
            _toastContainer.anchorMin = _toastContainer.anchorMax = new Vector2(1f, 0f);
            _toastContainer.pivot = new Vector2(1f, 0f);
            _toastContainer.anchoredPosition = new Vector2(-12f, 12f);
            _toastContainer.sizeDelta = new Vector2(300f, 0f);

            var layout = _toastContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.LowerRight;
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = _toastContainer.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        public void ShowToast(string title, string message)
        {
            if (_toastContainer == null) return;
            _toastContainer.SetAsLastSibling();

            RectTransform toast = CascadeUI.Rect("Toast", _toastContainer);
            var layoutElement = toast.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 58f;
            layoutElement.minHeight = 58f;
            var group = toast.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            Image background = CascadeUI.Panel(toast, Theme.ToastBg, 10);
            background.raycastTarget = false;

            RectTransform border = CascadeUI.Rect("Border", toast);
            CascadeUI.Stretch(border);
            CascadeUI.Outline(border, Theme.ToastBorder, 10);
            border.GetComponent<Image>().raycastTarget = false;

            RectTransform iconRect = CascadeUI.TopLeft("Icon", toast, 13f, 16f, 26f, 26f);
            CascadeUI.Image(iconRect, CascadeSprites.Circle(), Color.white, Image.Type.Simple);
            var iconGradient = iconRect.gameObject.AddComponent<CascadeGradient>();
            iconGradient.Top = Theme.ButtonPrimaryTop;
            iconGradient.Bottom = Theme.ButtonPrimaryBottom;
            RectTransform icon = CascadeUI.Center("Glyph", iconRect, 0f, 0f, 13f, 13f);
            CascadeUI.Image(icon, CascadeIcons.Get(CascadeIcon.Check), Color.white, Image.Type.Simple);

            RectTransform titleRect = LineRect("Title", toast, 50f, 28f, 8f, 21f);
            CascadeUI.Text(titleRect, title, 12f, Theme.TextPrimary, TextAlignmentOptions.Left, true);

            RectTransform messageRect = LineRect("Message", toast, 50f, 28f, 28f, 20f);
            CascadeUI.Text(messageRect, CascadeUI.Truncate(message, 52), 10.5f, Theme.TextSecondary, TextAlignmentOptions.Left);

            RectTransform close = CascadeUI.TopRight("Close", toast, 12f, 21f, 9f, 9f);
            CascadeUI.Image(close, CascadeIcons.Get(CascadeIcon.Close), Theme.TextTertiary, Image.Type.Simple);

            var view = toast.gameObject.AddComponent<CascadeToastView>();
            view.Group = group;
            view.Window = this;

            Register(toast, CascadeLayerGroup.Toast, () => view.Dismiss());
            LayoutRebuilder.MarkLayoutForRebuild(_toastContainer);
        }

        internal void MarkToastLayout()
        {
            if (_toastContainer != null) LayoutRebuilder.MarkLayoutForRebuild(_toastContainer);
        }

        // -----------------------------------------------------------------
        // Interaction
        // -----------------------------------------------------------------

        public CascadeInteractable Register(
            RectTransform rect,
            CascadeLayerGroup group,
            Action click = null,
            Action press = null,
            Action release = null,
            Action<Vector3> dragWorld = null,
            Action<bool> hover = null,
            bool slider = false)
        {
            var interactable = new CascadeInteractable
            {
                Rect = rect,
                Group = group,
                Order = Interactables.Count,
                Click = click,
                Press = press,
                Release = release,
                DragWorld = dragWorld,
                Hover = hover,
                IsSlider = slider
            };
            Interactables.Add(interactable);
            return interactable;
        }

        /// <summary>
        /// Removes an interactable registered from outside the window (the
        /// spectate overlay does this when the window is rebuilt or closed).
        /// </summary>
        public void Unregister(CascadeInteractable interactable)
        {
            if (interactable == null) return;
            Interactables.Remove(interactable);
            for (int i = 0; i < Interactables.Count; i++) Interactables[i].Order = i;
        }

        /// <summary>Root rect of the window; the menu moves it when dragged.</summary>
        public RectTransform RootRect
        {
            get { return _root; }
        }

        /// <summary>Moves the window inside the canvas (canvas units).</summary>
        public void SetWindowPosition(Vector2 anchoredPosition)
        {
            if (_root == null) return;
            _root.anchoredPosition = anchoredPosition;
            ClampToCanvas();
        }

        /// <summary>Local rotation of the window (relative to the canvas).</summary>
        public Quaternion WindowRotation
        {
            get { return _root != null ? _root.localRotation : Quaternion.identity; }
        }

        /// <summary>Rotates the window in place.</summary>
        public void SetWindowRotation(Quaternion localRotation)
        {
            if (_root != null) _root.localRotation = localRotation;
        }

        /// <summary>True when the point is inside the window rectangle.</summary>
        public bool ContainsPoint(Vector3 worldPoint)
        {
            if (_root == null) return false;

            Vector3 local = _root.InverseTransformPoint(worldPoint);
            return Mathf.Abs(local.z) < 60f && _root.rect.Contains(new Vector2(local.x, local.y));
        }

        /// <summary>
        /// True when the point is on the empty part of the titlebar. Titlebar
        /// buttons are interactables, so a press over them never starts a drag.
        /// </summary>
        public bool IsTitlebarPoint(Vector3 worldPoint)
        {
            if (_root == null || !_root.gameObject.activeInHierarchy) return false;

            Vector3 local = _root.InverseTransformPoint(worldPoint);
            Rect rect = _root.rect;
            float top = rect.yMax;
            return Mathf.Abs(local.z) < 60f &&
                   local.y <= top &&
                   local.y >= top - CascadeTheme.TitlebarHeight &&
                   local.x >= rect.xMin &&
                   local.x <= rect.xMax;
        }

        /// <summary>Centre of the window in canvas units.</summary>
        public Vector2 WindowPosition
        {
            get { return _root != null ? _root.anchoredPosition : Vector2.zero; }
        }

        public CascadeInteractable HitTest(Vector3 worldPoint)
        {            CascadeInteractable best = null;
            for (int i = Interactables.Count - 1; i >= 0; i--)
            {
                CascadeInteractable interactable = Interactables[i];
                if (interactable == null || interactable.Rect == null)
                {
                    Interactables.RemoveAt(i);
                    continue;
                }
                if (!interactable.Enabled || !interactable.IsAlive) continue;
                if (interactable.Group == CascadeLayerGroup.Content && !InsideViewport(worldPoint)) continue;
                if (!interactable.Contains(worldPoint)) continue;
                if (best == null ||
                    interactable.LayerRank > best.LayerRank ||
                    (interactable.LayerRank == best.LayerRank && interactable.Order > best.Order))
                {
                    best = interactable;
                }
            }
            return best;
        }

        private bool InsideViewport(Vector3 worldPoint)
        {
            if (_viewport == null) return false;
            Vector3 local = _viewport.InverseTransformPoint(worldPoint);
            return _viewport.rect.Contains(new Vector2(local.x, local.y));
        }

        private void RemoveInteractables(params CascadeLayerGroup[] groups)
        {
            for (int i = Interactables.Count - 1; i >= 0; i--)
            {
                CascadeInteractable interactable = Interactables[i];
                if (interactable == null || interactable.Rect == null)
                {
                    Interactables.RemoveAt(i);
                    continue;
                }
                for (int g = 0; g < groups.Length; g++)
                {
                    if (interactable.Group == groups[g])
                    {
                        Interactables.RemoveAt(i);
                        break;
                    }
                }
            }

            for (int i = 0; i < Interactables.Count; i++) Interactables[i].Order = i;
        }

        public void Scroll(float delta)
        {
            if (_scroll == null) return;
            _scroll.verticalNormalizedPosition = Mathf.Clamp01(_scroll.verticalNormalizedPosition + delta);
        }

        public void ScrollToTop()
        {
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>Scrolls the sidebar tab list (0 = top, 1 = bottom).</summary>
        public void ScrollSidebar(float delta)
        {
            if (_sidebarScroll == null) return;
            _sidebarScroll.verticalNormalizedPosition =
                Mathf.Clamp01(_sidebarScroll.verticalNormalizedPosition + delta);
        }

        /// <summary>True while the given screen point is over the sidebar.</summary>
        public bool SidebarContains(Vector2 screenPoint)
        {
            if (_sidebar == null || !_sidebarExpanded || _sidebarCurrent < 0.5f) return false;

            Canvas canvas = GetComponentInParent<Canvas>();
            Camera cam = canvas != null ? canvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(_sidebar, screenPoint, cam);
        }
    }
}

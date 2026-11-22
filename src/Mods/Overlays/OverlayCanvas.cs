using UnityEngine;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Tools;
using CascadeMenu.Mods.Cosmetics;
using CascadeMenu.Mods.Visual;
using CascadeMenu.Mods.Movement;

namespace CascadeMenu.Mods.Overlays
{
    /// <summary>
    /// Shared world-space canvas setup for the camera-parented overlays. Keeps
    /// the render mode, sorting order, scale, raycast blocking and CanvasGroup
    /// defaults identical across Clock, Compass, Crosshair, FPS Graph, HUD and
    /// Screen Tint.
    /// </summary>
    internal static class OverlayCanvas
    {
        internal static Canvas Create(string name, Camera camera, Transform fallbackParent,
            int sortingOrder, Vector2 size, float scale, Vector3 localPosition,
            out RectTransform rect, out CanvasGroup group)
        {
            GameObject root = new GameObject(name,
                typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));

            Transform parent = camera != null ? camera.transform : fallbackParent;
            if (parent != null) root.transform.SetParent(parent, false);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = sortingOrder;
            canvas.worldCamera = camera;

            rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.localScale = Vector3.one * scale;
            rect.localPosition = localPosition;
            rect.localRotation = Quaternion.identity;

            group = root.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;

            return canvas;
        }
    }

    /// <summary>
    /// Throttles text overlays to 4 Hz by default and skips assigning a TMP
    /// string when it has not changed.
    /// </summary>
    internal static class OverlayText
    {
        internal const float DefaultHz = 4f;

        internal static bool Due(ref float nextTime, float hz = DefaultHz)
        {
            float now = Time.unscaledTime;
            if (now < nextTime) return false;
            nextTime = now + 1f / Mathf.Max(0.1f, hz);
            return true;
        }

        internal static void Set(TMPro.TMP_Text label, string text)
        {
            if (label == null || label.text == text) return;
            label.text = text;
        }
    }
}

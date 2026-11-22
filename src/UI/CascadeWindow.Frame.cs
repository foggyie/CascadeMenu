using UnityEngine;
using UnityEngine.UI;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Overlays;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Tools;

namespace CascadeMenu.UI
{
    /// <summary>
    /// Window frame behaviour: keeping the window fully inside the canvas
    /// (it stops at the edges instead of being cut off) and resizing it from
    /// the bottom-right grip.
    /// </summary>
    internal sealed partial class CascadeWindow
    {
        private const float MinWindowWidth = 540f;
        private const float MinWindowHeight = 320f;
        private const float GripSize = 20f;

        // Static so a resized window keeps its size when it is rebuilt.
        private static Vector2 _savedSize;

        private bool _resizeStarted;
        private Vector2 _resizeOffset;

        private void ApplySavedSize()
        {
            if (_savedSize.x >= MinWindowWidth && _savedSize.y >= MinWindowHeight)
            {
                _root.sizeDelta = _savedSize;
            }
        }

        /// <summary>
        /// Moves the window back inside the canvas. Called after every drag
        /// step and resize, so the window can touch an edge but never leave it.
        /// </summary>
        internal void ClampToCanvas()
        {
            if (_root == null || _canvasRect == null) return;

            Rect canvas = _canvasRect.rect;
            float scale = WindowScale();
            Vector2 half = _root.sizeDelta * (scale * 0.5f);

            float limitX = Mathf.Max(0f, canvas.width * 0.5f - half.x);
            float limitY = Mathf.Max(0f, canvas.height * 0.5f - half.y);

            Vector2 position = _root.anchoredPosition;
            position.x = Mathf.Clamp(position.x, canvas.center.x - limitX, canvas.center.x + limitX);
            position.y = Mathf.Clamp(position.y, canvas.center.y - limitY, canvas.center.y + limitY);
            _root.anchoredPosition = position;
        }

        private float WindowScale()
        {
            float scale = _root != null ? _root.localScale.x : 1f;
            return scale < 0.05f ? 1f : scale;
        }

        // -----------------------------------------------------------------
        // Resize grip
        // -----------------------------------------------------------------

        private void BuildResizeGrip()
        {
            RectTransform grip = CascadeUI.Rect("ResizeGrip", _body);
            grip.anchorMin = grip.anchorMax = new Vector2(1f, 0f);
            grip.pivot = new Vector2(1f, 0f);
            grip.anchoredPosition = Vector2.zero;
            grip.sizeDelta = new Vector2(GripSize, GripSize);

            // Three dots on the diagonal, like a native resize corner.
            var dots = new Image[3];
            for (int i = 0; i < dots.Length; i++)
            {
                RectTransform dot = CascadeUI.Rect("Dot" + i, grip);
                dot.anchorMin = dot.anchorMax = new Vector2(1f, 0f);
                dot.pivot = new Vector2(1f, 0f);
                dot.anchoredPosition = new Vector2(-4f - i * 4f, 4f + (2 - i) * 4f - 4f);
                dot.sizeDelta = new Vector2(2.5f, 2.5f);
                dots[i] = CascadeUI.Image(dot, CascadeSprites.Circle(), Theme.TextTertiary, Image.Type.Simple);
                dots[i].raycastTarget = false;
            }

            Register(grip, CascadeLayerGroup.Chrome,
                press: () => _resizeStarted = false,
                release: EndResize,
                dragWorld: DragResize,
                hover: hovered =>
                {
                    for (int i = 0; i < dots.Length; i++)
                    {
                        dots[i].color = hovered ? (Color)Theme.TextPrimary : (Color)Theme.TextTertiary;
                    }
                },
                slider: true);
        }

        private void DragResize(Vector3 world)
        {
            if (_root == null || _canvasRect == null) return;

            Vector3 local3 = _canvasRect.InverseTransformPoint(world);
            Vector2 pointer = new Vector2(local3.x, local3.y);

            float scale = WindowScale();
            Vector2 size = _root.sizeDelta;
            Vector2 position = _root.anchoredPosition;

            // The top-left corner stays put; the bottom-right corner follows the pointer.
            Vector2 topLeft = new Vector2(position.x - size.x * scale * 0.5f, position.y + size.y * scale * 0.5f);
            Vector2 corner = new Vector2(topLeft.x + size.x * scale, topLeft.y - size.y * scale);

            if (!_resizeStarted)
            {
                _resizeOffset = corner - pointer;
                _resizeStarted = true;
            }

            Vector2 target = pointer + _resizeOffset;
            Rect canvas = _canvasRect.rect;
            float maxWidth = Mathf.Max(MinWindowWidth, (canvas.xMax - topLeft.x) / scale);
            float maxHeight = Mathf.Max(MinWindowHeight, (topLeft.y - canvas.yMin) / scale);

            float width = Mathf.Clamp((target.x - topLeft.x) / scale, MinWindowWidth, maxWidth);
            float height = Mathf.Clamp((topLeft.y - target.y) / scale, MinWindowHeight, maxHeight);

            _root.sizeDelta = new Vector2(width, height);
            _root.anchoredPosition = new Vector2(
                topLeft.x + width * scale * 0.5f,
                topLeft.y - height * scale * 0.5f);
            ClampToCanvas();

            _savedSize = _root.sizeDelta;
            UpdateWindowGlow(true);
            if (_menu != null) _menu.SetWindowOffset(_root.anchoredPosition);
        }

        private void EndResize()
        {
            _resizeStarted = false;
            if (_menu != null) _menu.SetWindowOffset(_root.anchoredPosition);

            // Sub-tab bar, 2-column cards, and width-dependent content reflow for the new size.
            UpdateWindowGlow(true);
            RebuildContent();
        }
    }
}

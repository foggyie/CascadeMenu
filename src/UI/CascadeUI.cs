using System;
using System.Collections.Generic;
using GorillaNetworking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CascadeMenu.UI
{
    internal enum CascadeLayerGroup
    {
        Chrome,
        Content,
        Overlay,
        Toast
    }

    internal sealed class CascadeInteractable
    {
        public RectTransform Rect;
        public CascadeLayerGroup Group;
        public int Order;
        public bool Enabled = true;
        public bool IsSlider;
        public Action Click;
        public Action Press;
        public Action Release;
        public Action<Vector3> DragWorld;
        public Action<bool> Hover;

        /// <summary>
        /// Timestamp of the last activation. Used to stop one physical press from
        /// firing several times when the finger press jitters on the collider.
        /// </summary>
        public float LastActivated = -10f;

        public bool IsAlive
        {
            get { return Rect != null && Rect.gameObject != null && Rect.gameObject.activeInHierarchy; }
        }

        public int LayerRank
        {
            get
            {
                switch (Group)
                {
                    case CascadeLayerGroup.Overlay: return 3;
                    case CascadeLayerGroup.Toast: return 2;
                    case CascadeLayerGroup.Chrome: return 1;
                    default: return 0;
                }
            }
        }

        public bool Contains(Vector3 worldPoint)
        {
            if (Rect == null) return false;
            Vector3 local = Rect.InverseTransformPoint(worldPoint);
            return Rect.rect.Contains(new Vector2(local.x, local.y));
        }
    }

    /// <summary>Small UI factory + shared helpers used by every Cascade widget.</summary>
    internal static class CascadeUI
    {
        private static TMP_FontAsset _font;
        private static bool _fontLogged;

        /// <summary>
        /// The Cascade window is viewed from a distance in VR. Its source layout is
        /// deliberately compact, so all TMP sizes get a small readability boost.
        /// </summary>
        public static float FontScale = 1.15f;

        public static TMP_FontAsset Font
        {
            get
            {
                if (_font != null) return _font;

                TMP_FontAsset gameFont = ResolveGameFont();
                TMP_FontAsset crispFont = TryCreateCrispFont();
                if (crispFont != null)
                {
                    if (gameFont != null)
                    {
                        crispFont.fallbackFontAssetTable = new List<TMP_FontAsset> { gameFont };
                    }
                    _font = crispFont;
                }
                else
                {
                    _font = gameFont;
                }

                if (!_fontLogged && _font != null)
                {
                    _fontLogged = true;
                    Debug.Log("[Cascade] UI font: " + _font.name +
                              " atlas=" + _font.atlasWidth + "x" + _font.atlasHeight +
                              " sampling=" + _font.faceInfo.pointSize.ToString("0.##") +
                              " mode=" + _font.atlasPopulationMode +
                              " fallback=" + (gameFont != null ? gameFont.name : "none"));
                }
                return _font;
            }
        }

        private static TMP_FontAsset ResolveGameFont()
        {
            TMP_FontAsset font = null;
            try
            {
                LocalisationFontPair pair;
                if (LocalisationManager.GetFontAssetForCurrentLocale(out pair) && pair.fontAsset != null)
                    font = pair.fontAsset;
            }
            catch
            {
                // The locale helper changes between game builds; TMP's default is a fine fallback.
            }

            if (font == null) font = TMP_Settings.defaultFontAsset;
            if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font == null)
            {
                TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                if (fonts != null && fonts.Length > 0) font = fonts[0];
            }
            return font;
        }

        private static TMP_FontAsset TryCreateCrispFont()
        {
            // Gorilla Tag ships a low-resolution pixel font for its own UI. Rendering
            // that font at Cascade's small sizes is what makes the window mushy, so
            // build a high-resolution dynamic SDF font from a clean system face instead.
            string[] families = { "Segoe UI", "Arial", "Helvetica Neue", "Noto Sans" };
            for (int i = 0; i < families.Length; i++)
            {
                try
                {
                    TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(families[i], "Regular", 64);
                    if (font == null) continue;
                    font.name = "Cascade UI " + families[i];
                    font.hideFlags = HideFlags.DontSave;
                    if (font.material == null || font.material.shader == null)
                    {
                        UnityEngine.Object.Destroy(font);
                        continue;
                    }
                    return font;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[Cascade] Could not create system font '" + families[i] + "': " + exception.Message);
                }
            }
            return null;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
            return rect;
        }

        public static RectTransform TopLeft(string name, Transform parent, float x, float y, float width, float height)
        {
            RectTransform rect = Rect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        public static RectTransform TopRight(string name, Transform parent, float right, float y, float width, float height)
        {
            RectTransform rect = Rect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-right, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        public static RectTransform Center(string name, Transform parent, float x, float y, float width, float height)
        {
            RectTransform rect = Rect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        public static void Stretch(RectTransform rect, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        public static Image Image(RectTransform rect, Sprite sprite, Color color, UnityEngine.UI.Image.Type type = UnityEngine.UI.Image.Type.Sliced)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = sprite != null ? type : UnityEngine.UI.Image.Type.Simple;
            image.raycastTarget = false;
            image.preserveAspect = false;
            return image;
        }

        /// <summary>
        /// Role-aware overload. Theme properties carry a <see cref="CascadeRole"/>
        /// so the created graphic can be repainted later without a scene search.
        /// </summary>
        public static Image Image(RectTransform rect, Sprite sprite, CascadeColor color, UnityEngine.UI.Image.Type type = UnityEngine.UI.Image.Type.Sliced)
        {
            Image image = Image(rect, sprite, (Color)color, type);
            CascadeTheme.Bind(image, color);
            return image;
        }

        public static Image Panel(RectTransform rect, Color color, int radius)
        {
            return Image(rect, CascadeSprites.Rounded(radius), color, UnityEngine.UI.Image.Type.Sliced);
        }

        public static Image Panel(RectTransform rect, CascadeColor color, int radius)
        {
            Image image = Panel(rect, (Color)color, radius);
            CascadeTheme.Bind(image, color);
            return image;
        }

        public static Image Solid(RectTransform rect, Color color)
        {
            return Image(rect, CascadeSprites.White(), color, UnityEngine.UI.Image.Type.Simple);
        }

        public static Image Solid(RectTransform rect, CascadeColor color)
        {
            Image image = Solid(rect, (Color)color);
            CascadeTheme.Bind(image, color);
            return image;
        }

        public static Image Outline(RectTransform rect, Color color, int radius, int thickness = 1)
        {
            return Image(rect, CascadeSprites.Outline(radius, thickness), color, UnityEngine.UI.Image.Type.Sliced);
        }

        public static Image Outline(RectTransform rect, CascadeColor color, int radius, int thickness = 1)
        {
            Image image = Outline(rect, (Color)color, radius, thickness);
            CascadeTheme.Bind(image, color);
            return image;
        }

        /// <summary>
        /// Clips the children of a viewport with a stencil mask.
        ///
        /// <see cref="RectMask2D"/> clips to an axis-aligned rectangle in canvas
        /// space, so as soon as the window is rotated its masked contents (the
        /// page rows and the sidebar tabs) disappear. A stencil mask is
        /// rasterised from the viewport's real, transformed shape, so clipping
        /// and scrolling keep working at any angle.
        /// </summary>
        public static void Clip(RectTransform rect)
        {
            Image image = Image(rect, CascadeSprites.White(), Color.white, UnityEngine.UI.Image.Type.Simple);
            image.raycastTarget = false;

            var mask = rect.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
        }

        public static TextMeshProUGUI Text(
            RectTransform rect,
            string text,
            float size,
            Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left,
            bool bold = false,
            bool wrap = false)
        {
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            TMP_FontAsset font = Font;
            if (font != null) label.font = font;
            label.fontSize = Mathf.Max(6f, size * FontScale);
            label.enableAutoSizing = false;
            label.color = color;
            label.alignment = alignment;
            label.richText = true;
            label.raycastTarget = false;
            label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.margin = Vector4.zero;
            label.extraPadding = false;
            return label;
        }

        public static TextMeshProUGUI Text(
            RectTransform rect,
            string text,
            float size,
            CascadeColor color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left,
            bool bold = false,
            bool wrap = false)
        {
            TextMeshProUGUI label = Text(rect, text, size, (Color)color, alignment, bold, wrap);
            CascadeTheme.Bind(label, color);
            return label;
        }

        public static TextMeshProUGUI Label(
            Transform parent,
            string name,
            string text,
            float size,
            Color color,
            float x,
            float y,
            float width,
            float height,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left,
            bool bold = false)
        {
            RectTransform rect = TopLeft(name, parent, x, y, width, height);
            return Text(rect, text, size, color, alignment, bold);
        }

        public static TextMeshProUGUI Label(
            Transform parent,
            string name,
            string text,
            float size,
            CascadeColor color,
            float x,
            float y,
            float width,
            float height,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left,
            bool bold = false)
        {
            RectTransform rect = TopLeft(name, parent, x, y, width, height);
            return Text(rect, text, size, color, alignment, bold);
        }

        public static void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null) return;
            CascadeTheme.UpdateAlpha(graphic, alpha);
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        public static Color Alpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        public static CascadeColor Alpha(CascadeColor color, float alpha)
        {
            return color.WithAlpha(alpha);
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        public static CascadeColor WithAlpha(CascadeColor color, float alpha)
        {
            return color.WithAlpha(alpha);
        }

        public static void SetTextColor(TMP_Text label, Color color)
        {
            if (label != null) label.color = color;
        }

        public static void SetTextColor(TMP_Text label, CascadeColor color)
        {
            if (label == null) return;
            label.color = color;
            CascadeTheme.Bind(label, color);
        }

        public static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max) return value ?? string.Empty;
            return value.Substring(0, Mathf.Max(0, max - 1)) + "…";
        }

        public static void DestroyChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
        }

        public static void ForceLayout(RectTransform rect)
        {
            if (rect == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }
    }

    /// <summary>Two-color vertical vertex tint for Image graphics (button gradients, glass sheen).</summary>
    internal sealed class CascadeGradient : BaseMeshEffect
    {
        private CascadeColor _top = Color.white;
        private CascadeColor _bottom = Color.white;

        public CascadeColor Top
        {
            get { return _top; }
            set
            {
                _top = value;
                CascadeTheme.Bind(this);
                if (graphic != null) graphic.SetVerticesDirty();
            }
        }

        public CascadeColor Bottom
        {
            get { return _bottom; }
            set
            {
                _bottom = value;
                CascadeTheme.Bind(this);
                if (graphic != null) graphic.SetVerticesDirty();
            }
        }

        /// <summary>Asks the owner graphic to rebuild with the current colors.</summary>
        public void Refresh()
        {
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vertexHelper)
        {
            if (!IsActive() || vertexHelper.currentVertCount == 0 || graphic == null) return;
            Rect rect = graphic.rectTransform.rect;
            if (rect.height <= 0.0001f) return;

            UIVertex vertex = new UIVertex();
            for (int i = 0; i < vertexHelper.currentVertCount; i++)
            {
                vertexHelper.PopulateUIVertex(ref vertex, i);
                float t = Mathf.InverseLerp(rect.yMin, rect.yMax, vertex.position.y);
                Color tint = Color.Lerp(Bottom, Top, t);
                vertex.color *= tint;
                vertexHelper.SetUIVertex(vertex, i);
            }
        }
    }
}

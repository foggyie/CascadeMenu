using System;
using System.Collections.Generic;
using System.Globalization;
using CascadeMenu.UI;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Shared helpers for the large batch of small mods: local player access,
    /// number formatting and the few pieces of rendering setup they all need
    /// (unlit materials, a soft circular particle texture and a single-draw
    /// billboard cloud).
    /// </summary>
    internal static class ModKit
    {
        private static Texture2D _softCircle;
        private static Sprite _softCircleSprite;

        internal static GTPlayer Player
        {
            get { return GTPlayer.Instance; }
        }

        internal static VRRig Rig
        {
            get { return VRRig.LocalRig; }
        }

        internal static Camera ViewCamera
        {
            get { return CascadeMenuBehaviour.ResolveViewCamera(); }
        }

        internal static Transform Head
        {
            get
            {
                Camera camera = ViewCamera;
                if (camera != null) return camera.transform;
                VRRig rig = Rig;
                return rig != null ? rig.transform : null;
            }
        }

        internal static Vector3 HeadPosition
        {
            get
            {
                Transform head = Head;
                return head != null ? head.position : Vector3.zero;
            }
        }

        internal static bool IsVr
        {
            get { return CascadeMenuBehaviour.IsVrActive(); }
        }

        internal static string F(float value, string format = "0.0")
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        internal static string Vec(Vector3 value)
        {
            return F(value.x) + ", " + F(value.y) + ", " + F(value.z);
        }

        internal static string Clock(double seconds)
        {
            if (seconds < 0d) seconds = 0d;
            int total = (int)seconds;
            int hours = total / 3600;
            int minutes = (total % 3600) / 60;
            int secs = total % 60;
            return hours > 0
                ? hours + ":" + minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + secs.ToString("00", CultureInfo.InvariantCulture)
                : minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + secs.ToString("00", CultureInfo.InvariantCulture);
        }

        internal static Color Hsv(float hue, float saturation = 0.85f, float value = 1f, float alpha = 1f)
        {
            Color color = Color.HSVToRGB(Mathf.Repeat(hue, 1f), Mathf.Clamp01(saturation), Mathf.Clamp01(value));
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        internal static Color Alpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        /// <summary>Unlit material used by every line, mesh and particle helper.</summary>
        internal static Material UnlitMaterial(Color color, Texture texture = null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("UI/Default");
            var material = new Material(shader);
            material.color = color;
            if (texture != null) material.mainTexture = texture;
            material.renderQueue = 4000;
            return material;
        }

        /// <summary>A white dot with a soft alpha falloff, built once.</summary>
        internal static Texture2D SoftCircleTexture()
        {
            if (_softCircle != null) return _softCircle;

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "CascadeSoftCircle";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha *= alpha;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            _softCircle = texture;
            return _softCircle;
        }

        internal static Sprite SoftCircleSprite()
        {
            if (_softCircleSprite != null) return _softCircleSprite;

            Texture2D texture = SoftCircleTexture();
            _softCircleSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            _softCircleSprite.name = "CascadeSoftCircle";
            return _softCircleSprite;
        }

        /// <summary>Creates a line renderer with the settings the menu already uses.</summary>
        internal static LineRenderer Line(string name, Transform parent, int sortingOrder, float width, bool loop = false)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = loop;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.material = UnlitMaterial(Color.white);
            line.sortingOrder = sortingOrder;
            line.positionCount = 0;
            return line;
        }

        /// <summary>Strips the collider from a primitive so it cannot move the player.</summary>
        internal static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 scale)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);

            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localScale = scale;

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = UnlitMaterial(Color.white);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sortingOrder = 240;
            }
            return go;
        }

        /// <summary>Recolours the renderer created by <see cref="Primitive"/>.</summary>
        internal static void Paint(GameObject go, Color color)
        {
            if (go == null) return;
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = UnlitMaterial(color);
        }

        /// <summary>Best-effort toast in the menu notification stack.</summary>
        internal static void Notify(string title, string message)
        {
            try
            {
                StandaloneNotificationOverlay.Notify(title, message);
            }
            catch
            {
                // A missing overlay must never break a mod action.
            }
        }

        private static Sprite _vignette;
        private static Sprite _scanlines;
        private static Sprite _grid;

        /// <summary>Dark edge gradient used by the vignette and night vision.</summary>
        internal static Sprite VignetteSprite()
        {
            if (_vignette != null) return _vignette;

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "CascadeVignette";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.05f, distance));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            _vignette = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            _vignette.name = "CascadeVignette";
            return _vignette;
        }

        /// <summary>Thin horizontal lines for the scanline filter.</summary>
        internal static Sprite ScanlineSprite()
        {
            if (_scanlines != null) return _scanlines;

            const int height = 256;
            const int width = 4;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "CascadeScanlines";
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Point;

            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                byte alpha = (byte)(y % 4 == 0 ? 220 : 0);
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            _scanlines = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
            _scanlines.name = "CascadeScanlines";
            return _scanlines;
        }

        /// <summary>Crossed guide lines on a transparent background.</summary>
        internal static Sprite GridSprite()
        {
            if (_grid != null) return _grid;

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "CascadeGrid";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool line = x == size / 3 || x == size * 2 / 3 || y == size / 3 || y == size * 2 / 3;
                    bool edge = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    byte alpha = (byte)(line ? 150 : (edge ? 90 : 0));
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            _grid = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            _grid.name = "CascadeGrid";
            return _grid;
        }
    }

    /// <summary>
    /// A camera-parented canvas that covers the whole view. Used by the
    /// full-screen visual filters, which build their own images on top of it.
    /// </summary>
    internal sealed class ScreenCanvas
    {
        private const float Size = 1600f;

        private Camera _camera;
        private readonly float _distance;

        internal GameObject Root { get; }
        internal Canvas Canvas { get; }
        internal RectTransform Rect { get; }

        internal ScreenCanvas(string name, int sortingOrder, float distance = 0.4f)
        {
            _distance = distance;

            Root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            Canvas = Root.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.WorldSpace;
            Canvas.sortingOrder = sortingOrder;

            Rect = (RectTransform)Root.transform;
            Rect.anchorMin = new Vector2(0.5f, 0.5f);
            Rect.anchorMax = new Vector2(0.5f, 0.5f);
            Rect.pivot = new Vector2(0.5f, 0.5f);
            Rect.sizeDelta = new Vector2(Size, Size);
            Rect.localScale = Vector3.one * 0.001f;
            Rect.localPosition = new Vector3(0f, 0f, _distance);
            Rect.localRotation = Quaternion.identity;

            CanvasGroup group = Root.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Follow();
        }

        /// <summary>Reparents to the active camera. Returns true when it moved.</summary>
        internal bool Follow()
        {
            if (Root == null) return false;

            Camera camera = ModKit.ViewCamera;
            if (camera == null || camera == _camera) return false;

            _camera = camera;
            Canvas.worldCamera = camera;
            Root.transform.SetParent(camera.transform, false);
            Rect.localPosition = new Vector3(0f, 0f, _distance);
            Rect.localRotation = Quaternion.identity;
            return true;
        }

        /// <summary>A centred image sized in canvas units.</summary>
        internal Image Image(string name, Sprite sprite, Color color, float width, float height)
        {
            RectTransform rect = CascadeUI.Rect(name, Rect);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, height);
            return CascadeUI.Image(rect, sprite, color, UnityEngine.UI.Image.Type.Simple);
        }

        internal void Destroy()
        {
            if (Root != null) UnityEngine.Object.Destroy(Root);
        }
    }

    /// <summary>
    /// One draw call for a few hundred camera-facing coloured quads. Used by
    /// the particle-style fun and visual mods so they do not spawn hundreds of
    /// renderers.
    /// </summary>
    internal sealed class BillboardCloud
    {
        private readonly Mesh _mesh;
        private readonly MeshRenderer _renderer;
        private readonly List<Vector3> _positions = new List<Vector3>(256);
        private readonly List<Color> _particleColors = new List<Color>(256);
        private readonly List<float> _sizes = new List<float>(256);
        private readonly List<Vector3> _vertices = new List<Vector3>(1024);
        private readonly List<Vector2> _uvs = new List<Vector2>(1024);
        private readonly List<Color> _colors = new List<Color>(1024);
        private readonly List<int> _triangles = new List<int>(1536);

        internal GameObject Root { get; }

        internal BillboardCloud(string name, int capacity, bool soft)
        {
            Root = new GameObject(name);

            var filter = Root.AddComponent<MeshFilter>();
            _renderer = Root.AddComponent<MeshRenderer>();
            _mesh = new Mesh();
            _mesh.name = name + "Mesh";
            _mesh.MarkDynamic();
            filter.sharedMesh = _mesh;

            _renderer.sharedMaterial = ModKit.UnlitMaterial(
                Color.white,
                soft ? ModKit.SoftCircleTexture() : null);
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.sortingOrder = 240;

            _positions.Capacity = capacity;
            _particleColors.Capacity = capacity;
            _sizes.Capacity = capacity;
        }

        internal void Begin()
        {
            _positions.Clear();
            _particleColors.Clear();
            _sizes.Clear();
        }

        internal void Add(Vector3 position, float size, Color color)
        {
            _positions.Add(position);
            _sizes.Add(size);
            _particleColors.Add(color);
        }

        internal void End(Camera camera)
        {
            if (_positions.Count == 0)
            {
                _mesh.Clear();
                return;
            }

            Transform view = camera != null ? camera.transform : null;
            Vector3 right = view != null ? view.right : Vector3.right;
            Vector3 up = view != null ? view.up : Vector3.up;

            _vertices.Clear();
            _uvs.Clear();
            _colors.Clear();
            _triangles.Clear();

            for (int i = 0; i < _positions.Count; i++)
            {
                float half = Mathf.Max(0.001f, _sizes[i] * 0.5f);
                Vector3 offsetRight = right * half;
                Vector3 offsetUp = up * half;
                Vector3 point = _positions[i];
                Color color = _particleColors[i];

                int baseIndex = _vertices.Count;
                _vertices.Add(point - offsetRight - offsetUp);
                _vertices.Add(point - offsetRight + offsetUp);
                _vertices.Add(point + offsetRight + offsetUp);
                _vertices.Add(point + offsetRight - offsetUp);

                _uvs.Add(new Vector2(0f, 0f));
                _uvs.Add(new Vector2(0f, 1f));
                _uvs.Add(new Vector2(1f, 1f));
                _uvs.Add(new Vector2(1f, 0f));

                _colors.Add(color);
                _colors.Add(color);
                _colors.Add(color);
                _colors.Add(color);

                _triangles.Add(baseIndex);
                _triangles.Add(baseIndex + 2);
                _triangles.Add(baseIndex + 1);
                _triangles.Add(baseIndex);
                _triangles.Add(baseIndex + 3);
                _triangles.Add(baseIndex + 2);
            }

            _mesh.SetVertices(_vertices);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.bounds = new Bounds(
                view != null ? view.position : Vector3.zero,
                Vector3.one * 600f);
        }

        internal void Destroy()
        {
            if (Root != null) UnityEngine.Object.Destroy(Root);
        }
    }
}

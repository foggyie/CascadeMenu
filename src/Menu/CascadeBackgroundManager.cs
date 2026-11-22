using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using BepInEx;
using UnityEngine;

namespace CascadeMenu.Menu
{
    /// <summary>
    /// Manages custom window background images stored in BepInEx/config/CascadeMenu/Backgrounds/
    /// (or any user-selected path). Loads PNG/JPG/JPEG bytes straight into memory so files are
    /// never locked on disk, bakes center-cover aspect ratio & macOS rounded corners into an
    /// in-memory Texture2D (100% VR stereo-safe with zero stencil mask artifacts), and provides
    /// an async Windows PowerShell OpenFileDialog picker that never freezes Unity or VR.
    /// </summary>
    internal static class CascadeBackgroundManager
    {
        private static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg" };

        private static Texture2D _currentTexture;
        private static Texture2D _frostedTexture;
        private static string _loadedKey = string.Empty;
        private static string _loadedName = "None";
        private static int _version;

        private static readonly object PickLock = new object();
        private static bool _browseInProgress;
        private static bool _hasPendingPick;
        private static string _pendingPickPath;
        private static string _pendingPickError;

        internal static Texture2D CurrentTexture => _currentTexture;
        internal static Texture2D FrostedTexture => _frostedTexture != null ? _frostedTexture : _currentTexture;
        internal static string CurrentName => _loadedName;
        internal static int Version => _version;
        internal static bool IsBrowsing => _browseInProgress;

        internal static string BackgroundsDirectory
        {
            get
            {
                string root = !string.IsNullOrEmpty(Paths.ConfigPath)
                    ? Paths.ConfigPath
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BepInEx", "config");
                return Path.Combine(root, "CascadeMenu", "Backgrounds");
            }
        }

        internal static void EnsureDirectory()
        {
            try
            {
                string dir = BackgroundsDirectory;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("Cascade backgrounds folder creation failed: " + ex.Message);
            }
        }

        /// <summary>Returns ["None", file1.png, file2.jpg, ...] from the Backgrounds folder.</summary>
        internal static string[] ListImageNames(string activeConfigValue)
        {
            EnsureDirectory();
            var list = new List<string> { "None" };

            try
            {
                string dir = BackgroundsDirectory;
                if (Directory.Exists(dir))
                {
                    string[] files = Directory.GetFiles(dir);
                    Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < files.Length; i++)
                    {
                        if (IsSupportedImage(files[i]))
                        {
                            list.Add(Path.GetFileName(files[i]));
                        }
                    }
                }
            }
            catch { }

            if (!string.IsNullOrWhiteSpace(activeConfigValue))
            {
                string activeName = Path.GetFileName(activeConfigValue.Trim());
                bool found = false;
                for (int i = 1; i < list.Count; i++)
                {
                    if (string.Equals(list[i], activeName, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found && !string.IsNullOrEmpty(activeName))
                {
                    list.Add(activeName);
                }
            }

            return list.ToArray();
        }

        internal static int IndexOfImage(string[] options, string configValue)
        {
            if (options == null || options.Length == 0 || string.IsNullOrWhiteSpace(configValue)) return 0;
            string target = Path.GetFileName(configValue.Trim());
            for (int i = 1; i < options.Length; i++)
            {
                if (string.Equals(options[i], target, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return 0;
        }

        internal static void SyncFromConfig(MenuConfig config, bool forceReload = false)
        {
            if (config == null) return;
            string value = (config.BackgroundImagePath.Value ?? string.Empty).Trim();
            if (!forceReload && string.Equals(value, _loadedKey, StringComparison.OrdinalIgnoreCase))
                return;

            _loadedKey = value;

            if (string.IsNullOrEmpty(value) || string.Equals(value, "None", StringComparison.OrdinalIgnoreCase))
            {
                ReleaseTexture();
                _loadedName = "None";
                _version++;
                return;
            }

            string fullPath = ResolvePath(value);
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            {
                ReleaseTexture();
                _loadedName = "Not found";
                _version++;
                return;
            }

            if (LoadFromDiskToMemory(fullPath))
            {
                _loadedName = Path.GetFileName(fullPath);
                _version++;
            }
            else
            {
                ReleaseTexture();
                _loadedName = "Load failed";
                _version++;
            }
        }

        /// <summary>Opens the Backgrounds folder in Windows File Explorer.</summary>
        internal static void OpenFolderInExplorer()
        {
            EnsureDirectory();
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "\"" + BackgroundsDirectory + "\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("Could not open backgrounds folder: " + ex.Message);
            }
        }

        /// <summary>
        /// Launches a Windows PowerShell OpenFileDialog on a background thread so
        /// Gorilla Tag / VR never stalls while the user picks a PNG/JPG/JPEG file.
        /// </summary>
        internal static bool StartBrowseDialog()
        {
            lock (PickLock)
            {
                if (_browseInProgress) return false;
                _browseInProgress = true;
                _hasPendingPick = false;
                _pendingPickPath = null;
                _pendingPickError = null;
            }

            EnsureDirectory();
            string initialDir = BackgroundsDirectory.Replace("'", "''");

            ThreadPool.QueueUserWorkItem(_ =>
            {
                string picked = null;
                string error = null;
                try
                {
                    string script =
                        "Add-Type -AssemblyName System.Windows.Forms; " +
                        "$d = New-Object System.Windows.Forms.OpenFileDialog; " +
                        "$d.Title = 'Select Cascade Menu Background Image'; " +
                        "$d.Filter = 'Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All Files (*.*)|*.*'; " +
                        "$d.InitialDirectory = '" + initialDir + "'; " +
                        "$d.RestoreDirectory = $true; " +
                        "$f = New-Object System.Windows.Forms.Form; $f.TopMost = $true; $f.StartPosition = 'CenterScreen'; " +
                        "if ($d.ShowDialog($f) -eq [System.Windows.Forms.DialogResult]::OK) { [Console]::Out.Write($d.FileName) }; " +
                        "$f.Dispose();";

                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -NonInteractive -STA -WindowStyle Hidden -Command \"" + script + "\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using (Process proc = Process.Start(psi))
                    {
                        if (proc != null)
                        {
                            picked = proc.StandardOutput.ReadToEnd().Trim();
                            proc.WaitForExit();
                        }
                    }

                    if (!string.IsNullOrEmpty(picked) && File.Exists(picked))
                    {
                        // Copy into BepInEx/config/CascadeMenu/Backgrounds so it stays organized & portable
                        EnsureDirectory();
                        string destName = Path.GetFileName(picked);
                        string destPath = Path.Combine(BackgroundsDirectory, destName);
                        if (!string.Equals(Path.GetFullPath(picked), Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
                        {
                            File.Copy(picked, destPath, true);
                        }
                        picked = destName;
                    }
                    else
                    {
                        picked = null;
                    }
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }

                lock (PickLock)
                {
                    _pendingPickPath = picked;
                    _pendingPickError = error;
                    _hasPendingPick = true;
                    _browseInProgress = false;
                }
            });

            return true;
        }

        /// <summary>Called on Unity main thread from CascadeMenuBehaviour.Update().</summary>
        internal static bool TryConsumePendingPick(out string fileName, out string error)
        {
            lock (PickLock)
            {
                if (!_hasPendingPick)
                {
                    fileName = null;
                    error = null;
                    return false;
                }

                fileName = _pendingPickPath;
                error = _pendingPickError;
                _hasPendingPick = false;
                _pendingPickPath = null;
                _pendingPickError = null;
                return true;
            }
        }

        private static string ResolvePath(string value)
        {
            if (Path.IsPathRooted(value) && File.Exists(value))
                return value;

            string inFolder = Path.Combine(BackgroundsDirectory, value);
            if (File.Exists(inFolder))
                return inFolder;

            return value;
        }

        private static bool IsSupportedImage(string path)
        {
            string ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext)) return false;
            for (int i = 0; i < SupportedExtensions.Length; i++)
            {
                if (string.Equals(ext, SupportedExtensions[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Reads the image file into memory, stretches the full image to fit the
        /// 850x530 UI (zero cutoff, smooth bilinear sampling), bakes 12px rounded
        /// corners, and also generates a frosted-glass blurred variant in memory so
        /// Clear / Frosted / Solid modes apply identically to the uploaded picture.
        /// </summary>
        private static bool LoadFromDiskToMemory(string fullPath)
        {
            Texture2D raw = null;
            try
            {
                byte[] bytes = File.ReadAllBytes(fullPath);
                if (bytes == null || bytes.Length == 0) return false;

                raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(raw, bytes, false))
                {
                    UnityEngine.Object.Destroy(raw);
                    return false;
                }

                // Stretch full image (u = 0..1, v = 0..1) cleanly to fit the UI with zero cutoff
                const int outW = 850;
                const int outH = 530;
                const float cornerRadius = 12f;

                int srcW = raw.width;
                int srcH = raw.height;
                Color32[] srcPixels = raw.GetPixels32();
                Color32[] sharpPixels = new Color32[outW * outH];
                byte[] cornerAlpha = new byte[outW * outH];

                float halfW = outW * 0.5f;
                float halfH = outH * 0.5f;
                float innerW = halfW - cornerRadius - 0.5f;
                float innerH = halfH - cornerRadius - 0.5f;

                int maxSrcX = Mathf.Max(0, srcW - 1);
                int maxSrcY = Mathf.Max(0, srcH - 1);

                for (int y = 0; y < outH; y++)
                {
                    float fy = (y / (float)(outH - 1)) * maxSrcY;
                    int y0 = (int)fy;
                    int y1 = Mathf.Min(y0 + 1, maxSrcY);
                    float ty = fy - y0;
                    int row0 = y0 * srcW;
                    int row1 = y1 * srcW;
                    int dyRow = y * outW;

                    float dyCorner = Mathf.Abs((y + 0.5f) - halfH) - innerH;

                    for (int x = 0; x < outW; x++)
                    {
                        float fx = (x / (float)(outW - 1)) * maxSrcX;
                        int x0 = (int)fx;
                        int x1 = Mathf.Min(x0 + 1, maxSrcX);
                        float tx = fx - x0;

                        // Bilinear sample so stretched images stay smooth and crisp
                        Color32 c00 = srcPixels[row0 + x0];
                        Color32 c10 = srcPixels[row0 + x1];
                        Color32 c01 = srcPixels[row1 + x0];
                        Color32 c11 = srcPixels[row1 + x1];

                        float invTx = 1f - tx;
                        float invTy = 1f - ty;
                        float w00 = invTx * invTy;
                        float w10 = tx * invTy;
                        float w01 = invTx * ty;
                        float w11 = tx * ty;

                        byte r = (byte)Mathf.RoundToInt(c00.r * w00 + c10.r * w10 + c01.r * w01 + c11.r * w11);
                        byte g = (byte)Mathf.RoundToInt(c00.g * w00 + c10.g * w10 + c01.g * w01 + c11.g * w11);
                        byte b = (byte)Mathf.RoundToInt(c00.b * w00 + c10.b * w10 + c01.b * w01 + c11.b * w11);
                        byte a = (byte)Mathf.RoundToInt(c00.a * w00 + c10.a * w10 + c01.a * w01 + c11.a * w11);

                        float cov = 1f;
                        if (dyCorner > -2f)
                        {
                            float dxCorner = Mathf.Abs((x + 0.5f) - halfW) - innerW;
                            if (dxCorner > -2f)
                            {
                                float ox = Mathf.Max(dxCorner, 0f);
                                float oy = Mathf.Max(dyCorner, 0f);
                                float sdf = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(dxCorner, dyCorner), 0f) - cornerRadius;
                                cov = Mathf.Clamp01(0.5f - sdf);
                            }
                        }

                        byte finalA = (byte)Mathf.RoundToInt(a * cov);
                        cornerAlpha[dyRow + x] = finalA;
                        sharpPixels[dyRow + x] = new Color32(r, g, b, finalA);
                    }
                }

                // Generate frosted-glass blurred version on a downscaled buffer for fast, silky blur
                Texture2D frostedTex = BuildFrostedTexture(sharpPixels, cornerAlpha, outW, outH);

                Texture2D sharpTex = new Texture2D(outW, outH, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                    name = "CascadeCustomBackgroundSharp"
                };
                sharpTex.SetPixels32(sharpPixels);
                sharpTex.Apply(false, true);

                UnityEngine.Object.Destroy(raw);
                ReleaseTexture();
                _currentTexture = sharpTex;
                _frostedTexture = frostedTex;
                return true;
            }
            catch (Exception ex)
            {
                if (raw != null) UnityEngine.Object.Destroy(raw);
                Plugin.Log?.LogWarning("Cascade background load failed: " + ex.Message);
                return false;
            }
        }

        private static Texture2D BuildFrostedTexture(Color32[] sharpPixels, byte[] cornerAlpha, int w, int h)
        {
            // Downsample 4x -> separable box blur x2 -> smooth bilinear upsample for creamy frosted glass
            const int bw = 212;
            const int bh = 132;
            var small = new Color[bw * bh];
            var temp = new Color[bw * bh];

            for (int y = 0; y < bh; y++)
            {
                int sy = Mathf.Clamp((int)((y + 0.5f) * h / bh), 0, h - 1) * w;
                int dy = y * bw;
                for (int x = 0; x < bw; x++)
                {
                    int sx = Mathf.Clamp((int)((x + 0.5f) * w / bw), 0, w - 1);
                    small[dy + x] = sharpPixels[sy + sx];
                }
            }

            // Two separable passes (radius 4) produce a smooth Gaussian-like frosted blur
            for (int pass = 0; pass < 2; pass++)
            {
                BoxBlurH(small, temp, bw, bh, 4);
                BoxBlurV(temp, small, bw, bh, 4);
            }

            // Upsample back with subtle cool frost lift + exact 12px rounded corners
            var frostedPixels = new Color32[w * h];
            int maxBx = bw - 1;
            int maxBy = bh - 1;
            for (int y = 0; y < h; y++)
            {
                float fy = (y / (float)(h - 1)) * maxBy;
                int y0 = (int)fy;
                int y1 = Mathf.Min(y0 + 1, maxBy);
                float ty = fy - y0;
                int r0 = y0 * bw;
                int r1 = y1 * bw;
                int rowOut = y * w;

                for (int x = 0; x < w; x++)
                {
                    float fx = (x / (float)(w - 1)) * maxBx;
                    int x0 = (int)fx;
                    int x1 = Mathf.Min(x0 + 1, maxBx);
                    float tx = fx - x0;

                    Color c = Color.Lerp(
                        Color.Lerp(small[r0 + x0], small[r0 + x1], tx),
                        Color.Lerp(small[r1 + x0], small[r1 + x1], tx),
                        ty);

                    // Subtle cool frosted-glass veil
                    c.r = Mathf.Lerp(c.r, 0.88f, 0.08f);
                    c.g = Mathf.Lerp(c.g, 0.93f, 0.08f);
                    c.b = Mathf.Lerp(c.b, 1.00f, 0.09f);

                    Color32 c32 = c;
                    c32.a = cornerAlpha[rowOut + x];
                    frostedPixels[rowOut + x] = c32;
                }
            }

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
                name = "CascadeCustomBackgroundFrosted"
            };
            tex.SetPixels32(frostedPixels);
            tex.Apply(false, true);
            return tex;
        }

        private static void BoxBlurH(Color[] src, Color[] dst, int w, int h, int radius)
        {
            float inv = 1f / (radius * 2 + 1);
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    float r = 0f, g = 0f, b = 0f;
                    for (int k = -radius; k <= radius; k++)
                    {
                        Color c = src[row + Mathf.Clamp(x + k, 0, w - 1)];
                        r += c.r; g += c.g; b += c.b;
                    }
                    dst[row + x] = new Color(r * inv, g * inv, b * inv, 1f);
                }
            }
        }

        private static void BoxBlurV(Color[] src, Color[] dst, int w, int h, int radius)
        {
            float inv = 1f / (radius * 2 + 1);
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    float r = 0f, g = 0f, b = 0f;
                    for (int k = -radius; k <= radius; k++)
                    {
                        Color c = src[Mathf.Clamp(y + k, 0, h - 1) * w + x];
                        r += c.r; g += c.g; b += c.b;
                    }
                    dst[y * w + x] = new Color(r * inv, g * inv, b * inv, 1f);
                }
            }
        }

        internal static void ReleaseTexture()
        {
            if (_currentTexture != null)
            {
                UnityEngine.Object.Destroy(_currentTexture);
                _currentTexture = null;
            }
            if (_frostedTexture != null)
            {
                UnityEngine.Object.Destroy(_frostedTexture);
                _frostedTexture = null;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Tools;
using CascadeMenu.Mods.Cosmetics;
using CascadeMenu.Mods.Visual;
using CascadeMenu.Mods.Movement;
using CascadeMenu.Mods.Overlays;

namespace CascadeMenu.Mods.Desktop
{
    /// <summary>
    /// Native Windows 10/11 bridge for enumerating desktop windows, capturing
    /// their live pixel buffer into a Unity Texture2D, and forwarding VR laser
    /// mouse / keyboard input via Win32 SendInput.
    /// </summary>
    internal static class Win32DesktopBridge
    {
        internal sealed class WindowEntry
        {
            public IntPtr Hwnd;
            public string Title;
            public string ProcessName;
            public int Width;
            public int Height;

            public bool IsFullScreen => Hwnd == IntPtr.Zero;

            public string DisplayLabel
            {
                get
                {
                    if (IsFullScreen) return "Full Screen (Primary Monitor)";
                    string proc = string.IsNullOrEmpty(ProcessName) ? "" : "[" + ProcessName + "] ";
                    string title = string.IsNullOrEmpty(Title) ? "Untitled" : Title;
                    string full = proc + title;
                    return full.Length > 44 ? full.Substring(0, 41) + "..." : full;
                }
            }
        }

        // -----------------------------------------------------------------
        // Win32 Constants
        // -----------------------------------------------------------------
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;
        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_TOOLWINDOW = 0x00000080L;
        private const int DWMWA_CLOAKED = 14;
        private const uint PW_RENDERFULLCONTENT = 0x00000002;
        private const uint SRCCOPY = 0x00CC0020;
        private const int COLORONCOLOR = 3;
        private const uint DIB_RGB_COLORS = 0;
        private const int SW_RESTORE = 9;

        private const uint INPUT_MOUSE = 0;
        private const uint INPUT_KEYBOARD = 1;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;

        private const uint WM_MOUSEMOVE = 0x0200;
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_RBUTTONDOWN = 0x0204;
        private const uint WM_RBUTTONUP = 0x0205;
        private const uint WM_MOUSEWHEEL = 0x020A;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const uint WM_CHAR = 0x0102;
        private const int MK_LBUTTON = 0x0001;
        private const int MK_RBUTTON = 0x0002;
        private const uint CWP_SKIPINVISIBLE = 0x0001;
        private const uint CWP_SKIPDISABLED = 0x0002;

        private const uint WM_GETICON = 0x007F;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;
        private const int ICON_SMALL2 = 2;
        private const int GCLP_HICON = -14;
        private const int GCLP_HICONSM = -34;
        private const uint SMTO_ABORTIFHUNG = 0x0002;
        private const uint DI_NORMAL = 0x0003;
        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        // -----------------------------------------------------------------
        // Win32 Structs (x64-safe explicit layout for SendInput)
        // -----------------------------------------------------------------
        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            public uint bmiColors;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        // -----------------------------------------------------------------
        // P/Invoke Imports
        // -----------------------------------------------------------------
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        internal static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        internal static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr ChildWindowFromPointEx(IntPtr hwndParent, POINT pt, uint uFlags);

        [DllImport("user32.dll", EntryPoint = "PostMessageW")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        internal static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam,
            uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")]
        private static extern IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon,
            int cxWidth, int cyWidth, uint istepIfAniCur, IntPtr hbrFlickerFreeDraw, uint diFlags);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll", EntryPoint = "LoadCursorW")]
        private static extern IntPtr LoadCursor(IntPtr hInstance, IntPtr lpCursorName);

        [StructLayout(LayoutKind.Sequential)]
        private struct ICONINFO
        {
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [DllImport("user32.dll")]
        private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
            ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint iUsage,
            out IntPtr ppvBits, IntPtr hSection, uint dwOffset);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
            IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

        [DllImport("gdi32.dll")]
        private static extern bool StretchBlt(IntPtr hdcDest, int nXOriginDest, int nYOriginDest, int nWidthDest, int nHeightDest,
            IntPtr hdcSrc, int nXOriginSrc, int nYOriginSrc, int nWidthSrc, int nHeightSrc, uint dwRop);

        [DllImport("gdi32.dll")]
        private static extern int SetStretchBltMode(IntPtr hdc, int iStretchMode);

        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines,
            [Out] byte[] lpvBits, ref BITMAPINFO lpbi, uint uUsage);

        // -----------------------------------------------------------------
        // Window Enumeration
        // -----------------------------------------------------------------
        internal static List<WindowEntry> EnumerateWindows()
        {
            var results = new List<WindowEntry>();
            int screenW = Math.Max(640, GetSystemMetrics(SM_CXSCREEN));
            int screenH = Math.Max(480, GetSystemMetrics(SM_CYSCREEN));

            results.Add(new WindowEntry
            {
                Hwnd = IntPtr.Zero,
                Title = "Full Screen (Primary Monitor)",
                ProcessName = "Desktop",
                Width = screenW,
                Height = screenH
            });

            uint selfPid = 0;
            try { selfPid = (uint)Process.GetCurrentProcess().Id; } catch { }

            try
            {
                EnumWindows((hwnd, lParam) =>
                {
                    if (!IsWindowVisible(hwnd)) return true;

                    int len = GetWindowTextLength(hwnd);
                    if (len <= 0) return true;

                    try
                    {
                        long exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
                        if ((exStyle & WS_EX_TOOLWINDOW) != 0) return true;
                    }
                    catch { }

                    try
                    {
                        if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
                            return true;
                    }
                    catch { }

                    GetWindowThreadProcessId(hwnd, out uint pid);
                    if (pid == selfPid) return true;

                    bool iconic = IsIconic(hwnd);
                    RECT rect = default;
                    if (!iconic)
                    {
                        if (!GetWindowRect(hwnd, out rect)) return true;
                        if (rect.Width < 120 || rect.Height < 80) return true;
                    }
                    else
                    {
                        rect = new RECT { Left = 0, Top = 0, Right = 1280, Bottom = 720 };
                    }

                    var sb = new StringBuilder(len + 1);
                    GetWindowText(hwnd, sb, sb.Capacity);
                    string title = sb.ToString().Trim();
                    if (string.IsNullOrEmpty(title)) return true;
                    if (title.Equals("Program Manager", StringComparison.OrdinalIgnoreCase)) return true;

                    string procName = "";
                    try
                    {
                        using (Process p = Process.GetProcessById((int)pid))
                        {
                            procName = p.ProcessName;
                        }
                    }
                    catch { }

                    results.Add(new WindowEntry
                    {
                        Hwnd = hwnd,
                        Title = title,
                        ProcessName = procName,
                        Width = rect.Width,
                        Height = rect.Height
                    });
                    return true;
                }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("[WindowsOverlay] EnumWindows failed: " + ex.Message);
            }

            return results;
        }

        internal static bool TryGetTargetRect(IntPtr hwnd, out RECT rect)
        {
            if (hwnd == IntPtr.Zero)
            {
                rect = new RECT
                {
                    Left = 0,
                    Top = 0,
                    Right = Math.Max(640, GetSystemMetrics(SM_CXSCREEN)),
                    Bottom = Math.Max(480, GetSystemMetrics(SM_CYSCREEN))
                };
                return true;
            }

            if (!IsWindow(hwnd))
            {
                rect = default;
                return false;
            }

            return GetWindowRect(hwnd, out rect) && rect.Width > 0 && rect.Height > 0;
        }

        /// <summary>
        /// Extracts the real Windows system default arrow cursor (IDC_ARROW = 32512)
        /// into a 32x32 Texture2D along with its normalized Unity Sprite pivot (hotspot).
        /// </summary>
        internal static Texture2D ExtractSystemArrowCursor(out Vector2 pivot)
        {
            pivot = new Vector2(0.06f, 0.94f);
            try
            {
                IntPtr hCursor = LoadCursor(IntPtr.Zero, (IntPtr)32512); // IDC_ARROW
                if (hCursor == IntPtr.Zero) return null;

                const int size = 32;
                if (GetIconInfo(hCursor, out ICONINFO info))
                {
                    float px = Mathf.Clamp01(info.xHotspot / (float)size);
                    float py = Mathf.Clamp01(1f - info.yHotspot / (float)size);
                    pivot = new Vector2(px, py);
                    if (info.hbmColor != IntPtr.Zero) DeleteObject(info.hbmColor);
                    if (info.hbmMask != IntPtr.Zero) DeleteObject(info.hbmMask);
                }

                IntPtr screenDc = IntPtr.Zero;
                IntPtr memDc = IntPtr.Zero;
                IntPtr hBmp = IntPtr.Zero;
                IntPtr oldBmp = IntPtr.Zero;

                try
                {
                    screenDc = GetDC(IntPtr.Zero);
                    memDc = CreateCompatibleDC(screenDc);

                    var bmi = new BITMAPINFO();
                    bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                    bmi.bmiHeader.biWidth = size;
                    bmi.bmiHeader.biHeight = size; // bottom-up matches Unity Texture2D
                    bmi.bmiHeader.biPlanes = 1;
                    bmi.bmiHeader.biBitCount = 32;
                    bmi.bmiHeader.biCompression = 0;

                    hBmp = CreateDIBSection(memDc, ref bmi, DIB_RGB_COLORS, out IntPtr bitsPtr, IntPtr.Zero, 0);
                    if (hBmp == IntPtr.Zero || bitsPtr == IntPtr.Zero) return null;

                    oldBmp = SelectObject(memDc, hBmp);
                    DrawIconEx(memDc, 0, 0, hCursor, size, size, 0, IntPtr.Zero, DI_NORMAL);

                    byte[] bytes = new byte[size * size * 4];
                    Marshal.Copy(bitsPtr, bytes, 0, bytes.Length);

                    bool anyAlpha = false;
                    for (int i = 3; i < bytes.Length; i += 4)
                    {
                        if (bytes[i] != 0) { anyAlpha = true; break; }
                    }
                    if (!anyAlpha)
                    {
                        // Monochrome XOR/AND cursor fallback: make drawn pixels opaque
                        for (int i = 0; i < bytes.Length; i += 4)
                        {
                            if (bytes[i] != 0 || bytes[i + 1] != 0 || bytes[i + 2] != 0)
                                bytes[i + 3] = 255;
                        }
                    }

                    var tex = new Texture2D(size, size, TextureFormat.BGRA32, false)
                    {
                        name = "CascadeWinDefaultCursor",
                        filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Clamp
                    };
                    tex.LoadRawTextureData(bytes);
                    tex.Apply(false, false);
                    return tex;
                }
                finally
                {
                    if (oldBmp != IntPtr.Zero && memDc != IntPtr.Zero) SelectObject(memDc, oldBmp);
                    if (hBmp != IntPtr.Zero) DeleteObject(hBmp);
                    if (memDc != IntPtr.Zero) DeleteDC(memDc);
                    if (screenDc != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screenDc);
                }
            }
            catch
            {
                return null;
            }
        }

        // -----------------------------------------------------------------
        // Real Windows App Icon Extraction (HICON -> Texture2D)
        // -----------------------------------------------------------------
        internal static Texture2D ExtractWindowIcon(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return null;

            IntPtr hIcon = IntPtr.Zero;
            bool ownsIcon = false;

            try
            {
                if (SendMessageTimeout(hwnd, WM_GETICON, (IntPtr)ICON_BIG, IntPtr.Zero, SMTO_ABORTIFHUNG, 60, out IntPtr res) != IntPtr.Zero && res != IntPtr.Zero)
                    hIcon = res;
                if (hIcon == IntPtr.Zero && SendMessageTimeout(hwnd, WM_GETICON, (IntPtr)ICON_SMALL2, IntPtr.Zero, SMTO_ABORTIFHUNG, 60, out res) != IntPtr.Zero && res != IntPtr.Zero)
                    hIcon = res;
                if (hIcon == IntPtr.Zero && SendMessageTimeout(hwnd, WM_GETICON, (IntPtr)ICON_SMALL, IntPtr.Zero, SMTO_ABORTIFHUNG, 60, out res) != IntPtr.Zero && res != IntPtr.Zero)
                    hIcon = res;
                if (hIcon == IntPtr.Zero)
                    hIcon = GetClassLongPtr(hwnd, GCLP_HICON);
                if (hIcon == IntPtr.Zero)
                    hIcon = GetClassLongPtr(hwnd, GCLP_HICONSM);

                if (hIcon == IntPtr.Zero)
                {
                    GetWindowThreadProcessId(hwnd, out uint pid);
                    try
                    {
                        using (Process p = Process.GetProcessById((int)pid))
                        {
                            string exePath = p.MainModule != null ? p.MainModule.FileName : null;
                            if (!string.IsNullOrEmpty(exePath))
                            {
                                var shfi = new SHFILEINFO();
                                if (SHGetFileInfo(exePath, 0, ref shfi, (uint)Marshal.SizeOf(typeof(SHFILEINFO)), SHGFI_ICON | SHGFI_LARGEICON) != IntPtr.Zero && shfi.hIcon != IntPtr.Zero)
                                {
                                    hIcon = shfi.hIcon;
                                    ownsIcon = true;
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (hIcon == IntPtr.Zero) return null;

                const int size = 32;
                IntPtr screenDc = IntPtr.Zero;
                IntPtr memDc = IntPtr.Zero;
                IntPtr hBmp = IntPtr.Zero;
                IntPtr oldBmp = IntPtr.Zero;

                try
                {
                    screenDc = GetDC(IntPtr.Zero);
                    memDc = CreateCompatibleDC(screenDc);

                    var bmi = new BITMAPINFO();
                    bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                    bmi.bmiHeader.biWidth = size;
                    bmi.bmiHeader.biHeight = size; // bottom-up matches Unity Texture2D
                    bmi.bmiHeader.biPlanes = 1;
                    bmi.bmiHeader.biBitCount = 32;
                    bmi.bmiHeader.biCompression = 0;

                    hBmp = CreateDIBSection(memDc, ref bmi, DIB_RGB_COLORS, out IntPtr bitsPtr, IntPtr.Zero, 0);
                    if (hBmp == IntPtr.Zero || bitsPtr == IntPtr.Zero) return null;

                    oldBmp = SelectObject(memDc, hBmp);
                    DrawIconEx(memDc, 0, 0, hIcon, size, size, 0, IntPtr.Zero, DI_NORMAL);

                    byte[] bytes = new byte[size * size * 4];
                    Marshal.Copy(bitsPtr, bytes, 0, bytes.Length);

                    // Ensure non-zero RGB pixels have visible alpha if icon lacked alpha channel
                    bool anyAlpha = false;
                    for (int i = 3; i < bytes.Length; i += 4)
                    {
                        if (bytes[i] != 0) { anyAlpha = true; break; }
                    }
                    if (!anyAlpha)
                    {
                        for (int i = 0; i < bytes.Length; i += 4)
                        {
                            if (bytes[i] != 0 || bytes[i + 1] != 0 || bytes[i + 2] != 0)
                                bytes[i + 3] = 255;
                        }
                    }

                    var tex = new Texture2D(size, size, TextureFormat.BGRA32, false)
                    {
                        name = "CascadeWinIcon",
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp
                    };
                    tex.LoadRawTextureData(bytes);
                    tex.Apply(false, false);
                    return tex;
                }
                finally
                {
                    if (oldBmp != IntPtr.Zero && memDc != IntPtr.Zero) SelectObject(memDc, oldBmp);
                    if (hBmp != IntPtr.Zero) DeleteObject(hBmp);
                    if (memDc != IntPtr.Zero) DeleteDC(memDc);
                    if (screenDc != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screenDc);
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                if (ownsIcon && hIcon != IntPtr.Zero) DestroyIcon(hIcon);
            }
        }

        // -----------------------------------------------------------------
        // Non-Invasive Direct Window Messages (never hijacks PC mouse/focus!)
        // -----------------------------------------------------------------
        private static IntPtr MakeLParam(int x, int y)
        {
            return (IntPtr)((y << 16) | (x & 0xFFFF));
        }

        private static bool ResolveTargetChildPoint(IntPtr rootHwnd, float u, float v, out IntPtr targetHwnd, out POINT clientPt, out POINT screenPt)
        {
            targetHwnd = rootHwnd;
            clientPt = default;
            screenPt = default;

            if (rootHwnd == IntPtr.Zero || !TryGetTargetRect(rootHwnd, out RECT rect)) return false;

            screenPt.X = rect.Left + Mathf.RoundToInt(Mathf.Clamp01(u) * Mathf.Max(1, rect.Width - 1));
            screenPt.Y = rect.Top + Mathf.RoundToInt((1f - Mathf.Clamp01(v)) * Mathf.Max(1, rect.Height - 1));

            clientPt = screenPt;
            if (!ScreenToClient(rootHwnd, ref clientPt)) return false;

            // Walk down into child render hosts (e.g. Chrome/Discord/Electron)
            IntPtr cur = rootHwnd;
            for (int depth = 0; depth < 6; depth++)
            {
                POINT testPt = screenPt;
                if (!ScreenToClient(cur, ref testPt)) break;
                IntPtr child = ChildWindowFromPointEx(cur, testPt, CWP_SKIPINVISIBLE | CWP_SKIPDISABLED);
                if (child == IntPtr.Zero || child == cur) break;
                cur = child;
            }

            targetHwnd = cur;
            clientPt = screenPt;
            ScreenToClient(targetHwnd, ref clientPt);
            return true;
        }

        internal static void PostWindowMouseMove(IntPtr hwnd, float u, float v)
        {
            if (!ResolveTargetChildPoint(hwnd, u, v, out IntPtr target, out POINT pt, out _)) return;
            PostMessage(target, WM_MOUSEMOVE, IntPtr.Zero, MakeLParam(pt.X, pt.Y));
        }

        internal static bool PostWindowMouseButton(IntPtr hwnd, float u, float v, bool rightButton, bool down, out int outX, out int outY)
        {
            outX = 0;
            outY = 0;
            if (!ResolveTargetChildPoint(hwnd, u, v, out IntPtr target, out POINT pt, out _)) return false;
            outX = pt.X;
            outY = pt.Y;
            IntPtr lp = MakeLParam(pt.X, pt.Y);
            PostMessage(target, WM_MOUSEMOVE, IntPtr.Zero, lp);
            if (rightButton)
                PostMessage(target, down ? WM_RBUTTONDOWN : WM_RBUTTONUP, down ? (IntPtr)MK_RBUTTON : IntPtr.Zero, lp);
            else
                PostMessage(target, down ? WM_LBUTTONDOWN : WM_LBUTTONUP, down ? (IntPtr)MK_LBUTTON : IntPtr.Zero, lp);
            return true;
        }

        internal static void PostWindowMouseWheel(IntPtr hwnd, float u, float v, int delta)
        {
            if (delta == 0) return;
            if (!ResolveTargetChildPoint(hwnd, u, v, out IntPtr target, out _, out POINT screenPt)) return;
            IntPtr wp = (IntPtr)((delta << 16) & unchecked((int)0xFFFF0000));
            PostMessage(target, WM_MOUSEWHEEL, wp, MakeLParam(screenPt.X, screenPt.Y));
        }

        internal static void PostWindowChar(IntPtr hwnd, char ch)
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return;
            PostMessage(hwnd, WM_CHAR, (IntPtr)ch, IntPtr.Zero);
        }

        internal static void PostWindowVirtualKey(IntPtr hwnd, ushort vk)
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return;
            PostMessage(hwnd, WM_KEYDOWN, (IntPtr)vk, IntPtr.Zero);
            PostMessage(hwnd, WM_KEYUP, (IntPtr)vk, IntPtr.Zero);
        }

        private static int _asyncClickBusy;

        /// <summary>
        /// Reliably clicks inside ANY Windows 10/11 app (Chrome, Discord, Spotify, Electron,
        /// Win32, etc.) from PC mode even when Gorilla Tag is covering it:
        /// briefly brings the target window forward on a worker thread, waits for DWM to
        /// present it, performs the click, then immediately restores Gorilla Tag + cursor.
        /// Never blocks Unity and never loops/traps your mouse.
        /// </summary>
        internal static bool PerformAsyncWindowClick(IntPtr hwnd, float u, float v, bool rightClick)
        {
            if (System.Threading.Interlocked.CompareExchange(ref _asyncClickBusy, 1, 0) != 0)
                return false;

            IntPtr prevForeground = GetForegroundWindow();
            bool hasSavedPos = GetCursorPos(out POINT savedPos);

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    if (hwnd != IntPtr.Zero && IsWindow(hwnd))
                    {
                        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
                        BringWindowToTop(hwnd);
                        SetForegroundWindow(hwnd);
                        System.Threading.Thread.Sleep(35);
                    }

                    if (TryGetTargetRect(hwnd, out RECT rect))
                    {
                        int px = rect.Left + Mathf.RoundToInt(Mathf.Clamp01(u) * Mathf.Max(1, rect.Width - 1));
                        int py = rect.Top + Mathf.RoundToInt((1f - Mathf.Clamp01(v)) * Mathf.Max(1, rect.Height - 1));
                        SetCursorPos(px, py);
                        System.Threading.Thread.Sleep(10);

                        if (rightClick)
                        {
                            SendMouseButton(true, true);
                            System.Threading.Thread.Sleep(25);
                            SendMouseButton(true, false);
                        }
                        else
                        {
                            SendMouseButton(false, true);
                            System.Threading.Thread.Sleep(25);
                            SendMouseButton(false, false);
                        }
                        System.Threading.Thread.Sleep(20);
                    }
                }
                catch { }
                finally
                {
                    try
                    {
                        if (prevForeground != IntPtr.Zero && prevForeground != hwnd && IsWindow(prevForeground))
                            SetForegroundWindow(prevForeground);
                        if (hasSavedPos)
                            SetCursorPos(savedPos.X, savedPos.Y);
                    }
                    catch { }
                    System.Threading.Interlocked.Exchange(ref _asyncClickBusy, 0);
                }
            });

            return true;
        }

        // -----------------------------------------------------------------
        // Mouse & Keyboard Injection (for VR / Full-Screen mode)
        // -----------------------------------------------------------------
        internal static void FocusWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return;
            try
            {
                if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
                SetForegroundWindow(hwnd);
            }
            catch { }
        }

        internal static bool MoveCursorToUV(IntPtr hwnd, float u, float v)
        {
            if (!TryGetTargetRect(hwnd, out RECT rect)) return false;

            int px = rect.Left + Mathf.RoundToInt(Mathf.Clamp01(u) * Mathf.Max(1, rect.Width - 1));
            int py = rect.Top + Mathf.RoundToInt((1f - Mathf.Clamp01(v)) * Mathf.Max(1, rect.Height - 1));
            return SetCursorPos(px, py);
        }

        internal static void SendMouseButton(bool rightButton, bool down)
        {
            uint flag = rightButton
                ? (down ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP)
                : (down ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP);

            var inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].U.mi.dwFlags = flag;
            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        internal static void SendRightClick()
        {
            var inputs = new INPUT[2];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].U.mi.dwFlags = MOUSEEVENTF_RIGHTDOWN;
            inputs[1].type = INPUT_MOUSE;
            inputs[1].U.mi.dwFlags = MOUSEEVENTF_RIGHTUP;
            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        internal static void SendMouseWheel(int delta)
        {
            if (delta == 0) return;
            var inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].U.mi.dwFlags = MOUSEEVENTF_WHEEL;
            inputs[0].U.mi.mouseData = unchecked((uint)delta);
            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        internal static void SendUnicodeChar(char ch)
        {
            var inputs = new INPUT[2];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki.wScan = ch;
            inputs[0].U.ki.dwFlags = KEYEVENTF_UNICODE;

            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U.ki.wScan = ch;
            inputs[1].U.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;

            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        internal static void SendVirtualKey(ushort vk, bool ctrl = false, bool shift = false)
        {
            var list = new List<INPUT>(6);
            if (ctrl) list.Add(MakeVkInput(0x11, false));
            if (shift) list.Add(MakeVkInput(0x10, false));
            list.Add(MakeVkInput(vk, false));
            list.Add(MakeVkInput(vk, true));
            if (shift) list.Add(MakeVkInput(0x10, true));
            if (ctrl) list.Add(MakeVkInput(0x11, true));

            INPUT[] arr = list.ToArray();
            SendInput((uint)arr.Length, arr, Marshal.SizeOf(typeof(INPUT)));
        }

        private static INPUT MakeVkInput(ushort vk, bool keyUp)
        {
            var inp = new INPUT();
            inp.type = INPUT_KEYBOARD;
            inp.U.ki.wVk = vk;
            inp.U.ki.dwFlags = keyUp ? KEYEVENTF_KEYUP : 0u;
            return inp;
        }

        // -----------------------------------------------------------------
        // Async Background-Thread Capture Session (zero main-thread stalls)
        // -----------------------------------------------------------------
        internal enum CaptureQualityMode
        {
            Performance720 = 0,
            Balanced1280 = 1,
            FullHd1920 = 2,
            Native = 3
        }

        internal sealed class CaptureSession : IDisposable
        {
            public Texture2D Texture { get; private set; }
            public int SourceWidth { get; private set; } = 1280;
            public int SourceHeight { get; private set; } = 720;
            public bool WindowLost { get; private set; }
            public CaptureQualityMode QualityMode { get; set; } = CaptureQualityMode.Balanced1280;

            private readonly object _frameLock = new object();
            private byte[] _workerBuf;
            private byte[] _readyPixels;
            private byte[] _uploadBuf;
            private int _readyW;
            private int _readyH;
            private bool _hasNewFrame;
            private bool _hasValidFrameEver;
            private bool _captureInFlight;
            private volatile bool _disposed;

            private int _texWidth;
            private int _texHeight;

            /// <summary>
            /// Uploads any completed background frame to Texture2D (sub-millisecond, race-free)
            /// and kicks off the next async capture if none is currently running.
            /// </summary>
            public bool PollAndKickCapture(IntPtr hwnd, bool kickNewCapture)
            {
                if (_disposed) return false;

                int w = 0, h = 0;
                bool copied = false;
                lock (_frameLock)
                {
                    if (_hasNewFrame && _readyPixels != null && _readyW > 0 && _readyH > 0)
                    {
                        int len = _readyPixels.Length;
                        if (_uploadBuf == null || _uploadBuf.Length != len)
                            _uploadBuf = new byte[len];
                        Buffer.BlockCopy(_readyPixels, 0, _uploadBuf, 0, len);
                        w = _readyW;
                        h = _readyH;
                        _hasNewFrame = false;
                        copied = true;
                    }
                }

                if (copied)
                {
                    EnsureTexture(w, h);
                    if (Texture != null)
                    {
                        Texture.LoadRawTextureData(_uploadBuf);
                        Texture.Apply(false, false);
                    }
                }

                if (kickNewCapture && !_captureInFlight)
                {
                    _captureInFlight = true;
                    CaptureQualityMode mode = QualityMode;
                    System.Threading.ThreadPool.QueueUserWorkItem(_ => CaptureWorker(hwnd, mode));
                }

                return !WindowLost;
            }

            private void CaptureWorker(IntPtr hwnd, CaptureQualityMode mode)
            {
                try
                {
                    if (_disposed) return;

                    if (!TryGetTargetRect(hwnd, out RECT rect))
                    {
                        WindowLost = true;
                        return;
                    }

                    int srcW = Math.Max(16, rect.Width);
                    int srcH = Math.Max(16, rect.Height);
                    SourceWidth = srcW;
                    SourceHeight = srcH;

                    int dstW;
                    int dstH;
                    if (mode == CaptureQualityMode.Native)
                    {
                        dstW = srcW;
                        dstH = srcH;
                    }
                    else
                    {
                        int maxW = mode == CaptureQualityMode.FullHd1920 ? 1920 : (mode == CaptureQualityMode.Balanced1280 ? 1280 : 720);
                        int maxH = mode == CaptureQualityMode.FullHd1920 ? 1080 : (mode == CaptureQualityMode.Balanced1280 ? 720 : 405);
                        float scale = Math.Min(1f, Math.Min((float)maxW / srcW, (float)maxH / srcH));
                        dstW = Math.Max(16, (int)(srcW * scale));
                        dstH = Math.Max(16, (int)(srcH * scale));
                    }
                    dstW = (dstW + 3) & ~3;

                    IntPtr screenDc = IntPtr.Zero;
                    IntPtr srcDc = IntPtr.Zero;
                    IntPtr srcBmp = IntPtr.Zero;
                    IntPtr oldSrcBmp = IntPtr.Zero;
                    IntPtr dstDc = IntPtr.Zero;
                    IntPtr dstDib = IntPtr.Zero;
                    IntPtr oldDstBmp = IntPtr.Zero;

                    try
                    {
                        screenDc = GetDC(IntPtr.Zero);
                        if (screenDc == IntPtr.Zero) return;

                        dstDc = CreateCompatibleDC(screenDc);
                        if (dstDc == IntPtr.Zero) return;

                        var bmi = new BITMAPINFO();
                        bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                        bmi.bmiHeader.biWidth = dstW;
                        bmi.bmiHeader.biHeight = dstH; // bottom-up matches Unity Texture2D
                        bmi.bmiHeader.biPlanes = 1;
                        bmi.bmiHeader.biBitCount = 32;
                        bmi.bmiHeader.biCompression = 0;

                        dstDib = CreateDIBSection(dstDc, ref bmi, DIB_RGB_COLORS, out IntPtr bitsPtr, IntPtr.Zero, 0);
                        if (dstDib == IntPtr.Zero || bitsPtr == IntPtr.Zero) return;

                        oldDstBmp = SelectObject(dstDc, dstDib);
                        SetStretchBltMode(dstDc, COLORONCOLOR);

                        bool sameSize = (dstW == srcW && dstH == srcH);
                        bool captured = false;

                        if (hwnd != IntPtr.Zero)
                        {
                            if (sameSize)
                            {
                                // Native 1:1 direct PrintWindow straight into DIB (no intermediate bitmap/stretch!)
                                if (PrintWindow(hwnd, dstDc, PW_RENDERFULLCONTENT) || PrintWindow(hwnd, dstDc, 0))
                                {
                                    captured = true;
                                }
                            }
                            else
                            {
                                srcDc = CreateCompatibleDC(screenDc);
                                srcBmp = CreateCompatibleBitmap(screenDc, srcW, srcH);
                                if (srcDc != IntPtr.Zero && srcBmp != IntPtr.Zero)
                                {
                                    oldSrcBmp = SelectObject(srcDc, srcBmp);
                                    if (PrintWindow(hwnd, srcDc, PW_RENDERFULLCONTENT) || PrintWindow(hwnd, srcDc, 0))
                                    {
                                        StretchBlt(dstDc, 0, 0, dstW, dstH, srcDc, 0, 0, srcW, srcH, SRCCOPY);
                                        captured = true;
                                    }
                                }
                            }
                        }

                        if (!captured)
                        {
                            if (sameSize)
                                BitBlt(dstDc, 0, 0, dstW, dstH, screenDc, rect.Left, rect.Top, SRCCOPY);
                            else
                                StretchBlt(dstDc, 0, 0, dstW, dstH, screenDc, rect.Left, rect.Top, srcW, srcH, SRCCOPY);
                        }

                        int byteLen = dstW * dstH * 4;
                        if (_workerBuf == null || _workerBuf.Length != byteLen)
                            _workerBuf = new byte[byteLen];

                        Marshal.Copy(bitsPtr, _workerBuf, 0, byteLen);
                        for (int i = 3; i < byteLen; i += 4)
                            _workerBuf[i] = 255;

                        // Reject transient blank all-white or all-black frames when DWM is mid-present
                        if (_hasValidFrameEver && IsBlankFrame(_workerBuf, byteLen))
                        {
                            return;
                        }

                        lock (_frameLock)
                        {
                            if (!_disposed)
                            {
                                byte[] prevReady = _readyPixels;
                                _readyPixels = _workerBuf;
                                _workerBuf = prevReady;
                                _readyW = dstW;
                                _readyH = dstH;
                                _hasNewFrame = true;
                                _hasValidFrameEver = true;
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        if (oldSrcBmp != IntPtr.Zero && srcDc != IntPtr.Zero) SelectObject(srcDc, oldSrcBmp);
                        if (srcBmp != IntPtr.Zero) DeleteObject(srcBmp);
                        if (srcDc != IntPtr.Zero) DeleteDC(srcDc);

                        if (oldDstBmp != IntPtr.Zero && dstDc != IntPtr.Zero) SelectObject(dstDc, oldDstBmp);
                        if (dstDib != IntPtr.Zero) DeleteObject(dstDib);
                        if (dstDc != IntPtr.Zero) DeleteDC(dstDc);

                        if (screenDc != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screenDc);
                    }
                }
                finally
                {
                    _captureInFlight = false;
                }
            }

            private void EnsureTexture(int width, int height)
            {
                if (Texture != null && _texWidth == width && _texHeight == height) return;
                if (Texture != null) UnityEngine.Object.Destroy(Texture);

                _texWidth = width;
                _texHeight = height;
                Texture = new Texture2D(width, height, TextureFormat.BGRA32, false)
                {
                    name = "CascadeWinOverlayCapture",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
            }

            private static bool IsBlankFrame(byte[] buf, int byteLen)
            {
                if (buf == null || byteLen < 64) return true;
                int stride = Math.Max(4, (byteLen / 16) & ~3);
                byte b0 = buf[0], g0 = buf[1], r0 = buf[2];
                bool isWhiteOrBlack = (b0 == 255 && g0 == 255 && r0 == 255) || (b0 == 0 && g0 == 0 && r0 == 0);
                if (!isWhiteOrBlack) return false;

                for (int i = stride; i + 2 < byteLen; i += stride)
                {
                    if (buf[i] != b0 || buf[i + 1] != g0 || buf[i + 2] != r0)
                        return false;
                }
                return true;
            }

            public void Dispose()
            {
                _disposed = true;
                lock (_frameLock)
                {
                    _readyPixels = null;
                    _workerBuf = null;
                    _uploadBuf = null;
                    _hasNewFrame = false;
                }
                if (Texture != null)
                {
                    UnityEngine.Object.Destroy(Texture);
                    Texture = null;
                }
                _texWidth = 0;
                _texHeight = 0;
            }
        }
    }
}

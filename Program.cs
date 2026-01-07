using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;

class Program
{
    static string ConfigFilePath => Path.Combine(AppContext.BaseDirectory, "config.json");

    // Track windows that already have overlays applied (by window handle)
    static readonly HashSet<IntPtr> _processedWindows = new();

    // Process names to ignore (system/shell windows that don't need overlays)
    static readonly string[] IgnoredProcessNames = {
        "explorer",
        "SystemSettings",
        "ApplicationFrameHost",
        "TextInputHost",
        "ShellExperienceHost",
        "SearchHost",
        "StartMenuExperienceHost",
        "LockApp"
    };

    static bool IsIgnoredProcess(string processName)
    {
        foreach (var ignored in IgnoredProcessNames)
        {
            if (processName.Equals(ignored, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    static int Main(string[] args)
    {
        bool listAllWindows = args.Length > 0 && 
            (args[0].Equals("--list-all", StringComparison.OrdinalIgnoreCase) ||
             args[0].Equals("-l", StringComparison.OrdinalIgnoreCase));

        bool watchMode = args.Length > 0 &&
            (args[0].Equals("--watch", StringComparison.OrdinalIgnoreCase) ||
             args[0].Equals("-w", StringComparison.OrdinalIgnoreCase));

        int watchIntervalSeconds = 3; // Default interval
        if (watchMode && args.Length > 1 && int.TryParse(args[1], out int customInterval) && customInterval > 0)
        {
            watchIntervalSeconds = customInterval;
        }

        if (listAllWindows)
        {
            ListAllWindows();
            return 0;
        }

        if (watchMode)
        {
            RunWatchMode(watchIntervalSeconds);
            return 0;
        }

        // Single run mode
        ApplyOverlays(verbose: true);
        return 0;
    }

    static void ListAllWindows()
    {
        Console.WriteLine("Listing all visible windows:");
        Console.WriteLine(new string('-', 80));
        
        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd)) return true;

            string title = GetTitle(hWnd);
            if (string.IsNullOrWhiteSpace(title)) return true;

            string processName = GetProcessName(hWnd);
            string className = GetClass(hWnd);
            bool ignored = IsIgnoredProcess(processName);

            Console.Write($"[{processName}] ({className})");
            if (ignored)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(" [IGNORED]");
                Console.ResetColor();
            }
            Console.WriteLine();
            Console.WriteLine($"  Title: {title}");
            Console.WriteLine();

            return true;
        }, IntPtr.Zero);
    }

    static void RunWatchMode(int intervalSeconds)
    {
        Console.WriteLine($"Watch mode started. Checking every {intervalSeconds} second(s). Press Ctrl+C to stop.");
        Console.WriteLine();

        // Handle Ctrl+C gracefully
        Console.CancelKeyPress += (sender, e) =>
        {
            Console.WriteLine();
            Console.WriteLine("Watch mode stopped.");
            e.Cancel = false;
        };

        while (true)
        {
            var appliedIcons = ApplyOverlays(verbose: false);
            if (appliedIcons.Count > 0)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Applied {appliedIcons.Count} overlay(s): {string.Join(", ", appliedIcons)}");
            }
            Thread.Sleep(intervalSeconds * 1000);
        }
    }

    static List<string> ApplyOverlays(bool verbose)
    {
        // Load configuration or use defaults
        var config = LoadConfig(verbose);
        string iconsDir = config.IconsPath;

        int matchedWindows = 0;
        var appliedIcons = new List<string>();

        var taskbar = (ITaskbarList3)new CTaskbarList();
        taskbar.HrInit();

        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd)) return true;

            // Grab window title
            string title = GetTitle(hWnd);
            if (string.IsNullOrWhiteSpace(title))
                return true;

            string processName = GetProcessName(hWnd);

            // Skip ignored system processes for efficiency
            if (IsIgnoredProcess(processName))
                return true;

            // Determine the icon name to use
            string? iconName = null;

            // Special handling for VS Code / VSCodium: extract workspace name
            if (IsCodeFamilyWindow(hWnd))
            {
                iconName = ExtractWorkspaceName(title);
            }

            // Fallback: use the process name as icon name
            if (string.IsNullOrWhiteSpace(iconName))
            {
                iconName = SanitizeFileStem(processName);
            }

            // Try to find an icon file
            string icoPath = Path.Combine(iconsDir, iconName + ".ico");

            if (!File.Exists(icoPath))
            {
                // No icon found for this window, skip it
                return true;
            }

            // Skip if we've already applied an overlay to this window
            if (_processedWindows.Contains(hWnd))
            {
                return true;
            }

            matchedWindows++;

            IntPtr hIcon = LoadImage(IntPtr.Zero, icoPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE);
            if (hIcon == IntPtr.Zero)
            {
                // Can't load the icon (bad .ico, permissions, etc.)
                if (verbose) Console.WriteLine($"[WARN] Failed to load: {icoPath}");
                return true;
            }

            taskbar.SetOverlayIcon(hWnd, hIcon, iconName);
            _processedWindows.Add(hWnd);
            appliedIcons.Add(iconName);
            if (verbose) Console.WriteLine($"[OK] {iconName}  <-  {processName}  ::  {title}");

            return true;
        }, IntPtr.Zero);

        if (verbose)
        {
            Console.WriteLine();
            Console.WriteLine($"Windows matched: {matchedWindows}");
            Console.WriteLine($"Overlays applied: {appliedIcons.Count}");
            Console.WriteLine($"Icons dir: {iconsDir}");
        }

        return appliedIcons;
    }

    static Config LoadConfig(bool verbose = true)
    {
        string defaultIconsDir = Path.Combine(Directory.GetCurrentDirectory(), "icons");

        if (!File.Exists(ConfigFilePath))
        {
            if (verbose) Console.WriteLine($"[INFO] No config file found, using default icons folder.");
            return new Config { IconsPath = defaultIconsDir };
        }

        try
        {
            string json = File.ReadAllText(ConfigFilePath);
            var config = JsonSerializer.Deserialize<Config>(json);

            if (config == null || string.IsNullOrWhiteSpace(config.IconsPath))
            {
                if (verbose) Console.WriteLine($"[WARN] Config file is empty or invalid, using default icons folder.");
                return new Config { IconsPath = defaultIconsDir };
            }

            // If the path is relative, make it relative to the exe directory
            if (!Path.IsPathRooted(config.IconsPath))
            {
                config.IconsPath = Path.Combine(AppContext.BaseDirectory, config.IconsPath);
            }

            if (verbose) Console.WriteLine($"[INFO] Loaded config from: {ConfigFilePath}");
            return config;
        }
        catch (Exception ex)
        {
            if (verbose) Console.WriteLine($"[WARN] Failed to load config: {ex.Message}. Using default icons folder.");
            return new Config { IconsPath = defaultIconsDir };
        }
    }

    static bool IsCodeFamilyWindow(IntPtr hWnd)
    {
        GetWindowThreadProcessId(hWnd, out uint pid);
        if (pid == 0) return false;

        try
        {
            var p = Process.GetProcessById((int)pid);
            string name = p.ProcessName;

            // VS Code: Code.exe
            // VSCodium: VSCodium.exe
            // (ProcessName doesn't include .exe)
            if (!name.Equals("Code", StringComparison.OrdinalIgnoreCase) &&
                !name.Equals("VSCodium", StringComparison.OrdinalIgnoreCase))
                return false;

            // Electron top-level window class is typically Chrome_WidgetWin_1
            // This helps exclude odd helper windows
            string cls = GetClass(hWnd);
            if (!cls.Equals("Chrome_WidgetWin_1", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    static string GetProcessName(IntPtr hWnd)
    {
        GetWindowThreadProcessId(hWnd, out uint pid);
        try
        {
            return Process.GetProcessById((int)pid).ProcessName;
        }
        catch
        {
            return "UnknownProcess";
        }
    }

    static string GetTitle(IntPtr hWnd)
    {
        var sb = new StringBuilder(2048);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    static string GetClass(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    static string? ExtractWorkspaceName(string title)
    {
        const string marker = "(Workspace)";

        int markerIndex = title.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
            return null;

        // We want the chunk immediately before "(Workspace)" in a pattern like:
        // "... - something (Workspace) - ..."
        // So take substring up to marker, then split by " - " and take last chunk.
        string beforeMarker = title.Substring(0, markerIndex).Trim();

        // Remove any trailing "-" or extra spaces
        beforeMarker = beforeMarker.TrimEnd('-', ' ');

        // Split on " - " and take last segment
        string[] parts = beforeMarker.Split(" - ", StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return null;

        string instance = parts[^1].Trim();

        // Optional: sanitize filename-unfriendly characters
        instance = SanitizeFileStem(instance);

        return string.IsNullOrWhiteSpace(instance) ? null : instance;
    }

    static string SanitizeFileStem(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s;
    }

    // --- Win32 ---
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    const uint IMAGE_ICON = 1;
    const uint LR_LOADFROMFILE = 0x0010;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr LoadImage(IntPtr hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    // --- Taskbar COM ---
    [ComImport]
    [Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
    [ClassInterface(ClassInterfaceType.None)]
    class CTaskbarList { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF")] // <-- correct IID
    interface ITaskbarList3
    {
        // ITaskbarList
        void HrInit();
        void AddTab(IntPtr hwnd);
        void DeleteTab(IntPtr hwnd);
        void ActivateTab(IntPtr hwnd);
        void SetActiveAlt(IntPtr hwnd);

        // ITaskbarList2
        void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);

        // ITaskbarList3
        void SetProgressValue(IntPtr hwnd, ulong ullCompleted, ulong ullTotal);
        void SetProgressState(IntPtr hwnd, TBPFLAG tbpFlags);
        void RegisterTab(IntPtr hwndTab, IntPtr hwndMDI);
        void UnregisterTab(IntPtr hwndTab);
        void SetTabOrder(IntPtr hwndTab, IntPtr hwndInsertBefore);
        void SetTabActive(IntPtr hwndTab, IntPtr hwndMDI, uint dwReserved);
        void ThumbBarAddButtons(IntPtr hwnd, uint cButtons, IntPtr pButtons);
        void ThumbBarUpdateButtons(IntPtr hwnd, uint cButtons, IntPtr pButtons);
        void ThumbBarSetImageList(IntPtr hwnd, IntPtr himl);
        void SetOverlayIcon(IntPtr hwnd, IntPtr hIcon, [MarshalAs(UnmanagedType.LPWStr)] string? pszDescription);
        void SetThumbnailTooltip(IntPtr hwnd, [MarshalAs(UnmanagedType.LPWStr)] string pszTip);
        void SetThumbnailClip(IntPtr hwnd, IntPtr prcClip);
    }

    enum TBPFLAG
    {
        TBPF_NOPROGRESS = 0,
        TBPF_INDETERMINATE = 0x1,
        TBPF_NORMAL = 0x2,
        TBPF_ERROR = 0x4,
        TBPF_PAUSED = 0x8
    }
}

class Config
{
    public string IconsPath { get; set; } = "icons";
}

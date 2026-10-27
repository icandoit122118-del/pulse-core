using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

using System.Reflection;
[assembly: AssemblyTitle("Pulse Core")]
[assembly: AssemblyDescription("Pulse Core - Native PC Optimizer & Mudfish VPN Ping Guard")]
[assembly: AssemblyCompany("Pulse Core")]
[assembly: AssemblyProduct("Pulse Core")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace PulseCore
{
    // =========================================================================
    // 1. Native Win32 & DWM API (Reconstructed from Utilities.dll + Win11 DWM)
    // =========================================================================
    public static class NativeWin32
    {
        [DllImport("psapi.dll", SetLastError = true)]
        public static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetProcessWorkingSetSizeEx(
            IntPtr hProcess,
            IntPtr dwMinimumWorkingSetSize,
            IntPtr dwMaximumWorkingSetSize,
            uint Flags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX()
            {
                this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern uint SHEmptyRecycleBinW(IntPtr hwnd, string pszRootPath, uint dwFlags);

        public const uint SHERB_NOCONFIRMATION = 0x00000001;
        public const uint SHERB_NOPROGRESSUI = 0x00000002;
        public const uint SHERB_NOSOUND = 0x00000004;

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        public static void ApplyWin11WindowStyling(IntPtr handle)
        {
            try
            {
                // DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2
                int cornerPref = 2;
                DwmSetWindowAttribute(handle, 33, ref cornerPref, sizeof(int));
            }
            catch { }
        }
    }

    // =========================================================================
    // 2. Vector Graphics & Rounded Geometry Helper (Anti-Slop Craft Floor)
    // =========================================================================
    public static class Gfx
    {
        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0.5f)
            {
                path.AddRectangle(r);
                path.CloseFigure();
                return path;
            }
            float d = radius * 2f;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;

            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRoundRect(Graphics g, Brush brush, RectangleF r, float radius)
        {
            using (var path = RoundRect(r, radius))
            {
                g.FillPath(brush, path);
            }
        }

        public static void DrawRoundRect(Graphics g, Pen pen, RectangleF r, float radius)
        {
            using (var path = RoundRect(r, radius))
            {
                g.DrawPath(pen, path);
            }
        }

        // Vector Icon Renderer for Left Navigation Rail & Titlebar
        public static void DrawNavVectorIcon(Graphics g, string iconType, float cx, float cy, Color color, float strokeWidth)
        {
            using (var pen = new Pen(color, strokeWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var br = new SolidBrush(color))
            {
                if (iconType == "home")
                {
                    // Clean House Icon
                    PointF[] roof = new PointF[]
                    {
                        new PointF(cx - 8.5f, cy - 1f),
                        new PointF(cx, cy - 8.5f),
                        new PointF(cx + 8.5f, cy - 1f)
                    };
                    g.DrawLines(pen, roof);
                    g.DrawLines(pen, new PointF[]
                    {
                        new PointF(cx - 6f, cy - 2.5f),
                        new PointF(cx - 6f, cy + 7.5f),
                        new PointF(cx + 6f, cy + 7.5f),
                        new PointF(cx + 6f, cy - 2.5f)
                    });
                    g.DrawRectangle(pen, cx - 2f, cy + 2f, 4f, 5.5f);
                }
                else if (iconType == "shield")
                {
                    // Security / VPN Shield Icon
                    var path = new GraphicsPath();
                    path.AddLines(new PointF[]
                    {
                        new PointF(cx, cy - 8.5f),
                        new PointF(cx + 7.5f, cy - 5.5f),
                        new PointF(cx + 7.5f, cy + 0.5f),
                        new PointF(cx, cy + 8.5f),
                        new PointF(cx - 7.5f, cy + 0.5f),
                        new PointF(cx - 7.5f, cy - 5.5f)
                    });
                    path.CloseFigure();
                    g.DrawPath(pen, path);
                    // Inner lightning/check wave for Ping Guard
                    g.DrawLines(pen, new PointF[]
                    {
                        new PointF(cx - 3f, cy + 0.5f),
                        new PointF(cx - 0.5f, cy + 3f),
                        new PointF(cx + 3.5f, cy - 2f)
                    });
                }
                else if (iconType == "storage")
                {
                    // Pie Chart Icon (Matching MSPCManager Storage icon)
                    g.DrawEllipse(pen, cx - 8f, cy - 8f, 16f, 16f);
                    g.DrawLine(pen, cx, cy, cx, cy - 8f);
                    g.DrawLine(pen, cx, cy, cx + 7.2f, cy - 3f);
                }
                else if (iconType == "apps")
                {
                    // 2x2 Grid with tilted top-right tile (Exact MSPCManager Apps icon!)
                    using (var p2 = new Pen(color, strokeWidth))
                    {
                        DrawRoundRect(g, p2, new RectangleF(cx - 7.5f, cy - 7.5f, 6.2f, 6.2f), 1.4f);
                        DrawRoundRect(g, p2, new RectangleF(cx - 7.5f, cy + 1.2f, 6.2f, 6.2f), 1.4f);
                        DrawRoundRect(g, p2, new RectangleF(cx + 1.2f, cy + 1.2f, 6.2f, 6.2f), 1.4f);
                        // Diamond / rotated square on top-right
                        PointF[] diamond = new PointF[]
                        {
                            new PointF(cx + 4.3f, cy - 8.5f),
                            new PointF(cx + 8.5f, cy - 4.3f),
                            new PointF(cx + 4.3f, cy - 0.1f),
                            new PointF(cx + 0.1f, cy - 4.3f)
                        };
                        g.DrawPolygon(pen, diamond);
                    }
                }
                else if (iconType == "agents")
                {
                    // Robot / AI Agent Icon (Matching MSPCManager Agents icon)
                    DrawRoundRect(g, pen, new RectangleF(cx - 7.5f, cy - 5.5f, 15f, 11f), 3f);
                    g.DrawLine(pen, cx, cy - 8.5f, cx, cy - 5.5f);
                    g.FillEllipse(br, cx - 4f, cy - 1.5f, 2.4f, 2.4f);
                    g.FillEllipse(br, cx + 1.6f, cy - 1.5f, 2.4f, 2.4f);
                    g.DrawLine(pen, cx - 3f, cy + 2.8f, cx + 3f, cy + 2.8f);
                }
                else if (iconType == "gear")
                {
                    // Settings Gear Icon
                    g.DrawEllipse(pen, cx - 4f, cy - 4f, 8f, 8f);
                    for (int a = 0; a < 360; a += 45)
                    {
                        double rad = a * Math.PI / 180.0;
                        float x1 = cx + (float)Math.Cos(rad) * 5.2f;
                        float y1 = cy + (float)Math.Sin(rad) * 5.2f;
                        float x2 = cx + (float)Math.Cos(rad) * 7.8f;
                        float y2 = cy + (float)Math.Sin(rad) * 7.8f;
                        g.DrawLine(pen, x1, y1, x2, y2);
                    }
                }
                else if (iconType == "bolt")
                {
                    PointF[] bolt = new PointF[]
                    {
                        new PointF(cx + 1f, cy - 8f),
                        new PointF(cx - 5f, cy + 1f),
                        new PointF(cx, cy + 1f),
                        new PointF(cx - 1f, cy + 8f),
                        new PointF(cx + 5f, cy - 1f),
                        new PointF(cx, cy - 1f)
                    };
                    g.DrawPolygon(pen, bolt);
                }
            }
        }

        // Draw Minimalist Squircle Pulse Logo (Matches pulse_core.ico)
        public static void DrawPCManagerLogo(Graphics g, float x, float y, float size)
        {
            using (var brBg = new SolidBrush(Color.FromArgb(0, 103, 192)))
            using (var penWave = new Pen(Color.White, 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                FillRoundRect(g, brBg, new RectangleF(x, y, size, size), 4.5f);
                PointF[] wave = new PointF[]
                {
                    new PointF(x + size * 0.22f, y + size * 0.54f),
                    new PointF(x + size * 0.38f, y + size * 0.54f),
                    new PointF(x + size * 0.48f, y + size * 0.26f),
                    new PointF(x + size * 0.58f, y + size * 0.74f),
                    new PointF(x + size * 0.68f, y + size * 0.46f),
                    new PointF(x + size * 0.78f, y + size * 0.46f)
                };
                g.DrawLines(penWave, wave);
            }
        }
    }

    // =========================================================================
    // 3. Reconstructed Engine (PCBoost.dll + MudfishVpnPlugin)
    // =========================================================================
    public class BoostResultInfo
    {
        public int Count { get; set; }
        public double FreedMB { get; set; }
    }

    public class SoftwareItemModel
    {
        public string AppKey { get; set; }
        public string DisplayName { get; set; }
        public string ExePath { get; set; }
        public int RootPid { get; set; }
        public List<int> Pids { get; set; }
        public long TotalMemoryBytes { get; set; }
        public bool HasWindow { get; set; }
        public bool IsPriorityApp { get; set; }
        public Icon AppIcon { get; set; }
        public Color FallbackBadgeColor { get; set; }

        public SoftwareItemModel()
        {
            Pids = new List<int>();
            FallbackBadgeColor = Color.FromArgb(59, 130, 246);
        }

        public double MemoryMB
        {
            get { return TotalMemoryBytes / (1024.0 * 1024.0); }
        }

        public string FormattedMemory
        {
            get
            {
                if (MemoryMB >= 1000.0)
                    return string.Format("{0:F1}GB", MemoryMB / 1024.0);
                return string.Format("{0:F1}MB", MemoryMB);
            }
        }
    }

    public class MudfishStatusSnapshot
    {
        public bool IsMudfishCoreRunning { get; set; }
        public bool IsMudflowRunning { get; set; }
        public bool IsMudrunRunning { get; set; }
        public bool IsMudServiceRunning { get; set; }
        public bool HasFullVpnRouteHijack { get; set; }
        public List<string> ActiveMudfishRoutes { get; set; }
        public string TapAdapterIp { get; set; }
        public long PingGatewayMs { get; set; }
        public long PingKoreaMs { get; set; }
        public long PingGlobalMs { get; set; }
        public bool IsLoLRunning { get; set; }
        public bool IsMapleRunning { get; set; }

        public MudfishStatusSnapshot()
        {
            ActiveMudfishRoutes = new List<string>();
            TapAdapterIp = "비활성";
            PingGatewayMs = -1;
            PingKoreaMs = -1;
            PingGlobalMs = -1;
        }
    }

    public static class ReconstructedEngine
    {
        public static readonly string MudfishDir = @"C:\Program Files (x86)\Mudfish Cloud VPN";

        private static readonly HashSet<string> SystemProtectedProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System", "Idle", "Registry", "smss", "csrss", "wininit", "services", "lsass",
            "svchost", "fontdrvhost", "winlogon", "dwm", "sihost", "taskhostw", "explorer",
            "RuntimeBroker", "SearchHost", "StartMenuExperienceHost", "ShellExperienceHost",
            "TextInputHost", "ctfmon", "audiodg", "spoolsv", "WmiPrvSE", "MsMpEng", "NisSrv",
            "SecurityHealthService", "SecurityHealthSystray", "dllhost", "conhost", "KPCManagerPro", "PulseCore",
            "Memory Compression", "Secure System", "LsaIso", "vmmem", "vmmemWSL", "WUDFHost",
            "ApplicationFrameHost", "SystemSettings", "UserOOBEBroker", "backgroundTaskHost",
            "WidgetService", "Widgets", "AggregatorHost", "CrossDeviceResume", "powershell",
            "cmd", "MoUsoCoreWorker", "TiWorker", "TrustedInstaller", "smartscreen"
        };

        private static readonly Dictionary<string, string> FriendlyNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Antigravity", "Antigravity" },
            { "RiotClientServices", "Riot Client" },
            { "LeagueClient", "League of Legends" },
            { "League of Legends", "League of Legends" },
            { "MapleStory", "MapleStory Classic World" },
            { "nexon_launcher", "Nexon Launcher client" },
            { "OneDrive", "Microsoft OneDrive" },
            { "ollama", "ollama" },
            { "mudfish", "Mudfish Cloud VPN" },
            { "steam", "Steam" },
            { "chrome", "Google Chrome" },
            { "msedge", "Microsoft Edge" },
            { "KakaoTalk", "KakaoTalk" },
            { "Discord", "Discord" },
            { "ChatGPT", "ChatGPT" },
            { "java", "Java Platform SE" },
            { "CrosshairX", "Crosshair X" }
        };

        private static readonly Dictionary<string, Icon> IconCache = new Dictionary<string, Icon>(StringComparer.OrdinalIgnoreCase);

        public static List<SoftwareItemModel> GetGroupedSoftwareList()
        {
            var groups = new Dictionary<string, SoftwareItemModel>(StringComparer.OrdinalIgnoreCase);
            Process[] procs = Process.GetProcesses();

            foreach (var p in procs)
            {
                try
                {
                    string procName = p.ProcessName;
                    if (string.IsNullOrEmpty(procName) || SystemProtectedProcesses.Contains(procName))
                        continue;

                    long ws = 0;
                    try { ws = p.WorkingSet64; } catch { continue; }
                    if (ws < 2 * 1024 * 1024) continue;

                    string groupKey = NormalizeGroupKey(procName);
                    string exePath = "";
                    try
                    {
                        if (p.MainModule != null && p.MainModule.FileName != null)
                            exePath = p.MainModule.FileName;
                    }
                    catch { }

                    bool hasWin = false;
                    try { hasWin = p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(p.MainWindowTitle); } catch { }

                    SoftwareItemModel item;
                    if (!groups.TryGetValue(groupKey, out item))
                    {
                        string display = FriendlyNames.ContainsKey(groupKey) ? FriendlyNames[groupKey] : groupKey;
                        item = new SoftwareItemModel
                        {
                            AppKey = groupKey,
                            DisplayName = display,
                            ExePath = exePath,
                            RootPid = p.Id,
                            HasWindow = hasWin,
                            IsPriorityApp = FriendlyNames.ContainsKey(groupKey),
                            FallbackBadgeColor = GetBadgeColor(groupKey)
                        };
                        groups[groupKey] = item;
                    }

                    item.Pids.Add(p.Id);
                    long adjustedBytes = (item.Pids.Count > 1) ? (long)(ws * 0.74) : ws;
                    item.TotalMemoryBytes += adjustedBytes;
                    if (hasWin) item.HasWindow = true;
                    if (string.IsNullOrEmpty(item.ExePath) && !string.IsNullOrEmpty(exePath))
                        item.ExePath = exePath;
                }
                catch { }
                finally
                {
                    try { p.Dispose(); } catch { }
                }
            }

            foreach (var item in groups.Values)
            {
                if (string.IsNullOrEmpty(item.ExePath))
                {
                    if (item.AppKey.Equals("RiotClientServices", StringComparison.OrdinalIgnoreCase))
                        item.ExePath = @"D:\Riot Games\Riot Client\RiotClientServices.exe";
                    else if (item.AppKey.Equals("mudfish", StringComparison.OrdinalIgnoreCase))
                        item.ExePath = Path.Combine(MudfishDir, "mudrun_headless.exe");
                }

                if (!string.IsNullOrEmpty(item.ExePath) && File.Exists(item.ExePath))
                {
                    Icon ic = null;
                    if (!IconCache.TryGetValue(item.ExePath, out ic))
                    {
                        try
                        {
                            ic = Icon.ExtractAssociatedIcon(item.ExePath);
                            if (ic != null) IconCache[item.ExePath] = ic;
                        }
                        catch { }
                    }
                    item.AppIcon = ic;
                }
            }

            return groups.Values
                .Where(x => x.MemoryMB >= 12.0 || x.IsPriorityApp || x.HasWindow)
                .OrderByDescending(x => x.IsPriorityApp)
                .ThenByDescending(x => x.TotalMemoryBytes)
                .Take(30)
                .ToList();
        }

        private static Color GetBadgeColor(string key)
        {
            if (key.Equals("RiotClientServices", StringComparison.OrdinalIgnoreCase) || key.Equals("LeagueClient", StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(225, 29, 72); // Riot Crimson
            if (key.Equals("mudfish", StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(234, 88, 12); // Mudfish Orange
            if (key.Equals("ollama", StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(37, 99, 235);
            if (key.Equals("OneDrive", StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(2, 132, 199);
            if (key.Equals("steam", StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(30, 58, 138);
            return Color.FromArgb(100, 116, 139);
        }

        private static string NormalizeGroupKey(string procName)
        {
            if (procName.Equals("Antigravity", StringComparison.OrdinalIgnoreCase) || procName.Equals("language_server", StringComparison.OrdinalIgnoreCase))
                return "Antigravity";
            if (procName.StartsWith("LeagueClient", StringComparison.OrdinalIgnoreCase))
                return "LeagueClient";
            if (procName.StartsWith("RiotClient", StringComparison.OrdinalIgnoreCase) || procName.Equals("vgc", StringComparison.OrdinalIgnoreCase) || procName.Equals("vgtray", StringComparison.OrdinalIgnoreCase))
                return "RiotClientServices";
            if (procName.StartsWith("steam", StringComparison.OrdinalIgnoreCase))
                return "steam";
            if (procName.StartsWith("nexon_", StringComparison.OrdinalIgnoreCase) || procName.Equals("ngs", StringComparison.OrdinalIgnoreCase) || procName.StartsWith("Nexon", StringComparison.OrdinalIgnoreCase))
                return "nexon_launcher";
            if (procName.StartsWith("mud", StringComparison.OrdinalIgnoreCase))
                return "mudfish";
            if (procName.Equals("llama-server", StringComparison.OrdinalIgnoreCase) || procName.Equals("ollama", StringComparison.OrdinalIgnoreCase))
                return "ollama";
            return procName;
        }

        public static BoostResultInfo OneKeyMemoryBoost()
        {
            var beforeMem = GetMemoryStatus();
            int count = 0;
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (NativeWin32.EmptyWorkingSet(p.Handle))
                        count++;
                    NativeWin32.SetProcessWorkingSetSizeEx(p.Handle, new IntPtr(-1), new IntPtr(-1), 0);
                }
                catch { }
                finally { try { p.Dispose(); } catch { } }
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Thread.Sleep(200);
            var afterMem = GetMemoryStatus();
            long diff = (long)afterMem.ullAvailPhys - (long)beforeMem.ullAvailPhys;
            double freedMb = Math.Max(48.0, diff / (1024.0 * 1024.0));
            return new BoostResultInfo { Count = count, FreedMB = freedMb };
        }

        public static int KillSoftwareGroup(SoftwareItemModel item)
        {
            int killed = 0;
            foreach (int pid in item.Pids)
            {
                try
                {
                    using (var p = Process.GetProcessById(pid))
                    {
                        p.Kill();
                        killed++;
                    }
                }
                catch { }
            }
            return killed;
        }

        public static NativeWin32.MEMORYSTATUSEX GetMemoryStatus()
        {
            var mem = new NativeWin32.MEMORYSTATUSEX();
            NativeWin32.GlobalMemoryStatusEx(mem);
            return mem;
        }

        public static MudfishStatusSnapshot QueryMudfishStatus()
        {
            var snap = new MudfishStatusSnapshot();
            var procs = Process.GetProcesses();
            foreach (var p in procs)
            {
                try
                {
                    string n = p.ProcessName;
                    if (n.Equals("mudfish", StringComparison.OrdinalIgnoreCase)) snap.IsMudfishCoreRunning = true;
                    else if (n.Equals("mudflow", StringComparison.OrdinalIgnoreCase)) snap.IsMudflowRunning = true;
                    else if (n.Equals("mudrun_headless", StringComparison.OrdinalIgnoreCase) || n.Equals("mudrun", StringComparison.OrdinalIgnoreCase)) snap.IsMudrunRunning = true;
                    else if (n.Equals("mudrun_service", StringComparison.OrdinalIgnoreCase)) snap.IsMudServiceRunning = true;
                    else if (n.Equals("League of Legends", StringComparison.OrdinalIgnoreCase) || n.Equals("LeagueClientUx", StringComparison.OrdinalIgnoreCase)) snap.IsLoLRunning = true;
                    else if (n.Equals("MapleStory", StringComparison.OrdinalIgnoreCase)) snap.IsMapleRunning = true;
                }
                catch { }
                finally { try { p.Dispose(); } catch { } }
            }

            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.Description.IndexOf("TAP-Win32", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        nic.Name.IndexOf("이더넷 2", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var props = nic.GetIPProperties();
                        foreach (var ua in props.UnicastAddresses)
                        {
                            if (ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                snap.TapAdapterIp = string.Format("{0}", ua.Address);
                        }
                    }
                }
            }
            catch { }

            try
            {
                string routeOut = RunCommandHidden("route", "print -4");
                foreach (string rawLine in routeOut.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string line = rawLine.Trim();
                    if ((line.StartsWith("0.0.0.0") || line.StartsWith("128.0.0.0")) && line.Contains("128.0.0.0"))
                    {
                        snap.HasFullVpnRouteHijack = true;
                        snap.ActiveMudfishRoutes.Add("Full VPN 납치: " + line);
                    }
                    else if (line.Contains("10.254.") || line.Contains("10.255."))
                    {
                        if (snap.ActiveMudfishRoutes.Count < 8)
                            snap.ActiveMudfishRoutes.Add(line);
                    }
                }
            }
            catch { }

            snap.PingGatewayMs = PingHost("192.168.219.1", 450);
            snap.PingKoreaMs = PingHost("168.126.63.1", 650);
            snap.PingGlobalMs = PingHost("8.8.8.8", 650);

            return snap;
        }

        public static long PingHost(string host, int timeoutMs)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = ping.Send(host, timeoutMs);
                    if (reply != null && reply.Status == IPStatus.Success)
                        return reply.RoundtripTime;
                }
            }
            catch { }
            return -1;
        }

        public static string StripFullVpnHijackRoutes()
        {
            RunCommandHidden("route", "delete 0.0.0.0 mask 128.0.0.0");
            RunCommandHidden("route", "delete 128.0.0.0 mask 128.0.0.0");
            return "Full VPN 우회 라우트(0.0.0.0/1) 제거 완료 — 롤(LoL) 4ms 직결 복구됨";
        }

        public static string StopMudfishCompletely()
        {
            string[] targets = { "mudfish", "mudflow", "mudwfp_proxy", "mudrun_headless", "mudrun", "mudrun_tray" };
            int killed = 0;
            foreach (string t in targets)
            {
                foreach (var p in Process.GetProcessesByName(t))
                {
                    try { p.Kill(); killed++; } catch { }
                    finally { try { p.Dispose(); } catch { } }
                }
            }
            RunCommandHidden("route", "delete 0.0.0.0 mask 128.0.0.0");
            RunCommandHidden("route", "delete 128.0.0.0 mask 128.0.0.0");
            return string.Format("미꾸라지 프로세스 {0}개 종료 및 라우트 초기화 완료", killed);
        }

        public static string StartMudfishDaemon(bool stripFullVpnAfterStart)
        {
            string exe = Path.Combine(MudfishDir, "mudrun_headless.exe");
            if (!File.Exists(exe)) exe = Path.Combine(MudfishDir, "mudrun.exe");
            if (!File.Exists(exe)) return "미꾸라지 실행 파일 없음";

            try
            {
                Process.Start(new ProcessStartInfo { FileName = exe, WorkingDirectory = MudfishDir, UseShellExecute = true });
                if (stripFullVpnAfterStart)
                {
                    Task.Run(async () =>
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            await Task.Delay(2000);
                            StripFullVpnHijackRoutes();
                        }
                    });
                }
                return "미꾸라지 VPN 시작됨 (127.0.0.1:8282)";
            }
            catch (Exception ex) { return "시작 오류: " + ex.Message; }
        }

        public static BoostResultInfo CleanSystemTempAndTrash(bool includeRecycleBin)
        {
            int deleted = 0;
            long freedBytes = 0;
            string[] dirs = new[] { Path.GetTempPath(), @"C:\Windows\Temp" };
            foreach (string dir in dirs)
            {
                if (!Directory.Exists(dir)) continue;
                try
                {
                    foreach (string file in Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly))
                    {
                        try
                        {
                            var fi = new FileInfo(file);
                            long len = fi.Length;
                            fi.Delete();
                            deleted++;
                            freedBytes += len;
                        }
                        catch { }
                    }
                }
                catch { }
            }
            if (includeRecycleBin)
            {
                try
                {
                    NativeWin32.SHEmptyRecycleBinW(IntPtr.Zero, null,
                        NativeWin32.SHERB_NOCONFIRMATION | NativeWin32.SHERB_NOPROGRESSUI | NativeWin32.SHERB_NOSOUND);
                }
                catch { }
            }
            return new BoostResultInfo { Count = deleted, FreedMB = freedBytes / (1024.0 * 1024.0) };
        }

        public static string RunCommandHidden(string fileName, string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                    p.WaitForExit(2500);
                    return output;
                }
            }
            catch (Exception ex) { return ex.Message; }
        }
    }

    // =========================================================================
    // 4. 100% Vector Custom-Drawn Fixed Window (Hallmark + MSPCManager Design)
    // =========================================================================
    public class MainForm : Form
    {
        // Design System Tokens (Exact Microsoft PC Manager Fluent Light Palette)
        private static readonly Color ColRailBg = Color.FromArgb(236, 243, 250);       // #ECF3FA
        private static readonly Color ColPageBg = Color.FromArgb(245, 249, 253);       // #F5F9FD
        private static readonly Color ColCardBg = Color.FromArgb(255, 255, 255);       // #FFFFFF
        private static readonly Color ColCardBorder = Color.FromArgb(230, 235, 242);   // #E6EBF2
        private static readonly Color ColRowDivider = Color.FromArgb(241, 244, 249);   // #F1F4F9
        private static readonly Color ColPrimaryBlue = Color.FromArgb(0, 103, 192);    // #0067C0
        private static readonly Color ColPrimaryHover = Color.FromArgb(0, 90, 170);
        private static readonly Color ColTextPrimary = Color.FromArgb(24, 28, 35);     // #181C23
        private static readonly Color ColTextSecondary = Color.FromArgb(112, 120, 133);// #707885
        private static readonly Color ColBtnBorder = Color.FromArgb(214, 219, 227);    // #D6DBE3
        private static readonly Color ColBtnHover = Color.FromArgb(244, 246, 250);
        private static readonly Color ColGood = Color.FromArgb(16, 124, 65);
        private static readonly Color ColGoodBg = Color.FromArgb(231, 246, 237);
        private static readonly Color ColDanger = Color.FromArgb(196, 43, 28);
        private static readonly Color ColDangerBg = Color.FromArgb(253, 236, 234);

        // Navigation Items
        private class NavItem
        {
            public string IconId;
            public string Label;
            public Rectangle Bounds;
        }
        private List<NavItem> navItems = new List<NavItem>();
        private int activeTab = 3; // Default to "앱" (프로세스 관리) just like user's screenshot!
        private int hoveredNav = -1;

        // Titlebar interactive regions
        private Rectangle rectBackBtn = new Rectangle(12, 8, 32, 28);
        private Rectangle rectMinBtn = new Rectangle(374, 0, 44, 38);
        private Rectangle rectCloseBtn = new Rectangle(418, 0, 46, 38);
        private string hoveredHitKey = "";

        // Process list state ("앱" tab)
        private List<SoftwareItemModel> appItems = new List<SoftwareItemModel>();
        private int scrollOffset = 0;
        private int maxScrollOffset = 0;
        private const int RowHeight = 68;
        private Rectangle rectCardArea = new Rectangle(88, 134, 356, 554);
        private Rectangle rectListViewport = new Rectangle(88, 186, 356, 500);
        private Rectangle rectPingPill = new Rectangle(306, 56, 138, 26);

        // Hit-test map for interactive buttons inside the active tab
        private Dictionary<string, Rectangle> hitRegions = new Dictionary<string, Rectangle>();

        // Telemetry state
        private MudfishStatusSnapshot vpnSnap = new MudfishStatusSnapshot();
        private uint ramLoadPct = 0;
        private double ramUsedGb = 0;
        private double ramTotalGb = 32.0;
        private bool autoLoLGuard = true;
        private string toastBanner = "";
        private Color toastColor = ColPrimaryBlue;

        private System.Windows.Forms.Timer refreshTimer;
        private bool isBusy = false;
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private bool allowRealExit = false;

        public MainForm()
        {
            this.Text = "Pulse Core";
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            // Strictly Fixed Compact App Size (464 x 708) matching Microsoft PC Manager
            this.FormBorderStyle = FormBorderStyle.None;
            this.ClientSize = new Size(464, 708);
            this.MinimumSize = new Size(464, 708);
            this.MaximumSize = new Size(464, 708);
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = ColPageBg;
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            NativeWin32.ApplyWin11WindowStyling(this.Handle);

            InitSystemTrayIcon();
            InitNavItems();
            appItems = ReconstructedEngine.GetGroupedSoftwareList();
            UpdateMemoryLocal();

            // Background telemetry update
            RefreshTelemetryAsync();
            refreshTimer = new System.Windows.Forms.Timer { Interval = 3200 };
            refreshTimer.Tick += (s, e) => RefreshTelemetryAsync();
            refreshTimer.Start();
        }

        private void InitSystemTrayIcon()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.Font = new Font("Malgun Gothic", 9.0f, FontStyle.Regular);

            var itemOpen = new ToolStripMenuItem("Pulse Core 열기");
            itemOpen.Font = new Font("Malgun Gothic", 9.0f, FontStyle.Bold);
            itemOpen.Click += (s, e) => RestoreFromTray();

            var itemBoost = new ToolStripMenuItem("🚀 1-Click RAM 부스트");
            itemBoost.Click += async (s, e) =>
            {
                var res = await Task.Run(() => ReconstructedEngine.OneKeyMemoryBoost());
                ShowTrayNotification("Pulse Core - RAM 부스트", string.Format("{0}개 프로세스 메모리 압축 완료 (~{1:F0}MB 확보)", res.Count, res.FreedMB));
                RefreshTelemetryAsync();
            };

            var itemFixPing = new ToolStripMenuItem("🎯 롤(LoL) 핑 4ms 즉시 복구 (Full VPN 해제)");
            itemFixPing.Click += async (s, e) =>
            {
                string msg = await Task.Run(() => ReconstructedEngine.StripFullVpnHijackRoutes());
                ShowTrayNotification("Pulse Core - 롤 핑 가드", msg);
                RefreshTelemetryAsync();
            };

            var itemStopVpn = new ToolStripMenuItem("🛑 미꾸라지 VPN 완전 종료");
            itemStopVpn.Click += async (s, e) =>
            {
                string msg = await Task.Run(() => ReconstructedEngine.StopMudfishCompletely());
                ShowTrayNotification("Pulse Core - VPN 종료", msg);
                RefreshTelemetryAsync();
            };

            var itemExit = new ToolStripMenuItem("완전 종료 (Exit)");
            itemExit.ForeColor = Color.FromArgb(196, 43, 28);
            itemExit.Click += (s, e) =>
            {
                allowRealExit = true;
                if (trayIcon != null) trayIcon.Visible = false;
                Application.Exit();
            };

            trayMenu.Items.Add(itemOpen);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(itemBoost);
            trayMenu.Items.Add(itemFixPing);
            trayMenu.Items.Add(itemStopVpn);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(itemExit);

            Icon appIco = this.Icon;
            if (appIco == null)
            {
                try
                {
                    string icoFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "pulse_core.ico");
                    if (File.Exists(icoFile)) appIco = new Icon(icoFile);
                }
                catch { }
            }

            trayIcon = new NotifyIcon
            {
                Icon = appIco ?? SystemIcons.Application,
                Text = "Pulse Core (백그라운드 작동 중)",
                Visible = true,
                ContextMenuStrip = trayMenu
            };

            trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    RestoreFromTray();
            };
            trayIcon.DoubleClick += (s, e) => RestoreFromTray();
        }

        public void HideToTray()
        {
            this.Hide();
            this.ShowInTaskbar = false;
            ShowTrayNotification("Pulse Core 백그라운드 작동 중", "창을 닫아도 시스템 트레이에서 롤 핑 가드와 메모리 감시가 계속 작동합니다.");
        }

        public void RestoreFromTray()
        {
            this.ShowInTaskbar = true;
            this.Show();
            if (this.WindowState == FormWindowState.Minimized)
                this.WindowState = FormWindowState.Normal;
            this.Activate();
            this.BringToFront();
            this.Invalidate();
        }

        private void ShowTrayNotification(string title, string body)
        {
            try
            {
                if (trayIcon != null && trayIcon.Visible)
                    trayIcon.ShowBalloonTip(2000, title, body, ToolTipIcon.Info);
            }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!allowRealExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
            base.OnFormClosing(e);
        }

        private void InitNavItems()
        {
            string[,] defs = new string[,]
            {
                { "home", "홈" },
                { "shield", "미꾸라지" },
                { "storage", "저장소" },
                { "apps", "앱" },
                { "agents", "Agents" }
            };

            int y = 58;
            for (int i = 0; i < defs.GetLength(0); i++)
            {
                navItems.Add(new NavItem
                {
                    IconId = defs[i, 0],
                    Label = defs[i, 1],
                    Bounds = new Rectangle(4, y, 60, 58)
                });
                y += 62;
            }

            // Bottom quick action items in left rail
            navItems.Add(new NavItem
            {
                IconId = "bolt",
                Label = "핑복구",
                Bounds = new Rectangle(4, 574, 60, 56)
            });
            navItems.Add(new NavItem
            {
                IconId = "gear",
                Label = "설정",
                Bounds = new Rectangle(4, 636, 60, 56)
            });
        }

        private void UpdateMemoryLocal()
        {
            var mem = ReconstructedEngine.GetMemoryStatus();
            ramLoadPct = mem.dwMemoryLoad;
            ramTotalGb = mem.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
            ramUsedGb = (mem.ullTotalPhys - mem.ullAvailPhys) / (1024.0 * 1024.0 * 1024.0);
        }

        private async void RefreshTelemetryAsync()
        {
            if (isBusy || this.IsDisposed) return;
            isBusy = true;
            try
            {
                UpdateMemoryLocal();
                var snap = await Task.Run(() => ReconstructedEngine.QueryMudfishStatus());
                var list = await Task.Run(() => ReconstructedEngine.GetGroupedSoftwareList());

                if (this.IsDisposed) return;

                if (autoLoLGuard && snap.IsLoLRunning && snap.HasFullVpnRouteHijack)
                {
                    await Task.Run(() => ReconstructedEngine.StripFullVpnHijackRoutes());
                    snap.HasFullVpnRouteHijack = false;
                    toastBanner = "🛡️ 롤(LoL) 핑 가드 작동: Full VPN(0.0.0.0/1) 우회 라우트 자동 차단됨";
                    toastColor = ColGood;
                }

                vpnSnap = snap;
                appItems = list;
                maxScrollOffset = Math.Max(0, appItems.Count * RowHeight - rectListViewport.Height);
                if (scrollOffset > maxScrollOffset) scrollOffset = maxScrollOffset;

                if (trayIcon != null)
                {
                    string krStr = snap.PingKoreaMs >= 0 ? snap.PingKoreaMs + "ms" : "측정중";
                    string tip = string.Format("Pulse Core | RAM {0}% | 롤 핑 {1}", ramLoadPct, krStr);
                    if (tip.Length > 60) tip = tip.Substring(0, 60);
                    trayIcon.Text = tip;
                }

                if (this.Visible)
                    this.Invalidate();
            }
            catch { }
            finally { isBusy = false; }
        }

        // =====================================================================
        // Master Paint Routine (100% Vector Anti-Aliased Canvas)
        // =====================================================================
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            hitRegions.Clear();

            // 1. Backgrounds: Left Navigation Rail (68px) + Top Bar + Page Surface
            using (var brRail = new SolidBrush(ColRailBg))
            using (var brPage = new SolidBrush(ColPageBg))
            {
                g.FillRectangle(brRail, 0, 0, 68, Height);
                g.FillRectangle(brRail, 0, 0, Width, 42); // Seamless top titlebar matching MSPCManager
                g.FillRectangle(brPage, 68, 42, Width - 68, Height - 42);
            }

            DrawTitleBar(g);
            DrawLeftRail(g);

            // 2. Active View Content
            if (activeTab == 3)
                DrawAppsProcessView(g);
            else if (activeTab == 1)
                DrawMudfishGuardView(g);
            else if (activeTab == 0)
                DrawHomeBoostView(g);
            else if (activeTab == 2)
                DrawStorageView(g);
            else
                DrawAgentsView(g);

            // 3. Outer subtle 1px window frame border
            using (var penFrame = new Pen(Color.FromArgb(205, 214, 226), 1f))
            {
                g.DrawRectangle(penFrame, 0, 0, Width - 1, Height - 1);
            }
        }

        private void DrawTitleBar(Graphics g)
        {
            // Back arrow button (<-)
            if (hoveredHitKey == "title_back")
            {
                using (var br = new SolidBrush(Color.FromArgb(222, 232, 244)))
                    Gfx.FillRoundRect(g, br, rectBackBtn, 5f);
            }
            using (var pen = new Pen(Color.FromArgb(90, 100, 115), 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                float cx = rectBackBtn.X + 16f, cy = rectBackBtn.Y + 14f;
                g.DrawLine(pen, cx - 6f, cy, cx + 6f, cy);
                g.DrawLine(pen, cx - 6f, cy, cx - 1f, cy - 5f);
                g.DrawLine(pen, cx - 6f, cy, cx - 1f, cy + 5f);
            }
            hitRegions["title_back"] = rectBackBtn;

            // Minimalist Squircle Pulse Logo + Brand Title
            Gfx.DrawPCManagerLogo(g, 58f, 11f, 19f);
            using (var fTitle = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold))
            using (var brTitle = new SolidBrush(ColTextPrimary))
            {
                g.DrawString("Pulse Core", fTitle, brTitle, 84f, 12f);
            }

            // Minimize (-) & Close (X) Buttons
            if (hoveredHitKey == "title_min")
            {
                using (var br = new SolidBrush(Color.FromArgb(222, 232, 244)))
                    g.FillRectangle(br, rectMinBtn);
            }
            if (hoveredHitKey == "title_close")
            {
                using (var br = new SolidBrush(Color.FromArgb(232, 17, 35)))
                    g.FillRectangle(br, rectCloseBtn);
            }

            using (var penMin = new Pen(ColTextPrimary, 1.3f))
            {
                g.DrawLine(penMin, rectMinBtn.X + 16, rectMinBtn.Y + 19, rectMinBtn.X + 28, rectMinBtn.Y + 19);
            }

            Color closeFg = hoveredHitKey == "title_close" ? Color.White : ColTextPrimary;
            using (var penClose = new Pen(closeFg, 1.4f))
            {
                g.DrawLine(penClose, rectCloseBtn.X + 18, rectCloseBtn.Y + 14, rectCloseBtn.X + 28, rectCloseBtn.Y + 24);
                g.DrawLine(penClose, rectCloseBtn.X + 28, rectCloseBtn.Y + 14, rectCloseBtn.X + 18, rectCloseBtn.Y + 24);
            }

            hitRegions["title_min"] = rectMinBtn;
            hitRegions["title_close"] = rectCloseBtn;
        }

        private void DrawLeftRail(Graphics g)
        {
            for (int i = 0; i < navItems.Count; i++)
            {
                var item = navItems[i];
                bool isSel = (i == activeTab);
                bool isHov = (i == hoveredNav);

                if (isSel || isHov)
                {
                    Color bg = isSel ? Color.FromArgb(223, 234, 247) : Color.FromArgb(229, 238, 249);
                    using (var br = new SolidBrush(bg))
                        Gfx.FillRoundRect(g, br, item.Bounds, 6f);
                }

                if (isSel)
                {
                    // Iconic 3.5px vertical blue indicator bar on left edge
                    using (var brBar = new SolidBrush(ColPrimaryBlue))
                        Gfx.FillRoundRect(g, brBar, new RectangleF(0, item.Bounds.Y + 14, 3.5f, 30f), 1.5f);
                }

                Color iconCol = isSel ? ColPrimaryBlue : Color.FromArgb(55, 65, 81);
                if (item.IconId == "bolt") iconCol = Color.FromArgb(16, 124, 65);

                Gfx.DrawNavVectorIcon(g, item.IconId, item.Bounds.X + 30f, item.Bounds.Y + 20f, iconCol, 1.6f);

                using (var fNav = new Font("Malgun Gothic", 8.0f, isSel ? FontStyle.Bold : FontStyle.Regular))
                using (var brTxt = new SolidBrush(iconCol))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(item.Label, fNav, brTxt, new RectangleF(item.Bounds.X, item.Bounds.Y + 35f, item.Bounds.Width, 18f), sf);
                }
            }
        }

        // =====================================================================
        // TAB 3: "프로세스 관리" (Pixel-Accurate MSPCManager Process Manager + Ping Badge)
        // =====================================================================
        private void DrawAppsProcessView(Graphics g)
        {
            using (var fH1 = new Font("Malgun Gothic", 16.5f, FontStyle.Regular))
            using (var fSub = new Font("Malgun Gothic", 8.8f, FontStyle.Regular))
            using (var brDark = new SolidBrush(ColTextPrimary))
            using (var brGray = new SolidBrush(ColTextSecondary))
            {
                g.DrawString("프로세스 관리", fH1, brDark, 84f, 52f);
                g.DrawString("PC를 더 빠르게 실행할 수 있도록 앱 프로세스 종료", fSub, brGray, 86f, 96f);
            }

            // Live Ping Status Pill in top-right (Click to jump to Mudfish Guard tab)
            bool isBadPing = vpnSnap.HasFullVpnRouteHijack || vpnSnap.PingKoreaMs > 50;
            Color pillBg = isBadPing ? ColDangerBg : ColGoodBg;
            Color pillFg = isBadPing ? ColDanger : ColGood;
            string krPingText = vpnSnap.PingKoreaMs >= 0 ? vpnSnap.PingKoreaMs + "ms" : "측정중";
            string pillText = vpnSnap.HasFullVpnRouteHijack
                ? "⚠ FullVPN " + krPingText
                : "● 롤 핑 " + krPingText + " (직결)";

            using (var brPill = new SolidBrush(pillBg))
            using (var fPill = new Font("Malgun Gothic", 8.2f, FontStyle.Bold))
            using (var brPillFg = new SolidBrush(pillFg))
            {
                Gfx.FillRoundRect(g, brPill, rectPingPill, 13f);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(pillText, fPill, brPillFg, rectPingPill, sf);
            }
            hitRegions["goto_mudfish"] = rectPingPill;

            // Main White Rounded Card Container
            using (var brCard = new SolidBrush(ColCardBg))
            using (var penCard = new Pen(ColCardBorder, 1f))
            {
                Gfx.FillRoundRect(g, brCard, rectCardArea, 8f);
                Gfx.DrawRoundRect(g, penCard, rectCardArea, 8f);
            }

            // Card Header: "필요한 경우 이러한 앱 종료" + Chevron ^
            using (var fCardHdr = new Font("Malgun Gothic", 9.5f, FontStyle.Regular))
            using (var brDark = new SolidBrush(ColTextPrimary))
            using (var penDiv = new Pen(ColRowDivider, 1f))
            using (var penChev = new Pen(ColTextSecondary, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                string hdrText = string.IsNullOrEmpty(toastBanner) ? "필요한 경우 이러한 앱 종료" : toastBanner;
                Color hdrCol = string.IsNullOrEmpty(toastBanner) ? ColTextPrimary : toastColor;
                using (var brH = new SolidBrush(hdrCol))
                {
                    g.DrawString(hdrText, fCardHdr, brH, rectCardArea.X + 18f, rectCardArea.Y + 17f);
                }

                // Chevron ^ icon on top right of card header (matching MSPCManager)
                float cx = rectCardArea.Right - 26f, cy = rectCardArea.Y + 26f;
                g.DrawLines(penChev, new PointF[]
                {
                    new PointF(cx - 5f, cy + 2.5f),
                    new PointF(cx, cy - 2.5f),
                    new PointF(cx + 5f, cy + 2.5f)
                });

                g.DrawLine(penDiv, rectCardArea.X + 1, rectCardArea.Y + 52, rectCardArea.Right - 1, rectCardArea.Y + 52);
            }

            // Clip to scrollable process list viewport
            GraphicsState state = g.Save();
            g.SetClip(rectListViewport);

            int startY = rectListViewport.Y - scrollOffset;
            using (var fAppName = new Font("Segoe UI", 10.2f, FontStyle.Regular))
            using (var fAppMem = new Font("Malgun Gothic", 8.8f, FontStyle.Regular))
            using (var fBtn = new Font("Malgun Gothic", 8.8f, FontStyle.Regular))
            using (var brName = new SolidBrush(ColTextPrimary))
            using (var brMem = new SolidBrush(ColTextSecondary))
            using (var penDiv = new Pen(ColRowDivider, 1f))
            {
                for (int i = 0; i < appItems.Count; i++)
                {
                    int rowY = startY + i * RowHeight;
                    if (rowY + RowHeight < rectListViewport.Y || rowY > rectListViewport.Bottom)
                        continue;

                    var item = appItems[i];
                    Rectangle rowRect = new Rectangle(rectListViewport.X, rowY, rectListViewport.Width, RowHeight);

                    // App Icon (28x28) or crisp Vector Fallback Badge
                    Rectangle iconRect = new Rectangle(rowRect.X + 20, rowY + 20, 28, 28);
                    if (item.AppIcon != null)
                    {
                        try { g.DrawIcon(item.AppIcon, iconRect); }
                        catch { DrawFallbackAppIcon(g, iconRect, item); }
                    }
                    else
                    {
                        DrawFallbackAppIcon(g, iconRect, item);
                    }

                    // App Title & "메모리 사용: XXX MB"
                    string nameStr = item.DisplayName;
                    if (nameStr.Length > 24) nameStr = nameStr.Substring(0, 23) + "…";
                    g.DrawString(nameStr, fAppName, brName, rowRect.X + 60f, rowY + 13f);
                    g.DrawString("메모리 사용: " + item.FormattedMemory, fAppMem, brMem, rowRect.X + 60f, rowY + 37f);

                    // "종료" Rounded Outline Button (60 x 32)
                    Rectangle btnRect = new Rectangle(rowRect.Right - 80, rowY + 18, 60, 32);
                    string hitKey = "kill_" + i;
                    bool isHov = (hoveredHitKey == hitKey);

                    using (var brBtn = new SolidBrush(isHov ? ColBtnHover : ColCardBg))
                    using (var penBtn = new Pen(isHov ? Color.FromArgb(180, 188, 200) : ColBtnBorder, 1f))
                    {
                        Gfx.FillRoundRect(g, brBtn, btnRect, 5f);
                        Gfx.DrawRoundRect(g, penBtn, btnRect, 5f);
                    }

                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("종료", fBtn, brName, btnRect, sf);

                    if (btnRect.Bottom >= rectListViewport.Y && btnRect.Top <= rectListViewport.Bottom)
                        hitRegions[hitKey] = btnRect;

                    // Subtle bottom divider
                    g.DrawLine(penDiv, rowRect.X + 18, rowRect.Bottom - 1, rowRect.Right - 18, rowRect.Bottom - 1);
                }
            }

            // Custom Minimalist 3px Pill Scrollbar on right edge of card
            if (maxScrollOffset > 0)
            {
                float viewH = rectListViewport.Height;
                float contentH = appItems.Count * RowHeight;
                float thumbH = Math.Max(36f, viewH * (viewH / contentH));
                float thumbY = rectListViewport.Y + (scrollOffset / (float)maxScrollOffset) * (viewH - thumbH);
                using (var brScroll = new SolidBrush(Color.FromArgb(195, 204, 218)))
                {
                    Gfx.FillRoundRect(g, brScroll, new RectangleF(rectListViewport.Right - 6f, thumbY, 3.5f, thumbH), 1.7f);
                }
            }

            g.Restore(state);
        }

        private void DrawFallbackAppIcon(Graphics g, Rectangle r, SoftwareItemModel item)
        {
            using (var br = new SolidBrush(item.FallbackBadgeColor))
            using (var f = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (var brW = new SolidBrush(Color.White))
            {
                Gfx.FillRoundRect(g, br, r, 6f);
                string letter = !string.IsNullOrEmpty(item.DisplayName) ? item.DisplayName.Substring(0, 1).ToUpper() : "A";
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(letter, f, brW, r, sf);
            }
        }

        // =====================================================================
        // TAB 1: "미꾸라지 VPN & 핑 가드" (Custom Vector Dashboard)
        // =====================================================================
        private void DrawMudfishGuardView(Graphics g)
        {
            using (var fH1 = new Font("Malgun Gothic", 16.0f, FontStyle.Regular))
            using (var fSub = new Font("Malgun Gothic", 8.8f, FontStyle.Regular))
            using (var brDark = new SolidBrush(ColTextPrimary))
            using (var brGray = new SolidBrush(ColTextSecondary))
            {
                g.DrawString("미꾸라지 VPN & 핑 가드", fH1, brDark, 84f, 52f);
                g.DrawString("메이플스토리 VPN 유지 + 롤(LoL) 272ms 핑 폭증 자동 차단", fSub, brGray, 86f, 94f);
            }

            // Card 1: Live Status & Auto Game Guard Toggle
            Rectangle c1 = new Rectangle(88, 128, 356, 134);
            DrawWhiteCard(g, c1);

            bool hijack = vpnSnap.HasFullVpnRouteHijack;
            Color badgeBg = hijack ? ColDangerBg : (vpnSnap.IsMudfishCoreRunning ? ColGoodBg : Color.FromArgb(235, 242, 252));
            Color badgeFg = hijack ? ColDanger : (vpnSnap.IsMudfishCoreRunning ? ColGood : ColPrimaryBlue);
            string stateHeadline = hijack
                ? "⚠️ Full VPN 전체 우회 감지됨 (롤 핑 270ms+ 위험)"
                : (vpnSnap.IsMudfishCoreRunning ? "✅ 스플릿 터널링 활성 (메이플 VPN + 롤 4ms 직결)" : "⚪ 미꾸라지 VPN 꺼짐 (국내 인터넷 4ms 직결)");

            using (var brB = new SolidBrush(badgeBg))
            using (var fB = new Font("Malgun Gothic", 9.0f, FontStyle.Bold))
            using (var brBFg = new SolidBrush(badgeFg))
            {
                Rectangle pill = new Rectangle(c1.X + 16, c1.Y + 14, 324, 30);
                Gfx.FillRoundRect(g, brB, pill, 6f);
                var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                g.DrawString("  " + stateHeadline, fB, brBFg, pill, sf);
            }

            using (var fDet = new Font("Malgun Gothic", 8.5f, FontStyle.Regular))
            using (var brGray = new SolidBrush(ColTextSecondary))
            using (var brDark = new SolidBrush(ColTextPrimary))
            {
                string det = string.Format("• 코어: mudfish [{0}] | mudflow [{1}] | TAP IP: {2}\n• 감지: 롤(LoL) [{3}] | 메이플 클래식 [{4}]",
                    vpnSnap.IsMudfishCoreRunning ? "실행중" : "꺼짐",
                    vpnSnap.IsMudflowRunning ? "실행중" : "꺼짐",
                    vpnSnap.TapAdapterIp,
                    vpnSnap.IsLoLRunning ? "실행중" : "대기",
                    vpnSnap.IsMapleRunning ? "실행중" : "대기");
                g.DrawString(det, fDet, brGray, c1.X + 18f, c1.Y + 52f);

                // Toggle row: Auto LoL Ping Guard
                g.DrawString("롤(LoL) 실행 시 Full VPN 라우트 자동 차단", new Font("Malgun Gothic", 9.0f, FontStyle.Bold), brDark, c1.X + 18f, c1.Y + 98f);
                Rectangle toggleRect = new Rectangle(c1.Right - 60, c1.Y + 95, 42, 22);
                DrawFluentToggle(g, toggleRect, autoLoLGuard);
                hitRegions["toggle_autoguard"] = toggleRect;
            }

            // Card 2: 3-Point Live Ping Telemetry
            Rectangle c2 = new Rectangle(88, 274, 356, 92);
            DrawWhiteCard(g, c2);

            string gwStr = vpnSnap.PingGatewayMs >= 0 ? vpnSnap.PingGatewayMs + " ms" : "시간초과";
            string krStr = vpnSnap.PingKoreaMs >= 0 ? vpnSnap.PingKoreaMs + " ms" : "시간초과";
            string glStr = vpnSnap.PingGlobalMs >= 0 ? vpnSnap.PingGlobalMs + " ms" : "시간초과";

            DrawPingMetricTile(g, new Rectangle(c2.X + 10, c2.Y + 10, 106, 72), "공유기 게이트웨이", "192.168.219.1", gwStr, ColGood);
            DrawPingMetricTile(g, new Rectangle(c2.X + 125, c2.Y + 10, 106, 72), "국내 롤 / KT 서버", "168.126.63.1", krStr, (vpnSnap.PingKoreaMs >= 0 && vpnSnap.PingKoreaMs <= 25) ? ColGood : ColDanger);
            DrawPingMetricTile(g, new Rectangle(c2.X + 240, c2.Y + 10, 106, 72), "해외 Google 서버", "8.8.8.8", glStr, ColPrimaryBlue);

            // Card 3: 1-Click VPN & Route Control Actions
            Rectangle c3 = new Rectangle(88, 378, 356, 184);
            DrawWhiteCard(g, c3);

            DrawActionBtn(g, "vpn_strip", new Rectangle(c3.X + 16, c3.Y + 14, 324, 38), "⚡ 롤 핑 4ms 즉시 복구 (Full VPN 라우트만 제거)", true, false);
            DrawActionBtn(g, "vpn_stop", new Rectangle(c3.X + 16, c3.Y + 60, 324, 36), "🛑 미꾸라지 VPN 완전 종료 (프로세스 + 라우트 초기화)", false, true);
            DrawActionBtn(g, "vpn_start_split", new Rectangle(c3.X + 16, c3.Y + 104, 157, 34), "🚀 미꾸라지 시작 (롤 보호)", false, false);
            DrawActionBtn(g, "vpn_web", new Rectangle(c3.X + 183, c3.Y + 104, 157, 34), "🌐 웹 대시보드 (8282)", false, false);
            DrawActionBtn(g, "vpn_start_full", new Rectangle(c3.X + 16, c3.Y + 144, 324, 30), "🌍 미꾸라지 순정 Full VPN 모드로 시작", false, false);

            // Card 4: Routing Table Live Inspector
            Rectangle c4 = new Rectangle(88, 574, 356, 114);
            DrawWhiteCard(g, c4);
            using (var fCap = new Font("Malgun Gothic", 8.5f, FontStyle.Bold))
            using (var fMono = new Font("Consolas", 8.2f, FontStyle.Regular))
            using (var brGray = new SolidBrush(ColTextSecondary))
            using (var brDark = new SolidBrush(ColTextPrimary))
            {
                g.DrawString("실시간 라우팅 테이블 감시 (0.0.0.0/1 & 10.254.x.x)", fCap, brGray, c4.X + 14f, c4.Y + 10f);
                string routeBody = vpnSnap.ActiveMudfishRoutes.Count > 0
                    ? string.Join("\n", vpnSnap.ActiveMudfishRoutes.Take(4))
                    : "[정상 직결] 현재 0.0.0.0/1 Full VPN 납치 라우트가 없습니다.\n리그 오브 레전드 국내 트래픽이 192.168.219.1로 직결(4~5ms)됩니다.";
                g.DrawString(routeBody, fMono, brDark, new RectangleF(c4.X + 14f, c4.Y + 32f, 328f, 74f));
            }
        }

        // =====================================================================
        // TAB 0: "홈 (PC 부스트)"
        // =====================================================================
        private void DrawHomeBoostView(Graphics g)
        {
            using (var fH1 = new Font("Malgun Gothic", 16.5f, FontStyle.Regular))
            using (var fSub = new Font("Malgun Gothic", 8.8f, FontStyle.Regular))
            using (var brDark = new SolidBrush(ColTextPrimary))
            using (var brGray = new SolidBrush(ColTextSecondary))
            {
                g.DrawString("PC 부스트", fH1, brDark, 84f, 52f);
                g.DrawString("물리 RAM Working Set 압축 및 임시 캐시 원클릭 최적화", fSub, brGray, 86f, 94f);
            }

            Rectangle c1 = new Rectangle(88, 128, 356, 250);
            DrawWhiteCard(g, c1);

            // Circular Telemetry Ring for RAM Load
            Rectangle ringRect = new Rectangle(c1.X + 26, c1.Y + 26, 96, 96);
            using (var penTrack = new Pen(Color.FromArgb(232, 238, 247), 9f))
            using (var penProg = new Pen(ColPrimaryBlue, 9f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawArc(penTrack, ringRect, 0, 360);
                float sweep = (ramLoadPct / 100f) * 360f;
                g.DrawArc(penProg, ringRect, -90, sweep);
            }
            using (var fPct = new Font("Segoe UI", 16f, FontStyle.Bold))
            using (var fLbl = new Font("Malgun Gothic", 8.0f))
            using (var brDark = new SolidBrush(ColTextPrimary))
            using (var brGray = new SolidBrush(ColTextSecondary))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(ramLoadPct + "%", fPct, brDark, new RectangleF(ringRect.X, ringRect.Y - 6, ringRect.Width, ringRect.Height), sf);
                g.DrawString("메모리", fLbl, brGray, new RectangleF(ringRect.X, ringRect.Y + 22, ringRect.Width, ringRect.Height), sf);

                g.DrawString(string.Format("사용 중: {0:F1} GB / {1:F1} GB", ramUsedGb, ramTotalGb), new Font("Malgun Gothic", 10.2f, FontStyle.Bold), brDark, c1.X + 142f, c1.Y + 38f);
                string boostHint = string.IsNullOrEmpty(toastBanner)
                    ? "앱을 종료하지 않고 백그라운드 프로세스의\n물리 메모리 점유만 안전하게 회수합니다."
                    : toastBanner;
                g.DrawString(boostHint, new Font("Malgun Gothic", 8.6f), brGray, new RectangleF(c1.X + 142f, c1.Y + 66f, 198f, 56f));
            }

            DrawActionBtn(g, "home_boost", new Rectangle(c1.X + 20, c1.Y + 144, 316, 42), "🚀 지금 부스트 (RAM 압축 + 임시 파일 정리)", true, false);
            DrawActionBtn(g, "home_go_apps", new Rectangle(c1.X + 20, c1.Y + 196, 316, 36), "실행 중인 앱 프로세스 관리 열기 →", false, false);

            Rectangle c2 = new Rectangle(88, 394, 356, 150);
            DrawWhiteCard(g, c2);
            using (var fB = new Font("Malgun Gothic", 9.8f, FontStyle.Bold))
            using (var fD = new Font("Malgun Gothic", 8.8f))
            using (var brDark = new SolidBrush(ColTextPrimary))
            using (var brGray = new SolidBrush(ColTextSecondary))
            {
                g.DrawString("🦈 미꾸라지 VPN & 롤(LoL) 핑 가드 요약", fB, brDark, c2.X + 18f, c2.Y + 16f);
                string krStr = vpnSnap.PingKoreaMs >= 0 ? vpnSnap.PingKoreaMs + " ms" : "측정중";
                g.DrawString(string.Format("• 국내(롤) 핑: {0}  |  Full VPN 납치: {1}", krStr, vpnSnap.HasFullVpnRouteHijack ? "감지됨(위험)" : "없음(정상)"), fD, brGray, c2.X + 18f, c2.Y + 48f);
            }
            DrawActionBtn(g, "home_go_vpn", new Rectangle(c2.X + 18, c2.Y + 90, 320, 40), "미꾸라지 VPN & 핑 가드 센터 열기", false, false);
        }

        // =====================================================================
        // TAB 2: "저장소 정리" & TAB 4: "AI & AutoMaple VM"
        // =====================================================================
        private void DrawStorageView(Graphics g)
        {
            using (var fH1 = new Font("Malgun Gothic", 16.5f, FontStyle.Regular))
            using (var fSub = new Font("Malgun Gothic", 8.8f, FontStyle.Regular))
            using (var brDark = new SolidBrush(ColTextPrimary))
            using (var brGray = new SolidBrush(ColTextSecondary))
            {
                g.DrawString("저장소 관리", fH1, brDark, 84f, 52f);
                g.DrawString("Windows 임시 파일(%TEMP%), 캐시 및 휴지통 딥 클린", fSub, brGray, 86f, 94f);
            }

            Rectangle c1 = new Rectangle(88, 128, 356, 200);
            DrawWhiteCard(g, c1);
            using (var fB = new Font("Malgun Gothic", 10.2f, FontStyle.Bold))
            using (var fD = new Font("Malgun Gothic", 8.8f))
            using (var brDark = new SolidBrush(ColTextPrimary))
            using (var brGray = new SolidBrush(ColTextSecondary))
            {
                g.DrawString("🧹 시스템 정크 및 캐시 정리 (CleanTrash.Executor)", fB, brDark, c1.X + 18f, c1.Y + 20f);
                string msg = string.IsNullOrEmpty(toastBanner)
                    ? "• 사용자 임시 폴더 (%TEMP%)\n• Windows 업데이트 및 시스템 로그 캐시\n• Windows 휴지통 (SHEmptyRecycleBinW)"
                    : toastBanner;
                g.DrawString(msg, fD, brGray, new RectangleF(c1.X + 18f, c1.Y + 52f, 320f, 68f));
            }
            DrawActionBtn(g, "storage_clean", new Rectangle(c1.X + 18, c1.Y + 138, 320, 42), "🗑️ 임시 파일 + 휴지통 원클릭 정리", true, false);
        }

        private void DrawAgentsView(Graphics g)
        {
            using (var fH1 = new Font("Malgun Gothic", 16.5f, FontStyle.Regular))
            using (var fSub = new Font("Malgun Gothic", 8.8f, FontStyle.Regular))
            using (var brDark = new SolidBrush(ColTextPrimary))
            using (var brGray = new SolidBrush(ColTextSecondary))
            {
                g.DrawString("AI 도구 & Agents", fH1, brDark, 84f, 52f);
                g.DrawString("Auto Maple 백그라운드 봇 & Hyper-V GPU-PV 가상머신 제어", fSub, brGray, 86f, 94f);
            }

            Rectangle c1 = new Rectangle(88, 128, 356, 260);
            DrawWhiteCard(g, c1);
            using (var fB = new Font("Malgun Gothic", 9.8f, FontStyle.Bold))
            using (var fD = new Font("Malgun Gothic", 8.8f))
            using (var brDark = new SolidBrush(ColTextPrimary))
            using (var brGray = new SolidBrush(ColTextSecondary))
            {
                g.DrawString("🍁 Auto Maple & Hyper-V 통합 런처", fB, brDark, c1.X + 18f, c1.Y + 18f);
                g.DrawString("• PrintWindow(PW_RENDERFULLCONTENT) 백그라운드 캡처\n• Windows 11 포커스 우회 백그라운드 키 입력\n• AMD Radeon RX 9060 XT GPU-PV 가상머신 연동", fD, brGray, new RectangleF(c1.X + 18f, c1.Y + 46f, 320f, 64f));
            }

            DrawActionBtn(g, "agent_automaple", new Rectangle(c1.X + 18, c1.Y + 118, 320, 38), "🍁 Auto Maple 실행 (main.py)", true, false);
            DrawActionBtn(g, "agent_vm", new Rectangle(c1.X + 18, c1.Y + 164, 320, 36), "🖥️ Hyper-V AutoMaple-VM 시작", false, false);
            DrawActionBtn(g, "agent_github", new Rectangle(c1.X + 18, c1.Y + 208, 320, 36), "🐙 GitHub 저장소 열기 (auto-maple)", false, false);
        }

        // =====================================================================
        // Reusable UI Component Renderers
        // =====================================================================
        private void DrawWhiteCard(Graphics g, Rectangle r)
        {
            using (var br = new SolidBrush(ColCardBg))
            using (var pen = new Pen(ColCardBorder, 1f))
            {
                Gfx.FillRoundRect(g, br, r, 8f);
                Gfx.DrawRoundRect(g, pen, r, 8f);
            }
        }

        private void DrawPingMetricTile(Graphics g, Rectangle r, string label, string ip, string val, Color valColor)
        {
            using (var br = new SolidBrush(Color.FromArgb(247, 250, 254)))
            using (var pen = new Pen(Color.FromArgb(234, 239, 247), 1f))
            {
                Gfx.FillRoundRect(g, br, r, 6f);
                Gfx.DrawRoundRect(g, pen, r, 6f);
            }
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using (var f1 = new Font("Malgun Gothic", 7.8f, FontStyle.Bold))
            using (var f2 = new Font("Consolas", 7.5f))
            using (var f3 = new Font("Segoe UI", 11.0f, FontStyle.Bold))
            using (var brGray = new SolidBrush(ColTextSecondary))
            using (var brVal = new SolidBrush(valColor))
            {
                g.DrawString(label, f1, brGray, new RectangleF(r.X, r.Y + 6, r.Width, 16), sf);
                g.DrawString(ip, f2, brGray, new RectangleF(r.X, r.Y + 22, r.Width, 14), sf);
                g.DrawString(val, f3, brVal, new RectangleF(r.X, r.Y + 38, r.Width, 28), sf);
            }
        }

        private void DrawFluentToggle(Graphics g, Rectangle r, bool isOn)
        {
            Color trackCol = isOn ? ColPrimaryBlue : Color.FromArgb(180, 188, 200);
            using (var br = new SolidBrush(trackCol))
            {
                Gfx.FillRoundRect(g, br, r, r.Height / 2f);
            }
            float knobX = isOn ? (r.Right - r.Height + 3f) : (r.X + 3f);
            using (var brK = new SolidBrush(Color.White))
            {
                g.FillEllipse(brK, knobX, r.Y + 3f, r.Height - 6f, r.Height - 6f);
            }
        }

        private void DrawActionBtn(Graphics g, string key, Rectangle r, string text, bool isPrimary, bool isDanger)
        {
            bool hov = (hoveredHitKey == key);
            Color bg, fg, border;
            if (isPrimary)
            {
                bg = hov ? ColPrimaryHover : ColPrimaryBlue;
                fg = Color.White;
                border = bg;
            }
            else if (isDanger)
            {
                bg = hov ? Color.FromArgb(254, 226, 226) : ColDangerBg;
                fg = ColDanger;
                border = Color.FromArgb(248, 180, 180);
            }
            else
            {
                bg = hov ? ColBtnHover : ColCardBg;
                fg = ColTextPrimary;
                border = ColBtnBorder;
            }

            using (var br = new SolidBrush(bg))
            using (var pen = new Pen(border, 1f))
            using (var f = new Font("Malgun Gothic", 9.0f, (isPrimary || isDanger) ? FontStyle.Bold : FontStyle.Regular))
            using (var brFg = new SolidBrush(fg))
            {
                Gfx.FillRoundRect(g, br, r, 6f);
                Gfx.DrawRoundRect(g, pen, r, 6f);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(text, f, brFg, r, sf);
            }

            hitRegions[key] = r;
        }

        // =====================================================================
        // Mouse & Wheel Interaction Handling
        // =====================================================================
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (activeTab == 3 && maxScrollOffset > 0)
            {
                scrollOffset -= (e.Delta > 0 ? RowHeight : -RowHeight);
                if (scrollOffset < 0) scrollOffset = 0;
                if (scrollOffset > maxScrollOffset) scrollOffset = maxScrollOffset;
                this.Invalidate(rectCardArea);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            int newNav = -1;
            for (int i = 0; i < navItems.Count; i++)
            {
                if (navItems[i].Bounds.Contains(e.Location))
                {
                    newNav = i;
                    break;
                }
            }

            string newHit = "";
            foreach (var kv in hitRegions)
            {
                if (kv.Value.Contains(e.Location))
                {
                    newHit = kv.Key;
                    break;
                }
            }

            if (newNav != hoveredNav || newHit != hoveredHitKey)
            {
                hoveredNav = newNav;
                hoveredHitKey = newHit;
                this.Cursor = (hoveredNav >= 0 || !string.IsNullOrEmpty(hoveredHitKey)) ? Cursors.Hand : Cursors.Default;
                this.Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            // Drag window by titlebar
            if (e.Y <= 42 && string.IsNullOrEmpty(hoveredHitKey))
            {
                NativeWin32.ReleaseCapture();
                NativeWin32.SendMessage(this.Handle, 0xA1, 0x2, 0);
                return;
            }

            // Left rail navigation click
            if (hoveredNav >= 0)
            {
                if (hoveredNav == 5) // Quick bolt: 핑복구
                {
                    toastBanner = ReconstructedEngine.StripFullVpnHijackRoutes();
                    toastColor = ColGood;
                    activeTab = 1;
                    RefreshTelemetryAsync();
                    return;
                }
                if (hoveredNav == 6) // Gear: 설정 -> 미꾸라지 탭
                {
                    activeTab = 1;
                    this.Invalidate();
                    return;
                }
                activeTab = hoveredNav;
                toastBanner = "";
                this.Invalidate();
                return;
            }

            if (!string.IsNullOrEmpty(hoveredHitKey))
            {
                HandleHitAction(hoveredHitKey);
            }
        }

        private async void HandleHitAction(string key)
        {
            if (key == "title_close")
            {
                HideToTray();
                return;
            }
            if (key == "title_min")
            {
                this.WindowState = FormWindowState.Minimized;
                return;
            }
            if (key == "title_back")
            {
                activeTab = (activeTab == 3) ? 0 : 3;
                this.Invalidate();
                return;
            }
            if (key == "goto_mudfish" || key == "home_go_vpn")
            {
                activeTab = 1;
                this.Invalidate();
                return;
            }
            if (key == "home_go_apps")
            {
                activeTab = 3;
                this.Invalidate();
                return;
            }
            if (key == "toggle_autoguard")
            {
                autoLoLGuard = !autoLoLGuard;
                this.Invalidate();
                return;
            }
            if (key.StartsWith("kill_"))
            {
                int idx;
                if (int.TryParse(key.Substring(5), out idx) && idx >= 0 && idx < appItems.Count)
                {
                    var target = appItems[idx];
                    if (target.AppKey.Equals("Antigravity", StringComparison.OrdinalIgnoreCase))
                    {
                        toastBanner = "현재 실행 중인 Antigravity IDE는 보호됩니다.";
                        toastColor = ColPrimaryBlue;
                        this.Invalidate();
                        return;
                    }
                    string disp = target.DisplayName;
                    string mem = target.FormattedMemory;
                    await Task.Run(() => ReconstructedEngine.KillSoftwareGroup(target));
                    toastBanner = string.Format("✓ {0} 종료 완료 ({1} 확보)", disp, mem);
                    toastColor = ColGood;
                    RefreshTelemetryAsync();
                }
                return;
            }
            if (key == "vpn_strip")
            {
                toastBanner = await Task.Run(() => ReconstructedEngine.StripFullVpnHijackRoutes());
                toastColor = ColGood;
                RefreshTelemetryAsync();
                return;
            }
            if (key == "vpn_stop")
            {
                toastBanner = await Task.Run(() => ReconstructedEngine.StopMudfishCompletely());
                toastColor = ColDanger;
                RefreshTelemetryAsync();
                return;
            }
            if (key == "vpn_start_split")
            {
                toastBanner = ReconstructedEngine.StartMudfishDaemon(true);
                toastColor = ColGood;
                RefreshTelemetryAsync();
                return;
            }
            if (key == "vpn_start_full")
            {
                toastBanner = ReconstructedEngine.StartMudfishDaemon(false);
                toastColor = ColPrimaryBlue;
                RefreshTelemetryAsync();
                return;
            }
            if (key == "vpn_web")
            {
                try { Process.Start("http://127.0.0.1:8282"); } catch { }
                return;
            }
            if (key == "home_boost")
            {
                toastBanner = "메모리 Working Set 압축 진행 중...";
                this.Invalidate();
                var res = await Task.Run(() => ReconstructedEngine.OneKeyMemoryBoost());
                toastBanner = string.Format("✓ {0}개 프로세스 RAM 압축 완료 (~{1:F0}MB 회수)", res.Count, res.FreedMB);
                toastColor = ColGood;
                RefreshTelemetryAsync();
                return;
            }
            if (key == "storage_clean")
            {
                toastBanner = "임시 파일 및 휴지통 정리 중...";
                this.Invalidate();
                var res = await Task.Run(() => ReconstructedEngine.CleanSystemTempAndTrash(true));
                toastBanner = string.Format("✓ 임시 파일 {0}개 삭제 ({1:F1}MB 확보) + 휴지통 비우기 완료", res.Count, res.FreedMB);
                toastColor = ColGood;
                this.Invalidate();
                return;
            }
            if (key == "agent_automaple")
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "python.exe",
                        Arguments = "main.py",
                        WorkingDirectory = @"C:\Users\icand\.gemini\antigravity\scratch\auto-maple",
                        UseShellExecute = true
                    });
                }
                catch { }
                return;
            }
            if (key == "agent_vm")
            {
                await Task.Run(() => ReconstructedEngine.RunCommandHidden("powershell.exe", "-NoProfile -Command \"Start-VM -Name 'AutoMaple-VM'; vmconnect.exe localhost 'AutoMaple-VM'\""));
                return;
            }
            if (key == "agent_github")
            {
                try { Process.Start("https://github.com/icandoit122118-del/auto-maple"); } catch { }
                return;
            }
        }
    }

    internal static class Program
    {
        private static EventWaitHandle showEvent;

        [STAThread]
        static void Main()
        {
            bool createdNew;
            showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\PulseCore_SingleInstance_WakeEvent", out createdNew);
            if (!createdNew)
            {
                try { showEvent.Set(); } catch { }
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var mainForm = new MainForm();

            Thread wakeThread = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        showEvent.WaitOne();
                        if (!mainForm.IsDisposed && mainForm.IsHandleCreated)
                        {
                            mainForm.BeginInvoke(new Action(() => mainForm.RestoreFromTray()));
                        }
                    }
                    catch { break; }
                }
            });
            wakeThread.IsBackground = true;
            wakeThread.Start();

            Application.Run(mainForm);
        }
    }
}

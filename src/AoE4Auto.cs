using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace AoE4Auto
{
    // ---------- Win32 结构 ----------
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; public POINT(int x, int y) { X = x; Y = y; } }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct INPUT64
    {
        [FieldOffset(0)] public uint type;
        [FieldOffset(8)] public MOUSEINPUT mi;
        [FieldOffset(8)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Explicit, Size = 28)]
    public struct INPUT32
    {
        [FieldOffset(0)] public uint type;
        [FieldOffset(4)] public MOUSEINPUT mi;
        [FieldOffset(4)] public KEYBDINPUT ki;
    }

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    // ---------- Win32 P/Invoke ----------
    public static class Native
    {
        public const uint MOUSEEVENTF_MOVE = 0x0001;
        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP = 0x0004;
        public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        public const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const int SW_RESTORE = 9;
        public const int VK_RETURN = 0x0D;
        public const int VK_MENU = 0x12;
        public const int VK_F9 = 120;
        public const int VK_F10 = 121;
        public const int MOD_NOREPEAT = 0x4000;
        public const int WM_HOTKEY = 0x0312;
        public const int SM_XVIRTUALSCREEN = 76;
        public const int SM_YVIRTUALSCREEN = 77;
        public const int SM_CXVIRTUALSCREEN = 78;
        public const int SM_CYVIRTUALSCREEN = 79;

        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("shcore.dll")] public static extern int SetProcessDpiAwareness(int value);
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT pt);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
        [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder name, int count);
        [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT pt);
        [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] public static extern IntPtr SetActiveWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, IntPtr extra);
        [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
        [DllImport("user32.dll", EntryPoint = "SendInput")] public static extern uint SendInput64(uint count, INPUT64[] inputs, int size);
        [DllImport("user32.dll", EntryPoint = "SendInput")] public static extern uint SendInput32(uint count, INPUT32[] inputs, int size);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, int modifiers, int vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }

    // ---------- 输入模拟 ----------
    public static class Input
    {
        public const uint MOVE = 0x0001;
        public const uint LEFTDOWN = 0x0002;
        public const uint LEFTUP = 0x0004;
        public const uint ABSOLUTE = 0x8000;
        public const uint VIRTUALDESK = 0x4000;
        public const uint KEYUP = 0x0002;

        public static bool Mouse(uint flags, int x, int y)
        {
            int dx = 0, dy = 0;
            if ((flags & MOVE) != 0)
            {
                int vx = Native.GetSystemMetrics(Native.SM_XVIRTUALSCREEN);
                int vy = Native.GetSystemMetrics(Native.SM_YVIRTUALSCREEN);
                int vw = Math.Max(1, Native.GetSystemMetrics(Native.SM_CXVIRTUALSCREEN));
                int vh = Math.Max(1, Native.GetSystemMetrics(Native.SM_CYVIRTUALSCREEN));
                dx = (int)Math.Round(((x - vx) * 65535.0) / (vw - 1));
                dy = (int)Math.Round(((y - vy) * 65535.0) / (vh - 1));
            }
            if (IntPtr.Size == 8)
            {
                INPUT64[] arr = new INPUT64[1];
                arr[0].type = 0;
                arr[0].mi = new MOUSEINPUT();
                arr[0].mi.dx = dx;
                arr[0].mi.dy = dy;
                arr[0].mi.dwFlags = flags;
                return Native.SendInput64(1, arr, Marshal.SizeOf(typeof(INPUT64))) != 0;
            }
            else
            {
                INPUT32[] arr = new INPUT32[1];
                arr[0].type = 0;
                arr[0].mi = new MOUSEINPUT();
                arr[0].mi.dx = dx;
                arr[0].mi.dy = dy;
                arr[0].mi.dwFlags = flags;
                return Native.SendInput32(1, arr, Marshal.SizeOf(typeof(INPUT32))) != 0;
            }
        }

        public static bool Key(int vk, int scan, bool up)
        {
            uint flags = up ? KEYUP : 0;
            if (IntPtr.Size == 8)
            {
                INPUT64[] arr = new INPUT64[1];
                arr[0].type = 1;
                arr[0].ki = new KEYBDINPUT();
                arr[0].ki.wVk = (ushort)vk;
                arr[0].ki.wScan = (ushort)scan;
                arr[0].ki.dwFlags = flags;
                return Native.SendInput64(1, arr, Marshal.SizeOf(typeof(INPUT64))) != 0;
            }
            else
            {
                INPUT32[] arr = new INPUT32[1];
                arr[0].type = 1;
                arr[0].ki = new KEYBDINPUT();
                arr[0].ki.wVk = (ushort)vk;
                arr[0].ki.wScan = (ushort)scan;
                arr[0].ki.dwFlags = flags;
                return Native.SendInput32(1, arr, Marshal.SizeOf(typeof(INPUT32))) != 0;
            }
        }
    }

    // ---------- 日志 ----------
    public static class Log
    {
        public static readonly object Sync = new object();
        private static string _path;

        public static void Init(string path) { _path = path; }

        public static void W(string msg) { C(null, msg); }

        public static void C(ConsoleColor? color, string msg)
        {
            lock (Sync)
            {
                string line = string.Format("[{0:HH:mm:ss}] {1}", DateTime.Now, msg);
                ConsoleColor old = Console.ForegroundColor;
                if (color.HasValue) { try { Console.ForegroundColor = color.Value; } catch { } }
                try { Console.WriteLine(line); } catch { }
                if (color.HasValue) { try { Console.ForegroundColor = old; } catch { } }
                if (!string.IsNullOrEmpty(_path))
                {
                    try { File.AppendAllText(_path, line + "\r\n", new UTF8Encoding(false)); }
                    catch { }
                }
            }
        }

        public static void Blank()
        {
            lock (Sync) { Console.WriteLine(); }
        }
    }

    // ---------- 配置 ----------
    public class Config
    {
        public int StartDelaySec;
        public int PanelWaitSec;
        public int BattleWaitSec;
        public int PollMs;
        public double MatchThreshold;
        public int BriefingTimeoutSec;
        public int VictoryTimeoutSec;
        public int PanelExtraClicks;
        public int MaxRounds;
        public int SaveDebugFrames;
        public int PauseOnExit;
        // ---- 新增：选择稍后 ----

        public Config()
        {
            StartDelaySec = 3;
            PanelWaitSec = 240;
            BattleWaitSec = 240;
            PollMs = 700;
            MatchThreshold = 0.6;
            BriefingTimeoutSec = 240;
            VictoryTimeoutSec = 1500;
            PanelExtraClicks = 2;
            PauseOnExit = 1;
                    }

        public static Config Load(string path)
        {
            Config cfg = new Config();
            if (!File.Exists(path))
            {
                try { cfg.Save(path); } catch { }
                return cfg;
            }
            string[] lines;
            try { lines = File.ReadAllLines(path, Encoding.UTF8); }
            catch { return cfg; }

            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith(";") || line.StartsWith("#") || line.StartsWith("[")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string val = line.Substring(eq + 1).Trim();
                int iv; double dv;
                switch (key)
                {
                    case "startdelaysec":
                        if (int.TryParse(val, out iv)) cfg.StartDelaySec = iv;
                        break;
                    case "panelwaitsec":
                        if (int.TryParse(val, out iv)) cfg.PanelWaitSec = Math.Max(10, iv);
                        break;
                    case "battlewaitsec":
                        if (int.TryParse(val, out iv)) cfg.BattleWaitSec = iv;
                        break;
                    case "pollms":
                        if (int.TryParse(val, out iv)) cfg.PollMs = Math.Max(100, iv);
                        break;
                    case "matchthreshold":
                        if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out dv)) cfg.MatchThreshold = dv;
                        break;
                    case "briefingtimeoutsec":
                        if (int.TryParse(val, out iv)) cfg.BriefingTimeoutSec = iv;
                        break;
                    case "victorytimeoutsec":
                        if (int.TryParse(val, out iv)) cfg.VictoryTimeoutSec = iv;
                        break;
                    case "panelextraclicks":
                        if (int.TryParse(val, out iv)) cfg.PanelExtraClicks = Math.Max(0, iv);
                        break;
                    case "maxrounds":
                        if (int.TryParse(val, out iv)) cfg.MaxRounds = Math.Max(0, iv);
                        break;
                    case "savedebugframes":
                        if (int.TryParse(val, out iv)) cfg.SaveDebugFrames = iv;
                        break;
                    case "pauseonexit":
                        if (int.TryParse(val, out iv)) cfg.PauseOnExit = iv;
                        break;
                }
            }
            return cfg;
        }

        public void Save(string path)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("; AoE4Auto 配置文件（保存后重新运行程序生效）");
            sb.AppendLine("; 数值单位：Sec=秒，Ms=毫秒；行尾不要加注释，");
            sb.AppendLine("[AoE4Auto]");
            sb.AppendLine("; 启动后等待多少秒再开始（留时间切窗口）");
            sb.AppendLine("StartDelaySec=" + StartDelaySec);
            sb.AppendLine("; 点击“开始游戏”后，最多等多少秒出现“选择一项增益”面板（不写死加载时间）");
            sb.AppendLine("PanelWaitSec=" + PanelWaitSec);
            sb.AppendLine("; 每局开始后等待多少秒才开始检测胜利（默认 240 秒 = 4 分钟）");
            sb.AppendLine("BattleWaitSec=" + BattleWaitSec);
            sb.AppendLine("; 检测轮询间隔（毫秒）");
            sb.AppendLine("PollMs=" + PollMs);
            sb.AppendLine("; 图像匹配阈值 0~1，识别不到可适当降低（0.6）");
            sb.AppendLine("MatchThreshold=" + MatchThreshold.ToString("0.00", CultureInfo.InvariantCulture));
            sb.AppendLine("; 等待“开始游戏”按钮出现的最长秒数");
            sb.AppendLine("BriefingTimeoutSec=" + BriefingTimeoutSec);
            sb.AppendLine("; 等待“胜利！”的最长秒数（超时只警告，不会退出）");
            sb.AppendLine("VictoryTimeoutSec=" + VictoryTimeoutSec);
            sb.AppendLine("; 检测到蓝色增益框时，额外补点几次（已废弃，保留兼容）");
            sb.AppendLine("PanelExtraClicks=" + PanelExtraClicks);
            sb.AppendLine("; 循环次数，0 表示一直循环");
            sb.AppendLine("MaxRounds=" + MaxRounds);
            sb.AppendLine("; 1=每次识别都把当前画面存到 frames 目录，便于排查");
            sb.AppendLine("SaveDebugFrames=" + SaveDebugFrames);
            sb.AppendLine("; 程序结束后是否等待按键再关窗口");
            sb.AppendLine("PauseOnExit=" + PauseOnExit);
            sb.AppendLine();
            try { File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true)); } catch { }
        }
    }

    // ---------- 模板 ----------
    public class Template
    {
        public int W;
        public int H;
        public double[] Px;   // 零均值灰度（640x480 参考系匹配用）
        public double Std;
        public double[] Gray; // 原始灰度 0..255（多尺度匹配用）

        public static Template FromBase64(string b64)
        {
            using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(b64)))
            using (Bitmap bmp = new Bitmap(ms))
            {
                Template t = new Template();
                t.W = bmp.Width;
                t.H = bmp.Height;
                int n = t.W * t.H;
                t.Px = new double[n];
                double sum = 0;
                for (int y = 0; y < t.H; y++)
                {
                    for (int x = 0; x < t.W; x++)
                    {
                        Color c = bmp.GetPixel(x, y);
                        double g = 0.114 * c.B + 0.587 * c.G + 0.299 * c.R;
                        t.Px[y * t.W + x] = g;
                        sum += g;
                    }
                }
                double mean = sum / n;
                double acc = 0;
                for (int i = 0; i < n; i++)
                {
                    t.Px[i] -= mean;
                    acc += t.Px[i] * t.Px[i];
                }
                // 注意：这里用未除以像素数的范数，与 Match 里的 sqrt(var) 配对才是正确的归一化互相关
                t.Std = Math.Sqrt(acc);
                t.Gray = new double[n];
                for (int i = 0; i < n; i++) t.Gray[i] = t.Px[i] + mean;
                return t;
            }
        }
    }

    public class Hit
    {
        public bool Found;
        public double Score;
        public int X;
        public int Y;
        public double Scale;
    }

    // ---------- 视觉 ----------
    public static class Vision
    {
        public const int RefW = 640;
        public const int RefH = 480;

        public static Rectangle BriefingRoi = new Rectangle(0, 255, 240, 90);
        public static Rectangle VictoryRoi = new Rectangle(0, 80, 340, 240);
        public static Rectangle PanelRoi = new Rectangle(240, 80, 400, 130);
        public static Rectangle ReplayRoi = new Rectangle(0, 410, 640, 70);

        public static Template BriefingTpl;
        public static Template VictoryTpl;
        public static Template PanelTpl;
        public static Template WaitTpl;
        public static Template ReplayTpl;

        // 最近一次截图使用的坐标基准（截图与点击必须同基准，否则窗口一动就点偏）
        public static POINT LastOrigin;
        public static int LastW;
        public static int LastH;
        public static POINT LastNativeOrigin;
        public static int LastNativeW;
        public static int LastNativeH;

        public static void Init()
        {
            BriefingTpl = Template.FromBase64(Templates.BriefingB64);
            VictoryTpl = Template.FromBase64(Templates.VictoryB64);
            PanelTpl = Template.FromBase64(Templates.PanelB64);
            WaitTpl = Template.FromBase64(Templates.WaitB64);
            ReplayTpl = Template.FromBase64(Templates.ReplayB64);
        }

        // 截取客户区并缩放到 640x480（原有流程）
        public static Bitmap Capture(IntPtr hwnd, out int cw, out int ch)
        {
            cw = 0; ch = 0;
            POINT p0 = new POINT(0, 0);
            Native.ClientToScreen(hwnd, ref p0);
            RECT rc;
            Native.GetClientRect(hwnd, out rc);
            POINT p1 = new POINT(rc.Right, rc.Bottom);
            Native.ClientToScreen(hwnd, ref p1);
            int w = p1.X - p0.X;
            int h = p1.Y - p0.Y;
            cw = w; ch = h;
            LastOrigin = p0; LastW = w; LastH = h;
            if (w <= 0 || h <= 0) return null;

            Bitmap full = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            try
            {
                using (Graphics g = Graphics.FromImage(full))
                {
                    g.Clear(Color.Black);
                    Rectangle vs = SystemInformation.VirtualScreen;
                    Rectangle dst = new Rectangle(p0.X, p0.Y, w, h);
                    Rectangle inter = Rectangle.Intersect(dst, vs);
                    if (inter.Width > 0 && inter.Height > 0)
                    {
                        g.CopyFromScreen(inter.Left, inter.Top, inter.Left - p0.X, inter.Top - p0.Y, inter.Size, CopyPixelOperation.SourceCopy);
                    }
                }
                if (w == RefW && h == RefH) return full;

                Bitmap scaled = new Bitmap(RefW, RefH, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(scaled))
                using (ImageAttributes ia = new ImageAttributes())
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.DrawImage(full, new Rectangle(0, 0, RefW, RefH), 0, 0, w, h, GraphicsUnit.Pixel, ia);
                }
                full.Dispose();
                return scaled;
            }
            catch
            {
                full.Dispose();
                return null;
            }
        }

        // 截取客户区，保持原生分辨率（“选择稍后”按钮匹配用）
        public static Bitmap CaptureNative(IntPtr hwnd, out int cw, out int ch)
        {
            cw = 0; ch = 0;
            POINT p0 = new POINT(0, 0);
            Native.ClientToScreen(hwnd, ref p0);
            RECT rc;
            Native.GetClientRect(hwnd, out rc);
            POINT p1 = new POINT(rc.Right, rc.Bottom);
            Native.ClientToScreen(hwnd, ref p1);
            int w = p1.X - p0.X;
            int h = p1.Y - p0.Y;
            cw = w; ch = h;
            LastNativeOrigin = p0; LastNativeW = w; LastNativeH = h;
            if (w <= 0 || h <= 0) return null;
            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            try
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Black);
                    Rectangle vs = SystemInformation.VirtualScreen;
                    Rectangle dst = new Rectangle(p0.X, p0.Y, w, h);
                    Rectangle inter = Rectangle.Intersect(dst, vs);
                    if (inter.Width > 0 && inter.Height > 0)
                    {
                        g.CopyFromScreen(inter.Left, inter.Top, inter.Left - p0.X, inter.Top - p0.Y, inter.Size, CopyPixelOperation.SourceCopy);
                    }
                }
                return bmp;
            }
            catch
            {
                bmp.Dispose();
                return null;
            }
        }

        // 取图片整幅灰度
        public static double[] ToGray(Bitmap bmp, out int w, out int h)
        {
            w = bmp.Width; h = bmp.Height;
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = bd.Stride;
                byte[] buf = new byte[stride * h];
                Marshal.Copy(bd.Scan0, buf, 0, buf.Length);
                double[] g = new double[w * h];
                for (int y = 0; y < h; y++)
                {
                    int row = y * stride;
                    int o = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        int i = row + x * 4;
                        g[o + x] = 0.114 * buf[i] + 0.587 * buf[i + 1] + 0.299 * buf[i + 2];
                    }
                }
                return g;
            }
            finally { bmp.UnlockBits(bd); }
        }

        // 取 ROI 灰度
        public static double[] GrayRoi(Bitmap bmp, Rectangle roi)
        {
            int w = bmp.Width, h = bmp.Height;
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = bd.Stride;
                byte[] buf = new byte[stride * h];
                Marshal.Copy(bd.Scan0, buf, 0, buf.Length);
                double[] g = new double[roi.Width * roi.Height];
                for (int y = 0; y < roi.Height; y++)
                {
                    int row = (roi.Y + y) * stride;
                    int o = y * roi.Width;
                    for (int x = 0; x < roi.Width; x++)
                    {
                        int i = row + (roi.X + x) * 4;
                        g[o + x] = 0.114 * buf[i] + 0.587 * buf[i + 1] + 0.299 * buf[i + 2];
                    }
                }
                return g;
            }
            finally { bmp.UnlockBits(bd); }
        }

        // 原有 640x480 模板匹配（归一化互相关）
        // 先按 1.0 倍匹配；匹配不上时再试几个缩放（适配不同分辨率/UI 缩放），
        // 这样常见情况不受影响，异常分辨率也能兜住。
        public static Hit Match(Bitmap frame, Template tpl, Rectangle roi, double threshold)
        {
            Hit hit = new Hit();
            Rectangle r = Rectangle.Intersect(roi, new Rectangle(0, 0, frame.Width, frame.Height));
            if (r.Width < 8 || r.Height < 6) return hit;
            double[] px = GrayRoi(frame, r);
            int w = r.Width, h = r.Height;

            double best = -2;
            int bx = 0, by = 0;
            int btw = tpl.W, bth = tpl.H;

            ScanScale(px, w, h, tpl.Gray, tpl.W, tpl.H, 1.0, ref best, ref bx, ref by, ref btw, ref bth);

            if (best < threshold)
            {
                double[] scales = new double[] { 0.85, 1.15, 1.3, 0.75 };
                for (int i = 0; i < scales.Length; i++)
                    ScanScale(px, w, h, tpl.Gray, tpl.W, tpl.H, scales[i], ref best, ref bx, ref by, ref btw, ref bth);
            }

            if (best > -1.5)
            {
                hit.Score = Math.Max(0.0, Math.Min(1.0, best));
                hit.Found = hit.Score >= threshold;
                hit.Scale = (double)btw / tpl.W;
                hit.X = r.X + bx + btw / 2;
                hit.Y = r.Y + by + bth / 2;
            }
            return hit;
        }

        private static void ScanScale(double[] px, int w, int h, double[] tplGray, int tw0, int th0, double scale,
                                      ref double best, ref int bx, ref int by, ref int btw, ref int bth)
        {
            int tw = (int)Math.Round(tw0 * scale);
            int th = (int)Math.Round(th0 * scale);
            if (tw < 6 || th < 4 || tw > w || th > h) return;

            double[] tpl = (Math.Abs(scale - 1.0) < 0.001)
                ? ZeroMean(tplGray)
                : ResampleTemplate(tplGray, tw0, th0, tw, th);
            double tstd = TplStd(tpl);
            if (tstd < 1e-6) return;
            double n = tw * th;

            for (int y = 0; y <= h - th; y++)
            {
                for (int x = 0; x <= w - tw; x++)
                {
                    double dot = 0, sum = 0, sum2 = 0;
                    for (int i = 0; i < th; i++)
                    {
                        int row = (y + i) * w + x;
                        int trow = i * tw;
                        for (int j = 0; j < tw; j++)
                        {
                            double v = px[row + j];
                            dot += v * tpl[trow + j];
                            sum += v;
                            sum2 += v * v;
                        }
                    }
                    double mean = sum / n;
                    double var = sum2 - n * mean * mean;
                    if (var <= 1e-3) continue;
                    double score = dot / (Math.Sqrt(var) * tstd);
                    if (score > best) { best = score; bx = x; by = y; btw = tw; bth = th; }
                }
            }
        }

        private static double[] ZeroMean(double[] src)
        {
            double sum = 0;
            for (int i = 0; i < src.Length; i++) sum += src[i];
            double mean = sum / src.Length;
            double[] dst = new double[src.Length];
            for (int i = 0; i < src.Length; i++) dst[i] = src[i] - mean;
            return dst;
        }

        private static double RectSum(double[] integ, int w, int x, int y, int tw, int th)
        {
            int stride = w + 1;
            return integ[(y + th) * stride + x + tw] - integ[y * stride + x + tw] - integ[(y + th) * stride + x] + integ[y * stride + x];
        }

        // ---------- 多尺度匹配（“选择稍后”按钮） ----------
        private static double _waitScale = -1;
        // 尺度档位加密到 0.05 一档：模板与实际画面差 5% 时也能对上
        private static readonly double[] Scales = new double[] { 1.0, 0.95, 1.05, 0.9, 1.1, 0.85, 1.15, 0.8, 1.2, 0.75, 1.25, 0.7, 1.3, 0.65, 1.4, 0.6, 1.5 };

        public static Hit MatchWait(Bitmap frame, double threshold)
        {
            Hit hit = new Hit();
            int w, h;
            double[] gray = ToGray(frame, out w, out h);
            if (w < 64 || h < 48 || WaitTpl == null) return hit;

            double k = Math.Max(1.0, Math.Ceiling(w / 960.0));
            int cw, ch;
            float[] coarse = Downsample(gray, w, h, k, out cw, out ch);
            double[] cInteg, cInteg2;
            BuildIntegrals(coarse, cw, ch, out cInteg, out cInteg2);
            float[] full = ToFloat(gray);

            List<double> order = new List<double>();
            if (_waitScale > 0) order.Add(_waitScale);
            for (int i = 0; i < Scales.Length; i++)
                if (Math.Abs(Scales[i] - _waitScale) > 0.001) order.Add(Scales[i]);

            double best = -2; int bx = 0, by = 0; double bscale = 1;
            bool debug = Environment.GetEnvironmentVariable("AOE_DEBUG") == "1";

            for (int si = 0; si < order.Count; si++)
            {
                double s = order[si];
                int fx, fy;
                double fs = EvalScale(coarse, cw, ch, cInteg, cInteg2, full, w, h, k, s, out fx, out fy);
                if (debug && fs > -2) Console.WriteLine("  s={0:0.00} score={1:0.000} at {2},{3}", s, fs, fx, fy);
                if (fs > best) { best = fs; bx = fx; by = fy; bscale = s; }
                if (best >= 0.93) break;
            }

            // 尺度精修（±4%）
            if (best > 0.40 && best < 0.93)
            {
                double[] fine = new double[] { bscale * 0.96, bscale * 0.98, bscale * 1.02, bscale * 1.04 };
                for (int i = 0; i < fine.Length; i++)
                {
                    int fx, fy;
                    double fs = EvalScale(coarse, cw, ch, cInteg, cInteg2, full, w, h, k, fine[i], out fx, out fy);
                    if (debug) Console.WriteLine("  fine s={0:0.00} score={1:0.000} at {2},{3}", fine[i], fs, fx, fy);
                    if (fs > best) { best = fs; bx = fx; by = fy; bscale = fine[i]; }
                }
            }

            // 局部精细打磨：位置 ±(3k+4) 像素、尺度 ±10%
            if (best > 0.30) Polish(full, w, h, k, ref best, ref bx, ref by, ref bscale);

            if (best > -1.5)
            {
                int tw = (int)Math.Round(WaitTpl.W * bscale);
                int th = (int)Math.Round(WaitTpl.H * bscale);
                hit.Score = Math.Max(0.0, Math.Min(1.0, best));
                hit.Found = hit.Score >= threshold;
                hit.Scale = bscale;
                hit.X = bx + tw / 2;
                hit.Y = by + th / 2;
                if (hit.Found) _waitScale = bscale;
            }
            return hit;
        }

        // 在给定尺度上做一次“粗搜索 + 全分辨率局部精修”，返回全分辨率左上角
        private static double EvalScale(float[] coarse, int cw, int ch, double[] cInteg, double[] cInteg2,
                                        float[] full, int w, int h, double k, double s,
                                        out int fx, out int fy)
        {
            fx = 0; fy = 0;
            int twc = (int)Math.Round(WaitTpl.W * s / k);
            int thc = (int)Math.Round(WaitTpl.H * s / k);
            if (twc < 10 || thc < 6 || twc > cw || thc > ch) return -2;

            double[] tmc = ResampleTemplate(WaitTpl.Gray, WaitTpl.W, WaitTpl.H, twc, thc);
            double tstdc = TplStd(tmc);
            if (tstdc < 1e-6) return -2;

            int cbx, cby;
            double cs = NccScan(coarse, cw, ch, cInteg, cInteg2, tmc, twc, thc, tstdc,
                                0, cw - twc, 0, ch - thc, 2, out cbx, out cby);
            if (cs < 0.20) return -2;

            // 在粗位置附近用步长 1 再扫一遍
            int lx0 = Math.Max(0, cbx - 3), lx1 = Math.Min(cw - twc, cbx + 3);
            int ly0 = Math.Max(0, cby - 3), ly1 = Math.Min(ch - thc, cby + 3);
            int lbx, lby;
            double ls = NccScan(coarse, cw, ch, cInteg, cInteg2, tmc, twc, thc, tstdc,
                                lx0, lx1, ly0, ly1, 1, out lbx, out lby);
            if (ls > cs) { cs = ls; cbx = lbx; cby = lby; }

            int tw = (int)Math.Round(WaitTpl.W * s);
            int th = (int)Math.Round(WaitTpl.H * s);
            if (tw < 10 || th < 6 || tw > w || th > h) return -2;

            double[] tmf = ResampleTemplate(WaitTpl.Gray, WaitTpl.W, WaitTpl.H, tw, th);
            double tstdf = TplStd(tmf);
            if (tstdf < 1e-6) return -2;

            int margin = (int)Math.Ceiling(2 * k) + 2;
            int cx = (int)Math.Round(cbx * k);
            int cy = (int)Math.Round(cby * k);
            int x0 = Math.Max(0, cx - margin), x1 = Math.Min(w - tw, cx + margin);
            int y0 = Math.Max(0, cy - margin), y1 = Math.Min(h - th, cy + margin);
            if (x1 < x0) x1 = x0;
            if (y1 < y0) y1 = y0;

            double fs = NccScanRegion(full, w, h, tmf, tw, th, tstdf, x0, x1, y0, y1, out fx, out fy);
            return fs;
        }


        // 在最佳位置附近做位置/尺度的精细打磨
        private static void Polish(float[] full, int w, int h, double k, ref double best, ref int bx, ref int by, ref double bscale)
        {
            int m = (int)Math.Round(3 * k) + 4;
            double s0 = bscale * 0.90, s1 = bscale * 1.10;
            for (int i = 0; i <= 8; i++)
            {
                double s = s0 + (s1 - s0) * i / 8.0;
                int tw = (int)Math.Round(WaitTpl.W * s);
                int th = (int)Math.Round(WaitTpl.H * s);
                if (tw < 10 || th < 6 || tw > w || th > h) continue;
                double[] tm = ResampleTemplate(WaitTpl.Gray, WaitTpl.W, WaitTpl.H, tw, th);
                double tstd = TplStd(tm);
                if (tstd < 1e-6) continue;
                int x0 = Math.Max(0, bx - m), x1 = Math.Min(w - tw, bx + m);
                int y0 = Math.Max(0, by - m), y1 = Math.Min(h - th, by + m);
                if (x1 < x0) x1 = x0;
                if (y1 < y0) y1 = y0;
                int fx, fy;
                double fs = NccScanRegion(full, w, h, tm, tw, th, tstd, x0, x1, y0, y1, out fx, out fy);
                if (fs > best) { best = fs; bx = fx; by = fy; bscale = s; }
            }
        }

        private static void BuildIntegrals(float[] img, int w, int h, out double[] integ, out double[] integ2)
        {
            int stride = w + 1;
            integ = new double[stride * (h + 1)];
            integ2 = new double[stride * (h + 1)];
            for (int y = 0; y < h; y++)
            {
                double rs = 0, rs2 = 0;
                for (int x = 0; x < w; x++)
                {
                    double v = img[y * w + x];
                    rs += v; rs2 += v * v;
                    integ[(y + 1) * stride + x + 1] = integ[y * stride + x + 1] + rs;
                    integ2[(y + 1) * stride + x + 1] = integ2[y * stride + x + 1] + rs2;
                }
            }
        }

        private static double TplStd(double[] tpl)
        {
            double acc = 0;
            for (int i = 0; i < tpl.Length; i++) acc += tpl[i] * tpl[i];
            return Math.Sqrt(acc);
        }

        private static float[] ToFloat(double[] a)
        {
            float[] r = new float[a.Length];
            for (int i = 0; i < a.Length; i++) r[i] = (float)a[i];
            return r;
        }

        // 粗搜索：整幅扫描（积分图取方差，模板直接乘）
        private static double NccScan(float[] img, int iw, int ih, double[] integ, double[] integ2,
                                      double[] tpl, int tw, int th, double tstd,
                                      int x0, int x1, int y0, int y1, int step, out int bx, out int by)
        {
            bx = x0; by = y0;
            double best = -2;
            double n = tw * th;
            for (int y = y0; y <= y1; y += step)
            {
                for (int x = x0; x <= x1; x += step)
                {
                    double dot = 0;
                    for (int i = 0; i < th; i++)
                    {
                        int row = (y + i) * iw + x;
                        int trow = i * tw;
                        for (int j = 0; j < tw; j++) dot += img[row + j] * tpl[trow + j];
                    }
                    double sum = RectSum(integ, iw, x, y, tw, th);
                    double sum2 = RectSum(integ2, iw, x, y, tw, th);
                    double mean = sum / n;
                    double var = sum2 - n * mean * mean;
                    if (var <= 1e-3) continue;
                    double sc = dot / (Math.Sqrt(var) * tstd);
                    if (sc > best) { best = sc; bx = x; by = y; }
                }
            }
            return best;
        }

        // 精修：小窗口内扫描，方差就地计算
        private static double NccScanRegion(float[] img, int iw, int ih, double[] tpl, int tw, int th, double tstd,
                                            int x0, int x1, int y0, int y1, out int bx, out int by)
        {
            bx = x0; by = y0;
            double best = -2;
            double n = tw * th;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    double dot = 0, sum = 0, sum2 = 0;
                    for (int i = 0; i < th; i++)
                    {
                        int row = (y + i) * iw + x;
                        int trow = i * tw;
                        for (int j = 0; j < tw; j++)
                        {
                            double v = img[row + j];
                            dot += v * tpl[trow + j];
                            sum += v; sum2 += v * v;
                        }
                    }
                    double mean = sum / n;
                    double var = sum2 - n * mean * mean;
                    if (var <= 1e-3) continue;
                    double sc = dot / (Math.Sqrt(var) * tstd);
                    if (sc > best) { best = sc; bx = x; by = y; }
                }
            }
            return best;
        }

        private static float[] Downsample(double[] src, int w, int h, double k, out int ow, out int oh)
        {
            ow = (int)Math.Round(w / k);
            oh = (int)Math.Round(h / k);
            if (ow < 1) ow = 1;
            if (oh < 1) oh = 1;
            float[] dst = new float[ow * oh];
            for (int y = 0; y < oh; y++)
            {
                int y0 = (int)(y * k), y1 = (int)Math.Min(h, (y + 1) * k);
                if (y1 <= y0) y1 = Math.Min(h, y0 + 1);
                for (int x = 0; x < ow; x++)
                {
                    int x0 = (int)(x * k), x1 = (int)Math.Min(w, (x + 1) * k);
                    if (x1 <= x0) x1 = Math.Min(w, x0 + 1);
                    double acc = 0; int cnt = 0;
                    for (int yy = y0; yy < y1; yy++)
                        for (int xx = x0; xx < x1; xx++) { acc += src[yy * w + xx]; cnt++; }
                    dst[y * ow + x] = (float)(acc / Math.Max(1, cnt));
                }
            }
            return dst;
        }

        // 双线性缩放模板并去均值
        private static double[] ResampleTemplate(double[] src, int sw, int sh, int dw, int dh)
        {
            double[] dst = new double[dw * dh];
            double sum = 0;
            for (int y = 0; y < dh; y++)
            {
                double sy = (y + 0.5) * sh / dh - 0.5;
                int y0 = (int)Math.Floor(sy); double fy = sy - y0;
                int y0c = Math.Max(0, Math.Min(sh - 1, y0));
                int y1c = Math.Max(0, Math.Min(sh - 1, y0 + 1));
                for (int x = 0; x < dw; x++)
                {
                    double sx = (x + 0.5) * sw / dw - 0.5;
                    int x0 = (int)Math.Floor(sx); double fx = sx - x0;
                    int x0c = Math.Max(0, Math.Min(sw - 1, x0));
                    int x1c = Math.Max(0, Math.Min(sw - 1, x0 + 1));
                    double v00 = src[y0c * sw + x0c];
                    double v10 = src[y0c * sw + x1c];
                    double v01 = src[y1c * sw + x0c];
                    double v11 = src[y1c * sw + x1c];
                    double v = (1 - fy) * ((1 - fx) * v00 + fx * v10) + fy * ((1 - fx) * v01 + fx * v11);
                    dst[y * dw + x] = v;
                    sum += v;
                }
            }
            double mean = sum / dst.Length;
            for (int i = 0; i < dst.Length; i++) dst[i] -= mean;
            return dst;
        }

        public static bool PanelPresent(Bitmap frame, double threshold, out double score)
        {
            Hit h = Match(frame, PanelTpl, PanelRoi, threshold);
            score = h.Score;
            return h.Found;
        }

        public static double Median(double[] a)
        {
            double[] c = (double[])a.Clone();
            Array.Sort(c);
            int n = c.Length;
            if (n == 0) return 0;
            return (n % 2 == 1) ? c[n / 2] : (c[n / 2 - 1] + c[n / 2]) / 2.0;
        }
    }

    // ---------- 终端进度显示 ----------
    public static class Status
    {
        private static readonly object Sync = new object();
        private static DateTime _stageStart = DateTime.Now;
        private static int _stepNo = 0;
        private static int _stepTotal = 0;

        public static void Steps(int total) { _stepTotal = total; _stepNo = 0; }

        public static void Begin(string text)
        {
            lock (Sync)
            {
                _stepNo++;
                _stageStart = DateTime.Now;
                string prefix = _stepTotal > 0 ? string.Format("[{0}/{1}] ", _stepNo, _stepTotal) : "";
                Log.C(ConsoleColor.Cyan, "▶ " + prefix + text);
            }
        }

        public static void Tick(string detail)
        {
            lock (Sync)
            {
                double s = (DateTime.Now - _stageStart).TotalSeconds;
                Log.C(ConsoleColor.DarkGray, string.Format("      … {0}（已 {1:0} 秒）", detail, s));
            }
        }

        public static void Ok(string text)
        {
            lock (Sync)
            {
                double s = (DateTime.Now - _stageStart).TotalSeconds;
                Log.C(ConsoleColor.Green, string.Format("  ✓ {0}（{1:0.0} 秒）", text, s));
            }
        }

        public static void Fail(string text)
        {
            lock (Sync) { Log.C(ConsoleColor.Red, "  ✗ " + text); }
        }

        public static void Warn(string text)
        {
            lock (Sync) { Log.C(ConsoleColor.Yellow, "  ! " + text); }
        }
    }

    // ---------- 机器人 ----------
    public class Bot
    {
        // 「选择稍后」点击后重新检测的间隔（秒）
        private const int WaitRepeatDelaySec = 5;

        private IntPtr _hwnd;
        private Config _cfg;
        private string _frameDir;
        private int _round;

        public Bot(IntPtr hwnd, Config cfg, string frameDir)
        {
            _hwnd = hwnd;
            _cfg = cfg;
            _frameDir = frameDir;
        }

        public void EnsureFrameDir()
        {
            if (_cfg.SaveDebugFrames != 1) return;
            try { if (!Directory.Exists(_frameDir)) Directory.CreateDirectory(_frameDir); }
            catch { }
        }

        public Bitmap Shot() { return ShotImpl(false); }

        public Bitmap ShotNative() { return ShotImpl(true); }

        private Bitmap ShotImpl(bool native)
        {
            Program.Activate(_hwnd);
            int cw, ch;
            Bitmap b = native ? Vision.CaptureNative(_hwnd, out cw, out ch) : Vision.Capture(_hwnd, out cw, out ch);
            if (b != null && _cfg.SaveDebugFrames == 1)
            {
                EnsureFrameDir();
                string name = string.Format("r{0:000}_{1:HHmmss_fff}.png", _round, DateTime.Now);
                try { b.Save(Path.Combine(_frameDir, name), ImageFormat.Png); } catch { }
            }
            return b;
        }

        public bool DetectBriefing(out Hit hit)
        {
            hit = null;
            Bitmap b = Shot();
            if (b == null) return false;
            try
            {
                hit = Vision.Match(b, Vision.BriefingTpl, Vision.BriefingRoi, _cfg.MatchThreshold);
                return hit.Found;
            }
            finally { b.Dispose(); }
        }

        public bool DetectVictory(out Hit hit)
        {
            hit = null;
            Bitmap b = Shot();
            if (b == null) return false;
            try
            {
                hit = Vision.Match(b, Vision.VictoryTpl, Vision.VictoryRoi, _cfg.MatchThreshold);
                return hit.Found;
            }
            finally { b.Dispose(); }
        }

        public bool DetectReplay(out Hit hit)
        {
            hit = null;
            Bitmap b = Shot();
            if (b == null) return false;
            try
            {
                hit = Vision.Match(b, Vision.ReplayTpl, Vision.ReplayRoi, _cfg.MatchThreshold);
                return hit.Found;
            }
            finally { b.Dispose(); }
        }

        public bool DetectPanel(out double score)
        {
            score = 0;
            Bitmap b = Shot();
            if (b == null) return false;
            try { return Vision.PanelPresent(b, _cfg.MatchThreshold, out score); }
            finally { b.Dispose(); }
        }

        // 新：检测“选择稍后”按钮（原生分辨率，多尺度）
        private DateTime _lastMissSave = DateTime.MinValue;
        private int _missSaveCount = 0;

        public bool DetectWait(out Hit hit)
        {
            hit = null;
            Bitmap b = ShotNative();
            if (b == null) return false;
            try
            {
                hit = Vision.MatchWait(b, 0.85);   // 「选择稍后」专用阈值：真实画面 0.97~1.00，关卡背景噪声 ≤0.77
                // 差一点但没到阈值：存一张图，方便事后按真实画面重裁模板
                if (hit != null && !hit.Found && hit.Score >= 0.55 && _missSaveCount < 20
                    && (DateTime.Now - _lastMissSave).TotalSeconds >= 8)
                {
                    try
                    {
                        if (!Directory.Exists(_frameDir)) Directory.CreateDirectory(_frameDir);
                        b.Save(Path.Combine(_frameDir, string.Format("miss_{0:HHmmss_fff}_{1:0.00}.png", DateTime.Now, hit.Score)), ImageFormat.Png);
                        _lastMissSave = DateTime.Now;
                        _missSaveCount++;
                    }
                    catch { }
                }
                return hit.Found;
            }
            finally { b.Dispose(); }
        }

        public void Run()
        {
            if (_cfg.StartDelaySec > 0)
            {
                int left = _cfg.StartDelaySec;
                Status.Begin(string.Format("倒计时 {0} 秒后开始（请切到游戏窗口）", left));
                while (left > 0 && !Program.Stop)
                {
                    left--;
                    if (left % 5 == 0 || left <= 3) Status.Tick(left + " 秒后开始…");
                    Sleep(1000);
                }
                Status.Ok("倒计时结束");
            }
            Status.Steps(0);
            while (!Program.Stop)
            {
                if (_cfg.MaxRounds > 0 && _round >= _cfg.MaxRounds)
                {
                    Status.Ok(string.Format("已完成设定的 {0} 轮，脚本结束。", _cfg.MaxRounds));
                    return;
                }
                _round++;
                Log.C(ConsoleColor.White, "----------------------------------------------------------");
                Status.Begin(string.Format("第 {0} 轮 开始", _round));
                Program.CheckResolution(_hwnd);
                try { RunOneRound(); }
                catch (Exception ex)
                {
                    Status.Fail("本轮异常：" + ex.Message);
                    Sleep(2000);
                }
            }
        }

        public void RunOneRound()
        {
            Status.Begin(string.Format("第 {0} 轮 · 等待「开始游戏」按钮（最长 {1} 秒）", _round, _cfg.BriefingTimeoutSec));
            DateTime deadline = DateTime.UtcNow.AddSeconds(_cfg.BriefingTimeoutSec);
            DateTime lastTick = DateTime.UtcNow;
            Hit brief = null;
            while (!Program.Stop)
            {
                Program.WaitIfPaused();
                if (Program.Stop) return;
                if (DetectBriefing(out brief)) break;
                if (DateTime.UtcNow > deadline)
                {
                    Status.Warn("时间到了还没看到按钮，继续等。请确认游戏停在关卡界面（不是主菜单/载入中）。");
                    deadline = DateTime.UtcNow.AddSeconds(_cfg.BriefingTimeoutSec);
                }
                if ((DateTime.UtcNow - lastTick).TotalSeconds >= 5)
                {
                    lastTick = DateTime.UtcNow;
                    Status.Tick("还没看到「开始游戏」按钮");
                }
                Sleep(_cfg.PollMs);
            }
            if (Program.Stop) return;
            Status.Ok(string.Format("识别到「开始游戏」（相似度 {0:0.00}）", brief.Score));

            if (!StartMission())
            {
                Status.Fail("未能确认进入对局，本轮中止，5 秒后重来");
                Sleep(5000);
                return;
            }

            Status.Begin(string.Format("第 {0} 轮 · 对局等待 {1} 秒（期间不检测胜利）", _round, _cfg.BattleWaitSec));
            int elapsed = 0;
            int total = Math.Max(0, _cfg.BattleWaitSec) * 1000;   // 秒 → 毫秒
            while (elapsed < total && !Program.Stop)
            {
                Program.WaitIfPaused();
                Sleep(1000);
                elapsed += 1000;
                if (elapsed % 10000 == 0)
                    Status.Tick(string.Format("已 {0} 秒 / 共 {1} 秒", elapsed / 1000, total / 1000));
            }
            if (Program.Stop) return;
            Status.Ok("对局等待结束，开始找「胜利！」");

            Status.Begin(string.Format("第 {0} 轮 · 检测「胜利！」（最长 {1} 秒）", _round, _cfg.VictoryTimeoutSec));
            DateTime vicDeadline = DateTime.UtcNow.AddSeconds(_cfg.VictoryTimeoutSec);
            DateTime lastWarn = DateTime.UtcNow;
            Hit vic = null;
            lastTick = DateTime.UtcNow;
            while (!Program.Stop)
            {
                Program.WaitIfPaused();
                if (Program.Stop) return;
                if (DetectVictory(out vic))
                {
                    Status.Ok(string.Format("检测到「胜利！」（相似度 {0:0.00}），按回车", vic.Score));
                    break;
                }
                if (DateTime.UtcNow > vicDeadline && (DateTime.UtcNow - lastWarn).TotalSeconds > 30)
                {
                    lastWarn = DateTime.UtcNow;
                    Status.Warn("还没出现「胜利！」，可能战斗失败或卡住，继续等（F10 退出）");
                }
                if ((DateTime.UtcNow - lastTick).TotalSeconds >= 5)
                {
                    lastTick = DateTime.UtcNow;
                    Status.Tick("还没看到「胜利！」画面");
                }
                Sleep(_cfg.PollMs);
            }
            if (Program.Stop) return;

            // ① 先按原版逻辑：回车
            Status.Ok("按回车（原版逻辑）");
            PressEnter();
            Sleep(1500);

            Status.Begin(string.Format("第 {0} 轮 · 等待回到关卡界面", _round));
            DateTime back = DateTime.UtcNow.AddSeconds(_cfg.BriefingTimeoutSec);
            int extra = 0;
            bool replayClicked = false;
            lastTick = DateTime.UtcNow;
            while (!Program.Stop)
            {
                Program.WaitIfPaused();
                if (Program.Stop) return;
                Hit h;
                if (DetectBriefing(out h))
                {
                    Status.Ok("已回到关卡界面，本轮完成");
                    return;
                }
                Hit vh;
                if (DetectVictory(out vh))
                {
                    // ② 仍在结算画面：原版做法是再补按两次回车
                    if (extra < 2)
                    {
                        extra++;
                        Status.Tick(string.Format("回车后仍停在结算画面，再按一次回车（第 {0} 次）", extra));
                        PressEnter();
                        Sleep(1200);
                        continue;
                    }
                    // ③ 回车不管用：改点「再玩一次」
                    if (!replayClicked)
                    {
                        Hit rp;
                        if (DetectReplay(out rp))
                        {
                            Status.Ok(string.Format("回车无效，改点「再玩一次」（相似度 {0:0.00}）", rp.Score));
                            Click(rp.X, rp.Y);
                            replayClicked = true;
                            Sleep(1800);
                            continue;
                        }
                        Status.Warn("没找到「再玩一次」，继续等待");
                        replayClicked = true;
                    }
                }
                if ((DateTime.UtcNow - lastTick).TotalSeconds >= 5)
                {
                    lastTick = DateTime.UtcNow;
                    Status.Tick("还没回到关卡界面");
                }
                Sleep(_cfg.PollMs);
                if (DateTime.UtcNow > back)
                {
                    Status.Warn("等待关卡界面超时，进入下一轮继续等");
                    return;
                }
            }
        }

        // 点击「开始游戏」-> 检测并点击「选择稍后」-> 5 秒后重检 -> 最多重复 2 次
        public bool StartMission()
        {
            Status.Begin(string.Format("第 {0} 轮 · 点「开始游戏」并等待「选择稍后」", _round));
            DateTime deadline = DateTime.UtcNow.AddSeconds(_cfg.PanelWaitSec);
            int startClicks = 0;
            DateTime lastStart = DateTime.MinValue;
            int waitClicks = 0;
            int waitMax = 2;   // 最多重复 2 次
            double bestWaitScore = 0;
            DateTime lastTick = DateTime.UtcNow;

            while (!Program.Stop)
            {
                Program.WaitIfPaused();
                if (Program.Stop) return false;
                if (DateTime.UtcNow >= deadline) break;

                // 1) 「选择稍后」
                if (waitClicks <= waitMax)
                {
                    Hit wh;
                    if (DetectWait(out wh))
                    {
                        Status.Ok(string.Format("识别到「选择稍后」（相似度 {0:0.00}，尺度 {1:0.00}x），第 {2} 次点击",
                            wh.Score, wh.Scale, waitClicks + 1));
                        ClickNative(wh.X, wh.Y);
                        waitClicks++;
                        if (waitClicks <= waitMax)
                        {
                            Status.Begin(string.Format("第 {0} 轮 · {1} 秒后重检「选择稍后」，仍在就再点一次", _round, WaitRepeatDelaySec));
                            Sleep(WaitRepeatDelaySec * 1000);
                            continue;
                        }
                        Status.Ok(string.Format("已点击 {0} 次「选择稍后」（达上限），进入下一步", waitClicks));
                        Sleep(800);
                        return true;
                    }

                    // 已经点过：这次没检测到，说明面板已经消失 → 直接进入下一步，不再等
                    if (waitClicks > 0)
                    {
                        Status.Ok(string.Format("第 {0} 次点击后已看不到「选择稍后」，视为已进入对局", waitClicks));
                        Sleep(500);
                        return true;
                    }
                    if (wh != null && wh.Score > bestWaitScore) bestWaitScore = wh.Score;
                }

                // 2) 「开始游戏」
                Hit bh;
                if (DetectBriefing(out bh))
                {
                    if (startClicks > 0 && (DateTime.UtcNow - lastStart).TotalSeconds < 6)
                    {
                        Sleep(500);
                        continue;
                    }
                    if (startClicks >= 3)
                    {
                        Status.Fail("已连续点 3 次「开始游戏」仍停在关卡界面");
                        Log.W("        多半是权限/环境问题：请把 exe 放到普通目录（如桌面），或用「启动_管理员.bat」运行。");
                        Sleep(1000);
                        return false;
                    }
                    if (startClicks == 0)
                        Status.Ok(string.Format("识别到「开始游戏」（相似度 {0:0.00}），点击", bh.Score));
                    else
                        Status.Tick(string.Format("画面仍停在关卡界面，第 {0} 次点「开始游戏」", startClicks + 1));
                    Click(bh.X, bh.Y);
                    startClicks++;
                    lastStart = DateTime.UtcNow;
                    Sleep(1200);
                    continue;
                }

                if ((DateTime.UtcNow - lastTick).TotalSeconds >= 5)
                {
                    lastTick = DateTime.UtcNow;
                    if (bestWaitScore > 0)
                        Status.Tick(string.Format("还没看到「选择稍后」，当前最高相似度 {0:0.00}（阈值 {1:0.00}）",
                            bestWaitScore, Math.Max(_cfg.MatchThreshold, 0.78)));
                    else
                        Status.Tick("等待面板出现…");
                }
                Sleep(500);
            }

            if (waitClicks > 0)
            {
                Status.Ok(string.Format("已点击 {0} 次「选择稍后」，进入对局", waitClicks));
                return true;
            }
            Status.Warn(string.Format("没检测到「选择稍后」（本阶段最高相似度 {0:0.00}，阈值 {1:0.00}），按已进入对局继续",
                bestWaitScore, Math.Max(_cfg.MatchThreshold, 0.78)));
            return true;
        }

        public void SleepWithProgress(int ms)
        {
            int step = 200;
            int left = ms;
            while (left > 0 && !Program.Stop)
            {
                int s = Math.Min(step, left);
                Thread.Sleep(s);
                left -= s;
                Program.WaitIfPaused();
            }
        }

        public void Sleep(int ms)
        {
            int left = ms;
            while (left > 0 && !Program.Stop)
            {
                int s = Math.Min(100, left);
                Thread.Sleep(s);
                left -= s;
            }
        }

        // 640x480 参考系坐标点击
        public void Click(int x, int y)
        {
            // 用“刚才那张截图”的尺寸换算，避免窗口在截图与点击之间移动导致偏差
            int cw = Vision.LastW, ch = Vision.LastH;
            if (cw <= 0 || ch <= 0) GetClientSize(out cw, out ch);
            int nx = (int)Math.Round(x * (double)cw / Vision.RefW);
            int ny = (int)Math.Round(y * (double)ch / Vision.RefH);
            Log.C(ConsoleColor.Gray, string.Format("      目标：640空间({0},{1}) → 客户区({2},{3})，客户区尺寸 {4}x{5}", x, y, nx, ny, cw, ch));
            ClickNative(nx, ny);
        }

        private void GetClientSize(out int w, out int h)
        {
            POINT p0 = new POINT(0, 0);
            Native.ClientToScreen(_hwnd, ref p0);
            RECT rc; Native.GetClientRect(_hwnd, out rc);
            POINT p1 = new POINT(rc.Right, rc.Bottom);
            Native.ClientToScreen(_hwnd, ref p1);
            w = p1.X - p0.X;
            h = p1.Y - p0.Y;
        }

        // 原生客户区坐标点击（坐标基准 = 最近一次截图）
        public void ClickNative(int x, int y)
        {
            if (!Program.Activate(_hwnd))
                Log.W("警告：游戏窗口未能切到前台，点击可能无效。");

            POINT p0 = Vision.LastNativeOrigin;
            int w = Vision.LastNativeW, h = Vision.LastNativeH;
            if (w <= 0 || h <= 0)
            {
                p0 = new POINT(0, 0);
                Native.ClientToScreen(_hwnd, ref p0);
                RECT rc; Native.GetClientRect(_hwnd, out rc);
                POINT p2 = new POINT(rc.Right, rc.Bottom);
                Native.ClientToScreen(_hwnd, ref p2);
                w = p2.X - p0.X; h = p2.Y - p0.Y;
            }

            int sx = p0.X + x;
            int sy = p0.Y + y;
            Log.C(ConsoleColor.Gray, string.Format("      点击屏幕 ({0},{1})，基准原点 ({2},{3})，尺寸 {4}x{5}，前台 0x{6:X}",
                sx, sy, p0.X, p0.Y, w, h, Native.GetForegroundWindow().ToInt64()));

            Native.SetCursorPos(sx, sy);
            Thread.Sleep(80);
            POINT cur;
            Native.GetCursorPos(out cur);
            bool onTarget = Math.Abs(cur.X - sx) <= 2 && Math.Abs(cur.Y - sy) <= 2;
            Log.C(onTarget ? ConsoleColor.Gray : ConsoleColor.Red,
                string.Format("      光标实际位置 ({0},{1})  {2}", cur.X, cur.Y, onTarget ? "OK" : "!! 没到目标位置"));

            if (!Input.Mouse(Input.LEFTUP, 0, 0))
                Log.C(ConsoleColor.Red, "  ! 点击失败：SendInput 返回 0（被系统拒绝），请用管理员身份运行");
            Thread.Sleep(50);
            bool moved = Input.Mouse(Input.MOVE | Input.ABSOLUTE | Input.VIRTUALDESK, sx, sy);
            if (!moved) Log.C(ConsoleColor.Red, "  ! 鼠标移动失败：SendInput 返回 0");
            Thread.Sleep(70);
            Input.Mouse(Input.MOVE | Input.ABSOLUTE | Input.VIRTUALDESK, sx, sy);
            Thread.Sleep(100);
            if (!Input.Mouse(Input.LEFTDOWN, 0, 0))
                Log.C(ConsoleColor.Red, "  ! 左键按下失败：SendInput 返回 0");
            Thread.Sleep(110);
            Input.Mouse(Input.LEFTUP, 0, 0);
            Thread.Sleep(60);
            Input.Mouse(Input.LEFTUP, 0, 0);
            Thread.Sleep(150);
            Input.Mouse(Input.MOVE | Input.ABSOLUTE | Input.VIRTUALDESK, p0.X + w - 5, p0.Y + h - 5);
        }

        public void PressEnter()
        {
            if (Environment.GetEnvironmentVariable("AOE_NOCLICK") == "1")
            {
                Log.C(ConsoleColor.Magenta, "        [测试模式] 本应按回车，已跳过");
                return;
            }
            if (!Program.Activate(_hwnd))
                Log.W("警告：游戏窗口未能切到前台，回车可能无效。");
            Thread.Sleep(150);
            Input.Key(Native.VK_RETURN, 28, false);
            Thread.Sleep(70);
            Input.Key(Native.VK_RETURN, 28, true);
        }
    }

    // ---------- 热键窗口 ----------
    public class HotkeyForm : Form
    {
        public HotkeyForm()
        {
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-32000, -32000);
            Size = new Size(1, 1);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.RegisterHotKey(Handle, 1, Native.MOD_NOREPEAT, Native.VK_F9);
            Native.RegisterHotKey(Handle, 2, Native.MOD_NOREPEAT, Native.VK_F10);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == 1) Program.TogglePause();
                else if (id == 2) { Program._quickExit = true; Program.RequestExit(); }
            }
            base.WndProc(ref m);
        }
    }

    // ---------- 主程序 ----------
    public static class Program
    {
        public static volatile bool Stop;
        public static volatile bool _paused;
        public static HotkeyForm _form;
        public static volatile bool _quickExit;

        public static void TogglePause()
        {
            _paused = !_paused;
            Log.W(_paused ? "已暂停（F9 继续）。" : "已继续。");
        }

        public static void WaitIfPaused()
        {
            while (_paused && !Stop) Thread.Sleep(100);
        }

        public static void RequestExit()
        {
            Stop = true;
            if (_form != null && _form.IsHandleCreated)
            {
                try { _form.BeginInvoke(new Action(delegate { Application.Exit(); })); }
                catch { }
            }
        }

        [STAThread]
        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = new UTF8Encoding(false); } catch { }
            try { Console.Title = "AoE4 自动循环脚本"; } catch { }
            Banner();

            AppDomain.CurrentDomain.UnhandledException += OnUnhandled;
            Application.ThreadException += OnThreadException;
            try { Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException); } catch { }

            try
            {
                return RunCore(args);
            }
            catch (Exception ex)
            {
                Log.C(ConsoleColor.Red, "【致命错误】" + ex.GetType().Name + "：" + ex.Message);
                if (!string.IsNullOrEmpty(ex.StackTrace)) Log.C(ConsoleColor.DarkRed, ex.StackTrace);
                Pause("按任意键退出……");
                return 1;
            }
        }

        private static void OnUnhandled(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            Log.C(ConsoleColor.Red, "【未处理异常】" + (ex == null ? "未知错误" : ex.GetType().Name + "：" + ex.Message));
            if (ex != null && !string.IsNullOrEmpty(ex.StackTrace)) Log.C(ConsoleColor.DarkRed, ex.StackTrace);
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            Log.C(ConsoleColor.Red, "【界面线程异常】" + e.Exception.GetType().Name + "：" + e.Exception.Message);
        }

        // 运行数据目录：优先 %LOCALAPPDATA%\AoE4Auto，不可写则退到 exe 目录下的 AoE4Auto_Data
        public static string GetDataDir(string exeDir)
        {
            string[] candidates = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AoE4Auto"),
                Path.Combine(exeDir, "AoE4Auto_Data"),
                Path.Combine(Path.GetTempPath(), "AoE4Auto")
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                if (CanWrite(candidates[i])) return candidates[i];
            }
            return exeDir;
        }

        // 真的写一个探针文件，确认这个目录能写（有 ACL/占用问题时提前发现）
        public static bool CanWrite(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".aoe_write_test");
                File.WriteAllText(probe, "1");
                File.Delete(probe);
                return true;
            }
            catch { return false; }
        }

        // 旧版把 ini 放在 exe 旁边，这里自动搬过来一次
        private static void MigrateIni(string exeDir, string iniPath)
        {
            try
            {
                if (File.Exists(iniPath)) return;
                string old = Path.Combine(exeDir, "AoE4Auto.ini");
                if (File.Exists(old))
                {
                    File.Copy(old, iniPath, false);
                    Log.C(ConsoleColor.Gray, "  已把旧的 AoE4Auto.ini 迁移到数据目录");
                }
            }
            catch { }
        }

        private static void Banner()
        {
            Log.C(ConsoleColor.White, "==========================================================");
            Log.C(ConsoleColor.White, "  AoE4 自动循环脚本   [选择稍后版]   .NET 4.0");
            Log.C(ConsoleColor.Gray, "  流程：找窗口 → 点「开始游戏」→ 点「选择稍后」(5秒重检，最多重复2次)");
            Log.C(ConsoleColor.Gray, "        → 等 4 分钟 → 等「胜利！」→ 回车 → 下一轮");
            Log.C(ConsoleColor.Gray, "  热键：F9 暂停/继续    F10 退出（立即）");
            Log.C(ConsoleColor.White, "==========================================================");
        }

        public static bool IsAdmin()
        {
            try
            {
                System.Security.Principal.WindowsIdentity id = System.Security.Principal.WindowsIdentity.GetCurrent();
                System.Security.Principal.WindowsPrincipal p = new System.Security.Principal.WindowsPrincipal(id);
                return p.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        private static void Pause(string text)
        {
            try { Console.WriteLine(); Console.WriteLine(text); Console.ReadKey(true); } catch { }
        }

        private static int RunCore(string[] args)
        {
            DpiFix();
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dataDir = GetDataDir(baseDir);
            string iniPath = Path.Combine(dataDir, "AoE4Auto.ini");
            MigrateIni(baseDir, iniPath);

            Status.Steps(5);
            Status.Begin("载入配置");
            Config cfg = Config.Load(iniPath);
            Status.Ok(string.Format("延时 {0}s、面板等待 {1}s、对局等待 {2}s、轮询 {3}ms、匹配阈值 {4:0.00}",
                cfg.StartDelaySec, cfg.PanelWaitSec, cfg.BattleWaitSec, cfg.PollMs, cfg.MatchThreshold));

            if (!IsAdmin())
            {
                Status.Warn("当前不是管理员权限。若游戏是普通权限一般没问题；如果点击无效，请右键 exe →「以管理员身份运行」。");
            }

            if (args.Length >= 2 && args[0] == "--test")
            {
                Vision.Init();
                return RunTest(args[1], cfg);
            }

            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "";
            if (mode == "--reset-config")
            {
                try { File.Delete(iniPath); } catch { }
                cfg.Save(iniPath);
                Status.Ok("已用当前默认值重写配置：" + iniPath);
                cfg = Config.Load(iniPath);
                Log.W(string.Format("当前配置：匹配阈值={0:0.00} 对局等待={1}s 胜利最长={2}s 轮询={3}ms",
                    cfg.MatchThreshold, cfg.BattleWaitSec, cfg.VictoryTimeoutSec, cfg.PollMs));
                if (cfg.PauseOnExit == 1) Pause("按任意键退出……");
                return 0;
            }

            if (mode == "--frames")
            {
                // 诊断用：本次运行把每次识别的画面都存下来（frames 目录），方便排查
                cfg.SaveDebugFrames = 1;
                Log.C(ConsoleColor.Yellow, "  ! 已开启画面保存（--frames），运行期间每次检测都会存一张 PNG");
            }
            bool isDetect = mode == "--detect";

            if (args.Length >= 2 && mode == "--selftest")
            {
                Vision.Init();
                return SelfTest(args[1], cfg);
            }

            Status.Begin("初始化（日志 + 内置模板）");
            Log.Init(Path.Combine(dataDir, "AoE4Auto.log"));
            Vision.Init();
            Status.Ok(string.Format("模板解码完成：开始游戏 {0}x{1}、胜利 {2}x{3}、选择稍后 {4}x{5}",
                Vision.BriefingTpl.W, Vision.BriefingTpl.H,
                Vision.VictoryTpl.W, Vision.VictoryTpl.H,
                Vision.WaitTpl.W, Vision.WaitTpl.H));
            Log.W("AoE4 自动循环脚本启动");
            Log.W("数据目录：" + dataDir);
            Log.W("配置文件：" + iniPath);
            Log.W(string.Format("功能确认：点击「选择稍后」已启用（模板 {0}x{1}，阈值 {2:0.00}，点击后 5 秒重检，最多重复 2 次）",
                Vision.WaitTpl.W, Vision.WaitTpl.H, 0.85));
            Log.W("程序位置：" + Application.ExecutablePath);

            Status.Begin("查找《帝国时代4》窗口（标题含 Age of Empires IV）");
            IntPtr hwnd = FindGameWindow();
            int tries = 0;
            while (hwnd == IntPtr.Zero && !Stop)
            {
                tries++;
                if (tries == 1) Status.Warn("没找到游戏窗口，请先启动游戏并停在关卡界面。每 2 秒自动重试……");
                else if (tries % 10 == 0) Status.Tick("仍未找到（已重试 " + tries + " 次）");
                Thread.Sleep(2000);
                hwnd = FindGameWindow();
            }
            if (Stop) return 0;
            Status.Ok("已找到窗口，句柄 " + hwnd.ToString());
            CheckResolution(hwnd);

            if (mode == "--shot")
            {
                Status.Begin("保存截图");
                Activate(hwnd);
                Thread.Sleep(300);
                int cw, ch;
                Bitmap bmp = Vision.Capture(hwnd, out cw, out ch);
                try
                {
                    if (bmp != null)
                    {
                        string path = args.Length < 2 ? Path.Combine(dataDir, "shot.png") : args[1];
                        bmp.Save(path, ImageFormat.Png);
                        Status.Ok("已保存截图：" + path + "（客户区 " + cw + "x" + ch + "，保存为 640x480）");
                    }
                    else Status.Fail("截图失败。");
                }
                finally { if (bmp != null) bmp.Dispose(); }
                return 0;
            }

            if (isDetect) { RunDetect(hwnd, cfg, baseDir); return 0; }

            Status.Begin("输入自检（能否模拟鼠标）");
            if (!InputSelfTest())
            {
                Status.Fail("SetCursorPos 被系统拒绝。");
                Log.W("======================================================");
                Log.W("【错误】当前环境不允许模拟鼠标（SetCursorPos 被系统拒绝）。");
                Log.W("       常见原因：exe 放在受限目录，或没有以管理员身份运行。");
                Log.W("       解决办法：把 exe 放到桌面等普通目录，");
                Log.W("                 右键 →“以管理员身份运行”。");
                Log.W("======================================================");
                if (cfg.PauseOnExit == 1) Pause("按任意键退出……");
                return 2;
            }
            Status.Ok("输入自检通过");

            Status.Begin("启动主循环");
            _form = new HotkeyForm();
            IntPtr handle = _form.Handle;

            Bot bot = new Bot(hwnd, cfg, Path.Combine(dataDir, "frames"));
            Thread t = new Thread(new ThreadStart(bot.Run));
            t.IsBackground = true;
            t.Start();
            Status.Ok("已启动，正在后台运行；关闭本窗口或按 F10 结束");

            Application.Run(new ApplicationContext(_form));

            Log.W("脚本已停止。");
            if (cfg.PauseOnExit == 1 && !_quickExit) Pause("按任意键关闭窗口……");
            return 0;
        }

        // 点击自检：把“算出来的坐标 / SendInput 结果 / 光标实际位置”全打出来
        public static void RunDetect(IntPtr hwnd, Config cfg, string baseDir)
        {
            Log.W("只检测模式（不会点击），按 Ctrl+C 退出。");
            while (true)
            {
                Program.Activate(hwnd);
                int cw, ch;
                Bitmap b = Vision.Capture(hwnd, out cw, out ch);
                if (b == null) { Thread.Sleep(1000); continue; }
                Hit bri, vic;
                double pan;
                try
                {
                    bri = Vision.Match(b, Vision.BriefingTpl, Vision.BriefingRoi, cfg.MatchThreshold);
                    vic = Vision.Match(b, Vision.VictoryTpl, Vision.VictoryRoi, cfg.MatchThreshold);
                    Vision.PanelPresent(b, cfg.MatchThreshold, out pan);
                }
                finally { b.Dispose(); }

                Bitmap bn = Vision.CaptureNative(hwnd, out cw, out ch);
                double waitScore = 0; double waitScale = 0;
                try
                {
                    if (bn != null)
                    {
                        Hit wh = Vision.MatchWait(bn, cfg.MatchThreshold);
                        waitScore = wh.Score; waitScale = wh.Scale;
                    }
                }
                finally { if (bn != null) bn.Dispose(); }

                Log.W(string.Format("开始游戏={0:0.00}  胜利={1:0.00}  增益面板={2:0.00}  选择稍后={3:0.00}@{4:0.00}x",
                    bri.Score, vic.Score, pan, waitScore, waitScale));
                Thread.Sleep(1000);
            }
        }

        public static int RunTest(string path, Config cfg)
        {
            using (Bitmap b = new Bitmap(path))
            {
                Hit bri = Vision.Match(b, Vision.BriefingTpl, Vision.BriefingRoi, cfg.MatchThreshold);
                Hit vic = Vision.Match(b, Vision.VictoryTpl, Vision.VictoryRoi, cfg.MatchThreshold);
                double pan;
                Vision.PanelPresent(b, cfg.MatchThreshold, out pan);
                Hit wh = Vision.MatchWait(b, cfg.MatchThreshold);
                Hit rp = Vision.Match(b, Vision.ReplayTpl, Vision.ReplayRoi, cfg.MatchThreshold);
                Log.W(string.Format("开始游戏={0:0.00}({1},{2})  胜利={3:0.00}({4},{5})  增益面板={6:0.00}  再玩一次={7:0.00}({8},{9})  选择稍后={10:0.00}({11},{12})@{13:0.00}x",
                    bri.Score, bri.X, bri.Y, vic.Score, vic.X, vic.Y, pan, rp.Score, rp.X, rp.Y, wh.Score, wh.X, wh.Y, wh.Scale));
            }
            if (cfg.PauseOnExit == 1) { Console.WriteLine("按任意键退出……"); try { Console.ReadKey(); } catch { } }
            return 0;
        }

        // 隐藏自检：对给定图片跑“选择稍后”检测
        public static int SelfTest(string path, Config cfg)
        {
            using (Bitmap b = new Bitmap(path))
            {
                double th = Math.Max(cfg.MatchThreshold, 0.78);
                Hit wh = Vision.MatchWait(b, th);
                Console.WriteLine("SELFTEST size={0}x{1} score={2:0.000} found={3} x={4} y={5} scale={6:0.00}",
                    b.Width, b.Height, wh.Score, wh.Found, wh.X, wh.Y, wh.Scale);
            }
            return 0;
        }

        public static bool InputSelfTest()
        {
            try
            {
                POINT p;
                if (!Native.GetCursorPos(out p)) return false;
                if (!Native.SetCursorPos(p.X, p.Y)) return false;
                return true;
            }
            catch { return false; }
        }

        public static void DpiFix()
        {
            try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); return; } catch { }
            try { Native.SetProcessDpiAwareness(2); return; } catch { }
            try { Native.SetProcessDPIAware(); } catch { }
        }

        private static IntPtr _best;
        private static int _bestArea;

        public static IntPtr FindGameWindow()
        {
            _best = IntPtr.Zero;
            _bestArea = 0;
            Native.EnumWindows(new EnumWindowsProc(EnumCallback), IntPtr.Zero);
            return _best;
        }

        private static bool EnumCallback(IntPtr hwnd, IntPtr lParam)
        {
            if (!Native.IsWindowVisible(hwnd)) return true;
            int len = Native.GetWindowTextLength(hwnd);
            if (len <= 0) return true;
            StringBuilder sb = new StringBuilder(len + 2);
            Native.GetWindowText(hwnd, sb, sb.Capacity);
            string title = sb.ToString();
            if (title.IndexOf("Age of Empires IV", StringComparison.OrdinalIgnoreCase) < 0) return true;

            RECT rc;
            if (!Native.GetClientRect(hwnd, out rc)) return true;
            int area = rc.Right * rc.Bottom;
            if (area <= 10000) return true;
            if (area > _bestArea) { _bestArea = area; _best = hwnd; }
            return true;
        }

        // 目标分辨率：模板按 640x480 客户区裁的，不符时红字提示但继续跑
        public const int ExpectW = 640;
        public const int ExpectH = 480;

        public static void CheckResolution(IntPtr hwnd)
        {
            try
            {
                POINT p0 = new POINT(0, 0);
                Native.ClientToScreen(hwnd, ref p0);
                RECT rc;
                Native.GetClientRect(hwnd, out rc);
                POINT p1 = new POINT(rc.Right, rc.Bottom);
                Native.ClientToScreen(hwnd, ref p1);
                int w = p1.X - p0.X;
                int h = p1.Y - p0.Y;
                if (w == ExpectW && h == ExpectH)
                    Log.C(ConsoleColor.Green, string.Format("  ✓ 分辨率符合 {0}x{1}", ExpectW, ExpectH));
                else
                    Log.C(ConsoleColor.Red, string.Format("  ! 分辨率不符：当前客户区 {0}x{1}，建议 {2}x{3}（继续尝试，识别可能不准）",
                        w, h, ExpectW, ExpectH));
            }
            catch { }
        }

        public static bool Activate(IntPtr hwnd)
        {
            for (int i = 0; i < 3; i++)
            {
                if (Native.GetForegroundWindow() == hwnd) { Thread.Sleep(80); return true; }
                if (Native.IsIconic(hwnd)) Native.ShowWindow(hwnd, Native.SW_RESTORE);

                IntPtr fg = Native.GetForegroundWindow();
                uint pid;
                uint tid = Native.GetWindowThreadProcessId(fg, out pid);
                uint cur = Native.GetCurrentThreadId();
                bool attached = false;
                try
                {
                    if (tid != 0 && tid != cur) attached = Native.AttachThreadInput(cur, tid, true);
                    Native.BringWindowToTop(hwnd);
                    Native.SetForegroundWindow(hwnd);
                    Native.SetActiveWindow(hwnd);
                }
                finally
                {
                    if (attached) Native.AttachThreadInput(cur, tid, false);
                }
                Thread.Sleep(180);
                if (Native.GetForegroundWindow() == hwnd) return true;

                Native.keybd_event((byte)Native.VK_MENU, 0, 0, IntPtr.Zero);
                Native.keybd_event((byte)Native.VK_MENU, 0, Native.KEYEVENTF_KEYUP, IntPtr.Zero);
                Thread.Sleep(50);
                Native.SetForegroundWindow(hwnd);
                Thread.Sleep(180);
                if (Native.GetForegroundWindow() == hwnd) return true;
            }
            return Native.GetForegroundWindow() == hwnd;
        }
    }
}

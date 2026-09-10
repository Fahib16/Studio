using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Custom.StudioBridge.Design
{
    public enum HoverTarget
    {
        None,
        Browser,
        Desktop
    }

    public class HoverTargetChangedEventArgs : EventArgs
    {
        public HoverTarget Target { get; set; }
        public IntPtr WindowHandle { get; set; }
        public string ProcessName { get; set; }
        public string WindowTitle { get; set; }
    }

    /// <summary>
    /// Memantau jendela di bawah kursor untuk menentukan apakah user sedang
    /// mengarah ke browser (ditangani Studio Bridge) atau ke aplikasi desktop
    /// (ditangani UI Automation), TANPA user perlu memilih dulu.
    ///
    /// Berbasis DWELL, bukan klik: peristiwa dikirim setelah kursor bertahan
    /// di jendela yang sama selama DwellMs. Ini penting supaya user tidak
    /// perlu klik dua kali (sekali untuk memilih jendela, sekali untuk memilih
    /// elemen) — begitu kursor "mendarat" di jendela target, picker yang tepat
    /// sudah aktif dan klik pertama langsung mengenai elemennya, seperti
    /// UiPath.
    ///
    /// Jeda dwell juga meredam jendela yang cuma terlewati saat kursor
    /// bergerak menyeberang layar; tanpa itu, picker akan dinyalakan dan
    /// dimatikan berkali-kali dalam sekali gerakan.
    /// </summary>
    public class HoverTargetWatcher : IDisposable
    {
        /// <summary>
        /// Proses yang dianggap browser. Firefox SENGAJA tidak masuk: extension
        /// Studio Bridge dibangun untuk Chrome/Edge (native messaging MV3),
        /// jadi menganggap Firefox sebagai browser hanya akan menghasilkan
        /// picker web yang tidak pernah merespons.
        /// </summary>
        private static readonly string[] BrowserProcesses =
        {
            "chrome", "msedge", "brave", "opera", "vivaldi"
        };

        public int PollMs { get; set; } = 80;
        public int DwellMs { get; set; } = 350;

        public event EventHandler<HoverTargetChangedEventArgs> TargetChanged;

        private Thread _thread;
        private volatile bool _running;

        private IntPtr _lastReported = IntPtr.Zero;

        public void Start()
        {
            if (_running) return;
            _running = true;

            _thread = new Thread(Loop) { IsBackground = true, Name = "HoverTargetWatcher" };
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
            _lastReported = IntPtr.Zero;
        }

        private void Loop()
        {
            IntPtr candidate = IntPtr.Zero;
            var since = DateTime.MinValue;

            while (_running)
            {
                try
                {
                    var hwnd = TopLevelWindowUnderCursor();

                    if (hwnd != candidate)
                    {
                        candidate = hwnd;
                        since = DateTime.UtcNow;
                    }
                    else if (candidate != IntPtr.Zero
                             && candidate != _lastReported
                             && (DateTime.UtcNow - since).TotalMilliseconds >= DwellMs)
                    {
                        _lastReported = candidate;
                        Report(candidate);
                    }
                }
                catch (Exception)
                {
                    // Jendela bisa hilang di antara dua panggilan Win32.
                    // Itu wajar saat user menutup sesuatu; jangan sampai
                    // mematikan pemantau.
                }

                Thread.Sleep(PollMs);
            }
        }

        private void Report(IntPtr hwnd)
        {
            var process = ProcessNameOf(hwnd);
            var isBrowser = false;

            foreach (var b in BrowserProcesses)
            {
                if (string.Equals(process, b, StringComparison.OrdinalIgnoreCase)) { isBrowser = true; break; }
            }

            var handler = TargetChanged;
            if (handler == null) return;

            handler(this, new HoverTargetChangedEventArgs
            {
                Target = isBrowser ? HoverTarget.Browser : HoverTarget.Desktop,
                WindowHandle = hwnd,
                ProcessName = process,
                WindowTitle = TitleOf(hwnd)
            });
        }

        /// <summary>
        /// WindowFromPoint mengembalikan kontrol ANAK yang tepat di bawah
        /// kursor (mis. satu tombol), bukan jendela aplikasinya. Untuk
        /// menentukan browser-atau-bukan yang dibutuhkan adalah jendela
        /// tingkat atas, jadi ditelusuri ke induknya lewat GetAncestor.
        /// </summary>
        private static IntPtr TopLevelWindowUnderCursor()
        {
            POINT pt;
            if (!GetCursorPos(out pt)) return IntPtr.Zero;

            var hwnd = WindowFromPoint(pt);
            if (hwnd == IntPtr.Zero) return IntPtr.Zero;

            var root = GetAncestor(hwnd, GA_ROOT);
            return root != IntPtr.Zero ? root : hwnd;
        }

        private static string ProcessNameOf(IntPtr hwnd)
        {
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            if (pid == 0) return null;

            try { return Process.GetProcessById((int)pid).ProcessName; }
            catch (Exception) { return null; }
        }

        private static string TitleOf(IntPtr hwnd)
        {
            var len = GetWindowTextLength(hwnd);
            if (len <= 0) return "";

            var sb = new StringBuilder(len + 1);
            GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public void Dispose() { Stop(); }

        #region Win32

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        private const uint GA_ROOT = 2;

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        #endregion
    }
}

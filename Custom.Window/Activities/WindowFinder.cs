using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Custom.Window
{
    public enum TitleMatchMode
    {
        /// <summary>Judul window MEMUAT teks yang dicari (default).</summary>
        Contains,

        Exact,
        StartsWith,

        /// <summary>Pola dengan * (banyak karakter) dan ? (satu karakter), mis. "Laporan * - Excel".</summary>
        Wildcard
    }

    /// <summary>
    /// Pencarian dan pengendalian window lewat Windows API.
    ///
    /// Sengaja TIDAK memakai IElement/Selector OpenRPA, mengikuti keputusan
    /// yang sudah diambil di MaximizeWindow versi lama: window aplikasi
    /// desktop dan window browser sama-sama window OS biasa yang punya judul,
    /// jadi satu jalur ini melayani keduanya tanpa perlu memisahkan
    /// web/desktop seperti pada activity yang menyasar elemen DI DALAM window.
    ///
    /// Keterbatasan yang ikut terbawa: kalau ada beberapa window dengan judul
    /// mirip, yang dipakai adalah yang PERTAMA cocok menurut urutan EnumWindows
    /// (umumnya mengikuti Z-order). Isi Process Name untuk mempersempit.
    /// </summary>
    internal static class WindowFinder
    {
        public const int SW_MINIMIZE = 6;
        public const int SW_MAXIMIZE = 3;
        public const int SW_RESTORE = 9;

        private const uint WM_CLOSE = 0x0010;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);

        private const uint GA_ROOT = 2;

        public static IntPtr Foreground()
        {
            return GetForegroundWindow();
        }

        /// <summary>
        /// Naik dari sebuah kontrol ke JENDELA tingkat atas yang memuatnya.
        ///
        /// Dibutuhkan jalur selector: elemen yang di-Indicate sering berupa
        /// kontrol di dalam jendela (tombol, kotak teks), sedangkan yang mau
        /// dimaksimalkan/ditutup adalah jendelanya. Tanpa ini, ShowWindow
        /// dipanggil pada handle kontrol dan tidak terjadi apa-apa — kegagalan
        /// diam yang membingungkan.
        /// </summary>
        public static IntPtr RootOf(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return IntPtr.Zero;
            var root = GetAncestor(hWnd, GA_ROOT);
            return root == IntPtr.Zero ? hWnd : root;
        }

        public static string TitleOf(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return "";

            var length = GetWindowTextLength(hWnd);
            if (length == 0) return "";

            var sb = new StringBuilder(length + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public static string ProcessNameOf(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return "";

            try
            {
                uint pid;
                GetWindowThreadProcessId(hWnd, out pid);
                return Process.GetProcessById((int)pid).ProcessName;
            }
            catch (Exception)
            {
                // Proses sudah keluar atau tidak bisa dibaca; nama kosong lebih
                // berguna daripada menggagalkan activity yang cuma mau tahu judul.
                return "";
            }
        }

        public static bool Show(IntPtr hWnd, int command)
        {
            return ShowWindow(hWnd, command);
        }

        public static bool Focus(IntPtr hWnd)
        {
            return SetForegroundWindow(hWnd);
        }

        /// <summary>
        /// Minta window menutup dirinya sendiri (WM_CLOSE), BUKAN mematikan
        /// prosesnya. Aplikasi masih sempat menyimpan dan menampilkan dialog
        /// "simpan perubahan?" — mematikan proses akan membuang pekerjaan yang
        /// belum tersimpan tanpa peringatan.
        /// </summary>
        public static void Close(IntPtr hWnd)
        {
            SendMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }

        public static bool Bounds(IntPtr hWnd, out RECT rect)
        {
            return GetWindowRect(hWnd, out rect);
        }

        public static bool Move(IntPtr hWnd, int x, int y, int width, int height)
        {
            return MoveWindow(hWnd, x, y, width, height, true);
        }

        /// <summary>
        /// Cari window pertama yang cocok judul (kalau diisi) DAN nama proses
        /// (kalau diisi). Keduanya kosong berarti tidak ada pencarian sama
        /// sekali — pemanggil yang memutuskan apakah mau memakai window aktif.
        /// </summary>
        public static IntPtr Find(string titlePattern, TitleMatchMode mode, string processName)
        {
            var found = IntPtr.Zero;

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;

                if (!string.IsNullOrEmpty(titlePattern))
                {
                    var title = TitleOf(hWnd);
                    if (title.Length == 0) return true;
                    if (!TitleMatches(title, titlePattern, mode)) return true;
                }

                if (!string.IsNullOrEmpty(processName))
                {
                    var name = ProcessNameOf(hWnd);
                    if (!string.Equals(name, processName, StringComparison.OrdinalIgnoreCase)) return true;
                }

                found = hWnd;
                return false;
            }, IntPtr.Zero);

            return found;
        }

        public static bool TitleMatches(string title, string pattern, TitleMatchMode mode)
        {
            switch (mode)
            {
                case TitleMatchMode.Exact:
                    return string.Equals(title, pattern, StringComparison.OrdinalIgnoreCase);

                case TitleMatchMode.StartsWith:
                    return title.StartsWith(pattern, StringComparison.OrdinalIgnoreCase);

                case TitleMatchMode.Wildcard:
                    var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
                    try { return Regex.IsMatch(title, regex, RegexOptions.IgnoreCase); }
                    catch (Exception) { return false; }

                default:
                    return title.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
    }
}

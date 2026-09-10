using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;

namespace Custom.StudioBridge.Design
{
    /// <summary>
    /// Menjalankan picker web sambil TETAP memantau kursor, supaya user bisa
    /// berpindah ke aplikasi desktop tanpa membatalkan Indicate.
    ///
    /// Kenapa perlu jendela tersendiri: StudioPipeClient.StartIndicate memblokir
    /// sampai user mengklik elemen di halaman (bisa puluhan detik). Kalau
    /// dipanggil langsung di UI thread, tidak ada yang bisa memantau kursor
    /// selama itu. Di sini panggilan tersebut dijalankan di task terpisah,
    /// sementara timer di UI thread mengawasi jendela di bawah kursor.
    ///
    /// Begitu kursor meninggalkan browser cukup lama, picker web DIBATALKAN
    /// lewat aksi cancelIndicate dan pemanggil diberi tahu untuk kembali ke
    /// picker desktop.
    /// </summary>
    public class WebIndicateWaitWindow : System.Windows.Window
    {
        /// <summary>
        /// Kursor harus berada di luar browser selama ini sebelum dianggap
        /// benar-benar berpindah. Tanpa jeda, kursor yang cuma melintasi tepi
        /// jendela browser sudah membatalkan picker.
        /// </summary>
        private const int LeaveDwellMs = 500;

        private static readonly string[] BrowserProcesses =
        { "chrome", "msedge", "brave", "opera", "vivaldi" };

        private readonly DispatcherTimer _timer = new DispatcherTimer();
        private readonly int _tabId;
        private readonly TextBlock _text = new TextBlock();

        private DateTime _outsideSince = DateTime.MinValue;
        private bool _closing;

        /// <summary>Hasil mentah dari picker web, null kalau tidak jadi.</summary>
        public JObject PickResult { get; private set; }

        /// <summary>True kalau user berpindah keluar browser.</summary>
        public bool LeftBrowser { get; private set; }

        public WebIndicateWaitWindow(int tabId)
        {
            _tabId = tabId;

            WindowStyle = System.Windows.WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;

            // TIDAK diaktifkan saat tampil: picker web berjalan di dalam
            // halaman dan butuh jendela browser tetap memegang fokus. Jendela
            // ini murni penunjuk status.
            ShowActivated = false;

            SizeToContent = SizeToContent.WidthAndHeight;
            Left = (SystemParameters.PrimaryScreenWidth / 2) - 280;
            Top = 20;

            _text.Text = "Memilih elemen di browser — arahkan kursor keluar browser untuk kembali ke desktop";
            _text.Foreground = Brushes.White;
            _text.FontSize = 14;
            _text.FontWeight = FontWeights.SemiBold;

            Content = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(33, 33, 33)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(16, 10, 16, 10),
                Child = _text
            };

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var tabId = _tabId;

            Task.Run(() =>
            {
                JObject result = null;
                try { result = StudioPipeClient.StartIndicate(tabId); }
                catch (Exception) { result = null; }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_closing) return;
                    PickResult = result;
                    Finish();
                }));
            });

            _timer.Interval = TimeSpan.FromMilliseconds(120);
            _timer.Tick += OnTick;
            _timer.Start();
        }

        private void OnTick(object sender, EventArgs e)
        {
            POINT pt;
            if (!GetCursorPos(out pt)) return;

            var hwnd = WindowFromPoint(pt);
            if (hwnd == IntPtr.Zero) return;

            var root = GetAncestor(hwnd, GA_ROOT);
            if (root == IntPtr.Zero) root = hwnd;

            if (IsBrowser(root)) { _outsideSince = DateTime.MinValue; return; }

            if (_outsideSince == DateTime.MinValue) { _outsideSince = DateTime.UtcNow; return; }
            if ((DateTime.UtcNow - _outsideSince).TotalMilliseconds < LeaveDwellMs) return;

            // Batalkan picker di halaman lebih dulu, supaya overlay birunya
            // tidak tertinggal dan klik berikutnya di aplikasi desktop tidak
            // ditelan olehnya.
            LeftBrowser = true;
            StudioPipeClient.CancelIndicate(_tabId);
            Finish();
        }

        private void Finish()
        {
            if (_closing) return;
            _closing = true;

            _timer.Stop();
            Close();
        }

        private static bool IsBrowser(IntPtr hwnd)
        {
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            if (pid == 0) return false;

            try
            {
                var name = Process.GetProcessById((int)pid).ProcessName;
                foreach (var b in BrowserProcesses)
                    if (string.Equals(name, b, StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch (Exception) { }

            return false;
        }

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
    }
}

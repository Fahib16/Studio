using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.AutomationElements.Infrastructure;

namespace Custom.StudioBridge.Design
{
    /// <summary>
    /// Picker elemen untuk aplikasi desktop — padanan overlay picker yang
    /// berjalan di dalam halaman untuk sisi web.
    ///
    /// Jendela ini SENGAJA tembus klik (WS_EX_TRANSPARENT), supaya
    /// WindowFromPoint melihat aplikasi di bawahnya, bukan overlay ini.
    /// Karena tembus klik, klik user tidak bisa ditangkap lewat event WPF
    /// biasa — dipakai hook mouse tingkat sistem yang sekaligus MENELAN klik
    /// tersebut, supaya memilih tombol "Send" di Postman tidak sekaligus
    /// benar-benar menekannya.
    ///
    /// Keyboard juga lewat hook tingkat sistem, bukan event KeyDown: setelah
    /// jeda F2 user biasanya mengklik aplikasi target, sehingga fokus keyboard
    /// pindah dari jendela ini dan event WPF berhenti sampai.
    /// </summary>
    // System.Windows.Window ditulis lengkap: FlaUI.Core.AutomationElements
    // juga punya kelas bernama Window, jadi nama pendeknya ambigu di sini.
    public class DesktopPickerWindow : System.Windows.Window
    {
        /// <summary>Lama jeda saat F2 ditekan.</summary>
        private const int PauseSeconds = 5;

        private readonly DispatcherTimer _timer = new DispatcherTimer();
        private readonly Rectangle _box = new Rectangle();
        private readonly Canvas _canvas = new Canvas();
        private readonly Border _hintBox = new Border();
        private readonly TextBlock _hint = new TextBlock();
        private readonly Border _countBox = new Border();
        private readonly TextBlock _countNumber = new TextBlock();
        private readonly TextBlock _countLabel = new TextBlock();

        private AutomationElement _current;
        private double _dpiScale = 1.0;

        private bool _paused;
        private DateTime _pauseUntil;

        /// <summary>
        /// Kursor harus bertahan di jendela browser selama ini sebelum
        /// pemilihan diserahkan ke picker web.
        ///
        /// Tanpa jeda, picker langsung terkunci ke web hanya karena kursor
        /// KEBETULAN berada di atas browser saat Indicate ditekan — padahal
        /// user mungkin baru mau bergerak ke Notepad.
        /// </summary>
        private const int BrowserDwellMs = 500;

        private DateTime _browserSince = DateTime.MinValue;

        private IntPtr _mouseHook = IntPtr.Zero;
        private IntPtr _keyHook = IntPtr.Zero;
        private LowLevelProc _mouseProc;
        private LowLevelProc _keyProc;

        private readonly int _ownProcessId = Process.GetCurrentProcess().Id;

        /// <summary>Elemen yang dipilih user, null kalau dibatalkan.</summary>
        public AutomationElement SelectedElement { get; private set; }

        /// <summary>
        /// True kalau kursor masuk jendela browser, sehingga pemilihan harus
        /// diserahkan ke picker web. Inilah yang membuat user tidak perlu
        /// memilih dulu "web atau desktop".
        /// </summary>
        public bool SwitchToWeb { get; private set; }

        /// <summary>Jendela browser yang ditunjuk saat beralih.</summary>
        public IntPtr BrowserWindow { get; private set; }

        /// <summary>
        /// Proses yang dianggap browser. Firefox tidak masuk: extension Studio
        /// Bridge dibangun untuk Chrome/Edge, jadi menganggapnya browser hanya
        /// akan menyerahkan ke picker web yang tidak pernah merespons.
        /// </summary>
        private static readonly string[] BrowserProcesses =
        { "chrome", "msedge", "brave", "opera", "vivaldi" };

        public DesktopPickerWindow()
        {
            WindowStyle = System.Windows.WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;

            // Menutupi SELURUH area virtual (semua monitor): elemen target bisa
            // saja berada di monitor kedua.
            // Overlay dimunculkan dengan memudar masuk; tanpa itu ia berkedip
            // muncul begitu saja menutupi seluruh layar.
            Opacity = 0;

            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            _box.Stroke = new SolidColorBrush(Color.FromRgb(0, 176, 255));
            _box.StrokeThickness = 3;
            _box.Fill = new SolidColorBrush(Color.FromArgb(70, 0, 176, 255));
            _box.Visibility = Visibility.Collapsed;

            _hint.Foreground = Brushes.White;
            _hint.FontSize = 15;
            _hint.FontWeight = FontWeights.SemiBold;
            _hintBox.Background = new SolidColorBrush(Color.FromRgb(33, 33, 33));
            _hintBox.CornerRadius = new CornerRadius(4);
            _hintBox.Padding = new Thickness(14, 8, 14, 8);
            _hintBox.Child = _hint;

            // Panel hitungan mundur F2: angka besar di tengah layar, mengikuti
            // gaya UiPath — supaya terlihat jelas sambil mata user sedang
            // tertuju ke aplikasi target, bukan ke bilah petunjuk di atas.
            _countNumber.Foreground = Brushes.White;
            _countNumber.FontSize = 44;
            _countNumber.FontWeight = FontWeights.Bold;
            _countNumber.HorizontalAlignment = HorizontalAlignment.Center;

            _countLabel.Text = "F2 lagi untuk memperpanjang";
            _countLabel.Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200));
            _countLabel.FontSize = 12;
            _countLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _countLabel.Margin = new Thickness(0, 4, 0, 0);

            var countStack = new StackPanel();
            countStack.Children.Add(_countNumber);
            countStack.Children.Add(_countLabel);

            _countBox.Background = new SolidColorBrush(Color.FromArgb(150, 33, 33, 33));
            _countBox.BorderBrush = new SolidColorBrush(Color.FromArgb(180, 0, 176, 255));
            _countBox.BorderThickness = new Thickness(2);
            _countBox.CornerRadius = new CornerRadius(10);
            _countBox.Padding = new Thickness(22, 12, 22, 12);
            _countBox.Child = countStack;
            _countBox.Visibility = Visibility.Collapsed;

            _canvas.Children.Add(_box);
            _canvas.Children.Add(_hintBox);
            _canvas.Children.Add(_countBox);
            Content = _canvas;

            CenterCountBox();

            // Diposisikan relatif LAYAR UTAMA, bukan tengah area virtual:
            // pada susunan dua monitor, tengah area virtual bisa jatuh di
            // perbatasan kedua layar dan petunjuknya terbelah.
            Canvas.SetTop(_hintBox, 20 - SystemParameters.VirtualScreenTop);
            Canvas.SetLeft(_hintBox, (SystemParameters.PrimaryScreenWidth / 2) - 260
                                     - SystemParameters.VirtualScreenLeft);

            ShowNormalHint();

            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        /// <summary>
        /// Pusatkan panel hitungan di layar utama.
        ///
        /// Dilakukan setelah panel diukur (ActualWidth terisi), bukan dari
        /// angka tetap: ukuran panel bergantung pada font sistem, jadi offset
        /// yang dihitung manual akan meleset di komputer dengan skala teks
        /// berbeda.
        /// </summary>
        private void CenterCountBox()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var w = _countBox.ActualWidth > 0 ? _countBox.ActualWidth : 220;
                var h = _countBox.ActualHeight > 0 ? _countBox.ActualHeight : 100;

                Canvas.SetLeft(_countBox, (SystemParameters.PrimaryScreenWidth / 2) - (w / 2)
                                          - SystemParameters.VirtualScreenLeft);
                Canvas.SetTop(_countBox, (SystemParameters.PrimaryScreenHeight / 2) - (h / 2)
                                         - SystemParameters.VirtualScreenTop);
            }), DispatcherPriority.Loaded);
        }

        private void ShowNormalHint()
        {
            _hint.Text = "Arahkan ke elemen, klik untuk memilih   |   F2 = jeda " +
                         PauseSeconds + " detik   |   Esc = batal";
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            var exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);

            var source = PresentationSource.FromVisual(this);
            if (source != null && source.CompositionTarget != null)
                _dpiScale = source.CompositionTarget.TransformToDevice.M11;

            InstallMouseHook();
            InstallKeyHook();

            // Overlay dimunculkan dengan memudar masuk, bukan berkedip muncul
            // menutupi seluruh layar sekaligus.
            FadeTo(1, 140, null);

            _timer.Interval = TimeSpan.FromMilliseconds(70);
            _timer.Tick += OnTick;
            _timer.Start();
        }

        private void OnClosed(object sender, EventArgs e)
        {
            _timer.Stop();
            RemoveMouseHook();
            RemoveKeyHook();
        }

        // ---------------- Jeda F2 ----------------

        /// <summary>
        /// Jeda pemilihan supaya user bisa membuka menu, dropdown, atau
        /// berpindah jendela dulu.
        ///
        /// Selama jeda, hook mouse DILEPAS: kalau tidak, klik untuk membuka
        /// menu akan ditelan hook dan menunya tidak pernah terbuka — yang
        /// justru menghapus gunanya jeda ini.
        /// </summary>
        private void BeginPause()
        {
            // Hitungan SELALU disetel ulang, termasuk saat jeda sedang
            // berjalan. Elemen yang ditunggu sering butuh lebih lama dari
            // perkiraan (halaman belum selesai memuat, dialog belum muncul),
            // jadi user harus bisa menekan F2 berkali-kali untuk memperpanjang
            // alih-alih kehilangan jeda di tengah jalan.
            _pauseUntil = DateTime.UtcNow.AddSeconds(PauseSeconds);

            if (_paused) return;

            _paused = true;
            RemoveMouseHook();
            _box.Visibility = Visibility.Collapsed;
            _countBox.Visibility = Visibility.Visible;
            CenterCountBox();
        }

        private void EndPause()
        {
            _paused = false;
            _countBox.Visibility = Visibility.Collapsed;
            InstallMouseHook();
            ShowNormalHint();
        }

        // ---------------- Loop ----------------

        private void OnTick(object sender, EventArgs e)
        {
            if (_paused)
            {
                // F2 tetap dipantau SELAMA jeda supaya bisa diperpanjang.
                if ((GetAsyncKeyState(VK_F2) & 0x8000) != 0) BeginPause();
                if ((GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0) { Cancel(); return; }

                var left = (_pauseUntil - DateTime.UtcNow).TotalSeconds;
                if (left <= 0) { EndPause(); return; }

                _countNumber.Text = Math.Ceiling(left).ToString("0");
                _hint.Text = "Dijeda — pemilihan berlanjut otomatis setelah hitungan selesai";
                return;
            }

            // Jalur CADANGAN untuk F2/Esc. Hook keyboard tingkat sistem
            // kadang tidak sampai — bisa karena aplikasi lain memasang hook
            // yang menelan lebih dulu, atau karena kebijakan integritas proses.
            // Polling status tombol tidak bergantung pada rantai hook sama
            // sekali, jadi jeda F2 tetap bisa dipakai dalam kondisi itu.
            if ((GetAsyncKeyState(VK_F2) & 0x8000) != 0) { BeginPause(); return; }
            if ((GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0) { Cancel(); return; }

            POINT pt;
            if (!GetCursorPos(out pt)) return;

            var underCursor = TopLevelWindowAt(pt);

            // Jendela milik Studio sendiri dilewati: kursor yang sekadar
            // melintasi jendela Studio tidak sedang menunjuk target apa pun.
            if (underCursor != IntPtr.Zero && ProcessIdOf(underCursor) == _ownProcessId)
            {
                _box.Visibility = Visibility.Collapsed;
                return;
            }

            if (IsBrowser(underCursor))
            {
                if (_browserSince == DateTime.MinValue) _browserSince = DateTime.UtcNow;

                var held = (DateTime.UtcNow - _browserSince).TotalMilliseconds;
                if (held < BrowserDwellMs)
                {
                    _box.Visibility = Visibility.Collapsed;
                    _hint.Text = "Browser terdeteksi — tahan sebentar untuk memilih elemen di halaman";
                    return;
                }

                // Serahkan ke picker web. Ditutup di sini supaya hook mouse
                // dilepas lebih dulu — kalau tidak, klik user di halaman akan
                // ikut tertelan hook ini.
                SwitchToWeb = true;
                BrowserWindow = underCursor;
                DialogResult = false;
                return;
            }

            if (_browserSince != DateTime.MinValue) { _browserSince = DateTime.MinValue; ShowNormalHint(); }

            var el = DesktopSelectorResolver.ElementFromPoint(pt.X, pt.Y);
            if (el == null) { _box.Visibility = Visibility.Collapsed; return; }

            _current = el;

            try
            {
                var r = el.BoundingRectangle;
                if (r.Width <= 0 || r.Height <= 0) { _box.Visibility = Visibility.Collapsed; return; }

                // BoundingRectangle dalam pixel FISIK; posisi di dalam jendela
                // WPF dalam satuan independen-DPI dan relatif terhadap sudut
                // kiri-atas area virtual. Tanpa dua koreksi ini, kotak meleset
                // di layar ber-scaling atau di monitor kedua.
                Canvas.SetLeft(_box, (r.X / _dpiScale) - SystemParameters.VirtualScreenLeft);
                Canvas.SetTop(_box, (r.Y / _dpiScale) - SystemParameters.VirtualScreenTop);
                _box.Width = r.Width / _dpiScale;
                _box.Height = r.Height / _dpiScale;
                _box.Visibility = Visibility.Visible;
            }
            catch (Exception)
            {
                // Elemen bisa hilang di antara dua tick (menu tertutup, dsb).
                _box.Visibility = Visibility.Collapsed;
            }
        }


        /// <summary>
        /// Memudarkan overlay ke opasitas tertentu, lalu menjalankan
        /// <paramref name="then"/>.
        ///
        /// Kalau animasinya gagal karena sebab apa pun, opasitasnya disetel
        /// langsung dan <paramref name="then"/> tetap dijalankan — overlay yang
        /// tidak pernah menutup akan mengunci seluruh layar, dan itu jauh lebih
        /// buruk daripada transisi yang menyentak.
        /// </summary>
        private void FadeTo(double target, double milliseconds, Action then)
        {
            try
            {
                var fade = new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = target,
                    Duration = TimeSpan.FromMilliseconds(milliseconds),
                    EasingFunction = new System.Windows.Media.Animation.QuadraticEase
                    {
                        EasingMode = target > 0
                            ? System.Windows.Media.Animation.EasingMode.EaseOut
                            : System.Windows.Media.Animation.EasingMode.EaseIn
                    }
                };

                if (then != null) fade.Completed += (s, e) => then();
                BeginAnimation(OpacityProperty, fade);
            }
            catch (Exception)
            {
                try
                {
                    BeginAnimation(OpacityProperty, null);
                    Opacity = target;
                }
                catch (Exception) { }

                if (then != null) then();
            }
        }

        private void Select()
        {
            SelectedElement = _current;
            DialogResult = _current != null;
        }

        private void Cancel()
        {
            SelectedElement = null;
            DialogResult = false;
        }

        // ---------------- Bantuan jendela ----------------

        private static bool IsBrowser(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;

            var pid = ProcessIdOf(hwnd);
            if (pid == 0) return false;

            try
            {
                var name = Process.GetProcessById(pid).ProcessName;
                foreach (var b in BrowserProcesses)
                    if (string.Equals(name, b, StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch (Exception) { }

            return false;
        }

        private static int ProcessIdOf(IntPtr hwnd)
        {
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            return (int)pid;
        }

        private static IntPtr TopLevelWindowAt(POINT pt)
        {
            var hwnd = WindowFromPoint(pt);
            if (hwnd == IntPtr.Zero) return IntPtr.Zero;

            var root = GetAncestor(hwnd, GA_ROOT);
            return root != IntPtr.Zero ? root : hwnd;
        }

        // ---------------- Hook tingkat sistem ----------------

        private void InstallMouseHook()
        {
            if (_mouseHook != IntPtr.Zero) return;
            _mouseProc = MouseCallback;
            _mouseHook = SetHook(WH_MOUSE_LL, _mouseProc);
        }

        private void RemoveMouseHook()
        {
            if (_mouseHook == IntPtr.Zero) return;
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        private void InstallKeyHook()
        {
            if (_keyHook != IntPtr.Zero) return;
            _keyProc = KeyCallback;
            _keyHook = SetHook(WH_KEYBOARD_LL, _keyProc);
        }

        private void RemoveKeyHook()
        {
            if (_keyHook == IntPtr.Zero) return;
            UnhookWindowsHookEx(_keyHook);
            _keyHook = IntPtr.Zero;
        }

        private static IntPtr SetHook(int type, LowLevelProc proc)
        {
            using (var module = Process.GetCurrentProcess().MainModule)
            {
                return SetWindowsHookEx(type, proc, GetModuleHandle(module.ModuleName), 0);
            }
        }

        private IntPtr MouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (int)wParam == WM_LBUTTONDOWN)
            {
                // Pemilihan dijadwalkan ke UI thread, BUKAN dikerjakan di dalam
                // hook. Hook berjalan di jalur input sistem; menahannya dengan
                // pekerjaan berat membuat mouse Windows tersendat, dan Windows
                // akan mencopot hook yang terlalu lama merespons.
                Dispatcher.BeginInvoke(new Action(Select));
                return (IntPtr)1; // telan klik
            }

            if (nCode >= 0 && (int)wParam == WM_LBUTTONUP) return (IntPtr)1;

            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        private IntPtr KeyCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (int)wParam == WM_KEYDOWN)
            {
                var vk = Marshal.ReadInt32(lParam);

                if (vk == VK_F2)
                {
                    Dispatcher.BeginInvoke(new Action(BeginPause));
                    return (IntPtr)1;
                }

                if (vk == VK_ESCAPE)
                {
                    Dispatcher.BeginInvoke(new Action(Cancel));
                    return (IntPtr)1;
                }
            }

            return CallNextHookEx(_keyHook, nCode, wParam, lParam);
        }

        private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

        private const int WH_MOUSE_LL = 14;
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_KEYDOWN = 0x0100;
        private const int VK_F2 = 0x71;
        private const int VK_ESCAPE = 0x1B;

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const uint GA_ROOT = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }
}

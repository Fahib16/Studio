using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace Custom.StudioBridge.Design
{
    /// <summary>
    /// Orkestrasi "Indicate on screen" (elemen) dan "Pick tab" (Attach Tab),
    /// dua-duanya dipakai bareng oleh Designer masing-masing activity.
    /// </summary>
    public static class IndicateHelper
    {
        public class IndicateResult
        {
            public int TabId { get; set; }
            public string Selector { get; set; }
            public string TagName { get; set; }
            public string PreviewText { get; set; }

            /// <summary>
            /// Screenshot area elemen (base64 PNG), sudah di-crop sisi
            /// extension (OffscreenCanvas di background.js). Bisa null kalau
            /// capture gagal -- itu TIDAK fatal, Indicate tetap dianggap
            /// sukses tanpa screenshot.
            /// </summary>
            public string ScreenshotBase64 { get; set; }
        }

        /// <summary>
        /// Halaman internal browser dan halaman milik extension lain
        /// (chrome-extension://, chrome://, edge://, about:, devtools://)
        /// TIDAK BISA disuntik script apa pun izinnya — itu batasan Chrome.
        /// Menampilkannya sebagai pilihan cuma menghasilkan dialog yang tidak
        /// perlu, dan kalau terpilih pasti gagal.
        ///
        /// Kasus nyata yang memicu ini: extension UiPath yang terpasang punya
        /// halamannya sendiri, sehingga dialog "Pilih Tab" selalu muncul
        /// walaupun tab web yang sesungguhnya cuma satu.
        /// </summary>
        private static bool IsAutomatable(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("file://", StringComparison.OrdinalIgnoreCase);
        }

        private static List<TabPickerItem> GetTabItems()
        {
            var tabsResult = StudioPipeClient.SendCommand("listTabs");
            var tabsArray = tabsResult as JArray ?? new JArray();

            return tabsArray
                .Where(t => IsAutomatable(t["url"]?.Value<string>()))
                .Select(t => new TabPickerItem
                {
                    Id = t["id"]?.Value<int>() ?? 0,
                    Url = t["url"]?.Value<string>(),
                    Title = t["title"]?.Value<string>(),
                    Display = $"{t["title"]?.Value<string>()} — {t["url"]?.Value<string>()}"
                }).ToList();
        }

        public static IndicateResult Run()
        {
            // Handle jendela Studio diambil DI AWAL, selagi Studio masih jadi
            // foreground window.
            var studioWindow = GetStudioWindowHandle();

            // Picker desktop dijalankan LEBIH DULU tanpa bertanya apa pun.
            // Daftar tab TIDAK diambil di sini: memanggil pipe saat browser
            // tertutup berarti menunggu timeout koneksi beberapa detik sebelum
            // apa pun muncul di layar — itu jeda yang terasa setiap kali
            // Indicate ditekan. Tab baru diminta kalau ternyata dibutuhkan.
            // Studio disingkirkan ke belakang supaya tidak menghalangi
            // aplikasi target, sekaligus mencegah kursor yang melintasinya
            // memicu highlight yang tidak diinginkan.
            SendStudioBack(studioWindow);

            // Loop: picker desktop dan picker web bisa saling gantian
            // mengikuti kursor. Sebelumnya peralihan cuma SATU ARAH — begitu
            // masuk jalur web, memindahkan kursor ke Notepad tidak
            // mengembalikan pemilihan ke desktop, dan user terpaksa
            // membatalkan lalu mengulang dari awal.
            DesktopPickerWindow picker;

            while (true)
            {
                picker = new DesktopPickerWindow();
                picker.ShowDialog();

                if (!picker.SwitchToWeb) break;

                RestoreFocus(picker.BrowserWindow);
                System.Threading.Thread.Sleep(150);

                var tabId = ResolveWebTab();
                if (tabId == null) { BringStudioFront(studioWindow); return null; }

                var waiter = new WebIndicateWaitWindow(tabId.Value);
                waiter.ShowDialog();

                if (waiter.PickResult != null)
                {
                    var webResult = BuildWebResult(waiter.PickResult, tabId.Value);
                    BringStudioFront(studioWindow);
                    return webResult;
                }

                // Kursor keluar dari browser: kembali ke picker desktop.
                if (waiter.LeftBrowser) continue;

                // Batal betulan (Esc di halaman).
                BringStudioFront(studioWindow);
                return null;
            }

            if (picker.SelectedElement == null)
            {
                BringStudioFront(studioWindow);
                return null; // dibatalkan
            }

            // Dipotret SEBELUM Studio dipulihkan: aplikasi target masih
            // terlihat di layar pada titik ini. Kalau Studio naik lebih dulu,
            // yang terpotret justru jendela Studio.
            var desktopShot = DesktopSelectorResolver.CaptureElement(picker.SelectedElement);

            BringStudioFront(studioWindow);

            try
            {
                var levels = DesktopSelectorResolver.BuildChain(picker.SelectedElement);
                var nodes = DesktopSelectorResolver.ToSelectorNodes(levels);
                DesktopSelectorResolver.ApplyDesktopDefaults(nodes);

                return new IndicateResult
                {
                    TabId = 0,
                    Selector = SelectorDocument.Build(nodes),
                    TagName = levels.Count > 0 ? levels[levels.Count - 1].NodeName : null,
                    ScreenshotBase64 = desktopShot
                };
            }
            catch (Exception ex)
            {
                BringStudioFront(studioWindow);
                MessageBox.Show("Gagal menyusun selector desktop: " + ex.Message, "Studio Bridge",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        /// <summary>
        /// Tentukan tab mana yang dipakai untuk indicate web.
        ///
        /// Tab aktif TIDAK langsung dipakai: kalau yang aktif kebetulan
        /// halaman New Tab atau chrome://, extension akan menolak menyuntik
        /// script dan user cuma dapat pesan error. Di sini tab yang tidak bisa
        /// diotomasi disaring lebih dulu, lalu:
        ///   - satu tab tersisa  -> dipakai langsung
        ///   - tab aktif termasuk yang bisa dipakai -> itu yang dipilih
        ///   - selain itu -> user memilih sendiri lewat dialog
        ///
        /// Mengembalikan null kalau tidak ada tab yang bisa dipakai atau user
        /// membatalkan.
        /// </summary>
        private static int? ResolveWebTab()
        {
            JArray raw;
            try
            {
                raw = StudioPipeClient.SendCommand("listTabs") as JArray ?? new JArray();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Gagal ambil daftar tab: " + ex.Message, "Studio Bridge",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            var usable = new List<TabPickerItem>();
            var activeIds = new List<int>();

            foreach (var t in raw)
            {
                var url = t["url"]?.Value<string>();
                if (!IsAutomatable(url)) continue;

                var id = t["id"]?.Value<int>() ?? 0;
                var title = t["title"]?.Value<string>();

                usable.Add(new TabPickerItem
                {
                    Id = id,
                    Url = url,
                    Title = title,
                    Display = $"{title} — {url}"
                });

                if (t["active"]?.Value<bool>() ?? false) activeIds.Add(id);
            }

            if (usable.Count == 0)
            {
                MessageBox.Show(
                    "Tidak ada tab yang bisa diotomasi di browser." + Environment.NewLine + Environment.NewLine +
                    "Halaman New Tab, chrome://, dan halaman extension memang tidak bisa disuntik script. " +
                    "Buka dulu halaman web biasa, lalu ulangi.",
                    "Studio Bridge", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            if (usable.Count == 1) return usable[0].Id;

            foreach (var item in usable)
                if (activeIds.Contains(item.Id)) return item.Id;

            var picker = new TabPickerWindow(usable);
            if (picker.ShowDialog() != true) return null;
            return picker.SelectedTab.Id;
        }

        /// <summary>
        /// Susun IndicateResult dari hasil mentah picker web.
        ///
        /// Pengambilan hasilnya sendiri dilakukan WebIndicateWaitWindow, yang
        /// menjalankan StartIndicate di task terpisah supaya kursor tetap bisa
        /// dipantau selama menunggu.
        /// </summary>
        private static IndicateResult BuildWebResult(JObject pickResult, int tabId)
        {
            var shot = pickResult["screenshotBase64"]?.Value<string>();

            // Selector XML disusun dari rantai leluhur memakai aturan default
            // yang sama dengan UI Explorer, supaya hasil Indicate langsung dan
            // hasil lewat Explorer konsisten. Kalau extension belum mengirim
            // levels (versi lama), jatuh kembali ke css path — selector CSS
            // tetap didukung runtime.
            string selector = null;
            var levels = pickResult["levels"] as JArray;
            if (levels != null && levels.Count > 0)
            {
                var nodes = levels.Select(SelectorNode.FromJson).ToList();
                SelectorDocument.ApplyDefaults(nodes);
                selector = SelectorDocument.Build(nodes);
            }
            if (string.IsNullOrWhiteSpace(selector))
                selector = pickResult["selector"]?.Value<string>();

            if (string.IsNullOrEmpty(shot))
            {
                var shotError = pickResult["screenshotError"]?.Value<string>();
                if (!string.IsNullOrEmpty(shotError))
                {
                    MessageBox.Show(
                        "Selector berhasil diambil, tapi screenshot gagal:" +
                        Environment.NewLine + Environment.NewLine + shotError,
                        "Studio Bridge", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            return new IndicateResult
            {
                TabId = tabId,
                Selector = selector,
                TagName = pickResult["tagName"]?.Value<string>(),
                PreviewText = pickResult["text"]?.Value<string>(),
                ScreenshotBase64 = shot
            };
        }

        /// <summary>
        /// Buka UI Explorer untuk menyunting selector yang sudah ada.
        ///
        /// Tab dipilih dengan cara yang sama seperti Run(), tapi TIDAK ada
        /// tahap "klik elemen di layar" — user masuk langsung ke Explorer dan
        /// menelusuri pohon, karena selectornya biasanya sudah ada dan yang
        /// mau dilakukan justru memperbaikinya.
        ///
        /// Selector desktop dibuka dalam mode desktop (pohon UI Automation),
        /// selector web dalam mode web (pohon DOM lewat extension).
        ///
        /// Return null kalau dibatalkan di langkah manapun.
        /// </summary>
        public static IndicateResult EditSelector(string currentSelector)
        {
            var kind = SelectorKindDetector.Detect(currentSelector);

            // Selector desktop dibuka dengan UI Explorer mode desktop: pohonnya
            // dibaca lewat UI Automation, bukan lewat extension browser.
            if (kind == SelectorKind.Desktop)
            {
                var desktopExplorer = new UiExplorerWindow(0, currentSelector, desktopMode: true);
                if (desktopExplorer.ShowDialog() != true) return null;

                return new IndicateResult
                {
                    TabId = 0,
                    Selector = desktopExplorer.ResultSelector,
                    ScreenshotBase64 = desktopExplorer.ResultScreenshotBase64
                };
            }

            var tabId = ResolveWebTab();
            if (tabId == null) return null;

            var explorer = new UiExplorerWindow(tabId.Value, currentSelector);
            if (explorer.ShowDialog() != true) return null;

            return new IndicateResult
            {
                TabId = tabId.Value,
                Selector = explorer.ResultSelector,

                // Null kalau user tidak menekan Indicate di dalam Explorer.
                // Pemanggil MEMBIARKAN screenshot lama kalau nilainya null,
                // bukan menimpanya jadi kosong.
                ScreenshotBase64 = explorer.ResultScreenshotBase64
            };
        }

        public static TabPickerItem PickTabForAttach()
        {
            try
            {
                var items = GetTabItems();

                if (items.Count == 0)
                {
                    MessageBox.Show("Tidak ada tab yang terbuka.", "Studio Bridge",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }

                var picker = new TabPickerWindow(items);
                if (picker.ShowDialog() != true) return null;

                return picker.SelectedTab;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal ambil daftar tab: {ex.Message}", "Studio Bridge",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        #region Kembalikan fokus ke jendela Studio

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        private const int SW_RESTORE = 9;

        /// <summary>
        /// Minimalkan jendela Studio selama pemilihan elemen berlangsung.
        /// </summary>
        /// <summary>
        /// Jendela Studio yang sedang diminimalkan selama Indicate berlangsung.
        /// Disimpan sebagai objek Window, bukan cuma handle: memulihkan jendela
        /// WPF milik sendiri jauh lebih andal lewat WindowState + Activate()
        /// daripada lewat ShowWindow/SetForegroundWindow pada HWND mentah, yang
        /// kerap gagal diam-diam karena aturan anti window-hijack Windows.
        /// </summary>
        /// <summary>
        /// Jendela Studio yang sedang disingkirkan selama Indicate berlangsung.
        /// </summary>
        private static Window _studioWindow;

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private static readonly IntPtr HWND_TOP = IntPtr.Zero;
        private static readonly IntPtr HWND_BOTTOM = (IntPtr)1;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        /// <summary>
        /// Singkirkan jendela Studio ke BELAKANG semua jendela lain.
        ///
        /// SENGAJA BUKAN minimize: OpenRPA menangkap peristiwa minimize dan
        /// menyembunyikan diri ke system tray, sehingga jendelanya tidak bisa
        /// dimunculkan lagi secara terprogram — user harus mengklik ikon tray
        /// manual, dan itu memutus alur Indicate.
        ///
        /// Dikirim ke belakang memberi hasil yang sama bagi user (Studio tidak
        /// menghalangi aplikasi target) tanpa memicu perilaku tray sama sekali.
        /// </summary>
        private static void SendStudioBack(IntPtr window)
        {
            _studioWindow = FindWindowByHandle(window);
            if (window == IntPtr.Zero) return;

            try
            {
                SetWindowPos(window, HWND_BOTTOM, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Kembalikan jendela Studio ke depan setelah pemilihan elemen selesai
        /// — baik karena elemen terpilih, dibatalkan dengan Esc, maupun gagal.
        ///
        /// Windows MENOLAK proses yang bukan foreground merebut fokus, jadi
        /// SetWindowPos(HWND_TOP) + Activate() saja kerap gagal diam-diam:
        /// jendela Studio tetap tertinggal di belakang browser, dan user harus
        /// mencarinya sendiri di taskbar. Yang dipakai di sini gabungan tiga
        /// hal yang saling menutupi:
        ///
        ///   1. kedipan TOPMOST -> NOTOPMOST, yang mengangkat jendela tanpa
        ///      perlu izin fokus,
        ///   2. RestoreFocus, yang menempel sebentar ke input queue jendela
        ///      foreground supaya perpindahan fokus diizinkan,
        ///   3. Activate() pada objek Window-nya, yang mengurus sisi WPF.
        /// </summary>
        private static void BringStudioFront(IntPtr window)
        {
            var studio = _studioWindow;
            _studioWindow = null;

            if (window != IntPtr.Zero)
            {
                try
                {
                    if (IsIconic(window)) ShowWindow(window, SW_RESTORE);

                    // Kedipan topmost: cara paling andal mengangkat jendela
                    // sendiri ke atas tanpa tersandung perlindungan fokus.
                    SetWindowPos(window, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                    SetWindowPos(window, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                    SetWindowPos(window, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
                }
                catch (Exception) { }
            }

            RestoreFocus(window);

            if (studio != null)
            {
                try
                {
                    if (studio.WindowState == WindowState.Minimized)
                        studio.WindowState = WindowState.Normal;

                    studio.Activate();
                    studio.Focus();
                }
                catch (Exception) { }
            }
        }

        private static Window FindWindowByHandle(IntPtr handle)
        {
            if (handle == IntPtr.Zero || Application.Current == null) return null;

            try
            {
                foreach (Window w in Application.Current.Windows)
                {
                    if (new System.Windows.Interop.WindowInteropHelper(w).Handle == handle) return w;
                }
            }
            catch (Exception) { }

            return null;
        }

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        private static IntPtr GetStudioWindowHandle()
        {
            try
            {
                var windows = Application.Current != null ? Application.Current.Windows : null;
                if (windows != null)
                {
                    foreach (Window w in windows)
                    {
                        if (w.IsActive)
                        {
                            var h = new System.Windows.Interop.WindowInteropHelper(w).Handle;
                            if (h != IntPtr.Zero) return h;
                        }
                    }
                }

                return Process.GetCurrentProcess().MainWindowHandle;
            }
            catch (Exception)
            {
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// Rebut kembali fokus OS untuk sebuah jendela Studio Bridge.
        /// Dipakai UI Explorer setelah highlight/indicate, karena browser baru
        /// saja dibawa ke depan.
        /// </summary>
        public static void FocusWindow(Window window)
        {
            if (window == null) return;
            try
            {
                var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                RestoreFocus(handle);
            }
            catch (Exception) { }
        }

        /// <summary>
        /// SetForegroundWindow() SAJA sering gagal diam-diam: Windows menolak
        /// proses yang bukan foreground merebut fokus (perlindungan anti
        /// window-hijack). Trik standarnya AttachThreadInput ke thread window
        /// yang sedang foreground (browser) sebentar, supaya Windows
        /// menganggap kita satu input queue dengannya dan mengizinkan
        /// perpindahan fokus, lalu dilepas lagi.
        /// </summary>
        public static void RestoreFocus(IntPtr studioWindow)
        {
            if (studioWindow == IntPtr.Zero) return;

            try
            {
                if (IsIconic(studioWindow))
                    ShowWindow(studioWindow, SW_RESTORE);

                var foreground = GetForegroundWindow();
                if (foreground == studioWindow) return;

                uint foregroundThread = GetWindowThreadProcessId(foreground, IntPtr.Zero);
                uint currentThread = GetCurrentThreadId();

                if (foregroundThread != 0 && foregroundThread != currentThread)
                {
                    AttachThreadInput(currentThread, foregroundThread, true);
                    SetForegroundWindow(studioWindow);
                    AttachThreadInput(currentThread, foregroundThread, false);
                }
                else
                {
                    SetForegroundWindow(studioWindow);
                }
            }
            catch (Exception)
            {
                // Fokus gagal balik bukan alasan melempar error ke user.
            }
        }

        #endregion
    }
}

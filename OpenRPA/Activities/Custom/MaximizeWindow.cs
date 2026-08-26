using System;
using System.Activities;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using OpenRPA.Interfaces;

namespace OpenRPA.Activities.Custom
{
    public enum TitleMatchMode
    {
        Contains,
        Exact,
        StartsWith
    }

    /// <summary>
    /// Activity kustom: Maximize Window ala UiPath untuk OpenRPA.
    ///
    /// BEDA PENDEKATAN dari Click/TypeInto: activity ini SENGAJA TIDAK pakai
    /// IElement/WindowsSelector/NMSelector sama sekali. Alasannya: saya tidak
    /// punya bukti nyata (belum pernah kelihatan di source manapun yang kamu
    /// share) bahwa IElement/UIElement OpenRPA punya method/property untuk
    /// maximize atau ekstrak native window handle (HWND) -- daripada
    /// menebak lagi (sudah beberapa kali salah tebak API OpenRPA), saya pakai
    /// Windows API standar (user32.dll) langsung, cari window lewat JUDUL.
    ///
    /// KEUNTUNGAN pendekatan ini: window aplikasi desktop dan window browser
    /// SAMA-SAMA cuma window OS biasa yang punya judul -- jadi satu logic ini
    /// otomatis jalan untuk keduanya, tanpa perlu jalur Windows/Web terpisah
    /// sama sekali (beda dari Click/TypeInto yang harus dipisah karena target-
    /// nya konten DI DALAM window, bukan window itu sendiri).
    ///
    /// KETERBATASAN: karena berbasis judul (bukan Selector), tidak bisa
    /// pakai Anchor atau fitur pencarian elemen lain yang sudah kita bangun
    /// di Click v4. Kalau ada banyak window dengan judul mirip, yang
    /// ke-maximize adalah window PERTAMA yang cocok kriteria (urutan dari
    /// EnumWindows, biasanya sesuai Z-order/urutan terakhir aktif).
    /// </summary>
    [Designer(typeof(Design.MaximizeWindowDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    public class MaximizeWindow : CodeActivity
    {
        public MaximizeWindow()
        {
            // Default merujuk ke variabel scope "browser" -- otomatis terisi
            // KALAU activity ini di-drop di dalam Body/Do milik Open Browser
            // yang di-drag FRESH dari toolbox (lihat OpenBrowser.Create()).
            // Pola ini SAMA PERSIS dengan ClickElement.cs asli yang default
            // Element = "item" saat nested di GetElement -- termasuk trade-
            // off yang sama: kalau dipakai BERDIRI SENDIRI (bukan di dalam
            // Open Browser), field ini akan tampil tanda seru merah karena
            // "browser" tidak dikenal di scope itu. Itu bukan bug, tinggal
            // kosongkan/isi manual kalau memang dipakai berdiri sendiri.
            Tab = new InArgument<NativeMessagingMessageTab>()
            {
                Expression = new Microsoft.VisualBasic.Activities.VisualBasicValue<NativeMessagingMessageTab>("browser")
            };
        }

        [Category("Input")]
        [DisplayName("Tab")]
        [Description("PALING DIREKOMENDASIKAN kalau ditaruh setelah/di dalam Open Browser: isi dengan " +
                      "Output \"Browser\" dari Open Browser (langsung nama variabelnya, atau nama delegate " +
                      "argument kalau di-nest di dalam Body-nya). Paling presisi karena langsung merujuk " +
                      "tab yang BENERAN baru dibuka, tidak nebak lewat judul/proses/fokus OS sama sekali.")]
        public InArgument<NativeMessagingMessageTab> Tab { get; set; }

        [Category("Input")]
        [DisplayName("Window Title")]
        [Description("Opsional. Kosongkan untuk maximize window yang SEDANG AKTIF (foreground) saat ini -- " +
                      "cocok ditaruh langsung setelah Open Browser/Attach Window/Attach Browser, tanpa perlu setting apa pun, " +
                      "persis seperti UiPath. Isi kalau mau target window tertentu berdasarkan judul.")]
        public InArgument<string> WindowTitle { get; set; }

        [Category("Input")]
        [DisplayName("Title Match Mode")]
        [DefaultValue(TitleMatchMode.Contains)]
        public TitleMatchMode TitleMatchMode { get; set; } = TitleMatchMode.Contains;

        [Category("Input")]
        [DisplayName("Process Name")]
        [Description("Opsional. Batasi pencarian ke proses tertentu (mis. \"chrome\", \"notepad\", tanpa .exe)")]
        public InArgument<string> ProcessName { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        public InArgument<bool> ContinueOnError { get; set; }

        // ----- Win32 API -----

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const int SW_MAXIMIZE = 3;

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = ContinueOnError != null ? ContinueOnError.Get(context) : false;

            try
            {
                var tab = Tab != null ? Tab.Get(context) : null;
                var titlePattern = WindowTitle != null ? WindowTitle.Get(context) : null;
                var processName = ProcessName != null ? ProcessName.Get(context) : null;
                var matchMode = TitleMatchMode;

                // Kalau Tab diisi (dari Output Open Browser), pakai judulnya
                // langsung -- ini paling presisi, prioritas di atas WindowTitle
                // manual maupun ProcessName.
                if (tab != null && !string.IsNullOrEmpty(tab.title))
                {
                    titlePattern = tab.title;
                    matchMode = TitleMatchMode.Contains; // judul tab browser kadang beda dikit dari judul window OS (mis. ada "- Google Chrome" di belakang)
                }

                IntPtr found = IntPtr.Zero;

                if (!string.IsNullOrEmpty(titlePattern) || !string.IsNullOrEmpty(processName))
                {
                    // ----- Jalur pasti: cari lewat judul dan/atau nama proses.
                    // LEBIH RELIABLE daripada GetForegroundWindow(), karena tidak
                    // bergantung window mana yang sedang punya fokus OS saat itu
                    // (yang bisa jadi Visual Studio/Studio itu sendiri kalau kamu
                    // test langsung dari situ, bukan window target yang dimaksud). -----
                    found = FindWindowByTitleOrProcess(titlePattern, matchMode, processName);

                    if (found == IntPtr.Zero)
                        throw new InvalidOperationException(
                            $"Maximize Window: window tidak ditemukan (Title: \"{titlePattern}\", " +
                            $"Match: {matchMode}, Process: \"{processName}\").");
                }
                else
                {
                    // ----- Fallback: keduanya kosong, pakai window yang sedang
                    // aktif (foreground) saat ini. CATATAN: kalau workflow ini
                    // ditest LANGSUNG dari Visual Studio/Studio (klik Run/Debug),
                    // window yang "aktif" menurut OS bisa jadi Studio itu sendiri,
                    // BUKAN window target -- isi ProcessName untuk hasil yang
                    // konsisten baik saat testing maupun dijalankan sungguhan. -----
                    found = GetForegroundWindow();

                    if (found == IntPtr.Zero)
                        throw new InvalidOperationException(
                            "Maximize Window: tidak ada window yang sedang aktif (foreground) saat ini.");
                }

                ShowWindow(found, SW_MAXIMIZE);
            }
            catch (Exception) when (continueOnError)
            {
                // Telan error kalau ContinueOnError = true
            }
        }

        /// <summary>
        /// Cari window pertama yang cocok Title (kalau diisi) DAN/ATAU Process
        /// (kalau diisi). Kalau titlePattern kosong tapi processName diisi,
        /// window PERTAMA milik proses itu langsung dipakai (tidak perlu match
        /// judul apa pun) -- ini yang bikin "maximize window Edge/Chrome" jalan
        /// stabil tanpa bergantung fokus OS.
        /// </summary>
        private static IntPtr FindWindowByTitleOrProcess(string titlePattern, TitleMatchMode matchMode, string processName)
        {
            IntPtr found = IntPtr.Zero;

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true; // lanjut cari

                if (!string.IsNullOrEmpty(titlePattern))
                {
                    int length = GetWindowTextLength(hWnd);
                    if (length == 0) return true;

                    var sb = new StringBuilder(length + 1);
                    GetWindowText(hWnd, sb, sb.Capacity);
                    string title = sb.ToString();

                    bool titleMatches;
                    switch (matchMode)
                    {
                        case TitleMatchMode.Exact:
                            titleMatches = string.Equals(title, titlePattern, StringComparison.OrdinalIgnoreCase);
                            break;
                        case TitleMatchMode.StartsWith:
                            titleMatches = title.StartsWith(titlePattern, StringComparison.OrdinalIgnoreCase);
                            break;
                        default: // Contains
                            titleMatches = title.IndexOf(titlePattern, StringComparison.OrdinalIgnoreCase) >= 0;
                            break;
                    }

                    if (!titleMatches) return true;
                }

                if (!string.IsNullOrEmpty(processName))
                {
                    try
                    {
                        GetWindowThreadProcessId(hWnd, out uint pid);
                        var proc = Process.GetProcessById((int)pid);
                        if (!string.Equals(proc.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                            return true; // lanjut cari, proses tidak cocok
                    }
                    catch
                    {
                        return true; // proses sudah exit / tidak bisa diakses, lanjut cari
                    }
                }

                found = hWnd;
                return false; // ketemu, stop enumerasi
            }, IntPtr.Zero);

            return found;
        }
    }
}

using System;
using System.Activities;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

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
                var titlePattern = WindowTitle != null ? WindowTitle.Get(context) : null;

                IntPtr found;

                if (string.IsNullOrEmpty(titlePattern))
                {
                    // ----- BARU: tanpa WindowTitle, maximize window yang SEDANG
                    // AKTIF (foreground). Ini yang bikin activity ini bisa
                    // dipakai TANPA setting apa pun setelah Open Browser/
                    // Attach Window/Attach Browser -- persis pengalaman UiPath,
                    // karena activity-activity itu biasanya sudah bikin window
                    // targetnya jadi foreground duluan sebelum Maximize Window
                    // dijalankan. -----
                    found = GetForegroundWindow();
                }
                else
                {
                    var processName = ProcessName != null ? ProcessName.Get(context) : null;
                    var matchMode = TitleMatchMode;
                    found = IntPtr.Zero;

                    EnumWindows((hWnd, lParam) =>
                {
                    if (!IsWindowVisible(hWnd)) return true; // lanjut cari

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

                    if (found == IntPtr.Zero)
                        throw new InvalidOperationException(
                            $"Maximize Window: window dengan judul \"{titlePattern}\" ({matchMode}) tidak ditemukan.");
                }

                if (found == IntPtr.Zero)
                    throw new InvalidOperationException(
                        "Maximize Window: tidak ada window yang sedang aktif (foreground) saat ini.");

                ShowWindow(found, SW_MAXIMIZE);
            }
            catch (Exception) when (continueOnError)
            {
                // Telan error kalau ContinueOnError = true
            }
        }
    }
}

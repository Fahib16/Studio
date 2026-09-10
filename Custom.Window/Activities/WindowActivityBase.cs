using System;
using System.Activities;
using System.ComponentModel;
using OpenRPA.Interfaces;

namespace Custom.Window
{
    /// <summary>
    /// Bagian yang sama untuk semua activity yang menyasar sebuah window:
    /// cara menemukannya.
    ///
    /// Urutan prioritas saat menentukan target:
    ///   1. Selector Studio (hasil Indicate) — paling presisi, dan satu-
    ///      satunya yang tidak bergantung judul jendela yang berubah-ubah.
    ///   2. Tab (output Open Browser / Attach Browser) — judulnya dipakai.
    ///   3. Window Title dan/atau Process Name.
    ///   4. Kalau semuanya kosong: window yang SEDANG AKTIF.
    ///
    /// Butir 4 sengaja dipertahankan dari MaximizeWindow versi lama karena
    /// itulah yang membuat "buka aplikasi lalu maximize" bisa ditulis tanpa
    /// mengisi apa pun. Perlu diingat saat menguji dari Studio: window aktif
    /// bisa jadi Studio itu sendiri, jadi isi Process Name kalau hasilnya
    /// harus konsisten.
    /// </summary>
    public abstract class WindowActivityBase : CodeActivity
    {
        [Category("Target")]
        [DisplayName("Selector")]
        [Description("Selector Studio (hasil tombol Indicate). Cara PALING PRESISI menunjuk jendela: " +
                     "kalau yang ditunjuk ternyata kontrol di dalam jendela, activity ini naik sendiri " +
                     "ke jendela tingkat atasnya. Kosongkan untuk memakai judul/proses.")]
        public InArgument<string> Selector { get; set; }

        /// <summary>Screenshot elemen hasil Indicate, hanya untuk kartu di canvas.</summary>
        [Browsable(false)]
        public string ScreenshotBase64 { get; set; }

        [Category("Target")]
        [DisplayName("Window Title")]
        [Description("Judul window yang dicari. Kosong berarti memakai window yang sedang aktif " +
                     "(kecuali Tab atau Process Name diisi).")]
        public InArgument<string> WindowTitle { get; set; }

        [Category("Target")]
        [DisplayName("Title Match Mode")]
        [Description("Contains (default), Exact, StartsWith, atau Wildcard (* dan ?).")]
        [DefaultValue(TitleMatchMode.Contains)]
        public TitleMatchMode TitleMatchMode { get; set; } = TitleMatchMode.Contains;

        [Category("Target")]
        [DisplayName("Process Name")]
        [Description("Opsional. Batasi ke proses tertentu, mis. chrome atau notepad (tanpa .exe).")]
        public InArgument<string> ProcessName { get; set; }

        [Category("Target")]
        [DisplayName("Tab")]
        [Description("Opsional. Output Browser dari Open Browser / Attach Browser; judul tab-nya " +
                     "dipakai untuk menemukan window. Paling presisi kalau targetnya browser.")]
        public InArgument<NativeMessagingMessageTab> Tab { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [Description("Kalau true, kegagalan activity ini tidak menghentikan workflow (default false).")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        protected bool GetContinueOnError(CodeActivityContext context)
        {
            return ContinueOnError != null && ContinueOnError.Get(context);
        }

        /// <summary>
        /// Temukan window target. Melempar kalau tidak ketemu — pemanggil yang
        /// memutuskan apakah itu ditelan ContinueOnError.
        /// </summary>
        protected IntPtr FindWindow(CodeActivityContext context)
        {
            // Selector Studio didahulukan: itu penunjuk paling tepat, dan
            // satu-satunya yang tidak bergantung pada judul jendela yang bisa
            // berubah-ubah.
            var selector = Selector != null ? Selector.Get(context) : null;
            if (!string.IsNullOrWhiteSpace(selector))
            {
                var element = Custom.StudioBridge.DesktopActions.WaitFor(selector, TimeSpan.FromSeconds(10));

                IntPtr handle;
                if (!element.Properties.NativeWindowHandle.TryGetValue(out handle) || handle == IntPtr.Zero)
                    throw new InvalidOperationException(
                        "Elemen yang ditunjuk selector tidak punya handle jendela, jadi tidak bisa " +
                        "dikendalikan sebagai jendela. Pilih jendelanya (bukan isinya) lewat Indicate.");

                return WindowFinder.RootOf(handle);
            }

            var title = WindowTitle != null ? WindowTitle.Get(context) : null;
            var processName = ProcessName != null ? ProcessName.Get(context) : null;
            var mode = TitleMatchMode;

            var tab = Tab != null ? Tab.Get(context) : null;
            if (tab != null && !string.IsNullOrEmpty(tab.title))
            {
                title = tab.title;

                // Judul window browser biasanya judul tab + nama browser
                // ("... - Google Chrome"), jadi pencocokan persis tidak akan
                // pernah kena.
                mode = TitleMatchMode.Contains;
            }

            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(processName))
            {
                var active = WindowFinder.Foreground();
                if (active == IntPtr.Zero)
                    throw new InvalidOperationException("Tidak ada window yang sedang aktif.");
                return active;
            }

            var found = WindowFinder.Find(title, mode, processName);
            if (found == IntPtr.Zero)
                throw new InvalidOperationException(
                    "Window tidak ditemukan (Title: \"" + title + "\", Match: " + mode +
                    ", Process: \"" + processName + "\").");

            return found;
        }
    }
}

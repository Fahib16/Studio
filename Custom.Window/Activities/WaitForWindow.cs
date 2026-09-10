using System;
using System.Activities;
using System.ComponentModel;
using System.Threading;

namespace Custom.Window
{
    /// <summary>
    /// Menunggu sebuah window muncul.
    ///
    /// Judulnya mendukung wildcard lewat Title Match Mode = Wildcard
    /// (* banyak karakter, ? satu karakter) — dibutuhkan karena judul window
    /// sering memuat bagian yang berubah-ubah, mis. "Laporan 2026-09 - Excel".
    ///
    /// Berbeda dari activity lain di project ini, judul di sini WAJIB diisi:
    /// menunggu "window aktif" tidak berarti apa-apa, karena selalu ada window
    /// yang aktif.
    ///
    /// Penungguannya memblokir thread workflow (pola yang sama dengan Wait For
    /// File dan Wait For Terminal Text), jadi tombol Stop baru terasa setelah
    /// timeout.
    /// </summary>
    [Designer(typeof(Design.WaitForWindowDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Wait For Window")]
    [Description("Menunggu sampai window dengan judul tertentu muncul.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.waitforwindow.png")]
    public sealed class WaitForWindow : CodeActivity
    {
        private const int PollMs = 250;

        public WaitForWindow()
        {
            DisplayName = "Wait For Window";
        }

        [Category("Target")]
        [RequiredArgument]
        [DisplayName("Window Title")]
        [Description("Judul window yang ditunggu. Mendukung * dan ? kalau Title Match Mode = Wildcard.")]
        public InArgument<string> WindowTitle { get; set; }

        [Category("Target")]
        [DisplayName("Title Match Mode")]
        [Description("Contains (default), Exact, StartsWith, atau Wildcard.")]
        [DefaultValue(TitleMatchMode.Contains)]
        public TitleMatchMode TitleMatchMode { get; set; } = TitleMatchMode.Contains;

        [Category("Target")]
        [DisplayName("Process Name")]
        [Description("Opsional. Batasi ke proses tertentu (tanpa .exe).")]
        public InArgument<string> ProcessName { get; set; }

        [Category("Options")]
        [DisplayName("Timeout")]
        [Description("Batas waktu menunggu (default 30 detik kalau kosong).")]
        public InArgument<TimeSpan> Timeout { get; set; }

        [Category("Options")]
        [DisplayName("Bring To Front")]
        [Description("Kalau true, window yang ketemu langsung dijadikan window aktif (default false).")]
        public InArgument<bool> BringToFront { get; set; }

        [Category("Options")]
        [DisplayName("Throw On Timeout")]
        [Description("True (default): lempar exception kalau timeout. False: cuma isi Found = false.")]
        [DefaultValue(true)]
        public bool ThrowOnTimeout { get; set; } = true;

        [Category("Output")]
        [DisplayName("Found")]
        public OutArgument<bool> Found { get; set; }

        [Category("Output")]
        [DisplayName("Handle")]
        public OutArgument<IntPtr> Handle { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [Description("Kalau true, kegagalan activity ini tidak menghentikan workflow (default false).")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = ContinueOnError != null && ContinueOnError.Get(context);

            try
            {
                var title = WindowTitle.Get(context);
                if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Window Title kosong.");

                var processName = ProcessName != null ? ProcessName.Get(context) : null;

                var timeout = Timeout != null ? Timeout.Get(context) : TimeSpan.Zero;
                if (timeout <= TimeSpan.Zero) timeout = TimeSpan.FromSeconds(30);

                var deadline = DateTime.UtcNow + timeout;
                var hWnd = IntPtr.Zero;

                while (DateTime.UtcNow < deadline)
                {
                    hWnd = WindowFinder.Find(title, TitleMatchMode, processName);
                    if (hWnd != IntPtr.Zero) break;
                    Thread.Sleep(PollMs);
                }

                var found = hWnd != IntPtr.Zero;

                if (found && BringToFront != null && BringToFront.Get(context))
                    WindowFinder.Focus(hWnd);

                if (Found != null) Found.Set(context, found);
                if (Handle != null) Handle.Set(context, hWnd);

                if (!found && ThrowOnTimeout)
                    throw new TimeoutException(
                        "Wait For Window: window dengan judul \"" + title + "\" tidak muncul dalam " +
                        timeout.TotalSeconds + " detik.");
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

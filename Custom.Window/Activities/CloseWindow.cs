using System;
using System.Activities;
using System.ComponentModel;
using System.Threading;

namespace Custom.Window
{
    /// <summary>
    /// Menutup sebuah window.
    ///
    /// Yang dikirim adalah WM_CLOSE — permintaan menutup, sama seperti user
    /// mengklik tombol X. Aplikasi masih sempat menyimpan dan boleh
    /// menampilkan dialog "simpan perubahan?". Activity ini SENGAJA TIDAK
    /// mematikan proses (Process.Kill), karena itu akan membuang pekerjaan
    /// yang belum tersimpan tanpa peringatan; untuk mematikan aplikasi secara
    /// paksa, OpenRPA sudah punya activity Close Application.
    ///
    /// Wait For Close (default true) menunggu window benar-benar hilang,
    /// supaya activity berikutnya tidak berjalan saat dialog konfirmasi masih
    /// terbuka.
    /// </summary>
    [Designer(typeof(Design.CloseWindowDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Close Window")]
    [Description("Menutup window (WM_CLOSE), bukan mematikan prosesnya.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.closewindow.png")]
    public sealed class CloseWindow : WindowActivityBase
    {
        public CloseWindow()
        {
            DisplayName = "Close Window";
            WaitForClose = new InArgument<bool>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<bool>("True"));
        }

        [Category("Options")]
        [DisplayName("Wait For Close")]
        [Description("True (default): tunggu sampai window benar-benar hilang.")]
        public InArgument<bool> WaitForClose { get; set; }

        [Category("Options")]
        [DisplayName("Timeout")]
        [Description("Batas menunggu window hilang (default 10 detik kalau kosong).")]
        public InArgument<TimeSpan> Timeout { get; set; }

        [Category("Output")]
        [DisplayName("Closed")]
        [Description("True kalau window sudah hilang saat activity selesai.")]
        public OutArgument<bool> Closed { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = GetContinueOnError(context);

            try
            {
                var hWnd = FindWindow(context);
                WindowFinder.Close(hWnd);

                var wait = WaitForClose == null || WaitForClose.Get(context);
                var closed = true;

                if (wait)
                {
                    var timeout = Timeout != null ? Timeout.Get(context) : TimeSpan.Zero;
                    if (timeout <= TimeSpan.Zero) timeout = TimeSpan.FromSeconds(10);

                    var deadline = DateTime.UtcNow + timeout;
                    closed = false;

                    while (DateTime.UtcNow < deadline)
                    {
                        // Judul kosong berarti window sudah tidak ada lagi.
                        if (string.IsNullOrEmpty(WindowFinder.TitleOf(hWnd))) { closed = true; break; }
                        Thread.Sleep(200);
                    }
                }

                if (Closed != null) Closed.Set(context, closed);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

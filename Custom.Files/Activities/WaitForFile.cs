using System;
using System.Activities;
using System.ComponentModel;
using System.IO;
using System.Threading;

namespace Custom.Files
{
    /// <summary>
    /// Menunggu sebuah berkas muncul — pemakaian utamanya menunggu hasil
    /// unduhan browser.
    ///
    /// Selain menunggu berkasnya ADA, secara default activity ini juga
    /// menunggu berkasnya SELESAI ditulis: ukurannya tidak berubah lagi
    /// selama dua kali pemeriksaan berturut-turut dan berkasnya sudah bisa
    /// dibuka untuk dibaca. Tanpa itu, activity berikutnya sering membaca
    /// berkas unduhan yang baru terisi separuh — kegagalan yang sulit dilacak
    /// karena tidak selalu terjadi.
    ///
    /// Timeout default 30 detik (bukan 10 detik seperti activity lain di
    /// solution ini) karena yang ditunggu adalah unduhan, bukan elemen UI.
    ///
    /// Catatan: penungguan ini MEMBLOKIR thread workflow, sama seperti
    /// Wait For Terminal Text di Custom.Terminal. Konsekuensinya, tombol Stop
    /// baru terasa setelah timeout tercapai.
    /// </summary>
    [Designer(typeof(Design.WaitForFileDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Wait For File")]
    [Description("Menunggu sampai sebuah berkas muncul dan selesai ditulis.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.waitforfile.png")]
    public sealed class WaitForFile : CodeActivity
    {
        private const int PollMs = 300;

        public WaitForFile()
        {
            DisplayName = "Wait For File";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Path")]
        [Description("Berkas yang ditunggu.")]
        public InArgument<string> Path { get; set; }

        [Category("Options")]
        [DisplayName("Timeout")]
        [Description("Batas waktu menunggu (default 30 detik kalau kosong).")]
        public InArgument<TimeSpan> Timeout { get; set; }

        [Category("Options")]
        [DisplayName("Wait Until Stable")]
        [Description("True (default): tunggu juga sampai ukuran berkas berhenti bertambah " +
                     "dan berkasnya bisa dibuka — dipakai untuk berkas unduhan.")]
        [DefaultValue(true)]
        public bool WaitUntilStable { get; set; } = true;

        [Category("Options")]
        [DisplayName("Throw On Timeout")]
        [Description("True (default): lempar exception kalau timeout. False: cuma isi Found = false.")]
        [DefaultValue(true)]
        public bool ThrowOnTimeout { get; set; } = true;

        [Category("Output")]
        [DisplayName("Found")]
        [Description("True kalau berkasnya muncul sebelum timeout.")]
        public OutArgument<bool> Found { get; set; }

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
                var path = Path.Get(context);
                if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path kosong.");

                var timeout = Timeout != null ? Timeout.Get(context) : TimeSpan.Zero;
                if (timeout <= TimeSpan.Zero) timeout = TimeSpan.FromSeconds(30);

                var deadline = DateTime.UtcNow + timeout;
                var found = false;
                long lastSize = -1;

                while (DateTime.UtcNow < deadline)
                {
                    if (File.Exists(path))
                    {
                        if (!WaitUntilStable) { found = true; break; }

                        long size;
                        try { size = new FileInfo(path).Length; }
                        catch (IOException) { size = -1; }   // masih dikunci penulisnya

                        if (size >= 0 && size == lastSize && CanOpenForRead(path))
                        {
                            found = true;
                            break;
                        }

                        lastSize = size;
                    }

                    Thread.Sleep(PollMs);
                }

                if (Found != null) Found.Set(context, found);

                if (!found && ThrowOnTimeout)
                {
                    throw new TimeoutException(
                        "Wait For File: \"" + path + "\" tidak muncul (atau belum selesai ditulis) dalam " +
                        timeout.TotalSeconds + " detik.");
                }
            }
            catch (Exception) when (continueOnError)
            {
            }
        }

        private static bool CanOpenForRead(string path)
        {
            try
            {
                using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)) return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}

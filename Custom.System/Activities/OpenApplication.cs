using System;
using System.Activities;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace CustomSystem
{
    /// <summary>
    /// Menjalankan sebuah program dan, kalau diminta, menunggu jendelanya siap.
    ///
    /// Pengganti Open Application milik OpenRPA.
    ///
    /// Menunggu JENDELA, bukan menunggu selang waktu tetap. Program yang baru
    /// dijalankan belum tentu punya jendela pada milidetik pertama, dan
    /// menunggu "lima detik" hanya bisa dua-duanya salah: kelamaan untuk
    /// program ringan, kurang untuk program berat.
    /// </summary>
    [Designer(typeof(Design.OpenApplicationDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Open Application")]
    [Description("Menjalankan program dan menunggu jendelanya siap.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.openapp.png")]
    public sealed class OpenApplication : CodeActivity
    {
        public OpenApplication()
        {
            DisplayName = "Open Application";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("File Name")]
        [Description("Program yang dijalankan, mis. charmap.exe atau jalur lengkapnya.")]
        public InArgument<string> FileName { get; set; }

        [Category("Input")]
        [DisplayName("Arguments")]
        [Description("Argumen baris perintah.")]
        public InArgument<string> Arguments { get; set; }

        [Category("Input")]
        [DisplayName("Working Directory")]
        [Description("Folder kerja program. Kosong berarti folder program itu sendiri.")]
        public InArgument<string> WorkingDirectory { get; set; }

        [Category("Input")]
        [DisplayName("Wait For Window")]
        [Description("Kalau true, tunggu sampai programnya punya jendela utama (default true).")]
        public InArgument<bool> WaitForWindow { get; set; }

        [Category("Input")]
        [DisplayName("Timeout")]
        [Description("Batas menunggu jendela; default 30 detik.")]
        public InArgument<TimeSpan> Timeout { get; set; }

        [Category("Output")]
        [DisplayName("Process Id")]
        [Description("Nomor proses yang MEMILIKI jendelanya. Untuk program peluncur, " +
                     "ini bukan proses yang dijalankan melainkan yang sesungguhnya tampil.")]
        public OutArgument<int> ProcessId { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [Description("Kalau true, kegagalan tidak menghentikan workflow (default false).")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = ContinueOnError != null && ContinueOnError.Get(context);

            try
            {
                var berkas = FileName.Get(context);
                if (string.IsNullOrWhiteSpace(berkas))
                    throw new InvalidOperationException("Open Application: File Name kosong.");

                var info = new ProcessStartInfo
                {
                    FileName = berkas,
                    Arguments = Arguments != null ? (Arguments.Get(context) ?? "") : "",
                    UseShellExecute = true,
                };

                var folder = WorkingDirectory != null ? WorkingDirectory.Get(context) : null;
                if (!string.IsNullOrWhiteSpace(folder)) info.WorkingDirectory = folder;

                var proses = Process.Start(info);
                if (proses == null)
                    throw new InvalidOperationException("Open Application: " + berkas + " tidak bisa dijalankan.");

                var tunggu = WaitForWindow == null || WaitForWindow.Get(context);

                if (!tunggu)
                {
                    if (ProcessId != null) ProcessId.Set(context, proses.Id);
                    return;
                }

                var batas = Timeout != null ? Timeout.Get(context) : TimeSpan.Zero;
                if (batas <= TimeSpan.Zero) batas = TimeSpan.FromSeconds(30);

                var berjendela = TungguJendela(proses, berkas, batas);

                if (berjendela == null)
                {
                    throw new InvalidOperationException(
                        "Open Application: " + berkas + " berjalan, tapi jendelanya belum muncul setelah "
                        + Math.Round(batas.TotalSeconds, 1) + " detik.");
                }

                if (ProcessId != null) ProcessId.Set(context, berjendela.Id);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }

        /// <summary>
        /// Tunggu sampai ada jendela, lalu kembalikan proses yang BENAR-BENAR
        /// memilikinya. Null kalau tidak ada jendela sampai batas waktu.
        ///
        /// Banyak program modern hanya peluncur: proses yang kita jalankan
        /// langsung keluar, dan jendelanya milik proses lain. Di Windows 11 itu
        /// berlaku untuk Notepad, Kalkulator, dan aplikasi Store lainnya —
        /// Notepad bahkan hanya menambah TAB di jendela yang sudah terbuka.
        ///
        /// Mengembalikan nomor proses peluncur yang sudah mati membuat Close
        /// Application sesudahnya tidak menutup apa pun. Itu persis kegagalan
        /// yang muncul waktu activity ini diuji pertama kali, dan itulah alasan
        /// pencarian pengganti di bawah ada.
        /// </summary>
        private static Process TungguJendela(Process proses, string berkas, TimeSpan batas)
        {
            var akhir = DateTime.UtcNow + batas;
            var nama = Path.GetFileNameWithoutExtension(berkas);

            while (DateTime.UtcNow < akhir)
            {
                if (!proses.HasExited)
                {
                    proses.Refresh();
                    if (proses.MainWindowHandle != IntPtr.Zero) return proses;
                }
                else
                {
                    var pengganti = CariBerjendela(nama);
                    if (pengganti != null) return pengganti;

                    // Peluncur yang sudah keluar dan tidak meninggalkan jendela
                    // bernama sama tetap dianggap BERHASIL: ada program yang
                    // memang hanya meneruskan perintah lalu selesai, dan tidak
                    // ada jendela yang harus ditunggu.
                    return proses;
                }

                Thread.Sleep(100);
            }

            return null;
        }

        /// <summary>Proses bernama sama yang punya jendela; yang paling baru dibuka.</summary>
        private static Process CariBerjendela(string nama)
        {
            Process terbaru = null;

            foreach (var p in Process.GetProcessesByName(nama))
            {
                try
                {
                    if (p.MainWindowHandle == IntPtr.Zero) continue;
                    if (terbaru == null || p.StartTime > terbaru.StartTime) terbaru = p;
                }
                catch (Exception)
                {
                    // Proses milik sesi lain atau tingkat hak yang lebih tinggi:
                    // StartTime-nya tidak bisa dibaca. Lewati saja.
                }
            }

            return terbaru;
        }
    }
}

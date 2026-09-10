using System;
using System.Activities;
using System.ComponentModel;
using System.IO;
using System.Linq;
using OpenRPA.Interfaces;

namespace Custom.Files
{
    /// <summary>
    /// Menunggu sebuah berkas selesai diunduh, lalu memberikan LOKASI LENGKAP
    /// berkas itu.
    ///
    /// Pengganti activity bawaan "Wait For Download" (OpenRPA.NM), yang
    /// disembunyikan dari toolbox. Bedanya:
    ///
    ///   * Bawaannya tidak mengembalikan lokasi berkas hasil unduhan, jadi
    ///     langkah berikutnya tidak tahu berkas mana yang baru turun. Di sini
    ///     hasilnya keluar lewat properti Downloaded File.
    ///   * Bawaannya hanya memantau folder unduhan Chrome lewat extension. Di
    ///     sini folder mana pun bisa dipantau, termasuk unduhan aplikasi
    ///     desktop.
    ///   * Berkas sementara (.crdownload, .tmp, .part, dan yang kamu sebut
    ///     sendiri) diabaikan, dan berkas baru dianggap selesai hanya setelah
    ///     ukurannya berhenti bertambah.
    ///
    /// Cara pakainya: taruh activity yang MEMICU unduhan (mis. Click pada
    /// tombol Download) di dalam kotak "Do". Isi folder ikut dicatat sebelum
    /// activity itu dijalankan, sehingga berkas lama tidak pernah tertukar
    /// dengan hasil unduhan baru.
    /// </summary>
    [Designer(typeof(Design.WaitForDownloadDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Wait For Download")]
    [Description("Menunggu unduhan selesai lalu memberikan lokasi berkasnya.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.waitfordownload.png")]
    public class WaitForDownload : NativeActivity
    {
        public WaitForDownload()
        {
            DisplayName = "Wait For Download";

            // Folder unduhan bawaan Windows, ditulis sebagai ekspresi supaya
            // terbaca dan bisa diganti langsung di kartunya.
            DownloadsFolder = new InArgument<string>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<string>(
                    "System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), \"Downloads\")"));
        }

        /// <summary>Activity yang memicu unduhan; dijalankan setelah isi folder dicatat.</summary>
        [Browsable(false)]
        public Activity Body { get; set; }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Downloads folder")]
        [Description("Folder yang dipantau. Bawaannya folder Downloads milik pengguna Windows.")]
        public InArgument<string> DownloadsFolder { get; set; }

        [Category("Input")]
        [DisplayName("Ignore file extensions")]
        [Description("Daftar akhiran berkas sementara yang diabaikan, dipisah koma, mis. \"tmp,dwn\". " +
                     "Akhiran unduhan yang umum (.crdownload, .part, .partial, .tmp, .download) " +
                     "sudah diabaikan tanpa perlu ditulis.")]
        public InArgument<string> IgnoreFileExtensions { get; set; }

        [Category("Options")]
        [DisplayName("Timeout")]
        [Description("Berapa lama menunggu berkas baru muncul dan selesai (default 60 detik kalau kosong).")]
        public InArgument<TimeSpan> Timeout { get; set; }

        [Category("Options")]
        [DisplayName("Throw On Timeout")]
        [Description("True (bawaan): melempar kesalahan kalau tidak ada unduhan yang selesai. " +
                     "False: Downloaded File dibiarkan kosong.")]
        [DefaultValue(true)]
        public bool ThrowOnTimeout { get; set; } = true;

        [Category("Output")]
        [DisplayName("Downloaded file")]
        [Description("Berkas hasil unduhan sebagai FileInfo. Pakai .FullName untuk lokasi lengkap, " +
                     ".Name untuk nama berkas beserta akhirannya, .Length untuk ukurannya.")]
        public OutArgument<FileInfo> DownloadedFile { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [Description("Kalau true, kegagalan activity ini tidak menghentikan workflow (default false).")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        /// <summary>Isi folder sebelum unduhan dipicu, supaya berkas lama tidak ikut terhitung.</summary>
        private readonly Variable<System.Collections.Generic.HashSet<string>> before =
            new Variable<System.Collections.Generic.HashSet<string>>();

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            // Body SENGAJA tidak didaftarkan lagi di sini.
            //
            // base.CacheMetadata sudah menemukannya sendiri lewat refleksi —
            // properti publik bertipe Activity otomatis jadi anak. Menambahkan
            // AddChild(Body) di atasnya membuat activity yang sama terdaftar
            // DUA KALI, dan WF menolak seluruh workflow sebelum baris pertama
            // dijalankan:
            //
            //   The activity 'Wait For Download' cannot reference activity
            //   'Click ...' because activity 'Click ...' is already referenced
            //   elsewhere in the workflow ...
            //
            // Akibatnya bukan cuma activity ini yang gagal: SATU workflow utuh
            // tidak bisa dijalankan sama sekali begitu memuat Wait For Download.
            base.CacheMetadata(metadata);
            metadata.AddImplementationVariable(before);
        }

        protected override void Execute(NativeActivityContext context)
        {
            var folder = Folder(context);

            before.Set(context, new System.Collections.Generic.HashSet<string>(
                Directory.Exists(folder)
                    ? Directory.GetFiles(folder).Select(Path.GetFileName)
                    : Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase));

            if (Body != null)
            {
                // Jejak ini masuk panel Output dan berkas log harian, jadi
                // urutannya bisa dibaca ulang. Tanpa itu, unduhan yang berjalan
                // diam-diam di balik layar membuat langkah SESUDAHNYA terlihat
                // seperti langkah yang pertama jalan.
                Log.Output("Wait For Download: menjalankan activity pemicu unduhan...");
                context.ScheduleActivity(Body, OnBodyDone);
                return;
            }

            Wait(context);
        }

        private void OnBodyDone(NativeActivityContext context, ActivityInstance instance)
        {
            Log.Output("Wait For Download: pemicu selesai, mulai menunggu berkas turun...");
            Wait(context);
        }

        private string Folder(NativeActivityContext context)
        {
            var folder = DownloadsFolder != null ? DownloadsFolder.Get(context) : null;
            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            }
            return folder;
        }

        /// <summary>
        /// Akhiran berkas yang dianggap "belum selesai". Peramban memakai nama
        /// sementara selama mengunduh, dan berkas itu tidak boleh dilaporkan
        /// sebagai hasil.
        /// </summary>
        private string[] Temporary(NativeActivityContext context)
        {
            var builtin = new[] { ".crdownload", ".part", ".partial", ".tmp", ".download", ".opdownload" };

            var extra = IgnoreFileExtensions != null ? IgnoreFileExtensions.Get(context) : null;
            if (string.IsNullOrWhiteSpace(extra)) return builtin;

            var more = extra.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().TrimStart('*'))
                .Where(x => x.Length > 0)
                .Select(x => x.StartsWith(".") ? x : "." + x);

            return builtin.Concat(more).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private void Wait(NativeActivityContext context)
        {
            var continueOnError = ContinueOnError != null && ContinueOnError.Get(context);

            try
            {
                var folder = Folder(context);
                if (!Directory.Exists(folder))
                    throw new DirectoryNotFoundException("Folder yang dipantau tidak ada: " + folder);

                var timeout = Timeout != null ? Timeout.Get(context) : TimeSpan.Zero;
                if (timeout == TimeSpan.Zero) timeout = TimeSpan.FromSeconds(60);

                var ignored = Temporary(context);
                var known = before.Get(context) ?? new System.Collections.Generic.HashSet<string>();

                var deadline = DateTime.UtcNow + timeout;
                string candidate = null;
                long lastSize = -1;
                var stableSince = DateTime.UtcNow;

                while (DateTime.UtcNow < deadline)
                {
                    var fresh = Directory.GetFiles(folder)
                        .Where(f => !known.Contains(Path.GetFileName(f)))
                        .Where(f => !ignored.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                        .OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc)
                        .FirstOrDefault();

                    if (fresh != null)
                    {
                        var size = new FileInfo(fresh).Length;

                        if (fresh != candidate || size != lastSize)
                        {
                            // Masih tumbuh: mulai hitung diam dari awal lagi.
                            candidate = fresh;
                            lastSize = size;
                            stableSince = DateTime.UtcNow;
                        }
                        else if (DateTime.UtcNow - stableSince > TimeSpan.FromMilliseconds(700) && CanOpen(fresh))
                        {
                            // Ukurannya berhenti berubah DAN berkasnya sudah
                            // tidak dikunci proses lain: unduhan selesai.
                            if (DownloadedFile != null) DownloadedFile.Set(context, new FileInfo(fresh));
                            Log.Output("Wait For Download: berkas selesai diunduh -> " + fresh);
                            return;
                        }
                    }

                    System.Threading.Thread.Sleep(250);
                }

                Log.Warning("Wait For Download: tidak ada berkas baru yang selesai di " + folder +
                            " dalam " + timeout.TotalSeconds + " detik.");

                if (ThrowOnTimeout)
                {
                    throw new TimeoutException(
                        "Tidak ada unduhan yang selesai di " + folder + " dalam " +
                        timeout.TotalSeconds + " detik.");
                }

                if (DownloadedFile != null) DownloadedFile.Set(context, (FileInfo)null);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }

        /// <summary>
        /// Berkas yang masih ditulis peramban biasanya terkunci. Bisa dibuka
        /// untuk dibaca berarti penulisannya sudah selesai.
        /// </summary>
        private static bool CanOpen(string path)
        {
            try
            {
                using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }
}

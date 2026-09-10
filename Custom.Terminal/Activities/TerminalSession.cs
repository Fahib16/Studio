using System;
using System.Activities;
using System.ComponentModel;
using Open3270;

namespace Custom.Terminal
{
    /// <summary>
    /// Activity kustom: Terminal Session ala UiPath.Terminal.Activities untuk OpenRPA.
    ///
    /// Berbeda TOTAL secara arsitektur dari Click/TypeInto — ini bukan soal cari
    /// elemen UI (WindowsSelector/NMSelector), tapi koneksi protokol TN3270 ke
    /// mainframe lewat library open-source Open3270 (NuGet: Install-Package Open3270).
    ///
    /// Container activity (NativeActivity + ActivityAction&lt;TNEmulator&gt; Body) —
    /// pola sama dengan AttachBrowser/AttachWindow yang sudah kita buat sebelumnya:
    /// connect sekali di awal, child activities (SendTerminalKey, TypeIntoTerminal,
    /// dst) pakai "session" yang sama lewat variabel scope, disconnect di akhir.
    ///
    /// API TNEmulator (Connect, Config.TermType, SendKeyFromText, SendText,
    /// WaitForText, CurrentScreenXML.Dump, Close) dikonfirmasi dari source resmi
    /// Open3270 (TheDemo.cs), BUKAN tebakan.
    /// </summary>
    [Designer(typeof(Design.TerminalSessionDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.terminalsession.png")]
    [DisplayName("Terminal Session")]
    [Description("Membuka koneksi terminal TN3270 dan menjalankan activity di dalamnya.")]
    public sealed class TerminalSession : NativeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public TerminalSession()
        {
            DisplayName = "Terminal Session";
        }

        // ===================== Connection =====================

        [Category("Connection")]
        [RequiredArgument]
        [DisplayName("Host")]
        [Description("Alamat/hostname mainframe, mis. \"mainframe.perusahaan.com\"")]
        public InArgument<string> Host { get; set; }

        [Category("Connection")]
        [RequiredArgument]
        [DisplayName("Port")]
        [Description("Port TN3270, biasanya 23 (Telnet standar)")]
        public InArgument<int> Port { get; set; } = 23;

        [Category("Connection")]
        [DisplayName("LU Name")]
        [Description("Opsional. Logical Unit name kalau host butuh LU tertentu.")]
        public InArgument<string> LuName { get; set; }

        [Category("Connection")]
        [DisplayName("Terminal Type")]
        [Description("Contoh: IBM-3278-2-E, IBM-3278-2, IBM-3279-2-E (tergantung yang didukung host)")]
        [DefaultValue("IBM-3278-2-E")]
        public string TerminalType { get; set; } = "IBM-3278-2-E";

        [Category("Connection")]
        [DisplayName("Fast Screen Mode")]
        [Description("Mode Open3270 untuk update layar lebih cepat")]
        [DefaultValue(true)]
        public bool FastScreenMode { get; set; } = true;

        [Category("Connection")]
        [DisplayName("Connect Timeout")]
        [Description("Batas waktu tunggu koneksi awal & WaitForText pertama (default 20 detik kalau kosong)")]
        public InArgument<TimeSpan> ConnectTimeout { get; set; }

        [Category("Connection")]
        [DisplayName("Wait For Text (opsional)")]
        [Description("Kalau diisi, tunggu teks ini muncul di layar setelah connect sebelum lanjut ke Body " +
                      "(mis. \"LIBRARY OF CONGRESS\" atau judul screen awal aplikasi mainframe kamu). " +
                      "Kosongkan kalau tidak perlu verifikasi screen awal.")]
        public InArgument<string> WaitForTextAfterConnect { get; set; }

        [Category("Connection")]
        [DisplayName("Wait For Text Row")]
        [DefaultValue(0)]
        public InArgument<int> WaitForTextRow { get; set; } = 0;

        [Category("Connection")]
        [DisplayName("Wait For Text Column")]
        [DefaultValue(0)]
        public InArgument<int> WaitForTextColumn { get; set; } = 0;

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }


        /// <summary>
        /// Dipanggil WF Designer saat activity ini DISERET dari toolbox.
        ///
        /// Tanpa ini Body dibiarkan null: kotak "Do" tidak punya argumen
        /// delegate, jadi tidak ada nama yang bisa dipakai untuk menunjuk
        /// terminalnya di dalam blok, dan langkah kedua tidak bisa ditambahkan
        /// tanpa membongkar isinya lebih dulu.
        /// </summary>
        public Activity Create(System.Windows.DependencyObject target)
        {
            var instance = new TerminalSession();

            instance.Body = new ActivityAction<Open3270.TNEmulator>
            {
                Argument = new DelegateInArgument<Open3270.TNEmulator> { Name = "session" },
                Handler = new System.Activities.Statements.Sequence { DisplayName = "Do" }
            };

            return instance;
        }

        // ===================== Body: activity anak =====================

        [Browsable(false)]
        public ActivityAction<TNEmulator> Body { get; set; }

        private readonly Variable<TNEmulator> _emulator = new Variable<TNEmulator>();

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            metadata.AddArgument(new RuntimeArgument("Host", typeof(string), ArgumentDirection.In, true));
            metadata.AddArgument(new RuntimeArgument("Port", typeof(int), ArgumentDirection.In));
            metadata.AddArgument(new RuntimeArgument("LuName", typeof(string), ArgumentDirection.In));
            metadata.AddArgument(new RuntimeArgument("ConnectTimeout", typeof(TimeSpan), ArgumentDirection.In));
            metadata.AddArgument(new RuntimeArgument("WaitForTextAfterConnect", typeof(string), ArgumentDirection.In));
            metadata.AddArgument(new RuntimeArgument("WaitForTextRow", typeof(int), ArgumentDirection.In));
            metadata.AddArgument(new RuntimeArgument("WaitForTextColumn", typeof(int), ArgumentDirection.In));
            metadata.AddArgument(new RuntimeArgument("ContinueOnError", typeof(bool), ArgumentDirection.In));
            metadata.AddImplementationVariable(_emulator);

            if (Body != null)
                metadata.AddDelegate(Body);
        }

        protected override void Execute(NativeActivityContext context)
        {
            var host = Host.Get(context);
            var port = Port.Get(context) is 0 ? 23 : Port.Get(context);
            var lu = LuName != null ? LuName.Get(context) : null;
            var timeout = ConnectTimeout != null ? ConnectTimeout.Get(context) : TimeSpan.Zero;
            if (timeout == TimeSpan.Zero) timeout = TimeSpan.FromSeconds(20);
            var waitText = WaitForTextAfterConnect != null ? WaitForTextAfterConnect.Get(context) : null;
            var waitRow = WaitForTextRow != null ? WaitForTextRow.Get(context) : 0;
            var waitCol = WaitForTextColumn != null ? WaitForTextColumn.Get(context) : 0;
            var continueOnError = ContinueOnError != null ? ContinueOnError.Get(context) : false;

            var emulator = new TNEmulator
            {
                Debug = false
            };
            emulator.Config.TermType = TerminalType;
            emulator.Config.FastScreenMode = FastScreenMode;
            context.SetValue(_emulator, emulator);

            try
            {
                emulator.Connect(host, port, lu);

                if (!string.IsNullOrEmpty(waitText))
                {
                    // Urutan koordinatnya lewat TerminalCoordinates, sama dengan
                    // seluruh activity lain di project ini.
                    //
                    // Sebelumnya di sini tertulis WaitForText(waitRow, waitCol, ...),
                    // yang SALAH dua kali: Open3270 memakai x = KOLOM lebih dulu,
                    // dan keduanya berbasis 0 sementara properti activity berbasis 1.
                    // Kesalahan yang sama pernah ada di Wait For Terminal Text dan
                    // sudah diperbaiki di sana; yang di sini tertinggal — akibat
                    // aturannya hidup di komentar, bukan di satu fungsi yang
                    // dipanggil semua orang.
                    bool found = emulator.WaitForText(
                        TerminalCoordinates.ToX(waitCol), TerminalCoordinates.ToY(waitRow),
                        waitText, (int)timeout.TotalMilliseconds);

                    if (!found)
                    {
                        emulator.Close();
                        throw new TimeoutException(
                            $"Terminal Session: teks \"{waitText}\" tidak muncul di layar (row {waitRow}, col {waitCol}) " +
                            $"dalam {timeout.TotalSeconds} detik setelah connect.");
                    }
                }

                if (Body != null)
                {
                    context.ScheduleAction(Body, emulator, OnBodyCompleted, OnBodyFaulted);
                }
                else
                {
                    emulator.Close();
                }
            }
            catch (Exception) when (continueOnError)
            {
                try { emulator.Close(); } catch { /* sudah gagal connect, aman diabaikan */ }
            }
        }

        private void OnBodyCompleted(NativeActivityContext context, ActivityInstance completedInstance)
        {
            // Ambil kembali instance TNEmulator yang sama lewat implementation
            // variable, pastikan koneksi ditutup di akhir scope.
            var emulator = context.GetValue(_emulator);
            try
            {
                emulator?.Close();
            }
            catch (Exception)
            {
                // Abaikan error saat close -- koneksi mungkin sudah putus duluan
                // (mis. host drop connection), tidak perlu bikin workflow gagal
                // cuma gara-gara cleanup.
            }
        }

        private void OnBodyFaulted(NativeActivityFaultContext faultContext, Exception propagatedException,
            ActivityInstance propagatedFrom)
        {
            // Tutup koneksi juga kalau Body gagal di tengah jalan (exception),
            // supaya tidak ada koneksi TN3270 yang menggantung.
            var emulator = _emulator.Get(faultContext);
            try
            {
                emulator?.Close();
            }
            catch (Exception)
            {
            }
            // Biarkan exception aslinya tetap ter-propagate (tidak di-handle
            // di sini), supaya Try Catch di level workflow tetap bisa menangani.
        }
    }
}

using System;
using System.Activities;
using System.ComponentModel;
using Custom.Shared;

namespace Custom.Flow.Activities
{
    /// <summary>
    /// Menunggu sampai sebuah sinyal bernama dibangkitkan.
    ///
    /// Pengganti Detector milik OpenRPA. Detector di sana memarkir workflow
    /// pada sebuah bookmark sampai plugin detektor menembakkannya; di sini
    /// prinsipnya sama, hanya sumbernya yang terbuka: siapa pun yang bisa
    /// memanggil <see cref="RobotControl.Raise"/> bisa membangunkannya —
    /// cabang lain dari workflow yang sama lewat Raise Signal, atau program
    /// yang menjalankannya.
    ///
    /// Workflow yang menunggu di sini TIDAK memakai prosesor: ia benar-benar
    /// diparkir oleh runtime, bukan berputar memeriksa. Itulah gunanya
    /// bookmark, dan itulah kenapa activity ini NativeActivity, bukan
    /// CodeActivity yang tidur.
    /// </summary>
    [Designer(typeof(Design.WaitForSignalDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Wait For Signal")]
    [Description("Menunggu sampai sebuah sinyal bernama dibangkitkan dari luar.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.waitsignal.png")]
    public sealed class WaitForSignal : NativeActivity
    {
        public WaitForSignal()
        {
            DisplayName = "Wait For Signal";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Signal")]
        [Description("Nama sinyalnya. Sama persis dengan yang dipakai Raise Signal.")]
        public InArgument<string> Signal { get; set; }

        [Category("Output")]
        [DisplayName("Payload")]
        [Description("Nilai yang dibawa sinyalnya, kalau ada.")]
        public OutArgument<object> Payload { get; set; }

        /// <summary>
        /// Nama sinyal disimpan supaya penunggunya bisa dilepas lagi saat
        /// bangun. Membiarkannya terdaftar membuat sinyal berikutnya
        /// membangunkan bookmark yang sudah tidak ada.
        /// </summary>
        private readonly Variable<string> _nama = new Variable<string>("_nama");

        protected override bool CanInduceIdle { get { return true; } }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);
            metadata.AddImplementationVariable(_nama);
        }

        protected override void Execute(NativeActivityContext context)
        {
            var nama = Signal.Get(context);

            if (string.IsNullOrWhiteSpace(nama))
                throw new InvalidOperationException("Wait For Signal: Signal kosong.");

            nama = nama.Trim();
            context.SetValue(_nama, nama);

            var bookmark = "jakforge_signal_" + nama;
            context.CreateBookmark(bookmark, OnSignal);

            // Yang didaftarkan hanya NAMA bookmark-nya. Yang benar-benar
            // melanjutkannya adalah host, lewat RobotControl.BookmarkResumer:
            // hanya pemilik WorkflowApplication yang bisa melakukannya.
            RobotControl.Await(nama, bookmark);

            RobotLog.Debug("Menunggu sinyal \"" + nama + "\".", DisplayName);
        }

        private void OnSignal(NativeActivityContext context, Bookmark bookmark, object value)
        {
            var nama = _nama.Get(context);

            // Penunggunya dilepas SEKARANG, bukan dibiarkan: bookmark-nya sudah
            // dipakai, dan sinyal berikutnya tidak boleh menembak bookmark yang
            // sudah tidak ada.
            RobotControl.StopAwaiting(nama);

            if (Payload != null) Payload.Set(context, value);

            RobotLog.Debug("Sinyal \"" + nama + "\" diterima.", DisplayName);
        }
    }

    // ==================================================================

    /// <summary>
    /// Membangkitkan sebuah sinyal bernama.
    ///
    /// Pasangan Wait For Signal. Gunanya di dalam Parallel: satu cabang
    /// menunggu, cabang lain membangunkannya ketika syaratnya terpenuhi.
    /// </summary>
    [Designer(typeof(Design.RaiseSignalDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Raise Signal")]
    [Description("Membangkitkan sinyal bernama; membangunkan Wait For Signal yang menunggunya.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.raisesignal.png")]
    public sealed class RaiseSignal : CodeActivity
    {
        public RaiseSignal()
        {
            DisplayName = "Raise Signal";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Signal")]
        [Description("Nama sinyalnya.")]
        public InArgument<string> Signal { get; set; }

        [Category("Input")]
        [DisplayName("Payload")]
        [Description("Nilai yang dibawa sinyalnya; diterima Wait For Signal.")]
        public InArgument<object> Payload { get; set; }

        [Category("Output")]
        [DisplayName("Delivered")]
        [Description("False kalau tidak ada yang sedang menunggu sinyal itu.")]
        public OutArgument<bool> Delivered { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var nama = Signal.Get(context);

            if (string.IsNullOrWhiteSpace(nama))
                throw new InvalidOperationException("Raise Signal: Signal kosong.");

            var sampai = RobotControl.Raise(nama, Payload != null ? Payload.Get(context) : null);

            if (Delivered != null) Delivered.Set(context, sampai);

            // Sinyal yang jatuh ke ruang kosong BUKAN kesalahan: pengirimnya
            // belum tentu tahu ada yang menunggu. Tapi dicatat, karena "kenapa
            // tidak terjadi apa-apa" adalah pertanyaan berikutnya.
            if (!sampai)
                RobotLog.Debug("Sinyal \"" + nama + "\" dibangkitkan, tapi tidak ada yang menunggunya.", DisplayName);
        }
    }
}

using System;
using System.Activities;
using System.ComponentModel;
using System.Windows.Markup;

namespace Custom.Flow.Activities
{
    /// <summary>
    /// Hentikan perulangan yang sedang berjalan.
    ///
    /// Bekerja dengan mencari bookmark "BreakBookmark" yang dipasang perulangan
    /// di sekelilingnya. Nama itu sama dengan yang dipakai perulangan OpenRPA
    /// yang lama, jadi Break ini juga menghentikan perulangan lama — workflow
    /// campuran tetap jalan.
    ///
    /// Kalau tidak ada perulangan di sekelilingnya, TIDAK terjadi apa-apa dan
    /// tidak ada kesalahan. Break di luar perulangan adalah kekeliruan menyusun
    /// workflow yang paling jelas terlihat di kanvas, bukan sesuatu yang layak
    /// menjatuhkan robot di tengah pekerjaan.
    /// </summary>
    [Designer(typeof(BreakDesigner))]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.break.png")]
    [DisplayName("Break")]
    [Description("Hentikan perulangan yang sedang berjalan.")]
    public class Break : NativeActivity
    {
        protected override bool CanInduceIdle { get { return true; } }

        protected override void Execute(NativeActivityContext context)
        {
            var bookmark = context.Properties.Find("BreakBookmark") as Bookmark;
            if (bookmark == null) return;

            // Bookmark milik sendiri dititipkan sebagai nilai: perulangannya
            // yang akan melanjutkannya setelah selesai membatalkan anak-anaknya.
            // Tanpa itu, activity ini menggantung dan workflow tidak pernah selesai.
            context.ResumeBookmark(bookmark, context.CreateBookmark());
        }
    }

    // ==================================================================

    /// <summary>
    /// Lewati sisa putaran ini dan lanjut ke putaran berikutnya.
    /// </summary>
    [Designer(typeof(ContinueDesigner))]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.continue.png")]
    [DisplayName("Continue")]
    [Description("Lewati sisa putaran ini, lanjut ke putaran berikutnya.")]
    public class Continue : NativeActivity
    {
        protected override bool CanInduceIdle { get { return true; } }

        protected override void Execute(NativeActivityContext context)
        {
            var bookmark = context.Properties.Find("ContinueBookmark") as Bookmark;
            if (bookmark == null) return;

            context.ResumeBookmark(bookmark, context.CreateBookmark());
        }
    }

    // ==================================================================

    /// <summary>
    /// Nonaktifkan sekelompok activity tanpa menghapusnya.
    ///
    /// Isinya tetap tersimpan di berkas dan tetap terlihat di kanvas, tapi tidak
    /// dijalankan. Berguna saat menelusuri masalah: mematikan satu bagian lebih
    /// aman daripada menghapusnya lalu mencoba menyusunnya kembali dari ingatan.
    /// </summary>
    [Designer(typeof(CommentOutDesigner))]
    [ContentProperty("Body")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.commentout.png")]
    [DisplayName("Comment Out")]
    [Description("Simpan activity di dalamnya tanpa menjalankannya.")]
    public class CommentOut : NativeActivity
    {
        [Browsable(false)]
        public Activity Body { get; set; }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            // Body didaftarkan sebagai anak IMPLEMENTASI, bukan anak biasa.
            //
            // Anak implementasi tetap divalidasi perancang — variabel dan
            // ekspresi di dalamnya tetap diperiksa, jadi kesalahan tidak
            // tersembunyi — tapi tidak pernah dijadwalkan. Kalau didaftarkan
            // sebagai anak biasa dan tidak dijadwalkan, WF mengeluhkan anak yang
            // tidak pernah dipakai.
            if (Body != null) metadata.AddImplementationChild(Body);
        }

        protected override void Execute(NativeActivityContext context)
        {
            // Sengaja kosong: itulah gunanya.
        }
    }
}

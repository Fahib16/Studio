using System;
using System.Activities;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Custom.Flow.Activities
{
    /// <summary>
    /// Dasar bagi semua perulangan yang bisa dihentikan dengan Break dan
    /// dilewati satu putaran dengan Continue.
    ///
    /// Ditulis ulang di sini alih-alih mewarisi BreakableLoop milik
    /// OpenRPA.Interfaces, supaya proyek ini tidak bergantung pada apa pun milik
    /// Studio — activity di dalamnya harus bisa berjalan di JakRunner juga.
    ///
    /// Nama properti konteks "BreakBookmark" dan "ContinueBookmark" SENGAJA
    /// dibuat sama persis dengan yang lama. Dengan begitu Break dari JakForge
    /// bekerja di dalam perulangan OpenRPA yang lama, dan sebaliknya — workflow
    /// campuran tetap jalan selama masa peralihan.
    /// </summary>
    /// <remarks>
    /// JANGAN memberi [DisplayName] pada properti bertipe Argument di kelas
    /// turunan mana pun.
    ///
    /// Refleksi metadata WF mendaftarkan argumen itu DUA KALI ketika nama
    /// tampilannya berbeda dari nama propertinya — satu di bawah nama properti,
    /// satu lagi di bawah nama tampilan — dan seluruh workflow lalu ditolak
    /// dengan "RuntimeArgument 'X' refers to an Argument which in turn is bound
    /// to RuntimeArgument named 'X'".
    ///
    /// Gejalanya menyesatkan: pesannya bicara tentang ikatan ganda, sehingga
    /// yang dicurigai adalah CacheMetadata — padahal CacheMetadata-nya benar.
    /// Ditemukan pada ForEachDataRow, yang punya [DisplayName("Data table")]
    /// di properti DataTable.
    ///
    /// Judul yang terbaca manusia ditaruh di kartu canvas (berkas Designer.xaml),
    /// bukan di atribut.
    /// </remarks>
    public abstract class JakForgeLoop : NativeActivity
    {
        /// <summary>Diisi turunannya: apa yang dikerjakan saat perulangan dimulai.</summary>
        protected abstract void StartLoop(NativeActivityContext context);

        /// <summary>True setelah Break dijalankan; turunannya memeriksa ini sebelum putaran berikutnya.</summary>
        protected bool BreakRequested;

        /// <summary>
        /// Variabel milik perulangan ini, misalnya Index dan Total.
        ///
        /// Disembunyikan dari perancang: yang membuatnya adalah template
        /// activity-nya sendiri, bukan orang yang menyusun workflow.
        /// </summary>
        [Browsable(false)]
        public Collection<Variable> Variables { get; set; } = new Collection<Variable>();

        protected override bool CanInduceIdle { get { return true; } }

        protected override void Execute(NativeActivityContext context)
        {
            BreakRequested = false;

            // Bookmark NonBlocking: perulangan tidak menunggu siapa pun
            // melanjutkannya. Ia hanya menyediakan alamat yang bisa dipanggil
            // Break dan Continue kalau memang dijalankan di dalamnya.
            var onBreak = context.CreateBookmark(OnBreak, BookmarkOptions.NonBlocking);
            context.Properties.Add("BreakBookmark", onBreak);

            var onContinue = context.CreateBookmark(OnContinue,
                BookmarkOptions.NonBlocking | BookmarkOptions.MultipleResume);
            context.Properties.Add("ContinueBookmark", onContinue);

            StartLoop(context);
        }

        private void OnBreak(NativeActivityContext context, Bookmark bookmark, object value)
        {
            context.CancelChildren();
            BreakRequested = true;

            // Break menitipkan bookmark-nya sendiri sebagai nilai, dan harus
            // dilanjutkan — kalau tidak, activity Break itu menggantung selamanya
            // dan workflow-nya tidak pernah selesai.
            var waiting = value as Bookmark;
            if (waiting != null) context.ResumeBookmark(waiting, value);
        }

        private void OnContinue(NativeActivityContext context, Bookmark bookmark, object value)
        {
            context.CancelChildren();

            var waiting = value as Bookmark;
            if (waiting != null) context.ResumeBookmark(waiting, value);
        }

        // ------------------------------------------------------------------
        // Index dan Total
        //
        // Keduanya adalah VARIABEL biasa yang dibuat template activity, bukan
        // properti. Jadi cara menulisinya lewat PropertyDescriptor pada
        // DataContext — sama seperti workflow menulis variabel lewat Assign.
        // ------------------------------------------------------------------

        protected void SetTotal(NativeActivityContext context, int value)
        {
            var descriptor = Descriptor<int>(context, "Total");
            if (descriptor == null) return;

            try { descriptor.SetValue(context.DataContext, value); }
            catch (Exception) { }
        }

        protected void IncrementIndex(NativeActivityContext context)
        {
            var descriptor = Descriptor<int>(context, "Index");
            if (descriptor == null) return;

            try
            {
                var current = descriptor.GetValue(context.DataContext);
                var index = 0;

                if (current is int) index = (int)current;
                else if (current != null) int.TryParse(current.ToString(), out index);

                descriptor.SetValue(context.DataContext, index + 1);
            }
            catch (Exception)
            {
                // Index yang gagal diperbarui tidak boleh menghentikan
                // perulangan: ia penanda kemajuan, bukan bagian dari pekerjaan.
            }
        }

        private PropertyDescriptor Descriptor<T>(NativeActivityContext context, string name)
        {
            if (context == null || Variables.Count == 0 || string.IsNullOrEmpty(name)) return null;

            foreach (var variable in Variables)
            {
                if (variable.Type != typeof(T)) continue;
                if (!string.Equals(variable.Name, name, StringComparison.OrdinalIgnoreCase)) continue;

                return context.DataContext.GetProperties()[variable.Name];
            }

            return null;
        }
    }
}

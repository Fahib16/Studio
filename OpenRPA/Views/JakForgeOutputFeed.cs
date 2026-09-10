using System;
using System.Collections.Generic;

namespace OpenRPA.Views
{
    /// <summary>Satu baris di panel Output.</summary>
    public class OutputEntry
    {
        public DateTime Time { get; set; }

        /// <summary>Info, Warn, Error, atau Trace.</summary>
        public string Level { get; set; }

        public string Message { get; set; }

        public string TimeText { get { return Time.ToString(@"HH:mm:ss"); } }
    }

    /// <summary>
    /// Penampung baris Output beserta hitungannya per level.
    ///
    /// Kenapa ada: panel Output lama hanya menampung baris berkategori
    /// "Output". Pesan Warning, Error, dan Trace tidak pernah sampai ke sana —
    /// itulah sebabnya di activity Log Message hanya level Info yang terlihat
    /// bekerja.
    ///
    /// ATURAN PENTING: kelas ini TIDAK BOLEH menyentuh UI sama sekali.
    ///
    /// Versi pertama mengirim setiap baris ke thread UI lewat
    /// Dispatcher.BeginInvoke. OpenRPA menulis ribuan baris log saat memuat
    /// plugin dan proyek, dan saat itu thread UI sedang sibuk menyiapkan
    /// Studio — antrean dispatcher menumpuk, tiap baris memicu pembaruan
    /// daftar berfilter, dan Studio berhenti di "loading workflow toolbox"
    /// sampai mesinnya ikut tersendat.
    ///
    /// Sekarang baris hanya ditumpuk di memori dengan kunci biasa; panel
    /// Output yang MENARIK isinya secara berkala. Kalau panelnya belum pernah
    /// dibuka, tidak ada pekerjaan UI sama sekali.
    /// </summary>
    public static class JakForgeOutputFeed
    {
        private static readonly object gate = new object();
        private static readonly LinkedList<OutputEntry> buffer = new LinkedList<OutputEntry>();

        /// <summary>Bertambah setiap ada perubahan; panel memakainya untuk tahu perlu menggambar ulang atau tidak.</summary>
        public static long Version { get; private set; }

        public static int InfoCount { get; private set; }
        public static int WarnCount { get; private set; }
        public static int ErrorCount { get; private set; }
        public static int TraceCount { get; private set; }

        /// <summary>Batas baris supaya panel tidak menghabiskan memori pada workflow panjang.</summary>
        private const int MaxEntries = 2000;

        /// <summary>
        /// Kategori yang tidak pernah masuk panel: penanda masuk-keluar fungsi
        /// dan lalu lintas jaringan. Keduanya ribuan baris per menit dan tidak
        /// membantu siapa pun yang sedang membaca hasil workflow.
        /// </summary>
        private static bool Ignored(string category)
        {
            return category == "Func" || category == "Network" || category == "network" || category == "Tracing";
        }

        /// <summary>
        /// Menerjemahkan kategori Trace milik OpenRPA menjadi level yang
        /// ditampilkan.
        /// </summary>
        public static string LevelOf(string category)
        {
            if (string.IsNullOrEmpty(category)) return "Info";

            switch (category)
            {
                case "Error": return "Error";
                case "Warning": return "Warn";
                case "Output":
                case "Information": return "Info";
                default: return "Trace";
            }
        }

        public static void Add(DateTime when, string category, string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (Ignored(category)) return;

            var entry = new OutputEntry
            {
                Time = when,
                Level = LevelOf(category),
                Message = message
            };

            lock (gate)
            {
                buffer.AddFirst(entry);
                while (buffer.Count > MaxEntries) buffer.RemoveLast();

                switch (entry.Level)
                {
                    case "Error": ErrorCount++; break;
                    case "Warn": WarnCount++; break;
                    case "Info": InfoCount++; break;
                    default: TraceCount++; break;
                }

                Version++;
            }

            // Salinan ke berkas harian. Trace sengaja dilewati: isinya ribuan
            // baris teknis per menit dan hanya akan mengubur baris yang benar
            // benar berasal dari workflow.
            if (entry.Level != "Trace") JakForgeLogFile.Write(entry.Time, entry.Level, entry.Message);
        }

        /// <summary>Salinan isi panel saat ini, terbaru di depan.</summary>
        public static OutputEntry[] Snapshot()
        {
            lock (gate)
            {
                var copy = new OutputEntry[buffer.Count];
                buffer.CopyTo(copy, 0);
                return copy;
            }
        }

        public static void Clear()
        {
            lock (gate)
            {
                buffer.Clear();
                InfoCount = WarnCount = ErrorCount = TraceCount = 0;
                Version++;
            }
        }
    }
}

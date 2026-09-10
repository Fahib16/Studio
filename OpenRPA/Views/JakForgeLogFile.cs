using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace OpenRPA.Views
{
    /// <summary>
    /// Menyimpan isi panel Output ke berkas teks, satu berkas per tanggal.
    ///
    /// Kenapa perlu: panel Output dikosongkan setiap kali flow dijalankan,
    /// supaya yang terlihat hanya hasil jalan yang sedang berlangsung. Tanpa
    /// salinan di disk, jejak jalan sebelumnya hilang begitu saja — padahal
    /// justru jalan yang GAGAL kemarin yang paling perlu dibaca ulang.
    ///
    /// Lokasinya: %LOCALAPPDATA%\JakForge\Logs\yyyy-MM-dd.txt
    ///
    /// Penulisannya SENGAJA lewat antrean dan satu thread latar, bukan
    /// File.AppendAllText langsung di pemanggil. OpenRPA menulis ribuan baris
    /// saat memuat plugin dan proyek; membuka-menutup berkas seribu kali di
    /// thread yang sedang sibuk menyiapkan Studio adalah cara yang sudah
    /// terbukti membuat Studio terasa menggantung.
    /// </summary>
    public static class JakForgeLogFile
    {
        private static readonly object gate = new object();
        private static readonly Queue<string> pending = new Queue<string>();
        private static Thread writer;
        private static bool started;

        /// <summary>Batas antrean; kalau penulisan tersendat, baris terlama dibuang daripada memakan memori.</summary>
        private const int MaxPending = 20000;

        public static string Folder
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "JakForge", "Logs");
            }
        }

        /// <summary>Berkas untuk hari ini. Namanya per tanggal, jadi berganti sendiri lewat tengah malam.</summary>
        public static string FileForToday()
        {
            return Path.Combine(Folder, DateTime.Now.ToString("yyyy-MM-dd") + ".txt");
        }

        /// <summary>
        /// Tandai awal sebuah jalan. Dipanggil bersamaan dengan pengosongan
        /// panel Output, supaya batas antar-jalan tetap terlihat di berkas
        /// walaupun di layar sudah bersih.
        /// </summary>
        public static void BeginRun(string workflowName, bool withTracking)
        {
            Enqueue(new string('=', 78));
            Enqueue(string.Format("=== MULAI {0} — {1} ({2}) ===",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                string.IsNullOrEmpty(workflowName) ? "(tanpa nama)" : workflowName,
                withTracking ? "Debug" : "Run"));
            Enqueue(new string('=', 78));
        }

        public static void EndRun(string workflowName, string outcome)
        {
            Enqueue(string.Format("=== SELESAI {0} — {1}: {2} ===",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                string.IsNullOrEmpty(workflowName) ? "(tanpa nama)" : workflowName,
                outcome));
            Enqueue("");
        }

        /// <summary>Satu baris Output; dipanggil dari JakForgeOutputFeed.Add.</summary>
        public static void Write(DateTime when, string level, string message)
        {
            Enqueue(string.Format("{0} [{1,-5}] {2}",
                when.ToString("HH:mm:ss.fff"), level, message));
        }

        private static void Enqueue(string line)
        {
            lock (gate)
            {
                pending.Enqueue(line);
                while (pending.Count > MaxPending) pending.Dequeue();

                if (!started)
                {
                    started = true;
                    writer = new Thread(Loop) { IsBackground = true, Name = "JakForgeLogFile" };
                    writer.Start();
                }

                Monitor.Pulse(gate);
            }
        }

        private static void Loop()
        {
            var batch = new StringBuilder();

            while (true)
            {
                try
                {
                    batch.Length = 0;

                    lock (gate)
                    {
                        while (pending.Count == 0) Monitor.Wait(gate, 1000);

                        while (pending.Count > 0) batch.AppendLine(pending.Dequeue());
                    }

                    if (batch.Length == 0) continue;

                    var folder = Folder;
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    File.AppendAllText(FileForToday(), batch.ToString(), Encoding.UTF8);
                }
                catch (Exception)
                {
                    // Kegagalan menulis log TIDAK BOLEH menjatuhkan Studio, dan
                    // juga tidak boleh dilaporkan lewat Log — itu akan memanggil
                    // balik ke sini dan berputar tanpa henti. Diamkan saja, lalu
                    // beri jeda supaya tidak jadi loop sibuk kalau diska penuh.
                    try { Thread.Sleep(2000); } catch (Exception) { }
                }
            }
        }
    }
}

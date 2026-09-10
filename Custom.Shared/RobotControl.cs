using System;
using System.Collections.Generic;

namespace Custom.Shared
{
    /// <summary>
    /// Saluran kendali antara activity dan program yang menjalankannya.
    ///
    /// Polanya sama dengan <see cref="RobotLog"/>, dan alasannya sama: activity
    /// dibangun oleh pemuat XAML, bukan oleh kode kita, jadi tidak ada tempat
    /// untuk menitipkan rujukan ke host-nya. Yang statis begini adalah satu-
    /// satunya jalan yang tidak menuntut setiap activity menerima host-nya
    /// sebagai argumen.
    ///
    /// Aman karena satu program hanya menjalankan satu automasi pada satu
    /// waktu: asisten yang menjalankan dua robot sekaligus di satu mesin akan
    /// berebut mouse dan papan ketik, jadi itu memang tidak dilakukan.
    /// </summary>
    public static class RobotControl
    {
        private static readonly object Gembok = new object();
        /// <summary>Nama sinyal ke nama bookmark yang menunggunya.</summary>
        private static readonly Dictionary<string, string> Penunggu =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Diminta berhenti dari DALAM workflow.
        ///
        /// Host yang memutuskan bagaimana caranya: JakRunner membatalkan
        /// WorkflowApplication-nya, Studio menghentikan jalannya. Activity
        /// tidak perlu tahu mana yang sedang menjalankannya.
        ///
        /// Kalau TIDAK ADA yang mendengarkan, permintaannya bukan diabaikan
        /// diam-diam: pemanggilnya diberi tahu lewat nilai kembalian, dan
        /// activity Stop Robot melemparkan pengecualian supaya jalannya tetap
        /// berhenti. Robot yang diminta berhenti lalu terus berjalan adalah
        /// hasil yang paling buruk dari ketiganya.
        /// </summary>
        public static event Action<string> StopRequested;

        /// <summary>Benar kalau ada host yang siap menghentikan.</summary>
        public static bool CanStop { get { return StopRequested != null; } }

        /// <summary>Minta berhenti. Mengembalikan false kalau tidak ada yang mendengarkan.</summary>
        public static bool RequestStop(string reason)
        {
            var handler = StopRequested;
            if (handler == null) return false;

            handler(reason ?? "");
            return true;
        }

        // ------------------------------------------------------------------
        // Sinyal
        // ------------------------------------------------------------------

        /// <summary>
        /// Cara melanjutkan sebuah bookmark, dipasang oleh HOST.
        ///
        /// Ini bagian yang tidak bisa dikerjakan activity sendiri: hanya
        /// pemilik WorkflowApplication yang bisa melanjutkan bookmark-nya.
        /// Activity hanya bisa MENDAFTARKAN nama bookmark-nya di sini; yang
        /// benar-benar membangunkannya adalah host.
        ///
        /// Percobaan pertama saya memakai WorkflowInstanceProxy lewat
        /// GetExtension, dan itu keliru: proxy itu bukan ekstensi, jadi yang
        /// didapat null dan sinyalnya tidak pernah sampai. Ketahuan hanya
        /// karena diuji dengan menjalankannya.
        /// </summary>
        public static Func<string, object, bool> BookmarkResumer;

        /// <summary>
        /// Daftarkan nama bookmark yang menunggu sebuah sinyal.
        ///
        /// Nama yang sama didaftarkan dua kali akan MENGGANTI yang lama, bukan
        /// menumpuk: dua penunggu untuk satu nama berarti hanya salah satunya
        /// yang akan bangun, dan tidak ada cara menebak yang mana.
        /// </summary>
        public static void Await(string signal, string bookmarkName)
        {
            if (string.IsNullOrWhiteSpace(signal) || string.IsNullOrWhiteSpace(bookmarkName)) return;

            lock (Gembok) { Penunggu[signal.Trim()] = bookmarkName; }
        }

        public static void StopAwaiting(string signal)
        {
            if (string.IsNullOrWhiteSpace(signal)) return;

            lock (Gembok) { Penunggu.Remove(signal.Trim()); }
        }

        /// <summary>
        /// Bangunkan penunggu sebuah sinyal.
        ///
        /// Bisa dipanggil activity Raise Signal, bisa juga oleh host, dan
        /// itulah yang membuatnya menjadi padanan Detector: sumber luar apa pun
        /// yang bisa memanggil metode ini bisa membangunkan workflow yang
        /// sedang menunggu.
        ///
        /// Mengembalikan false kalau tidak ada yang menunggu nama itu, ATAU
        /// kalau tidak ada host yang bisa melanjutkan bookmark-nya. Sinyal yang
        /// jatuh ke ruang kosong bukan kesalahan, tapi pemanggilnya berhak
        /// tahu bedanya antara "sampai" dan "tidak".
        /// </summary>
        public static bool Raise(string signal, object payload = null)
        {
            string bookmark;

            lock (Gembok)
            {
                if (string.IsNullOrWhiteSpace(signal)) return false;
                if (!Penunggu.TryGetValue(signal.Trim(), out bookmark)) return false;
            }

            var resumer = BookmarkResumer;
            if (resumer == null) return false;

            return resumer(bookmark, payload);
        }

        /// <summary>Nama sinyal yang sedang ditunggu; dipakai pengujian dan diagnosa.</summary>
        public static IEnumerable<string> Waiting
        {
            get
            {
                lock (Gembok) { return new List<string>(Penunggu.Keys); }
            }
        }

        /// <summary>Bersihkan seluruh penunggu; dipanggil host sebelum jalan baru.</summary>
        public static void Reset()
        {
            lock (Gembok) { Penunggu.Clear(); }
        }
    }
}

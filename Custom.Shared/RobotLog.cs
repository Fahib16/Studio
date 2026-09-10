using System;

namespace Custom.Shared
{
    public enum RobotLogLevel
    {
        Trace,
        Debug,
        Info,
        Warning,
        Error,
    }

    public class RobotLogEntry
    {
        public DateTime Time { get; set; }
        public RobotLogLevel Level { get; set; }
        public string Message { get; set; }

        /// <summary>Activity yang menulisnya, kalau memang berasal dari activity.</summary>
        public string Source { get; set; }

        public string LevelText
        {
            get
            {
                switch (Level)
                {
                    case RobotLogLevel.Error: return "ERROR";
                    case RobotLogLevel.Warning: return "WARN";
                    case RobotLogLevel.Debug: return "DEBUG";
                    case RobotLogLevel.Trace: return "TRACE";
                    default: return "INFO";
                }
            }
        }
    }

    /// <summary>
    /// Saluran log robot yang dipakai bersama.
    ///
    /// Activity menulis ke sini; yang MENAMPUNG-nya adalah program yang sedang
    /// menjalankannya — JakRunner memasukkannya ke panel log dan mengirimnya ke
    /// ForgeHub, Studio memasukkannya ke panel Output.
    ///
    /// Dibuat statis dengan sengaja. Activity dibangun oleh pemuat XAML, bukan
    /// oleh kode kita, jadi tidak ada tempat untuk menitipkan rujukan ke
    /// penampungnya. Aman karena satu program hanya menjalankan satu automasi
    /// pada satu waktu — asisten yang menjalankan dua robot sekaligus di satu
    /// mesin akan berebut mouse dan papan ketik, jadi itu memang tidak dilakukan.
    /// </summary>
    public static class RobotLog
    {
        /// <summary>
        /// Nomor pekerjaan ForgeHub yang sedang dikerjakan, kalau ada.
        ///
        /// Disetel penampungnya sebelum automasi dimulai, supaya baris log bisa
        /// ditautkan ke pekerjaannya di dasbor — tanpa itu, log dari dua
        /// pekerjaan berbeda tercampur jadi satu daftar panjang.
        /// </summary>
        public static string JobId;

        /// <summary>Nama proses yang sedang berjalan, untuk penandaan di ForgeHub.</summary>
        public static string ProcessName;

        public static event Action<RobotLogEntry> Written;

        public static void Write(RobotLogLevel level, string message, string source = null)
        {
            if (string.IsNullOrEmpty(message)) return;

            var handler = Written;
            if (handler == null) return;

            var entry = new RobotLogEntry
            {
                Time = DateTime.Now,
                Level = level,
                Message = message,
                Source = source,
            };

            try
            {
                handler(entry);
            }
            catch (Exception)
            {
                // Penampung yang bermasalah tidak boleh menjatuhkan robot yang
                // sedang berjalan. Log adalah catatan tentang pekerjaan, bukan
                // pekerjaan itu sendiri.
            }
        }

        public static void Trace(string message, string source = null) { Write(RobotLogLevel.Trace, message, source); }
        public static void Debug(string message, string source = null) { Write(RobotLogLevel.Debug, message, source); }
        public static void Info(string message, string source = null) { Write(RobotLogLevel.Info, message, source); }
        public static void Warning(string message, string source = null) { Write(RobotLogLevel.Warning, message, source); }
        public static void Error(string message, string source = null) { Write(RobotLogLevel.Error, message, source); }
    }
}

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows.Threading;

namespace JakRunner.Core
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error,
    }

    public class LogLine
    {
        public DateTime Time { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; }

        public string TimeText { get { return Time.ToString("HH:mm:ss"); } }

        /// <summary>Jam dalam kurung siku, bentuk yang dipakai di panel log.</summary>
        public string Bracketed { get { return "[" + Time.ToString("HH:mm:ss") + "]"; } }

        public bool IsError { get { return Level == LogLevel.Error; } }
    }

    /// <summary>
    /// Catatan jalannya automasi: yang tampil di panel "Log Aktivitas Terbaru"
    /// dan "Daftar Kesalahan".
    ///
    /// Baris ditambahkan dari thread mana pun — WorkflowApplication memanggil
    /// balik di thread-nya sendiri — jadi penambahannya selalu dilempar ke
    /// dispatcher UI. ObservableCollection tidak boleh disentuh dari thread lain,
    /// dan pelanggaran itu tidak selalu langsung terlihat: ia muncul belakangan
    /// sebagai daftar yang isinya kacau.
    /// </summary>
    public class RunLog : INotifyPropertyChanged
    {
        private readonly Dispatcher _dispatcher;
        private const int MaxLines = 500;

        public RunLog(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public ObservableCollection<LogLine> Lines { get; } = new ObservableCollection<LogLine>();
        public ObservableCollection<LogLine> Errors { get; } = new ObservableCollection<LogLine>();

        public int ErrorCount { get { return Errors.Count; } }

        public void Info(string message) { Add(LogLevel.Info, message); }
        public void Warning(string message) { Add(LogLevel.Warning, message); }
        public void Error(string message) { Add(LogLevel.Error, message); }

        public void Add(LogLevel level, string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            var line = new LogLine { Time = DateTime.Now, Level = level, Message = message };

            Action apply = () =>
            {
                Lines.Insert(0, line);
                while (Lines.Count > MaxLines) Lines.RemoveAt(Lines.Count - 1);

                if (level == LogLevel.Error)
                {
                    Errors.Insert(0, line);
                    while (Errors.Count > MaxLines) Errors.RemoveAt(Errors.Count - 1);
                    Notify("ErrorCount");
                }
            };

            if (_dispatcher == null || _dispatcher.CheckAccess()) apply();
            else _dispatcher.BeginInvoke(apply);

            WriteToFile(line);
        }

        public void Clear()
        {
            Action apply = () => { Lines.Clear(); Errors.Clear(); Notify("ErrorCount"); };

            if (_dispatcher == null || _dispatcher.CheckAccess()) apply();
            else _dispatcher.BeginInvoke(apply);
        }

        /// <summary>
        /// Salinan ke berkas harian, satu berkas per tanggal — sama seperti di
        /// Studio, supaya jalan yang gagal kemarin masih bisa dibaca hari ini.
        /// </summary>
        private static void WriteToFile(LogLine line)
        {
            try
            {
                var folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "JakForge", "Logs");

                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                var path = Path.Combine(folder, "JakRunner-" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt");

                File.AppendAllText(path,
                    string.Format("{0} [{1,-7}] {2}{3}",
                        line.Time.ToString("HH:mm:ss.fff"), line.Level, line.Message, Environment.NewLine),
                    Encoding.UTF8);
            }
            catch (Exception)
            {
                // Gagal menulis log tidak boleh menjatuhkan robot yang sedang jalan.
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Notify(string name)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(name));
        }
    }
}

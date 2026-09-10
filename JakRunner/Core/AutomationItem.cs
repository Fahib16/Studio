using System;
using System.ComponentModel;

namespace JakRunner.Core
{
    /// <summary>Keadaan sebuah automasi di daftar.</summary>
    public enum RunState
    {
        Ready,
        Running,
        Paused,
        Succeeded,
        Failed,
    }

    /// <summary>
    /// Satu automasi yang bisa dijalankan: satu berkas workflow di dalam sebuah
    /// proyek.
    /// </summary>
    public class AutomationItem : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string ProjectName { get; set; }

        /// <summary>Lokasi lengkap berkas .xaml-nya.</summary>
        public string FilePath { get; set; }

        /// <summary>Folder proyeknya; dipakai Invoke Workflow untuk mencari sub-workflow.</summary>
        public string ProjectFolder { get; set; }

        public DateTime LastModified { get; set; }

        private RunState _state = RunState.Ready;
        public RunState State
        {
            get { return _state; }
            set
            {
                if (_state == value) return;
                _state = value;
                Notify("State");
                Notify("StateText");
                Notify("IsRunning");
                Notify("IsIdle");
                Notify("StateBackground");
                Notify("StateForeground");
                Notify("CpuText");
                Notify("MemoryText");
            }
        }

        private double _progress;
        /// <summary>0..100. Bukan kemajuan sesungguhnya — WF tidak melaporkannya —
        /// melainkan penanda bahwa robot masih hidup.</summary>
        public double Progress
        {
            get { return _progress; }
            set { if (Math.Abs(_progress - value) < 0.01) return; _progress = value; Notify("Progress"); }
        }

        private string _message = "";
        public string Message
        {
            get { return _message; }
            set { if (_message == value) return; _message = value; Notify("Message"); }
        }

        public bool IsRunning { get { return State == RunState.Running || State == RunState.Paused; } }
        public bool IsIdle { get { return !IsRunning; } }

        public string StateText
        {
            get
            {
                switch (State)
                {
                    case RunState.Running: return "Berjalan";
                    case RunState.Paused: return "Dijeda";
                    case RunState.Succeeded: return "Selesai";
                    case RunState.Failed: return "Gagal";
                    default: return "Siap";
                }
            }
        }

        public string Subtitle
        {
            get { return ProjectName + "  ·  " + LastModified.ToString("dd MMM yyyy HH:mm"); }
        }


        /// <summary>
        /// Warna latar lencana status. Dihitung di sini, bukan lewat converter,
        /// supaya XAML-nya tetap bisa dibaca tanpa melompat ke berkas lain.
        /// </summary>
        public System.Windows.Media.Brush StateBackground
        {
            get
            {
                switch (State)
                {
                    case RunState.Running: return Brush("#DFF2E5");
                    case RunState.Paused: return Brush("#FBEFD5");
                    case RunState.Succeeded: return Brush("#DFF2E5");
                    case RunState.Failed: return Brush("#F8E2DE");
                    default: return Brush("#E8DECB");
                }
            }
        }

        public System.Windows.Media.Brush StateForeground
        {
            get
            {
                switch (State)
                {
                    case RunState.Running: return Brush("#2E9E5B");
                    case RunState.Paused: return Brush("#C98A16");
                    case RunState.Succeeded: return Brush("#2E9E5B");
                    case RunState.Failed: return Brush("#C0392B");
                    default: return Brush("#6B5B47");
                }
            }
        }

        /// <summary>Pemakaian CPU proses saat automasi ini berjalan.</summary>
        private double _cpuPercent;
        public double CpuPercent
        {
            get { return _cpuPercent; }
            set { if (Math.Abs(_cpuPercent - value) < 0.01) return; _cpuPercent = value; Notify("CpuPercent"); Notify("CpuText"); }
        }

        private double _memoryMb;
        public double MemoryMb
        {
            get { return _memoryMb; }
            set { if (Math.Abs(_memoryMb - value) < 0.01) return; _memoryMb = value; Notify("MemoryMb"); Notify("MemoryText"); }
        }

        public string CpuText { get { return IsRunning ? "CPU: " + CpuPercent.ToString("0.0") + "%" : "CPU: -"; } }
        public string MemoryText { get { return IsRunning ? "Memori: " + MemoryMb.ToString("0") + " MB" : "Memori: -"; } }

        private static System.Windows.Media.Brush Brush(string hex)
        {
            var brush = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Notify(string name)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(name));
        }
    }
}

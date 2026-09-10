using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Threading;

namespace JakRunner.Core
{
    /// <summary>
    /// Angka kinerja untuk panel "Parameter Kinerja Rinci": CPU, RAM, dan
    /// jumlah core.
    ///
    /// Yang diukur PROSES INI, bukan seluruh mesin. Alasannya, yang ingin
    /// diketahui orang saat menonton robot berjalan adalah berapa banyak yang
    /// dipakai robotnya — angka seluruh mesin bercampur dengan browser, Excel,
    /// dan apa pun yang kebetulan terbuka.
    ///
    /// PerformanceCounter sengaja TIDAK dipakai. Ia mahal saat pertama dibuat
    /// (memindai seluruh kategori counter, kerap memakan detik), dan pada mesin
    /// yang daftar counter-nya rusak ia melempar kesalahan yang membingungkan.
    /// TotalProcessorTime memberi angka yang sama dengan aritmetika biasa.
    /// </summary>
    public class MachineMetrics : INotifyPropertyChanged
    {
        private readonly Process _process = Process.GetCurrentProcess();
        private readonly DispatcherTimer _timer;

        private TimeSpan _lastCpu;
        private DateTime _lastSample;

        public MachineMetrics(Dispatcher dispatcher)
        {
            _lastCpu = _process.TotalProcessorTime;
            _lastSample = DateTime.UtcNow;

            CoreCount = Environment.ProcessorCount;

            _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
            {
                Interval = TimeSpan.FromSeconds(1),
            };

            _timer.Tick += (s, e) => Sample();
            _timer.Start();
        }

        public int CoreCount { get; private set; }

        private double _cpuPercent;
        public double CpuPercent
        {
            get { return _cpuPercent; }
            private set { _cpuPercent = value; Notify("CpuPercent"); Notify("CpuText"); }
        }

        private double _memoryMb;
        public double MemoryMb
        {
            get { return _memoryMb; }
            private set { _memoryMb = value; Notify("MemoryMb"); Notify("MemoryText"); }
        }

        private int _threadCount;
        public int ThreadCount
        {
            get { return _threadCount; }
            private set { _threadCount = value; Notify("ThreadCount"); Notify("ThreadText"); }
        }

        public string CpuText { get { return CpuPercent.ToString("0.0") + " %"; } }
        public string MemoryText { get { return MemoryMb.ToString("0") + " MB"; } }
        public string CoreText { get { return CoreCount + " core"; } }
        public string ThreadText { get { return ThreadCount + " thread"; } }

        private void Sample()
        {
            try
            {
                _process.Refresh();

                var now = DateTime.UtcNow;
                var cpu = _process.TotalProcessorTime;

                var elapsed = (now - _lastSample).TotalMilliseconds;
                var used = (cpu - _lastCpu).TotalMilliseconds;

                if (elapsed > 0)
                {
                    // Dibagi jumlah core supaya hasilnya 0..100 untuk seluruh
                    // mesin, bukan 0..(100 x core).
                    var percent = used / (elapsed * Environment.ProcessorCount) * 100.0;
                    CpuPercent = Math.Max(0, Math.Min(100, percent));
                }

                _lastCpu = cpu;
                _lastSample = now;

                MemoryMb = _process.WorkingSet64 / 1024.0 / 1024.0;
                ThreadCount = _process.Threads.Count;
            }
            catch (Exception)
            {
                // Proses bisa saja sedang ditutup saat diukur; angka lama tetap
                // dipakai daripada menjatuhkan aplikasi karena sebuah angka.
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

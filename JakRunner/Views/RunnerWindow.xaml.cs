using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using JakRunner.Core;

namespace JakRunner.Views
{
    /// <summary>
    /// Jendela JakRunner: menu samping, kartu ringkasan, tabel automasi, dan
    /// lajur rincian yang bisa dilebarkan.
    /// </summary>
    public partial class RunnerWindow : Window
    {
        private readonly ObservableCollection<AutomationItem> _visible =
            new ObservableCollection<AutomationItem>();

        private List<AutomationItem> _all = new List<AutomationItem>();

        private readonly RunLog _log;
        private readonly RunnerEngine _engine;
        private readonly MachineMetrics _metrics;
        private readonly ForgeHubClient _hub;

        private bool _expanded;
        private AutomationItem _detailOf;

        /// <summary>
        /// Pekerjaan ForgeHub yang sedang dikerjakan, kalau ada.
        ///
        /// Diperlukan supaya hasil akhirnya bisa dilaporkan balik. Automasi yang
        /// dijalankan orang lewat tombol Play TIDAK punya nomor ini, dan memang
        /// tidak dilaporkan sebagai pekerjaan — ForgeHub hanya tahu tentang
        /// pekerjaan yang ia sendiri jadwalkan.
        /// </summary>
        private string _hubJobId;

        private System.Windows.Threading.DispatcherTimer _jobPoll;

        /// <summary>Berapa kali automasi selesai dan gagal HARI INI.</summary>
        private int _successToday;
        private int _failedToday;
        private DateTime _countersFor = DateTime.Today;

        private const double DetailWidth = 470;
        private const double BaseWidth = 1000;

        public RunnerWindow()
        {
            InitializeComponent();

            // Tema dan bahasa mengikuti Studio, dan diperiksa ulang tiap kali
            // jendela ini kembali aktif. Orang yang baru saja menggantinya di
            // Studio lalu berpindah ke sini akan langsung melihat hasilnya,
            // tanpa perlu menjalankan ulang JakRunner.
            Activated += (s, e) =>
            {
                if (Custom.Shared.JakForgeUi.Reload())
                {
                    Core.RunnerTheming.Apply();
                    TerjemahkanAntarmuka();
                }
            };

            TerjemahkanAntarmuka();

            _log = new RunLog(Dispatcher);
            _engine = new RunnerEngine(Dispatcher, _log);
            _metrics = new MachineMetrics(Dispatcher);
            _hub = new ForgeHubClient(_log);

            AutomationList.ItemsSource = _visible;
            LogList.ItemsSource = _log.Lines;
            ErrorList.ItemsSource = _log.Errors;

            CoreText.Text = _metrics.CoreCount.ToString();

            _engine.Changed += OnEngineChanged;
            _log.Errors.CollectionChanged += (s, e) => UpdateErrorPanel();
            _metrics.PropertyChanged += (s, e) => UpdateMetrics();

            // Saluran log robot: activity menulis ke sana, dan di sinilah ia
            // ditampung. RunLog yang kemudian menampilkannya di panel,
            // menulisnya ke berkas harian, dan mengirimkannya ke ForgeHub.
            Custom.Shared.RobotLog.Written += OnRobotLogWritten;

            Loaded += (s, e) =>
            {
                Reload();
                UpdateStats();
                UpdateMetrics();
                UpdateErrorPanel();
                ShowParameters(null);

                _hub.Start(_metrics);
                HubStatusText.Text = _hub.StatusText;
                _hub.StatusChanged += () => HubStatusText.Text = _hub.StatusText;

                // Jembatan peramban dinyalakan saat JakRunner dibuka, bukan saat
                // activity peramban pertama dijalankan: menyambung ke ekstensi
                // butuh beberapa detik, dan menunggunya di tengah automasi
                // membuat langkah pertama tampak menggantung.
                BrowserBridge.Start(_log);

                StartJobPolling();
            };

            Closed += (s, e) =>
            {
                Custom.Shared.RobotLog.Written -= OnRobotLogWritten;

                if (_jobPoll != null) _jobPoll.Stop();
                _hub.Stop();
            };
        }

        /// <summary>
        /// Terapkan bahasa ke teks yang tertulis langsung di XAML.
        ///
        /// Hanya bagian yang TETAP; teks yang dihasilkan kode (pesan log,
        /// keterangan kesalahan) tidak disentuh di sini.
        ///
        /// JakRunner tidak punya pemilih bahasanya sendiri: yang dipakai adalah
        /// setelan Studio, dan itu memang yang diminta.
        /// </summary>
        private void TerjemahkanAntarmuka()
        {
            NavBeranda.Content = Custom.Shared.JakForgeText.T("Beranda");
            NavJadwal.Content = Custom.Shared.JakForgeText.T("Jadwal");
            NavLog.Content = Custom.Shared.JakForgeText.T("Log");
            NavPengaturan.Content = Custom.Shared.JakForgeText.T("Pengaturan");
            NavProfil.Content = Custom.Shared.JakForgeText.T("Profil");

            PageTitle.Text = Custom.Shared.JakForgeText.T("Dasbor Agen Pelari");
            TableTitle.Text = Custom.Shared.JakForgeText.T("Status Agen");
            SearchPlaceholder.Text = Custom.Shared.JakForgeText.T("Cari Agen atau Log");

            StopButton.Content = Custom.Shared.JakForgeText.T("Hentikan");
            AssignButton.Content = Custom.Shared.JakForgeText.T("TUGASKAN AUTOMASI BARU");

            // Tanda "›" bukan bagian kalimatnya, jadi disambung di sini supaya
            // kamusnya tidak perlu memuat hiasan.
            ExpandButton.Content = Custom.Shared.JakForgeText.T("Lebarkan") + "  ›";
        }

        // ------------------------------------------------------------------
        // Daftar automasi
        // ------------------------------------------------------------------

        private void Reload()
        {
            try
            {
                _all = AutomationCatalog.Load();
                ApplyFilter();

                if (_all.Count == 0)
                {
                    _log.Warning("Tidak ada proyek yang ditemukan. Buat proyek dulu di JakForge Studio.");
                }
            }
            catch (Exception ex)
            {
                _log.Error("Gagal membaca daftar automasi: " + ex.Message);
            }
        }

        private void ApplyFilter()
        {
            var query = (SearchBox.Text ?? "").Trim();

            var matched = string.IsNullOrEmpty(query)
                ? _all
                : _all.Where(x =>
                        (x.Name ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                        || (x.ProjectName ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                      .ToList();

            _visible.Clear();
            foreach (var item in matched) _visible.Add(item);

            CountText.Text = matched.Count + " automasi";
            UpdateStats();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded) return;

            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                ? Visibility.Visible : Visibility.Collapsed;

            ApplyFilter();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            Reload();
        }

        // ------------------------------------------------------------------
        // Menu samping
        // ------------------------------------------------------------------

        /// <summary>
        /// Menu samping mengganti apa yang ditampilkan bagian utama.
        ///
        /// Semuanya memakai daftar yang sama, disaring berbeda — bukan halaman
        /// terpisah. Halaman terpisah untuk data yang sama hanya menambah
        /// tempat yang bisa ketinggalan zaman satu sama lain.
        /// </summary>
        private void Nav_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;

            var tag = (sender as RadioButton)?.Tag as string;

            switch (tag)
            {
                case "jadwal":
                    PageTitle.Text = "Jadwal";
                    TableTitle.Text = "Automasi terjadwal";
                    _log.Info("Penjadwalan diatur di ForgeHub; JakRunner menampilkan hasilnya.");
                    break;

                case "log":
                    PageTitle.Text = "Log";
                    TableTitle.Text = "Status Agen";
                    if (!_expanded) Expand_Click(sender, e);
                    break;

                case "pengaturan":
                    PageTitle.Text = "Pengaturan";
                    TableTitle.Text = "Status Agen";
                    ShowSettings();
                    break;

                case "profil":
                    PageTitle.Text = "Profil";
                    TableTitle.Text = "Status Agen";
                    _log.Info("Masuk sebagai " + Environment.UserName + " di " + Environment.MachineName + ".");
                    break;

                default:
                    PageTitle.Text = "Dasbor Agen Pelari";
                    TableTitle.Text = "Status Agen";
                    break;
            }
        }

        private void ShowSettings()
        {
            var message =
                "Alamat ForgeHub: " + (string.IsNullOrEmpty(ForgeHubClient.BaseUrl) ? "(belum disetel)" : ForgeHubClient.BaseUrl)
                + Environment.NewLine + Environment.NewLine
                + "Disetel lewat berkas jakrunner.json di sebelah JakRunner.exe:" + Environment.NewLine
                + "{ \"forgeHubUrl\": \"http://localhost:8080\", \"username\": \"FH_Admin\", \"password\": \"...\" }"
                + Environment.NewLine + Environment.NewLine
                + "Tanpa berkas itu JakRunner tetap bekerja penuh — hanya tidak mengirim "
                + "denyut dan log ke ForgeHub.";

            MessageBox.Show(this, message, "Pengaturan", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ------------------------------------------------------------------
        // Menjalankan
        // ------------------------------------------------------------------

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button)?.Tag as AutomationItem;
            if (item == null) return;

            _detailOf = item;
            DetailTitle.Text = "Detail Agen Diperluas: " + item.Name;

            BeginRun(item);
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            _engine.Stop();
            OnEngineChanged();
        }

        private void Assign_Click(object sender, RoutedEventArgs e)
        {
            var item = AutomationList.Items.OfType<AutomationItem>().FirstOrDefault(x => x.IsIdle);

            if (item == null)
            {
                MessageBox.Show(this, "Tidak ada automasi yang siap dijalankan.",
                    "Tugaskan automasi", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _detailOf = item;
            DetailTitle.Text = "Detail Agen Diperluas: " + item.Name;

            BeginRun(item);
        }

        private void Detail_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button)?.Tag as AutomationItem;
            if (item == null) return;

            _detailOf = item;
            DetailTitle.Text = "Detail Agen Diperluas: " + item.Name;

            if (!_expanded) Expand_Click(sender, e);
        }

        // ------------------------------------------------------------------
        // Pekerjaan dari ForgeHub
        // ------------------------------------------------------------------

        /// <summary>
        /// Tanya ForgeHub tiap 10 detik apakah ada pekerjaan untuk robot ini.
        ///
        /// Penjemputan, bukan pendorongan. JakRunner berjalan di komputer meja
        /// yang biasanya di balik NAT tanpa alamat tetap; ForgeHub tidak punya
        /// jalan untuk menghubunginya lebih dulu.
        /// </summary>
        private void StartJobPolling()
        {
            _jobPoll = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10),
            };

            _jobPoll.Tick += (s, e) => PollForJob();
            _jobPoll.Start();
        }

        private void PollForJob()
        {
            // Satu automasi pada satu waktu. Mengambil pekerjaan kedua saat yang
            // pertama masih jalan berarti dua robot memperebutkan mouse dan
            // papan ketik yang sama.
            if (_engine.Current != null && _engine.Current.IsRunning) return;
            if (string.IsNullOrEmpty(ForgeHubClient.BaseUrl)) return;

            // Permintaannya di thread latar: ForgeHub yang lambat menjawab tidak
            // boleh membekukan jendela selama 8 detik tiap 10 detik.
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                ForgeHubClient.ClaimedJob job;

                try
                {
                    job = _hub.ClaimNextJob();
                }
                catch (Exception ex)
                {
                    _log.Warning("ForgeHub: gagal menanyakan pekerjaan — " + ex.Message);
                    return;
                }

                if (job == null) return;

                Dispatcher.BeginInvoke(new Action(() => StartHubJob(job)));
            });
        }

        private void StartHubJob(ForgeHubClient.ClaimedJob job)
        {
            var item = MatchAutomation(job.ProcessName);

            if (item == null)
            {
                // Prosesnya ada di ForgeHub tapi tidak ada di komputer ini.
                // Pekerjaannya HARUS ditandai gagal, bukan didiamkan: kalau
                // tidak, ia akan berstatus "berjalan" selamanya di dasbor.
                var message = "Proses '" + job.ProcessName + "' tidak ada di komputer ini. "
                    + "Salin proyeknya ke sini, atau jalankan di robot lain.";

                _log.Error(message);
                _hub.ReportJob(job.Id, "FAULTED", 0, message);
                return;
            }

            _hubJobId = job.Id;
            Custom.Shared.RobotLog.JobId = job.Id;

            _detailOf = item;
            DetailTitle.Text = "Detail Agen Diperluas: " + item.Name;

            _log.Info("ForgeHub menugaskan " + job.ProcessName + ".");
            _hub.ReportJob(job.Id, "RUNNING", 10, "Dijalankan oleh JakRunner.");

            BeginRun(item, job.Inputs);
        }

        /// <summary>
        /// Cari automasi yang dimaksud sebuah nama proses ForgeHub.
        ///
        /// Studio menerbitkan satu PROYEK sebagai satu proses, dan titik masuknya
        /// Main.xaml — jadi yang dicari lebih dulu adalah workflow "Main" di dalam
        /// proyek bernama itu. Dua pencarian berikutnya hanya jaring pengaman
        /// untuk paket yang diterbitkan dengan cara lain.
        /// </summary>
        private AutomationItem MatchAutomation(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return null;

            var main = _all.FirstOrDefault(x =>
                string.Equals(x.ProjectName, processName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Name, "Main", StringComparison.OrdinalIgnoreCase));

            if (main != null) return main;

            var byProject = _all.FirstOrDefault(x =>
                string.Equals(x.ProjectName, processName, StringComparison.OrdinalIgnoreCase));

            if (byProject != null) return byProject;

            return _all.FirstOrDefault(x =>
                string.Equals(x.Name, processName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Satu baris dari saluran log robot masuk ke catatan JakRunner.
        ///
        /// Tingkat Trace dan Debug ikut masuk: itulah jejak activity demi
        /// activity yang membuat panel log terlihat hidup saat robot berjalan.
        /// Yang menyaring adalah pembacanya, bukan penulisnya — baris yang tidak
        /// pernah dicatat tidak bisa dipulihkan belakangan.
        /// </summary>
        private void OnRobotLogWritten(Custom.Shared.RobotLogEntry entry)
        {
            if (entry == null) return;

            LogLevel level;

            switch (entry.Level)
            {
                case Custom.Shared.RobotLogLevel.Error: level = LogLevel.Error; break;
                case Custom.Shared.RobotLogLevel.Warning: level = LogLevel.Warning; break;
                default: level = LogLevel.Info; break;
            }

            _log.Add(level, entry.Message);

            // Baris yang sama dikirim ke ForgeHub. Inilah yang membuat dasbor
            // memperlihatkan langkah demi langkah robot yang sedang berjalan,
            // bukan hanya "mulai" lalu "selesai".
            _hub.Enqueue(entry.LevelText, entry.Message, _engine.Current);
        }

        /// <summary>
        /// Mulai menjalankan satu automasi.
        ///
        /// Langganan perubahan keadaan dipasang SEBELUM Start(). Kegagalan saat
        /// memuat berkas — XAML rusak, tipe activity tidak dikenal — terjadi di
        /// dalam Start() secara langsung; kalau langganannya baru dipasang
        /// sesudahnya, perubahan ke keadaan "gagal" itu sudah lewat dan hasilnya
        /// tidak pernah dilaporkan ke ForgeHub. Pekerjaannya lalu berstatus
        /// "berjalan" selamanya di dasbor.
        /// </summary>
        private void BeginRun(AutomationItem item)
        {
            BeginRun(item, null);
        }

        /// <summary>
        /// Mulai menjalankan, dengan argumen masukan kalau pekerjaannya membawa.
        /// </summary>
        private void BeginRun(AutomationItem item, System.Collections.Generic.Dictionary<string, object> inputs)
        {
            if (item == null) return;

            item.PropertyChanged -= RunningItemChanged;
            item.PropertyChanged += RunningItemChanged;

            _hub.JobStarting(item);
            _engine.Start(item, inputs);

            OnEngineChanged();
        }

        private void OnEngineChanged()
        {
            var item = _engine.Current;

            StopButton.IsEnabled = item != null && item.IsRunning;

            if (item != null)
            {
                item.PropertyChanged -= RunningItemChanged;
                item.PropertyChanged += RunningItemChanged;
            }

            UpdateStats();
            ShowParameters(_engine.LastOutputs);
        }

        private void RunningItemChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var item = sender as AutomationItem;
            if (item == null || e.PropertyName != "State") return;

            // HANYA keadaan akhir yang dihitung dan dilaporkan.
            //
            // Perpindahan ke Running juga memicu pemberitahuan ini, dan tanpa
            // penjagaan di bawah, jalan yang baru DIMULAI langsung dilaporkan
            // ke ForgeHub sebagai "berhasil" — lalu kegagalan yang menyusul
            // beberapa milidetik kemudian tidak pernah terlihat di dasbor.
            if (item.State != RunState.Succeeded && item.State != RunState.Failed) return;

            RollCountersIfNewDay();

            if (item.State == RunState.Succeeded) _successToday++;
            if (item.State == RunState.Failed) _failedToday++;

            _hub.JobFinished(item);

            // Hasil akhirnya dilaporkan balik, lalu nomornya dilepas — supaya
            // jalan berikutnya yang dimulai orang lewat tombol Play tidak
            // ikut dilaporkan sebagai hasil pekerjaan yang sudah selesai itu.
            if (!string.IsNullOrEmpty(_hubJobId))
            {
                var failed = item.State == RunState.Failed;

                _hub.ReportJob(_hubJobId,
                    failed ? "FAULTED" : "SUCCESSFUL",
                    100,
                    failed ? item.Message : "Selesai tanpa kesalahan.");

                _hubJobId = null;
                Custom.Shared.RobotLog.JobId = null;
            }

            StopButton.IsEnabled = item.IsRunning;
            UpdateStats();
            ShowParameters(_engine.LastOutputs);
        }

        /// <summary>
        /// Hitungan "hari ini" harus benar-benar hari ini.
        ///
        /// JakRunner biasa dibiarkan terbuka semalaman; tanpa pemeriksaan ini,
        /// angka kemarin masih terpajang sebagai angka hari ini.
        /// </summary>
        private void RollCountersIfNewDay()
        {
            if (_countersFor == DateTime.Today) return;

            _countersFor = DateTime.Today;
            _successToday = 0;
            _failedToday = 0;
        }

        private void UpdateStats()
        {
            RollCountersIfNewDay();

            StatAgents.Text = _all.Count.ToString();
            StatRunning.Text = _all.Count(x => x.IsRunning).ToString();
            StatSuccess.Text = _successToday.ToString();
            StatFailed.Text = _failedToday.ToString();
        }

        // ------------------------------------------------------------------
        // Panel rincian
        // ------------------------------------------------------------------

        private void ShowParameters(IDictionary<string, object> outputs)
        {
            if (outputs == null || outputs.Count == 0)
            {
                ParameterList.ItemsSource = null;
                NoParameterText.Visibility = Visibility.Visible;
                return;
            }

            ParameterList.ItemsSource = outputs
                .Select(kv => new
                {
                    Key = kv.Key,
                    Value = kv.Value == null ? "(kosong)" : kv.Value.ToString(),
                })
                .ToList();

            NoParameterText.Visibility = Visibility.Collapsed;
        }

        private void UpdateErrorPanel()
        {
            NoErrorText.Visibility = _log.Errors.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateMetrics()
        {
            CpuText.Text = _metrics.CpuText;
            MemText.Text = _metrics.MemoryText;
            CoreText.Text = _metrics.CoreCount.ToString();
            ThreadText.Text = _metrics.ThreadCount.ToString();

            // Angka yang sama juga ditempelkan ke automasi yang sedang berjalan,
            // supaya kolom kinerja di tabel terisi.
            var running = _engine.Current;
            if (running != null && running.IsRunning)
            {
                running.CpuPercent = _metrics.CpuPercent;
                running.MemoryMb = _metrics.MemoryMb;
            }
        }

        // ------------------------------------------------------------------
        // Melebar dan menyempit
        // ------------------------------------------------------------------

        private void Expand_Click(object sender, RoutedEventArgs e)
        {
            _expanded = !_expanded;

            ExpandButton.Content = _expanded ? "‹  Sempitkan" : "Lebarkan  ›";

            if (_detailOf != null) DetailTitle.Text = "Detail Agen Diperluas: " + _detailOf.Name;

            var targetWindow = _expanded ? BaseWidth + DetailWidth : BaseWidth;
            DetailColumn.Width = new GridLength(_expanded ? DetailWidth : 0);

            var animation = new DoubleAnimation
            {
                To = targetWindow,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };

            animation.Completed += (s, args) =>
            {
                // Animasi harus dilepas, kalau tidak Width tetap terkunci
                // olehnya dan jendelanya tidak bisa diubah ukurannya dengan tangan.
                BeginAnimation(WidthProperty, null);
                Width = targetWindow;
            };

            BeginAnimation(WidthProperty, animation);
        }
    }
}

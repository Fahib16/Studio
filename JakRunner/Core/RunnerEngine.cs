using System;
using System.Activities;
using System.Activities.XamlIntegration;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Threading;

namespace JakRunner.Core
{
    /// <summary>
    /// Menjalankan satu automasi.
    ///
    /// Runtime-nya milik JakRunner sendiri: berkas .xaml dimuat dengan
    /// ActivityXamlServices lalu dijalankan lewat WorkflowApplication. Tidak ada
    /// bagian Studio yang terlibat, jadi JakRunner tetap bekerja di mesin yang
    /// hanya memasang JakRunner.
    ///
    /// Yang perlu diketahui: activity kustom dimuat dari folder yang sama dengan
    /// JakRunner.exe. Selama berkas Custom.*.dll ada di sebelahnya, workflow yang
    /// memakainya bisa dijalankan. Activity yang memang bergantung pada runtime
    /// Studio — Invoke OpenRPA, misalnya — tidak bisa, dan itu dilaporkan apa
    /// adanya sebagai kesalahan, bukan didiamkan.
    /// </summary>
    public class RunnerEngine
    {
        private readonly Dispatcher _dispatcher;
        private readonly RunLog _log;

        private WorkflowApplication _application;
        private AutomationItem _current;
        private DispatcherTimer _heartbeat;

        public RunnerEngine(Dispatcher dispatcher, RunLog log)
        {
            _dispatcher = dispatcher;
            _log = log;

            // Activity Stop Robot meminta berhenti lewat saluran bersama.
            // Tanpa pendengar ini, activity itu hanya bisa melempar; dengan
            // pendengarnya, jalannya berakhir RAPI dan tidak ditandai gagal.
            Custom.Shared.RobotControl.StopRequested += alasan =>
            {
                if (_application == null) return;

                _log.Info(string.IsNullOrWhiteSpace(alasan)
                    ? "Workflow meminta berhenti."
                    : "Workflow meminta berhenti: " + alasan);

                _stopDimintaWorkflow = true;

                // Dijadwalkan, bukan dipanggil langsung: permintaannya datang
                // DARI DALAM activity yang sedang dieksekusi, dan menghentikan
                // WorkflowApplication dari dalam eksekusinya sendiri akan
                // menemui runtime yang sedang sibuk dengan activity itu.
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    var app = _application;
                    if (app == null) return;

                    try { app.Cancel(TimeSpan.FromSeconds(10)); }
                    catch (Exception) { /* sudah berhenti sendiri di antaranya */ }
                });
            };
        }

        /// <summary>
        /// Benar kalau berhentinya diminta workflow sendiri, bukan orang.
        ///
        /// Dipakai supaya jalan yang berhenti atas kemauannya sendiri dilaporkan
        /// sebagai SELESAI, bukan sebagai dihentikan paksa.
        /// </summary>
        private bool _stopDimintaWorkflow;

        public AutomationItem Current { get { return _current; } }
        public bool IsBusy { get { return _application != null; } }

        /// <summary>Argumen keluaran dari jalan terakhir; ditampilkan di panel Parameter.</summary>
        public IDictionary<string, object> LastOutputs { get; private set; }

        public event Action Changed;

        // ------------------------------------------------------------------

        public void Start(AutomationItem item)
        {
            Start(item, null);
        }

        /// <summary>
        /// Jalankan satu automasi, dengan argumen masukan kalau ada.
        ///
        /// Argumennya datang dari pekerjaan ForgeHub (kolom input_json).
        /// Sebelumnya WorkflowApplication dibuat tanpa argumen sama sekali,
        /// jadi pekerjaan yang dijadwalkan dengan argumen selalu berjalan
        /// dengan nilai bawaan — tanpa satu pun tanda bahwa argumennya hilang.
        /// </summary>
        public void Start(AutomationItem item, Dictionary<string, object> inputs)
        {
            if (item == null) return;

            if (IsBusy)
            {
                _log.Warning("Masih ada automasi yang berjalan: " + _current.Name);
                return;
            }

            if (!File.Exists(item.FilePath))
            {
                _log.Error("Berkas tidak ditemukan: " + item.FilePath);
                item.State = RunState.Failed;
                item.Message = "Berkas tidak ditemukan";
                return;
            }

            try
            {
                _log.Clear();
                _stopDimintaWorkflow = false;
                Custom.Shared.RobotControl.Reset();
                _log.Info("Memuat " + item.Name + " (" + item.ProjectName + ")");

                // Titik tolak untuk sub-workflow yang dipanggil dari dalam.
                // Harus disetel SEBELUM pemuatan, karena Invoke Workflow bisa
                // langsung dieksekusi begitu jalannya dimulai.
                Custom.Orchestrator.Runtime.WorkflowContext.ProjectFolder =
                    item.ProjectFolder ?? Path.GetDirectoryName(item.FilePath);
                Custom.Orchestrator.Runtime.WorkflowContext.ProjectName = item.Name;

                Custom.Shared.RobotLog.ProcessName = item.Name;

                var activity = WorkflowLoader.Load(item.FilePath);

                _current = item;
                item.State = RunState.Running;
                item.Progress = 0;
                item.Message = "Berjalan";
                LastOutputs = null;

                var diterima = SaringArgumen(activity, inputs);

                _application = diterima != null && diterima.Count > 0
                    ? new WorkflowApplication(activity, diterima)
                    : new WorkflowApplication(activity);

                // Hanya pemilik WorkflowApplication yang bisa melanjutkan
                // bookmark, jadi caranya dititipkan ke saluran bersama supaya
                // activity Wait For Signal bisa dibangunkan dari luar.
                var aplikasi = _application;
                Custom.Shared.RobotControl.BookmarkResumer = (bookmark, muatan) =>
                {
                    try { return aplikasi.ResumeBookmark(bookmark, muatan) == BookmarkResumptionResult.Success; }
                    catch (Exception) { return false; }
                };

                // Pelacakan per-activity: inilah yang membuat panel log dan
                // dasbor ForgeHub menunjukkan apa yang SEDANG dikerjakan, bukan
                // hanya "mulai" lalu "selesai" dengan kekosongan di antaranya.
                _application.Extensions.Add(new RunnerTracking());

                _application.Completed = OnCompleted;
                _application.Aborted = OnAborted;
                _application.OnUnhandledException = OnUnhandledException;
                _application.Unloaded = e => { };

                StartHeartbeat();

                _log.Info("Mulai menjalankan " + item.Name);
                _application.Run();
            }
            catch (Exception ex)
            {
                _log.Error("Gagal memulai: " + Describe(ex));
                item.State = RunState.Failed;
                item.Message = ex.Message;
                Finish();
            }
        }

        public void Pause()
        {
            var app = _application;
            if (app == null || _current == null) return;

            try
            {
                // WorkflowApplication tidak punya "jeda" sungguhan. Yang bisa
                // dilakukan adalah MEMBONGKARNYA ke penyimpanan sementara —
                // tanpa penyimpanan itu, satu-satunya jeda yang jujur adalah
                // menghentikan robot. Jadi yang ditawarkan di sini memang
                // penghentian, bukan jeda yang menipu.
                _log.Warning("Jeda belum didukung runtime; automasi dihentikan.");
                Stop();
            }
            catch (Exception ex)
            {
                _log.Error("Gagal menjeda: " + Describe(ex));
            }
        }

        public void Stop()
        {
            var app = _application;
            if (app == null) return;

            try
            {
                _log.Warning("Menghentikan " + (_current != null ? _current.Name : "automasi"));
                app.Terminate("Dihentikan dari JakRunner", TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                _log.Error("Gagal menghentikan: " + Describe(ex));
                Finish();
            }
        }

        // ------------------------------------------------------------------


        /// <summary>
        /// Ambil hanya argumen yang MEMANG dideklarasikan workflow-nya.
        ///
        /// WorkflowApplication melempar kalau diberi nama argumen yang tidak
        /// dikenal, dan pekerjaan yang dijadwalkan untuk satu proses bisa saja
        /// dipakai ulang untuk proses lain yang argumennya berbeda. Menolak
        /// seluruh jalan karena satu nama yang tidak cocok adalah hukuman yang
        /// tidak sebanding — jadi yang tidak dikenal dicatat di log, dan
        /// sisanya tetap dijalankan.
        ///
        /// Argumen KELUARAN sengaja dilewati: mengisinya sebagai masukan
        /// ditolak runtime.
        /// </summary>
        private Dictionary<string, object> SaringArgumen(Activity activity, Dictionary<string, object> inputs)
        {
            if (inputs == null || inputs.Count == 0) return null;

            var dynamic = activity as DynamicActivity;

            if (dynamic == null)
            {
                _log.Warning("Argumen masukan dilewati: workflow ini tidak mendeklarasikan argumen.");
                return null;
            }

            var diterima = new Dictionary<string, object>();
            var ditolak = new List<string>();

            foreach (var pasangan in inputs)
            {
                var properti = dynamic.Properties.FirstOrDefault(p =>
                    string.Equals(p.Name, pasangan.Key, StringComparison.OrdinalIgnoreCase));

                if (properti == null) { ditolak.Add(pasangan.Key); continue; }

                var arah = properti.Value as Argument;
                if (arah != null && arah.Direction == ArgumentDirection.Out) { ditolak.Add(pasangan.Key); continue; }

                // Namanya dipakai persis seperti yang dideklarasikan workflow,
                // bukan seperti yang tertulis di pekerjaan: pencocokannya
                // mengabaikan besar-kecil huruf, tapi runtime tidak.
                diterima[properti.Name] = pasangan.Value;
            }

            if (ditolak.Count > 0)
            {
                _log.Warning("Argumen yang tidak dikenal workflow ini, dilewati: " +
                             string.Join(", ", ditolak.ToArray()));
            }

            if (diterima.Count > 0)
            {
                _log.Info("Argumen masukan: " + string.Join(", ", diterima.Keys.ToArray()));
            }

            return diterima;
        }
        private void OnCompleted(WorkflowApplicationCompletedEventArgs e)
        {
            LastOutputs = e.Outputs;

            if (e.CompletionState == ActivityInstanceState.Faulted)
            {
                _log.Error("Selesai dengan kesalahan: " + Describe(e.TerminationException));
                SetState(RunState.Failed, e.TerminationException != null ? e.TerminationException.Message : "Gagal");
            }
            else if (e.CompletionState == ActivityInstanceState.Canceled)
            {
                _log.Warning("Dibatalkan.");
                SetState(RunState.Failed, "Dibatalkan");
            }
            else
            {
                if (e.Outputs != null)
                {
                    foreach (var kv in e.Outputs)
                        _log.Info("Keluaran " + kv.Key + " = " + (kv.Value == null ? "(kosong)" : kv.Value.ToString()));
                }

                _log.Info("Selesai tanpa kesalahan.");
                SetState(RunState.Succeeded, "Selesai");
            }

            Finish();
        }

        private void OnAborted(WorkflowApplicationAbortedEventArgs e)
        {
            _log.Error("Dihentikan: " + Describe(e.Reason));
            SetState(RunState.Failed, e.Reason != null ? e.Reason.Message : "Dihentikan");
            Finish();
        }

        private UnhandledExceptionAction OnUnhandledException(WorkflowApplicationUnhandledExceptionEventArgs e)
        {
            var where = e.ExceptionSource != null ? e.ExceptionSource.DisplayName : "(tidak diketahui)";
            _log.Error("Kesalahan pada \"" + where + "\": " + Describe(e.UnhandledException));

            var trace = Where(e.UnhandledException);
            if (trace != null) _log.Error("    " + trace);

            // Dihentikan, bukan dilanjutkan: melanjutkan di atas keadaan yang
            // sudah salah menghasilkan kerusakan yang lebih sulit ditelusuri
            // daripada kesalahan aslinya.
            return UnhandledExceptionAction.Terminate;
        }

        private void SetState(RunState state, string message)
        {
            var item = _current;
            if (item == null) return;

            Run(() =>
            {
                item.State = state;
                item.Message = message;
                item.Progress = state == RunState.Succeeded ? 100 : item.Progress;
            });
        }

        private void Finish()
        {
            _application = null;

            Run(() =>
            {
                StopHeartbeat();
                var handler = Changed;
                if (handler != null) handler();
            });
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Bilah kemajuan yang bergerak selama robot hidup.
        ///
        /// WF tidak melaporkan kemajuan — tidak ada yang bisa dilaporkan, karena
        /// panjang sebuah workflow tidak diketahui sebelum dijalankan. Jadi
        /// bilah ini penanda "masih hidup", bukan ukuran seberapa jauh. Itu
        /// disebutkan juga di layar supaya tidak disalahartikan.
        /// </summary>
        private void StartHeartbeat()
        {
            StopHeartbeat();

            _heartbeat = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(400),
            };

            _heartbeat.Tick += (s, e) =>
            {
                var item = _current;
                if (item == null || !item.IsRunning) return;

                item.Progress = item.Progress >= 95 ? 10 : item.Progress + 3;
            };

            _heartbeat.Start();
        }

        private void StopHeartbeat()
        {
            if (_heartbeat == null) return;
            _heartbeat.Stop();
            _heartbeat = null;
        }

        private void Run(Action action)
        {
            if (_dispatcher == null || _dispatcher.CheckAccess()) action();
            else _dispatcher.BeginInvoke(action);
        }

        /// <summary>Pesan lengkap beserta rantai penyebabnya.</summary>
        private static string Describe(Exception ex)
        {
            if (ex == null) return "(tanpa keterangan)";

            var parts = new List<string>();
            var current = ex;

            while (current != null)
            {
                parts.Add(current.GetType().Name + ": " + current.Message);
                current = current.InnerException;
            }

            return string.Join("  <-  ", parts);
        }

        /// <summary>
        /// Beberapa baris teratas jejak tumpukan, untuk dicatat setelah pesan
        /// kesalahannya.
        ///
        /// "Object reference not set to an instance of an object" tanpa jejak
        /// tumpukan tidak memberi tahu apa pun: activity mana pun bisa
        /// menghasilkannya, dan yang null bisa apa saja. Satu baris yang menyebut
        /// method dan berkasnya sudah cukup untuk tahu ke mana harus melihat.
        ///
        /// Dibatasi enam baris — sisanya hampir selalu bagian dalam runtime WF
        /// yang sama untuk semua kesalahan.
        /// </summary>
        private static string Where(Exception ex)
        {
            var deepest = ex;
            while (deepest != null && deepest.InnerException != null) deepest = deepest.InnerException;

            if (deepest == null || string.IsNullOrEmpty(deepest.StackTrace)) return null;

            var lines = deepest.StackTrace
                .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .Take(6)
                .Select(x => x.Trim());

            return string.Join(Environment.NewLine + "    ", lines);
        }
    }
}

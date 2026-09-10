using System;
using System.Activities;
using System.Collections;
using System.ComponentModel;
using System.Threading;
using Custom.Shared;
using Newtonsoft.Json.Linq;

namespace Custom.Orchestrator.Activities
{
    /// <summary>
    /// Menjalankan proses LAIN lewat ForgeHub, di robot mana pun yang siap.
    ///
    /// Pengganti Invoke OpenFlow dan Invoke Remote OpenRPA milik OpenRPA.
    /// Keduanya menjalankan workflow di layanan OpenFlow; padanannya di
    /// JakForge adalah menitipkan pekerjaan ke ForgeHub.
    ///
    /// Bedanya dengan Invoke Workflow: Invoke Workflow menjalankan berkas
    /// .xaml DI SINI, di robot yang sama, dan menunggu selesai. Yang ini
    /// menitipkannya ke ForgeHub, yang lalu memilih robot — bisa mesin lain,
    /// bisa nanti. Dipakai untuk memecah pekerjaan ke beberapa robot, atau
    /// memulai sesuatu yang tidak perlu ditunggu.
    ///
    /// Menunggu selesai bersifat OPSIONAL, dan bawaannya TIDAK menunggu.
    /// Robot yang menahan dirinya sampai robot lain selesai adalah dua robot
    /// yang sibuk untuk satu pekerjaan.
    /// </summary>
    [Designer(typeof(StartJobDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Start Job")]
    [Description("Menitipkan proses lain ke ForgeHub untuk dijalankan robot mana pun yang siap.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.startjob.png")]
    public sealed class StartJob : CodeActivity
    {
        public StartJob()
        {
            DisplayName = "Start Job";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Process Name")]
        [Description("Nama proses seperti yang terdaftar di ForgeHub.")]
        public InArgument<string> ProcessName { get; set; }

        [Category("Input")]
        [DisplayName("Robot Name")]
        [Description("Robot tertentu. Kosong berarti robot mana saja yang siap.")]
        public InArgument<string> RobotName { get; set; }

        [Category("Input")]
        [DisplayName("Priority")]
        [Description("Low, Normal, atau High. Kosong berarti Normal.")]
        public InArgument<string> Priority { get; set; }

        [Category("Input")]
        [DisplayName("Input Arguments")]
        [Description("Argumen masukan untuk workflow-nya, mis. " +
                     "new Dictionary(Of String, Object) From {{\"in_Nama\", \"Budi\"}}.")]
        public InArgument<IDictionary> InputArguments { get; set; }

        [Category("Menunggu")]
        [DisplayName("Wait For Completion")]
        [Description("Kalau true, tunggu sampai pekerjaannya selesai (default false).")]
        public InArgument<bool> WaitForCompletion { get; set; }

        [Category("Menunggu")]
        [DisplayName("Timeout")]
        [Description("Batas menunggu; default 30 menit. Hanya dipakai kalau menunggu.")]
        public InArgument<TimeSpan> Timeout { get; set; }

        [Category("Output")]
        [DisplayName("Job Id")]
        [Description("Nomor pekerjaan yang dibuat.")]
        public OutArgument<string> JobId { get; set; }

        [Category("Output")]
        [DisplayName("Final State")]
        [Description("Keadaan akhir kalau ditunggu; kosong kalau tidak menunggu.")]
        public OutArgument<string> FinalState { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [Description("Kalau true, kegagalan tidak menghentikan workflow (default false).")]
        [System.ComponentModel.Editor(typeof(ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = ContinueOnError != null && ContinueOnError.Get(context);

            try
            {
                if (!Runtime.HubConnection.IsConfigured)
                {
                    throw new InvalidOperationException(
                        "Start Job: ForgeHub belum disetel. Isi alamatnya di Studio lewat " +
                        "Design -> ForgeHub -> Sambungkan.");
                }

                var proses = ProcessName.Get(context);

                if (string.IsNullOrWhiteSpace(proses))
                    throw new InvalidOperationException("Start Job: Process Name kosong.");

                var body = new JObject
                {
                    ["processName"] = proses,
                    ["source"] = "Workflow",
                };

                var robot = RobotName != null ? RobotName.Get(context) : null;
                if (!string.IsNullOrWhiteSpace(robot)) body["robotName"] = robot;

                var prioritas = Priority != null ? Priority.Get(context) : null;
                body["priority"] = string.IsNullOrWhiteSpace(prioritas) ? "Normal" : prioritas.Trim();

                var masukan = InputArguments != null ? InputArguments.Get(context) : null;
                if (masukan != null && masukan.Count > 0)
                {
                    var isi = new JObject();

                    foreach (DictionaryEntry e in masukan)
                        isi[Convert.ToString(e.Key)] = e.Value == null ? null : JToken.FromObject(e.Value);

                    body["inputJson"] = isi.ToString(Newtonsoft.Json.Formatting.None);
                }

                var jawaban = Runtime.HubConnection.Send("POST", "/api/jobs", body);
                var jobId = jawaban != null ? (string)jawaban["id"] : null;

                if (string.IsNullOrEmpty(jobId))
                    throw new InvalidOperationException("Start Job: ForgeHub tidak mengembalikan nomor pekerjaan.");

                if (JobId != null) JobId.Set(context, jobId);

                RobotLog.Info("Pekerjaan " + jobId.Substring(0, Math.Min(12, jobId.Length)) +
                              " untuk proses \"" + proses + "\" dititipkan ke ForgeHub.", DisplayName);

                if (WaitForCompletion == null || !WaitForCompletion.Get(context))
                {
                    if (FinalState != null) FinalState.Set(context, "");
                    return;
                }

                var batas = Timeout != null ? Timeout.Get(context) : TimeSpan.Zero;
                if (batas <= TimeSpan.Zero) batas = TimeSpan.FromMinutes(30);

                var akhir = Tunggu(jobId, batas, proses);
                if (FinalState != null) FinalState.Set(context, akhir);
            }
            catch (Exception) when (continueOnError)
            {
                if (FinalState != null) FinalState.Set(context, "");
            }
        }

        /// <summary>
        /// Tanyakan keadaan pekerjaannya sampai selesai atau waktunya habis.
        ///
        /// Jeda bertambah: mula-mula tiap dua detik, lalu melebar sampai
        /// sepuluh. Pekerjaan yang cepat selesai tetap ketahuan segera,
        /// sementara pekerjaan sejam tidak menghasilkan ribuan permintaan yang
        /// semuanya menjawab hal yang sama.
        /// </summary>
        private string Tunggu(string jobId, TimeSpan batas, string proses)
        {
            var habis = DateTime.UtcNow + batas;
            var jeda = 2;

            while (DateTime.UtcNow < habis)
            {
                Thread.Sleep(TimeSpan.FromSeconds(jeda));
                if (jeda < 10) jeda++;

                var job = Runtime.HubConnection.Send("GET", "/api/jobs/" + Uri.EscapeDataString(jobId), null);
                var keadaan = job != null ? (string)job["state"] : null;

                if (string.IsNullOrEmpty(keadaan)) continue;

                switch (keadaan.ToUpperInvariant())
                {
                    case "SUCCESSFUL":
                        return keadaan;

                    case "FAULTED":
                    case "STOPPED":
                        // Kegagalan pekerjaan yang DITUNGGU adalah kegagalan
                        // yang menunggunya juga. Menunggu lalu mengabaikan
                        // hasilnya membuat penantiannya tidak ada artinya.
                        throw new InvalidOperationException(
                            "Start Job: proses \"" + proses + "\" berakhir dengan keadaan " + keadaan +
                            ". Nomor pekerjaan " + jobId + ".");
                }
            }

            throw new TimeoutException(
                "Start Job: proses \"" + proses + "\" belum selesai setelah " +
                Math.Round(batas.TotalMinutes, 1) + " menit. Nomor pekerjaan " + jobId + ".");
        }
    }
}

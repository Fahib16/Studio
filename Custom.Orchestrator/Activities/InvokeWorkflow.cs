using System;
using System.Activities;
using System.Activities.XamlIntegration;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Custom.Orchestrator.Runtime;
using Custom.Shared;

namespace Custom.Orchestrator.Activities
{
    /// <summary>
    /// Jalankan workflow lain dari proyek yang sama.
    ///
    /// Pengganti "Invoke OpenRPA" — dan bukan sekadar ganti nama. Yang lama
    /// hidup di dalam Studio: Execute-nya mencari WorkflowInstance.Instances dan
    /// RobotInstance, jadi ia hanya bisa berjalan kalau Studio yang menjalankannya.
    /// Akibatnya template ReFramework — yang seluruhnya dirangkai dari
    /// sub-workflow — tidak bisa dijalankan JakRunner sama sekali.
    ///
    /// Yang ini berdiri di atas runtime WF biasa, jadi ia bekerja di mana pun:
    /// di kanvas Studio, di JakRunner, dan di pekerjaan yang dikirim ForgeHub.
    ///
    /// Variabel dioper berdasarkan KESAMAAN NAMA. Itulah cara ReFramework
    /// mengoper Config, TransactionItem, dan SystemException antar berkas —
    /// tidak ada pemetaan yang ditulis di mana pun.
    /// </summary>
    [Designer(typeof(InvokeWorkflowDesigner))]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.invokeworkflow.png")]
    [DisplayName("Invoke Workflow")]
    [Description("Jalankan berkas .xaml lain dari proyek ini, lalu lanjutkan.")]
    public class InvokeWorkflow : NativeActivity
    {
        public InvokeWorkflow()
        {
            Arguments = new Dictionary<string, Argument>();
        }

        [RequiredArgument]
        [Category("Input")]
        [Description("Nama berkas .xaml yang dijalankan, relatif terhadap folder proyek.")]
        public InArgument<string> WorkflowFile { get; set; }

        [Category("Input")]
        [Description("Argumen yang dioper. Kosongkan untuk mengoper variabel yang namanya sama.")]
        public Dictionary<string, Argument> Arguments { get; set; }

        [Category("Options")]
        [Description("Kalau dicentang, keluaran sub-workflow TIDAK disalin balik ke variabel di sini.")]
        public InArgument<bool> IsolateVariables { get; set; }

        // ------------------------------------------------------------------
        // Nama properti lama, demi proyek yang sudah dibuat sebelum penggantian
        //
        // Workflow yang dibuat saat activity ini masih bernama InvokeOpenRPA
        // menuliskan properti dengan nama-nama di bawah. Tanpa alias ini, berkas
        // itu gagal dimuat dengan "property not found" — dan proyek yang sudah
        // jadi tiba-tiba rusak hanya karena kami mengganti nama.
        //
        // Semuanya disembunyikan dari perancang: yang baru memakai nama baru.
        // ------------------------------------------------------------------

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public InArgument<string> workflow
        {
            get { return WorkflowFile; }
            set { WorkflowFile = value; }
        }

        /// <summary>
        /// Diterima dan diabaikan.
        ///
        /// Invoke Workflow SELALU menunggu sub-workflow selesai. Satu robot hanya
        /// punya satu mouse dan satu papan ketik, jadi menjalankan dua workflow
        /// berbarengan di mesin yang sama justru membuat keduanya saling merusak.
        /// </summary>
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public InArgument<bool> WaitForCompleted { get; set; }

        /// <summary>
        /// Diterima dan diabaikan.
        ///
        /// Peninggalan dari cara lama, saat sub-workflow dijalankan sebagai
        /// instance terpisah yang bisa masih hidup dari jalan sebelumnya. Di sini
        /// sub-workflow dijalankan langsung di dalam pemanggilnya, jadi tidak ada
        /// instance lama yang perlu dihentikan.
        /// </summary>
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public InArgument<bool> KillIfRunning { get; set; }

        /// <summary>
        /// Berkas yang sudah pernah diurai, disimpan agar tidak diurai ulang.
        ///
        /// ReFramework memanggil Process.xaml sekali per transaksi. Tanpa
        /// penyimpanan ini, seribu transaksi berarti seribu kali mengurai berkas
        /// XAML yang sama persis — bagian paling lambat dari seluruh jalannya.
        /// </summary>
        private static readonly Dictionary<string, CacheEntry> Cache =
            new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);

        private static readonly object CacheGate = new object();

        private class CacheEntry
        {
            public DateTime WrittenAt;
            public string Xaml;
        }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            var fileArgument = new RuntimeArgument("WorkflowFile", typeof(string), ArgumentDirection.In, true);
            var isolateArgument = new RuntimeArgument("IsolateVariables", typeof(bool), ArgumentDirection.In);

            metadata.AddArgument(fileArgument);
            metadata.AddArgument(isolateArgument);

            metadata.Bind(WorkflowFile, fileArgument);
            metadata.Bind(IsolateVariables, isolateArgument);

            // Nama lama tetap didaftarkan supaya berkas XAML lama yang masih
            // menuliskannya bisa dimuat. Nilainya dibaca lalu diabaikan.
            var waitArgument = new RuntimeArgument("WaitForCompleted", typeof(bool), ArgumentDirection.In);
            var killArgument = new RuntimeArgument("KillIfRunning", typeof(bool), ArgumentDirection.In);

            metadata.AddArgument(waitArgument);
            metadata.AddArgument(killArgument);

            metadata.Bind(WaitForCompleted, waitArgument);
            metadata.Bind(KillIfRunning, killArgument);

            if (Arguments == null) return;

            foreach (var entry in Arguments)
            {
                if (entry.Value == null) continue;

                var runtimeArgument = new RuntimeArgument(
                    entry.Key, entry.Value.ArgumentType, entry.Value.Direction);

                metadata.AddArgument(runtimeArgument);
                metadata.Bind(entry.Value, runtimeArgument);
            }
        }

        protected override void Execute(NativeActivityContext context)
        {
            var requested = WorkflowFile.Get(context);

            if (string.IsNullOrWhiteSpace(requested))
                throw new InvalidOperationException("Invoke Workflow tanpa nama berkas.");

            var path = WorkflowContext.Resolve(requested);

            if (path == null)
                throw new FileNotFoundException(
                    "Workflow '" + requested + "' tidak ditemukan di folder proyek "
                    + (WorkflowContext.ProjectFolder ?? "(belum diketahui)") + ".");

            RobotLog.Info("Memanggil " + Path.GetFileName(path), DisplayName);

            var child = LoadCached(path);
            var inputs = CollectInputs(context, child);

            var outputs = WorkflowInvoker.Invoke(child, inputs);

            if (!IsolateVariables.Get(context)) ApplyOutputs(context, outputs);

            RobotLog.Info("Selesai " + Path.GetFileName(path), DisplayName);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Muat berkas, pakai salinan di memori kalau berkasnya belum berubah.
        ///
        /// Yang disimpan adalah TEKS-nya, bukan Activity hasil uraiannya. Satu
        /// pohon Activity tidak boleh dijalankan dua kali secara bersarang, dan
        /// mengurai ulang dari teks jauh lebih murah daripada membaca cakram.
        /// </summary>
        private static Activity LoadCached(string path)
        {
            var writtenAt = File.GetLastWriteTimeUtc(path);
            string xaml = null;

            lock (CacheGate)
            {
                CacheEntry entry;
                if (Cache.TryGetValue(path, out entry) && entry.WrittenAt == writtenAt) xaml = entry.Xaml;
            }

            if (xaml == null)
            {
                xaml = File.ReadAllText(path);

                lock (CacheGate)
                {
                    Cache[path] = new CacheEntry { WrittenAt = writtenAt, Xaml = xaml };
                }
            }

            using (var reader = new StringReader(xaml))
            {
                return ActivityXamlServices.Load(reader);
            }
        }

        private static IDictionary<string, object> CollectInputs(NativeActivityContext context, Activity child)
        {
            var inputs = new Dictionary<string, object>();

            var dynamic = child as DynamicActivity;
            if (dynamic == null) return inputs;

            var properties = context.DataContext.GetProperties();

            foreach (DynamicActivityProperty property in dynamic.Properties)
            {
                if (property.Type == null || !property.Type.IsGenericType) continue;

                var definition = property.Type.GetGenericTypeDefinition();
                if (definition != typeof(InArgument<>) && definition != typeof(InOutArgument<>)) continue;

                var source = properties.Find(property.Name, false);
                if (source == null) continue;

                try
                {
                    inputs[property.Name] = source.GetValue(context.DataContext);
                }
                catch (Exception)
                {
                    // Variabel yang tidak terbaca dilewati; sub-workflow memakai
                    // nilai bawaan argumennya.
                }
            }

            return inputs;
        }

        private static void ApplyOutputs(NativeActivityContext context, IDictionary<string, object> outputs)
        {
            if (outputs == null || outputs.Count == 0) return;

            var properties = context.DataContext.GetProperties();

            foreach (var entry in outputs)
            {
                var target = properties.Find(entry.Key, false);
                if (target == null || target.IsReadOnly) continue;

                try
                {
                    target.SetValue(context.DataContext, entry.Value);
                }
                catch (Exception)
                {
                    // Tipe yang tidak cocok dilewati, bukan menjatuhkan seluruh
                    // jalan hanya karena satu keluaran tidak bisa ditempatkan.
                }
            }
        }
    }
}

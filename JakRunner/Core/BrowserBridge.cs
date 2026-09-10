using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using OpenRPA.Interfaces;
using OpenRPA.NM;

namespace JakRunner.Core
{
    /// <summary>
    /// Menyalakan jembatan ke peramban dari JakRunner.
    ///
    /// Activity peramban berbicara ke ekstensi Chrome lewat NMHook, dan sampai
    /// sekarang yang menyalakan NMHook hanya Studio. Akibatnya proyek yang
    /// memakai Open Browser tidak bisa dijalankan asisten sama sekali — padahal
    /// justru proyek seperti itulah yang paling sering dijadwalkan.
    ///
    /// Yang dikerjakan di sini hanya dua: menyediakan pengganti IOpenRPAClient
    /// yang dibutuhkan NMHook, dan menyambungkan pipa-pipanya. Selebihnya —
    /// ekstensi di peramban, dan program native host yang dijalankan peramban
    /// itu — sudah ada di luar sana.
    /// </summary>
    public static class BrowserBridge
    {
        private static readonly object Gate = new object();
        private static bool _started;

        /// <summary>Sudah tersambung ke setidaknya satu peramban?</summary>
        public static bool IsConnected
        {
            get
            {
                try { return NMHook.connected; }
                catch (Exception) { return false; }
            }
        }

        /// <summary>
        /// Nyalakan jembatannya. Aman dipanggil berkali-kali.
        ///
        /// TIDAK pernah melempar: peramban yang belum siap bukan alasan untuk
        /// menolak menjalankan automasi yang tidak menyentuh peramban sama sekali.
        /// Activity peramban yang dijalankan tanpa jembatan akan melapor sendiri,
        /// dengan pesan yang menyebutkan apa yang kurang.
        /// </summary>
        public static void Start(RunLog log)
        {
            lock (Gate)
            {
                if (_started) return;
                _started = true;
            }

            try
            {
                // NMHook membaca Plugin.client saat pesan dari peramban tiba —
                // misalnya saat tab berpindah atau unduhan selesai. Tanpa ini,
                // pesan pertama menjatuhkan robot dengan NullReferenceException
                // di thread pipa, jauh dari activity mana pun.
                if (Plugin.client == null) Plugin.client = new RunnerClient();

                RepairHostRegistration(log);

                NMHook.Connected += browser => log.Info("Peramban tersambung: " + browser + ".");
                NMHook.onDisconnected += browser => log.Warning("Peramban terputus: " + browser + ".");

                NMHook.checkForPipes(true, true, true);

                log.Info("Jembatan peramban dinyalakan. Menunggu Chrome dengan ekstensi JakForge.");
            }
            catch (Exception ex)
            {
                log.Warning("Jembatan peramban gagal dinyalakan — " + ex.Message
                    + ". Automasi yang tidak memakai peramban tetap bisa dijalankan.");
            }
        }

        /// <summary>
        /// Perbaiki pendaftaran native messaging host, tapi HANYA kalau rusak.
        ///
        /// Yang diperiksa bukan ada-tidaknya kunci registri, melainkan apakah
        /// berkas exe yang ditunjuk manifest itu benar-benar ada. Bedanya nyata:
        /// berkas chromemanifest.json yang ikut dibangun berisi penanda
        /// "REPLACEPATH", dan pendaftaran yang tampak lengkap di registri tetap
        /// tidak bisa dipakai peramban karena manifestnya belum menunjuk ke mana
        /// pun. Itulah keadaan yang ditemukan saat pertama kali dicoba.
        ///
        /// Kalau jalurnya sudah menunjuk berkas yang ada — pemasangan lain,
        /// versi lain — ia DIBIARKAN. Mengarahkannya ke salinan kita sendiri bisa
        /// merusak jembatan yang tadinya bekerja.
        /// </summary>
        private static void RepairHostRegistration(RunLog log)
        {
            try
            {
                if (HostManifestIsUsable()) return;

                NMHook.registreChromeNativeMessagingHost(false);
                NMHook.registreffNativeMessagingHost(false);

                log.Info(HostManifestIsUsable()
                    ? "Pendaftaran native messaging host diperbaiki."
                    : "Pendaftaran native messaging host dicoba diperbaiki, tapi masih belum lengkap.");
            }
            catch (Exception ex)
            {
                log.Warning("Perbaikan pendaftaran native messaging host dilewati — " + ex.Message);
            }
        }

        /// <summary>Apakah manifest yang ditunjuk registri menunjuk exe yang ada?</summary>
        private static bool HostManifestIsUsable()
        {
            try
            {
                string manifestPath;

                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                           @"Software\Google\Chrome\NativeMessagingHosts\com.openrpa.msg"))
                {
                    if (key == null) return false;
                    manifestPath = key.GetValue("") as string;
                }

                if (string.IsNullOrEmpty(manifestPath) || !System.IO.File.Exists(manifestPath)) return false;

                var manifest = Newtonsoft.Json.Linq.JObject.Parse(
                    System.IO.File.ReadAllText(manifestPath));

                var hostPath = (string)manifest["path"];

                return !string.IsNullOrEmpty(hostPath) && System.IO.File.Exists(hostPath);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Pengganti IOpenRPAClient seperlunya untuk JakRunner.
    ///
    /// NMHook menuntut sebuah IOpenRPAClient, tapi yang benar-benar dibacanya
    /// saat menjalankan automasi hanya dua: <see cref="WorkflowInstances"/> —
    /// dipakai untuk membangunkan detektor URL dan unduhan — dan
    /// <see cref="isRunningInChildSession"/>.
    ///
    /// Sisanya milik perancang: kanvas, jendela, daftar designer. JakRunner tidak
    /// punya satu pun, dan mengembalikan null untuk itu bukan penambal melainkan
    /// jawaban yang benar.
    /// </summary>
    internal class RunnerClient : IOpenRPAClient
    {
        // Peristiwa di bawah wajib ada karena antarmukanya menuntut, tapi
        // JakRunner tidak pernah membangkitkannya — pembacanya hanya ada di
        // Studio. Peringatan "tidak pernah dipakai" dimatikan di sini saja,
        // bukan di seluruh proyek.
#pragma warning disable 67
        public event StatusEventHandler Status;
        public event SignedinEventHandler Signedin;
        public event ConnectedEventHandler Connected;
        public event DisconnectedEventHandler Disconnected;
        public event ReadyForActionEventHandler ReadyForAction;
#pragma warning restore 67

        public ObservableCollection<IWorkitemQueue> WorkItemQueues { get; set; } =
            new ObservableCollection<IWorkitemQueue>();

        public bool isReadyForAction { get; set; } = true;

        /// <summary>
        /// Selalu false: sesi anak adalah fitur Studio, dan JakRunner selalu
        /// berjalan di sesi pengguna yang sedang login.
        /// </summary>
        public bool isRunningInChildSession { get { return false; } }

        public IMainWindow Window { get; set; }

        public IDesigner CurrentDesigner { get { return null; } }
        public IDesigner[] Designers { get { return new IDesigner[0]; } }

        /// <summary>
        /// Kosong, tapi TIDAK null.
        ///
        /// NMHook memutarinya tiap kali pesan peramban tiba. Daftar kosong berarti
        /// tidak ada yang perlu dibangunkan; null berarti NullReferenceException
        /// di thread pipa.
        /// </summary>
        public List<IWorkflowInstance> WorkflowInstances { get { return new List<IWorkflowInstance>(); } }

        public IDesigner GetWorkflowDesignerByIDOrRelativeFilename(string idOrRelativeFilename) { return null; }
        public IWorkflow GetWorkflowByIDOrRelativeFilename(string idOrRelativeFilename) { return null; }
        public IWorkflowInstance GetWorkflowInstanceByInstanceId(string instanceId) { return null; }

        public void ParseCommandLineArgs(IList<string> args) { }

    }
}

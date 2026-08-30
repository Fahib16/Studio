using System;
using System.Activities;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace Custom.StudioBridge
{
    /// <summary>
    /// Activity: Studio Launch Chrome. Khusus untuk "nyalain Chrome dari
    /// kondisi tertutup total" -- dipisah dari Studio Open Tab (saran user)
    /// supaya Open Tab tetap simpel untuk kasus normal (Chrome sudah aktif).
    ///
    /// BARU: auto-deteksi profile TERAKHIR DIPAKAI, persis seperti klik
    /// ikon Chrome biasa (dan seperti Open Browser NMHook versi lama).
    /// Chrome sendiri nyimpen info ini di file "Local State"
    /// (%LOCALAPPDATA%\Google\Chrome\User Data\Local State, JSON) --
    /// field profile.last_used. Kita baca file itu LANGSUNG (tidak perlu
    /// Chrome jalan dulu), pakai isinya sebagai --profile-directory kalau
    /// property ProfileDirectory dikosongkan. Kalau baca file ini gagal
    /// (path beda/format berubah/dll), fallback ke perilaku lama (tidak
    /// pakai argumen profile sama sekali) -- tidak bikin activity gagal
    /// cuma karena deteksi ini tidak berhasil.
    /// </summary>
    [Designer(typeof(Design.StudioLaunchChromeDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    public class StudioLaunchChrome : CodeActivity
    {
        [Category("Input")]
        [DisplayName("Url")]
        [Description("Opsional. Kosongkan untuk buka Chrome ke halaman New Tab biasa.")]
        public InArgument<string> Url { get; set; }

        [Category("Options")]
        [DisplayName("Profile Directory")]
        [Description("Opsional. Kosongkan = otomatis deteksi profile TERAKHIR DIPAKAI (persis seperti klik " +
                      "ikon Chrome biasa). Isi manual cuma kalau kamu mau paksa profile TERTENTU yang beda " +
                      "dari yang terakhir dipakai.")]
        public InArgument<string> ProfileDirectory { get; set; }

        [Category("Options")]
        [DisplayName("Wait For Ready")]
        [Description("Berapa lama tunggu Chrome+extension siap menerima command setelah diluncurkan (default 10 detik)")]
        public InArgument<TimeSpan> WaitForReady { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        public InArgument<bool> ContinueOnError { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = ContinueOnError != null ? ContinueOnError.Get(context) : false;

            try
            {
                var url = Url != null ? Url.Get(context) : null;
                var profileDirectory = ProfileDirectory != null ? ProfileDirectory.Get(context) : null;
                var waitFor = WaitForReady != null ? WaitForReady.Get(context) : TimeSpan.Zero;
                if (waitFor == TimeSpan.Zero) waitFor = TimeSpan.FromSeconds(10);

                if (string.IsNullOrEmpty(profileDirectory))
                {
                    profileDirectory = TryDetectLastUsedProfile();
                }

                var argsBuilder = new StringBuilder();
                if (!string.IsNullOrEmpty(profileDirectory))
                {
                    argsBuilder.Append($"--profile-directory=\"{profileDirectory}\" ");
                }
                if (!string.IsNullOrEmpty(url))
                {
                    argsBuilder.Append($"\"{url}\"");
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = "chrome.exe",
                    Arguments = argsBuilder.ToString().Trim(),
                    UseShellExecute = true
                });

                // Tunggu Chrome+extension+native host siap. Kita tidak
                // punya cara "tunggu event pasti" dari sini (proses ini
                // sendiri tidak nyambung ke native host) -- jadi cukup
                // Sleep sesuai WaitForReady, biarkan activity BERIKUTNYA
                // (mis. Studio Open Tab) yang benar-benar coba connect.
                Thread.Sleep((int)waitFor.TotalMilliseconds);

                // BARU: bersihkan tab "New Tab" (chrome://newtab/) kosong
                // yang KADANG ikut terbuka bersamaan -- ini perilaku
                // bawaan Chrome sendiri (setting "lanjutkan sesi
                // terakhir"/"buka halaman tertentu saat start" bisa bikin
                // Chrome nambah tab New Tab DI SAMPING url yang kita minta
                // lewat command-line argument, bukan menggantikannya).
                // Ditemukan kasus nyata: walau Url sudah diisi benar, New
                // Tab kosong tetap muncul jadi tab terpisah. Kita bersihkan
                // di sini supaya hasil akhirnya cuma 1 tab (yang diminta),
                // TIDAK FATAL kalau langkah ini gagal (mis. pipe belum
                // benar-benar siap) -- itu urusan activity berikutnya.
                try
                {
                    CleanupExtraNewTabs();
                }
                catch
                {
                    // Diamkan -- ini cuma pembersihan opsional, bukan inti
                    // tugas activity ini.
                }
            }
            catch (Exception) when (continueOnError)
            {
                // Telan error kalau ContinueOnError = true
            }
        }

        /// <summary>
        /// Baca %LOCALAPPDATA%\Google\Chrome\User Data\Local State (JSON
        /// bawaan Chrome sendiri), ambil field profile.last_used. Return
        /// null kalau file tidak ada/gagal dibaca/field tidak ketemu --
        /// caller akan fallback ke "tidak pakai argumen profile" kalau ini
        /// terjadi, bukan melempar error.
        /// </summary>
        /// <summary>
        /// Cari semua tab "New Tab" (chrome://newtab/) yang kosong, tutup
        /// semuanya. Dipanggil setelah launch+wait, sebelum activity ini
        /// selesai -- lihat catatan di Execute() soal kenapa ini perlu.
        /// </summary>
        private static void CleanupExtraNewTabs()
        {
            var result = StudioPipeClient.SendCommand("listTabs");
            var tabsArray = result as JArray ?? new JArray();

            foreach (var tab in tabsArray)
            {
                var url = tab["url"]?.Value<string>();
                if (url == "chrome://newtab/")
                {
                    var tabId = tab["id"]?.Value<int>() ?? 0;
                    if (tabId != 0)
                    {
                        StudioPipeClient.SendCommand("closeTab", new JObject { ["tabId"] = tabId });
                    }
                }
            }
        }

        private static string TryDetectLastUsedProfile()
        {
            try
            {
                var localStatePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Google", "Chrome", "User Data", "Local State");

                if (!File.Exists(localStatePath)) return null;

                var json = File.ReadAllText(localStatePath);
                var root = JObject.Parse(json);
                return root["profile"]?["last_used"]?.Value<string>();
            }
            catch
            {
                return null; // gagal baca/parse -- fallback aman, bukan error
            }
        }
    }
}

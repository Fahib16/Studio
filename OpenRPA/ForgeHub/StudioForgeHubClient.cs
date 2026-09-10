using OpenRPA.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenRPA.ForgeHub
{
    /// <summary>
    /// Penghubung Studio ke ForgeHub.
    ///
    /// Tugasnya dua: menerbitkan proyek yang sedang dibuka sebagai paket, dan
    /// membaca kembali daftar paket yang sudah ada di sana. Studio TIDAK
    /// berdenyut dan tidak mengambil pekerjaan — itu tugas JakRunner. Studio
    /// adalah tempat automasi dibuat, bukan tempat automasi berjalan.
    ///
    /// Setiap panggilan berdiri sendiri dan punya batas waktu. Studio tidak
    /// boleh membeku menunggu server yang mati, dan tidak boleh menolak bekerja
    /// hanya karena ForgeHub belum disetel.
    /// </summary>
    public class StudioForgeHubClient
    {
        private static readonly object Gate = new object();
        private static StudioForgeHubClient _instance;

        public static StudioForgeHubClient Instance
        {
            get
            {
                lock (Gate)
                {
                    if (_instance == null) _instance = new StudioForgeHubClient();
                    return _instance;
                }
            }
        }

        private string _token;
        private DateTime _tokenSince = DateTime.MinValue;

        /// <summary>
        /// Berapa lama token dianggap masih segar.
        ///
        /// Server memberi token 12 jam. Sepuluh jam memberi ruang agar Studio
        /// memperbarui sendiri sebelum kedaluwarsa, bukan menemukan tokennya
        /// mati tepat saat orangnya menekan Terbitkan.
        /// </summary>
        private static readonly TimeSpan TokenFreshFor = TimeSpan.FromHours(10);

        // ------------------------------------------------------------------
        // Setelan
        // ------------------------------------------------------------------

        public string Url { get; private set; }
        public string Username { get; private set; }

        private string _password;

        public bool IsConfigured
        {
            get { return !string.IsNullOrEmpty(Url) && !string.IsNullOrEmpty(Username); }
        }

        public bool IsConnected { get { return !string.IsNullOrEmpty(_token); } }

        /// <summary>
        /// Berkas setelan, di sebelah data Studio.
        ///
        /// Terpisah dari setelan Studio yang lain supaya bisa dihapus sendiri
        /// tanpa mengganggu apa pun, dan supaya jelas berkas mana yang memuat
        /// kata sandi kalau proyeknya dibagikan.
        /// </summary>
        private static string SettingsPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "JakForge", "forgehub.json");
            }
        }

        private class Settings
        {
            [JsonProperty("url")] public string Url { get; set; }
            [JsonProperty("username")] public string Username { get; set; }
            [JsonProperty("password")] public string Password { get; set; }
        }

        private StudioForgeHubClient()
        {
            Load();
        }

        public void Load()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return;

                var settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(SettingsPath));
                if (settings == null) return;

                Url = settings.Url;
                Username = settings.Username;
                _password = Unprotect(settings.Password);
            }
            catch (Exception ex)
            {
                Log.Error("ForgeHub: gagal membaca setelan — " + ex.Message);
            }
        }

        public void Save(string url, string username, string password)
        {
            Url = (url ?? "").TrimEnd('/');
            Username = username;
            _password = password;

            // Setelan berubah berarti token lama tidak lagi menggambarkan
            // siapa kita; ia dibuang, bukan dipakai untuk server yang baru.
            _token = null;

            try
            {
                var folder = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(new Settings
                {
                    Url = Url,
                    Username = Username,
                    Password = Protect(password),
                }, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Log.Error("ForgeHub: gagal menyimpan setelan — " + ex.Message);
            }
        }

        /// <summary>
        /// Kata sandi disandikan dengan DPAPI, terikat ke akun Windows ini.
        ///
        /// Ini bukan pengganti brankas kata sandi: siapa pun yang bisa
        /// menjalankan program sebagai pengguna ini bisa membukanya kembali.
        /// Yang dicegah adalah hal yang jauh lebih sering terjadi — berkas
        /// setelan ikut tersalin ke tempat lain, lalu sandinya terbaca sebagai
        /// teks biasa oleh siapa saja.
        /// </summary>
        private static string Protect(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;

            try
            {
                var bytes = System.Security.Cryptography.ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(value), null,
                    System.Security.Cryptography.DataProtectionScope.CurrentUser);

                return Convert.ToBase64String(bytes);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Unprotect(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;

            try
            {
                var bytes = System.Security.Cryptography.ProtectedData.Unprotect(
                    Convert.FromBase64String(value), null,
                    System.Security.Cryptography.DataProtectionScope.CurrentUser);

                return Encoding.UTF8.GetString(bytes);
            }
            catch (Exception)
            {
                // Disandikan oleh akun lain, atau berkasnya rusak. Sama saja
                // dengan tidak ada sandi: orangnya diminta mengisi ulang.
                return null;
            }
        }

        // ------------------------------------------------------------------
        // Menyambung
        // ------------------------------------------------------------------

        /// <summary>Masuk ke ForgeHub. Melempar Exception dengan sebab yang jelas kalau gagal.</summary>
        public void Connect()
        {
            if (!IsConfigured)
                throw new Exception("Alamat ForgeHub dan nama pengguna belum diisi.");

            var body = new JObject
            {
                ["username"] = Username,
                ["password"] = _password ?? "",
            };

            var response = Send("POST", "/api/auth/login", body.ToString(), false);
            var parsed = JObject.Parse(response);

            _token = (string)parsed["token"];
            _tokenSince = DateTime.UtcNow;

            if (string.IsNullOrEmpty(_token))
                throw new Exception("ForgeHub tidak memberikan token.");

            Log.Information("ForgeHub: tersambung ke " + Url + " sebagai " + Username + ".");
        }

        private void EnsureConnected()
        {
            if (!IsConnected || DateTime.UtcNow - _tokenSince > TokenFreshFor) Connect();
        }

        // ------------------------------------------------------------------
        // Menerbitkan
        // ------------------------------------------------------------------


        /// <summary>
        /// Versi BERIKUTNYA untuk sebuah proyek.
        ///
        /// Sebelumnya versi dipatok mati "1.0.0" di tempat pemanggilnya, jadi
        /// setiap penerbitan menimpa versi yang sama: riwayatnya tidak pernah
        /// bertambah, dan tidak ada cara mengetahui paket mana yang sedang
        /// dijalankan robot.
        ///
        /// Yang dinaikkan angka KETIGA. Angka pertama dan kedua urusan orang
        /// yang memutuskan bahwa sesuatu berubah besar; menerbitkan ulang dari
        /// Studio bukan keputusan seperti itu.
        ///
        /// Gagal menanyakan daftar paket TIDAK menghalangi penerbitan: yang
        /// dikembalikan "1.0.0", dan ForgeHub tetap menerimanya. Menolak
        /// menerbitkan hanya karena nomor versinya tidak bisa dihitung adalah
        /// hukuman yang tidak sebanding.
        /// </summary>
        public string NextVersion(string projectName)
        {
            try
            {
                var response = Send("GET", "/api/packages", null, true);
                var daftar = JArray.Parse(response);

                var tertinggi = new int[] { 0, 0, 0 };
                var ketemu = false;

                foreach (var p in daftar)
                {
                    if (!string.Equals((string)p["name"], projectName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var angka = Urai((string)p["version"]);
                    if (angka == null) continue;

                    ketemu = true;
                    if (LebihBesar(angka, tertinggi)) tertinggi = angka;
                }

                if (!ketemu) return "1.0.0";

                return tertinggi[0] + "." + tertinggi[1] + "." + (tertinggi[2] + 1);
            }
            catch (Exception ex)
            {
                Log.Debug("ForgeHub: versi berikutnya tidak bisa dihitung, memakai 1.0.0 — " + ex.Message);
                return "1.0.0";
            }
        }

        /// <summary>"1.2.3" menjadi {1,2,3}; null kalau bentuknya bukan itu.</summary>
        private static int[] Urai(string versi)
        {
            if (string.IsNullOrWhiteSpace(versi)) return null;

            var bagian = versi.Trim().Split('.');
            if (bagian.Length < 1 || bagian.Length > 3) return null;

            var hasil = new int[] { 0, 0, 0 };

            for (var i = 0; i < bagian.Length; i++)
            {
                int n;
                if (!int.TryParse(bagian[i], out n) || n < 0) return null;
                hasil[i] = n;
            }

            return hasil;
        }

        private static bool LebihBesar(int[] a, int[] b)
        {
            for (var i = 0; i < 3; i++)
            {
                if (a[i] != b[i]) return a[i] > b[i];
            }
            return false;
        }
        /// <summary>
        /// Kemas satu folder proyek menjadi zip lalu kirim ke ForgeHub.
        ///
        /// Mengembalikan keterangan singkat untuk ditampilkan ke pengguna.
        /// </summary>
        public string PublishProject(string projectFolder, string projectName, string version, string description)
        {
            if (string.IsNullOrEmpty(projectFolder) || !Directory.Exists(projectFolder))
                throw new Exception("Folder proyek tidak ditemukan: " + projectFolder);

            EnsureConnected();

            var archive = Path.Combine(Path.GetTempPath(),
                "jakforge-publish-" + Guid.NewGuid().ToString("N") + ".zip");

            try
            {
                ZipFolder(projectFolder, archive);

                var bytes = File.ReadAllBytes(archive);

                if (bytes.LongLength > 64L * 1024 * 1024)
                    throw new Exception("Proyek ini lebih dari 64 MB setelah dikemas. "
                        + "Keluarkan berkas besar dari folder proyek sebelum menerbitkan.");

                var body = new JObject
                {
                    ["name"] = projectName,
                    ["version"] = version,
                    ["description"] = description,
                    ["entryPoint"] = "Main.xaml",
                    ["contentBase64"] = Convert.ToBase64String(bytes),
                };

                var response = Send("POST", "/api/packages", body.ToString(), true);
                var parsed = JObject.Parse(response);

                var size = (long?)parsed["sizeBytes"] ?? bytes.LongLength;

                Log.Information("ForgeHub: " + projectName + " " + version + " diterbitkan (" + size + " bita).");

                return projectName + " " + version + " diterbitkan ke ForgeHub ("
                    + (size / 1024) + " KB). Prosesnya sudah siap dijalankan robot.";
            }
            finally
            {
                try { if (File.Exists(archive)) File.Delete(archive); } catch (Exception) { }
            }
        }

        /// <summary>
        /// Kemas folder, lewati apa yang tidak perlu ikut.
        ///
        /// ZipFile.CreateFromDirectory tidak bisa menyaring, dan tanpa saringan
        /// folder .git beserta seluruh riwayatnya ikut terkirim — kerap jauh
        /// lebih besar daripada proyeknya sendiri.
        /// </summary>
        private static void ZipFolder(string folder, string target)
        {
            var skipDirectories = new[] { ".git", ".vs", "bin", "obj", "node_modules", ".jakforge-cache" };

            using (var stream = new FileStream(target, FileMode.Create))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                var root = folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

                foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                {
                    var relative = file.Substring(root.Length);
                    var parts = relative.Split(Path.DirectorySeparatorChar);

                    var skip = false;
                    for (var i = 0; i < parts.Length - 1; i++)
                    {
                        foreach (var name in skipDirectories)
                        {
                            if (string.Equals(parts[i], name, StringComparison.OrdinalIgnoreCase)) { skip = true; break; }
                        }
                        if (skip) break;
                    }

                    if (skip) continue;

                    // Pemisah jalur di dalam zip selalu "/", apa pun sistemnya.
                    var entryName = relative.Replace(Path.DirectorySeparatorChar, '/');

                    zip.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
                }
            }
        }

        // ------------------------------------------------------------------
        // Membaca
        // ------------------------------------------------------------------

        public List<string> ListProcesses()
        {
            EnsureConnected();

            var response = Send("GET", "/api/processes", null, true);
            var array = JArray.Parse(response);

            var names = new List<string>();
            foreach (var item in array) names.Add((string)item["name"]);

            return names;
        }

        /// <summary>Jadwalkan satu proses untuk dijalankan robot mana pun yang siap.</summary>
        public void StartJob(string processName)
        {
            EnsureConnected();

            var body = new JObject
            {
                ["processName"] = processName,
                ["source"] = "Studio",
                ["priority"] = "Normal",
            };

            Send("POST", "/api/jobs", body.ToString(), true);
        }

        // ------------------------------------------------------------------
        // Pengiriman
        // ------------------------------------------------------------------

        private string Send(string method, string path, string json, bool withToken)
        {
            if (string.IsNullOrEmpty(Url))
                throw new Exception("Alamat ForgeHub belum diisi.");

            var request = (HttpWebRequest)WebRequest.Create(Url.TrimEnd('/') + path);
            request.Method = method;
            request.ContentType = "application/json";

            // Penerbitan mengirim berkas berukuran megabita lewat jaringan yang
            // bisa lambat; batas waktunya jauh lebih longgar daripada denyut.
            request.Timeout = 120000;
            request.ReadWriteTimeout = 120000;

            if (withToken && !string.IsNullOrEmpty(_token))
                request.Headers["Authorization"] = "Bearer " + _token;

            if (json != null)
            {
                var payload = Encoding.UTF8.GetBytes(json);
                request.ContentLength = payload.Length;

                using (var stream = request.GetRequestStream())
                    stream.Write(payload, 0, payload.Length);
            }

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                // Pesan kesalahan dari server jauh lebih berguna daripada
                // "The remote server returned an error: (400) Bad Request",
                // jadi badan tanggapannya dibaca dan dipakai kalau ada.
                var http = ex.Response as HttpWebResponse;

                if (http != null)
                {
                    if (http.StatusCode == HttpStatusCode.Unauthorized) _token = null;

                    string detail = null;

                    try
                    {
                        using (var reader = new StreamReader(http.GetResponseStream(), Encoding.UTF8))
                        {
                            var text = reader.ReadToEnd();
                            if (!string.IsNullOrEmpty(text))
                            {
                                var parsed = JObject.Parse(text);
                                detail = (string)parsed["error"];
                            }
                        }
                    }
                    catch (Exception) { }

                    if (!string.IsNullOrEmpty(detail)) throw new Exception(detail);

                    throw new Exception("ForgeHub menjawab " + (int)http.StatusCode + " " + http.StatusCode + ".");
                }

                throw new Exception("ForgeHub tidak bisa dihubungi di " + Url + " — " + ex.Message);
            }
        }
    }
}

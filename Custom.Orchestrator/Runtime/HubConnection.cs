using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Custom.Orchestrator.Runtime
{
    /// <summary>
    /// Sambungan ke ForgeHub yang dipakai activity saat robot BERJALAN.
    ///
    /// Sengaja tidak bergantung pada apa pun milik Studio maupun JakRunner:
    /// activity yang sama harus bekerja di dua tempat itu, dan proyek ini
    /// dirancang supaya keduanya bisa merujuknya tanpa saling menyeret.
    ///
    /// Setelannya dicari di dua tempat, berurutan:
    ///   1. jakrunner.json di sebelah program yang sedang berjalan
    ///   2. %LOCALAPPDATA%\JakForge\forgehub.json (dituliskan Studio)
    ///
    /// Yang pertama ketemu dipakai. Dengan begitu, JakRunner memakai berkasnya
    /// sendiri sementara Studio memakai setelan yang diisi lewat dialog
    /// Sambungkan — tanpa activity-nya perlu tahu ia sedang berjalan di mana.
    /// </summary>
    public static class HubConnection
    {
        private static readonly object Gate = new object();

        private static Settings _settings;
        private static bool _settingsRead;
        private static string _token;
        private static DateTime _tokenSince = DateTime.MinValue;

        private static readonly TimeSpan TokenFreshFor = TimeSpan.FromHours(10);

        private class Settings
        {
            [JsonProperty("forgeHubUrl")] public string ForgeHubUrl { get; set; }
            [JsonProperty("url")] public string Url { get; set; }
            [JsonProperty("username")] public string Username { get; set; }
            [JsonProperty("password")] public string Password { get; set; }
            [JsonProperty("robotName")] public string RobotName { get; set; }

            /// <summary>Kedua berkas memakai nama kunci berbeda untuk hal yang sama.</summary>
            public string Address { get { return string.IsNullOrEmpty(ForgeHubUrl) ? Url : ForgeHubUrl; } }
        }

        // ------------------------------------------------------------------

        /// <summary>Alamat ForgeHub, atau null kalau memang belum disetel.</summary>
        public static string BaseUrl
        {
            get
            {
                var settings = Load();
                return settings == null ? null : settings.Address;
            }
        }

        public static bool IsConfigured { get { return !string.IsNullOrEmpty(BaseUrl); } }

        public static string RobotName
        {
            get
            {
                var settings = Load();

                if (settings != null && !string.IsNullOrEmpty(settings.RobotName))
                    return settings.RobotName;

                return Environment.MachineName + "-" + Environment.UserName;
            }
        }

        /// <summary>
        /// Buang setelan dan token yang tersimpan.
        ///
        /// Dipanggil setelah setelan diubah dari Studio, supaya activity
        /// berikutnya membaca berkas yang baru dan bukan yang sudah usang di memori.
        /// </summary>
        public static void Forget()
        {
            lock (Gate)
            {
                _settings = null;
                _settingsRead = false;
                _token = null;
            }
        }

        private static Settings Load()
        {
            lock (Gate)
            {
                if (_settingsRead) return _settings;

                _settingsRead = true;
                _settings = ReadFirstAvailable();

                return _settings;
            }
        }

        private static Settings ReadFirstAvailable()
        {
            foreach (var path in CandidatePaths())
            {
                try
                {
                    if (!File.Exists(path)) continue;

                    var parsed = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(path));
                    if (parsed != null && !string.IsNullOrEmpty(parsed.Address)) return parsed;
                }
                catch (Exception)
                {
                    // Berkas rusak sama saja dengan tidak ada: dilewati diam-diam,
                    // dan activity akan melapor "ForgeHub belum disetel".
                }
            }

            return null;
        }

        private static IEnumerable<string> CandidatePaths()
        {
            string beside = null;

            try
            {
                var folder = Path.GetDirectoryName(
                    System.Reflection.Assembly.GetEntryAssembly() != null
                        ? System.Reflection.Assembly.GetEntryAssembly().Location
                        : System.Reflection.Assembly.GetExecutingAssembly().Location);

                if (!string.IsNullOrEmpty(folder)) beside = Path.Combine(folder, "jakrunner.json");
            }
            catch (Exception)
            {
            }

            if (beside != null) yield return beside;

            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "JakForge", "forgehub.json");
        }

        // ------------------------------------------------------------------
        // Kata sandi Studio disimpan tersandi DPAPI; JakRunner menyimpannya apa adanya.
        // ------------------------------------------------------------------

        private static string Password(Settings settings)
        {
            var raw = settings.Password;
            if (string.IsNullOrEmpty(raw)) return "";

            // Ditebak dari bentuknya: hasil DPAPI selalu base64 dan panjang.
            // Kalau pembukaannya gagal, nilainya memang kata sandi biasa.
            try
            {
                var bytes = Convert.FromBase64String(raw);

                var plain = System.Security.Cryptography.ProtectedData.Unprotect(
                    bytes, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);

                return Encoding.UTF8.GetString(plain);
            }
            catch (Exception)
            {
                return raw;
            }
        }

        // ------------------------------------------------------------------
        // Permintaan
        // ------------------------------------------------------------------

        /// <summary>
        /// Kirim permintaan ke ForgeHub dan kembalikan jawabannya.
        ///
        /// Melempar Exception dengan sebab yang bisa dibaca kalau gagal. Activity
        /// yang memakainya memang HARUS gagal saat ForgeHub tidak bisa dihubungi:
        /// berbeda dengan denyut dan pengiriman log yang boleh hilang tanpa
        /// akibat, mengambil butir antrean yang diam-diam gagal akan membuat
        /// robot memproses "tidak ada transaksi" seolah pekerjaannya sudah habis.
        /// </summary>
        public static JToken Send(string method, string path, JObject body)
        {
            var settings = Load();

            if (settings == null)
                throw new InvalidOperationException(
                    "ForgeHub belum disetel. Di Studio: Design → ForgeHub → Sambungkan. "
                    + "Di JakRunner: isi jakrunner.json di sebelah JakRunner.exe.");

            EnsureToken(settings);

            var response = Request(settings, method, path, body == null ? null : body.ToString(), true);

            return string.IsNullOrEmpty(response) ? null : JToken.Parse(response);
        }

        private static void EnsureToken(Settings settings)
        {
            lock (Gate)
            {
                if (!string.IsNullOrEmpty(_token) && DateTime.UtcNow - _tokenSince < TokenFreshFor) return;
            }

            var body = new JObject
            {
                ["username"] = settings.Username,
                ["password"] = Password(settings),
            };

            var response = Request(settings, "POST", "/api/auth/login", body.ToString(), false);
            var token = (string)JObject.Parse(response)["token"];

            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("ForgeHub menolak nama pengguna atau kata sandi.");

            lock (Gate)
            {
                _token = token;
                _tokenSince = DateTime.UtcNow;
            }
        }

        private static string Request(Settings settings, string method, string path, string json, bool withToken)
        {
            var request = (HttpWebRequest)WebRequest.Create(settings.Address.TrimEnd('/') + path);
            request.Method = method;
            request.ContentType = "application/json";
            request.Timeout = 30000;
            request.ReadWriteTimeout = 30000;

            if (withToken)
            {
                string token;
                lock (Gate) token = _token;

                if (!string.IsNullOrEmpty(token)) request.Headers["Authorization"] = "Bearer " + token;
            }

            // GET tidak boleh punya badan permintaan: memanggil GetRequestStream()
            // untuk GET melempar sebelum permintaannya sempat dikirim.
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
                var http = ex.Response as HttpWebResponse;

                if (http == null)
                    throw new InvalidOperationException(
                        "ForgeHub di " + settings.Address + " tidak bisa dihubungi — " + ex.Message);

                if (http.StatusCode == HttpStatusCode.Unauthorized)
                {
                    lock (Gate) _token = null;
                }

                // Pesan dari server jauh lebih berguna daripada "(400) Bad Request".
                string detail = null;

                try
                {
                    using (var reader = new StreamReader(http.GetResponseStream(), Encoding.UTF8))
                    {
                        var text = reader.ReadToEnd();
                        if (!string.IsNullOrEmpty(text)) detail = (string)JObject.Parse(text)["error"];
                    }
                }
                catch (Exception)
                {
                }

                throw new InvalidOperationException(detail
                    ?? ("ForgeHub menjawab " + (int)http.StatusCode + " " + http.StatusCode + "."));
            }
        }
    }
}

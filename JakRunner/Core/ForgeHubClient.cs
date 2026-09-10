using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JakRunner.Core
{
    /// <summary>
    /// Penghubung ke ForgeHub: mengirim denyut robot, log, dan keadaan
    /// pekerjaan.
    ///
    /// Seluruhnya OPSIONAL dan tidak pernah menghalangi. Kalau berkas
    /// setelannya tidak ada, atau ForgeHub sedang mati, JakRunner tetap
    /// bekerja penuh — automasi tetap bisa dijalankan, log tetap tercatat di
    /// berkas harian. Itu keputusan yang disengaja: asisten yang menolak
    /// bekerja karena servernya tidak bisa dihubungi adalah asisten yang tidak
    /// berguna justru saat jaringannya sedang bermasalah.
    ///
    /// Pengirimannya SELALU di thread latar. Satu permintaan HTTP ke server
    /// yang mati menunggu sampai batas waktunya habis, dan menunggu itu di
    /// thread UI berarti jendelanya membeku.
    /// </summary>
    public class ForgeHubClient
    {
        private readonly RunLog _log;
        private readonly object _gate = new object();
        private readonly Queue<JObject> _pendingLogs = new Queue<JObject>();

        private Timer _heartbeat;
        private Timer _logShipper;
        private MachineMetrics _metrics;

        private string _token;
        private bool _connected;
        private DateTime _lastAttempt = DateTime.MinValue;

        private static Settings _settings;

        public ForgeHubClient(RunLog log)
        {
            _log = log;
        }

        public event Action StatusChanged;

        public string StatusText
        {
            get
            {
                if (_settings == null || string.IsNullOrEmpty(_settings.ForgeHubUrl))
                    return "ForgeHub: tidak disetel";

                return _connected
                    ? "ForgeHub: tersambung\n" + _settings.ForgeHubUrl
                    : "ForgeHub: tidak tersambung\n" + _settings.ForgeHubUrl;
            }
        }

        public static string BaseUrl
        {
            get { return _settings != null ? _settings.ForgeHubUrl : null; }
        }

        // ------------------------------------------------------------------

        private class Settings
        {
            [JsonProperty("forgeHubUrl")] public string ForgeHubUrl { get; set; }
            [JsonProperty("username")] public string Username { get; set; }
            [JsonProperty("password")] public string Password { get; set; }
            [JsonProperty("robotName")] public string RobotName { get; set; }
        }

        /// <summary>
        /// Baca setelan dari jakrunner.json di sebelah JakRunner.exe.
        ///
        /// Kredensial disimpan di berkas terpisah, bukan ditanam di kode: yang
        /// pertama bisa diganti tanpa membangun ulang, dan bisa tidak
        /// diikutsertakan saat aplikasinya dibagikan.
        /// </summary>
        private static Settings LoadSettings()
        {
            try
            {
                var folder = Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
                var path = Path.Combine(folder, "jakrunner.json");

                if (!File.Exists(path)) return null;

                return JsonConvert.DeserializeObject<Settings>(File.ReadAllText(path));
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void Start(MachineMetrics metrics)
        {
            _metrics = metrics;
            _settings = LoadSettings();

            if (_settings == null || string.IsNullOrEmpty(_settings.ForgeHubUrl))
            {
                _log.Info("ForgeHub tidak disetel — JakRunner bekerja mandiri.");
                Notify();
                return;
            }

            if (string.IsNullOrEmpty(_settings.RobotName))
                _settings.RobotName = Environment.MachineName + "-" + Environment.UserName;

            _log.Info("Menyambung ke ForgeHub di " + _settings.ForgeHubUrl + "...");

            // Denyut tiap 15 detik; log dikirim tiap 3 detik dalam satu bundel.
            _heartbeat = new Timer(_ => SendHeartbeat(), null, TimeSpan.Zero, TimeSpan.FromSeconds(15));
            _logShipper = new Timer(_ => ShipLogs(), null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
        }

        public void Stop()
        {
            try { if (_heartbeat != null) _heartbeat.Dispose(); } catch (Exception) { }
            try { if (_logShipper != null) _logShipper.Dispose(); } catch (Exception) { }
        }

        // ------------------------------------------------------------------
        // Kejadian yang dilaporkan
        // ------------------------------------------------------------------

        public void JobStarting(AutomationItem item)
        {
            if (item == null) return;

            Enqueue("INFO", "Mulai menjalankan " + item.Name, item);
            SendHeartbeatAsync("BUSY");
        }

        public void JobFinished(AutomationItem item)
        {
            if (item == null) return;

            var level = item.State == RunState.Failed ? "ERROR" : "INFO";
            var text = item.State == RunState.Failed
                ? "GAGAL: " + item.Name + " — " + item.Message
                : "Selesai: " + item.Name;

            Enqueue(level, text, item);
            SendHeartbeatAsync("AVAILABLE");
        }

        /// <summary>Satu baris log untuk dikirim pada bundel berikutnya.</summary>
        public void Enqueue(string level, string message, AutomationItem item)
        {
            if (_settings == null || string.IsNullOrEmpty(_settings.ForgeHubUrl)) return;

            var line = new JObject
            {
                ["level"] = level,
                ["message"] = message,
                ["robotName"] = _settings.RobotName,
                ["machineName"] = Environment.MachineName,
                ["processName"] = item != null ? item.Name : Custom.Shared.RobotLog.ProcessName,

                // Nomor pekerjaan ikut, supaya dasbor bisa menampilkan log SATU
                // jalan saja alih-alih seluruh riwayat penyewa yang tercampur.
                ["jobId"] = Custom.Shared.RobotLog.JobId,

                ["loggedAt"] = DateTime.Now.ToString("o"),
            };

            lock (_gate)
            {
                _pendingLogs.Enqueue(line);

                // Kalau ForgeHub lama tidak bisa dihubungi, antreannya dibatasi
                // supaya tidak menumpuk tanpa batas di memori.
                while (_pendingLogs.Count > 500) _pendingLogs.Dequeue();
            }
        }

        // ------------------------------------------------------------------
        // Pengiriman
        // ------------------------------------------------------------------

        private void SendHeartbeatAsync(string status)
        {
            ThreadPool.QueueUserWorkItem(_ => SendHeartbeat(status));
        }

        private void SendHeartbeat(string status = "AVAILABLE")
        {
            if (_settings == null || string.IsNullOrEmpty(_settings.ForgeHubUrl)) return;
            if (!EnsureToken()) return;

            var body = new JObject
            {
                ["status"] = status,
                ["cpuPercent"] = _metrics != null ? _metrics.CpuPercent : 0,
                ["memoryMb"] = _metrics != null ? _metrics.MemoryMb : 0,
                ["machineName"] = Environment.MachineName,
            };

            var ok = Post("/api/robots/" + Uri.EscapeDataString(_settings.RobotName) + "/heartbeat", body.ToString());
            SetConnected(ok);
        }

        // ------------------------------------------------------------------
        // Pekerjaan yang dikirim ForgeHub
        // ------------------------------------------------------------------

        /// <summary>Satu pekerjaan yang sudah menjadi milik robot ini.</summary>
        public class ClaimedJob
        {
            public string Id;
            public string ProcessName;

            /// <summary>
            /// Argumen masukan pekerjaan, dari kolom input_json.
            ///
            /// ForgeHub sudah lama mengirimkannya, tapi sebelumnya tidak ada
            /// yang membacanya di sini -- sehingga pekerjaan yang dijadwalkan
            /// dengan argumen selalu berjalan dengan nilai bawaan, tanpa
            /// satu pun tanda bahwa argumennya hilang.
            /// </summary>
            public Dictionary<string, object> Inputs;
        }

        /// <summary>
        /// Ambil satu pekerjaan berikutnya, atau null kalau tidak ada.
        ///
        /// Sisi server menandai pekerjaannya RUNNING dalam langkah yang sama,
        /// jadi begitu ini mengembalikan sesuatu, pekerjaan itu SUDAH menjadi
        /// tanggung jawab robot ini — tidak ada robot lain yang akan mendapatnya.
        /// </summary>
        public ClaimedJob ClaimNextJob()
        {
            if (_settings == null || string.IsNullOrEmpty(_settings.ForgeHubUrl)) return null;
            if (!EnsureToken()) return null;

            var response = Send("GET",
                "/api/jobs/next?robot=" + Uri.EscapeDataString(_settings.RobotName), null, true);

            if (response == null) { SetConnected(false); return null; }

            SetConnected(true);

            try
            {
                var parsed = JObject.Parse(response);
                var job = parsed["job"];

                if (job == null || job.Type == JTokenType.Null) return null;

                return new ClaimedJob
                {
                    Id = (string)job["id"],
                    ProcessName = (string)job["processName"],
                    Inputs = BacaMasukan(job["inputJson"] ?? job["input_json"]),
                };
            }
            catch (Exception ex)
            {
                _log.Warning("ForgeHub: jawaban pekerjaan tidak bisa dibaca — " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Ubah input_json pekerjaan menjadi pasangan nama-nilai.
        ///
        /// Isinya bisa berupa objek JSON, bisa juga string yang BERISI JSON --
        /// tergantung bagaimana pekerjaannya dibuat. Keduanya diterima, dan
        /// bentuk yang tidak dikenali menghasilkan daftar kosong, bukan
        /// kegagalan: argumen yang salah bentuk tidak boleh membatalkan
        /// seluruh pekerjaan sebelum robotnya sempat mulai.
        /// </summary>
        private Dictionary<string, object> BacaMasukan(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;

            try
            {
                var obj = token as JObject;

                if (obj == null)
                {
                    var teks = token.Type == JTokenType.String ? (string)token : null;
                    if (string.IsNullOrWhiteSpace(teks)) return null;
                    obj = JObject.Parse(teks);
                }

                var hasil = new Dictionary<string, object>();

                foreach (var p in obj.Properties())
                {
                    switch (p.Value.Type)
                    {
                        case JTokenType.Integer: hasil[p.Name] = (long)p.Value; break;
                        case JTokenType.Float: hasil[p.Name] = (double)p.Value; break;
                        case JTokenType.Boolean: hasil[p.Name] = (bool)p.Value; break;
                        case JTokenType.Null: hasil[p.Name] = null; break;
                        default: hasil[p.Name] = (string)p.Value; break;
                    }
                }

                return hasil.Count == 0 ? null : hasil;
            }
            catch (Exception ex)
            {
                _log.Warning("ForgeHub: argumen pekerjaan tidak bisa dibaca — " + ex.Message);
                return null;
            }
        }

        /// <summary>Laporkan kemajuan atau hasil akhir sebuah pekerjaan.</summary>
        public void ReportJob(string jobId, string state, int progress, string info)
        {
            if (string.IsNullOrEmpty(jobId)) return;
            if (_settings == null || string.IsNullOrEmpty(_settings.ForgeHubUrl)) return;

            var body = new JObject
            {
                ["state"] = state,
                ["progress"] = progress,
                ["info"] = info,
            };

            // Di thread latar: laporan yang gagal terkirim tidak boleh membekukan
            // jendela, dan tidak boleh menghentikan automasi yang sedang berjalan.
            ThreadPool.QueueUserWorkItem(_ =>
            {
                if (!EnsureToken()) return;
                Post("/api/jobs/" + Uri.EscapeDataString(jobId) + "/state", body.ToString());
            });
        }

        private void ShipLogs()
        {
            if (_settings == null || string.IsNullOrEmpty(_settings.ForgeHubUrl)) return;

            JArray lines;

            lock (_gate)
            {
                if (_pendingLogs.Count == 0) return;

                lines = new JArray();
                while (_pendingLogs.Count > 0) lines.Add(_pendingLogs.Dequeue());
            }

            if (!EnsureToken())
            {
                // Gagal masuk: barisnya dikembalikan ke antrean, jangan dibuang.
                lock (_gate)
                {
                    foreach (var line in lines) _pendingLogs.Enqueue((JObject)line);
                }
                return;
            }

            var body = new JObject { ["lines"] = lines };

            if (!Post("/api/logs", body.ToString()))
            {
                lock (_gate)
                {
                    foreach (var line in lines) _pendingLogs.Enqueue((JObject)line);
                }
                SetConnected(false);
            }
        }

        /// <summary>
        /// Pastikan ada token yang bisa dipakai.
        ///
        /// Percobaan masuk dibatasi sekali per 30 detik. Tanpa jeda itu, setiap
        /// denyut dan setiap bundel log akan mencoba masuk lagi ke server yang
        /// sedang mati — puluhan permintaan per menit yang semuanya gagal.
        /// </summary>
        private bool EnsureToken()
        {
            if (!string.IsNullOrEmpty(_token)) return true;
            if ((DateTime.UtcNow - _lastAttempt).TotalSeconds < 30) return false;

            _lastAttempt = DateTime.UtcNow;

            try
            {
                var body = new JObject
                {
                    ["username"] = _settings.Username,
                    ["password"] = _settings.Password,
                };

                var response = Send("POST", "/api/auth/login", body.ToString(), false);
                if (response == null) { SetConnected(false); return false; }

                var parsed = JObject.Parse(response);
                _token = (string)parsed["token"];

                if (!string.IsNullOrEmpty(_token))
                {
                    _log.Info("Tersambung ke ForgeHub sebagai " + _settings.Username + ".");
                    SetConnected(true);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _log.Warning("ForgeHub: gagal masuk — " + ex.Message);
            }

            SetConnected(false);
            return false;
        }

        private bool Post(string path, string json)
        {
            return Send("POST", path, json, true) != null;
        }

        private string Send(string method, string path, string json, bool withToken)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(_settings.ForgeHubUrl.TrimEnd('/') + path);
                request.Method = method;
                request.ContentType = "application/json";
                request.Timeout = 8000;
                request.ReadWriteTimeout = 8000;

                if (withToken && !string.IsNullOrEmpty(_token))
                    request.Headers["Authorization"] = "Bearer " + _token;

                // GET tidak boleh punya badan permintaan. Memanggil
                // GetRequestStream() untuk GET melempar ProtocolViolationException
                // sebelum permintaannya sempat dikirim sama sekali.
                if (json != null)
                {
                    var payload = Encoding.UTF8.GetBytes(json);
                    request.ContentLength = payload.Length;

                    using (var stream = request.GetRequestStream())
                        stream.Write(payload, 0, payload.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                // 401 berarti tokennya sudah tidak berlaku: dibuang supaya
                // percobaan berikutnya masuk lagi, bukan mengulang token mati.
                var http = ex.Response as HttpWebResponse;
                if (http != null && http.StatusCode == HttpStatusCode.Unauthorized) _token = null;

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void SetConnected(bool value)
        {
            if (_connected == value) return;

            _connected = value;

            var handler = StatusChanged;
            if (handler == null) return;

            try
            {
                if (System.Windows.Application.Current != null)
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(handler);
                else
                    handler();
            }
            catch (Exception)
            {
            }
        }

        private void Notify()
        {
            var handler = StatusChanged;
            if (handler != null) handler();
        }
    }
}

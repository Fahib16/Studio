using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Custom.StudioBridge
{
    /// <summary>
    /// Helper komunikasi ke native host lewat Named Pipe "StudioNativeHostPipe"
    /// -- protokol sama persis dengan TestClient.ps1 (satu baris JSON + newline).
    ///
    /// BATCH 4: timeout sekarang dikirim juga ke native host (field "timeoutMs")
    /// supaya kedua sisi sepakat berapa lama menunggu. Sebelumnya native host
    /// selalu pakai 10 detik mati, yang tidak cukup untuk "indicate" (menunggu
    /// user mengklik elemen di layar).
    /// </summary>
    public static class StudioPipeClient
    {
        private const string PipeName = "StudioNativeHostPipe";

        /// <summary>
        /// Margin tambahan di sisi klien: klien harus menunggu SEDIKIT LEBIH LAMA
        /// dari native host, kalau tidak klien menyerah duluan sebelum pesan
        /// timeout dari host sempat sampai -- error-nya jadi membingungkan
        /// ("koneksi ditutup tanpa balasan") padahal penyebab sebenarnya timeout.
        /// </summary>
        private const int ClientTimeoutMarginMs = 3000;

        /// <summary>
        /// Margin native host DI ATAS timeout operasinya.
        ///
        /// Tiga lapisan menunggu peristiwa yang sama, dan urutannya HARUS
        /// menaik: extension menunggu elemen, native host menunggu extension,
        /// klien menunggu native host. Kalau dua lapisan kedaluwarsa pada
        /// detik yang sama, yang sampai ke user adalah pesan lapisan LUAR
        /// ("browser mungkin tidak aktif") -- padahal sebabnya ada di dalam
        /// (elemennya tidak muncul).
        ///
        /// Persis itu yang terjadi begitu extension mulai benar-benar
        /// menunggu elemen: kolom "timeoutMs" dipakai untuk dua arti
        /// sekaligus, sehingga host selalu menyerah bersamaan dengan
        /// extension dan menutupi sebab sebenarnya.
        /// </summary>
        private const int HostTimeoutMarginMs = 5000;

        public static JToken SendCommand(
            string action,
            JObject extraParams = null,
            int connectTimeoutMs = 5000,
            int responseTimeoutMs = 10000)
        {
            var request = new JObject
            {
                ["action"] = action,
                ["timeoutMs"] = responseTimeoutMs
            };

            if (extraParams != null)
            {
                foreach (var prop in extraParams.Properties())
                    request[prop.Name] = prop.Value;
            }

            // "timeoutMs" adalah anggaran OPERASI di sisi extension: berapa
            // lama menunggu elemen muncul atau halaman selesai dimuat.
            // Activity mengisinya lewat extraParams, jadi nilainya sering
            // BERBEDA dari responseTimeoutMs -- dan sebelumnya nilai itulah
            // yang tanpa sengaja ikut menjadi anggaran native host.
            //
            // Anggaran host sekarang dikirim di kolomnya sendiri, dan selalu
            // lebih besar daripada operasi terlama yang mungkin dijalankan
            // extension, supaya sebab kegagalan yang sebenarnya tidak lagi
            // tertutup oleh timeout lapisan luar.
            var operationTimeoutMs = request["timeoutMs"]?.Value<int>() ?? responseTimeoutMs;
            var hostTimeoutMs = Math.Max(responseTimeoutMs, operationTimeoutMs) + HostTimeoutMarginMs;
            request["hostTimeoutMs"] = hostTimeoutMs;

            using (var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut))
            {
                ConnectWithRetry(pipe, connectTimeoutMs);

                var requestBytes = Encoding.UTF8.GetBytes(request.ToString(Newtonsoft.Json.Formatting.None) + "\n");
                pipe.Write(requestBytes, 0, requestBytes.Length);
                pipe.Flush();

                var responseLine = ReadLineWithTimeout(pipe, hostTimeoutMs + ClientTimeoutMarginMs);

                if (string.IsNullOrEmpty(responseLine))
                    throw new InvalidOperationException("Studio Bridge: native host menutup koneksi tanpa balasan.");

                var response = JObject.Parse(responseLine);
                var success = response["success"]?.Value<bool>() ?? false;

                if (!success)
                {
                    var errorMsg = response["error"]?.Value<string>() ?? "Unknown error dari native host.";
                    throw new InvalidOperationException($"Studio Bridge: {errorMsg}");
                }

                return response["data"];
            }
        }

        /// <summary>
        /// Dipakai oleh IndicateHelper.Run() (alur dua tahap: pilih tab dulu
        /// lewat dialog, baru masuk mode indicate -- tab-nya sudah ditentukan
        /// SEBELUM method ini dipanggil, beda dari Indicate() di bawah yang
        /// membiarkan native host pakai tab aktif).
        ///
        /// Mengembalikan JObject mentah (selector/tagName/text/
        /// screenshotBase64 -- screenshotBase64 sudah di-crop di sisi
        /// extension lewat OffscreenCanvas, lihat background.js), atau NULL
        /// kalau user membatalkan (Esc) -- itu BUKAN error, jadi tidak
        /// dilempar sebagai exception ke pemanggil (background.js mengirim
        /// pesan error yang mengandung kata "Dibatalkan" saat Esc ditekan,
        /// itu yang dijadikan penanda untuk dikonversi jadi null di sini).
        /// </summary>
        public static JObject StartIndicate(int tabId, int timeoutMs = 60000)
        {
            var extra = new JObject();
            if (tabId != 0) extra["tabId"] = tabId;

            try
            {
                var data = SendCommand("indicate", extra, connectTimeoutMs: 5000, responseTimeoutMs: timeoutMs);
                return data as JObject;
            }
            catch (InvalidOperationException ex) when (
                ex.Message.IndexOf("Dibatalkan", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return null;
            }
        }

        /// <summary>
        /// Minta user memilih elemen langsung di layar. Screenshot elemen
        /// SUDAH DI-CROP DI SISI EXTENSION (OffscreenCanvas di background.js)
        /// sebelum sampai ke sini -- C# tinggal menyimpan base64-nya, tanpa
        /// perlu System.Drawing atau logic crop apa pun.
        ///
        /// Timeout default 60 detik -- user butuh waktu untuk pindah ke browser,
        /// cari elemennya, lalu mengklik.
        /// </summary>
        public static IndicateResult Indicate(int tabId = 0, int timeoutMs = 60000)
        {
            var extra = new JObject();
            if (tabId != 0) extra["tabId"] = tabId;

            var data = SendCommand("indicate", extra, connectTimeoutMs: 5000, responseTimeoutMs: timeoutMs);

            return new IndicateResult
            {
                Selector = data?["selector"]?.Value<string>(),
                Unique = data?["unique"]?.Value<bool>() ?? false,
                TagName = data?["tagName"]?.Value<string>(),
                Text = data?["text"]?.Value<string>(),
                TabId = data?["tabId"]?.Value<int>() ?? 0,
                ScreenshotBase64 = data?["screenshotBase64"]?.Value<string>()
            };
        }

        /// <summary>
        /// Ambil pohon DOM halaman untuk panel Visual Tree UI Explorer.
        /// currentSelector opsional: kalau diisi, balasan memuat "path"
        /// (deretan indeks anak dari root) supaya UI Explorer bisa langsung
        /// membuka dan menyorot elemen yang sedang dipakai activity.
        ///
        /// Timeout lebih longgar dari aksi lain: menyusun pohon halaman besar
        /// bisa makan beberapa detik, dan payload-nya pun jauh lebih besar.
        /// </summary>
        public static JObject GetDomTree(int tabId, string currentSelector = null, int timeoutMs = 30000)
        {
            var extra = new JObject();
            if (tabId != 0) extra["tabId"] = tabId;
            if (!string.IsNullOrEmpty(currentSelector)) extra["selector"] = currentSelector;

            return SendCommand("getDomTree", extra, responseTimeoutMs: timeoutMs) as JObject;
        }

        /// <summary>
        /// Ambil RANTAI LELUHUR sebuah elemen (dari body sampai elemen itu),
        /// lengkap dengan atribut tiap tingkat. Inilah bahan Selector Editor
        /// berjenjang — sepadan dengan daftar baris &lt;webctrl ... /&gt; di
        /// UI Explorer UiPath.
        /// </summary>
        public static JObject GetElementChain(int tabId, string selector, int timeoutMs = 15000)
        {
            var extra = new JObject { ["selector"] = selector };
            if (tabId != 0) extra["tabId"] = tabId;

            return SendCommand("getElementChain", extra, responseTimeoutMs: timeoutMs) as JObject;
        }

        /// <summary>
        /// Hitung berapa elemen yang cocok dengan selector.
        ///
        /// Selector yang sintaksnya salah TIDAK melempar exception — dikembalikan
        /// sebagai valid=false berikut pesannya, karena di UI Explorer user
        /// memang sering mengetik selector setengah jadi.
        /// </summary>
        public static JObject ValidateSelector(int tabId, string selector, int timeoutMs = 10000)
        {
            var extra = new JObject { ["selector"] = selector ?? "" };
            if (tabId != 0) extra["tabId"] = tabId;

            return SendCommand("validateSelector", extra, responseTimeoutMs: timeoutMs) as JObject;
        }

        /// <summary>
        /// Sorot elemen untuk UI Explorer: tab dibawa ke depan, digulir ke
        /// tengah, disorot biru terang.
        ///
        /// Memakai aksi TERSENDIRI ("explorerHighlight"), bukan aksi
        /// "highlight" yang dipakai activity StudioHighlight saat workflow
        /// berjalan. Dengan begitu penyesuaian tampilan di sini tidak pernah
        /// mengubah perilaku otomasi yang sudah jalan — activity runtime
        /// tetap memanggil SendCommand("highlight", ...) langsung dan tidak
        /// merebut layar.
        /// </summary>
        public static void ExplorerHighlight(int tabId, string selector, int timeoutMs = 10000)
        {
            var extra = new JObject { ["selector"] = selector };
            if (tabId != 0) extra["tabId"] = tabId;

            SendCommand("explorerHighlight", extra, responseTimeoutMs: timeoutMs);
        }

        /// <summary>
        /// Batalkan picker web yang sedang berjalan. Dipakai auto-detect hover
        /// saat kursor berpindah dari browser ke aplikasi desktop.
        ///
        /// Kegagalan SENGAJA ditelan: kalau tab sudah tertutup atau tidak ada
        /// picker yang berjalan, tidak ada yang perlu dibatalkan — melempar
        /// error di sini hanya akan mengganggu alur indicate yang sedang jalan.
        /// </summary>
        public static void CancelIndicate(int tabId)
        {
            try
            {
                var extra = new JObject();
                if (tabId != 0) extra["tabId"] = tabId;
                SendCommand("cancelIndicate", extra, responseTimeoutMs: 5000);
            }
            catch (Exception) { }
        }


        /// <summary>
        /// Sambung ke pipe, dan TERUS COBA selama jatah waktunya belum habis.
        ///
        /// `NamedPipeClientStream.Connect` hanya sabar untuk satu keadaan:
        /// pipe-nya belum ada sama sekali (ERROR_FILE_NOT_FOUND) — itu ditunggu
        /// sampai batas waktu. Keadaan kedua, pipe-nya ADA tapi semua instans
        /// sedang terpakai, langsung dilempar sebagai IOException berbunyi
        /// "The semaphore timeout period has expired." Pesan itu yang selama
        /// ini sampai ke user saat menekan Indicate, dan sama sekali tidak
        /// menjelaskan apa pun.
        ///
        /// Keadaan kedua itu sebenarnya SEMENTARA: begitu perintah yang sedang
        /// berjalan selesai, instansnya bebas lagi. Jadi di sini keduanya
        /// diperlakukan sama — dicoba lagi sampai jatah waktu habis, baru
        /// menyerah dengan pesan yang bisa dimengerti.
        /// </summary>
        private static void ConnectWithRetry(NamedPipeClientStream pipe, int timeoutMs)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            while (true)
            {
                try
                {
                    pipe.Connect(200);
                    return;
                }
                catch (TimeoutException) { }
                catch (IOException ex) when (IsPipeBusy(ex)) { }

                if (sw.ElapsedMilliseconds >= timeoutMs)
                    throw new InvalidOperationException(
                        "Studio Bridge: tidak bisa terhubung ke native host dalam " +
                        (timeoutMs / 1000.0).ToString("0.#") + " detik. Pastikan browser (Chrome/Edge) " +
                        "sedang terbuka dengan extension Studio Automation Bridge aktif, dan tidak ada " +
                        "proses Indicate lain yang masih menunggu.");

                System.Threading.Thread.Sleep(50);
            }
        }

        /// <summary>
        /// ERROR_SEM_TIMEOUT (121) dan ERROR_PIPE_BUSY (231): pipe-nya ada,
        /// tapi belum ada instans yang bisa dipakai. Bukan kegagalan permanen.
        /// </summary>
        private static bool IsPipeBusy(IOException ex)
        {
            return ex.HResult == unchecked((int)0x80070079)
                || ex.HResult == unchecked((int)0x800700E7);
        }
        /// <summary>
        /// Baca satu baris balasan dari pipe.
        ///
        /// Dibaca per BLOK 8KB, bukan per byte. Versi lama membaca satu byte
        /// per satu operasi async — waktu balasan masih ~200 byte itu tidak
        /// terasa, tapi sejak screenshot ikut dikirim (puluhan ribu byte)
        /// biayanya jadi puluhan ribu operasi async berurutan, dan itulah
        /// penyebab utama Indicate terasa lambat.
        ///
        /// Byte-nya dikumpulkan dulu di MemoryStream lalu di-decode SEKALI di
        /// akhir. Ini juga memperbaiki bug lama yang tidak kentara: versi
        /// sebelumnya menyusun string dengan (char)byte, yang merusak karakter
        /// non-ASCII (mis. teks elemen berisi é atau —) karena UTF-8 multi-byte
        /// diperlakukan sebagai satu karakter per byte.
        /// </summary>
        private static string ReadLineWithTimeout(NamedPipeClientStream pipe, int timeoutMs)
        {
            var buffer = new byte[8192];

            using (var ms = new MemoryStream())
            using (var cts = new System.Threading.CancellationTokenSource(timeoutMs))
            {
                try
                {
                    while (true)
                    {
                        var readTask = pipe.ReadAsync(buffer, 0, buffer.Length, cts.Token);
                        int bytesRead = readTask.GetAwaiter().GetResult();
                        if (bytesRead == 0) break;

                        int newline = Array.IndexOf(buffer, (byte)'\n', 0, bytesRead);
                        if (newline >= 0)
                        {
                            ms.Write(buffer, 0, newline);
                            break;
                        }

                        ms.Write(buffer, 0, bytesRead);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException("Studio Bridge: timeout menunggu balasan dari native host.");
                }

                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }
    }

    /// <summary>
    /// Hasil dari proses indicate.
    /// </summary>
    public class IndicateResult
    {
        public string Selector { get; set; }

        /// <summary>
        /// False berarti selector yang dihasilkan cocok ke LEBIH DARI SATU
        /// elemen -- masih bisa dipakai (yang pertama yang kena), tapi rawan
        /// salah sasaran kalau halamannya berubah.
        /// </summary>
        public bool Unique { get; set; }

        public string TagName { get; set; }
        public string Text { get; set; }
        public int TabId { get; set; }

        /// <summary>
        /// Base64 PNG elemen, SUDAH DI-CROP di extension. Null kalau capture
        /// gagal (halaman chrome://, dsb.) atau extension tidak punya izin --
        /// itu tidak fatal, Indicate tetap sukses tanpa screenshot.
        /// </summary>
        public string ScreenshotBase64 { get; set; }
    }
}

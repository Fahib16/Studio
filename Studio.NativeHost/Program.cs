using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace Studio.NativeHost
{
    /// <summary>
    /// Native Messaging Host untuk extension "Studio Automation Bridge" -- BATCH 2.
    ///
    /// Proses ini dijalankan OTOMATIS oleh Chrome/Edge (lewat connectNative
    /// di sisi extension), stdin/stdout-nya sudah tersambung ke browser sejak
    /// proses ini mulai. Di atas itu, kita TAMBAH Named Pipe server supaya
    /// proses LAIN (Studio, atau untuk testing: TestClient.ps1) bisa kirim
    /// command ke sini, yang lalu kita teruskan ke extension via stdout,
    /// tunggu balasannya lewat stdin, lalu kirim balik hasilnya ke pipe.
    ///
    /// Named Pipe DIPILIH (bukan TCP) karena ini komunikasi lokal
    /// antar-proses di Windows yang sama -- lebih ringan dan tidak perlu
    /// alokasi port TCP yang bisa bentrok.
    ///
    /// PENTING: bridge ini cuma BISA jalan kalau ada browser yang sedang
    /// terbuka DENGAN extension aktif (karena proses ini sendiri hidupnya
    /// bergantung pada koneksi connectNative dari extension). Kalau tidak
    /// ada browser yang connect, proses ini tidak akan ada sama sekali untuk
    /// menerima koneksi pipe.
    /// </summary>
    public class Program
    {
        private const string PipeName = "StudioNativeHostPipe";
        private const int DefaultResponseTimeoutSeconds = 10;

        private static Stream _browserStdin;
        private static Stream _browserStdout;
        private static readonly object _writeLock = new object();

        // Korelasi request/response lewat "id" -- browser bisa balas kapan
        // saja, kita cocokkan ke request pipe yang sedang menunggu.
        private static readonly Dictionary<string, JObject> _pendingResponses = new Dictionary<string, JObject>();
        private static readonly Dictionary<string, ManualResetEventSlim> _pendingWaits = new Dictionary<string, ManualResetEventSlim>();
        private static readonly object _pendingLock = new object();

        public static void Main(string[] args)
        {
            _browserStdin = Console.OpenStandardInput();
            _browserStdout = Console.OpenStandardOutput();

            // Thread terpisah: terus baca pesan MASUK dari browser (balasan
            // atas command yang kita kirim).
            var browserReaderThread = new Thread(BrowserReadLoop) { IsBackground = true };
            browserReaderThread.Start();

            // Main thread: layani Named Pipe, terima command dari Studio/test
            // client, teruskan ke browser, tunggu balasan, kirim balik.
            RunPipeServerLoop();
        }

        private static void BrowserReadLoop()
        {
            try
            {
                while (true)
                {
                    var message = ReadNativeMessage(_browserStdin);
                    if (message == null)
                    {
                        // Browser/extension tutup koneksi (stdin EOF) -- proses
                        // ini SELESAI TUGASNYA, keluar total. Tanpa ini, proses
                        // bisa jadi "zombie" yang terus jalan (masih nge-lock
                        // file .exe-nya sendiri, bikin rebuild gagal diam-diam
                        // di Visual Studio tanpa pesan error yang jelas).
                        Environment.Exit(0);
                        return;
                    }

                    var id = (string)message["id"];
                    if (string.IsNullOrEmpty(id)) continue; // pesan tanpa id, abaikan (bukan balasan yang kita tunggu)

                    lock (_pendingLock)
                    {
                        _pendingResponses[id] = message;
                        if (_pendingWaits.TryGetValue(id, out var evt))
                            evt.Set();
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("BrowserReadLoop", ex);
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Jumlah pelayan pipe yang jalan bersamaan.
        ///
        /// DULU cuma SATU, dan itu sumber bug "The semaphore timeout period
        /// has expired" yang muncul saat Indicate. Alurnya begini: perintah
        /// "indicate" MENAHAN sambungan sampai user mengklik elemen — bisa
        /// semenit penuh. Selama itu satu-satunya instans pipe terpakai, jadi
        /// perintah kedua tidak kebagian instans sama sekali. Yang paling
        /// sering kena justru "cancelIndicate", yang dikirim persis SELAGI
        /// indicate masih menunggu — jadi pembatalannya tidak pernah sampai,
        /// indicate lama tetap menggantung, dan perintah berikutnya
        /// (mis. "listTabs") ikut kehabisan waktu menunggu instans.
        ///
        /// Windows mengembalikan ERROR_SEM_TIMEOUT untuk keadaan itu, dan
        /// .NET menerjemahkannya jadi IOException apa adanya — makanya pesan
        /// yang sampai ke user berbunyi soal semaphore, sesuatu yang sama
        /// sekali tidak menjelaskan apa yang terjadi.
        /// </summary>
        private const int PipeInstances = 4;

        /// <summary>0 = belum pernah; dipakai supaya kegagalan "semua instans
        /// terpakai" cukup dicatat SEKALI per proses. Sebelum ini, proses host
        /// kedua mencatatnya tiap 500 ms tanpa henti — berkas error.log sampai
        /// 4 MB berisi 4000-an baris yang sama.</summary>
        private static int _busyLogged;

        private static void RunPipeServerLoop()
        {
            // Pelayan tambahan di thread sendiri; satu dijalankan di thread ini
            // supaya Main tidak keburu selesai.
            for (int i = 1; i < PipeInstances; i++)
            {
                var worker = new Thread(PipeWorkerLoop) { IsBackground = true };
                worker.Start();
            }

            PipeWorkerLoop();
        }

        private static void PipeWorkerLoop()
        {
            while (true)
            {
                try
                {
                    using (var pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, PipeInstances))
                    {
                        pipe.WaitForConnection();
                        Interlocked.Exchange(ref _busyLogged, 0);
                        ServePipeRequest(pipe);
                    }
                }
                catch (Exception ex) when (IsPipeOwnedByAnotherProcess(ex))
                {
                    // Proses host LAIN sudah memegang pipe ini (browser kedua,
                    // profil kedua, atau host lama yang belum sempat keluar).
                    // Proses ini jadi cadangan: diam saja, dan baru mengambil
                    // alih kalau pemegangnya berhenti. Jedanya sengaja panjang
                    // supaya tidak jadi loop sibuk.
                    if (Interlocked.Exchange(ref _busyLogged, 1) == 0)
                        LogError("PipeWorkerLoop (host lain memegang pipe, proses ini jadi cadangan)", ex);
                    Thread.Sleep(5000);
                }
                catch (Exception ex)
                {
                    LogError("PipeWorkerLoop", ex);
                    Thread.Sleep(500); // hindari busy-loop kalau ada error berulang
                }
            }
        }

        /// <summary>
        /// Dua kegagalan yang artinya sama: pipe ini sudah dipegang proses host
        /// LAIN, jadi proses ini tidak boleh ikut melayani.
        ///
        ///   ERROR_PIPE_BUSY (231)    jatah instansnya sudah habis.
        ///   ERROR_ACCESS_DENIED (5)  pipe-nya dibuat dengan jumlah instans yang
        ///                            berbeda — ini yang terjadi kalau host versi
        ///                            lama (yang cuma punya SATU instans) masih
        ///                            hidup saat host versi baru mulai. Windows
        ///                            menuntut semua instans sepakat soal angka
        ///                            itu.
        /// </summary>
        private static bool IsPipeOwnedByAnotherProcess(Exception ex)
        {
            if (ex is UnauthorizedAccessException) return true;

            var io = ex as IOException;
            return io != null
                && (io.HResult == unchecked((int)0x800700E7)
                 || io.HResult == unchecked((int)0x80070005));
        }

        /// <summary>
        /// Melayani SATU sambungan: baca permintaan, teruskan ke extension,
        /// tunggu balasan, kirim balik. Isinya sama persis dengan versi lama —
        /// yang berubah hanya siapa yang memanggilnya, dan sekarang beberapa
        /// sambungan bisa dilayani berbarengan.
        /// </summary>
        private static void ServePipeRequest(NamedPipeServerStream pipe)
        {
            var request = ReadPipeMessage(pipe);
            if (request == null) return;

            var id = Guid.NewGuid().ToString("N");
            request["id"] = id;

            // "hostTimeoutMs" adalah anggaran KITA -- berapa lama menunggu
            // balasan extension. "timeoutMs" adalah anggaran operasi DI DALAM
            // extension (menunggu elemen muncul, menunggu halaman dimuat), dan
            // biasanya lebih kecil.
            //
            // Dulu satu kolom dipakai untuk kedua arti itu. Selama extension
            // selalu menjawab seketika, itu tidak kelihatan; begitu extension
            // benar-benar menunggu elemen, kita dan extension kedaluwarsa pada
            // detik yang sama dan yang terbaca user adalah "browser mungkin
            // tidak aktif" -- bukan sebab sebenarnya.
            //
            // Kolom lama tetap dibaca sebagai cadangan supaya klien versi lama
            // masih dilayani seperti biasa.
            var timeoutMs = request["hostTimeoutMs"]?.Value<int>()
                         ?? request["timeoutMs"]?.Value<int>()
                         ?? (DefaultResponseTimeoutSeconds * 1000);

            var waitEvent = new ManualResetEventSlim(false);
            lock (_pendingLock) { _pendingWaits[id] = waitEvent; }

            // Teruskan request ke extension lewat native messaging.
            WriteNativeMessage(_browserStdout, request);

            bool got = waitEvent.Wait(TimeSpan.FromMilliseconds(timeoutMs));

            JObject response;
            lock (_pendingLock)
            {
                _pendingWaits.Remove(id);
                if (got && _pendingResponses.TryGetValue(id, out response))
                {
                    _pendingResponses.Remove(id);
                }
                else
                {
                    response = new JObject
                    {
                        ["id"] = id,
                        ["success"] = false,
                        ["error"] = "Timeout menunggu balasan dari extension (browser mungkin tidak aktif)."
                    };
                }
            }

            WritePipeMessage(pipe, response);
        }

        // ===================== Protokol Native Messaging (Batch 1, tidak berubah) =====================

        private static JObject ReadNativeMessage(Stream stdin)
        {
            var lengthBytes = new byte[4];
            int totalRead = 0;
            while (totalRead < 4)
            {
                int read = stdin.Read(lengthBytes, totalRead, 4 - totalRead);
                if (read == 0) return null;
                totalRead += read;
            }

            int length = BitConverter.ToInt32(lengthBytes, 0);
            if (length <= 0 || length > 1024 * 1024 * 10)
                throw new InvalidDataException($"Panjang pesan tidak wajar: {length} byte.");

            var messageBytes = new byte[length];
            totalRead = 0;
            while (totalRead < length)
            {
                int read = stdin.Read(messageBytes, totalRead, length - totalRead);
                if (read == 0) throw new EndOfStreamException("stdin tertutup di tengah pesan.");
                totalRead += read;
            }

            return JObject.Parse(Encoding.UTF8.GetString(messageBytes));
        }

        private static void WriteNativeMessage(Stream stdout, JObject message)
        {
            var jsonBytes = Encoding.UTF8.GetBytes(message.ToString(Newtonsoft.Json.Formatting.None));
            var lengthBytes = BitConverter.GetBytes(jsonBytes.Length);

            lock (_writeLock)
            {
                stdout.Write(lengthBytes, 0, 4);
                stdout.Write(jsonBytes, 0, jsonBytes.Length);
                stdout.Flush();
            }
        }

        // ===================== Protokol Named Pipe (BARU, internal kita sendiri) =====================
        // Lebih sederhana: satu baris JSON diakhiri newline. Ini komunikasi
        // ANTAR PROSES KITA SENDIRI (Studio <-> native host), jadi bebas
        // pilih format -- tidak terikat spesifikasi Chrome seperti native
        // messaging di atas.

        private static JObject ReadPipeMessage(NamedPipeServerStream pipe)
        {
            var sb = new StringBuilder();
            int b;
            while ((b = pipe.ReadByte()) != -1)
            {
                if (b == '\n') break;
                sb.Append((char)b);
            }
            var line = sb.ToString();
            return string.IsNullOrEmpty(line) ? null : JObject.Parse(line);
        }

        private static void WritePipeMessage(NamedPipeServerStream pipe, JObject message)
        {
            var bytes = Encoding.UTF8.GetBytes(message.ToString(Newtonsoft.Json.Formatting.None) + "\n");
            pipe.Write(bytes, 0, bytes.Length);
            pipe.Flush();
        }

        private static void LogError(string context, Exception ex)
        {
            try
            {
                var logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Studio", "NativeHost", "error.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                File.AppendAllText(logPath, $"[{DateTime.Now:O}] [{context}] {ex}\n\n");
            }
            catch
            {
                // Kalau logging sendiri gagal, tidak ada lagi yang bisa dilakukan.
            }
        }
    }
}

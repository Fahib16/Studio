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
        private const int ResponseTimeoutSeconds = 10;

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

        private static void RunPipeServerLoop()
        {
            while (true)
            {
                try
                {
                    using (var pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1))
                    {
                        pipe.WaitForConnection();

                        var request = ReadPipeMessage(pipe);
                        if (request == null) continue;

                        var id = Guid.NewGuid().ToString("N");
                        request["id"] = id;

                        var waitEvent = new ManualResetEventSlim(false);
                        lock (_pendingLock) { _pendingWaits[id] = waitEvent; }

                        // Teruskan request ke extension lewat native messaging.
                        WriteNativeMessage(_browserStdout, request);

                        bool got = waitEvent.Wait(TimeSpan.FromSeconds(ResponseTimeoutSeconds));

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
                }
                catch (Exception ex)
                {
                    LogError("RunPipeServerLoop", ex);
                    Thread.Sleep(500); // hindari busy-loop kalau ada error berulang
                }
            }
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

using System;
using System.IO.Pipes;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Custom.StudioBridge
{
    /// <summary>
    /// Helper komunikasi ke native host lewat Named Pipe "StudioNativeHostPipe"
    /// -- protokol SAMA PERSIS dengan TestClient.ps1 (satu baris JSON + newline,
    /// dua arah). Dipakai bareng oleh semua activity di project ini supaya
    /// logic koneksi/error-handling tidak diduplikasi di tiap activity.
    /// </summary>
    public static class StudioPipeClient
    {
        private const string PipeName = "StudioNativeHostPipe";

        /// <summary>
        /// Kirim satu command ke native host, tunggu balasannya, return data-nya.
        /// Throw exception kalau: gagal connect (native host/browser tidak aktif),
        /// timeout, atau native host balas success=false.
        /// </summary>
        public static JToken SendCommand(string action, JObject extraParams = null, int connectTimeoutMs = 5000, int responseTimeoutMs = 10000)
        {
            var request = new JObject { ["action"] = action };
            if (extraParams != null)
            {
                foreach (var prop in extraParams.Properties())
                    request[prop.Name] = prop.Value;
            }

            using (var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut))
            {
                try
                {
                    pipe.Connect(connectTimeoutMs);
                }
                catch (TimeoutException)
                {
                    throw new InvalidOperationException(
                        "Studio Bridge: tidak bisa terhubung ke native host. Pastikan browser (Chrome/Edge) " +
                        "sedang terbuka dengan extension Studio Automation Bridge aktif.");
                }

                var requestLine = request.ToString(Newtonsoft.Json.Formatting.None) + "\n";
                var requestBytes = Encoding.UTF8.GetBytes(requestLine);
                pipe.Write(requestBytes, 0, requestBytes.Length);
                pipe.Flush();

                var responseLine = ReadLineWithTimeout(pipe, responseTimeoutMs);

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
        /// Baca satu baris (sampai newline) dari pipe, dengan timeout yang
        /// BENERAN efektif -- pakai ReadAsync + Task.Wait(timeout), bukan
        /// ReadByte() blocking biasa (yang tidak bisa dibatasi waktu tunggunya
        /// dengan reliable di semua kondisi NamedPipeClientStream).
        /// </summary>
        private static string ReadLineWithTimeout(NamedPipeClientStream pipe, int timeoutMs)
        {
            var sb = new StringBuilder();
            var buffer = new byte[1];

            using (var cts = new System.Threading.CancellationTokenSource(timeoutMs))
            {
                try
                {
                    while (true)
                    {
                        var readTask = pipe.ReadAsync(buffer, 0, 1, cts.Token);
                        int bytesRead = readTask.GetAwaiter().GetResult();
                        if (bytesRead == 0) break; // pipe ditutup

                        if (buffer[0] == (byte)'\n') break;
                        sb.Append((char)buffer[0]);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException("Studio Bridge: timeout menunggu balasan dari native host.");
                }
            }

            return sb.ToString();
        }
    }
}

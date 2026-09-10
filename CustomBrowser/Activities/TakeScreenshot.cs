using System;
using System.Activities;
using System.ComponentModel;
using System.IO;
using Custom.StudioBridge;
using Custom.StudioBridge.Design;
using Newtonsoft.Json.Linq;

namespace Custom.Browser
{
    /// <summary>
    /// Memotret halaman web atau satu elemen.
    ///
    /// Selector KOSONG berarti seluruh bagian halaman yang TERLIHAT di layar —
    /// bukan seluruh halaman termasuk yang harus digulir. Chrome hanya
    /// menyediakan potret area yang terlihat (captureVisibleTab); menjahit
    /// beberapa potret sambil menggulir menghasilkan gambar yang sering salah
    /// pada halaman dengan elemen melayang, jadi tidak dilakukan.
    ///
    /// Selector desktop dipotret dari layar seukuran elemennya.
    ///
    /// Hasilnya selalu PNG. Path opsional: kalau diisi, gambar juga ditulis ke
    /// berkas; Image Base64 tetap terisi supaya bisa dipakai activity lain
    /// tanpa membaca berkasnya lagi.
    ///
    /// Tidak mewarisi ElementActivityBase karena di sini Selector justru
    /// OPSIONAL, sedangkan di kelas dasar itu wajib.
    /// </summary>
    [Designer(typeof(Design.TakeScreenshotDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Take Screenshot")]
    [Description("Memotret halaman web atau satu elemen web/desktop.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.takescreenshot.png")]
    public sealed class TakeScreenshot : CodeActivity
    {
        public TakeScreenshot()
        {
            DisplayName = "Take Screenshot";
        }

        [Category("Target")]
        [DisplayName("Selector")]
        [Description("Kosong berarti seluruh halaman yang terlihat. Isi untuk memotret satu elemen " +
                     "(web maupun desktop).")]
        public InArgument<string> Selector { get; set; }

        [Category("Target")]
        [DisplayName("TabId")]
        [Description("Opsional, hanya untuk target web. Kosongkan untuk memakai tab yang sedang aktif.")]
        public InArgument<int?> TabId { get; set; }

        [Category("Input")]
        [DisplayName("Path")]
        [Description("Opsional. Kalau diisi, gambar disimpan ke berkas ini (PNG). " +
                     "Foldernya dibuat otomatis kalau belum ada.")]
        public InArgument<string> Path { get; set; }

        [Category("Options")]
        [DisplayName("Timeout")]
        [Description("Berapa lama menunggu elemen sebelum menyerah (default 10 detik kalau kosong).")]
        public InArgument<TimeSpan> Timeout { get; set; }

        [Category("Output")]
        [DisplayName("Image Base64")]
        [Description("Gambar PNG dalam bentuk base64.")]
        public OutArgument<string> ImageBase64 { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [Description("Kalau true, kegagalan activity ini tidak menghentikan workflow (default false).")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        [Browsable(false)]
        public string ScreenshotBase64 { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = ContinueOnError != null && ContinueOnError.Get(context);

            try
            {
                var selector = Selector != null ? Selector.Get(context) : null;

                var timeout = Timeout != null ? Timeout.Get(context) : TimeSpan.Zero;
                if (timeout <= TimeSpan.Zero) timeout = TimeSpan.FromSeconds(10);
                var timeoutMs = (int)timeout.TotalMilliseconds;

                string base64;

                if (!string.IsNullOrWhiteSpace(selector) &&
                    SelectorKindDetector.Detect(selector) == SelectorKind.Desktop)
                {
                    base64 = DesktopActions.Screenshot(selector, timeout);
                }
                else
                {
                    var request = new JObject { ["timeoutMs"] = timeoutMs };
                    if (!string.IsNullOrWhiteSpace(selector)) request["selector"] = selector;

                    var tabId = TabId != null ? TabId.Get(context) : null;
                    if (tabId.HasValue) request["tabId"] = tabId.Value;

                    var result = StudioPipeClient.SendCommand(
                        "screenshot", request, responseTimeoutMs: timeoutMs + 15000);

                    base64 = result?["imageBase64"]?.Value<string>();
                }

                if (string.IsNullOrEmpty(base64))
                    throw new InvalidOperationException("Screenshot kosong: tidak ada gambar yang diterima.");

                var path = Path != null ? Path.Get(context) : null;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    var folder = System.IO.Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    File.WriteAllBytes(path, Convert.FromBase64String(base64));
                }

                if (ImageBase64 != null) ImageBase64.Set(context, base64);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

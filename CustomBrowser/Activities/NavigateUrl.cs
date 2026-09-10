using System;
using System.Activities;
using System.ComponentModel;
using Custom.StudioBridge;
using Newtonsoft.Json.Linq;

namespace Custom.Browser
{
    /// <summary>
    /// Membuka alamat lain di tab yang sudah ada.
    ///
    /// Berbeda dari Open Browser yang membuka jendela/tab BARU: activity ini
    /// memakai tab yang sedang aktif (atau TabId tertentu), seperti mengetik
    /// alamat di bilah alamat.
    ///
    /// Wait For Load (default true) menunggu halaman selesai dimuat sebelum
    /// melanjutkan. Tanpa itu, activity berikutnya sering berjalan di atas
    /// halaman LAMA — kegagalan yang paling sering terjadi pada otomasi web
    /// dan paling sulit dilacak karena hanya kadang-kadang terjadi, tergantung
    /// kecepatan jaringan.
    /// </summary>
    [Designer(typeof(Design.NavigateUrlDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Navigate URL")]
    [Description("Membuka alamat lain di tab yang sudah ada.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.navigateurl.png")]
    public sealed class NavigateUrl : CodeActivity
    {
        public NavigateUrl()
        {
            DisplayName = "Navigate URL";
            WaitForLoad = new InArgument<bool>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<bool>("True"));
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Url")]
        [Description("Alamat yang dituju, mis. https://contoh.id/laporan")]
        public InArgument<string> Url { get; set; }

        [Category("Input")]
        [DisplayName("TabId")]
        [Description("Opsional. Kosongkan untuk memakai tab yang sedang aktif.")]
        public InArgument<int?> TabId { get; set; }

        [Category("Options")]
        [DisplayName("Wait For Load")]
        [Description("True (default): tunggu halaman selesai dimuat.")]
        public InArgument<bool> WaitForLoad { get; set; }

        [Category("Options")]
        [DisplayName("Timeout")]
        [Description("Batas menunggu halaman selesai (default 30 detik kalau kosong).")]
        public InArgument<TimeSpan> Timeout { get; set; }

        [Category("Output")]
        [DisplayName("Title")]
        [Description("Judul halaman setelah dimuat.")]
        public OutArgument<string> Title { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [Description("Kalau true, kegagalan activity ini tidak menghentikan workflow (default false).")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = ContinueOnError != null && ContinueOnError.Get(context);

            try
            {
                var url = Url.Get(context);
                if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("Url kosong.");

                var result = BrowserCommands.Navigate(
                    "navigate",
                    TabId != null ? TabId.Get(context) : null,
                    WaitForLoad == null || WaitForLoad.Get(context),
                    Timeout != null ? Timeout.Get(context) : TimeSpan.Zero,
                    extra => extra["url"] = url);

                if (Title != null) Title.Set(context, result?["title"]?.Value<string>() ?? "");
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }

    /// <summary>
    /// Bagian yang dipakai bersama Navigate URL, Go Back, Go Forward, dan
    /// Refresh Page — keempatnya hanya berbeda pada nama aksinya.
    /// </summary>
    internal static class BrowserCommands
    {
        public static JToken Navigate(string action, int? tabId, bool waitForLoad, TimeSpan timeout,
                                      Action<JObject> fill = null)
        {
            if (timeout <= TimeSpan.Zero) timeout = TimeSpan.FromSeconds(30);
            var timeoutMs = (int)timeout.TotalMilliseconds;

            var request = new JObject
            {
                ["waitForLoad"] = waitForLoad,
                ["timeoutMs"] = timeoutMs
            };

            if (tabId.HasValue) request["tabId"] = tabId.Value;
            if (fill != null) fill(request);

            return StudioPipeClient.SendCommand(action, request, responseTimeoutMs: timeoutMs + 15000);
        }
    }
}

using System;
using System.Activities;
using System.ComponentModel;
using Newtonsoft.Json.Linq;

namespace Custom.StudioBridge
{
    [Designer(typeof(Design.StudioClickDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    public class StudioClick : CodeActivity
    {
        [Category("Input")]
        [DisplayName("TabId")]
        [Description("Opsional. Kosongkan untuk pakai tab yang sedang aktif.")]
        public InArgument<int?> TabId { get; set; }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Selector")]
        public InArgument<string> Selector { get; set; }

        [Category("Options")]
        [DisplayName("Timeout")]
        [Description("Berapa lama tunggu elemen muncul sebelum menyerah (default 10 detik kalau kosong)")]
        public InArgument<TimeSpan> Timeout { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        public InArgument<bool> ContinueOnError { get; set; }

        /// <summary>
        /// BARU: screenshot area elemen (base64 PNG) hasil "Indicate on
        /// screen" terakhir, cuma buat ditampilkan di canvas (mirip
        /// Informative Screenshot UiPath) -- BUKAN input yang dievaluasi
        /// saat runtime, makanya plain property (bukan InArgument) dan
        /// disembunyikan dari Properties panel.
        /// </summary>
        [Browsable(false)]
        public string ScreenshotBase64 { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = ContinueOnError != null ? ContinueOnError.Get(context) : false;

            try
            {
                var tabId = TabId != null ? TabId.Get(context) : null;
                var selector = Selector.Get(context);
                var timeout = Timeout != null ? Timeout.Get(context) : TimeSpan.Zero;
                if (timeout == TimeSpan.Zero) timeout = TimeSpan.FromSeconds(10);
                var timeoutMs = (int)timeout.TotalMilliseconds;

                var request = new JObject { ["selector"] = selector, ["timeoutMs"] = timeoutMs };
                if (tabId.HasValue) request["tabId"] = tabId.Value;

                StudioPipeClient.SendCommand("click", request, responseTimeoutMs: timeoutMs + 15000);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

using System;
using System.Activities;
using System.ComponentModel;
using Custom.StudioBridge.Design;
using Newtonsoft.Json.Linq;

namespace Custom.StudioBridge
{
    [Designer(typeof(Design.StudioGetTextDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.gettext.png")]
    [DisplayName("Get Text")]
    [Description("Membaca teks dari elemen web atau desktop.")]
    public class StudioGetText : CodeActivity
    {
        public StudioGetText()
        {
            DisplayName = "Get Text";
        }

        [Category("Input")]
        [DisplayName("TabId")]
        [Description("Opsional. Hanya dipakai untuk target web. Kosongkan untuk pakai tab yang sedang aktif.")]
        public InArgument<int?> TabId { get; set; }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Selector")]
        [Description("Selector web (<webctrl .../>) atau desktop (<wnd .../><ctrl .../>). " +
                     "Jenis targetnya dikenali otomatis dari bentuk selector.")]
        public InArgument<string> Selector { get; set; }

        [Category("Options")]
        [DisplayName("Timeout")]
        [Description("Berapa lama tunggu elemen muncul sebelum menyerah (default 10 detik kalau kosong)")]
        public InArgument<TimeSpan> Timeout { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        [Category("Output")]
        [DisplayName("Text")]
        public OutArgument<string> Text { get; set; }

        [Browsable(false)]
        public string ScreenshotBase64 { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = ContinueOnError != null ? ContinueOnError.Get(context) : false;

            try
            {
                var selector = Selector.Get(context);
                var timeout = Timeout != null ? Timeout.Get(context) : TimeSpan.Zero;
                if (timeout == TimeSpan.Zero) timeout = TimeSpan.FromSeconds(10);

                if (SelectorKindDetector.Detect(selector) == SelectorKind.Desktop)
                {
                    var desktopText = DesktopActions.GetText(selector, timeout);
                    if (Text != null) Text.Set(context, desktopText);
                    return;
                }

                var tabId = TabId != null ? TabId.Get(context) : null;
                var timeoutMs = (int)timeout.TotalMilliseconds;

                var request = new JObject { ["selector"] = selector, ["timeoutMs"] = timeoutMs };
                if (tabId.HasValue) request["tabId"] = tabId.Value;

                var result = StudioPipeClient.SendCommand("getText", request, responseTimeoutMs: timeoutMs + 15000);

                var text = result?["text"]?.Value<string>() ?? "";
                if (Text != null) Text.Set(context, text);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

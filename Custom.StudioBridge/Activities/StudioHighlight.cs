using System;
using System.Activities;
using System.ComponentModel;
using Custom.StudioBridge.Design;
using Newtonsoft.Json.Linq;

namespace Custom.StudioBridge
{
    [Designer(typeof(Design.StudioHighlightDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.highlight.png")]
    [DisplayName("Highlight")]
    [Description("Menyorot elemen web atau desktop sebentar, untuk memastikan selector menunjuk elemen yang benar.")]
    public class StudioHighlight : CodeActivity
    {
        public StudioHighlight()
        {
            DisplayName = "Highlight";
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
                    DesktopActions.Highlight(selector, timeout);
                    return;
                }

                var tabId = TabId != null ? TabId.Get(context) : null;
                var timeoutMs = (int)timeout.TotalMilliseconds;

                // Aksi "highlight" (bukan "explorerHighlight"): jalur runtime
                // sengaja TIDAK membawa tab ke depan, supaya tidak merebut
                // layar di tengah workflow dan merusak langkah berikutnya yang
                // bergantung pada fokus jendela.
                var request = new JObject { ["selector"] = selector, ["timeoutMs"] = timeoutMs };
                if (tabId.HasValue) request["tabId"] = tabId.Value;

                StudioPipeClient.SendCommand("highlight", request, responseTimeoutMs: timeoutMs + 15000);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

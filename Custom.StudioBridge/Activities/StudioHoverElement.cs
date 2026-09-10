using System;
using System.Activities;
using System.ComponentModel;
using Custom.StudioBridge.Design;
using Newtonsoft.Json.Linq;

namespace Custom.StudioBridge
{
    /// <summary>
    /// Mengarahkan penunjuk mouse ke elemen web ATAU desktop tanpa menekan
    /// tombol, supaya menu, tooltip, atau tampilan yang hanya muncul saat
    /// disentuh ikut terbuka.
    ///
    /// Pengganti activity bawaan OpenRPA "Move Element" yang memakai selector
    /// JSON lama.
    ///
    /// Catatan jujur soal sisi web: halaman tidak punya penunjuk mouse
    /// sungguhan, jadi yang dikirim adalah rangkaian event yang sama dengan
    /// yang dikirim peramban saat mouse masuk ke elemen. Halaman yang
    /// memeriksa "isTrusted" tidak akan tertipu, tapi menu dan tooltip biasa
    /// bereaksi normal. Sisi desktop benar-benar menggerakkan kursor.
    /// </summary>
    [Designer(typeof(Design.StudioHoverElementDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Hover Element")]
    [Description("Mengarahkan mouse ke elemen web atau desktop tanpa mengklik.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.hoverelement.png")]
    public class StudioHoverElement : CodeActivity
    {
        public StudioHoverElement()
        {
            DisplayName = "Hover Element";
        }

        [Category("Target")]
        [DisplayName("TabId")]
        [Description("Opsional. Hanya dipakai untuk target web. Kosongkan untuk pakai tab yang sedang aktif.")]
        public InArgument<int?> TabId { get; set; }

        [Category("Target")]
        [RequiredArgument]
        [DisplayName("Selector")]
        [Description("Selector web (<webctrl .../>) atau desktop (<wnd .../><ctrl .../>). " +
                     "Jenis targetnya dikenali otomatis dari bentuk selector.")]
        public InArgument<string> Selector { get; set; }

        [Category("Target")]
        [DisplayName("Timeout")]
        [Description("Berapa lama tunggu elemen muncul sebelum menyerah (default 10 detik kalau kosong)")]
        public InArgument<TimeSpan> Timeout { get; set; }

        [Category("Options")]
        [DisplayName("Offset X")]
        [Description("Titik yang disentuh diukur dari sudut KIRI-ATAS elemen. Kosong (-1) berarti tengah elemen. " +
                     "Hanya berlaku untuk target desktop.")]
        public InArgument<int> OffsetX { get; set; }

        [Category("Options")]
        [DisplayName("Offset Y")]
        [Description("Kosong (-1) berarti tengah elemen. Hanya berlaku untuk target desktop.")]
        public InArgument<int> OffsetY { get; set; }

        [Category("Options")]
        [DisplayName("Post Wait")]
        [Description("Jeda setelah mouse diarahkan, memberi waktu menu atau tooltip muncul.")]
        public InArgument<TimeSpan> PostWait { get; set; }

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
                var selector = Selector.Get(context);
                var timeout = Timeout != null ? Timeout.Get(context) : TimeSpan.Zero;
                if (timeout == TimeSpan.Zero) timeout = TimeSpan.FromSeconds(10);

                if (SelectorKindDetector.Detect(selector) == SelectorKind.Desktop)
                {
                    DesktopActions.Hover(selector, timeout, Positive(OffsetX, context), Positive(OffsetY, context));
                }
                else
                {
                    var tabId = TabId != null ? TabId.Get(context) : null;
                    var timeoutMs = (int)timeout.TotalMilliseconds;

                    var request = new JObject { ["selector"] = selector, ["timeoutMs"] = timeoutMs };
                    if (tabId.HasValue) request["tabId"] = tabId.Value;

                    StudioPipeClient.SendCommand("hover", request, responseTimeoutMs: timeoutMs + 15000);
                }

                var postWait = PostWait != null ? PostWait.Get(context) : TimeSpan.Zero;
                if (postWait > TimeSpan.Zero) System.Threading.Thread.Sleep(postWait);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }

        /// <summary>Offset dianggap kosong kalau negatif; 0 adalah titik yang sah.</summary>
        private static int? Positive(InArgument<int> argument, CodeActivityContext context)
        {
            if (argument == null) return null;
            var value = argument.Get(context);
            return value < 0 ? (int?)null : value;
        }
    }
}

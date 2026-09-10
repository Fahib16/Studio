using System;
using System.Activities;
using System.ComponentModel;
using Custom.StudioBridge.Design;
using Newtonsoft.Json.Linq;

namespace Custom.StudioBridge
{
    /// <summary>
    /// Menaruh fokus keyboard pada elemen web ATAU desktop, tanpa mengkliknya.
    ///
    /// Pengganti activity bawaan OpenRPA "Focus Element", yang memakai selector
    /// JSON lama. Bedanya dengan Click: klik memindahkan fokus SEKALIGUS
    /// menekan tombol mouse, dan pada sebagian aplikasi penekanan itu punya
    /// akibat sendiri (menu terbuka, kotak centang berubah). Kalau yang
    /// dibutuhkan cuma fokus, activity ini yang benar.
    /// </summary>
    [Designer(typeof(Design.StudioFocusElementDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Focus Element")]
    [Description("Menaruh fokus keyboard pada elemen web atau desktop, tanpa mengklik.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.focuselement.png")]
    public class StudioFocusElement : CodeActivity
    {
        public StudioFocusElement()
        {
            DisplayName = "Focus Element";
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
        [DisplayName("Post Wait")]
        [Description("Jeda setelah fokus dipindahkan.")]
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
                    DesktopActions.Focus(selector, timeout);
                }
                else
                {
                    var tabId = TabId != null ? TabId.Get(context) : null;
                    var timeoutMs = (int)timeout.TotalMilliseconds;

                    var request = new JObject { ["selector"] = selector, ["timeoutMs"] = timeoutMs };
                    if (tabId.HasValue) request["tabId"] = tabId.Value;

                    StudioPipeClient.SendCommand("focus", request, responseTimeoutMs: timeoutMs + 15000);
                }

                var postWait = PostWait != null ? PostWait.Get(context) : TimeSpan.Zero;
                if (postWait > TimeSpan.Zero) System.Threading.Thread.Sleep(postWait);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

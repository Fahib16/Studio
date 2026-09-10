using System;
using System.Activities;
using System.Activities.Presentation.PropertyEditing;
using System.ComponentModel;
using Custom.StudioBridge.Design;
using Newtonsoft.Json.Linq;

namespace Custom.StudioBridge
{
    /// <summary>
    /// Mengetik teks ke elemen di halaman web ATAU aplikasi desktop.
    ///
    /// Satu-satunya activity pengisian teks di Studio ini. Activity
    /// "Type Into (UiPath style)" yang dulu memakai selector JSON bawaan
    /// OpenRPA sudah DIHAPUS, dan pilihan-pilihannya (klik dulu, ketik
    /// sungguhan, jeda antar tombol, tombol penahan, post wait) dipindahkan
    /// ke sini.
    ///
    /// Jenis target dibaca dari BENTUK selector, bukan dari properti terpisah.
    /// </summary>
    [Designer(typeof(Design.StudioSetTextDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Type Into")]
    [Description("Mengetik teks ke elemen web atau desktop.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.typeinto.png")]
    public class StudioSetText : CodeActivity
    {
        public StudioSetText()
        {
            DisplayName = "Type Into";
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

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Text")]
        [Description("Teks yang diketik. Token seperti {ENTER} dan {TAB} dikenali kalau " +
                     "Simulate Keystrokes dinyalakan (target desktop).")]
        public InArgument<string> Text { get; set; }

        [Category("Options")]
        [DisplayName("Empty Field")]
        [Description("Kosongkan isi field dulu sebelum mengetik. Hanya berlaku untuk target desktop " +
                     "yang harus diketik manual; jalur web selalu menimpa nilainya.")]
        public InArgument<bool> EmptyField { get; set; } = true;

        [Category("Options")]
        [DisplayName("Click Before Typing")]
        [Description("Klik elemennya dulu supaya benar-benar fokus (default true). " +
                     "Hanya target desktop pada jalur ketik manual.")]
        public InArgument<bool> ClickBeforeTyping { get; set; } = true;

        [Category("Options")]
        [DisplayName("Simulate Keystrokes")]
        [Description("True: ketik lewat penekanan tombol sungguhan — menghormati token {ENTER} dan " +
                     "Key Modifiers, dan dikenali aplikasi yang mengabaikan pengisian nilai langsung. " +
                     "False (default): isi nilainya langsung, jauh lebih cepat. Hanya target desktop.")]
        public InArgument<bool> SimulateKeystrokes { get; set; } = false;

        [Category("Options")]
        [DisplayName("Delay Between Keys (ms)")]
        [Description("Jeda antar tombol saat Simulate Keystrokes aktif (default 10 ms).")]
        public InArgument<int> DelayBetweenKeysMs { get; set; } = 10;

        [Category("Options")]
        [Editor(typeof(KeyModifiersOptionsEditor), typeof(ExtendedPropertyValueEditor))]
        [DisplayName("Key Modifiers")]
        [Description("Tombol yang ditahan selama mengetik, mis. {LCONTROL}. Hanya target desktop.")]
        public InArgument<string> KeyModifiers { get; set; }

        [Category("Options")]
        [DisplayName("Post Wait")]
        [Description("Jeda setelah selesai mengetik.")]
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
                var text = Text.Get(context);
                var timeout = Timeout != null ? Timeout.Get(context) : TimeSpan.Zero;
                if (timeout == TimeSpan.Zero) timeout = TimeSpan.FromSeconds(10);

                if (SelectorKindDetector.Detect(selector) == SelectorKind.Desktop)
                {
                    DesktopActions.SetText(
                        selector, text, timeout,
                        EmptyField == null || EmptyField.Get(context),
                        ClickBeforeTyping == null || ClickBeforeTyping.Get(context),
                        SimulateKeystrokes != null && SimulateKeystrokes.Get(context),
                        DelayBetweenKeysMs != null ? DelayBetweenKeysMs.Get(context) : 10,
                        KeyModifiers != null ? KeyModifiers.Get(context) : null);
                }
                else
                {
                    var tabId = TabId != null ? TabId.Get(context) : null;
                    var timeoutMs = (int)timeout.TotalMilliseconds;

                    var request = new JObject { ["selector"] = selector, ["text"] = text, ["timeoutMs"] = timeoutMs };
                    if (tabId.HasValue) request["tabId"] = tabId.Value;

                    StudioPipeClient.SendCommand("setText", request, responseTimeoutMs: timeoutMs + 15000);
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

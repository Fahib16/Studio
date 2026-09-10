using System;
using System.Activities;
using System.Activities.Presentation.PropertyEditing;
using System.ComponentModel;
using Custom.StudioBridge.Design;
using Newtonsoft.Json.Linq;

namespace Custom.StudioBridge
{
    /// <summary>
    /// Mengklik elemen di halaman web ATAU aplikasi desktop.
    ///
    /// Satu-satunya activity klik di Studio ini. Activity "Click (UiPath style)"
    /// yang dulu memakai selector JSON bawaan OpenRPA sudah DIHAPUS, dan
    /// pilihan-pilihannya (tombol mouse, offset, tombol penahan, fokus, klik
    /// virtual, post wait) dipindahkan ke sini — jadi tidak ada lagi dua cara
    /// mengklik dengan dua bentuk selector yang berbeda.
    ///
    /// Jenis target dibaca dari BENTUK selector, bukan dari properti terpisah,
    /// supaya tidak mungkin selector desktop dijalankan lewat jalur browser
    /// hanya karena suatu pilihan lupa diubah.
    /// </summary>
    [Designer(typeof(Design.StudioClickDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Click")]
    [Description("Mengklik elemen web atau desktop.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.click.png")]
    public class StudioClick : CodeActivity
    {
        public StudioClick()
        {
            DisplayName = "Click";
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
        [DisplayName("Double Click")]
        [Description("Klik ganda alih-alih klik tunggal. Berlaku untuk target web maupun desktop.")]
        public InArgument<bool> DoubleClick { get; set; } = false;

        [Category("Input")]
        [DisplayName("Mouse Button")]
        [Description("Left (default), Right, atau Middle. Klik kanan di web juga memicu contextmenu.")]
        [DefaultValue(MouseButtonKind.Left)]
        public MouseButtonKind MouseButton { get; set; } = MouseButtonKind.Left;

        [Category("Input")]
        [Editor(typeof(KeyModifiersOptionsEditor), typeof(ExtendedPropertyValueEditor))]
        [DisplayName("Key Modifiers")]
        [Description("Tombol yang ditahan saat mengklik, mis. {LCONTROL}. Di web diterjemahkan " +
                     "menjadi ctrlKey/shiftKey/altKey pada event mouse-nya.")]
        public InArgument<string> KeyModifiers { get; set; }

        [Category("Options")]
        [DisplayName("Offset X")]
        [Description("Titik klik diukur dari sudut KIRI-ATAS elemen. Kosong (-1) berarti tengah elemen.")]
        public InArgument<int> OffsetX { get; set; }

        [Category("Options")]
        [DisplayName("Offset Y")]
        [Description("Kosong (-1) berarti tengah elemen.")]
        public InArgument<int> OffsetY { get; set; }

        [Category("Options")]
        [DisplayName("Focus")]
        [Description("Fokuskan elemennya dulu sebelum diklik (hanya target desktop).")]
        public InArgument<bool> Focus { get; set; }

        [Category("Options")]
        [DisplayName("Virtual Click")]
        [Description("Klik tanpa menggerakkan kursor, lewat pola Invoke UI Automation — tidak " +
                     "mengganggu pekerjaan orang di depan layar. Hanya target desktop, dan hanya " +
                     "untuk kontrol yang mendukungnya. Jalur web memang selalu virtual.")]
        public InArgument<bool> VirtualClick { get; set; }

        [Category("Options")]
        [DisplayName("Post Wait")]
        [Description("Jeda setelah mengklik, untuk memberi aplikasi waktu bereaksi.")]
        public InArgument<TimeSpan> PostWait { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [Description("Kalau true, kegagalan activity ini tidak menghentikan workflow (default false).")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        /// <summary>
        /// Screenshot elemen hasil Indicate terakhir, cuma untuk ditampilkan
        /// di canvas (mirip Informative Screenshot UiPath) -- BUKAN input yang
        /// dievaluasi saat runtime, makanya plain property dan disembunyikan
        /// dari Properties panel.
        /// </summary>
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

                var doubleClick = DoubleClick != null && DoubleClick.Get(context);
                var keyModifiers = KeyModifiers != null ? KeyModifiers.Get(context) : null;

                int? offsetX = Positive(OffsetX, context);
                int? offsetY = Positive(OffsetY, context);

                if (SelectorKindDetector.Detect(selector) == SelectorKind.Desktop)
                {
                    DesktopActions.Click(selector, timeout, doubleClick, MouseButton, offsetX, offsetY,
                                         keyModifiers,
                                         Focus != null && Focus.Get(context),
                                         VirtualClick != null && VirtualClick.Get(context));
                }
                else
                {
                    var tabId = TabId != null ? TabId.Get(context) : null;
                    var timeoutMs = (int)timeout.TotalMilliseconds;

                    var request = new JObject
                    {
                        ["selector"] = selector,
                        ["timeoutMs"] = timeoutMs,
                        ["doubleClick"] = doubleClick,
                        ["button"] = MouseButtonKinds.ToDomButton(MouseButton)
                    };

                    if (offsetX.HasValue) request["offsetX"] = offsetX.Value;
                    if (offsetY.HasValue) request["offsetY"] = offsetY.Value;

                    var modifiers = KeyModifierMap.ToWeb(keyModifiers);
                    if (modifiers != null) request["modifiers"] = modifiers;

                    if (tabId.HasValue) request["tabId"] = tabId.Value;

                    StudioPipeClient.SendCommand("click", request, responseTimeoutMs: timeoutMs + 15000);
                }

                var postWait = PostWait != null ? PostWait.Get(context) : TimeSpan.Zero;
                if (postWait > TimeSpan.Zero) System.Threading.Thread.Sleep(postWait);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }

        /// <summary>
        /// Offset dianggap "tidak diisi" kalau negatif. 0 adalah titik yang sah
        /// (sudut kiri-atas elemen), jadi tidak bisa dipakai sebagai penanda
        /// kosong.
        /// </summary>
        private static int? Positive(InArgument<int> argument, CodeActivityContext context)
        {
            if (argument == null) return null;
            var value = argument.Get(context);
            return value < 0 ? (int?)null : value;
        }
    }

    /// <summary>
    /// Terjemahan tombol penahan dari bentuk token OpenRPA ({LCONTROL}) ke
    /// bendera event mouse DOM.
    ///
    /// Dipisah dari KeyboardInput karena sisi web tidak mengenal virtual key
    /// code sama sekali — yang dipahami halaman hanyalah ctrlKey/shiftKey/altKey.
    /// </summary>
    internal static class KeyModifierMap
    {
        public static JObject ToWeb(string keyModifiers)
        {
            if (string.IsNullOrWhiteSpace(keyModifiers)) return null;

            var text = keyModifiers.ToUpperInvariant();

            var ctrl = text.Contains("CONTROL") || text.Contains("CTRL");
            var shift = text.Contains("SHIFT");
            var alt = text.Contains("MENU") || text.Contains("ALT");

            if (!ctrl && !shift && !alt) return null;

            return new JObject { ["ctrl"] = ctrl, ["shift"] = shift, ["alt"] = alt };
        }
    }
}

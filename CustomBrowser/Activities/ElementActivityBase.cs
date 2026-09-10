using System;
using System.Activities;
using System.ComponentModel;
using System.Threading;
using Custom.StudioBridge;
using Custom.StudioBridge.Design;
using Newtonsoft.Json.Linq;

namespace Custom.Browser
{
    /// <summary>
    /// Kelas dasar activity yang menyasar SATU ELEMEN, baik di halaman web
    /// maupun di aplikasi desktop.
    ///
    /// Jenis targetnya dibaca dari BENTUK selector lewat SelectorKindDetector,
    /// bukan dari properti terpisah — pola yang sama persis dengan StudioClick
    /// dan StudioSetText. Dengan begitu tidak mungkin selector desktop
    /// dijalankan lewat jalur browser hanya karena suatu pilihan lupa diubah.
    ///
    /// Kelas ini abstract, jadi tidak ikut muncul di toolbox.
    /// </summary>
    public abstract class ElementActivityBase : CodeActivity
    {
        [Category("Target")]
        [RequiredArgument]
        [DisplayName("Selector")]
        [Description("Selector web (<webctrl .../>) atau desktop (<wnd .../><ctrl .../>). " +
                     "Jenis targetnya dikenali otomatis dari bentuk selector.")]
        public InArgument<string> Selector { get; set; }

        [Category("Target")]
        [DisplayName("TabId")]
        [Description("Opsional, hanya untuk target web. Kosongkan untuk memakai tab yang sedang aktif.")]
        public InArgument<int?> TabId { get; set; }

        [Category("Options")]
        [DisplayName("Timeout")]
        [Description("Berapa lama menunggu elemen sebelum menyerah (default 10 detik kalau kosong).")]
        public InArgument<TimeSpan> Timeout { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [Description("Kalau true, kegagalan activity ini tidak menghentikan workflow (default false).")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        /// <summary>
        /// Screenshot elemen hasil Indicate terakhir — hanya untuk ditampilkan
        /// di kartu canvas, bukan input runtime.
        /// </summary>
        [Browsable(false)]
        public string ScreenshotBase64 { get; set; }

        protected bool GetContinueOnError(CodeActivityContext context)
        {
            return ContinueOnError != null && ContinueOnError.Get(context);
        }

        protected string GetSelector(CodeActivityContext context)
        {
            var selector = Selector.Get(context);
            if (string.IsNullOrWhiteSpace(selector)) throw new ArgumentException("Selector kosong.");
            return selector;
        }

        protected TimeSpan GetTimeout(CodeActivityContext context)
        {
            var timeout = Timeout != null ? Timeout.Get(context) : TimeSpan.Zero;
            return timeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(10) : timeout;
        }

        protected bool IsDesktop(string selector)
        {
            return SelectorKindDetector.Detect(selector) == SelectorKind.Desktop;
        }

        /// <summary>
        /// Kirim satu perintah ke extension untuk selector web.
        /// </summary>
        protected JToken SendWeb(CodeActivityContext context, string action, string selector,
                                 TimeSpan timeout, Action<JObject> fill = null)
        {
            var timeoutMs = (int)timeout.TotalMilliseconds;

            var request = new JObject { ["selector"] = selector, ["timeoutMs"] = timeoutMs };

            var tabId = TabId != null ? TabId.Get(context) : null;
            if (tabId.HasValue) request["tabId"] = tabId.Value;

            if (fill != null) fill(request);

            return StudioPipeClient.SendCommand(action, request, responseTimeoutMs: timeoutMs + 15000);
        }

        /// <summary>
        /// Apakah elemen ada SEKARANG (sekali periksa, tanpa menunggu).
        /// Dipakai Element Exists dan Wait Element Vanish, yang keduanya perlu
        /// jawaban ada/tidak berulang kali — bukan "pastikan ada".
        /// </summary>
        protected bool ExistsNow(CodeActivityContext context, string selector)
        {
            if (IsDesktop(selector)) return DesktopActions.Exists(selector);

            var tabId = TabId != null ? TabId.Get(context) : null;
            var result = StudioPipeClient.ValidateSelector(tabId ?? 0, selector);

            var count = result?["count"]?.Value<int>() ?? 0;
            return count > 0;
        }

        /// <summary>
        /// Ulangi pemeriksaan sampai hasilnya sesuai yang ditunggu, atau
        /// sampai waktu habis. Mengembalikan true kalau keadaan yang ditunggu
        /// tercapai.
        /// </summary>
        protected bool PollUntil(CodeActivityContext context, string selector, bool wantedExists, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;

            while (true)
            {
                if (ExistsNow(context, selector) == wantedExists) return true;
                if (DateTime.UtcNow >= deadline) return false;
                Thread.Sleep(250);
            }
        }
    }
}

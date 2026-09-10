using System;
using System.Activities;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using OpenRPA.Interfaces;
using OpenRPA.NM;

namespace Custom.Browser
{
    /// <summary>
    /// Activity kustom: Attach Browser ala UiPath (UiPath.Core.Activities.BrowserScope)
    /// untuk Custom.Browser (OpenRPA).
    ///
    /// VERSI 3 -- lepas dari selector OpenRPA. Versi sebelumnya membuka
    /// dialog SelectorWindow("NM", ...) milik OpenRPA dan menyimpan selector
    /// JSON-nya, padahal saat dijalankan yang benar-benar dibaca dari selector
    /// itu hanyalah URL tab. Sekarang tombol "Indicate browser on screen"
    /// memakai pemilih tab dari jembatan Studio sendiri
    /// (Custom.StudioBridge.Design.IndicateHelper.PickTabForAttach) dan yang
    /// tersimpan langsung URL-nya di properti Url. Properti Selector lama
    /// masih ada tapi disembunyikan, semata supaya workflow yang sudah
    /// terlanjur menyimpannya tetap bisa dibuka.
    ///
    /// Properti dan nama disesuaikan sedekat mungkin dengan BrowserScope asli:
    /// Url, Browser (input, utk chaining dari scope lain), BrowserType,
    /// Timeout, Output UiBrowser. "Private" dan "SearchScope" milik UiPath
    /// SENGAJA TIDAK ditiru -- tidak ada padanan nyata di arsitektur NMHook
    /// OpenRPA, tidak mau bikin properti dekoratif yang tidak ngapa-ngapain.
    /// </summary>
    [Designer(typeof(Design.AttachBrowserDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.attachbrowser.png")]
    [DisplayName("Attach Browser")]
    [Description("Menyambung ke jendela browser yang sudah terbuka.")]
    public sealed class AttachBrowser : NativeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public AttachBrowser()
        {
            DisplayName = "Attach Browser";
        }

        /// <summary>
        /// Alamat tab yang mau dipakai; cocok kalau URL tab MEMUAT teks ini.
        ///
        /// Menggantikan properti Selector lama yang berisi selector JSON
        /// OpenRPA. Dari selector itu pun sebenarnya hanya URL-nya yang dibaca
        /// saat dijalankan, jadi tidak ada kemampuan yang hilang — yang hilang
        /// hanya ketergantungan pada selector bawaan OpenRPA.
        /// </summary>
        [Category("Input")]
        [DisplayName("Url")]
        [Description("Cocokkan dengan tab yang URL-nya memuat teks ini. Isi lewat tombol " +
                     "\"Indicate browser on screen\", atau ketik sendiri.")]
        public InArgument<string> Url { get; set; }

        /// <summary>
        /// Properti lama, dipertahankan HANYA supaya workflow yang sudah
        /// terlanjur menyimpannya tetap bisa dibuka. Tidak lagi dipakai saat
        /// dijalankan dan disembunyikan dari panel Properties.
        /// </summary>
        [Browsable(false)]
        public InArgument<string> Selector { get; set; }

        [Category("Input")]
        [DisplayName("Browser")]
        [Description("Alternatif Selector: tab yang sudah didapat dari activity lain (mis. Output UiBrowser " +
                      "dari Attach Browser/Open Browser lain). Isi salah satu dari Selector atau Browser.")]
        public InArgument<NativeMessagingMessageTab> Browser { get; set; }

        [Category("Input")]
        [DisplayName("Browser Type")]
        [DefaultValue(OpenBrowserKind.Chrome)]
        public OpenBrowserKind BrowserType { get; set; } = OpenBrowserKind.Chrome;

        [Category("Input")]
        [DisplayName("Timeout")]
        [Description("Batas waktu retry mencari tab (default 3 detik kalau kosong)")]
        public InArgument<TimeSpan> Timeout { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        [Category("Output")]
        [DisplayName("UiBrowser")]
        [Description("Info tab yang berhasil di-attach (title, url, id, dst)")]
        public OutArgument<NativeMessagingMessageTab> UiBrowser { get; set; }

        [Browsable(false)]
        public ActivityAction<NativeMessagingMessageTab> Body { get; set; }

        // Dipakai designer untuk preview thumbnail (pola sama seperti GetElement.Image)
        [Browsable(false)]
        public string Image { get; set; }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            metadata.AddArgument(new RuntimeArgument("Url", typeof(string), ArgumentDirection.In));
            metadata.AddArgument(new RuntimeArgument("Selector", typeof(string), ArgumentDirection.In));
            metadata.AddArgument(new RuntimeArgument("Browser", typeof(NativeMessagingMessageTab), ArgumentDirection.In));
            metadata.AddArgument(new RuntimeArgument("Timeout", typeof(TimeSpan), ArgumentDirection.In));
            metadata.AddArgument(new RuntimeArgument("ContinueOnError", typeof(bool), ArgumentDirection.In));
            metadata.AddArgument(new RuntimeArgument("UiBrowser", typeof(NativeMessagingMessageTab), ArgumentDirection.Out));

            if (Body != null)
                metadata.AddDelegate(Body);
        }

        protected override void Execute(NativeActivityContext context)
        {
            var continueOnError = ContinueOnError != null ? ContinueOnError.Get(context) : false;

            try
            {
                var browserFromInput = Browser != null ? Browser.Get(context) : null;
                var wantedUrl = Url != null ? Url.Get(context) : null;

                if (browserFromInput == null && string.IsNullOrEmpty(wantedUrl))
                    throw new ArgumentException("Attach Browser: salah satu dari [Url] atau [Browser] harus diisi.");

                var browserType = MapBrowser(BrowserType);
                var timeout = Timeout != null ? Timeout.Get(context) : TimeSpan.Zero;
                if (timeout == TimeSpan.Zero) timeout = TimeSpan.FromSeconds(3);

                NativeMessagingMessageTab found;

                if (browserFromInput != null)
                {
                    // Jalur "Browser" input -- langsung pakai tab yang sudah
                    // ada, tidak perlu cari lagi. Cuma pastikan tetap ke-select.
                    found = browserFromInput;
                    NMHook.enumtabs();
                    NMHook.selecttab(found.browser, found.id);
                }
                else
                {
                    // Jalur "Url" -- cari tab yang alamatnya memuat teks ini.
                    var url = wantedUrl;

                    var sw = Stopwatch.StartNew();
                    found = null;
                    do
                    {
                        NMHook.enumtabs();
                        found = NMHook.tabs
                            .Where(t => t.browser == browserType)
                            .FirstOrDefault(t => t.url != null && t.url.IndexOf(url, StringComparison.OrdinalIgnoreCase) >= 0);
                    } while (found == null && sw.Elapsed < timeout);

                    if (found == null)
                        throw new InvalidOperationException(
                            $"Attach Browser: tab dengan URL mengandung \"{url}\" tidak ditemukan dalam {timeout.TotalSeconds} detik.");

                    NMHook.selecttab(browserType, found.id);
                }

                if (UiBrowser != null) context.SetValue(UiBrowser, found);

                if (Body != null)
                    context.ScheduleAction(Body, found, OnBodyCompleted, OnBodyFaulted);
            }
            catch (Exception) when (continueOnError)
            {
                // Telan error kalau ContinueOnError = true
            }
        }

        private static string MapBrowser(OpenBrowserKind kind)
        {
            switch (kind)
            {
                case OpenBrowserKind.Firefox: return "ff";
                case OpenBrowserKind.Edge: return "edge";
                default: return "chrome";
            }
        }

        private void OnBodyCompleted(NativeActivityContext context, ActivityInstance completedInstance)
        {
            // Sengaja kosong -- Attach Browser tidak menutup tab di akhir scope.
        }

        private void OnBodyFaulted(NativeActivityFaultContext faultContext, Exception propagatedException,
            ActivityInstance propagatedFrom)
        {
            // Biarkan exception ter-propagate.
        }

        /// <summary>
        /// IActivityTemplateFactory.Create -- pola sama seperti OpenBrowser,
        /// delegate argument Body otomatis dinamai "browser" kalau di-drag
        /// fresh dari toolbox.
        /// </summary>
        public Activity Create(System.Windows.DependencyObject target)
        {
            var fef = new AttachBrowser();
            var da = new DelegateInArgument<NativeMessagingMessageTab> { Name = "browser" };
            fef.Body = new ActivityAction<NativeMessagingMessageTab>
            {
                Argument = da,

                // Sama seperti Open Browser: Do berisi Sequence sejak awal,
                // supaya langkah kedua bisa langsung ditambahkan.
                Handler = new System.Activities.Statements.Sequence { DisplayName = "Do" }
            };
            return fef;
        }
    }
}

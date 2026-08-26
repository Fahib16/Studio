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
    /// VERSI 2 -- dirombak total setelah user share screenshot BrowserScope
    /// asli. Ternyata UiPath pakai SELECTOR + tombol Indicate, BUKAN cuma
    /// text matching Url/Title seperti versi pertama saya. Sekarang activity
    /// ini pakai pola yang SAMA PERSIS dengan Open_Selector di Click v4 --
    /// dialog SelectorWindow("NM", ...) yang SAMA, cuma hasilnya disimpan ke
    /// property "Selector" di sini, lalu saat Execute() kita ekstrak info
    /// URL tab dari selector itu (lewat NMSelectorItem, PERSIS pola yang
    /// dipakai GetElement.cs versi NM: "var s = new NMSelectorItem(sel[0]);
    /// if (!string.IsNullOrEmpty(s.url)) ...").
    ///
    /// Properti dan nama disesuaikan sedekat mungkin dengan BrowserScope asli:
    /// Selector, Browser (input, utk chaining dari scope lain), BrowserType,
    /// Timeout, Output UiBrowser. "Private" dan "SearchScope" milik UiPath
    /// SENGAJA TIDAK ditiru -- tidak ada padanan nyata di arsitektur NMHook
    /// OpenRPA, tidak mau bikin properti dekoratif yang tidak ngapa-ngapain.
    /// </summary>
    [Designer(typeof(Design.AttachBrowserDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    public sealed class AttachBrowser : NativeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        [Category("Input")]
        [DisplayName("Selector")]
        [Description("Selector hasil \"Indicate browser on screen\". Diisi otomatis lewat tombol Indicate di designer.")]
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
                var selectorJson = Selector != null ? Selector.Get(context) : null;

                if (browserFromInput == null && string.IsNullOrEmpty(selectorJson))
                    throw new ArgumentException("Attach Browser: salah satu dari [Selector] atau [Browser] harus diisi.");

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
                    // Jalur "Selector" -- ekstrak URL dari selector, cari
                    // tab yang cocok. Pola PERSIS GetElement.cs versi NM:
                    // "var s = new NMSelectorItem(sel[0]); if (!string.IsNullOrEmpty(s.url)) ..."
                    var sel = new NMSelector(selectorJson);
                    var firstItem = new NMSelectorItem(sel[0]);
                    var url = firstItem.url;

                    if (string.IsNullOrEmpty(url))
                        throw new InvalidOperationException(
                            "Attach Browser: Selector tidak mengandung info URL yang valid.");

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
                Argument = da
            };
            return fef;
        }
    }
}

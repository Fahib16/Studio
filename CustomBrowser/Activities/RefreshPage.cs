using System;
using System.Activities;
using System.ComponentModel;
using Newtonsoft.Json.Linq;

namespace Custom.Browser
{
    /// <summary>
    /// Memuat ulang halaman di tab.
    ///
    /// Wait For Load (default true) menunggu halaman selesai dimuat, dengan
    /// alasan yang sama seperti Navigate URL: tanpa itu activity berikutnya
    /// bisa berjalan di atas halaman lama.
    /// </summary>
    [Designer(typeof(Design.RefreshPageDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Refresh Page")]
    [Description("Memuat ulang halaman di tab.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.refreshpage.png")]
    public sealed class RefreshPage : CodeActivity
    {
        public RefreshPage()
        {
            DisplayName = "Refresh Page";
            WaitForLoad = new InArgument<bool>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<bool>("True"));
        }

        [Category("Input")]
        [DisplayName("TabId")]
        [Description("Opsional. Kosongkan untuk memakai tab yang sedang aktif.")]
        public InArgument<int?> TabId { get; set; }

        [Category("Options")]
        [DisplayName("Wait For Load")]
        [Description("True (default): tunggu halaman selesai dimuat.")]
        public InArgument<bool> WaitForLoad { get; set; }

        [Category("Options")]
        [DisplayName("Timeout")]
        [Description("Batas menunggu halaman selesai (default 30 detik kalau kosong).")]
        public InArgument<TimeSpan> Timeout { get; set; }

        [Category("Output")]
        [DisplayName("Title")]
        public OutArgument<string> Title { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [Description("Kalau true, kegagalan activity ini tidak menghentikan workflow (default false).")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = ContinueOnError != null && ContinueOnError.Get(context);

            try
            {
                var result = BrowserCommands.Navigate(
                    "reload",
                    TabId != null ? TabId.Get(context) : null,
                    WaitForLoad == null || WaitForLoad.Get(context),
                    Timeout != null ? Timeout.Get(context) : TimeSpan.Zero);

                if (Title != null) Title.Set(context, result?["title"]?.Value<string>() ?? "");
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

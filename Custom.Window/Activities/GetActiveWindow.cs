using System;
using System.Activities;
using System.ComponentModel;

namespace Custom.Window
{
    /// <summary>
    /// Mengambil keterangan window yang sedang aktif (foreground).
    ///
    /// Berdiri sendiri, tidak mewarisi WindowActivityBase, karena tidak ada
    /// yang perlu dicari: yang ditanyakan justru window mana yang sedang
    /// aktif. Menampilkan properti pencarian di sini hanya akan membingungkan.
    ///
    /// Handle dikeluarkan sebagai IntPtr apa adanya supaya bisa dipakai
    /// activity/kode lain yang memang bekerja dengan handle window.
    /// </summary>
    [Designer(typeof(Design.GetActiveWindowDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Get Active Window")]
    [Description("Mengambil judul, nama proses, dan handle window yang sedang aktif.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.getactivewindow.png")]
    public sealed class GetActiveWindow : CodeActivity
    {
        public GetActiveWindow()
        {
            DisplayName = "Get Active Window";
        }

        [Category("Output")]
        [DisplayName("Title")]
        public OutArgument<string> Title { get; set; }

        [Category("Output")]
        [DisplayName("Process Name")]
        public OutArgument<string> ProcessName { get; set; }

        [Category("Output")]
        [DisplayName("Handle")]
        [Description("Handle window (HWND).")]
        public OutArgument<IntPtr> Handle { get; set; }

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
                var hWnd = WindowFinder.Foreground();
                if (hWnd == IntPtr.Zero)
                    throw new InvalidOperationException("Tidak ada window yang sedang aktif.");

                if (Title != null) Title.Set(context, WindowFinder.TitleOf(hWnd));
                if (ProcessName != null) ProcessName.Set(context, WindowFinder.ProcessNameOf(hWnd));
                if (Handle != null) Handle.Set(context, hWnd);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

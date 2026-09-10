using System;
using System.Activities;
using System.ComponentModel;

namespace Custom.Window
{
    /// <summary>
    /// Mengembalikan window ke ukuran semula (dari maximize atau minimize).
    ///
    /// Cara menemukan window-nya sama persis dengan Maximize Window (lihat
    /// WindowActivityBase): Tab, lalu judul/proses, lalu window aktif.
    /// </summary>
    [Designer(typeof(Design.RestoreWindowDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Restore Window")]
    [Description("Mengembalikan window ke ukuran semula (dari maximize atau minimize).")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.restorewindow.png")]
    public sealed class RestoreWindow : WindowActivityBase
    {
        public RestoreWindow()
        {
            DisplayName = "Restore Window";
        }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = GetContinueOnError(context);

            try
            {
                WindowFinder.Show(FindWindow(context), WindowFinder.SW_RESTORE);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

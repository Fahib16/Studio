using System;
using System.Activities;
using System.ComponentModel;

namespace Custom.Window
{
    /// <summary>
    /// Meminimalkan window ke taskbar.
    ///
    /// Cara menemukan window-nya sama persis dengan Maximize Window (lihat
    /// WindowActivityBase): Tab, lalu judul/proses, lalu window aktif.
    /// </summary>
    [Designer(typeof(Design.MinimizeWindowDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Minimize Window")]
    [Description("Meminimalkan window ke taskbar.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.minimizewindow.png")]
    public sealed class MinimizeWindow : WindowActivityBase
    {
        public MinimizeWindow()
        {
            DisplayName = "Minimize Window";
        }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = GetContinueOnError(context);

            try
            {
                WindowFinder.Show(FindWindow(context), WindowFinder.SW_MINIMIZE);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

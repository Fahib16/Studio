using System;
using System.Activities;
using System.ComponentModel;

namespace Custom.Window
{
    /// <summary>
    /// Memaksimalkan sebuah window.
    ///
    /// PINDAHAN dari project OpenRPA (OpenRPA.Activities.Custom.MaximizeWindow)
    /// ke kategori Custom.Window. Cara kerjanya tidak berubah — masih memakai
    /// Windows API lewat judul/proses, dan properti Tab tetap ada. Yang berubah
    /// hanya nama tipenya, jadi workflow lama yang sudah menyimpan tipe lama
    /// perlu di-drag ulang dari toolbox.
    /// </summary>
    [Designer(typeof(Design.MaximizeWindowDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Maximize Window")]
    [Description("Memaksimalkan window yang ditunjuk (atau window yang sedang aktif).")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.maximizewindow.png")]
    public sealed class MaximizeWindow : WindowActivityBase
    {
        public MaximizeWindow()
        {
            DisplayName = "Maximize Window";
        }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = GetContinueOnError(context);

            try
            {
                WindowFinder.Show(FindWindow(context), WindowFinder.SW_MAXIMIZE);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

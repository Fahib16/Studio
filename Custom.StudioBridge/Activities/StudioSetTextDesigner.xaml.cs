using System.Activities;
using System.Activities.Expressions;
using System.Windows;

namespace Custom.StudioBridge.Design
{
    /// <summary>
    /// Code-behind Studio Set Text -- tombol "Indicate on screen" isi Selector +
    /// ScreenshotBase64 (ditampilkan sbg "Informative Screenshot" di
    /// canvas, mirip UiPath). TabId SENGAJA TIDAK diisi dari sini (lihat
    /// catatan histori bug: TabId itu ephemeral, tidak boleh di-hardcode).
    /// </summary>
    public partial class StudioSetTextDesigner
    {
        public StudioSetTextDesigner()
        {
            InitializeComponent();
        }

        private void Indicate_Click(object sender, RoutedEventArgs e)
        {
            var result = IndicateHelper.Run();
            if (result == null) return; // dibatalkan di langkah manapun

            ModelItem.Properties["Selector"].SetValue(
                new InArgument<string>() { Expression = new Literal<string>(result.Selector) });

            ModelItem.Properties["ScreenshotBase64"].SetValue(result.ScreenshotBase64);
        }
    }
}

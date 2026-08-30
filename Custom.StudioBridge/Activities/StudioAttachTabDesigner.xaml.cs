using System.Activities;
using System.Activities.Expressions;
using System.Windows;

namespace Custom.StudioBridge.Design
{
    /// <summary>
    /// Code-behind Studio Attach Tab -- tombol "Pick from open tabs" pakai
    /// IndicateHelper.PickTabForAttach() (BEDA dari Run() yang dipakai
    /// Click/SetText/dst -- ini tidak masuk mode crosshair, cuma pilih dari
    /// daftar tab yang lagi kebuka), lalu isi UrlPattern dengan Url tab itu.
    /// </summary>
    public partial class StudioAttachTabDesigner
    {
        public StudioAttachTabDesigner()
        {
            InitializeComponent();
        }

        private void Indicate_Click(object sender, RoutedEventArgs e)
        {
            var picked = IndicateHelper.PickTabForAttach();
            if (picked == null) return; // dibatalkan

            ModelItem.Properties["UrlPattern"].SetValue(
                new InArgument<string>() { Expression = new Literal<string>(picked.Url) });
        }
    }
}

using Microsoft.VisualBasic.Activities;
using OpenRPA.Interfaces;
using OpenRPA.NM;
using System;
using System.Activities;
using System.Activities.Expressions;
using System.Activities.Presentation.Model;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Custom.Browser.Design
{
    /// <summary>
    /// Code-behind AttachBrowserDesigner. OpenSelector_Click PERSIS pola
    /// Open_Selector di UiPathStyleClickDesigner (mode NM/Web) yang sudah
    /// terbukti jalan -- bedanya cuma target property-nya "Selector" milik
    /// AttachBrowser di sini.
    /// </summary>
    public partial class AttachBrowserDesigner : INotifyPropertyChanged
    {
        public AttachBrowserDesigner()
        {
            InitializeComponent();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Memilih browser yang mau dipakai dari daftar tab yang sedang
        /// terbuka.
        ///
        /// Sebelumnya tombol ini membuka SelectorWindow milik OpenRPA dan
        /// menyimpan selector JSON-nya. Padahal dari selector itu yang
        /// benar-benar dipakai saat dijalankan hanyalah URL-nya. Sekarang
        /// pemilihnya memakai daftar tab dari jembatan Studio sendiri
        /// (IndicateHelper.PickTabForAttach), dan yang disimpan langsung
        /// URL-nya — tidak ada lagi selector OpenRPA di sini.
        /// </summary>
        private void OpenSelector_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picked = Custom.StudioBridge.Design.IndicateHelper.PickTabForAttach();
                if (picked == null) return;

                ModelItem.Properties["Url"].SetValue(
                    new InArgument<string>() { Expression = new Literal<string>(picked.Url ?? "") });

                NotifyPropertyChanged("Url");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    "Gagal mengambil daftar tab: " + ex.Message, "Attach Browser",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}

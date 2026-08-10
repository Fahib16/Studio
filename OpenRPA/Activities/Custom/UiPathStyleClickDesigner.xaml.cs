using Microsoft.VisualBasic.Activities;
using OpenRPA.Interfaces;
using System;
using System.Activities;
using System.Activities.Expressions;
using System.Activities.Presentation.Model;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OpenRPA.Activities.Custom.Design
{
    /// <summary>
    /// Code-behind UiPathStyleClickDesigner v4.
    ///
    /// 4 tombol Indicate terpisah (Desktop/Web x Target/Anchor) -- sengaja
    /// TIDAK satu tombol dengan toggle mode, supaya tidak perlu binding
    /// custom untuk state toggle (pelajaran dari bug ComboBox/enum binding
    /// sebelumnya). Masing-masing tombol independen dan sederhana.
    ///
    /// KETERBATASAN yang perlu disadari: badge "HasAnchor" (dipakai buat
    /// ⚓ icon di collapsed view & badge "Anchor: True/False") cuma ke-refresh
    /// setelah aksi lewat tombol kita (Open_Anchor_*/Clear_Anchor) atau saat
    /// designer pertama kali dimuat. Kalau AnchorSelector diedit manual lewat
    /// kotak ExpressionTextBox-nya langsung, badge itu TIDAK otomatis
    /// ter-refresh sampai ada aksi lain yang men-trigger NotifyPropertyChanged.
    /// Ini keterbatasan kosmetik saja, tidak mempengaruhi Execute() runtime
    /// (yang selalu baca nilai AnchorSelector langsung, bukan dari badge ini).
    /// </summary>
    public partial class UiPathStyleClickDesigner : INotifyPropertyChanged
    {
        public UiPathStyleClickDesigner()
        {
            InitializeComponent();
            HighlightImage = Interfaces.Extensions.GetImageSourceFromResource("search.png");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(String propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public BitmapFrame HighlightImage { get; set; }

        public bool HasAnchor
        {
            get
            {
                var anchor = ModelItem.GetValue<string>("AnchorSelector");
                return !string.IsNullOrEmpty(anchor);
            }
        }

        // ===================== INDICATE TARGET =====================

        private void Open_Selector_Desktop(object sender, RoutedEventArgs e)
        {
            OpenSelectorDialog(isWeb: false, isAnchor: false);
        }

        private void Open_Selector_Web(object sender, RoutedEventArgs e)
        {
            OpenSelectorDialog(isWeb: true, isAnchor: false);
        }

        // ===================== INDICATE ANCHOR =====================

        private void Open_Anchor_Desktop(object sender, RoutedEventArgs e)
        {
            OpenSelectorDialog(isWeb: false, isAnchor: true);
        }

        private void Open_Anchor_Web(object sender, RoutedEventArgs e)
        {
            OpenSelectorDialog(isWeb: true, isAnchor: true);
        }

        private void Clear_Anchor_Click(object sender, RoutedEventArgs e)
        {
            ModelItem.Properties["AnchorSelector"].SetValue(new InArgument<string>() { Expression = new Literal<string>("") });
            NotifyPropertyChanged("HasAnchor");
        }

        /// <summary>
        /// Satu method dipakai untuk 4 tombol di atas (Target/Anchor x
        /// Desktop/Web), supaya logic Open_Selector tidak diduplikasi 4x.
        /// Perbedaannya cuma: property mana yang di-set (Selector vs
        /// AnchorSelector) dan kelas selector mana yang dipakai
        /// (WindowsSelector vs NMSelector) -- pola masing-masing tetap
        /// PERSIS mengikuti GetElementDesigner.xaml.cs versi Windows/NM.
        /// </summary>
        private void OpenSelectorDialog(bool isWeb, bool isAnchor)
        {
            string propertyName = isAnchor ? "AnchorSelector" : "Selector";
            string existingValue = ModelItem.GetValue<string>(propertyName);
            const int maxresults = 1;

            Interfaces.Selector.SelectorWindow selectors;

            if (isWeb)
            {
                var selector = !string.IsNullOrEmpty(existingValue)
                    ? new OpenRPA.NM.NMSelector(existingValue)
                    : new OpenRPA.NM.NMSelector("[{Selector: 'NM'}]");
                selectors = new Interfaces.Selector.SelectorWindow("NM", selector, null, maxresults);
            }
            else
            {
                var selector = !string.IsNullOrEmpty(existingValue)
                    ? new OpenRPA.Windows.WindowsSelector(existingValue)
                    : new OpenRPA.Windows.WindowsSelector("[{Selector: 'Windows'}]");
                selectors = new Interfaces.Selector.SelectorWindow("Windows", selector, null, maxresults);
            }

            if (selectors.ShowDialog() == true)
            {
                ModelItem.Properties[propertyName].SetValue(new InArgument<string>() { Expression = new Literal<string>(selectors.vm.json) });

                var l = selectors.vm.Selector.Last();
                if (l.Element != null)
                {
                    // Preview thumbnail cuma untuk Target (Image), bukan Anchor --
                    // supaya kartu tetap nunjukin elemen yang mau DIKLIK sebagai
                    // preview utama, bukan elemen anchornya.
                    if (!isAnchor)
                    {
                        ModelItem.Properties["Image"].SetValue(l.Element.ImageString());
                        NotifyPropertyChanged("Image");
                    }
                }

                if (isAnchor) NotifyPropertyChanged("HasAnchor");
            }
        }

        // ===================== HIGHLIGHT =====================

        private async void Highlight_Click(object sender, RoutedEventArgs e)
        {
            string selectorStr = ModelItem.GetValue<string>("Selector");
            if (string.IsNullOrEmpty(selectorStr))
            {
                HighlightImage = Interfaces.Extensions.GetImageSourceFromResource(".searchfailed.png");
                NotifyPropertyChanged("HighlightImage");
                return;
            }
            string anchorStr = ModelItem.GetValue<string>("AnchorSelector");
            const int maxresults = 1;

            HighlightImage = Interfaces.Extensions.GetImageSourceFromResource("search.png");
            NotifyPropertyChanged("HighlightImage");

            // Deteksi jenis dari isi Selector-nya sendiri, sama seperti Execute()
            // di runtime -- pakai method DetectIsWeb yang sama (internal static
            // di UiPathStyleClick, accessible karena satu assembly/namespace).
            bool isWeb = UiPathStyleClick.DetectIsWeb(selectorStr);

            if (isWeb)
            {
                OpenRPA.NM.NMElement anchorEl = null;
                if (!string.IsNullOrEmpty(anchorStr))
                {
                    var anchorSel = new OpenRPA.NM.NMSelector(anchorStr);
                    var anchorRes = OpenRPA.NM.NMSelector.GetElementsWithuiSelector(anchorSel, null, 1);
                    anchorEl = anchorRes != null && anchorRes.Length > 0 ? anchorRes[0] : null;
                }

                var selector = new OpenRPA.NM.NMSelector(selectorStr);
                var res = OpenRPA.NM.NMSelector.GetElementsWithuiSelector(selector, anchorEl, maxresults);
                var elements = res != null ? res.ToList() : new List<OpenRPA.NM.NMElement>();

                HighlightImage = elements.Count() > 0
                    ? Interfaces.Extensions.GetImageSourceFromResource("searchfound.png")
                    : Interfaces.Extensions.GetImageSourceFromResource(".searchfailed.png");
                NotifyPropertyChanged("HighlightImage");
                foreach (var ele in elements) await ele.Highlight(false, System.Drawing.Color.Red, TimeSpan.FromSeconds(1));
            }
            else
            {
                await Task.Run(() =>
                {
                    OpenRPA.UIElement anchorEl = null;
                    if (!string.IsNullOrEmpty(anchorStr))
                    {
                        var anchorSel = new OpenRPA.Windows.WindowsSelector(anchorStr);
                        var anchorRes = OpenRPA.Windows.WindowsSelector.GetElementsWithuiSelector(anchorSel, null, 1, null);
                        anchorEl = anchorRes != null && anchorRes.Length > 0 ? anchorRes[0] : null;
                    }

                    var selector = new OpenRPA.Windows.WindowsSelector(selectorStr);
                    var res = OpenRPA.Windows.WindowsSelector.GetElementsWithuiSelector(selector, anchorEl, maxresults, null);
                    var elements = res != null ? res.ToList() : new List<OpenRPA.UIElement>();

                    HighlightImage = elements.Count() > 0
                        ? Interfaces.Extensions.GetImageSourceFromResource("searchfound.png")
                        : Interfaces.Extensions.GetImageSourceFromResource(".searchfailed.png");
                    NotifyPropertyChanged("HighlightImage");
                    foreach (var ele in elements) ele.Highlight(false, System.Drawing.Color.Red, TimeSpan.FromSeconds(1));
                });
            }
        }

        public string ImageString
        {
            get { return ModelItem.GetValue<string>("Image"); }
        }

        public BitmapImage Image
        {
            get
            {
                var image = ImageString;
                if (string.IsNullOrEmpty(image)) return null;
                System.Drawing.Bitmap b = Task.Run(() =>
                {
                    return Interfaces.Image.Util.LoadBitmap(image);
                }).Result;
                using (b)
                {
                    if (b == null) return null;
                    return Interfaces.Image.Util.BitmapToImageSource(b, Interfaces.Image.Util.ActivityPreviewImageWidth, Interfaces.Image.Util.ActivityPreviewImageHeight);
                }
            }
        }

        private void ActivityDesigner_Loaded(object sender, RoutedEventArgs e)
        {
            NotifyPropertyChanged("HasAnchor");
        }
    }
}

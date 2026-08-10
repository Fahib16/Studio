using Microsoft.VisualBasic.Activities;
using OpenRPA.Interfaces;
using OpenRPA.Windows;
using OpenRPA.NM;
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

namespace OpenRPA.Activities
{
    public partial class TypeIntoDesigner : INotifyPropertyChanged
    {
        public TypeIntoDesigner()
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

        private void Open_Selector(object sender, RoutedEventArgs e)
        {
            TargetTechnology tech = (TargetTechnology)ModelItem.Properties["Technology"].ComputedValue;
            string SelectorString = ModelItem.GetValue<string>("Selector");
            const int maxresults = 1;

            Interfaces.Selector.SelectorWindow selectors;

            if (tech == TargetTechnology.Web)
            {
                // Gunakan format strict JSON: [{"Selector": "NM"}]
                OpenRPA.Interfaces.Selector.Selector selector = !string.IsNullOrEmpty(SelectorString)
                    ? (OpenRPA.Interfaces.Selector.Selector)new NMSelector(SelectorString)
                    : (OpenRPA.Interfaces.Selector.Selector)new NMSelector("[{\"Selector\": \"NM\"}]");

                selectors = new Interfaces.Selector.SelectorWindow("NM", selector, null, maxresults);
            }
            else
            {
                ModelItem loadFrom = ModelItem.Parent;
                WindowsSelector anchor = null;
                while (loadFrom != null && loadFrom.Parent != null)
                {
                    var p = loadFrom.Properties.Where(x => x.Name == "Selector").FirstOrDefault();
                    if (p != null)
                    {
                        var loadFromSelectorString = loadFrom.GetValue<string>("Selector");
                        if (!string.IsNullOrEmpty(loadFromSelectorString))
                            anchor = new WindowsSelector(loadFromSelectorString);
                        break;
                    }
                    loadFrom = loadFrom.Parent;
                }

                // Gunakan format strict JSON: [{"Selector": "Windows"}]
                OpenRPA.Interfaces.Selector.Selector selector = !string.IsNullOrEmpty(SelectorString)
                    ? (OpenRPA.Interfaces.Selector.Selector)new WindowsSelector(SelectorString)
                    : (OpenRPA.Interfaces.Selector.Selector)new WindowsSelector("[{\"Selector\": \"Windows\"}]");

                selectors = new Interfaces.Selector.SelectorWindow("Windows", selector, anchor, maxresults);
            }

            if (selectors.ShowDialog() == true)
            {
                ModelItem.Properties["Selector"].SetValue(new InArgument<string>() { Expression = new Literal<string>(selectors.vm.json) });
                var l = selectors.vm.Selector.Last();
                if (l.Element != null)
                {
                    ModelItem.Properties["Image"].SetValue(l.Element.ImageString());
                    NotifyPropertyChanged("Image");
                }
            }
        }
        private void Highlight_Click(object sender, RoutedEventArgs e)
        {
            TargetTechnology tech = (TargetTechnology)ModelItem.Properties["Technology"].ComputedValue;
            string SelectorString = ModelItem.GetValue<string>("Selector");

            HighlightImage = Interfaces.Extensions.GetImageSourceFromResource("search.png");
            NotifyPropertyChanged("HighlightImage");

            if (string.IsNullOrEmpty(SelectorString))
            {
                HighlightImage = Interfaces.Extensions.GetImageSourceFromResource(".searchfailed.png");
                NotifyPropertyChanged("HighlightImage");
                return;
            }

            const int maxresults = 1;

            Task.Run(() =>
            {
                var elements = new List<IElement>();

                if (tech == TargetTechnology.Web)
                {
                    var selector = new NMSelector(SelectorString);
                    var res = NMSelector.GetElementsWithuiSelector(selector, null, maxresults);
                    if (res != null) elements.AddRange(res);
                }
                else
                {
                    var selector = new WindowsSelector(SelectorString);
                    var res = WindowsSelector.GetElementsWithuiSelector(selector, null, maxresults, null);
                    if (res != null) elements.AddRange(res);
                }

                if (elements.Count() > 0)
                {
                    HighlightImage = Interfaces.Extensions.GetImageSourceFromResource("searchfound.png");
                }
                else
                {
                    HighlightImage = Interfaces.Extensions.GetImageSourceFromResource(".searchfailed.png");
                }
                NotifyPropertyChanged("HighlightImage");

                foreach (var ele in elements) ele.Highlight(false, System.Drawing.Color.Red, TimeSpan.FromSeconds(1));
            });
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
        }
    }
}
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

        private void OpenSelector_Click(object sender, RoutedEventArgs e)
        {
            string SelectorString = ModelItem.GetValue<string>("Selector");
            const int maxresults = 1;

            OpenRPA.Interfaces.Selector.SelectorWindow selectors;
            if (!string.IsNullOrEmpty(SelectorString))
            {
                var selector = new NMSelector(SelectorString);
                selectors = new OpenRPA.Interfaces.Selector.SelectorWindow("NM", selector, null, maxresults);
            }
            else
            {
                var selector = new NMSelector("[{Selector: 'NM'}]");
                selectors = new OpenRPA.Interfaces.Selector.SelectorWindow("NM", selector, null, maxresults);
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
    }
}

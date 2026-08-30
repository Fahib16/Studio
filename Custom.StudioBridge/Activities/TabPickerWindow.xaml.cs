using System.Collections.Generic;
using System.Windows;

namespace Custom.StudioBridge.Design
{
    public class TabPickerItem
    {
        public int Id { get; set; }
        public string Url { get; set; }
        public string Title { get; set; }
        public string Display { get; set; }
    }

    public partial class TabPickerWindow : Window
    {
        public TabPickerItem SelectedTab { get; private set; }

        public TabPickerWindow(List<TabPickerItem> tabs)
        {
            InitializeComponent();
            TabListBox.ItemsSource = tabs;
            if (tabs.Count > 0) TabListBox.SelectedIndex = 0;
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            if (TabListBox.SelectedItem is TabPickerItem item)
            {
                SelectedTab = item;
                DialogResult = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}

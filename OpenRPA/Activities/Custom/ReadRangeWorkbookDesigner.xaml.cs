using System;
using System.Activities;
using System.Activities.Expressions;
using System.Activities.Presentation.Model;
using System.Windows;

// PERBAIKAN 3: Menghapus ".Excel" dari namespace
namespace OpenRPA.Activities
{
    public partial class ReadRangeWorkbookDesigner
    {
        public ReadRangeWorkbookDesigner()
        {
            InitializeComponent();
        }

        private void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih File Excel",
                Filter = "Excel Files (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|All Files (*.*)|*.*",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedFile = $"\"{openFileDialog.FileName}\"";
                ModelItem.Properties["WorkbookPath"].SetValue(new InArgument<string>
                {
                    Expression = new Microsoft.VisualBasic.Activities.VisualBasicValue<string>(selectedFile)
                });
            }
        }
    }
}
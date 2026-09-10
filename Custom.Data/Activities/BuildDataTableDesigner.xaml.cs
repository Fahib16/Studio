using System.Windows;

namespace Custom.Data.Design
{
    /// <summary>
    /// Kartu canvas Build Data Table: definisi kolom bisa diketik langsung
    /// sebagai teks, atau disunting lewat dialog kecil. Dialog hanya alat
    /// bantu — nilainya tetap satu string yang sama.
    /// </summary>
    public partial class BuildDataTableDesigner
    {
        public BuildDataTableDesigner()
        {
            InitializeComponent();
        }

        private void EditColumns_Click(object sender, RoutedEventArgs e)
        {
            var current = ModelItem.Properties["Columns"].ComputedValue as string;

            var window = new ColumnEditorWindow(current)
            {
                Owner = Window.GetWindow(this)
            };

            if (window.ShowDialog() != true) return;

            ModelItem.Properties["Columns"].SetValue(window.Result);
        }
    }
}

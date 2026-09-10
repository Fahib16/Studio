using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Custom.Data.Design
{
    /// <summary>
    /// Satu baris di editor kolom.
    /// </summary>
    public class ColumnRow
    {
        public string Name { get; set; }
        public string Type { get; set; }
    }

    /// <summary>
    /// Editor kolom sederhana untuk Build Data Table.
    ///
    /// Dibangun dari kode, bukan XAML, mengikuti pola DesktopPickerWindow di
    /// Custom.StudioBridge — dialog sekecil ini tidak memerlukan berkas XAML
    /// terpisah, dan dengan begitu tidak ada Page baru yang perlu didaftarkan
    /// di csproj.
    ///
    /// Hasilnya dikembalikan sebagai string "Nama:Tipe; Nama:Tipe" yang sama
    /// persis dengan yang bisa diketik langsung di kartu — editor ini hanya
    /// alat bantu, bukan satu-satunya jalan mengisi.
    /// </summary>
    public class ColumnEditorWindow : Window
    {
        private readonly ObservableCollection<ColumnRow> rows = new ObservableCollection<ColumnRow>();
        private readonly DataGrid grid;
        private readonly TextBlock error;

        public string Result { get; private set; }

        public ColumnEditorWindow(string definitions)
        {
            Title = "Kolom Data Table";
            Width = 460;
            Height = 380;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var root = new DockPanel { Margin = new Thickness(10) };

            var hint = new TextBlock
            {
                Text = "Isi nama kolom dan tipenya. Tipe kosong berarti String.",
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = System.Windows.Media.Brushes.DimGray
            };
            DockPanel.SetDock(hint, Dock.Top);
            root.Children.Add(hint);

            error = new TextBlock
            {
                Foreground = System.Windows.Media.Brushes.Firebrick,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0),
                Visibility = Visibility.Collapsed
            };
            DockPanel.SetDock(error, Dock.Bottom);
            root.Children.Add(error);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var ok = new Button { Content = "OK", Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
            ok.Click += OnOk;

            var cancel = new Button { Content = "Batal", Width = 80, IsCancel = true };

            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(buttons);

            var types = new List<string> { "String", "Int32", "Int64", "Double", "Decimal", "Boolean", "DateTime", "Object" };

            grid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = true,
                CanUserDeleteRows = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                ItemsSource = rows
            };

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Nama",
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                Binding = new System.Windows.Data.Binding("Name") { Mode = System.Windows.Data.BindingMode.TwoWay }
            });

            grid.Columns.Add(new DataGridComboBoxColumn
            {
                Header = "Tipe",
                Width = 130,
                ItemsSource = types,
                SelectedItemBinding = new System.Windows.Data.Binding("Type") { Mode = System.Windows.Data.BindingMode.TwoWay }
            });

            root.Children.Add(grid);
            Content = root;

            Load(definitions);
        }

        private void Load(string definitions)
        {
            if (string.IsNullOrWhiteSpace(definitions)) return;

            foreach (var raw in definitions.Split(new[] { ';', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var part = raw.Trim();
                if (part.Length == 0) continue;

                var colon = part.IndexOf(':');
                var name = (colon < 0 ? part : part.Substring(0, colon)).Trim();
                var type = colon < 0 ? "String" : part.Substring(colon + 1).Trim();

                string normalized;
                try { normalized = BuildDataTable.TypeName(BuildDataTable.ResolveType(type)); }
                catch (Exception) { normalized = "String"; }

                rows.Add(new ColumnRow { Name = name, Type = normalized });
            }
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            grid.CommitEdit(DataGridEditingUnit.Row, true);

            var valid = rows.Where(r => !string.IsNullOrWhiteSpace(r.Name)).ToList();
            var text = string.Join("; ", valid.Select(r =>
                r.Name.Trim() + ":" + (string.IsNullOrWhiteSpace(r.Type) ? "String" : r.Type)));

            // Divalidasi memakai fungsi yang SAMA dengan yang dipakai saat
            // runtime, supaya tidak ada definisi yang lolos di sini tapi gagal
            // saat workflow dijalankan.
            try
            {
                BuildDataTable.Build(text, null);
            }
            catch (Exception ex)
            {
                error.Text = ex.Message;
                error.Visibility = Visibility.Visible;
                return;
            }

            Result = text;
            DialogResult = true;
            Close();
        }
    }
}

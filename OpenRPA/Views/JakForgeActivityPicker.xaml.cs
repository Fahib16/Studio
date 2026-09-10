using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using OpenRPA.Interfaces;

namespace OpenRPA.Views
{
    /// <summary>
    /// Kotak cari activity untuk tombol "+" di kanvas.
    ///
    /// Daftarnya diambil dari toolbox (WFToolbox.AllTools), bukan dari
    /// pemindaian sendiri, supaya isinya tidak mungkin berbeda dengan panel
    /// Aktivitas — termasuk penyaringan activity bawaan yang sengaja
    /// disembunyikan.
    /// </summary>
    public partial class JakForgeActivityPicker : Window
    {
        /// <summary>Satu baris hasil pencarian.</summary>
        public class Entry
        {
            public string Name { get; set; }
            public string Category { get; set; }
            public Type Type { get; set; }
            public BitmapSource Icon { get; set; }
        }

        private List<Entry> all = new List<Entry>();

        private JakForgeActivityPicker()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                Fill();
                SearchBox.Focus();
            };
        }

        /// <summary>
        /// Menampilkan kotak cari dan mengembalikan tipe activity yang dipilih,
        /// atau null kalau dibatalkan.
        /// </summary>
        public static Type Pick(Window owner)
        {
            var picker = new JakForgeActivityPicker();
            if (owner != null && owner.IsLoaded) picker.Owner = owner;
            var ok = picker.ShowDialog();
            return ok == true ? picker.Selected : null;
        }

        public Type Selected { get; private set; }

        private void Fill()
        {
            try
            {
                all = WFToolbox.AllTools()
                    .Select(t => new Entry
                    {
                        Name = t.Item2,
                        Category = t.Item1,
                        Type = t.Item3,
                        Icon = IconOf(t.Item3)
                    })
                    .OrderBy(t => t.Name)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error("JakForge picker: " + ex.ToString());
                all = new List<Entry>();
            }

            Apply("");
        }

        private void Apply(string filter)
        {
            IEnumerable<Entry> rows = all;

            if (!string.IsNullOrWhiteSpace(filter))
            {
                var needle = filter.Trim();
                rows = all.Where(r =>
                    r.Name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    r.Category.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            Results.ItemsSource = rows.ToList();
            if (Results.Items.Count > 0) Results.SelectedIndex = 0;
        }

        /// <summary>
        /// Ikon toolbox activity, diubah menjadi gambar WPF. Kalau activity itu
        /// tidak punya ikon, kolomnya dibiarkan kosong — lebih baik daripada
        /// memasang ikon asal yang menyesatkan.
        /// </summary>
        private static BitmapSource IconOf(Type type)
        {
            try
            {
                var attribute = System.ComponentModel.TypeDescriptor.GetAttributes(type)[typeof(System.Drawing.ToolboxBitmapAttribute)]
                                as System.Drawing.ToolboxBitmapAttribute;
                if (attribute == null) return null;

                using (var image = attribute.GetImage(type) as System.Drawing.Bitmap)
                {
                    if (image == null) return null;

                    var handle = image.GetHbitmap();
                    try
                    {
                        var source = Imaging.CreateBitmapSourceFromHBitmap(
                            handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        source.Freeze();
                        return source;
                    }
                    finally
                    {
                        NativeMethods.DeleteObject(handle);
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            Apply(SearchBox.Text);
        }

        private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Down:
                    if (Results.Items.Count > 0)
                    {
                        Results.SelectedIndex = Math.Min(Results.SelectedIndex + 1, Results.Items.Count - 1);
                        Results.ScrollIntoView(Results.SelectedItem);
                    }
                    e.Handled = true;
                    break;

                case Key.Up:
                    if (Results.Items.Count > 0)
                    {
                        Results.SelectedIndex = Math.Max(Results.SelectedIndex - 1, 0);
                        Results.ScrollIntoView(Results.SelectedItem);
                    }
                    e.Handled = true;
                    break;

                case Key.Enter:
                    Accept();
                    e.Handled = true;
                    break;

                case Key.Escape:
                    DialogResult = false;
                    e.Handled = true;
                    break;
            }
        }

        private void Results_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { Accept(); e.Handled = true; }
            if (e.Key == Key.Escape) { DialogResult = false; e.Handled = true; }
        }

        private void Results_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Accept();
        }

        private void Accept()
        {
            var entry = Results.SelectedItem as Entry;
            if (entry == null) return;

            Selected = entry.Type;
            DialogResult = true;
        }

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("gdi32.dll")]
            public static extern bool DeleteObject(IntPtr hObject);
        }
    }
}

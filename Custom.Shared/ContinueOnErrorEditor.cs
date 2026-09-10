using System;
using System.Activities;
using System.Activities.Expressions;
using System.Activities.Presentation.PropertyEditing;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.VisualBasic.Activities;

namespace Custom.Shared
{
    /// <summary>
    /// Kotak centang tiga keadaan untuk properti bertipe InArgument&lt;bool&gt;.
    ///
    /// Urutan yang dipakai SENGAJA berbeda dari bawaan WPF (false, true, kosong).
    /// Di sini: kosong, True, False, kembali kosong. Alasannya, keadaan awal
    /// properti seperti Continue On Error memang kosong, jadi klik pertama
    /// sebaiknya langsung berarti "ya".
    /// </summary>
    public class TriStateBox : CheckBox
    {
        protected override void OnToggle()
        {
            if (IsChecked == null) IsChecked = true;
            else if (IsChecked == true) IsChecked = false;
            else IsChecked = null;
        }
    }

    /// <summary>
    /// Alat bantu bersama untuk membaca dan menulis InArgument&lt;bool&gt;.
    ///
    /// Versi sebelumnya menempel pada <c>StringValue</c> milik panel Properties.
    /// Itu sumber bug yang terlihat di layar: begitu properti berisi sebuah
    /// InArgument yang Expression-nya kosong, <c>StringValue</c> jatuh ke
    /// <c>ToString()</c> dan yang tampil justru
    /// <c>System.Activities.InArgument`1[System.Boolean]</c> — bukan True,
    /// bukan False, dan bukan kosong.
    ///
    /// Sekarang keduanya membaca dan menulis <c>Value</c>, yaitu InArgument-nya
    /// sendiri, jadi tidak ada lagi bentuk perantara yang bisa bocor ke layar.
    /// </summary>
    internal static class BoolArgument
    {
        /// <summary>InArgument -> true / false / null (kosong atau ekspresi lain).</summary>
        public static bool? ToBool(object value)
        {
            var text = ToText(value);
            if (text.Equals("True", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Equals("False", StringComparison.OrdinalIgnoreCase)) return false;
            return null;
        }

        /// <summary>InArgument -> teks ekspresinya, "" kalau memang kosong.</summary>
        public static string ToText(object value)
        {
            if (value == null) return "";

            var arg = value as InArgument<bool>;
            if (arg == null)
            {
                // Bisa juga sudah berupa bool atau teks, mis. saat properti
                // baru saja diisi dari kode.
                if (value is bool) return ((bool)value) ? "True" : "False";
                var raw = value.ToString();
                return raw != null && raw.StartsWith("System.Activities.InArgument") ? "" : (raw ?? "").Trim();
            }

            var literal = arg.Expression as Literal<bool>;
            if (literal != null) return literal.Value ? "True" : "False";

            var vb = arg.Expression as VisualBasicValue<bool>;
            if (vb != null) return (vb.ExpressionText ?? "").Trim();

            return arg.Expression == null ? "" : (arg.Expression.ToString() ?? "").Trim();
        }

        /// <summary>Teks -> InArgument, atau null kalau teksnya kosong.</summary>
        public static InArgument<bool> FromText(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return null;

            if (text.Equals("True", StringComparison.OrdinalIgnoreCase))
                return new InArgument<bool>(true);
            if (text.Equals("False", StringComparison.OrdinalIgnoreCase))
                return new InArgument<bool>(false);

            // Selain itu diperlakukan sebagai ekspresi Visual Basic, supaya
            // Continue On Error tetap boleh diisi mis. "cfg_lanjutTerus".
            return new InArgument<bool>(new VisualBasicValue<bool>(text));
        }
    }

    /// <summary>
    /// Kotak centang &lt;-&gt; InArgument&lt;bool&gt;.
    ///
    /// Kalau isinya ekspresi yang bukan True/False, kotak centang ditampilkan
    /// setengah tercentang dan tidak memaksakan apa pun; mengklik barulah
    /// menimpanya.
    /// </summary>
    public class BoolArgumentCheckConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return BoolArgument.ToBool(value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool) return ((bool)value) ? new InArgument<bool>(true) : new InArgument<bool>(false);
            return null;    // kosong
        }
    }

    /// <summary>Kotak teks &lt;-&gt; InArgument&lt;bool&gt;.</summary>
    public class BoolArgumentTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return BoolArgument.ToText(value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return BoolArgument.FromText(value as string);
        }
    }

    /// <summary>
    /// Editor panel Properties untuk Continue On Error: kotak centang di kiri,
    /// kotak teks di kanan. Keduanya menunjuk nilai yang sama, jadi mengetik
    /// ekspresi sendiri tetap bisa.
    ///
    /// Templatenya dibuat di kode, bukan di berkas XAML terpisah, supaya tidak
    /// ada pack URI baru yang bisa salah ketik dan baru ketahuan saat runtime.
    /// </summary>
    public class ContinueOnErrorEditor : PropertyValueEditor
    {
        public ContinueOnErrorEditor()
        {
            InlineEditorTemplate = Build();
        }

        private static DataTemplate Build()
        {
            // FrameworkElementFactory tidak bisa mengisi koleksi
            // ColumnDefinitions sebuah Grid, jadi tata letaknya memakai
            // DockPanel: kotak centang menempel di kiri, kotak teks mengisi
            // sisanya.
            var dock = new FrameworkElementFactory(typeof(DockPanel));
            dock.SetValue(DockPanel.LastChildFillProperty, true);

            var check = new FrameworkElementFactory(typeof(TriStateBox));
            check.SetValue(DockPanel.DockProperty, Dock.Left);
            check.SetValue(FrameworkElement.MarginProperty, new Thickness(2, 0, 6, 0));
            check.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            check.SetValue(FrameworkElement.ToolTipProperty,
                "Klik untuk berganti: kosong, True, False, kosong lagi.");
            check.SetBinding(CheckBox.IsCheckedProperty, new Binding("Value")
            {
                Mode = BindingMode.TwoWay,
                Converter = new BoolArgumentCheckConverter(),
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

            var box = new FrameworkElementFactory(typeof(TextBox));
            box.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            box.SetValue(Control.BorderThicknessProperty, new Thickness(1));
            box.SetBinding(TextBox.TextProperty, new Binding("Value")
            {
                Mode = BindingMode.TwoWay,
                Converter = new BoolArgumentTextConverter(),
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
            });

            dock.AppendChild(check);
            dock.AppendChild(box);

            var template = new DataTemplate { VisualTree = dock };
            template.Seal();
            return template;
        }
    }
}

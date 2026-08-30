using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Custom.StudioBridge.Design
{
    /// <summary>
    /// Converter WPF: ubah string data-URL Base64 ("data:image/png;base64,...")
    /// jadi BitmapImage yang bisa ditampilkan langsung di <Image Source="...">.
    /// Dipakai di ke-4 Designer (Click/SetText/GetText/Highlight) untuk
    /// nampilin "Informative Screenshot" hasil Indicate.
    /// </summary>
    public class Base64ToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var base64 = value as string;
            if (string.IsNullOrEmpty(base64)) return null;

            try
            {
                // Hapus prefix "data:image/png;base64," kalau ada -- kita
                // simpan data URL lengkap dari extension, tapi Convert.FromBase64String
                // cuma butuh bagian base64-nya saja.
                var commaIndex = base64.IndexOf(',');
                var pureBase64 = commaIndex >= 0 ? base64.Substring(commaIndex + 1) : base64;

                var bytes = System.Convert.FromBase64String(pureBase64);

                var bitmap = new BitmapImage();
                using (var stream = new MemoryStream(bytes))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad; // biar stream boleh di-dispose setelah ini
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }
                bitmap.Freeze(); // supaya aman dipakai lintas thread oleh WPF Designer

                return bitmap;
            }
            catch
            {
                return null; // data korup/tidak valid -- jangan sampai bikin Designer crash
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Converter WPF TERPISAH (beda tipe hasil dari Base64ToImageConverter
    /// di atas): string kosong/null -> Collapsed, ada isinya -> Visible.
    /// Dipakai buat sembunyikan area screenshot kalau ScreenshotBase64
    /// belum pernah di-isi (activity belum pernah di-Indicate).
    /// </summary>
    public class NullOrEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var str = value as string;
            return string.IsNullOrEmpty(str) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}

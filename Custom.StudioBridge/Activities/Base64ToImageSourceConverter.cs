using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Custom.StudioBridge.Design
{
    /// <summary>
    /// Konversi string base64 PNG (hasil screenshot elemen dari Indicate
    /// on screen) jadi ImageSource yang bisa ditampilkan lewat &lt;Image&gt;
    /// di Designer XAML. Return null kalau string kosong/gagal decode --
    /// Image dengan Source null otomatis render kosong (tidak error).
    /// </summary>
    public class Base64ToImageSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var base64 = value as string;
            if (string.IsNullOrEmpty(base64)) return null;

            try
            {
                var bytes = System.Convert.FromBase64String(base64);
                using (var stream = new MemoryStream(bytes))
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad; // biar stream bisa di-dispose setelah ini
                    image.StreamSource = stream;
                    image.EndInit();
                    image.Freeze(); // biar bisa dipakai lintas-thread dgn aman
                    return image;
                }
            }
            catch
            {
                return null; // base64 korup/tidak valid -- render kosong, jangan crash Designer
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}

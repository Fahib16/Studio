using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Custom.StudioBridge.Design
{
    /// <summary>
    /// Ubah string base64 PNG (property SelectorImage) jadi ImageSource untuk
    /// ditampilkan di kartu activity.
    ///
    /// CacheOption = OnLoad WAJIB: tanpa itu BitmapImage memegang MemoryStream
    /// yang sudah kita dispose, dan gambarnya jadi kosong/melempar exception
    /// saat WPF baru benar-benar membacanya (lazy loading).
    /// </summary>
    public class Base64ImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var base64 = value as string;
            if (string.IsNullOrEmpty(base64)) return null;

            try
            {
                var bytes = System.Convert.FromBase64String(StripPrefix(base64));

                using (var stream = new MemoryStream(bytes))
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream;
                    image.EndInit();
                    image.Freeze(); // supaya aman dipakai lintas thread
                    return image;
                }
            }
            catch (Exception)
            {
                // Data rusak -> tampilkan tanpa gambar, jangan bikin designer crash.
                return null;
            }
        }

        private static string StripPrefix(string s)
        {
            var idx = s.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
            return idx >= 0 ? s.Substring(idx + 7) : s;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}

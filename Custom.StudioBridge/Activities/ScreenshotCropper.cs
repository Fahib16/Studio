using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Custom.StudioBridge.Design
{
    /// <summary>
    /// Memotong screenshot SELURUH tab (hasil chrome.tabs.captureVisibleTab)
    /// jadi area elemen yang dipilih saja.
    ///
    /// KENAPA CROP DI SINI, BUKAN DI EXTENSION: service worker MV3 tidak punya
    /// DOM, jadi crop di sana harus lewat OffscreenCanvas + createImageBitmap
    /// yang lebih rewel dan sulit di-debug. Di C# cukup System.Drawing. Ongkosnya
    /// screenshot full-tab ikut lewat pipe (ratusan KB base64), yang masih
    /// jauh di bawah batas praktis Named Pipe kita.
    ///
    /// BUTUH REFERENCE: System.Drawing (assembly .NET Framework bawaan, tinggal
    /// Add Reference -> Assemblies -> Framework -> System.Drawing).
    /// </summary>
    internal static class ScreenshotCropper
    {
        /// <summary>Padding di sekeliling elemen, dalam pixel fisik.</summary>
        private const int PaddingPx = 6;

        /// <summary>
        /// Batas lebar hasil crop. Elemen selebar layar (mis. tabel besar)
        /// dikecilkan supaya kartu activity di canvas tidak jadi raksasa dan
        /// file .xaml workflow tidak membengkak.
        /// </summary>
        private const int MaxWidth = 320;

        /// <summary>Batas tinggi hasil crop, alasan sama.</summary>
        private const int MaxHeight = 120;

        /// <summary>
        /// Ambil data URL PNG (atau base64 polos) dari screenshot full tab,
        /// potong ke rect elemen, kembalikan base64 PNG polos (tanpa prefix
        /// "data:image/png;base64,").
        ///
        /// Rect datang dalam CSS pixel dari getBoundingClientRect(), sedangkan
        /// screenshot dalam pixel FISIK -- makanya semua koordinat dikali
        /// devicePixelRatio. Kalau langkah ini dilewat, di layar HiDPI (scaling
        /// 125%/150%, umum di Windows) hasil crop akan meleset ke kiri-atas.
        /// </summary>
        public static string CropToElement(
            string screenshotDataUrl,
            double rectLeft, double rectTop,
            double rectWidth, double rectHeight,
            double devicePixelRatio)
        {
            if (string.IsNullOrEmpty(screenshotDataUrl)) return null;

            var raw = StripDataUrlPrefix(screenshotDataUrl);
            var bytes = Convert.FromBase64String(raw);

            using (var input = new MemoryStream(bytes))
            using (var full = new Bitmap(input))
            {
                // Rect kosong / tidak dikirim extension -> pakai screenshot apa
                // adanya, dikecilkan. Lebih baik menampilkan sesuatu daripada
                // tidak sama sekali.
                if (rectWidth <= 0 || rectHeight <= 0)
                    return Encode(Downscale(full));

                var ratio = devicePixelRatio > 0 ? devicePixelRatio : 1.0;

                int x = (int)Math.Round(rectLeft * ratio) - PaddingPx;
                int y = (int)Math.Round(rectTop * ratio) - PaddingPx;
                int w = (int)Math.Round(rectWidth * ratio) + PaddingPx * 2;
                int h = (int)Math.Round(rectHeight * ratio) + PaddingPx * 2;

                // Jepit ke dalam batas gambar -- elemen di tepi layar bisa
                // menghasilkan rect yang sebagian keluar frame, dan Bitmap.Clone
                // melempar OutOfMemoryException (bukan ArgumentException, jadi
                // membingungkan) kalau rect-nya keluar batas.
                if (x < 0) { w += x; x = 0; }
                if (y < 0) { h += y; y = 0; }
                if (x + w > full.Width) w = full.Width - x;
                if (y + h > full.Height) h = full.Height - y;

                if (w <= 0 || h <= 0) return Encode(Downscale(full));

                using (var cropped = full.Clone(new Rectangle(x, y, w, h), full.PixelFormat))
                {
                    return Encode(Downscale(cropped));
                }
            }
        }

        private static string StripDataUrlPrefix(string s)
        {
            var idx = s.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
            return idx >= 0 ? s.Substring(idx + 7) : s;
        }

        /// <summary>
        /// Kecilkan proporsional kalau melebihi batas. Mengembalikan bitmap yang
        /// sama (bukan salinan) kalau sudah muat, jadi pemanggil TIDAK boleh
        /// men-dispose hasilnya secara terpisah -- karena itu Encode() dipanggil
        /// langsung di dalam using milik bitmap aslinya.
        /// </summary>
        private static Bitmap Downscale(Bitmap source)
        {
            if (source.Width <= MaxWidth && source.Height <= MaxHeight)
                return source;

            double scale = Math.Min((double)MaxWidth / source.Width,
                                    (double)MaxHeight / source.Height);

            int w = Math.Max(1, (int)(source.Width * scale));
            int h = Math.Max(1, (int)(source.Height * scale));

            var resized = new Bitmap(w, h);
            using (var g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(source, 0, 0, w, h);
            }
            return resized;
        }

        private static string Encode(Bitmap bitmap)
        {
            using (var output = new MemoryStream())
            {
                bitmap.Save(output, ImageFormat.Png);
                return Convert.ToBase64String(output.ToArray());
            }
        }
    }
}

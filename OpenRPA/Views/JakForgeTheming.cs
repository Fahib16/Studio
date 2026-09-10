using System;
using System.Linq;
using System.Windows;
using Custom.Shared;

namespace OpenRPA.Views
{
    /// <summary>
    /// Menerapkan tema terang/gelap ke Studio.
    ///
    /// Yang ditukar hanya SATU kamus: paletnya. Gaya-gaya di Themes/JakForge.xaml
    /// merujuk kuas lewat DynamicResource, jadi menukar palet cukup untuk
    /// mengubah tampilan tanpa menjalankan ulang Studio.
    ///
    /// BATASNYA perlu diketahui: yang ikut berubah adalah permukaan JakForge —
    /// layar mulai, daftar proyek, panel Output, kanvas, dan kartu activity.
    /// Jendela bawaan OpenRPA tidak ikut, karena tema ini sengaja tidak memuat
    /// gaya implisit; memaksakannya akan mengubah tampilan kontrol yang sudah
    /// bekerja, dan itu risiko yang tidak sebanding dengan warnanya.
    /// </summary>
    public static class JakForgeTheming
    {
        private const string Terang = "/OpenRPA;component/Themes/JakForgePalette.Light.xaml";
        private const string Gelap = "/OpenRPA;component/Themes/JakForgePalette.Dark.xaml";

        public static void Apply()
        {
            Apply(JakForgeUi.Theme);
        }

        public static void Apply(UiTheme theme)
        {
            var app = Application.Current;
            if (app == null) return;

            var diminta = theme == UiTheme.Dark ? Gelap : Terang;
            var kamus = app.Resources.MergedDictionaries;

            var lama = kamus.FirstOrDefault(d =>
                d.Source != null &&
                d.Source.OriginalString.IndexOf("JakForgePalette.", StringComparison.OrdinalIgnoreCase) >= 0);

            // Sudah benar: jangan ditukar. Menukar kamus dengan isi yang sama
            // tetap memaksa seluruh jendela menghitung ulang gayanya.
            if (lama != null && lama.Source.OriginalString.Equals(diminta, StringComparison.OrdinalIgnoreCase))
                return;

            var baru = new ResourceDictionary { Source = new Uri(diminta, UriKind.Relative) };

            if (lama == null) kamus.Insert(0, baru);
            else kamus[kamus.IndexOf(lama)] = baru;
        }
    }
}

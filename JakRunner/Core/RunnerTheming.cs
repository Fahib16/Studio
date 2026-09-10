using System;
using System.Linq;
using System.Windows;
using Custom.Shared;

namespace JakRunner.Core
{
    /// <summary>
    /// Menerapkan tema terang/gelap ke seluruh jendela JakRunner.
    ///
    /// JakRunner TIDAK punya pilihan temanya sendiri: ia mengikuti setelan
    /// Studio, sesuai permintaan. Yang dikerjakan di sini hanya membaca setelan
    /// bersama itu dan menukar paletnya.
    ///
    /// Yang ditukar hanya SATU kamus — paletnya. Gaya-gayanya merujuk kuas
    /// lewat DynamicResource, jadi menukar palet cukup untuk mengubah seluruh
    /// tampilan tanpa menjalankan ulang program.
    /// </summary>
    public static class RunnerTheming
    {
        private const string Terang = "/JakRunner;component/Views/RunnerPalette.Light.xaml";
        private const string Gelap = "/JakRunner;component/Views/RunnerPalette.Dark.xaml";

        /// <summary>Terapkan tema yang tertulis di setelan bersama.</summary>
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
                d.Source.OriginalString.IndexOf("RunnerPalette.", StringComparison.OrdinalIgnoreCase) >= 0);

            // Sudah benar: jangan ditukar. Menukar kamus dengan isi yang sama
            // tetap memaksa seluruh jendela menghitung ulang gayanya.
            if (lama != null && lama.Source.OriginalString.Equals(diminta, StringComparison.OrdinalIgnoreCase))
                return;

            var baru = new ResourceDictionary { Source = new Uri(diminta, UriKind.Relative) };

            if (lama == null)
            {
                // Paletnya harus berada SEBELUM RunnerTheme, karena gaya di
                // sana merujuk kuas-kuas ini.
                kamus.Insert(0, baru);
            }
            else
            {
                kamus[kamus.IndexOf(lama)] = baru;
            }
        }
    }
}

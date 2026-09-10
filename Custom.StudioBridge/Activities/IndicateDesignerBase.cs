using System;
using System.Activities;
using System.Activities.Expressions;
using System.Windows;

namespace Custom.StudioBridge.Design
{
    /// <summary>
    /// Kelas dasar SEMUA kartu activity yang punya Selector — di project mana
    /// pun (StudioBridge, Browser, Data, Window).
    ///
    /// Isinya satu hal: tombol "Indicate on screen" dan "Edit selector" yang
    /// memanggil IndicateHelper. Sebelumnya logika ini disalin ke setiap
    /// code-behind designer (enam salinan), dan tiap salinan berisiko
    /// menyimpang — mis. lupa aturan "nama yang sudah diubah user tidak boleh
    /// ditimpa". Sekarang satu tempat.
    ///
    /// Turunan dari Custom.Shared.PathDesignerBase supaya satu kartu bisa
    /// punya Indicate DAN tombol pemilih berkas sekaligus (mis. Take
    /// Screenshot yang menyimpan hasil ke berkas).
    ///
    /// Kartu turunan cukup memberi tahu satu hal: <see cref="ActivityLabel"/>,
    /// yaitu awalan nama yang dipakai saat activity dinamai otomatis dari
    /// elemen yang dipilih.
    /// </summary>
    public class IndicateDesignerBase : Custom.Shared.PathDesignerBase
    {
        /// <summary>
        /// Awalan nama activity setelah Indicate, mis. "Click" menjadi
        /// "Click 'Simpan'". Default: nama tipe activity-nya.
        /// </summary>
        protected virtual string ActivityLabel
        {
            get { return ModelItem != null ? ModelItem.ItemType.Name : ""; }
        }

        /// <summary>
        /// Nama properti selector di activity-nya. Hampir selalu "Selector";
        /// bisa ditimpa kalau suatu activity memakai nama lain.
        /// </summary>
        protected virtual string SelectorProperty
        {
            get { return "Selector"; }
        }

        protected void Indicate_Click(object sender, RoutedEventArgs e)
        {
            var result = IndicateHelper.Run();
            if (result == null) return;   // dibatalkan di langkah mana pun

            ApplySelector(result.Selector, result.ScreenshotBase64);
        }

        protected void EditSelector_Click(object sender, RoutedEventArgs e)
        {
            var result = IndicateHelper.EditSelector(ReadCurrentSelector());
            if (result == null) return;

            ApplySelector(result.Selector, result.ScreenshotBase64);
        }

        private void ApplySelector(string selector, string screenshot)
        {
            ModelItem.Properties[SelectorProperty].SetValue(
                new InArgument<string>() { Expression = new Literal<string>(selector) });

            // Screenshot HANYA ditimpa kalau user benar-benar menekan Indicate
            // (di dalam Explorer sekalipun). Menyunting selector saja tidak
            // menghapus konteks visual yang masih berguna.
            if (!string.IsNullOrEmpty(screenshot) && HasProperty("ScreenshotBase64"))
                ModelItem.Properties["ScreenshotBase64"].SetValue(screenshot);

            ApplyDisplayName(selector);
        }

        /// <summary>
        /// Namai ulang activity sesuai elemen yang dipilih, seperti UiPath.
        ///
        /// Nama yang sudah DIUBAH SENDIRI oleh user tidak ditimpa: begitu
        /// seseorang menamai activity secara sengaja, nama itu lebih berarti
        /// daripada apa pun yang bisa kita simpulkan dari selector.
        /// </summary>
        private void ApplyDisplayName(string selector)
        {
            var label = ActivityLabel;
            if (string.IsNullOrEmpty(label)) return;

            var current = ModelItem.Properties["DisplayName"].ComputedValue as string;

            var isDefault = string.IsNullOrEmpty(current)
                            || current == label
                            || current == ModelItem.ItemType.Name
                            || current.StartsWith(label + " '", StringComparison.Ordinal);

            if (!isDefault) return;

            var name = SelectorLabel.Build(label, selector);
            if (!string.IsNullOrEmpty(name)) ModelItem.Properties["DisplayName"].SetValue(name);
        }

        /// <summary>
        /// Baca selector saat ini sebagai teks biasa.
        ///
        /// Hanya Literal yang bisa dibaca isinya: kalau Selector diisi ekspresi
        /// VB (mis. gabungan variabel), nilainya baru ada saat runtime, jadi
        /// tidak ada yang bisa dikirim ke Explorer. Dalam kasus itu Explorer
        /// dibuka dengan kotak kosong, bukan dengan teks ekspresi yang akan
        /// salah dipakai sebagai selector.
        /// </summary>
        private string ReadCurrentSelector()
        {
            var arg = ModelItem.Properties[SelectorProperty].ComputedValue as InArgument<string>;
            var literal = arg?.Expression as Literal<string>;
            return literal?.Value;
        }

        private bool HasProperty(string name)
        {
            foreach (var p in ModelItem.Properties)
            {
                if (p.Name == name) return true;
            }
            return false;
        }
    }
}

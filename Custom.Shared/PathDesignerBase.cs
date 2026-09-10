using System;
using System.Activities;
using System.Activities.Presentation;
using System.Linq;
using System.Windows;

namespace Custom.Shared
{
    /// <summary>
    /// Kelas dasar kartu activity yang punya properti path.
    ///
    /// Menyediakan satu handler tombol browse untuk semua kasus, supaya
    /// keenam project Custom.* tidak masing-masing menyalin kode dialog dan
    /// aturan penulisan nilainya.
    ///
    /// Tombolnya cukup menyebut apa yang dia inginkan lewat Tag:
    ///
    ///     Tag="NamaProperty|mode|Nama Filter (*.xlsx)|*.xlsx"
    ///
    /// mode:
    ///   file    pilih satu berkas yang sudah ada
    ///   files   pilih beberapa berkas (ditulis sebagai array VB)
    ///   save    tentukan berkas tujuan (boleh yang belum ada)
    ///   folder  pilih folder
    ///
    /// Filter boleh dikosongkan; kalau kosong dipakai "Semua berkas".
    ///
    /// Nilainya ditulis sebagai EKSPRESI VB berkutip ("C:\data\a.xlsx"),
    /// bukan Literal. Alasannya sama seperti nilai default di activity lain:
    /// Literal tersimpan benar tapi tidak bisa ditampilkan ExpressionTextBox,
    /// jadi kotaknya akan terlihat kosong padahal sudah berisi path — persis
    /// kebingungan yang tombol ini seharusnya hilangkan.
    /// </summary>
    public class PathDesignerBase : ActivityDesigner
    {
        protected void Browse_Click(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null || ModelItem == null) return;

            var spec = element.Tag as string;
            if (string.IsNullOrWhiteSpace(spec)) return;

            var parts = spec.Split('|');
            var property = parts[0];
            var mode = parts.Length > 1 ? parts[1].Trim().ToLowerInvariant() : "file";

            // Sisa Tag setelah mode adalah rantai filter apa adanya
            // ("Nama|*.pola|Nama lain|*.pola2"). Ditulis terpisah di Tag karena
            // bentuk gabungannya memakai karakter yang sama dengan pemisah Tag,
            // dan disatukan kembali di sini.
            var filter = parts.Length > 3 ? string.Join("|", parts.Skip(2)) : null;

            if (!ModelItem.Properties.Any(p => p.Name == property)) return;

            var current = ReadCurrentExpression(property);
            var title = TitleFor(mode, property);

            if (mode == "files")
            {
                var files = PathPicker.OpenFiles(title, filter, current);
                if (files == null || files.Length == 0) return;

                SetExpression(property, PathPicker.ToVbArrayLiteral(files), typeof(string[]));
                return;
            }

            string picked;
            switch (mode)
            {
                case "save": picked = PathPicker.SaveFile(title, filter, current); break;
                case "folder": picked = PathPicker.Folder(title, current); break;
                default: picked = PathPicker.OpenFile(title, filter, current); break;
            }

            if (picked == null) return;   // dibatalkan

            SetExpression(property, PathPicker.ToVbLiteral(picked), typeof(string));
        }

        private static string TitleFor(string mode, string property)
        {
            switch (mode)
            {
                case "folder": return "Pilih folder untuk " + property;
                case "save": return "Tentukan berkas untuk " + property;
                case "files": return "Pilih berkas untuk " + property;
                default: return "Pilih berkas untuk " + property;
            }
        }

        /// <summary>
        /// Isi kotak ekspresi saat ini sebagai teks, kalau memang berupa
        /// ekspresi teks. Dipakai untuk membuka dialog di folder yang sedang
        /// dipakai user, bukan selalu di folder bawaan.
        /// </summary>
        private string ReadCurrentExpression(string property)
        {
            try
            {
                var value = ModelItem.Properties[property].ComputedValue;

                var stringArg = value as InArgument<string>;
                if (stringArg != null)
                {
                    var vb = stringArg.Expression as Microsoft.VisualBasic.Activities.VisualBasicValue<string>;
                    if (vb != null) return vb.ExpressionText;

                    var literal = stringArg.Expression as System.Activities.Expressions.Literal<string>;
                    if (literal != null) return literal.Value;
                }

                return value as string;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void SetExpression(string property, string vbExpression, Type valueType)
        {
            if (valueType == typeof(string[]))
            {
                ModelItem.Properties[property].SetValue(
                    new InArgument<string[]>(
                        new Microsoft.VisualBasic.Activities.VisualBasicValue<string[]>(vbExpression)));
                return;
            }

            // Sebagian properti path berupa string biasa (plain property),
            // bukan InArgument — mis. yang hanya dipakai saat mendesain.
            var current = ModelItem.Properties[property].ComputedValue;
            if (current is string || ModelItem.Properties[property].PropertyType == typeof(string))
            {
                ModelItem.Properties[property].SetValue(PathPicker.FromVbLiteral(vbExpression));
                return;
            }

            ModelItem.Properties[property].SetValue(
                new InArgument<string>(
                    new Microsoft.VisualBasic.Activities.VisualBasicValue<string>(vbExpression)));
        }
    }
}

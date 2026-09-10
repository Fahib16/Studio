using System.Collections.Generic;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace OpenRPA.CodeEditor
{
    /// <summary>
    /// Kurung dan kutip yang menutup sendiri di kotak ekspresi activity.
    ///
    /// Mengetik <c>(</c> langsung menghasilkan <c>()</c> dengan kursor di
    /// tengahnya; begitu juga <c>[</c>, <c>{</c>, <c>"</c>, dan <c>'</c>.
    /// Kalau ada teks tersorot, teks itu DIBUNGKUS alih-alih ditimpa.
    ///
    /// Versi sebelumnya memasang class handler untuk TextBox dan tidak pernah
    /// bekerja: kanvas menerbitkan <see cref="EditorService"/> sebagai
    /// IExpressionEditorService, jadi kotak ekspresi di kartu activity adalah
    /// AvalonEdit, bukan TextBox. Karena itu logikanya sekarang di sini.
    ///
    /// Dipisahkan dari CodeEditor dengan sengaja: seluruh keputusannya hanya
    /// bergantung pada dokumen dan posisi kursor, jadi ia bisa diuji tanpa
    /// menghidupkan editor lengkap beserta Roslyn dan kamus sumber dayanya.
    /// </summary>
    public static class AutoPair
    {
        private static readonly Dictionary<char, char> Opening = new Dictionary<char, char>
        {
            { '(', ')' }, { '[', ']' }, { '{', '}' }, { '"', '"' }, { '\'', '\'' },
        };

        private static readonly HashSet<char> Closing = new HashSet<char> { ')', ']', '}', '"', '\'' };

        public static void Attach(TextArea area)
        {
            if (area == null) return;

            area.TextEntering += (sender, e) =>
            {
                if (e.Text == null || e.Text.Length != 1) return;
                if (HandleTyped(area, e.Text[0])) e.Handled = true;
            };

            area.PreviewKeyDown += (sender, e) =>
            {
                if (e.Key != Key.Back) return;
                if (HandleBackspace(area)) e.Handled = true;
            };
        }

        /// <summary>
        /// Kerjakan satu karakter yang diketik. Mengembalikan true kalau
        /// karakternya sudah ditangani dan editor tidak perlu menyisipkannya
        /// lagi.
        /// </summary>
        public static bool HandleTyped(TextArea area, char typed)
        {
            var doc = area != null ? area.Document : null;
            if (doc == null) return false;

            var selection = area.Selection;
            var offset = area.Caret.Offset;

            // Hormati bagian yang dikunci editor. AvalonEdit menyatakannya lewat
            // ReadOnlySectionProvider, bukan satu bendera untuk seluruh kotak.
            if (!area.ReadOnlySectionProvider.CanInsert(offset)) return false;

            // Mengetik penutup tepat di depan penutup yang sama: lewati saja,
            // jangan menumpuk. Inilah yang membuat mengetik "abc" terasa wajar --
            // kutip penutupnya sudah ada sejak kutip pembuka diketik.
            if (Closing.Contains(typed) && selection.IsEmpty)
            {
                if (offset < doc.TextLength && doc.GetCharAt(offset) == typed)
                {
                    area.Caret.Offset = offset + 1;
                    return true;
                }
            }

            char close;
            if (!Opening.TryGetValue(typed, out close)) return false;

            // Ada teks tersorot: dibungkus, bukan ditimpa.
            if (!selection.IsEmpty)
            {
                var segment = selection.SurroundingSegment;
                var start = segment.Offset;
                var length = segment.Length;

                doc.Replace(start, length, typed + doc.GetText(start, length) + close);
                area.Selection = Selection.Create(area, start + 1, start + 1 + length);
                return true;
            }

            // Kutip di TENGAH kata tidak dipasangkan: mengetik apostrof pada
            // "user's" tidak boleh berubah menjadi "user''s".
            if ((typed == '"' || typed == '\'') && TouchingWord(doc, offset)) return false;

            doc.Insert(offset, typed.ToString() + close);
            area.Caret.Offset = offset + 1;
            return true;
        }

        /// <summary>
        /// Backspace di antara pasangan KOSONG menghapus keduanya, supaya tidak
        /// meninggalkan penutup yang menggantung sendirian.
        /// </summary>
        public static bool HandleBackspace(TextArea area)
        {
            var doc = area != null ? area.Document : null;
            if (doc == null) return false;
            if (!area.Selection.IsEmpty) return false;

            var offset = area.Caret.Offset;
            if (offset <= 0 || offset >= doc.TextLength) return false;

            char harusnya;
            if (!Opening.TryGetValue(doc.GetCharAt(offset - 1), out harusnya)) return false;
            if (doc.GetCharAt(offset) != harusnya) return false;

            doc.Remove(offset - 1, 2);
            return true;
        }

        /// <summary>Huruf, angka, atau garis bawah tepat sebelum/sesudah kursor.</summary>
        private static bool TouchingWord(TextDocument doc, int offset)
        {
            if (offset > 0)
            {
                var sebelum = doc.GetCharAt(offset - 1);
                if (char.IsLetterOrDigit(sebelum) || sebelum == '_') return true;
            }

            if (offset < doc.TextLength)
            {
                var sesudah = doc.GetCharAt(offset);
                if (char.IsLetterOrDigit(sesudah) || sesudah == '_') return true;
            }

            return false;
        }
    }
}

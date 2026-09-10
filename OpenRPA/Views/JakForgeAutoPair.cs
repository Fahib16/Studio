using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OpenRPA.Views
{
    /// <summary>
    /// Kurung dan kutip yang menutup sendiri untuk kotak teks BIASA di kartu
    /// activity — misalnya properti bertipe string di designer buatan sendiri.
    ///
    /// PENTING: kotak EKSPRESI di kanvas bukan TextBox. Kanvas menerbitkan
    /// CodeEditor.EditorService sebagai IExpressionEditorService, jadi kotak
    /// ekspresinya adalah AvalonEdit dan sama sekali tidak tersentuh class
    /// handler di sini. Itulah sebabnya pemasangan ini dulu terlihat "tidak
    /// berfungsi": yang paling sering dipakai orang justru satu-satunya yang
    /// tidak terlayani.
    ///
    /// Pasangannya untuk AvalonEdit ada di OpenRPA.CodeEditor.AutoPair, dan
    /// perilakunya sengaja dibuat sama persis. Kalau salah satunya diubah,
    /// yang lain harus ikut.
    /// </summary>
    public static class JakForgeAutoPair
    {
        private static readonly Dictionary<char, char> Opening = new Dictionary<char, char>
        {
            { '(', ')' },
            { '[', ']' },
            { '{', '}' },
            { '"', '"' },
            { '\'', '\'' },
        };

        private static readonly HashSet<char> Closing = new HashSet<char> { ')', ']', '}', '"', '\'' };

        private static bool installed;

        public static void Install()
        {
            if (installed) return;
            installed = true;

            EventManager.RegisterClassHandler(typeof(TextBox), System.Windows.UIElement.PreviewTextInputEvent,
                new TextCompositionEventHandler(OnTextInput), true);
            EventManager.RegisterClassHandler(typeof(TextBox), System.Windows.UIElement.PreviewKeyDownEvent,
                new KeyEventHandler(OnKeyDown), true);
        }

        private static void OnTextInput(object sender, TextCompositionEventArgs e)
        {
            var box = sender as TextBox;
            if (box == null || box.IsReadOnly) return;
            if (e.Text == null || e.Text.Length != 1) return;
            if (!InsideWorkflowCanvas(box)) return;

            var typed = e.Text[0];

            // Mengetik penutup tepat di depan penutup yang sama: lewati saja,
            // jangan menumpuk. Ini yang membuat mengetik "abc" terasa wajar —
            // kutip penutupnya sudah ada, tinggal dilewati.
            if (Closing.Contains(typed) && box.SelectionLength == 0)
            {
                var caret = box.CaretIndex;
                if (caret < box.Text.Length && box.Text[caret] == typed)
                {
                    box.CaretIndex = caret + 1;
                    e.Handled = true;
                    return;
                }
            }

            char close;
            if (!Opening.TryGetValue(typed, out close)) return;

            // Kutip di TENGAH kata tidak dipasangkan: mengetik apostrof pada
            // "user's" tidak boleh berubah jadi "user''s".
            if ((typed == '"' || typed == '\'') && box.SelectionLength == 0 && TouchingWord(box)) return;

            var start = box.SelectionStart;
            var length = box.SelectionLength;
            var selected = length > 0 ? box.SelectedText : "";

            box.SelectedText = typed + selected + close;

            // Tanpa sorotan: kursor di antara pasangan.
            // Dengan sorotan: teksnya tetap tersorot di dalam pasangan.
            if (length == 0)
            {
                box.CaretIndex = start + 1;
            }
            else
            {
                box.SelectionStart = start + 1;
                box.SelectionLength = length;
            }

            e.Handled = true;
        }

        private static void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Back) return;

            var box = sender as TextBox;
            if (box == null || box.IsReadOnly) return;
            if (box.SelectionLength != 0) return;
            if (!InsideWorkflowCanvas(box)) return;

            var caret = box.CaretIndex;
            if (caret <= 0 || caret >= box.Text.Length) return;

            var before = box.Text[caret - 1];
            var after = box.Text[caret];

            char expected;
            if (!Opening.TryGetValue(before, out expected)) return;
            if (after != expected) return;

            // Pasangan kosong dihapus sekaligus, supaya Backspace tidak
            // meninggalkan kurung penutup yang menggantung sendirian.
            box.Text = box.Text.Remove(caret - 1, 2);
            box.CaretIndex = caret - 1;
            e.Handled = true;
        }

        /// <summary>Huruf atau angka tepat sebelum/sesudah kursor.</summary>
        private static bool TouchingWord(TextBox box)
        {
            var caret = box.CaretIndex;
            var text = box.Text ?? "";

            if (caret > 0 && (char.IsLetterOrDigit(text[caret - 1]) || text[caret - 1] == '_')) return true;
            if (caret < text.Length && (char.IsLetterOrDigit(text[caret]) || text[caret] == '_')) return true;

            return false;
        }

        /// <summary>
        /// Benar hanya kalau kotak teks ini berada di dalam kartu activity di
        /// kanvas. Pengecekannya lewat pohon visual, bukan lewat nama kontrol,
        /// supaya berlaku untuk semua activity tanpa perlu didaftarkan satu per
        /// satu.
        /// </summary>
        private static bool InsideWorkflowCanvas(DependencyObject node)
        {
            var hops = 0;

            while (node != null && hops++ < 60)
            {
                if (node is System.Activities.Presentation.View.ExpressionTextBox) return true;
                if (node is System.Activities.Presentation.WorkflowViewElement) return true;
                if (node is System.Activities.Presentation.ActivityDesigner) return true;

                var parent = VisualTreeHelper.GetParent(node);
                if (parent == null)
                {
                    var element = node as FrameworkElement;
                    parent = element != null ? element.Parent : null;
                }
                node = parent;
            }

            return false;
        }
    }
}

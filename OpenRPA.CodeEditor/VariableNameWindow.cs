using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OpenRPA.CodeEditor
{
    /// <summary>
    /// Kotak kecil untuk menanyakan NAMA variabel saat Ctrl+K ditekan.
    ///
    /// Urutannya sengaja dibalik dari versi lama. Dulu nama harus diketik dulu
    /// ke dalam kotak ekspresi, baru Ctrl+K ditekan — dan kalau kotaknya kosong,
    /// yang terbentuk variabel tanpa nama. Sekarang Ctrl+K yang lebih dulu,
    /// namanya ditanyakan, dan tipe variabelnya ditampilkan supaya jelas apa
    /// yang sedang dibuat.
    ///
    /// Jendelanya dibangun di kode, bukan lewat berkas XAML terpisah, supaya
    /// tidak ada pack URI baru yang bisa salah dan baru ketahuan saat runtime.
    /// </summary>
    internal static class VariableNameWindow
    {
        /// <summary>
        /// Mengembalikan nama yang diketik user, atau null kalau dibatalkan.
        /// </summary>
        public static string Ask(Type variableType, string suggested)
        {
            var window = new Window
            {
                Title = "Buat variabel",
                SizeToContent = SizeToContent.Height,
                Width = 420,
                WindowStyle = WindowStyle.ToolWindow,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
            };

            try { window.Owner = Application.Current != null ? Application.Current.MainWindow : null; }
            catch (Exception) { }

            var root = new StackPanel { Margin = new Thickness(16) };

            root.Children.Add(new TextBlock
            {
                Text = "Nama variabel",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4),
            });

            var box = new TextBox
            {
                Text = suggested ?? "",
                Padding = new Thickness(4, 3, 4, 3),
                FontFamily = new FontFamily("Consolas"),
            };
            root.Children.Add(box);

            root.Children.Add(new TextBlock
            {
                Text = "Tipe: " + FriendlyName(variableType),
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 6, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0),
            };

            var ok = new Button { Content = "Buat", Width = 84, Padding = new Thickness(0, 4, 0, 4), IsDefault = true };
            var cancel = new Button { Content = "Batal", Width = 84, Padding = new Thickness(0, 4, 0, 4), Margin = new Thickness(8, 0, 0, 0), IsCancel = true };

            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            root.Children.Add(buttons);

            window.Content = root;

            string result = null;

            ok.Click += (s, e) =>
            {
                var name = (box.Text ?? "").Trim();
                if (!IsValidName(name))
                {
                    MessageBox.Show(window,
                        "Nama variabel harus diawali huruf atau garis bawah, dan hanya boleh berisi huruf, angka, dan garis bawah.",
                        "Buat variabel", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                result = name;
                window.DialogResult = true;
            };

            window.Loaded += (s, e) => { box.Focus(); box.SelectAll(); };

            // Enter di dalam kotak teks langsung berarti "Buat".
            box.KeyDown += (s, e) => { if (e.Key == Key.Enter) ok.RaiseEvent(new RoutedEventArgs(ButtonBase_ClickEvent)); };

            return window.ShowDialog() == true ? result : null;
        }

        private static readonly RoutedEvent ButtonBase_ClickEvent = System.Windows.Controls.Primitives.ButtonBase.ClickEvent;

        private static bool IsValidName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (!char.IsLetter(name[0]) && name[0] != '_') return false;

            foreach (var c in name)
            {
                if (!char.IsLetterOrDigit(c) && c != '_') return false;
            }
            return true;
        }

        /// <summary>Nama tipe yang enak dibaca: List(Of String), bukan List`1[System.String].</summary>
        public static string FriendlyName(Type type)
        {
            if (type == null) return "(tidak diketahui)";

            if (!type.IsGenericType) return type.FullName ?? type.Name;

            var name = type.Name;
            var tick = name.IndexOf('`');
            if (tick > 0) name = name.Substring(0, tick);

            var args = type.GetGenericArguments();
            var parts = new string[args.Length];
            for (var i = 0; i < args.Length; i++) parts[i] = FriendlyName(args[i]);

            return name + "(Of " + string.Join(", ", parts) + ")";
        }
    }
}

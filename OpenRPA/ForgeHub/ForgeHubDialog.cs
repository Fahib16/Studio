using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OpenRPA.ForgeHub
{
    /// <summary>
    /// Kotak dialog ForgeHub di Studio.
    ///
    /// Dibangun di kode, bukan XAML, dengan palet yang sama seperti pemilih
    /// template: latar krem, sudut membulat, aksen cokelat. Alasannya sama —
    /// dialog Windows bawaan terlihat seperti aplikasi lain yang kebetulan
    /// muncul di atas Studio.
    /// </summary>
    public static class ForgeHubDialog
    {
        // ------------------------------------------------------------------
        // Palet, diambil dari kamus tema Studio dengan cadangan yang aman
        // ------------------------------------------------------------------

        private static Brush Res(FrameworkElement owner, string key, Brush fallback)
        {
            var value = owner != null ? owner.TryFindResource(key) : Application.Current.TryFindResource(key);
            return value as Brush ?? fallback;
        }

        private static FontFamily Display(FrameworkElement owner)
        {
            var value = owner != null ? owner.TryFindResource("JF.FontDisplay") : null;
            return value as FontFamily ?? new FontFamily("Segoe UI");
        }

        // ------------------------------------------------------------------
        // Menyambung
        // ------------------------------------------------------------------

        /// <summary>
        /// Tanyakan alamat, nama pengguna, dan kata sandi, lalu coba masuk.
        ///
        /// Percobaan masuknya dilakukan SEBELUM dialog ditutup. Menyimpan
        /// setelan yang ternyata salah lalu gagal diam-diam nanti saat menerbitkan
        /// adalah cara paling membingungkan untuk memberi tahu bahwa sandinya keliru.
        /// </summary>
        public static bool ShowConnect(Window owner)
        {
            var client = StudioForgeHubClient.Instance;

            var cream = Res(owner, "JF.Surface", Brushes.WhiteSmoke);
            var panel = Res(owner, "JF.Panel", Brushes.White);
            var line = Res(owner, "JF.Border", Brushes.Gainsboro);
            var brown900 = Res(owner, "JF.Brown900", Brushes.Black);
            var brown700 = Res(owner, "JF.Brown700", Brushes.SaddleBrown);
            var muted = Res(owner, "JF.TextMuted", Brushes.Gray);

            var window = new Window
            {
                Title = "Sambungkan ke ForgeHub",
                Owner = owner,
                Width = 500,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
            };

            var shell = new Border
            {
                Background = cream,
                CornerRadius = new CornerRadius(14),
                BorderBrush = line,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(14),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 26,
                    ShadowDepth = 3,
                    Opacity = 0.22,
                    Color = Colors.Black,
                },
            };

            var root = new StackPanel { Margin = new Thickness(28, 24, 28, 22) };

            root.Children.Add(new TextBlock
            {
                Text = "Sambungkan ke ForgeHub",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = brown900,
                FontFamily = Display(owner),
            });

            root.Children.Add(new TextBlock
            {
                Text = "ForgeHub adalah orkestrator tempat proyek diterbitkan, dijadwalkan, "
                     + "dan dipantau. Studio tetap bekerja penuh tanpa ini.",
                FontSize = 12.5,
                Foreground = muted,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 20),
            });

            var urlBox = Field(root, panel, line, muted, "Alamat ForgeHub",
                string.IsNullOrEmpty(client.Url) ? "http://localhost:8080" : client.Url, false);

            var userBox = Field(root, panel, line, muted, "Nama pengguna",
                string.IsNullOrEmpty(client.Username) ? "FH_Admin" : client.Username, false);

            var passwordBox = (PasswordBox)Field(root, panel, line, muted, "Kata sandi", "", true);

            var status = new TextBlock
            {
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0),
                Visibility = Visibility.Collapsed,
            };

            root.Children.Add(status);

            // ---- Tombol ----
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0),
            };

            var cancel = new Button
            {
                Content = "Batal",
                Padding = new Thickness(18, 8, 18, 8),
                Margin = new Thickness(0, 0, 10, 0),
                MinWidth = 92,
            };

            var connect = new Button
            {
                Content = "Sambungkan",
                Padding = new Thickness(18, 8, 18, 8),
                MinWidth = 118,
                Foreground = cream,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                IsDefault = true,
            };

            // JF.HeaderPrimary menggambar teks krem di atas latar TRANSPARAN.
            // Di dialog berlatar krem, teksnya jadi tak terbaca — jadi latarnya
            // disediakan bingkai pembungkus ini.
            var connectHost = new Border
            {
                Background = brown700,
                CornerRadius = new CornerRadius(9),
                Child = connect,
            };

            buttons.Children.Add(cancel);
            buttons.Children.Add(connectHost);
            root.Children.Add(buttons);

            shell.Child = root;
            window.Content = shell;

            // Jendela tanpa bingkai tetap harus bisa digeser.
            shell.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    try { window.DragMove(); } catch (InvalidOperationException) { }
                }
            };

            var connected = false;

            cancel.Click += (s, e) => window.Close();

            connect.Click += (s, e) =>
            {
                var url = (urlBox as TextBox).Text.Trim();
                var user = (userBox as TextBox).Text.Trim();

                if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(user))
                {
                    Show(status, "Alamat dan nama pengguna wajib diisi.", Brushes.Firebrick);
                    return;
                }

                connect.IsEnabled = false;
                connect.Content = "Menyambung…";

                // Setelan disimpan lebih dulu supaya Connect() memakainya, lalu
                // hasilnya dinilai. Kalau gagal, setelannya tetap tersimpan —
                // orangnya tinggal membetulkan satu kolom, bukan mengetik ulang semua.
                client.Save(url, user, passwordBox.Password);

                try
                {
                    client.Connect();
                    connected = true;
                    window.Close();
                }
                catch (Exception ex)
                {
                    Show(status, ex.Message, Brushes.Firebrick);
                }
                finally
                {
                    connect.IsEnabled = true;
                    connect.Content = "Sambungkan";
                }
            };

            window.KeyDown += (s, e) => { if (e.Key == Key.Escape) window.Close(); };

            window.Loaded += (s, e) => passwordBox.Focus();
            window.ShowDialog();

            return connected;
        }

        private static Control Field(Panel parent, Brush panelBrush, Brush line, Brush muted,
            string label, string value, bool secret)
        {
            parent.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = muted,
                Margin = new Thickness(0, 0, 0, 5),
            });

            Control input;

            if (secret)
            {
                input = new PasswordBox { FontSize = 13.5, BorderThickness = new Thickness(0), Background = Brushes.Transparent };
            }
            else
            {
                input = new TextBox { Text = value, FontSize = 13.5, BorderThickness = new Thickness(0), Background = Brushes.Transparent };
            }

            var host = new Border
            {
                Background = panelBrush,
                BorderBrush = line,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(0, 0, 0, 15),
                Child = input,
            };

            parent.Children.Add(host);

            return input;
        }

        private static void Show(TextBlock status, string message, Brush color)
        {
            status.Text = message;
            status.Foreground = color;
            status.Visibility = Visibility.Visible;
        }

        // ------------------------------------------------------------------
        // Pesan hasil
        // ------------------------------------------------------------------

        public static void Info(Window owner, string title, string message)
        {
            MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static void Problem(Window owner, string title, string message)
        {
            MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

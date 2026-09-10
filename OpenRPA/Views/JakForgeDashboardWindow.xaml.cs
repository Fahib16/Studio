using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using OpenRPA.Interfaces;

namespace OpenRPA.Views
{
    /// <summary>
    /// Dashboard JakForge: daftar proyek dan pintu masuk ke editor kanvas.
    ///
    /// Jendela ini TIDAK menyimpan datanya sendiri. Sumbernya
    /// RobotInstance.instance.Projects, yaitu daftar proyek yang sama dengan
    /// yang dipakai jendela utama, jadi tidak ada kemungkinan dua daftar
    /// berbeda isi.
    ///
    /// Membuka kartu proyek berarti membuka workflow pertamanya di jendela
    /// utama lewat MainWindow.OnOpenWorkflow, bukan lewat jalur baru; dengan
    /// begitu perilaku setelah proyek terbuka persis sama dengan sebelumnya.
    /// </summary>
    public partial class JakForgeDashboardWindow : Window
    {
        public JakForgeDashboardWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => { Refresh(); FadeIn(); };

            // Ukuran rancangan 1180x780 lebih besar daripada layar kecil
            // (mis. 1280x720 satuan WPF). Kalau tidak dipangkas, bilah judul
            // beserta tombolnya berada di luar layar dan jendelanya tidak bisa
            // ditutup.
            var work = SystemParameters.WorkArea;
            Width = Math.Min(Width, work.Width - 40);
            Height = Math.Min(Height, work.Height - 40);
        }

        /// <summary>
        /// Memuat ulang daftar proyek. Dipakai JakForgeShell setiap layar ini
        /// ditampilkan kembali lewat tombol Home, supaya proyek yang baru
        /// dibuat langsung terlihat.
        /// </summary>
        public void RefreshProjects()
        {
            try { Refresh(); }
            catch (Exception ex) { Log.Error("JakForge dashboard: " + ex.ToString()); }
        }

        /// <summary>
        /// Menutup layar ini berarti keluar dari aplikasi selama kanvas belum
        /// pernah tampil; keputusannya ada di JakForgeShell.
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            JakForgeShell.HomeClosing(e);
        }

        /// <summary>
        /// Muncul dengan memudar masuk sambil membesar sedikit dari 98%.
        /// Gerak sekecil ini tidak terbaca sebagai animasi, hanya membuat
        /// perpindahan dari layar Loading terasa menyambung.
        /// </summary>
        private void FadeIn()
        {
            try
            {
                var scale = new ScaleTransform(0.98, 0.98);
                RenderTransformOrigin = new Point(0.5, 0.5);
                RenderTransform = scale;
                Opacity = 0;

                var ease = new System.Windows.Media.Animation.QuadraticEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                };
                var duration = TimeSpan.FromMilliseconds(260);

                BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = 1,
                    Duration = duration,
                    EasingFunction = ease
                });

                var grow = new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = 1,
                    Duration = duration,
                    EasingFunction = ease
                };
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            }
            catch (Exception ex)
            {
                Log.Debug("JakForge dashboard: " + ex.Message);
                Opacity = 1;
            }
        }

        /// <summary>
        /// Menyembunyikan jendela dengan memudar keluar, lalu menjalankan
        /// tindakan lanjutannya. Berpindah ke kanvas jadi terasa menyambung,
        /// bukan berkedip.
        ///
        /// SEMBUNYI, bukan tutup: layar Home dipakai lagi setiap tombol Home
        /// ditekan, dan membangunnya ulang berarti memuat daftar proyek dari
        /// awal tanpa alasan.
        /// </summary>
        private void FadeOutThen(Action next)
        {
            Action finish = () =>
            {
                if (next != null) next();
                try
                {
                    Hide();
                    BeginAnimation(OpacityProperty, null);
                    Opacity = 1;
                }
                catch (Exception) { }
            };

            try
            {
                var fade = new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new System.Windows.Media.Animation.QuadraticEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn
                    }
                };
                fade.Completed += (s, e) => finish();
                BeginAnimation(OpacityProperty, fade);
            }
            catch (Exception)
            {
                finish();
            }
        }

        private void Refresh()
        {
            FillUserBlock();

            var projects = new List<IProject>();
            try
            {
                if (RobotInstance.instance != null && RobotInstance.instance.Projects != null)
                    projects = RobotInstance.instance.Projects.ToList();
            }
            catch (Exception ex)
            {
                Log.Error("JakForge dashboard: " + ex.ToString());
            }

            var recent = projects.OrderByDescending(p => p._modified).Take(3).ToList();
            RecentBadge.Text = recent.Count.ToString();

            RecentList.Items.Clear();
            foreach (var p in recent) RecentList.Items.Add(BuildCard(p));

            ProjectList.Items.Clear();
            foreach (var p in projects.OrderBy(p => p.name)) ProjectList.Items.Add(BuildCard(p));

            if (projects.Count == 0)
                ProjectList.Items.Add(EmptyState("Belum ada proyek. Mulai dengan tombol Buat Proyek Baru."));
        }

        private void FillUserBlock()
        {
            var name = Environment.UserName;
            var online = false;

            try
            {
                online = global.isConnected;
                if (online && global.webSocketClient != null && global.webSocketClient.user != null &&
                    !string.IsNullOrWhiteSpace(global.webSocketClient.user.name))
                {
                    name = global.webSocketClient.user.name;
                }
            }
            catch (Exception)
            {
                // Tidak tersambung ke OpenFlow: nama Windows sudah cukup.
            }

            UserName.Text = name;
            UserStatus.Text = online ? "Online" : "Offline";
            UserStatusDot.Fill = (Brush)FindResource(online ? "JF.Ok" : "JF.TextMuted");
            UserInitials.Text = Initials(name);
        }

        private static string Initials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Split(new[] { ' ', '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            if (parts.Length == 1) return parts[0].Substring(0, 1).ToUpperInvariant();
            return (parts[0].Substring(0, 1) + parts[1].Substring(0, 1)).ToUpperInvariant();
        }

        /// <summary>
        /// Kartu proyek. Baris keterangannya sengaja memakai angka yang benar
        /// ada di OpenRPA (waktu ubah dan jumlah workflow), bukan "Status:
        /// Selesai" seperti pada mockup yang tidak punya sumber data.
        /// </summary>
        private Button BuildCard(IProject project)
        {
            var tile = new Border
            {
                Width = 56,
                Height = 56,
                CornerRadius = new CornerRadius(12),
                Background = (Brush)FindResource("JF.Panel"),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            tile.Child = new Path
            {
                Data = (Geometry)FindResource("Ico.Doc"),
                Stroke = (Brush)FindResource("JF.Brown700"),
                StrokeThickness = 1.5,
                Stretch = Stretch.Uniform,
                Width = 26,
                Height = 26
            };

            var dots = new TextBlock
            {
                Text = "⋮",
                FontSize = 18,
                Foreground = (Brush)FindResource("JF.TextMuted"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -4, 0, 0)
            };

            var head = new Grid();
            head.Children.Add(tile);
            head.Children.Add(dots);

            var title = new TextBlock
            {
                Text = project.name,
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("JF.Brown900"),
                FontFamily = (FontFamily)FindResource("JF.FontBody"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 16, 0, 0)
            };

            var body = new StackPanel();
            body.Children.Add(head);
            body.Children.Add(title);
            body.Children.Add(Line("Diubah: " + Ago(project._modified)));
            body.Children.Add(Line("Workflow: " + WorkflowCount(project)));

            var card = new Button { Style = (Style)FindResource("ProjectCard"), Content = body, Tag = project };
            card.Click += Card_Click;
            card.ToolTip = project.Path;
            return card;
        }

        private TextBlock Line(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 13,
                Margin = new Thickness(0, 6, 0, 0),
                Foreground = (Brush)FindResource("JF.TextMuted"),
                FontFamily = (FontFamily)FindResource("JF.FontBody")
            };
        }

        private Border EmptyState(string text)
        {
            return new Border
            {
                Padding = new Thickness(22),
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (Brush)FindResource("JF.TextMuted"),
                    FontFamily = (FontFamily)FindResource("JF.FontBody")
                }
            };
        }

        private static int WorkflowCount(IProject project)
        {
            try { return project.Workflows == null ? 0 : project.Workflows.Count(); }
            catch (Exception) { return 0; }
        }

        private static string Ago(DateTime when)
        {
            if (when == DateTime.MinValue) return "belum pernah";

            var span = DateTime.Now - when.ToLocalTime();
            if (span.TotalSeconds < 90) return "baru saja";
            if (span.TotalMinutes < 60) return ((int)span.TotalMinutes) + " menit lalu";
            if (span.TotalHours < 24) return ((int)span.TotalHours) + " jam lalu";
            if (span.TotalDays < 30) return ((int)span.TotalDays) + " hari lalu";
            return when.ToLocalTime().ToString("d MMM yyyy");
        }

        private void Card_Click(object sender, RoutedEventArgs e)
        {
            var project = (sender as Button)?.Tag as IProject;
            if (project == null) return;

            var main = MainWindow.instance;
            if (main == null) return;

            try
            {
                FadeOutThen(() =>
                {
                    var workflow = project.Workflows == null ? null : project.Workflows.FirstOrDefault();
                    if (workflow != null)
                    {
                        main.OnOpenWorkflow(workflow);
                    }
                    else
                    {
                        // Proyek tanpa workflow: buka saja panel proyek jendela utama
                        // supaya orang bisa menambahkan workflow di sana.
                        main.OnOpen(null);
                    }

                    JakForgeShell.ShowCanvas();
                });
            }
            catch (Exception ex)
            {
                Log.Error("JakForge dashboard: " + ex.ToString());
            }
        }

        private void NewProject_Click(object sender, RoutedEventArgs e)
        {
            var main = MainWindow.instance;
            if (main == null) return;

            try
            {
                FadeOutThen(() =>
                {
                    JakForgeShell.ShowCanvas();
                    main.NewProjectCommand.Execute(null);
                });
            }
            catch (Exception ex)
            {
                Log.Error("JakForge dashboard: " + ex.ToString());
            }
        }

        private void Nav_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;

            var tag = (sender as RadioButton)?.Tag as string;
            switch (tag)
            {
                case "recent":
                    NavDashboard.IsChecked = true;
                    break;

                case "template":
                    NewFromTemplate();
                    NavDashboard.IsChecked = true;
                    break;

                case "settings":
                    Settings_Click(sender, e);
                    NavDashboard.IsChecked = true;
                    break;
            }
        }


        /// <summary>
        /// Buat proyek baru dari sebuah template.
        ///
        /// Pemilihnya dibangun di kode, bukan berkas XAML tersendiri: isinya
        /// cuma daftar pendek dan satu kotak nama, dan menambah berkas XAML baru
        /// berarti menambah satu pack URI lagi yang bisa salah dan baru ketahuan
        /// saat dijalankan.
        /// </summary>
        /// <summary>
        /// Buat proyek baru dari sebuah template.
        ///
        /// Jendelanya dibangun di kode, tapi memakai palet dan bentuk yang sama
        /// dengan layar Home: latar krem, kartu putih bersudut membulat, aksen
        /// cokelat. Versi sebelumnya memakai ListBox dan Button bawaan Windows,
        /// dan hasilnya terlihat seperti dialog dari aplikasi lain yang
        /// kebetulan muncul di atas Studio.
        /// </summary>
        private async void NewFromTemplate()
        {
            try
            {
                var templates = Templates.ProjectTemplates.All;

                var picker = new Window
                {
                    Title = "Proyek Baru",
                    Owner = this,
                    Width = 720,
                    Height = 520,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    WindowStyle = WindowStyle.None,
                    ResizeMode = ResizeMode.NoResize,
                    AllowsTransparency = true,
                    Background = Brushes.Transparent,
                };

                var cream = TryFindResource("JF.Surface") as Brush ?? Brushes.White;
                var panel = TryFindResource("JF.Panel") as Brush ?? Brushes.WhiteSmoke;
                var border = TryFindResource("JF.Border") as Brush ?? Brushes.Gainsboro;
                var brown900 = TryFindResource("JF.Brown900") as Brush ?? Brushes.Black;
                var brown700 = TryFindResource("JF.Brown700") as Brush ?? Brushes.SaddleBrown;
                var muted = TryFindResource("JF.TextMuted") as Brush ?? Brushes.Gray;
                var accent = TryFindResource("JF.Accent") as Brush ?? Brushes.AntiqueWhite;

                // Bingkai luar: sudut membulat dan bayangan, sama seperti kartu
                // proyek di layar Home.
                var shell = new Border
                {
                    Background = cream,
                    CornerRadius = new CornerRadius(14),
                    BorderBrush = border,
                    BorderThickness = new Thickness(1),
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        BlurRadius = 26,
                        ShadowDepth = 3,
                        Opacity = 0.22,
                        Color = Colors.Black,
                    },
                    Margin = new Thickness(14),
                };

                var root = new Grid();
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // ---- Kepala ----
                var header = new StackPanel { Margin = new Thickness(28, 24, 28, 8) };
                header.Children.Add(new TextBlock
                {
                    Text = "Proyek Baru",
                    FontSize = 24,
                    FontWeight = FontWeights.Bold,
                    Foreground = brown900,
                    FontFamily = TryFindResource("JF.FontDisplay") as FontFamily ?? new FontFamily("Segoe UI"),
                });
                header.Children.Add(new TextBlock
                {
                    Text = "Pilih titik mulai, lalu beri nama proyeknya.",
                    FontSize = 13,
                    Foreground = muted,
                    Margin = new Thickness(0, 4, 0, 0),
                });
                Grid.SetRow(header, 0);
                root.Children.Add(header);

                // ---- Daftar template sebagai kartu ----
                var list = new ItemsControl { Margin = new Thickness(28, 12, 28, 0) };
                var cards = new List<Border>();
                Templates.ProjectTemplate chosen = templates.Count > 0 ? templates[0] : null;

                var stack = new StackPanel();

                foreach (var template in templates)
                {
                    var current = template;

                    var card = new Border
                    {
                        Background = Brushes.White,
                        BorderBrush = border,
                        BorderThickness = new Thickness(1.5),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(16, 14, 16, 14),
                        Margin = new Thickness(0, 0, 0, 10),
                        Cursor = System.Windows.Input.Cursors.Hand,
                        Tag = current,
                    };

                    var content = new StackPanel();
                    content.Children.Add(new TextBlock
                    {
                        Text = current.Name,
                        FontSize = 14.5,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = brown900,
                    });
                    content.Children.Add(new TextBlock
                    {
                        Text = current.Description,
                        FontSize = 12,
                        Foreground = muted,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 5, 0, 0),
                    });

                    card.Child = content;

                    card.MouseLeftButtonUp += (s, e) =>
                    {
                        chosen = current;
                        foreach (var other in cards)
                        {
                            var selected = ReferenceEquals(other, card);
                            other.BorderBrush = selected ? brown700 : border;
                            other.Background = selected ? accent : Brushes.White;
                        }
                    };

                    cards.Add(card);
                    stack.Children.Add(card);
                }

                if (cards.Count > 0)
                {
                    cards[0].BorderBrush = brown700;
                    cards[0].Background = accent;
                }

                var scroller = new ScrollViewer
                {
                    Content = stack,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Margin = new Thickness(28, 12, 28, 0),
                    BorderThickness = new Thickness(0),
                };
                Grid.SetRow(scroller, 1);
                root.Children.Add(scroller);

                // ---- Kaki: nama proyek dan tombol ----
                var footer = new Grid { Margin = new Thickness(28, 16, 28, 24) };
                footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var nameLabel = new TextBlock
                {
                    Text = "Nama proyek",
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = brown700,
                    Margin = new Thickness(2, 0, 0, 6),
                };
                Grid.SetRow(nameLabel, 0);
                footer.Children.Add(nameLabel);

                var bottom = new Grid();
                bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var nameHost = new Border
                {
                    Background = Brushes.White,
                    BorderBrush = border,
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(9),
                    Padding = new Thickness(12, 9, 12, 9),
                    Margin = new Thickness(0, 0, 12, 0),
                };

                var nameBox = new TextBox
                {
                    Text = Project.UniqueName("ReFramework", null),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    FontSize = 14,
                    Foreground = brown900,
                };
                nameHost.Child = nameBox;
                Grid.SetColumn(nameHost, 0);
                bottom.Children.Add(nameHost);

                var buttons = new StackPanel { Orientation = Orientation.Horizontal };

                var cancel = new Button
                {
                    Content = "Batal",
                    Style = TryFindResource("JF.HeaderGhost") as Style,
                    Padding = new Thickness(18, 10, 18, 10),
                    Margin = new Thickness(0, 0, 8, 0),
                    IsCancel = true,
                };

                var create = new Button
                {
                    Content = "Buat Proyek",
                    Style = TryFindResource("JF.HeaderPrimary") as Style,
                    Padding = new Thickness(22, 10, 22, 10),
                    IsDefault = true,
                };

                // JF.HeaderPrimary memberi teks krem di atas latar TRANSPARAN —
                // ia dirancang untuk bilah cokelat di kepala jendela. Di dialog
                // berlatar krem, teksnya jadi nyaris tak terbaca. Jadi latarnya
                // disediakan oleh bingkai pembungkus ini.
                var createHost = new Border
                {
                    Background = brown700,
                    CornerRadius = new CornerRadius(9),
                    Child = create,
                };

                buttons.Children.Add(cancel);
                buttons.Children.Add(createHost);
                Grid.SetColumn(buttons, 1);
                bottom.Children.Add(buttons);

                Grid.SetRow(bottom, 1);
                footer.Children.Add(bottom);

                Grid.SetRow(footer, 2);
                root.Children.Add(footer);

                shell.Child = root;
                picker.Content = shell;

                // Jendela tanpa bilah judul: menyeret bagian mana pun
                // memindahkannya, kalau tidak ia terkunci di tengah layar.
                shell.MouseLeftButtonDown += (s, e) =>
                {
                    if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                    {
                        try { picker.DragMove(); } catch (Exception) { }
                    }
                };

                string chosenName = null;

                create.Click += (s, e) =>
                {
                    var name = (nameBox.Text ?? "").Trim();
                    if (name.Length == 0)
                    {
                        MessageBox.Show(picker, "Nama proyek belum diisi.", "Proyek baru",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    chosenName = name;
                    picker.DialogResult = true;
                };

                picker.Loaded += (s, e) => { nameBox.Focus(); nameBox.SelectAll(); };

                if (picker.ShowDialog() != true) return;

                Mouse.OverrideCursor = Cursors.Wait;

                IWorkflow workflow;
                try
                {
                    workflow = await Templates.ProjectTemplates.CreateProject(chosen, chosenName);
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }

                RefreshProjects();

                var main = MainWindow.instance;
                if (main != null && workflow != null)
                {
                    JakForgeShell.ShowCanvas();
                    main.OnOpenWorkflow(workflow);
                }
            }
            catch (Exception ex)
            {
                Log.Error("JakForge template: " + ex.ToString());
                MessageBox.Show(this, ex.Message, "Proyek baru dari template",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var main = MainWindow.instance;
            if (main == null) return;

            try
            {
                FadeOutThen(() =>
                {
                    // Pengaturan dibuka sebagai tab di jendela kanvas, jadi
                    // kanvasnya harus tampil dulu.
                    JakForgeShell.ShowCanvas();
                    main.SettingsCommand.Execute(null);
                });
            }
            catch (Exception ex)
            {
                Log.Error("JakForge dashboard: " + ex.ToString());
            }
        }

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("https://openrpa.dk/");
            }
            catch (Exception ex)
            {
                Log.Error("JakForge dashboard: " + ex.ToString());
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                return;
            }
            try { DragMove(); } catch (Exception) { }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
    }
}

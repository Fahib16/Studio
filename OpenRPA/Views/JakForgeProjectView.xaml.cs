using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OpenRPA.Interfaces;

namespace OpenRPA.Views
{
    /// <summary>
    /// Panel Project: pohon isi proyek — dependensi, workflow, dan berkas di
    /// foldernya. Klik dua kali pada workflow membukanya di kanvas.
    /// </summary>
    public partial class JakForgeProjectView : UserControl
    {
        public JakForgeProjectView()
        {
            InitializeComponent();
            Loaded += (s, e) => { BindProjectTitle(); Fill(); };
        }


        /// <summary>
        /// Sambungkan judul panel ke nama proyek yang sedang dibuka.
        ///
        /// Bindingnya dipasang dari kode, bukan XAML, karena DataContext panel
        /// ini bukan MainWindow — sumbernya harus disebut eksplisit. MainWindow
        /// mengabarkan perubahan ProjectHeader, jadi judulnya ikut berubah
        /// sendiri saat proyek lain dibuka.
        /// </summary>
        private void BindProjectTitle()
        {
            try
            {
                var main = MainWindow.instance;
                if (main == null || ProjectTitle == null) return;

                ProjectTitle.SetBinding(System.Windows.Controls.TextBlock.TextProperty,
                    new System.Windows.Data.Binding("ProjectHeader") { Source = main });
            }
            catch (Exception ex)
            {
                Log.Error("JakForge project title: " + ex.Message);
            }
        }

        public static JakForgeProjectView Instance { get; private set; }

        /// <summary>Dipanggil dari luar setelah proyek atau workflow berubah.</summary>
        public static void ReloadAll()
        {
            var view = Instance;
            if (view == null) return;

            try { view.Dispatcher.BeginInvoke(new Action(view.Fill)); }
            catch (Exception) { }
        }

        private void Fill()
        {
            Instance = this;

            try
            {
                Tree.Items.Clear();

                var projects = RobotInstance.instance != null && RobotInstance.instance.Projects != null
                    ? RobotInstance.instance.Projects.ToList()
                    : new System.Collections.Generic.List<IProject>();

                if (projects.Count == 0)
                {
                    Tree.Items.Add(Leaf("Belum ada proyek", null, null));
                    return;
                }

                var filter = SearchBox == null ? "" : (SearchBox.Text ?? "").Trim();

                foreach (var project in projects.OrderBy(p => p.name))
                {
                    var root = Node(project.name, "#4A3927", project);
                    root.IsExpanded = projects.Count == 1;

                    // --- Dependencies ---
                    var deps = Node("Dependencies", "#7A6A58", null);
                    if (project.dependencies != null && project.dependencies.Count > 0)
                    {
                        foreach (var d in project.dependencies.OrderBy(d => d.Key))
                            deps.Items.Add(Leaf(d.Key + " = " + d.Value, "#8D6C43", null));
                    }
                    else
                    {
                        deps.Items.Add(Leaf("(tidak ada)", "#9A8B78", null));
                    }
                    root.Items.Add(deps);

                    // --- Workflows ---
                    var flows = Node("Workflows", "#7A6A58", null);
                    flows.IsExpanded = true;
                    try
                    {
                        foreach (var wf in project.Workflows.OrderBy(w => w.name))
                        {
                            if (filter.Length > 0 &&
                                wf.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                            flows.Items.Add(Leaf(wf.name, "#8A5A2B", wf));
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug("JakForge project: " + ex.Message);
                    }
                    root.Items.Add(flows);

                    // --- Berkas di folder proyek ---
                    var files = Node("Berkas", "#7A6A58", null);
                    try
                    {
                        var dir = project.Path;
                        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        {
                            foreach (var sub in Directory.GetDirectories(dir).OrderBy(x => x))
                                files.Items.Add(Leaf(Path.GetFileName(sub) + Path.DirectorySeparatorChar, "#B08046", null));

                            foreach (var f in Directory.GetFiles(dir).OrderBy(x => x))
                            {
                                var name = Path.GetFileName(f);
                                if (filter.Length > 0 &&
                                    name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                                files.Items.Add(Leaf(name, "#6E5844", f));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug("JakForge project: " + ex.Message);
                    }
                    root.Items.Add(files);

                    Tree.Items.Add(root);
                }
            }
            catch (Exception ex)
            {
                Log.Error("JakForge project: " + ex.ToString());
            }
        }

        private TreeViewItem Node(string text, string color, object tag)
        {
            var item = new TreeViewItem
            {
                Header = text,
                Tag = tag,
                FontFamily = (FontFamily)FindResource("JF.FontBody"),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush(color)
            };
            return item;
        }

        private TreeViewItem Leaf(string text, string color, object tag)
        {
            var item = new TreeViewItem
            {
                Header = text,
                Tag = tag,
                FontFamily = (FontFamily)FindResource("JF.FontBody"),
                FontSize = 12.5,
                Foreground = Brush(color)
            };
            return item;
        }

        private Brush Brush(string color)
        {
            if (string.IsNullOrEmpty(color)) return (Brush)FindResource("JF.TextMuted");
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        }

        private void Tree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = Tree.SelectedItem as TreeViewItem;
            if (item == null) return;

            var workflow = item.Tag as IWorkflow;
            if (workflow != null)
            {
                var main = MainWindow.instance;
                if (main == null) return;

                JakForgeShell.ShowCanvas();
                main.OnOpenWorkflow(workflow);
                e.Handled = true;
                return;
            }

            // Berkas biasa: serahkan ke aplikasi bawaan Windows.
            var path = item.Tag as string;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try { System.Diagnostics.Process.Start(path); }
                catch (Exception ex) { Log.Error("JakForge project: " + ex.Message); }
                e.Handled = true;
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            Fill();
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var folder = FolderOnDisk(Current());
                if (string.IsNullOrEmpty(folder))
                {
                    Log.Output("Folder proyek belum ada di disk — simpan proyeknya dulu.");
                    return;
                }
                System.Diagnostics.Process.Start("explorer.exe", "\"" + folder + "\"");
            }
            catch (Exception ex)
            {
                Log.Error("JakForge project: " + ex.Message);
            }
        }

        private void Packages_Click(object sender, RoutedEventArgs e)
        {
            var main = MainWindow.instance;
            if (main == null) return;

            try
            {
                JakForgeShell.ShowCanvas();
                if (main.ManagePackagesCommand.CanExecute(null)) main.ManagePackagesCommand.Execute(null);
            }
            catch (Exception ex)
            {
                Log.Error("JakForge project: " + ex.Message);
            }
        }

        private void NewWorkflow_Click(object sender, RoutedEventArgs e)
        {
            var main = MainWindow.instance;
            if (main == null) return;

            try
            {
                JakForgeShell.ShowCanvas();
                if (main.NewWorkflowCommand.CanExecute(null)) main.NewWorkflowCommand.Execute(null);
            }
            catch (Exception ex)
            {
                Log.Error("JakForge project: " + ex.Message);
            }
        }

        private void Search_Changed(object sender, TextChangedEventArgs e)
        {
            Fill();
        }

        /// <summary>Proyek yang sedang dipilih di pohon, kalau tidak ada ambil yang pertama.</summary>
        /// <summary>
        /// Folder proyek yang BENAR-BENAR ada di disk.
        ///
        /// <c>Project.Path</c> menunjuk <c>ProjectsDirectory\{nama}</c>, padahal
        /// penyimpanan folder-per-proyek menaruh isinya satu tingkat lebih
        /// dalam: <c>ProjectsDirectory\offline\{nama}</c> saat tidak tersambung
        /// ke OpenCore, atau <c>ProjectsDirectory\{host}\{nama}</c> saat
        /// tersambung. Menyerahkan path yang tidak ada ke Explorer membuatnya
        /// membuka Documents begitu saja — persis yang terjadi selama ini.
        /// </summary>
        private static string FolderOnDisk(IProject project)
        {
            if (project == null) return null;

            var root = Interfaces.Extensions.ProjectsDirectory;
            if (string.IsNullOrEmpty(root)) return null;

            var name = project.name;

            if (!string.IsNullOrEmpty(project.Path) && Directory.Exists(project.Path)) return project.Path;

            if (!string.IsNullOrEmpty(name))
            {
                // Langsung di bawah akar; tidak ada lagi tingkat "offline" atau
                // nama host orchestrator. Lihat BasePath() di provider
                // penyimpanan folder-per-proyek.
                var direct = Path.Combine(root, name);
                if (Directory.Exists(direct)) return direct;

                // Nama folder dirapikan dari karakter yang tidak boleh dipakai
                // nama berkas, jadi tebakan di atas bisa meleset satu-dua huruf.
                if (Directory.Exists(root))
                {
                    var match = Directory.GetDirectories(root).FirstOrDefault(
                        d => string.Equals(Path.GetFileName(d), name, StringComparison.OrdinalIgnoreCase));
                    if (match != null) return match;
                }
            }

            return Directory.Exists(root) ? root : null;
        }

        private IProject Current()
        {
            var item = Tree.SelectedItem as TreeViewItem;
            while (item != null)
            {
                var project = item.Tag as IProject;
                if (project != null) return project;
                item = ItemsControl.ItemsControlFromItemContainer(item) as TreeViewItem;
            }

            try
            {
                return RobotInstance.instance != null && RobotInstance.instance.Projects != null
                    ? RobotInstance.instance.Projects.FirstOrDefault()
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

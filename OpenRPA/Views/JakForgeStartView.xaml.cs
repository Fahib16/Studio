using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OpenRPA.Interfaces;

namespace OpenRPA.Views
{
    /// <summary>
    /// Halaman awal kanvas, menggantikan tab "Open project".
    ///
    /// Daftar proyek sudah ada di panel Project; menampilkannya lagi sebagai
    /// tab dokumen hanya memakan satu tab penuh untuk isi yang sama.
    /// </summary>
    public partial class JakForgeStartView : UserControl
    {
        public JakForgeStartView()
        {
            InitializeComponent();
            Loaded += (s, e) => Refresh();
        }

        public static JakForgeStartView Instance { get; private set; }

        /// <summary>Perbarui teks petunjuknya setelah proyek berganti.</summary>
        public static void ReloadAll()
        {
            var view = Instance;
            if (view == null) return;

            try { view.Dispatcher.BeginInvoke(new Action(view.Refresh)); }
            catch (Exception) { }
        }

        private void Refresh()
        {
            Instance = this;

            try
            {
                var workflow = MainWorkflow();

                if (workflow == null)
                {
                    OpenMainText.Visibility = Visibility.Collapsed;
                    HintText.Text = "Belum ada workflow. Buat proyek baru dari layar Home, "
                                  + "atau pilih workflow di panel Project.";
                    return;
                }

                OpenMainText.Visibility = Visibility.Visible;
                HintText.Text = "Workflow lain ada di panel Project di sebelah kiri.";
            }
            catch (Exception ex)
            {
                Log.Error("JakForge start view: " + ex.Message);
            }
        }

        /// <summary>
        /// Workflow utama proyek yang sedang dibuka.
        ///
        /// "Main" lebih diutamakan — itu nama yang dipakai template ReFramework
        /// dan yang dicari orang lebih dulu. Kalau tidak ada, workflow pertama
        /// milik proyek pertama.
        /// </summary>
        private static IWorkflow MainWorkflow()
        {
            var instance = RobotInstance.instance;
            if (instance == null || instance.Projects == null) return null;

            var projects = instance.Projects.ToList();
            if (projects.Count == 0) return null;

            foreach (var project in projects)
            {
                var main = project.Workflows.FirstOrDefault(
                    w => string.Equals(w.name, "Main", StringComparison.OrdinalIgnoreCase));
                if (main != null) return main;
            }

            return projects[0].Workflows.FirstOrDefault();
        }

        private void OpenMain_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var workflow = MainWorkflow();
                if (workflow == null) return;

                var main = MainWindow.instance;
                if (main != null) main.OnOpenWorkflow(workflow);
            }
            catch (Exception ex)
            {
                Log.Error("JakForge start view: " + ex.ToString());
                MessageBox.Show(ex.Message, "Buka workflow", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using OpenRPA.Interfaces;

namespace OpenRPA.Views
{
    /// <summary>
    /// Panel Debug: keadaan jalannya workflow, nilai variabelnya, dan pesan
    /// kesalahan kalau gagal — plus tombol Continue/Step/Stop/Toggle
    /// Breakpoint yang menempel ke perintah yang memang sudah ada.
    ///
    /// Datanya DITARIK setiap setengah detik dari WorkflowInstance.Instances.
    /// Berlangganan peristiwa dari mesin workflow berarti thread pekerja
    /// menyentuh UI; jalur itu sudah pernah dicoba untuk panel Output dan
    /// akibatnya Studio berhenti di layar Loading.
    /// </summary>
    public partial class JakForgeDebugView : UserControl
    {
        private readonly DispatcherTimer timer = new DispatcherTimer(DispatcherPriority.Background);
        private readonly ObservableCollection<RunRow> runs = new ObservableCollection<RunRow>();
        private readonly ObservableCollection<VariableRow> variables = new ObservableCollection<VariableRow>();

        /// <summary>
        /// Ringkasan isi panel pada penarikan terakhir. Dipakai supaya panel
        /// hanya digambar ulang saat ada yang benar-benar berubah — kalau
        /// tidak, daftar akan berkedip dan pilihan user hilang tiap setengah
        /// detik.
        /// </summary>
        private string lastSignature = "";

        /// <summary>InstanceId jalan yang sedang dipilih user, "" berarti ikut yang terbaru.</summary>
        private string pinnedInstanceId = "";

        /// <summary>Jendela utama, atau null kalau Studio jalan tanpa UI.</summary>
        private static MainWindow Main
        {
            get { return RobotInstance.instance == null ? null : RobotInstance.instance.Window as MainWindow; }
        }

        public JakForgeDebugView()
        {
            InitializeComponent();

            RunList.ItemsSource = runs;
            VariableGrid.ItemsSource = variables;

            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += (s, e) => Sync();

            Loaded += (s, e) => { timer.Start(); Sync(); };
            Unloaded += (s, e) => timer.Stop();
        }

        private void Sync()
        {
            try
            {
                // Disalin dulu lewat ToList: WorkflowInstance.Instances itu List<T>,
                // dan Reverse() di atasnya membalik daftar ASLINYA milik mesin
                // workflow, bukan sekadar urutan tampilan di panel ini.
                var instances = WorkflowInstance.Instances.Where(i => i != null).ToList();
                instances.Reverse();                       // yang terbaru di atas
                if (instances.Count > 50) instances = instances.Take(50).ToList();

                var signature = string.Join("|",
                    instances.Select(i => i.InstanceId + ":" + i.state + ":" + VariableCount(i)));
                signature += "#" + pinnedInstanceId;

                if (signature == lastSignature) return;
                lastSignature = signature;

                RebuildRuns(instances);
                RebuildDetail(Selected(instances));
            }
            catch (Exception ex)
            {
                // Panel debug tidak boleh menjatuhkan Studio hanya karena
                // gagal membaca keadaan sebuah jalan.
                Log.Debug("Panel Debug: " + ex.Message);
                timer.Stop();
            }
        }

        private static int VariableCount(IWorkflowInstance instance)
        {
            var vars = instance.Variables;
            return vars == null ? 0 : vars.Count;
        }

        private void RebuildRuns(List<WorkflowInstance> instances)
        {
            var keepId = pinnedInstanceId;

            runs.Clear();
            foreach (var instance in instances)
            {
                runs.Add(new RunRow
                {
                    InstanceId = instance.InstanceId,
                    Name = instance.Workflow != null ? instance.Workflow.name : "(tanpa nama)",
                    Detail = Describe(instance)
                });
            }

            var restore = runs.FirstOrDefault(r => r.InstanceId == keepId) ?? runs.FirstOrDefault();

            // Pilihan disetel tanpa memicu ulang penarikan: handler
            // SelectionChanged hanya mencatat pilihan user, bukan yang ini.
            RunList.SelectionChanged -= RunList_SelectionChanged;
            RunList.SelectedItem = restore;
            RunList.SelectionChanged += RunList_SelectionChanged;
        }

        private static string Describe(WorkflowInstance instance)
        {
            var state = string.IsNullOrEmpty(instance.state) ? "?" : instance.state;

            var watch = instance.runWatch;
            if (watch == null) return state;

            var seconds = watch.Elapsed.TotalSeconds;
            return state + " · " + seconds.ToString("0.0") + " dtk";
        }

        private WorkflowInstance Selected(List<WorkflowInstance> instances)
        {
            if (!string.IsNullOrEmpty(pinnedInstanceId))
            {
                var pinned = instances.FirstOrDefault(i => i.InstanceId == pinnedInstanceId);
                if (pinned != null) return pinned;
            }
            return instances.FirstOrDefault();
        }

        private void RebuildDetail(WorkflowInstance instance)
        {
            variables.Clear();

            if (instance == null)
            {
                StateText.Text = "tidak ada yang berjalan";
                ErrorBox.Visibility = Visibility.Collapsed;
                UpdateButtons(null);
                return;
            }

            StateText.Text = Describe(instance);

            var vars = instance.Variables;
            if (vars != null)
            {
                foreach (var pair in vars.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
                {
                    variables.Add(new VariableRow
                    {
                        Name = pair.Key,
                        TypeName = pair.Value != null && pair.Value.type != null ? pair.Value.type.Name : "",
                        Value = Render(pair.Value == null ? null : pair.Value.value)
                    });
                }
            }

            if (instance.hasError && !string.IsNullOrEmpty(instance.errormessage))
            {
                ErrorText.Text = instance.errormessage;
                ErrorBox.Visibility = Visibility.Visible;
            }
            else
            {
                ErrorBox.Visibility = Visibility.Collapsed;
            }

            UpdateButtons(instance);
        }

        /// <summary>
        /// Nilai variabel dipendekkan: sebuah DataTable atau teks panjang bisa
        /// berisi ribuan karakter, dan menaruhnya utuh di satu sel membuat
        /// panel tersendat setiap kali digambar ulang.
        /// </summary>
        private static string Render(object value)
        {
            if (value == null) return "(null)";

            string text;
            try { text = value.ToString(); }
            catch (Exception) { return "(tidak bisa dibaca)"; }

            if (text == null) return "(null)";
            text = text.Replace("\r", " ").Replace("\n", " ");
            return text.Length <= 200 ? text : text.Substring(0, 200) + " …";
        }

        private void UpdateButtons(WorkflowInstance instance)
        {
            var running = instance != null && !instance.isCompleted;
            StopButton.IsEnabled = running;

            var main = Main;
            var designer = main == null ? null : main.JakForgeDesigner();
            var paused = designer != null && designer.BreakPointhit;

            ContinueButton.IsEnabled = paused;
            StepButton.IsEnabled = designer != null;
        }

        private void RunList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var row = RunList.SelectedItem as RunRow;
            pinnedInstanceId = row == null ? "" : row.InstanceId;
            lastSignature = "";     // paksa gambar ulang detailnya
            Sync();
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            var main = Main;
            if (main != null) main.JakForgeDebugContinue();
        }

        private void Step_Click(object sender, RoutedEventArgs e)
        {
            var main = Main;
            if (main != null) main.JakForgeDebugStep();
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            var main = Main;
            if (main != null) main.JakForgeDebugStop();
        }

        private void Breakpoint_Click(object sender, RoutedEventArgs e)
        {
            var main = Main;
            if (main != null) main.JakForgeDebugToggleBreakpoint();
        }

        public class RunRow
        {
            public string InstanceId { get; set; }
            public string Name { get; set; }
            public string Detail { get; set; }
        }

        public class VariableRow
        {
            public string Name { get; set; }
            public string TypeName { get; set; }
            public string Value { get; set; }
        }
    }
}

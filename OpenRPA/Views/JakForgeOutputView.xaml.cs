using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using OpenRPA.Interfaces;

namespace OpenRPA.Views
{
    /// <summary>
    /// Panel Output: menampilkan baris log dengan penanda level, penghitung
    /// yang sekaligus menjadi saringan, dan kotak cari.
    ///
    /// Panel ini MENARIK isi dari JakForgeOutputFeed setiap beberapa ratus
    /// milidetik, bukan menunggu dikirimi tiap baris. Jalur "dikirimi" pernah
    /// dicoba dan hasilnya fatal: penulisan log terjadi ribuan kali saat Studio
    /// memuat, dan setiap baris memaksa pekerjaan di thread UI yang sedang
    /// sibuk, sehingga Studio berhenti di layar Loading.
    /// </summary>
    public partial class JakForgeOutputView : UserControl
    {
        private readonly DispatcherTimer timer = new DispatcherTimer(DispatcherPriority.Background);
        private long lastVersion = -1;
        private bool lastFilterDirty = true;

        public JakForgeOutputView()
        {
            InitializeComponent();

            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += (s, e) => Sync();

            Loaded += (s, e) => { timer.Start(); Sync(); };
            Unloaded += (s, e) => timer.Stop();
        }

        private void Sync()
        {
            try
            {
                var version = JakForgeOutputFeed.Version;
                if (version == lastVersion && !lastFilterDirty) return;

                lastVersion = version;
                lastFilterDirty = false;

                InfoLabel.Text = "Info " + JakForgeOutputFeed.InfoCount;
                WarnLabel.Text = "Warn " + JakForgeOutputFeed.WarnCount;
                ErrorLabel.Text = "Error " + JakForgeOutputFeed.ErrorCount;
                TraceLabel.Text = "Trace " + JakForgeOutputFeed.TraceCount;

                var needle = SearchBox == null ? "" : (SearchBox.Text ?? "").Trim();

                var rows = JakForgeOutputFeed.Snapshot()
                    .Where(Wanted)
                    .Where(r => needle.Length == 0 ||
                                (r.Message != null &&
                                 r.Message.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0))
                    .Take(500)   // yang terlihat cukup segini; sisanya tetap tersimpan
                    .ToList();

                Rows.ItemsSource = rows;
            }
            catch (Exception ex)
            {
                Log.Debug("JakForge output: " + ex.Message);
            }
        }

        private bool Wanted(OutputEntry entry)
        {
            if (entry == null) return false;

            switch (entry.Level)
            {
                case "Info": return ShowInfo.IsChecked == true;
                case "Warn": return ShowWarn.IsChecked == true;
                case "Error": return ShowError.IsChecked == true;
                default: return ShowTrace.IsChecked == true;
            }
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            lastFilterDirty = true;
            Sync();
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            JakForgeOutputFeed.Clear();
            lastFilterDirty = true;
            Sync();
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var rows = Rows.ItemsSource as System.Collections.Generic.IEnumerable<OutputEntry>;
                if (rows == null) return;

                var lines = rows.Select(r => string.Format("[{0}][{1}] {2}", r.TimeText, r.Level, r.Message));
                Clipboard.SetText(string.Join(Environment.NewLine, lines));
            }
            catch (Exception ex)
            {
                Log.Error("JakForge output: " + ex.ToString());
            }
        }
    }
}

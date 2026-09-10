using System;
using System.Linq;
using System.Windows;

namespace JakForge.Installer
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Mode pencopotan: dipanggil oleh entri di Settings > Apps, yang
            // menjalankan berkas ini dengan argumen /copot. Tanpa jendela
            // pemasangan sama sekali — cuma satu pertanyaan.
            if (e.Args.Any(a => string.Equals(a, "/copot", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(a, "/uninstall", StringComparison.OrdinalIgnoreCase)))
            {
                Copot();
                return;
            }

            new MainWindow().Show();
        }

        private void Copot()
        {
            var folder = Pemasang.FolderTerpasang();

            if (string.IsNullOrEmpty(folder))
            {
                MessageBox.Show("JakForge Studio tidak tercatat sebagai terpasang di akun ini.",
                    "JakForge Studio", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            var jalan = Pemasang.ProsesYangMengganggu();
            if (jalan.Length > 0)
            {
                MessageBox.Show(
                    "Tutup dulu: " + string.Join(", ", jalan) + ".\n\n"
                    + "Windows mengunci berkas yang sedang dipakai, jadi pencopotan akan "
                    + "meninggalkan sisa kalau dijalankan sekarang.",
                    "JakForge Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown();
                return;
            }

            var jawab = MessageBox.Show(
                "Copot JakForge Studio?\n\n"
                + "Yang dihapus: berkas program, pintasan, dan pendaftaran peramban.\n\n"
                + "Yang TIDAK dihapus: project Anda di " + Pemasang.FolderProject
                + " dan setelan di " + Pemasang.FolderData + ". "
                + "Keduanya berisi pekerjaan Anda, jadi penghapusannya sengaja diserahkan kepada Anda.",
                "JakForge Studio", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (jawab == MessageBoxResult.Yes)
            {
                Pemasang.Copot(folder);

                MessageBox.Show("JakForge Studio sudah dicopot.",
                    "JakForge Studio", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            Shutdown();
        }
    }
}

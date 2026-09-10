using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace JakForge.Installer
{
    public partial class MainWindow : Window
    {
        private readonly Pilihan _pilihan = new Pilihan();

        public MainWindow()
        {
            InitializeComponent();

            KotakFolder.Text = Pemasang.FolderBawaan;
            TeksVersi.Text = "versi " + Versi();

            // Kalau Studio sudah pernah dipasang, pasang ulang ke tempat yang
            // sama. Memasang ke folder kedua akan meninggalkan dua salinan
            // yang saling bersaing mendaftarkan native host yang sama.
            var terpasang = Pemasang.FolderTerpasang();
            if (!string.IsNullOrEmpty(terpasang)) KotakFolder.Text = terpasang;

            PeriksaProses();
        }

        private static string Versi()
        {
            try
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return v.Major + "." + v.Minor + "." + v.Build;
            }
            catch (Exception)
            {
                return "1.0.0";
            }
        }

        /// <summary>
        /// Memberi tahu di DEPAN kalau ada proses yang akan mengunci berkas,
        /// bukan membiarkan penyalinan gagal separuh jalan.
        /// </summary>
        private void PeriksaProses()
        {
            var jalan = Pemasang.ProsesYangMengganggu();

            if (jalan.Length == 0)
            {
                PeringatanProses.Visibility = Visibility.Collapsed;
                TombolPasang.IsEnabled = true;
                return;
            }

            TeksPeringatan.Text =
                "Masih berjalan: " + string.Join(", ", jalan) + ". "
                + "Windows mengunci berkas yang sedang dipakai, jadi tutup dulu semuanya — "
                + "kalau tidak, sebagian berkas akan tetap versi lama tanpa ada pesan yang menjelaskannya. "
                + "Setelah ditutup, tekan Pasang lagi.";

            PeringatanProses.Visibility = Visibility.Visible;
            TombolPasang.IsEnabled = true;
        }

        // ------------------------------------------------------------------
        // Bilah judul
        // ------------------------------------------------------------------

        private void BilahJudul_Seret(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void Tutup_Klik(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // ------------------------------------------------------------------
        // Pilihan folder
        // ------------------------------------------------------------------

        private void UbahFolder_Klik(object sender, RoutedEventArgs e)
        {
            // Dialog pemilih folder milik Windows dipanggil lewat COM Shell
            // terlambat, supaya pemasang ini tidak perlu referensi ke
            // System.Windows.Forms hanya demi satu kotak dialog.
            try
            {
                var t = Type.GetTypeFromProgID("Shell.Application");
                if (t == null) return;

                dynamic shell = Activator.CreateInstance(t);
                dynamic folder = shell.BrowseForFolder(0, "Pilih folder pemasangan", 0, KotakFolder.Text);

                if (folder != null) KotakFolder.Text = folder.Self.Path;
            }
            catch (Exception)
            {
                // Dialognya gagal muncul; kotak teksnya tetap bisa diketik
                // langsung, jadi tidak ada yang benar-benar buntu.
            }
        }

        // ------------------------------------------------------------------
        // Memasang
        // ------------------------------------------------------------------

        private async void Pasang_Klik(object sender, RoutedEventArgs e)
        {
            var jalan = Pemasang.ProsesYangMengganggu();
            if (jalan.Length > 0)
            {
                PeriksaProses();
                return;
            }

            if (!Pemasang.AdaMuatan())
            {
                TeksPeringatan.Text =
                    "Pemasang ini dibangun tanpa muatan, jadi tidak ada yang bisa dipasang. "
                    + "Jalankan buat-installer.ps1 untuk membangunnya dengan benar.";
                PeringatanProses.Visibility = Visibility.Visible;
                return;
            }

            _pilihan.FolderTujuan = KotakFolder.Text.Trim();
            _pilihan.PasangJembatanPeramban = CentangPeramban.IsChecked == true;
            _pilihan.PintasanDesktop = CentangDesktop.IsChecked == true;
            _pilihan.JalankanSetelahSelesai = CentangJalankan.IsChecked == true;
            _pilihan.Versi = Versi();

            LayarSambutan.Visibility = Visibility.Collapsed;
            LayarProses.Visibility = Visibility.Visible;
            TombolTutupJendela.IsEnabled = false;

            Exception gagal = null;

            await Task.Run(() =>
            {
                try
                {
                    var pemasang = new Pemasang((pesan, persen) =>
                        Dispatcher.Invoke(() => Kemajuan(pesan, persen)));

                    pemasang.Pasang(_pilihan);
                }
                catch (Exception ex)
                {
                    gagal = ex;
                }
            });

            TombolTutupJendela.IsEnabled = true;
            TampilkanSelesai(gagal);
        }

        private void Kemajuan(string pesan, int persen)
        {
            TeksLangkah.Text = pesan;
            TeksPersen.Text = persen + "%";

            // Lebar batang dihitung dari lebar induknya saat itu juga, bukan
            // dari nilai tetap: jendelanya boleh berubah ukuran suatu saat.
            var induk = (BatangProgres.Parent as FrameworkElement);
            if (induk != null && induk.ActualWidth > 0)
                BatangProgres.Width = induk.ActualWidth * persen / 100.0;
        }

        private void TampilkanSelesai(Exception gagal)
        {
            LayarProses.Visibility = Visibility.Collapsed;
            LayarSelesai.Visibility = Visibility.Visible;

            if (gagal != null)
            {
                JudulSelesai.Text = "Pemasangan gagal";
                RingkasanSelesai.Text = gagal.Message;
                KotakLangkahTerakhir.Visibility = Visibility.Collapsed;
                TombolBukaEkstensi.Visibility = Visibility.Collapsed;
                return;
            }

            JudulSelesai.Text = "JakForge Studio terpasang";

            var hasil = _pilihan.HasilPeramban;
            var baris = new System.Text.StringBuilder();

            baris.AppendLine("Program   : " + _pilihan.FolderTujuan);
            baris.AppendLine("Setelan   : " + Pemasang.FolderData);
            baris.Append("Project   : " + Pemasang.FolderProject + "\\<Nama Project>");

            RingkasanSelesai.Text = baris.ToString();

            if (!_pilihan.PasangJembatanPeramban || hasil.EkstensiTerdaftar.Count == 0)
            {
                KotakLangkahTerakhir.Visibility = Visibility.Collapsed;
                TombolBukaEkstensi.Visibility = Visibility.Collapsed;
                return;
            }

            TeksLangkahTerakhir.Text =
                "Jembatan native host sudah terdaftar untuk "
                + string.Join(", ", hasil.PerambanTerdaftar) + ", dan ekstensinya sudah didaftarkan ke "
                + string.Join(", ", hasil.EkstensiTerdaftar) + ".\n\n"
                + "1. TUTUP peramban Anda sepenuhnya, lalu buka lagi.\n"
                + "2. Ia menampilkan \"Ekstensi baru ditambahkan\" — tekan Aktifkan.\n\n"
                + "Konfirmasi itu tidak bisa dilewati tanpa hak administrator, dan memang "
                + "disengaja: kalau ada jalannya, program apa pun bisa menanam ekstensi di "
                + "peramban Anda tanpa sepengetahuan Anda.\n\n"

                // Jalan cadangan ditulis DI SINI, bukan di dokumentasi.
                //
                // Ada satu keadaan yang membuat pemasangan otomatis gagal diam-diam:
                // kalau ekstensi dengan ID yang sama pernah dipasang lalu dibuang
                // sendiri oleh pengguna di komputer itu, peramban menolak
                // memasangnya kembali lewat registry — tanpa pesan apa pun. Orang
                // yang mengalaminya tidak punya cara menebak apa yang salah, dan
                // dokumentasi yang tidak dibuka tidak menolong siapa pun.
                + "KALAU SETELAH ITU TIDAK MUNCUL JUGA\n"
                + "Buka chrome://extensions, nyalakan Developer mode, tekan "
                + "\"Load unpacked\", lalu pilih folder:\n"
                + System.IO.Path.Combine(_pilihan.FolderTujuan, "Extension") + "\n\n"
                + "Atau jalankan dari folder pemasangan:\n"
                + "  powershell -ExecutionPolicy Bypass -File pasang-jembatan-chrome.ps1 -Periksa";
        }

        private void BukaEkstensi_Klik(object sender, RoutedEventArgs e)
        {
            // Dicoba berurutan, bukan hanya Chrome: komputer tujuan belum tentu
            // punya Chrome, dan tombol yang tidak melakukan apa-apa lebih buruk
            // daripada tombol yang tidak ada.
            foreach (var peramban in new[] { "chrome.exe", "msedge.exe", "brave.exe" })
            {
                try
                {
                    Process.Start(peramban, "chrome://extensions");
                    return;
                }
                catch (Exception)
                {
                }
            }

            // Tidak ada peramban yang bisa dijalankan; buka saja foldernya supaya
            // "Load unpacked" tinggal menempel jalurnya.
            try
            {
                var folder = System.IO.Path.Combine(_pilihan.FolderTujuan, "Extension");
                if (System.IO.Directory.Exists(folder)) Process.Start("explorer.exe", folder);
            }
            catch (Exception)
            {
            }
        }

        private void Selesai_Klik(object sender, RoutedEventArgs e)
        {
            if (_pilihan.JalankanSetelahSelesai)
            {
                try
                {
                    var exe = Path.Combine(_pilihan.FolderTujuan, "OpenRPA.exe");
                    if (File.Exists(exe))
                        Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = _pilihan.FolderTujuan });
                }
                catch (Exception) { }
            }

            Close();
        }
    }
}

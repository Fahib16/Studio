using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Microsoft.Win32;

namespace JakForge.Installer
{
    /// <summary>
    /// Pemasangan JakForge Studio, seluruhnya di dalam profil pengguna.
    ///
    /// KENAPA TIDAK BUTUH ADMIN
    ///
    /// Satu-satunya alasan pemasang biasa menuntut hak administrator adalah
    /// karena ia menulis ke Program Files dan HKEY_LOCAL_MACHINE. Pemasang ini
    /// tidak menyentuh keduanya:
    ///
    ///   berkas program   %LOCALAPPDATA%\Programs\JakForge Studio
    ///   data & setelan   %LOCALAPPDATA%\JakForge\Studio
    ///   project          Documents\JakForge
    ///   pendaftaran      HKEY_CURRENT_USER
    ///   pintasan         Start Menu milik pengguna
    ///
    /// Semuanya milik pengguna yang sedang login, jadi tidak ada satu langkah
    /// pun yang memunculkan permintaan elevasi.
    /// </summary>
    internal class Pemasang
    {
        public const string NamaProduk = "JakForge Studio";
        public const string KunciUninstall = "JakForgeStudio";

        /// <summary>Nama berkas .exe utama di dalam muatan.</summary>
        private const string ExeUtama = "OpenRPA.exe";

        private readonly Action<string, int> _lapor;

        public Pemasang(Action<string, int> lapor)
        {
            _lapor = lapor ?? ((p, s) => { });
        }

        // ------------------------------------------------------------------
        // Tempat
        // ------------------------------------------------------------------

        public static string FolderBawaan
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", NamaProduk);
            }
        }

        public static string FolderData
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "JakForge", "Studio");
            }
        }

        public static string FolderProject
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "JakForge");
            }
        }

        // ------------------------------------------------------------------
        // Pemeriksaan sebelum mulai
        // ------------------------------------------------------------------

        /// <summary>
        /// Nama proses Studio yang sedang berjalan, kalau ada.
        ///
        /// Diperiksa DI DEPAN, bukan saat penyalinan gagal di tengah jalan.
        /// Windows mengunci .exe dan .dll yang sedang dipakai, jadi memasang
        /// di atas Studio yang sedang jalan menghasilkan pemasangan separuh:
        /// sebagian berkas baru, sebagian lama, dan tidak ada pesan yang
        /// menjelaskan kenapa program jadi aneh sesudahnya.
        /// </summary>
        public static string[] ProsesYangMengganggu()
        {
            var nama = new[] { "OpenRPA", "JakRunner", "Studio.NativeHost" };
            var jalan = new List<string>();

            foreach (var n in nama)
            {
                try
                {
                    if (System.Diagnostics.Process.GetProcessesByName(n).Length > 0) jalan.Add(n);
                }
                catch (Exception)
                {
                }
            }

            return jalan.ToArray();
        }

        public static bool AdaMuatan()
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
            {
                return s != null;
            }
        }

        // ------------------------------------------------------------------
        // Memasang
        // ------------------------------------------------------------------

        public void Pasang(Pilihan pilihan)
        {
            _lapor("Menyiapkan folder…", 5);
            Directory.CreateDirectory(pilihan.FolderTujuan);
            Directory.CreateDirectory(FolderData);
            Directory.CreateDirectory(FolderProject);

            _lapor("Menyalin berkas program…", 10);
            BongkarMuatan(pilihan.FolderTujuan);

            if (pilihan.PasangJembatanPeramban)
            {
                _lapor("Mendaftarkan jembatan peramban…", 70);
                var jembatan = new JembatanPeramban(pilihan.FolderTujuan);
                pilihan.HasilPeramban = jembatan.Daftarkan();
            }

            _lapor("Membuat pintasan…", 85);
            BuatPintasan(pilihan);

            _lapor("Mendaftarkan entri uninstall…", 92);
            DaftarkanUninstall(pilihan);

            _lapor("Selesai.", 100);
        }

        private void BongkarMuatan(string tujuan)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
            {
                if (stream == null)
                    throw new InvalidOperationException(
                        "Pemasang ini dibangun tanpa muatan. Jalankan buat-installer.ps1 untuk membangunnya dengan benar.");

                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    var total = zip.Entries.Count;
                    var n = 0;

                    foreach (var entry in zip.Entries)
                    {
                        n++;

                        // Entri yang namanya berakhir '/' adalah folder kosong.
                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            Directory.CreateDirectory(Path.Combine(tujuan, entry.FullName));
                            continue;
                        }

                        var berkas = Path.Combine(tujuan, entry.FullName);

                        // Penjagaan terhadap entri yang jalurnya keluar dari
                        // folder tujuan ("zip slip"). Muatan ini kita sendiri
                        // yang membuatnya, jadi seharusnya tidak pernah terjadi
                        // — tapi "seharusnya" bukan alasan untuk membiarkan
                        // sebuah arsip menulis ke mana pun di disk.
                        var penuh = Path.GetFullPath(berkas);
                        var akar = Path.GetFullPath(tujuan);
                        if (!penuh.StartsWith(akar, StringComparison.OrdinalIgnoreCase)) continue;

                        Directory.CreateDirectory(Path.GetDirectoryName(penuh));
                        entry.ExtractToFile(penuh, true);

                        if (n % 25 == 0)
                            _lapor("Menyalin berkas program… (" + n + "/" + total + ")",
                                   10 + (int)(55.0 * n / total));
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // Pintasan
        // ------------------------------------------------------------------

        private void BuatPintasan(Pilihan pilihan)
        {
            var target = Path.Combine(pilihan.FolderTujuan, ExeUtama);
            if (!File.Exists(target)) return;

            var startMenu = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Programs), "JakForge");
            Directory.CreateDirectory(startMenu);

            BuatSatuPintasan(Path.Combine(startMenu, NamaProduk + ".lnk"), target, pilihan.FolderTujuan);

            if (pilihan.PintasanDesktop)
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                BuatSatuPintasan(Path.Combine(desktop, NamaProduk + ".lnk"), target, pilihan.FolderTujuan);
            }
        }

        /// <summary>
        /// Membuat berkas .lnk lewat COM WScript.Shell, dipanggil terlambat
        /// (late binding) supaya tidak perlu referensi COM interop apa pun.
        ///
        /// Tanpa pustaka tambahan, dan tanpa menulis format .lnk sendiri —
        /// format itu tidak berdokumen resmi dan salah satu bidangnya
        /// menyimpan jalur dalam dua bentuk sekaligus.
        /// </summary>
        private static void BuatSatuPintasan(string berkasLnk, string target, string kerja)
        {
            try
            {
                var t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) return;

                dynamic shell = Activator.CreateInstance(t);
                dynamic lnk = shell.CreateShortcut(berkasLnk);

                lnk.TargetPath = target;
                lnk.WorkingDirectory = kerja;
                lnk.IconLocation = target + ",0";
                lnk.Description = NamaProduk;
                lnk.Save();
            }
            catch (Exception)
            {
                // Pintasan yang gagal dibuat bukan alasan menggagalkan
                // pemasangan; programnya sudah terpasang dan bisa dijalankan
                // dari foldernya.
            }
        }

        // ------------------------------------------------------------------
        // Entri "Apps & features"
        // ------------------------------------------------------------------

        private void DaftarkanUninstall(Pilihan pilihan)
        {
            try
            {
                // HKCU, bukan HKLM: entri per-pengguna muncul di Settings >
                // Apps tanpa perlu hak administrator sama sekali.
                using (var k = Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + KunciUninstall))
                {
                    if (k == null) return;

                    var exe = Path.Combine(pilihan.FolderTujuan, ExeUtama);

                    k.SetValue("DisplayName", NamaProduk);
                    k.SetValue("DisplayVersion", pilihan.Versi ?? "1.0.0");
                    k.SetValue("Publisher", "JakForge");
                    k.SetValue("InstallLocation", pilihan.FolderTujuan);
                    k.SetValue("DisplayIcon", exe);
                    k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    k.SetValue("EstimatedSize", UkuranKb(pilihan.FolderTujuan), RegistryValueKind.DWord);

                    var pencopot = Path.Combine(pilihan.FolderTujuan, "JakForgeStudioSetup.exe");
                    k.SetValue("UninstallString", "\"" + pencopot + "\" /copot");
                }

                // Pemasang menyalin dirinya sendiri ke folder tujuan supaya
                // pencopotan tetap mungkin sesudah berkas setup aslinya
                // dihapus dari Downloads — dan itu selalu terjadi.
                var asal = Assembly.GetExecutingAssembly().Location;
                var salinan = Path.Combine(pilihan.FolderTujuan, "JakForgeStudioSetup.exe");

                if (!string.Equals(asal, salinan, StringComparison.OrdinalIgnoreCase))
                    File.Copy(asal, salinan, true);
            }
            catch (Exception)
            {
            }
        }

        private static int UkuranKb(string folder)
        {
            try
            {
                long total = 0;
                foreach (var f in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                    total += new FileInfo(f).Length;

                return (int)(total / 1024);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // ------------------------------------------------------------------
        // Mencopot
        // ------------------------------------------------------------------

        /// <summary>
        /// Mencopot pemasangan. Yang DIHAPUS hanya berkas program dan
        /// pendaftaran; data dan project TIDAK disentuh.
        ///
        /// Itu pilihan sadar. Documents\JakForge berisi pekerjaan orang, dan
        /// pencopot yang menghapusnya diam-diam adalah pencopot yang hanya
        /// perlu salah sekali untuk menghancurkan berbulan-bulan kerja.
        /// </summary>
        public static void Copot(string folderTujuan)
        {
            try { new JembatanPeramban(folderTujuan).BatalkanPendaftaran(); } catch (Exception) { }

            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(
                    @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + KunciUninstall, false);
            }
            catch (Exception) { }

            try
            {
                var startMenu = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs), "JakForge");

                if (Directory.Exists(startMenu)) Directory.Delete(startMenu, true);

                var desktop = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    NamaProduk + ".lnk");

                if (File.Exists(desktop)) File.Delete(desktop);
            }
            catch (Exception) { }

            try
            {
                // Berkas .exe pencopot ini sendiri sedang berjalan dari folder
                // yang mau dihapus, jadi ia tidak bisa menghapus dirinya. Yang
                // lain dihapus sekarang; sisanya dititipkan ke cmd yang jalan
                // sesudah proses ini keluar.
                var sendiri = Assembly.GetExecutingAssembly().Location;

                foreach (var f in Directory.GetFiles(folderTujuan, "*", SearchOption.AllDirectories))
                {
                    if (string.Equals(f, sendiri, StringComparison.OrdinalIgnoreCase)) continue;
                    try { File.Delete(f); } catch (Exception) { }
                }

                // ping, BUKAN timeout, dan itu bukan selera.
                //
                // `timeout` membaca handle konsol untuk mendeteksi penekanan
                // tombol. Proses ini dijalankan dengan CreateNoWindow, jadi
                // tidak ada konsol, dan timeout langsung gagal dengan "Input
                // redirection is not supported". Operator `&` tetap menjalankan
                // perintah berikutnya, jadi rmdir berjalan SEKETIKA — saat
                // .exe pencopot masih hidup dan masih mengunci dirinya sendiri.
                // Akibatnya folder tidak terhapus dan berkas 100+ MB tertinggal
                // selamanya, sementara semua yang lain terlihat berhasil.
                //
                // `ping 127.0.0.1 -n 3` menunggu ~2 detik tanpa menyentuh
                // konsol sama sekali.
                // Perulangan, bukan dua percobaan bernomor.
                //
                // Berapa lama proses ini masih hidup TIDAK bisa ditebak: sesudah
                // memanggil ini, ia masih menampilkan kotak "sudah dicopot" dan
                // menunggu orangnya menekan OK — bisa dua detik, bisa dua menit.
                // Selama itu .exe-nya terkunci dan foldernya tidak bisa dihapus.
                // Percobaan berjadwal tetap akan meleset; yang diperlukan adalah
                // mencoba terus sampai berhasil.
                //
                // rmdir yang berhasil membuat foldernya lenyap, jadi putaran
                // berikutnya tidak mengerjakan apa-apa. Sepuluh putaran dengan
                // jeda dua detik memberi kelonggaran sekitar 20 detik.
                var perintah =
                    "/c for /l %i in (1,1,10) do ("
                    + "ping 127.0.0.1 -n 3 >nul"
                    + " & rmdir /s /q \"" + folderTujuan + "\" 2>nul"
                    + ")";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = perintah,
                    CreateNoWindow = true,
                    UseShellExecute = false,

                    // WorkingDirectory WAJIB di luar folder yang mau dihapus.
                    //
                    // Proses anak mewarisi current directory induknya, dan
                    // induknya berjalan DARI folder yang sedang dicopot. Sebuah
                    // folder tidak bisa dihapus selama ada proses yang
                    // menjadikannya current directory — jadi rmdir berhasil
                    // mengosongkan isinya lalu gagal menghapus foldernya
                    // sendiri, diam-diam, dan meninggalkan folder kosong.
                    WorkingDirectory = Path.GetTempPath()
                });
            }
            catch (Exception) { }
        }

        public static string FolderTerpasang()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + KunciUninstall))
                {
                    if (k == null) return null;
                    return k.GetValue("InstallLocation") as string;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    internal class Pilihan
    {
        public string FolderTujuan = Pemasang.FolderBawaan;
        public bool PasangJembatanPeramban = true;
        public bool PintasanDesktop = true;
        public bool JalankanSetelahSelesai = true;
        public string Versi;

        /// <summary>Diisi pemasang: peramban mana yang berhasil didaftarkan.</summary>
        public HasilPendaftaran HasilPeramban = new HasilPendaftaran();
    }
}

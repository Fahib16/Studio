using System;
using System.Windows;
using OpenRPA.Interfaces;

namespace OpenRPA.Views
{
    /// <summary>
    /// Pengatur perpindahan antara layar Home (daftar proyek) dan editor kanvas,
    /// mengikuti cara UiPath Studio bekerja.
    ///
    /// Aturannya:
    ///
    ///   * Sesudah layar Loading, yang muncul HANYA layar Home. Jendela kanvas
    ///     sudah dimuat di belakang layar tetapi disembunyikan, karena
    ///     pemuatannya memakan waktu dan sebagian besar penyiapan Studio
    ///     bergantung padanya.
    ///
    ///   * Kanvas baru ditampilkan saat sebuah proyek dibuka atau dibuat.
    ///
    ///   * Tombol Home di bilah atas mengembalikan tampilan ke layar Home tanpa
    ///     menutup apa pun; pekerjaan yang sedang dibuka tetap terbuka.
    ///
    ///   * Menutup layar Home saat kanvas belum pernah tampil berarti keluar
    ///     dari aplikasi. Kalau kanvas sudah tampil, menutup Home cuma
    ///     menyembunyikannya.
    /// </summary>
    internal static class JakForgeShell
    {
        private static JakForgeDashboardWindow home;

        /// <summary>Apakah editor kanvas sudah pernah ditampilkan di sesi ini.</summary>
        public static bool CanvasShown { get; private set; }

        /// <summary>
        /// Munculkan sebuah jendela dengan memudarkannya masuk.
        ///
        /// Yang dianimasikan opasitas ISI jendela, bukan opasitas jendelanya.
        /// Window.Opacity pada jendela biasa dikerjakan Windows lewat
        /// SetLayeredWindowAttributes — per bingkai, di CPU, untuk seluruh luas
        /// jendela. Opasitas elemen di dalamnya digabung di kartu grafis, jadi
        /// jauh lebih halus dan tidak menyentak di jendela sebesar ini.
        /// </summary>
        private static void FadeIn(Window window, double milliseconds)
        {
            if (window == null) return;

            try
            {
                var content = window.Content as System.Windows.UIElement;

                if (content == null)
                {
                    window.Show();
                    return;
                }

                content.Opacity = 0;
                window.Show();

                var fade = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(milliseconds),
                    EasingFunction = new System.Windows.Media.Animation.QuadraticEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                    }
                };

                content.BeginAnimation(System.Windows.UIElement.OpacityProperty, fade);
            }
            catch (Exception)
            {
                // Animasi gagal TIDAK BOLEH membuat jendelanya ikut tidak
                // tampil — itu akan meninggalkan pemakainya tanpa layar sama
                // sekali.
                try
                {
                    var content = window.Content as System.Windows.UIElement;
                    if (content != null)
                    {
                        content.BeginAnimation(System.Windows.UIElement.OpacityProperty, null);
                        content.Opacity = 1;
                    }
                    window.Show();
                }
                catch (Exception) { }
            }
        }

        /// <summary>Menampilkan layar Home dan menyembunyikan kanvas.</summary>
        public static void ShowHome()
        {
            var main = MainWindow.instance;

            try
            {
                if (home == null || !home.IsLoaded)
                {
                    home = new JakForgeDashboardWindow();
                    home.Closed += (s, e) => { home = null; };
                }
                else
                {
                    home.RefreshProjects();
                }

                // Kanvas baru disembunyikan SESUDAH Home mulai tampil, supaya
                // tidak ada momen tanpa jendela sama sekali di antara keduanya.
                if (home.IsVisible) { home.Show(); home.Activate(); }
                else { FadeIn(home, 200); home.Activate(); }

                if (main != null) main.Visibility = Visibility.Hidden;
            }
            catch (Exception ex)
            {
                Log.Error("JakForgeShell.ShowHome: " + ex.ToString());

                // Kalau layar Home gagal tampil, jangan tinggalkan pemakainya
                // tanpa jendela sama sekali.
                if (main != null) main.Visibility = Visibility.Visible;
            }
        }

        /// <summary>Menampilkan editor kanvas dan menyembunyikan layar Home.</summary>
        public static void ShowCanvas()
        {
            var main = MainWindow.instance;
            if (main == null) return;

            try
            {
                CanvasShown = true;

                main.Visibility = Visibility.Visible;
                main.Activate();
                main.JakForgeFadeInContent();

                // Home disembunyikan pada dispatcher berikutnya, setelah kanvas
                // benar-benar tergambar — kalau langsung, layar sempat kosong.
                if (home != null)
                {
                    var closing = home;
                    main.Dispatcher.BeginInvoke(new Action(() => { try { closing.Hide(); } catch (Exception) { } }),
                        System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }
            catch (Exception ex)
            {
                Log.Error("JakForgeShell.ShowCanvas: " + ex.ToString());
            }
        }

        /// <summary>
        /// Dipanggil saat layar Home ditutup. Selama kanvas belum pernah tampil,
        /// layar Home adalah satu-satunya jendela yang dilihat orang, jadi
        /// menutupnya berarti menutup aplikasi.
        /// </summary>
        /// <summary>
        /// Penanda bahwa perintah keluar sudah dijalankan.
        ///
        /// WAJIB ada: Application.Shutdown() menutup semua jendela, dan
        /// penutupan itu memicu OnClosing layar Home LAGI. Tanpa penanda ini,
        /// Shutdown memanggil dirinya sendiri terus-menerus di tumpukan yang
        /// sama sampai StackOverflowException — jenis kegagalan yang tidak bisa
        /// ditangkap, mematikan proses seketika, dan membuat Windows Error
        /// Reporting membekukan seluruh thread sehingga aplikasi tampak
        /// "menggantung" di layar Loading.
        /// </summary>
        private static bool shuttingDown;

        public static void HomeClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (CanvasShown)
            {
                // Kanvas masih terbuka di belakang: cukup sembunyikan Home,
                // supaya tombol Home nanti membukanya lagi dengan cepat.
                e.Cancel = true;
                if (home != null) home.Hide();
                var main = MainWindow.instance;
                if (main != null)
                {
                    main.Visibility = Visibility.Visible;
                    main.Activate();
                }
                return;
            }

            if (shuttingDown) return;
            shuttingDown = true;

            try { Application.Current.Shutdown(); }
            catch (Exception) { }
        }
    }
}

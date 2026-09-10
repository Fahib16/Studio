using System;
using System.Windows;

namespace OpenRPA.Views
{
    /// <summary>
    /// Layar Loading JakForge.
    ///
    /// Dipakai lewat tiga method statis saja (Show/SetStatus/CloseIfOpen)
    /// supaya pemanggilnya tidak perlu memegang referensi window-nya sendiri.
    ///
    /// CATATAN PENTING soal ShutdownMode="OnMainWindowClose":
    /// WPF menjadikan window PERTAMA yang dibuat sebagai Application.MainWindow.
    /// Karena layar ini muncul sebelum MainWindow, tanpa penanganan khusus
    /// menutupnya akan mematikan seluruh aplikasi. Karena itu ShowSplash()
    /// mengingat MainWindow sebelumnya dan CloseIfOpen() hanya menutup dirinya
    /// setelah jendela utama yang asli sudah terpasang (lihat pemanggilan di
    /// MainWindow.Window_Loaded).
    /// </summary>
    public partial class JakForgeLoadingWindow : Window
    {
        private static JakForgeLoadingWindow current;

        /// <summary>Ukuran rancangan, sama dengan kanvas di dalam Viewbox.</summary>
        private const double DesignWidth = 520;
        private const double DesignHeight = 740;

        public JakForgeLoadingWindow()
        {
            InitializeComponent();

            // Di layar yang lebih pendek dari tinggi rancangan, jendela ini
            // akan menggantung keluar layar kalau ukurannya dibiarkan tetap.
            // Kecilkan proporsional supaya selalu muat utuh.
            var work = SystemParameters.WorkArea;
            var scale = Math.Min(1.0, (work.Height * 0.92) / DesignHeight);
            Height = DesignHeight * scale;
            Width = DesignWidth * scale;
        }

        /// <summary>
        /// Thread khusus tempat layar Loading hidup.
        ///
        /// Animasinya HARUS berada di thread lain. Kalau layar ini dijalankan
        /// di thread UI yang sama dengan penyiapan Studio, animasinya berhenti
        /// setiap kali thread itu sibuk memuat plugin, proyek, dan toolbox —
        /// dan justru saat itulah layar Loading sedang tampil. Hasilnya batang
        /// progres dan cincin titik bergerak tersendat.
        /// </summary>
        private static System.Threading.Thread thread;

        public static void ShowSplash()
        {
            if (current != null) return;
            if (Application.Current == null) return;

            var ready = new System.Threading.ManualResetEventSlim(false);

            thread = new System.Threading.Thread(() =>
            {
                try
                {
                    current = new JakForgeLoadingWindow();
                    current.Show();
                    ready.Set();
                    System.Windows.Threading.Dispatcher.Run();
                }
                catch (Exception)
                {
                    ready.Set();
                }
            });

            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.IsBackground = true;
            thread.Name = "JakForgeSplash";
            thread.Start();

            // Tunggu sebentar sampai jendelanya benar-benar tampil, supaya
            // jendela utama tidak keburu muncul lebih dulu.
            ready.Wait(TimeSpan.FromSeconds(5));
        }

        /// <summary>
        /// Dipanggil jendela utama yang asli begitu selesai dimuat: merebut
        /// kembali status Application.MainWindow dari splash, lalu menutup
        /// splash. Urutannya penting, kebalikannya mematikan aplikasi.
        /// </summary>
        public static void HandOverTo(Window realMainWindow)
        {
            var app = Application.Current;
            if (app != null && realMainWindow != null) app.MainWindow = realMainWindow;
            CloseIfOpen();
        }

        public static void SetStatus(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var w = current;
            if (w == null) return;

            // Layar ini hidup di thread-nya sendiri, jadi perubahannya harus
            // dikirim ke dispatcher MILIK JENDELA ITU, bukan dispatcher
            // aplikasi.
            try
            {
                w.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (current != null) current.StatusText.Text = text;
                }));
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Menutup layar Loading dengan memudarkannya lebih dulu, bukan
        /// menghilangkannya seketika: perpindahan ke dashboard jadi tidak
        /// terasa menyentak.
        /// </summary>
        public static void CloseIfOpen()
        {
            var w = current;
            if (w == null) return;
            current = null;

            try
            {
                w.Dispatcher.BeginInvoke(new Action(() =>
                {
                    Action shutdown = () =>
                    {
                        try { w.Close(); } catch (Exception) { }

                        // Thread splash punya dispatcher sendiri; tanpa ini
                        // thread-nya tetap hidup sampai aplikasi ditutup.
                        try { w.Dispatcher.InvokeShutdown(); } catch (Exception) { }
                    };

                    try
                    {
                        var fade = new System.Windows.Media.Animation.DoubleAnimation
                        {
                            To = 0,
                            Duration = TimeSpan.FromMilliseconds(180),
                            EasingFunction = new System.Windows.Media.Animation.QuadraticEase
                            {
                                EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn
                            }
                        };
                        fade.Completed += (s, e) => shutdown();
                        w.BeginAnimation(OpacityProperty, fade);
                    }
                    catch (Exception)
                    {
                        shutdown();
                    }
                }));
            }
            catch (Exception)
            {
            }
        }
    }
}

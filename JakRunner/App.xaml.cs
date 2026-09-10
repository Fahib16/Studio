using System;
using System.IO;
using System.Text;
using System.Windows;

namespace JakRunner
{
    /// <summary>
    /// JakRunner: asisten JakForge untuk menjalankan automasi attended.
    ///
    /// Aplikasi ini BERDIRI SENDIRI. Ia tidak memuat Studio, tidak memakai
    /// RobotInstance, dan tidak butuh Studio terpasang — daftar automasinya
    /// dibaca langsung dari folder proyek, dan menjalankannya memakai runtime
    /// Workflow Foundation miliknya sendiri.
    ///
    /// Sebelumnya "asisten" hanyalah jendela lain di dalam Studio, dipilih lewat
    /// tanda isagent di setting.json. Akibatnya menjalankan asisten berarti
    /// memuat seluruh Studio — pita, kanvas, designer — untuk sesuatu yang cuma
    /// perlu menekan tombol Play.
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            InstallCrashLog();

            // Tema mengikuti setelan bersama yang ditulis Studio. Diterapkan
            // SEBELUM jendela pertama dibuat, supaya tidak ada kedipan terang
            // sepersekian detik sebelum berganti gelap.
            Core.RunnerTheming.Apply();
        }

        /// <summary>
        /// Kegagalan yang tidak tertangani ditulis ke berkas, bukan hanya
        /// menutup jendela tanpa jejak.
        /// </summary>
        private void InstallCrashLog()
        {
            DispatcherUnhandledException += (s, e) =>
            {
                Write("DispatcherUnhandledException", e.Exception);
                MessageBox.Show(e.Exception.Message, "JakRunner", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                Write("UnhandledException", e.ExceptionObject as Exception);
        }

        private static void Write(string source, Exception ex)
        {
            try
            {
                var text = new StringBuilder();
                text.AppendLine("[" + DateTime.Now.ToString("O") + "] " + source);

                var current = ex;
                while (current != null)
                {
                    text.AppendLine(current.GetType().FullName + ": " + current.Message);
                    text.AppendLine(current.StackTrace);
                    current = current.InnerException;
                    if (current != null) text.AppendLine("--- penyebab ---");
                }

                text.AppendLine();

                var folder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                File.AppendAllText(Path.Combine(folder, "jakrunner-crash.log"), text.ToString());
            }
            catch (Exception)
            {
            }
        }
    }
}

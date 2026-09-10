using OpenRPA.Interfaces;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace OpenRPA
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, ISingleInstanceApp
    {
        [STAThread]
        public static void Main()
        {
            if (SingleInstance<App>.InitializeAsFirstInstance("OpenRPA"))
            {
                AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
                // AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceHandler;
                try
                {
                    var args = Environment.GetCommandLineArgs();
                    CommandLineParser parser = new CommandLineParser();
                    // parser.Parse(string.Join(" ", args), true);
                    var options = parser.Parse(args, true);
                    if (options.ContainsKey("workingdir"))
                    {
                        var filepath = options["workingdir"].ToString();
                        if (System.IO.Directory.Exists(filepath))
                        {
                            Log.ResetLogPath(filepath);
                        }
                        else
                        {
                            MessageBox.Show("Path not found " + filepath);
                            return;
                        }
                    }
                }
                catch (Exception)
                {
                }
                var application = new App();
                application.InitializeComponent();
                application.Run();
                // application.Run();
                // Allow single instance code to perform cleanup operations
                SingleInstance<App>.Cleanup();
            }
        }
        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            Log.Function("MainWindow", "CurrentDomain_UnhandledException");
            try
            {
                Exception ex = (Exception)args.ExceptionObject;
                Log.Error(ex.ToString());
                Log.Error("MyHandler caught : " + ex.Message);
                Log.Error("Runtime terminating: {0}", (args.IsTerminating).ToString());
            }
            catch (Exception)
            {
            }
        }
        public static System.Windows.Forms.NotifyIcon notifyIcon { get; set; } = new System.Windows.Forms.NotifyIcon();
        // --- TAMBAHKAN BLOK INI UNTUK MENGGANTIKAN GENERATE OTOMATIS VISUAL STUDIO ---

        public App()
        {
            // PALING DULU, sebelum baris apa pun yang menyentuh Config.local.
            //
            // Config.local membaca settings.json saat pertama kali disentuh,
            // dan kalau berkas itu belum sempat disalin dari tata letak lama,
            // yang terbaca adalah setelan bawaan — lalu setelan bawaan itulah
            // yang tersimpan, dan setelan lama pengguna terkubur tanpa pernah
            // terbaca sekali pun.
            Interfaces.Extensions.PindahkanDataLamaKalauPerlu();

            if (!string.IsNullOrEmpty(Config.local.culture))
            {
                try
                {
                    var cultur = System.Globalization.CultureInfo.GetCultureInfo(Config.local.culture);
                    System.Threading.Thread.CurrentThread.CurrentUICulture = cultur;
                    System.Globalization.CultureInfo.DefaultThreadCurrentCulture = cultur;
                    System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = cultur;
                    ProcessThreadCollection currentThreads = Process.GetCurrentProcess().Threads;
                    foreach (object obj in currentThreads)
                    {
                        try
                        {
                            Thread t = obj as Thread;
                            if (t != null)
                            {
                                t.CurrentUICulture = cultur;
                                t.CurrentCulture = cultur;
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }


                }
                catch (Exception)
                {
                }
            }
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(LoadFromSameFolder);
            try
            {
                var iconStream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Resources/JakForge/jakforge.ico")).Stream;
                notifyIcon.Icon = new System.Drawing.Icon(iconStream);
                notifyIcon.Visible = false;
                //notifyIcon.ShowBalloonTip(5000, "Title", "Text", System.Windows.Forms.ToolTipIcon.Info);
                notifyIcon.Click += nIcon_Click;
                notifyIcon.DoubleClick += nIcon_Click;
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }
        private static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
        {
            foreach (DirectoryInfo dir in source.GetDirectories())
            {
                CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
            }

            foreach (FileInfo file in source.GetFiles())
            {
                file.CopyTo(System.IO.Path.Combine(target.FullName, file.Name));
            }
        }
        static Assembly LoadFromSameFolder(object sender, ResolveEventArgs args)
        {
            string assemblyPath = "";
            if (args != null && !string.IsNullOrEmpty(args.Name)) assemblyPath = args.Name;
            try
            {
                assemblyPath = new AssemblyName(args.Name).Name + ".dll";
            }
            catch (Exception)
            {
            }
            try
            {
                if (args.Name.StartsWith("CefSharp"))
                {
                    string assemblyName = args.Name.Split(new[] { ',' }, 2)[0] + ".dll";
                    string archSpecificPath = System.IO.Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase,
                                                           Environment.Is64BitProcess ? "x64" : "x86",
                                                           assemblyName);

                    return File.Exists(archSpecificPath)
                               ? Assembly.LoadFile(archSpecificPath)
                               : null;
                }
                string folderPath = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                assemblyPath = System.IO.Path.Combine(folderPath, new AssemblyName(args.Name).Name + ".dll");
                if (System.IO.File.Exists(assemblyPath)) return Assembly.LoadFrom(assemblyPath);

                folderPath = Interfaces.Extensions.PluginsDirectory;
                assemblyPath = System.IO.Path.Combine(folderPath, new AssemblyName(args.Name).Name + ".dll");
                if (System.IO.File.Exists(assemblyPath)) return Assembly.LoadFrom(assemblyPath);

                folderPath = Path.Combine(Interfaces.Extensions.DataDirectory, "extensions");
                assemblyPath = System.IO.Path.Combine(folderPath, new AssemblyName(args.Name).Name + ".dll");
                if (System.IO.File.Exists(assemblyPath)) return Assembly.LoadFrom(assemblyPath);

                folderPath = System.IO.Path.GetTempPath();
                assemblyPath = System.IO.Path.Combine(folderPath, new AssemblyName(args.Name).Name + ".dll");
                if (System.IO.File.Exists(assemblyPath)) return Assembly.LoadFrom(assemblyPath);
            }
            catch (Exception ex)
            {
                Log.Error(assemblyPath);
                Log.Error(ex.ToString());
            }
            return null;
        }
        /// <summary>
        /// Menulis kegagalan yang tidak tertangani ke sebuah berkas.
        ///
        /// Tanpa ini, kegagalan saat memuat jendela utama hanya meninggalkan
        /// catatan "XamlParseException" di Event Viewer — tanpa pesan, tanpa
        /// baris yang salah. Berkasnya berisi pesan lengkap beserta
        /// InnerException, yang biasanya justru penyebab sebenarnya.
        ///
        /// Lokasinya: [folder proyek]\jakforge-crash.log
        /// </summary>

        /// <summary>
        /// Jalankan JakRunner, asisten yang sekarang berdiri sendiri.
        ///
        /// Dicari di sebelah Studio: keduanya dipasang ke folder yang sama.
        /// Kalau tidak ada di sana, katakan apa adanya — jangan diam-diam
        /// membuka Studio, karena yang diminta orang tadi bukan Studio.
        /// </summary>
        private static void LaunchJakRunner()
        {
            try
            {
                var folder = System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
                var path = System.IO.Path.Combine(folder, "JakRunner.exe");

                if (!System.IO.File.Exists(path))
                {
                    MessageBox.Show(
                        "JakRunner.exe tidak ditemukan di " + folder + "." + Environment.NewLine + Environment.NewLine +
                        "Asisten sekarang aplikasi tersendiri, bukan lagi mode di dalam Studio.",
                        "JakRunner", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                System.Diagnostics.Process.Start(path);
            }
            catch (Exception ex)
            {
                Log.Error("Gagal menjalankan JakRunner: " + ex.Message);
            }
        }

        private void InstallCrashLog()
        {
            Action<string, object> write = (source, error) =>
            {
                try
                {
                    var text = new System.Text.StringBuilder();
                    text.AppendLine("==== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + source + "] ====");

                    var ex = error as Exception;
                    while (ex != null)
                    {
                        text.AppendLine(ex.GetType().FullName + ": " + ex.Message);
                        text.AppendLine(ex.StackTrace);
                        ex = ex.InnerException;
                        if (ex != null) text.AppendLine("---- InnerException ----");
                    }

                    if (error != null && !(error is Exception)) text.AppendLine(error.ToString());
                    text.AppendLine();

                    var folder = Interfaces.Extensions.DataDirectory;
                    if (string.IsNullOrEmpty(folder) || !System.IO.Directory.Exists(folder))
                        folder = System.IO.Path.GetTempPath();

                    System.IO.File.AppendAllText(
                        System.IO.Path.Combine(folder, "jakforge-crash.log"), text.ToString());
                }
                catch (Exception)
                {
                }
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) => write("AppDomain", e.ExceptionObject);
            DispatcherUnhandledException += (s, e) => write("Dispatcher", e.Exception);
        }

        void nIcon_Click(object sender, EventArgs e)
        {
            GenericTools.Restore();
        }
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if (notifyIcon != null)
            {
                if (notifyIcon.Icon != null) notifyIcon.Icon.Dispose();
                notifyIcon.Dispose();
            }
        }
        public bool SignalExternalCommandLineArgs(IList<string> args)
        {
            nIcon_Click(null, null);
            RobotInstance.instance.ParseCommandLineArgs(args);
            return true;
        }
        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                AutomationHelper.syncContext = System.Threading.SynchronizationContext.Current;
                System.Threading.Thread.CurrentThread.Name = "UIThread";

                InstallCrashLog();

                // Kurung dan kutip yang menutup sendiri di kotak ekspresi activity.
                Views.JakForgeAutoPair.Install();

                // Tema terang/gelap dari setelan bersama. Diterapkan SEBELUM
                // jendela pertama dibuat, supaya tidak ada kedipan terang
                // sepersekian detik sebelum berganti gelap.
                Views.JakForgeTheming.Apply();

                // Layar Loading JakForge tampil lebih dulu supaya jendela
                // pertama yang dilihat orang bukan jendela kosong.
                Views.JakForgeLoadingWindow.ShowSplash();

                // Studio adalah IDE MURNI.
                //
                // Dulu proses yang sama bisa menjadi asisten: kalau isagent
                // menyala di setting.json, StartupUri diarahkan ke AgentWindow.
                // Akibatnya menjalankan asisten berarti memuat seluruh Studio —
                // pita, kanvas, WF Designer — untuk sesuatu yang cuma perlu
                // menekan tombol Play.
                //
                // Asisten sekarang aplikasi tersendiri: JakRunner.exe. Argumen
                // --assistant tetap dikenali supaya pintasan lama tidak berujung
                // buntu; yang dilakukannya menjalankan JakRunner lalu menutup
                // Studio.
                if (e.Args.Contains("--assistant"))
                {
                    LaunchJakRunner();
                    Shutdown();
                    return;
                }

                StartupUri = new Uri("/OpenRPA;component/MainWindow.xaml", UriKind.Relative);
                notifyIcon.Visible = false;

                // ... (biarkan sisa kode di bawahnya tetap sama seperti aslinya)
                if (Config.local.files_pending_deletion.Length > 0)
                // ...
                {
                    bool sucess = true;
                    foreach (var f in Config.local.files_pending_deletion)
                    {
                        try
                        {
                            if (System.IO.File.Exists(f)) System.IO.File.Delete(f);
                        }
                        catch (Exception ex)
                        {
                            sucess = false;
                            Log.Error(ex.ToString());
                        }
                    }
                    if (sucess)
                    {
                        Config.local.files_pending_deletion = new string[] { };
                        Config.Save();
                    }
                }

                if (Config.local.restore_dependencies_on_startup)
                {
                    Log.Debug("Package restore on startup enabled -> cleaning existing extensions.");
                    var extensionsPath = Path.Combine(Interfaces.Extensions.DataDirectory, "extensions");
                    if (Directory.Exists(extensionsPath))
                    {
                        foreach (var file in Directory.GetFiles(extensionsPath))
                        {
                            try
                            {
                                File.Delete(file);
                            }
                            catch (Exception ex)
                            {
                                Log.Error("Could not clean extension: " + ex.ToString());
                            }
                        }
                    }
                }

                RobotInstance.instance.Status += App_Status;
                Input.InputDriver.Instance.initCancelKey(Config.local.cancelkey);
                Plugins.LoadPlugins(RobotInstance.instance, Interfaces.Extensions.PluginsDirectory, false);
                RobotInstance.instance.Initialize();
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                Console.WriteLine(ex.ToString());
                MessageBox.Show(ex.Message);
            }

            await Task.Run(async () =>
            {
                try
                {
                    // if (Config.local.showloadingscreen) splash.BusyContent = "loading plugins";
                    // Plugins.LoadPlugins(RobotInstance.instance, Interfaces.Extensions.ProjectsDirectory);
                    // if (Config.local.showloadingscreen) splash.BusyContent = "Initialize main window";
                    await RobotInstance.instance.init();
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString());
                    Console.WriteLine(ex.ToString());
                    MessageBox.Show(ex.Message);
                }
            });
        }
        private void App_Status(string message)
        {
            try
            {
                Log.Debug(message);
                Views.JakForgeLoadingWindow.SetStatus(message);
                // notifyIcon.ShowBalloonTip(5000, "Title", message, System.Windows.Forms.ToolTipIcon.Info);
                // if (splash != null) splash.BusyContent = message;
            }
            catch (Exception)
            {
            }
        }
    }
}

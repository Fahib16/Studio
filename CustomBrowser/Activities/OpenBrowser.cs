using System;
using System.Activities;
using System.ComponentModel;
using OpenRPA.Interfaces;
using OpenRPA.NM;

namespace Custom.Browser
{
    public enum OpenBrowserKind
    {
        Chrome,
        Firefox,
        Edge
    }

    public enum OpenBrowserUserDataFolderMode
    {
        Automatic,
        DefaultFolder,
        CustomFolder
    }

    /// <summary>
    /// Activity kustom: Open Browser ala UiPath untuk OpenRPA.
    ///
    /// Execute() logic-nya DISALIN PERSIS dari OpenURL.cs asli (namespace
    /// OpenRPA.NM) — bukan tebakan, itu activity bawaan yang sudah teruji.
    /// Bedanya: activity ini SEKARANG PUNYA Body (drop-zone), sesuai
    /// keputusan kamu untuk tampilan semirip mungkin dengan UiPath.
    ///
    /// PENTING — batasan yang perlu disadari:
    ///   - Body di sini SEKADAR VISUAL/pengelompokan. Activity anak (Click,
    ///     TypeInto, GetElement, dst) TIDAK menerima "browser" ini lewat
    ///     mekanisme resmi apa pun — mereka tetap resolve elemen sendiri-
    ///     sendiri lewat Selector masing-masing, sama seperti kalau OpenBrowser
    ///     ini tidak ada. Ini beda dari GetElement/TerminalSession yang
    ///     Body-nya benar-benar functional (child activity terima variabel
    ///     dari parent).
    ///   - Kenapa begini: NMHook (mesin di balik native-messaging OpenRPA)
    ///     melacak tab AKTIF secara GLOBAL, bukan lewat variabel yang
    ///     dioper ke child activity. Activity Click/TypeInto/GetElement
    ///     versi Web otomatis kerja di tab yang lagi aktif menurut NMHook,
    ///     berapa pun dalamnya nested di canvas.
    ///   - Sebagai bonus tidak wajib, delegate argument Body BISA diisi objek
    ///     tab yang berhasil dibuka/dipilih -- activity anak BOLEH baca
    ///     infonya (Title/Url/dst) kalau perlu, tapi tidak WAJIB dan tidak
    ///     mempengaruhi target elemen mana pun.
    ///   - BARU: implementasi IActivityTemplateFactory (Create()) supaya
    ///     kalau activity ini di-DRAG FRESH DARI TOOLBOX, delegate argument
    ///     Body-nya OTOMATIS diberi nama "browser" -- pola PERSIS sama
    ///     seperti GetElement.Create() yang menamai delegate argument-nya
    ///     "item". Ini yang bikin MaximizeWindow (yang default-nya merujuk
    ///     ke "browser") bisa langsung jalan zero-config kalau di-drop di
    ///     dalam Body Open Browser yang di-drag fresh ini.
    ///     CATATAN: kalau OpenBrowser SUDAH ada di canvas kamu SEBELUM
    ///     perubahan ini (dibuat manual, bukan lewat drag toolbox baru),
    ///     delegate argument-nya TIDAK otomatis ke-rename -- perlu drag ulang
    ///     instance baru dari toolbox, atau rename manual argument-nya jadi
    ///     "browser" di kotak kecil pojok drop-zone.
    ///   - OpenBrowser TIDAK menutup tab di akhir scope (beda dari
    ///     TerminalSession) — ini konsisten dengan OpenURL asli yang juga
    ///     tidak pernah menutup tab. Kalau butuh tutup, pakai CloseTab
    ///     activity terpisah.
    /// </summary>
    [Designer(typeof(Design.OpenBrowserDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.openbrowser.png")]
    [DisplayName("Open Browser")]
    [Description("Membuka browser ke sebuah alamat, lalu menjalankan activity di dalamnya.")]
    public sealed class OpenBrowser : NativeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public OpenBrowser()
        {
            DisplayName = "Open Browser";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Url")]
        public InArgument<string> Url { get; set; }

        [Category("Input")]
        [DisplayName("Browser Type")]
        [DefaultValue(OpenBrowserKind.Chrome)]
        public OpenBrowserKind BrowserType { get; set; } = OpenBrowserKind.Chrome;

        [Category("Options")]
        [DisplayName("New Tab")]
        [Description("True: selalu buka tab baru. False: pakai tab yang sudah ada kalau URL-nya cocok.")]
        public InArgument<bool> NewTab { get; set; }

        [Category("Options")]
        [DisplayName("User Data Folder Mode")]
        [DefaultValue(OpenBrowserUserDataFolderMode.Automatic)]
        public OpenBrowserUserDataFolderMode UserDataFolderMode { get; set; } = OpenBrowserUserDataFolderMode.Automatic;

        [Category("Options")]
        [DisplayName("User Data Folder Path")]
        public InArgument<string> UserDataFolderPath { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        [Category("Output")]
        [DisplayName("Browser")]
        [Description("Info tab yang berhasil dibuka/dipilih (title, url, id, dst) -- mirip UiBrowser di UiPath")]
        public OutArgument<NativeMessagingMessageTab> Browser { get; set; }

        [Browsable(false)]
        public ActivityAction<NativeMessagingMessageTab> Body { get; set; }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            metadata.AddArgument(new RuntimeArgument("Url", typeof(string), ArgumentDirection.In, true));
            metadata.AddArgument(new RuntimeArgument("NewTab", typeof(bool), ArgumentDirection.In));
            metadata.AddArgument(new RuntimeArgument("UserDataFolderPath", typeof(string), ArgumentDirection.In));
            metadata.AddArgument(new RuntimeArgument("ContinueOnError", typeof(bool), ArgumentDirection.In));
            metadata.AddArgument(new RuntimeArgument("Browser", typeof(NativeMessagingMessageTab), ArgumentDirection.Out));

            if (Body != null)
                metadata.AddDelegate(Body);
        }

        /// <summary>
        /// Apakah robot ini berjalan di dalam sesi anak?
        ///
        /// Jawabannya ada pada Plugin.client — dan yang mengisinya adalah Studio
        /// saat menyala. Di JakRunner tidak ada yang mengisinya, sehingga
        /// membacanya langsung melempar NullReferenceException dan seluruh
        /// activity Open Browser gagal dengan pesan yang tidak menyebut sebabnya.
        ///
        /// Tanpa Studio memang tidak ada sesi anak, jadi "tidak" adalah jawaban
        /// yang benar — bukan sekadar penambal.
        /// </summary>
        /// <remarks>
        /// Sengaja TIDAK ada pemeriksaan "jembatan harus tersambung" di sini.
        ///
        /// Percobaan sebelumnya menambahkannya, dan itu keliru: membuka peramban
        /// tidak memerlukan ekstensi sama sekali — yang memerlukannya hanyalah
        /// menemukan tab yang sudah terbuka. Tanpa ekstensi, daftar tab kosong,
        /// activity ini meluncurkan peramban seperti biasa, dan hasilnya benar.
        /// Pemeriksaan itu justru menolak jalan yang selama ini bekerja di Studio.
        /// </remarks>
        private static bool InChildSession()
        {
            try
            {
                return Plugin.client != null && Plugin.client.isRunningInChildSession;
            }
            catch (Exception)
            {
                return false;
            }
        }

        protected override void Execute(NativeActivityContext context)
        {
            var continueOnError = ContinueOnError != null ? ContinueOnError.Get(context) : false;

            try
            {
                var url = Url.Get(context);
                var browser = MapBrowser(BrowserType);
                var newtab = NewTab != null ? NewTab.Get(context) : false;

                // ----- Bagian ini DISALIN PERSIS dari OpenURL.cs asli (OpenRPA.NM) -----
                bool alreadyOpenAndSelected = false;
                if (!string.IsNullOrEmpty(url))
                {
                    NMHook.enumtabs();
                    var tab = NMHook.FindTabByURL(browser, url);
                    if (tab != null)
                    {
                        if (!tab.highlighted || !tab.selected)
                        {
                            NMHook.selecttab(browser, tab.id);
                        }
                        else
                        {
                            alreadyOpenAndSelected = true;
                        }
                    }
                }

                if (!alreadyOpenAndSelected)
                {
                    var userDataFolderMode = MapUserDataFolderMode(UserDataFolderMode);
                    var userDataFolderPath = UserDataFolderPath != null ? UserDataFolderPath.Get(context) : null;
                    string profilepath = "";
                    string profilename = "";

                    if (userDataFolderMode == "automatic")
                    {
                        if (InChildSession())
                        {
                            profilepath = userDataFolderPath;
                            if (string.IsNullOrEmpty(profilepath))
                            {
                                profilepath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\openrpa\\ChildSession\\" + browser;
                                profilename = "ChildSession";
                            }
                        }
                    }
                    else if (userDataFolderMode == "customfolder")
                    {
                        profilepath = userDataFolderPath;
                        if (string.IsNullOrEmpty(profilepath))
                        {
                            if (InChildSession())
                            {
                                profilepath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\openrpa\\ChildSession\\" + browser;
                                profilename = "ChildSession";
                            }
                            else
                            {
                                profilepath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\openrpa\\" + browser;
                                profilename = "openrpa";
                            }
                        }
                    }
                    // "defaultfolder" -> profilepath/profilename dibiarkan kosong (pakai folder default browser)

                    if (NMHook.connected)
                    {
                        NMHook.openurl(browser, url, newtab, profilename, profilepath);
                    }
                    else
                    {
                        // NMHook.openurl menunggu SAMPAI 20 DETIK agar addon
                        // OpenRPA lama menyambung — dan pada pemasangan JakForge
                        // addon itu tidak dipakai sama sekali, sehingga
                        // penantiannya selalu habis sia-sia. Dua puluh detik itu
                        // muncul di setiap Open Browser, sebelum satu pun langkah
                        // berikutnya dikerjakan.
                        //
                        // Jadi perambannya diluncurkan langsung, lalu yang
                        // ditunggu adalah tanda kesiapan yang BENAR-BENAR dipakai
                        // activity sesudahnya: jembatan JakForge melihat tabnya.
                        LaunchBrowser(browser, url, profilepath);
                        WaitForPage(url);
                    }
                }
                // ----- Akhir bagian yang disalin dari OpenURL.cs -----

                // ----- BARU: resolve tab sekali, dipakai utk Output DAN Body -----
                NativeMessagingMessageTab currentTab = ResolveCurrentTab(browser);
                if (Browser != null) Browser.Set(context, currentTab);

                if (Body != null)
                {
                    context.ScheduleAction(Body, currentTab, OnBodyCompleted, OnBodyFaulted);
                }
            }
            catch (Exception) when (continueOnError)
            {
                // Telan error kalau ContinueOnError = true, sama seperti activity lain yang sudah kita buat
            }
        }

        /// <summary>
        /// Luncurkan peramban pada sebuah alamat.
        ///
        /// Sama persis dengan yang dilakukan NMHook.openurl pada cabang "addon
        /// tidak tersambung", tanpa penantian dua puluh detik sesudahnya.
        /// </summary>
        private static void LaunchBrowser(string browser, string url, string profilepath)
        {
            var exe =
                browser == "edge" ? "msedge.exe" :
                browser == "ff" ? "firefox.exe" :
                "chrome.exe";

            var arguments = string.IsNullOrEmpty(profilepath)
                ? "\"" + url + "\""
                : "--user-data-dir=\"" + profilepath + "\" \"" + url + "\"";

            System.Diagnostics.Process.Start(exe, arguments);
        }

        /// <summary>
        /// Tunggu sampai jembatan JakForge benar-benar melihat halamannya.
        ///
        /// Inilah tanda kesiapan yang tepat: activity berikutnya — Click, Type
        /// Into — bekerja lewat jembatan yang sama, jadi begitu jembatan itu
        /// melihat tabnya, langkah berikutnya pasti bisa dikerjakan. Menunggu
        /// selang waktu tetap hanya bisa dua-duanya salah: kelamaan saat
        /// perambannya cepat, dan tetap kurang saat halamannya berat.
        ///
        /// Kalau jembatannya sendiri tidak menjawab, penantian berhenti setelah
        /// beberapa detik dan pekerjaan diteruskan: activity berikutnya yang
        /// akan melapor dengan pesannya sendiri, dan itu lebih jelas daripada
        /// menggantung di sini.
        /// </summary>
        private static void WaitForPage(string url)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            var host = HostOf(url);

            var bridgeSilentUntil = DateTime.UtcNow.AddSeconds(5);

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var tabs = Custom.StudioBridge.StudioPipeClient.SendCommand(
                        "listTabs", null, 1500, 4000) as Newtonsoft.Json.Linq.JArray;

                    if (tabs != null)
                    {
                        bridgeSilentUntil = DateTime.MaxValue;

                        foreach (var tab in tabs)
                        {
                            var tabUrl = (string)tab["url"];
                            if (string.IsNullOrEmpty(tabUrl)) continue;

                            if (string.IsNullOrEmpty(host) || HostOf(tabUrl) == host) return;
                        }
                    }
                }
                catch (Exception)
                {
                    // Jembatan belum menjawab. Wajar pada detik-detik pertama:
                    // perambannya baru saja diluncurkan.
                    if (DateTime.UtcNow > bridgeSilentUntil) return;
                }

                System.Threading.Thread.Sleep(250);
            }
        }

        private static string HostOf(string url)
        {
            try { return new Uri(url).Host.ToLowerInvariant(); }
            catch (Exception) { return null; }
        }

        private static NativeMessagingMessageTab ResolveCurrentTab(string browser)
        {
            if (browser == "edge") return NMHook.CurrentEdgeTab;
            if (browser == "ff") return NMHook.CurrentFFTab;
            return NMHook.CurrentChromeTab;
        }

        private static string MapBrowser(OpenBrowserKind kind)
        {
            switch (kind)
            {
                case OpenBrowserKind.Firefox: return "ff";
                case OpenBrowserKind.Edge: return "edge";
                default: return "chrome";
            }
        }

        private static string MapUserDataFolderMode(OpenBrowserUserDataFolderMode mode)
        {
            switch (mode)
            {
                case OpenBrowserUserDataFolderMode.DefaultFolder: return "defaultfolder";
                case OpenBrowserUserDataFolderMode.CustomFolder: return "customfolder";
                default: return "automatic";
            }
        }

        private void OnBodyCompleted(NativeActivityContext context, ActivityInstance completedInstance)
        {
            // Sengaja kosong -- OpenBrowser tidak menutup tab di akhir scope,
            // konsisten dengan OpenURL asli yang juga tidak pernah menutup tab.
        }

        private void OnBodyFaulted(NativeActivityFaultContext faultContext, Exception propagatedException,
            ActivityInstance propagatedFrom)
        {
            // Biarkan exception ter-propagate, Try Catch di level workflow
            // tetap bisa menangani.
        }

        /// <summary>
        /// IActivityTemplateFactory.Create -- dipanggil OTOMATIS oleh WF
        /// Designer saat activity ini di-drag dari TOOLBOX (bukan saat
        /// sekadar di-load dari XAML yang sudah ada). Pola PERSIS sama
        /// dengan GetElement.Create() -- bikin instance baru dengan Body
        /// yang delegate argument-nya sudah dinamai "browser" dari awal.
        /// </summary>
        public Activity Create(System.Windows.DependencyObject target)
        {
            var fef = new OpenBrowser();
            var da = new DelegateInArgument<NativeMessagingMessageTab> { Name = "browser" };
            fef.Body = new ActivityAction<NativeMessagingMessageTab>
            {
                Argument = da,

                // Do diisi Sequence sejak awal, seperti Open Browser milik
                // UiPath. Tanpa ini kotak Do hanya memuat SATU activity, dan
                // begitu langkah kedua dibutuhkan user harus membongkar dulu
                // isinya untuk menyisipkan Sequence sendiri.
                Handler = new System.Activities.Statements.Sequence { DisplayName = "Do" }
            };
            return fef;
        }
    }
}

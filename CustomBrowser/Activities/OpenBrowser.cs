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
    ///   - Sebagai bonus tidak wajib, delegate argument Body ("browser")
    ///     tetap diisi objek tab yang berhasil dibuka/dipilih — activity
    ///     anak BOLEH baca infonya (Title/Url/dst) kalau perlu, tapi tidak
    ///     WAJIB dan tidak mempengaruhi target elemen mana pun.
    ///   - OpenBrowser TIDAK menutup tab di akhir scope (beda dari
    ///     TerminalSession) — ini konsisten dengan OpenURL asli yang juga
    ///     tidak pernah menutup tab. Kalau butuh tutup, pakai CloseTab
    ///     activity terpisah.
    /// </summary>
    [Designer(typeof(Design.OpenBrowserDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    public sealed class OpenBrowser : NativeActivity
    {
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
                        if (Plugin.client.isRunningInChildSession)
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
                            if (Plugin.client.isRunningInChildSession)
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

                    NMHook.openurl(browser, url, newtab, profilename, profilepath);
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
    }
}

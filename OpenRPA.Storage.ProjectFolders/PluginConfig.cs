namespace OpenRPA.Storage.ProjectFolders
{
    class PluginConfig
    {
        private static string pluginname => "StorageProjectFolders";

        private static Config _globallocal = null;
        public static Config globallocal
        {
            get
            {
                if (_globallocal == null) _globallocal = Config.local;
                return _globallocal;
            }
        }

        /// <summary>
        /// PENTING soal nama key: Config.GetProperty menyusun nama setelan dari
        /// argumen pertama DITAMBAH nama properti pemanggilnya
        /// (CallerMemberName). Jadi getter "enabled" di bawah menghasilkan key
        /// "StorageProjectFolders_enabled", dan "strict" menghasilkan
        /// "StorageProjectFolders_strict" — walaupun keduanya mengirim
        /// argumen yang sama persis.
        ///
        /// Jangan tergoda mengirim nama yang sudah berakhiran "_strict" di
        /// sini: hasilnya justru key ganda "..._strict_strict" yang tidak
        /// pernah dibaca siapa pun, dan setelan-nya diam-diam tidak berfungsi.
        /// </summary>
        public static bool enabled
        {
            get { return globallocal.GetProperty(pluginname, false); }
            set { globallocal.SetProperty(pluginname, value); }
        }

        public static bool strict
        {
            get { return globallocal.GetProperty(pluginname, false); }
            set { globallocal.SetProperty(pluginname, value); }
        }
    }
}

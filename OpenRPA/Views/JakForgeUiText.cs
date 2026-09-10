using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;
using Custom.Shared;

namespace OpenRPA.Views
{
    /// <summary>
    /// Menerjemahkan bilah header dan pita Studio ke bahasa yang sedang dipakai.
    ///
    /// KENAPA TIDAK MEMAKAI JakForgeText: kamus di Custom.Shared berkunci teks
    /// Indonesia, karena seluruh antarmuka JakRunner memang ditulis Indonesia.
    /// Pita Studio TIDAK begitu — isinya campur tiga sumber: label Inggris yang
    /// ditulis keras ("New", "Save"), judul grup Indonesia ("Berkas", "Alat"),
    /// dan {x:Static or:strings.*} yang menghasilkan Inggris dari resx OpenRPA.
    /// Kamus berkunci satu bahasa tidak bisa menangani ketiganya sekaligus.
    ///
    /// Jadi tabel di sini punya EMPAT lajur: teks aslinya apa adanya, lalu
    /// Indonesia, Inggris, dan Jawa. Pencariannya BALIK — teks dalam bahasa apa
    /// pun menemukan barisnya, lalu diambil lajur bahasa tujuan. Akibatnya
    /// penerjemahan tidak menyimpan keadaan apa pun dan boleh dijalankan
    /// berulang kali: menerjemahkan teks yang sudah diterjemahkan menghasilkan
    /// hal yang sama. Itu penting, karena Apply dipanggil lagi setiap kali
    /// bahasanya berubah, di atas pita yang sudah diterjemahkan sebelumnya.
    ///
    /// Teks yang tidak ada di tabel dikembalikan APA ADANYA — tabel yang belum
    /// lengkap membuat sebagian pita tidak ikut berubah, bukan memunculkan kode
    /// mentah di layar.
    ///
    /// BATASNYA: yang diterjemahkan hanya akar yang diberikan ke Apply — bilah
    /// header, pita, dan judul panel dok. Kanvas TIDAK ikut, dan itu disengaja:
    /// nama activity di kanvas adalah isi buatan pengguna, dan activity bernama
    /// "Save" tidak boleh berubah menjadi "Simpan" hanya karena bahasa
    /// antarmukanya diganti.
    /// </summary>
    public static class JakForgeUiText
    {
        private sealed class Baris
        {
            public string Id;
            public string En;
            public string Jv;
        }

        private static readonly Dictionary<string, Baris> Peta =
            new Dictionary<string, Baris>(StringComparer.Ordinal);

        /// <summary>
        /// Daftarkan satu baris. Setiap varian menjadi kunci menuju baris yang
        /// sama.
        ///
        /// Kunci yang SUDAH ada tidak ditimpa: dua baris yang kebetulan berbagi
        /// satu kata harus menuju satu tujuan, bukan saling menimpa bergantian
        /// menurut urutan pendaftaran.
        /// </summary>
        private static void R(string asli, string id, string en, string jv)
        {
            var baris = new Baris { Id = id, En = en, Jv = jv };

            Daftar(asli, baris);
            Daftar(id, baris);
            Daftar(en, baris);
            Daftar(jv, baris);
        }

        private static void Daftar(string kunci, Baris baris)
        {
            if (string.IsNullOrEmpty(kunci)) return;
            if (Peta.ContainsKey(kunci)) return;

            Peta[kunci] = baris;
        }

        static JakForgeUiText()
        {
            // ---------------- bilah header ----------------
            R("HOME", "BERANDA", "HOME", "NGAREP");
            R("DESIGN", "DESAIN", "DESIGN", "DESAIN");
            R("Run/Debug", "Jalankan/Debug", "Run/Debug", "Lakokna/Debug");
            R("Run", "Jalankan", "Run", "Lakokna");
            R("Save", "Simpan", "Save", "Simpen");
            R("Version Control", "Kontrol Versi", "Version Control", "Kontrol Versi");
            R("Slow Motion", "Gerak Lambat", "Slow Motion", "Gerak Alon");

            R("Kembali ke layar Home (daftar proyek)",
              "Kembali ke layar Home (daftar proyek)",
              "Back to the Home screen (project list)",
              "Bali menyang layar Ngarep (dhaptar proyek)");
            R("Perkakas perancangan workflow",
              "Perkakas perancangan workflow",
              "Workflow design tools",
              "Piranti ngrancang workflow");
            R("Perkakas penelusuran jalannya workflow",
              "Perkakas penelusuran jalannya workflow",
              "Tools for tracing a workflow run",
              "Piranti nlusuri lakune workflow");
            R("Jalankan workflow yang sedang dibuka",
              "Jalankan workflow yang sedang dibuka",
              "Run the workflow that is open",
              "Lakokna workflow sing lagi dibukak");
            R("Pilihan penelusuran", "Pilihan penelusuran", "Debug options", "Pilihan panlusuran");
            R("Jalankan di sesi anak (tanpa penelusuran di kanvas)",
              "Jalankan di sesi anak (tanpa penelusuran di kanvas)",
              "Run in a child session (no canvas tracing)",
              "Lakokna ing sesi anak (tanpa panlusuran ing kanvas)");
            R("Simpan workflow", "Simpan workflow", "Save the workflow", "Simpen workflow");
            R("Belum ada integrasi version control di Studio ini.",
              "Belum ada integrasi version control di Studio ini.",
              "This Studio has no version control integration yet.",
              "Studio iki durung ana integrasi kontrol versi.");
            R("Minimalkan", "Minimalkan", "Minimize", "Cilikna");
            R("Perbesar", "Perbesar", "Maximize", "Gedhekna");
            R("Tutup", "Tutup", "Close", "Tutup");

            // ---------------- tab pita ----------------
            R("Design", "Desain", "Design", "Desain");

            // ---------------- judul grup ----------------
            R("Berkas", "Berkas", "File", "Berkas");
            R("Jalankan", "Jalankan", "Run", "Lakokna");
            R("Sunting", "Sunting", "Edit", "Sunting");
            R("Alat", "Alat", "Tools", "Piranti");
            R("Proyek", "Proyek", "Project", "Proyek");
            R("Kelola", "Kelola", "Manage", "Ngatur");
            R("Search", "Cari", "Search", "Golek");
            R("UI Language", "Bahasa Antarmuka", "UI Language", "Basa Antarmuka");
            R("Browser addons", "Addon Peramban", "Browser addons", "Addon Panjlajah");
            R("Plugins", "Plugin", "Plugins", "Plugin");
            R("Langkah", "Langkah", "Step", "Langkah");
            R("Penelusuran", "Penelusuran", "Tracing", "Panlusuran");
            R("Catatan", "Catatan", "Logs", "Cathetan");
            R("Sesi Anak", "Sesi Anak", "Child Session", "Sesi Anak");
            R("Change Type", "Ganti Jenis", "Change Type", "Ganti Jinis");
            R("Cancel key", "Tombol Batal", "Cancel key", "Tombol Batal");
            R("Runtime", "Saat Berjalan", "Runtime", "Nalika Mlaku");
            R("Logging", "Pencatatan", "Logging", "Pancathetan");

            // ---------------- tombol tab Design ----------------
            R("New", "Baru", "New", "Anyar");
            R("Export as\nTemplate", "Ekspor jadi\nTemplat", "Export as\nTemplate", "Ekspor dadi\nTemplat");
            R("Publish", "Terbitkan", "Publish", "Terbitna");
            R("Debug File", "Berkas Debug", "Debug File", "Berkas Debug");
            R("Stop", "Berhenti", "Stop", "Mandheg");
            R("Cut", "Potong", "Cut", "Kethok");
            R("Copy", "Salin", "Copy", "Salin");
            R("Paste", "Tempel", "Paste", "Tempel");
            R("Undo", "Batalkan", "Undo", "Balekna");
            R("Redo", "Ulangi", "Redo", "Baleni");
            R("Manage\nPackages", "Kelola\nPaket", "Manage\nPackages", "Ngatur\nPaket");
            R("Recording", "Rekam", "Recording", "Ngrekam");
            R("UI\nExplorer", "Penjelajah\nUI", "UI\nExplorer", "Panjlajah\nUI");
            R("User\nEvents", "Kejadian\nPengguna", "User\nEvents", "Kedadean\nPangguna");
            R("Open", "Buka", "Open", "Bukak");
            R("Import", "Impor", "Import", "Impor");
            R("Export", "Ekspor", "Export", "Ekspor");
            R("Reload", "Muat Ulang", "Reload", "Muat Maneh");
            R("Sambungkan", "Sambungkan", "Connect", "Sambungna");
            R("Buka Dasbor", "Buka Dasbor", "Open Dashboard", "Bukak Papan Kontrol");
            R("Delete", "Hapus", "Delete", "Busak");
            R("Permissions", "Hak Akses", "Permissions", "Hak Akses");
            R("Detectors", "Detektor", "Detectors", "Dhetektor");
            R("Work Item Queues", "Antrean Pekerjaan", "Work Item Queues", "Antrean Pagaweyan");
            R("Run plugins", "Plugin Jalan", "Run plugins", "Plugin Playon");
            R("Recorder plugins", "Plugin Perekam", "Recorder plugins", "Plugin Ngrekam");
            R("Tampilan", "Tampilan", "Appearance", "Tampilan");

            // ---------------- tooltip tab Design ----------------
            R("Workflow baru di proyek yang sedang dibuka",
              "Workflow baru di proyek yang sedang dibuka",
              "New workflow in the open project",
              "Workflow anyar ing proyek sing dibukak");
            R("Ekspor proyek menjadi berkas yang bisa dipakai ulang",
              "Ekspor proyek menjadi berkas yang bisa dipakai ulang",
              "Export the project as a reusable file",
              "Ekspor proyek dadi berkas sing bisa dienggo maneh");
            R("Ekspor proyek (padanan Publish)",
              "Ekspor proyek (padanan Publish)",
              "Export the project (the Publish equivalent)",
              "Ekspor proyek (padhane Publish)");
            R("Jalankan dengan penelusuran di kanvas",
              "Jalankan dengan penelusuran di kanvas",
              "Run with tracing on the canvas",
              "Lakokna kanthi panlusuran ing kanvas");
            R("Hentikan workflow yang sedang berjalan",
              "Hentikan workflow yang sedang berjalan",
              "Stop the running workflow",
              "Mandhegna workflow sing lagi mlaku");
            R("Potong activity terpilih", "Potong activity terpilih",
              "Cut the selected activity", "Kethok activity sing dipilih");
            R("Salin activity terpilih", "Salin activity terpilih",
              "Copy the selected activity", "Salin activity sing dipilih");
            R("Tempel activity", "Tempel activity", "Paste an activity", "Tempel activity");
            R("Batalkan perubahan terakhir", "Batalkan perubahan terakhir",
              "Undo the last change", "Balekna owahan pungkasan");
            R("Ulangi perubahan yang dibatalkan", "Ulangi perubahan yang dibatalkan",
              "Redo the undone change", "Baleni owahan sing dibatalke");
            R("Kelola paket dan dependensi proyek", "Kelola paket dan dependensi proyek",
              "Manage project packages and dependencies", "Ngatur paket lan gumantunge proyek");
            R("Rekam langkah menjadi activity", "Rekam langkah menjadi activity",
              "Record steps into activities", "Ngrekam langkah dadi activity");
            R("Buka penjelajah elemen dan editor selector JakForge",
              "Buka penjelajah elemen dan editor selector JakForge",
              "Open the JakForge element explorer and selector editor",
              "Bukak panjlajah elemen lan editor selector JakForge");
            R("Detector: menjalankan workflow saat sesuatu terjadi",
              "Detector: menjalankan workflow saat sesuatu terjadi",
              "Detector: run a workflow when something happens",
              "Dhetektor: nglakokna workflow nalika ana kedadean");
            R("Buka proyek", "Buka proyek", "Open a project", "Bukak proyek");
            R("Impor proyek atau workflow dari berkas", "Impor proyek atau workflow dari berkas",
              "Import a project or workflow from a file", "Impor proyek utawa workflow saka berkas");
            R("Ekspor proyek ke berkas", "Ekspor proyek ke berkas",
              "Export the project to a file", "Ekspor proyek menyang berkas");
            R("Muat ulang dari disk", "Muat ulang dari disk", "Reload from disk", "Muat maneh saka disk");
            R("Kemas proyek ini dan kirim ke ForgeHub sebagai paket",
              "Kemas proyek ini dan kirim ke ForgeHub sebagai paket",
              "Package this project and send it to ForgeHub",
              "Paketi proyek iki lan kirim menyang ForgeHub");
            R("Atur alamat, nama pengguna, dan kata sandi ForgeHub",
              "Atur alamat, nama pengguna, dan kata sandi ForgeHub",
              "Set the ForgeHub address, username and password",
              "Atur alamat, jeneng pangguna, lan tembung sandi ForgeHub");
            R("Buka dasbor ForgeHub di peramban", "Buka dasbor ForgeHub di peramban",
              "Open the ForgeHub dashboard in a browser", "Bukak papan kontrol ForgeHub ing panjlajah");
            R("Gandakan workflow atau proyek terpilih", "Gandakan workflow atau proyek terpilih",
              "Duplicate the selected workflow or project", "Tikelna workflow utawa proyek sing dipilih");
            R("Hapus workflow atau proyek terpilih", "Hapus workflow atau proyek terpilih",
              "Delete the selected workflow or project", "Busak workflow utawa proyek sing dipilih");
            R("Atur hak akses", "Atur hak akses", "Set access rights", "Atur hak akses");
            R("Bahasa dan mode gelap, dipakai bersama JakRunner",
              "Bahasa dan mode gelap, dipakai bersama JakRunner",
              "Language and dark mode, shared with JakRunner",
              "Basa lan mode peteng, dienggo bareng JakRunner");

            // ---------------- tab Debug ----------------
            R("Restart", "Mulai Ulang", "Restart", "Bali Miwiti");
            R("Continue", "Lanjutkan", "Continue", "Terusna");
            R("Execution Trail", "Jejak Jalan", "Execution Trail", "Tapak Playon");
            R("Slow Step", "Langkah Lambat", "Slow Step", "Langkah Alon");
            R("Visual Tracking", "Pelacakan Visual", "Visual Tracking", "Nglacak Visual");
            R("Minimize saat jalan", "Minimalkan saat jalan", "Minimize while running", "Cilikna nalika mlaku");
            R("Open Logs", "Buka Catatan", "Open Logs", "Bukak Cathetan");
            R("Child\nSession", "Sesi\nAnak", "Child\nSession", "Sesi\nAnak");
            R("Play in\nChild", "Jalankan di\nSesi Anak", "Play in\nChild", "Lakokna ing\nSesi Anak");
            R("Swap SendKeys", "Tukar SendKeys", "Swap SendKeys", "Ijolna SendKeys");
            R("Swap Virtual Clicking", "Tukar Klik Virtual", "Swap Virtual Clicking", "Ijolna Klik Virtual");
            R("Swap Animate", "Tukar Animasi", "Swap Animate", "Ijolna Animasi");
            R("Download", "Unduh", "Download", "Undhuh");

            R("Hentikan lalu jalankan lagi dari awal", "Hentikan lalu jalankan lagi dari awal",
              "Stop, then run again from the beginning", "Mandhegna banjur lakokna maneh saka wiwitan");
            R("Jalankan satu activity lalu berhenti lagi", "Jalankan satu activity lalu berhenti lagi",
              "Run one activity, then stop again", "Lakokna siji activity banjur mandheg maneh");
            R("Lanjutkan sampai breakpoint berikutnya", "Lanjutkan sampai breakpoint berikutnya",
              "Continue to the next breakpoint", "Terusna nganti breakpoint sabanjure");
            R("Pasang atau lepas breakpoint pada activity terpilih",
              "Pasang atau lepas breakpoint pada activity terpilih",
              "Set or clear a breakpoint on the selected activity",
              "Pasang utawa uculna breakpoint ing activity sing dipilih");
            R("Sorot activity yang sedang berjalan di kanvas",
              "Sorot activity yang sedang berjalan di kanvas",
              "Highlight the running activity on the canvas",
              "Sorot activity sing lagi mlaku ing kanvas");
            R("Perlambat jalannya workflow supaya mudah diikuti",
              "Perlambat jalannya workflow supaya mudah diikuti",
              "Slow the workflow down so it is easy to follow",
              "Alonana lakune workflow supaya gampang ditutke");
            R("Sorot activity yang sedang dijalankan", "Sorot activity yang sedang dijalankan",
              "Highlight the activity being run", "Sorot activity sing lagi dilakokna");
            R("Sembunyikan Studio selama workflow berjalan",
              "Sembunyikan Studio selama workflow berjalan",
              "Hide Studio while the workflow runs",
              "Umpetna Studio sasuwene workflow mlaku");
            R("Buka panel Output", "Buka panel Output", "Open the Output panel", "Bukak panel Output");
            R("Buka sesi anak", "Buka sesi anak", "Open a child session", "Bukak sesi anak");
            R("Jalankan di dalam sesi anak", "Jalankan di dalam sesi anak",
              "Run inside a child session", "Lakokna ing jero sesi anak");

            // ---------------- centang Runtime dan Logging ----------------
            R("Recording Overlay", "Lapisan Perekaman", "Recording Overlay", "Lapisan Ngrekam");
            R("Use SendKeys", "Pakai SendKeys", "Use SendKeys", "Nganggo SendKeys");
            R("Use Virtual Clicks", "Pakai Klik Virtual", "Use Virtual Clicks", "Nganggo Klik Virtual");
            R("Use Animate Mouse", "Pakai Animasi Tetikus", "Use Animate Mouse", "Nganggo Animasi Tetikus");
            R("Record directly into designer", "Rekam langsung ke perancang",
              "Record directly into designer", "Ngrekam langsung menyang pangrancang");
            R("Enable Child Sessions", "Aktifkan Sesi Anak", "Enable Child Sessions", "Uripna Sesi Anak");
            R("Output", "Keluaran", "Output", "Metu");
            R("Warning", "Peringatan", "Warning", "Penget");
            R("Verbose", "Rinci", "Verbose", "Rinci");
            R("Selector", "Selektor", "Selector", "Selektor");
            R("Selector Verbose", "Selektor Rinci", "Selector Verbose", "Selektor Rinci");
            R("Network", "Jaringan", "Network", "Jaringan");

            // ---------------- judul panel dok dan bilah status ----------------
            R("Toolbox", "Kotak Perkakas", "Toolbox", "Kothak Piranti");
            R("Snippets", "Potongan", "Snippets", "Cuwilan");
            R("Properties", "Properti", "Properties", "Properti");
            R("Workflow Instances", "Instans Workflow", "Workflow Instances", "Instansi Workflow");
            R("Disconnected", "Tidak tersambung", "Disconnected", "Ora kesambung");
        }

        /// <summary>
        /// Terjemahkan satu teks. Yang tidak dikenali dikembalikan apa adanya.
        /// </summary>
        public static string T(string teks)
        {
            if (string.IsNullOrEmpty(teks)) return teks;

            Baris baris;
            if (!Peta.TryGetValue(teks, out baris)) return teks;

            switch (JakForgeUi.Language)
            {
                case UiLanguage.English: return baris.En;
                case UiLanguage.Jawa: return baris.Jv;
                default: return baris.Id;
            }
        }

        /// <summary>
        /// Terjemahkan seluruh teks di bawah satu akar.
        ///
        /// Pohon yang ditelusuri adalah pohon LOGIS, bukan visual: pita
        /// membangun pohon visualnya hanya untuk tab yang sedang terbuka, jadi
        /// tab yang belum pernah dibuka tidak akan ikut kalau ditelusuri secara
        /// visual. Pohon logis memuat semua yang ditulis di XAML sejak awal.
        /// </summary>
        public static void Apply(DependencyObject akar)
        {
            if (akar == null) return;

            Terjemahkan(akar);

            foreach (var anak in LogicalTreeHelper.GetChildren(akar))
            {
                var d = anak as DependencyObject;
                if (d != null) Apply(d);
            }
        }

        private static void Terjemahkan(object simpul)
        {
            var fe = simpul as FrameworkElement;
            if (fe != null)
            {
                var tip = fe.ToolTip as string;
                if (tip != null) fe.ToolTip = T(tip);
            }

            var tab = simpul as RibbonTab;
            if (tab != null)
            {
                var h = tab.Header as string;
                if (h != null) tab.Header = T(h);
            }

            var grup = simpul as RibbonGroup;
            if (grup != null)
            {
                var h = grup.Header as string;
                if (h != null) grup.Header = T(h);
            }

            var tombol = simpul as RibbonButton;
            if (tombol != null)
            {
                tombol.Label = T(tombol.Label);

                // Content hanya disentuh kalau isinya memang teks. Tombol yang
                // isinya panel atau gambar akan HILANG kalau Content-nya ditimpa
                // string.
                var isi = tombol.Content as string;
                if (isi != null) tombol.Content = T(isi);
            }

            var centang = simpul as RibbonCheckBox;
            if (centang != null) centang.Label = T(centang.Label);

            var teks = simpul as TextBlock;
            if (teks != null) teks.Text = T(teks.Text);

            var menu = simpul as MenuItem;
            if (menu != null)
            {
                var h = menu.Header as string;
                if (h != null) menu.Header = T(h);
            }
        }
    }
}

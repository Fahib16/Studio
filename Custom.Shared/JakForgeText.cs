using System.Collections.Generic;

namespace Custom.Shared
{
    /// <summary>
    /// Terjemahan antarmuka Studio dan JakRunner.
    ///
    /// KUNCINYA adalah teks Indonesianya sendiri, sama seperti di ForgeHub.
    /// Teks yang belum diterjemahkan karena itu jatuh kembali ke bahasa
    /// Indonesia yang BENAR — bukan menjadi kode seperti "btn.run" yang bocor
    /// ke layar. Dan sumbernya tetap bisa dibaca tanpa membuka kamusnya.
    ///
    /// Kamus yang belum lengkap tidak pernah merusak antarmuka; ia hanya
    /// membuat sebagian tetap berbahasa Indonesia.
    /// </summary>
    public static class JakForgeText
    {
        private static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            // --- JakRunner: menu dan judul ---
            { "Beranda", "Home" },
            { "Jadwal", "Schedule" },
            { "Log", "Logs" },
            { "Pengaturan", "Settings" },
            { "Profil", "Profile" },
            { "Dasbor Agen Pelari", "Runner Agent Dashboard" },
            { "Cari Agen atau Log", "Search agents or logs" },

            // --- JakRunner: kartu ringkasan ---
            { "Agen Aktif", "Active agents" },
            { "Automasi Berjalan", "Running automations" },
            { "Berhasil Hari Ini", "Succeeded today" },
            { "Gagal Hari Ini", "Failed today" },

            // --- JakRunner: tabel ---
            { "Status Agen", "Agent status" },
            { "Nama Automasi", "Automation" },
            { "Status", "State" },
            { "Automasi Aktif", "Current activity" },
            { "Kinerja CPU/Memori", "CPU / memory" },
            { "Detail", "Details" },
            { "automasi", "automations" },

            // --- JakRunner: tombol ---
            { "Muat ulang", "Refresh" },
            { "Hentikan", "Stop" },
            { "TUGASKAN AUTOMASI BARU", "ASSIGN NEW AUTOMATION" },
            { "Pilih automasi untuk dijalankan", "Pick an automation to run" },
            { "Lebarkan", "Expand" },
            { "Jalankan", "Run" },
            { "Tutup", "Close" },
            { "Simpan", "Save" },
            { "Batal", "Cancel" },

            // --- keadaan ---
            { "Siap", "Ready" },
            { "Berjalan", "Running" },
            { "Selesai", "Finished" },
            { "Gagal", "Failed" },
            { "Dihentikan", "Stopped" },

            // --- setelan tampilan ---
            { "Tampilan", "Appearance" },
            { "Bahasa", "Language" },
            { "Tema", "Theme" },
            { "Terang", "Light" },
            { "Gelap", "Dark" },
            { "Indonesia", "Indonesian" },
            { "Inggris", "English" },
            { "Jawa", "Javanese" },
            { "Setelan ini dipakai bersama Studio dan JakRunner.",
              "This setting is shared by Studio and JakRunner." },

            // --- sambungan ---
            { "ForgeHub: tersambung", "ForgeHub: connected" },
            { "ForgeHub: tidak tersambung", "ForgeHub: not connected" },
        };

        private static readonly Dictionary<string, string> Jawa = new Dictionary<string, string>
        {
            // --- JakRunner: menu dan judul ---
            { "Beranda", "Ngarep" },
            { "Jadwal", "Jadwal" },
            { "Log", "Log" },
            { "Pengaturan", "Setelan" },
            { "Profil", "Profil" },
            { "Dasbor Agen Pelari", "Papan Kontrol Agèn Playon" },
            { "Cari Agen atau Log", "Golèk agèn utawa log" },

            // --- JakRunner: kartu ringkasan ---
            { "Agen Aktif", "Agèn urip" },
            { "Automasi Berjalan", "Otomatisasi mlaku" },
            { "Berhasil Hari Ini", "Kasil dina iki" },
            { "Gagal Hari Ini", "Gagal dina iki" },

            // --- JakRunner: tabel ---
            { "Status Agen", "Kahanan agèn" },
            { "Nama Automasi", "Jeneng otomatisasi" },
            { "Status", "Kahanan" },
            { "Automasi Aktif", "Sing lagi digarap" },
            { "Kinerja CPU/Memori", "CPU / mèmori" },
            { "Detail", "Rincian" },
            { "automasi", "otomatisasi" },

            // --- JakRunner: tombol ---
            { "Muat ulang", "Muat manèh" },
            { "Hentikan", "Mandhegna" },
            { "TUGASKAN AUTOMASI BARU", "PASRAHNA OTOMATISASI ANYAR" },
            { "Pilih automasi untuk dijalankan", "Pilih otomatisasi sing arep dilakokna" },
            { "Lebarkan", "Ambakna" },
            { "Jalankan", "Lakokna" },
            { "Tutup", "Tutup" },
            { "Simpan", "Simpen" },
            { "Batal", "Batal" },

            // --- keadaan ---
            { "Siap", "Siyaga" },
            { "Berjalan", "Mlaku" },
            { "Selesai", "Rampung" },
            { "Gagal", "Gagal" },
            { "Dihentikan", "Dipandhegaké" },

            // --- setelan tampilan ---
            { "Tampilan", "Tampilan" },
            { "Bahasa", "Basa" },
            { "Tema", "Tema" },
            { "Terang", "Padhang" },
            { "Gelap", "Peteng" },
            { "Indonesia", "Indonesia" },
            { "Inggris", "Inggris" },
            { "Jawa", "Jawa" },
            { "Setelan ini dipakai bersama Studio dan JakRunner.",
              "Setelan iki dienggo bareng Studio lan JakRunner." },

            // --- sambungan ---
            { "ForgeHub: tersambung", "ForgeHub: kesambung" },
            { "ForgeHub: tidak tersambung", "ForgeHub: ora kesambung" },
        };

        /// <summary>
        /// Terjemahkan satu teks ke bahasa yang sedang dipakai.
        ///
        /// Yang tidak ada di kamus dikembalikan APA ADANYA.
        /// </summary>
        public static string T(string teks)
        {
            if (string.IsNullOrEmpty(teks)) return teks;

            Dictionary<string, string> kamus;

            switch (JakForgeUi.Language)
            {
                case UiLanguage.English: kamus = English; break;
                case UiLanguage.Jawa: kamus = Jawa; break;
                default: return teks;
            }

            string hasil;
            return kamus.TryGetValue(teks, out hasil) ? hasil : teks;
        }

        /// <summary>Jumlah kunci per bahasa; dipakai pengujian.</summary>
        public static int KeyCount(UiLanguage language)
        {
            switch (language)
            {
                case UiLanguage.English: return English.Count;
                case UiLanguage.Jawa: return Jawa.Count;
                default: return 0;
            }
        }

        /// <summary>Seluruh kunci sebuah bahasa; dipakai pengujian.</summary>
        public static IEnumerable<string> Keys(UiLanguage language)
        {
            switch (language)
            {
                case UiLanguage.English: return English.Keys;
                case UiLanguage.Jawa: return Jawa.Keys;
                default: return new string[0];
            }
        }
    }
}

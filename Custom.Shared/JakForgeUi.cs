using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Custom.Shared
{
    /// <summary>Bahasa antarmuka yang tersedia.</summary>
    public enum UiLanguage
    {
        Indonesia,
        English,
        Jawa,
    }

    /// <summary>Terang atau gelap.</summary>
    public enum UiTheme
    {
        Light,
        Dark,
    }

    /// <summary>
    /// Setelan tampilan yang DIPAKAI BERSAMA Studio dan JakRunner.
    ///
    /// Disimpan di satu berkas di %LOCALAPPDATA%\JakForge\ui.json, bukan di
    /// setelan masing-masing program. Alasannya diminta secara langsung:
    /// asisten mengikuti setelan Studio. Kalau keduanya menyimpan sendiri-
    /// sendiri, "mengikuti" berarti menyalin — dan salinan selalu bisa
    /// ketinggalan.
    ///
    /// Berkasnya JSON yang ditulis tangan, tanpa pustaka: isinya dua kata, dan
    /// menarik seluruh serializer untuk dua kata bukan pertukaran yang masuk
    /// akal. Berkas yang rusak atau tidak ada menghasilkan NILAI BAWAAN, bukan
    /// kegagalan — setelan tampilan tidak boleh menghalangi program berjalan.
    /// </summary>
    public static class JakForgeUi
    {
        private static readonly object Gembok = new object();

        private static UiLanguage _language = UiLanguage.Indonesia;
        private static UiTheme _theme = UiTheme.Light;
        private static bool _sudahDibaca;

        /// <summary>Diberitahukan setiap kali setelannya berubah, di proses ini.</summary>
        public static event Action Changed;

        public static string SettingsPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "JakForge", "ui.json");
            }
        }

        public static UiLanguage Language
        {
            get { Pastikan(); return _language; }
            set { Simpan(value, Theme); }
        }

        public static UiTheme Theme
        {
            get { Pastikan(); return _theme; }
            set { Simpan(Language, value); }
        }

        /// <summary>
        /// Baca ulang dari berkasnya.
        ///
        /// Dipanggil JakRunner saat jendelanya kembali aktif, supaya perubahan
        /// yang dibuat di Studio ikut terpakai tanpa perlu menjalankan ulang.
        /// Mengembalikan true kalau ada yang BERUBAH — pemanggilnya cuma perlu
        /// menggambar ulang kalau memang perlu.
        /// </summary>
        public static bool Reload()
        {
            var bahasaLama = _language;
            var temaLama = _theme;

            lock (Gembok)
            {
                _sudahDibaca = false;
                Baca();
            }

            var berubah = bahasaLama != _language || temaLama != _theme;
            if (berubah) Beritahu();

            return berubah;
        }

        public static void Set(UiLanguage language, UiTheme theme)
        {
            Simpan(language, theme);
        }

        // ------------------------------------------------------------------

        private static void Pastikan()
        {
            lock (Gembok)
            {
                if (!_sudahDibaca) Baca();
            }
        }

        private static void Baca()
        {
            _sudahDibaca = true;

            try
            {
                var path = SettingsPath;
                if (!File.Exists(path)) return;

                var isi = File.ReadAllText(path, Encoding.UTF8);

                _language = BacaBahasa(Nilai(isi, "language"), _language);
                _theme = BacaTema(Nilai(isi, "theme"), _theme);
            }
            catch (Exception)
            {
                // Berkas rusak, terkunci, atau folder tidak bisa dibaca:
                // nilai bawaannya dipertahankan. Setelan tampilan tidak boleh
                // menghalangi program berjalan.
            }
        }

        private static void Simpan(UiLanguage language, UiTheme theme)
        {
            lock (Gembok)
            {
                _language = language;
                _theme = theme;
                _sudahDibaca = true;

                try
                {
                    var path = SettingsPath;
                    var folder = Path.GetDirectoryName(path);
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    File.WriteAllText(path,
                        "{\n" +
                        "  \"language\": \"" + language.ToString().ToLowerInvariant() + "\",\n" +
                        "  \"theme\": \"" + theme.ToString().ToLowerInvariant() + "\"\n" +
                        "}\n",
                        Encoding.UTF8);
                }
                catch (Exception)
                {
                    // Gagal menulis berarti setelannya hanya berlaku untuk sesi
                    // ini. Itu tetap lebih baik daripada menjatuhkan program.
                }
            }

            Beritahu();
        }

        private static void Beritahu()
        {
            var handler = Changed;
            if (handler != null) handler();
        }

        /// <summary>
        /// Ambil nilai sebuah kunci dari JSON sederhana.
        ///
        /// Cukup untuk berkas yang bentuknya kita tulis sendiri dan hanya
        /// berisi dua kunci bernilai teks. Bukan parser JSON, dan tidak
        /// berpura-pura menjadi parser JSON.
        /// </summary>
        private static string Nilai(string json, string kunci)
        {
            var tanda = "\"" + kunci + "\"";
            var i = json.IndexOf(tanda, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return null;

            i = json.IndexOf(':', i + tanda.Length);
            if (i < 0) return null;

            var buka = json.IndexOf('"', i + 1);
            if (buka < 0) return null;

            var tutup = json.IndexOf('"', buka + 1);
            if (tutup < 0) return null;

            return json.Substring(buka + 1, tutup - buka - 1);
        }

        private static UiLanguage BacaBahasa(string teks, UiLanguage bawaan)
        {
            if (string.IsNullOrWhiteSpace(teks)) return bawaan;

            switch (teks.Trim().ToLower(CultureInfo.InvariantCulture))
            {
                case "english":
                case "en": return UiLanguage.English;
                case "jawa":
                case "jv": return UiLanguage.Jawa;
                case "indonesia":
                case "id": return UiLanguage.Indonesia;
                default: return bawaan;
            }
        }

        private static UiTheme BacaTema(string teks, UiTheme bawaan)
        {
            if (string.IsNullOrWhiteSpace(teks)) return bawaan;

            switch (teks.Trim().ToLower(CultureInfo.InvariantCulture))
            {
                case "dark":
                case "gelap": return UiTheme.Dark;
                case "light":
                case "terang": return UiTheme.Light;
                default: return bawaan;
            }
        }
    }
}

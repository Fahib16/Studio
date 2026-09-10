using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace JakForge.Installer
{
    internal class HasilPendaftaran
    {
        public readonly List<string> PerambanTerdaftar = new List<string>();
        public readonly List<string> EkstensiTerdaftar = new List<string>();
        public string IdEkstensi;
        public string Catatan;
    }

    /// <summary>
    /// Mendaftarkan jembatan Studio ke Chrome, Edge, dan Brave — seluruhnya
    /// di HKEY_CURRENT_USER, jadi tanpa hak administrator.
    ///
    /// Ada DUA pendaftaran yang berbeda dan sering tertukar:
    ///
    ///   1. NATIVE MESSAGING HOST. Memberi tahu peramban di mana
    ///      Studio.NativeHost.exe berada dan ekstensi mana yang boleh
    ///      memanggilnya. Ini bisa OTOMATIS SEPENUHNYA.
    ///
    ///   2. EKSTENSI ITU SENDIRI. Peramban memasangnya dari berkas .crx yang
    ///      ditunjuk registry. Ini terpasang otomatis, TAPI Chrome dan Edge
    ///      sengaja menampilkan konfirmasi sekali sebelum mengaktifkannya.
    ///      Tidak ada jalan memintas itu tanpa hak administrator — dan itu
    ///      memang disengaja Google: kalau ada jalannya, program apa pun bisa
    ///      menanam ekstensi di peramban orang tanpa sepengetahuannya.
    /// </summary>
    internal class JembatanPeramban
    {
        private const string NamaHost = "com.jakforge.studiobridge";

        private readonly string _folderPasang;

        public JembatanPeramban(string folderPasang)
        {
            _folderPasang = folderPasang;
        }

        /// <summary>
        /// Peramban yang didukung: nama, cabang registry native host, dan
        /// cabang registry ekstensi.
        /// </summary>
        private static readonly Tuple<string, string, string>[] Peramban =
        {
            Tuple.Create("Chrome",
                @"Software\Google\Chrome\NativeMessagingHosts",
                @"Software\Google\Chrome\Extensions"),

            Tuple.Create("Edge",
                @"Software\Microsoft\Edge\NativeMessagingHosts",
                @"Software\Microsoft\Edge\Extensions"),

            Tuple.Create("Brave",
                @"Software\BraveSoftware\Brave-Browser\NativeMessagingHosts",
                @"Software\BraveSoftware\Brave-Browser\Extensions"),
        };

        private string FolderEkstensi { get { return Path.Combine(_folderPasang, "Extension"); } }
        private string BerkasCrx { get { return Path.Combine(FolderEkstensi, "JakForgeBridge.crx"); } }
        private string ManifestEkstensi { get { return Path.Combine(FolderEkstensi, "manifest.json"); } }

        private string FolderJembatan
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "JakForge", "ChromeBridge");
            }
        }

        private string BerkasManifestHost { get { return Path.Combine(FolderJembatan, NamaHost + ".json"); } }

        // ------------------------------------------------------------------
        // ID ekstensi
        // ------------------------------------------------------------------

        /// <summary>
        /// ID Chrome sebuah ekstensi: SHA-256 dari kunci publik (DER), 16 bita
        /// pertama, tiap nibble 0-f dipetakan ke a-p.
        ///
        /// Dihitung, bukan ditulis tetap. Kalau suatu saat kuncinya diganti,
        /// yang perlu diubah cuma manifest ekstensinya — bukan berkas ini,
        /// yang akan diam-diam mendaftarkan ID lama dan menghasilkan
        /// "Access to the specified native messaging host is forbidden."
        /// yang tidak menyebut ID mana pun.
        /// </summary>
        private static string HitungId(string kunciBase64)
        {
            var der = Convert.FromBase64String(kunciBase64);

            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(der);
                var sb = new StringBuilder(32);

                for (var i = 0; i < 16; i++)
                {
                    sb.Append((char)('a' + (hash[i] >> 4)));
                    sb.Append((char)('a' + (hash[i] & 0x0F)));
                }

                return sb.ToString();
            }
        }

        private string BacaKunciDariManifest()
        {
            if (!File.Exists(ManifestEkstensi)) return null;

            // Diurai dengan pembacaan sederhana, bukan pustaka JSON: pemasang
            // ini sengaja tidak punya dependensi apa pun, dan yang dicari cuma
            // satu bidang yang isinya base64 satu baris.
            var teks = File.ReadAllText(ManifestEkstensi);
            var i = teks.IndexOf("\"key\"", StringComparison.Ordinal);
            if (i < 0) return null;

            var buka = teks.IndexOf('"', teks.IndexOf(':', i) + 1);
            if (buka < 0) return null;

            var tutup = teks.IndexOf('"', buka + 1);
            if (tutup < 0) return null;

            return teks.Substring(buka + 1, tutup - buka - 1);
        }

        private static string BacaVersiDariManifest(string manifest)
        {
            if (!File.Exists(manifest)) return "1.0";

            var teks = File.ReadAllText(manifest);
            var i = teks.IndexOf("\"version\"", StringComparison.Ordinal);
            if (i < 0) return "1.0";

            var buka = teks.IndexOf('"', teks.IndexOf(':', i) + 1);
            if (buka < 0) return "1.0";

            var tutup = teks.IndexOf('"', buka + 1);
            if (tutup < 0) return "1.0";

            return teks.Substring(buka + 1, tutup - buka - 1);
        }

        // ------------------------------------------------------------------
        // Mendaftarkan
        // ------------------------------------------------------------------

        public HasilPendaftaran Daftarkan()
        {
            var hasil = new HasilPendaftaran();

            var kunci = BacaKunciDariManifest();
            if (string.IsNullOrEmpty(kunci))
            {
                hasil.Catatan = "manifest ekstensi tidak punya field \"key\", jadi ID-nya tidak tetap";
                return hasil;
            }

            var id = HitungId(kunci);
            hasil.IdEkstensi = id;

            var exe = CariNativeHost();
            if (exe == null)
            {
                hasil.Catatan = "Studio.NativeHost.exe tidak ada di dalam paket";
                return hasil;
            }

            TulisManifestHost(exe, id);

            foreach (var p in Peramban)
            {
                var nama = p.Item1;

                if (DaftarkanNativeHost(p.Item2)) hasil.PerambanTerdaftar.Add(nama);

                if (File.Exists(BerkasCrx) && DaftarkanEkstensi(p.Item3, id))
                    hasil.EkstensiTerdaftar.Add(nama);
            }

            return hasil;
        }

        private string CariNativeHost()
        {
            var calon = new[]
            {
                Path.Combine(_folderPasang, "Studio.NativeHost.exe"),
                Path.Combine(_folderPasang, "NativeHost", "Studio.NativeHost.exe"),
            };

            return calon.FirstOrDefault(File.Exists);
        }

        private void TulisManifestHost(string exe, string idEkstensi)
        {
            Directory.CreateDirectory(FolderJembatan);

            // Disusun tangan, bukan lewat pustaka JSON, dan backslash-nya
            // diloloskan secara eksplisit. Jalur Windows penuh backslash, dan
            // backslash yang lolos begitu saja menghasilkan JSON yang SAH tapi
            // menunjuk berkas yang salah — gagal tanpa pesan apa pun.
            var isi = new StringBuilder();
            isi.AppendLine("{");
            isi.AppendLine("  \"name\": \"" + NamaHost + "\",");
            isi.AppendLine("  \"description\": \"Studio Native Messaging Host\",");
            isi.AppendLine("  \"path\": \"" + exe.Replace("\\", "\\\\") + "\",");
            isi.AppendLine("  \"type\": \"stdio\",");
            isi.AppendLine("  \"allowed_origins\": [");
            isi.AppendLine("    \"chrome-extension://" + idEkstensi + "/\"");
            isi.AppendLine("  ]");
            isi.AppendLine("}");

            // TANPA BOM, dan itu bukan kerewelan. Pengurai manifest native host
            // Chrome menolak berkas yang tidak diawali "{", jadi BOM membuat
            // native host tidak pernah dijalankan — tanpa pesan di sisi mana
            // pun. Gejalanya persis sama dengan "ekstensi tidak terpasang".
            File.WriteAllText(BerkasManifestHost, isi.ToString(), new UTF8Encoding(false));
        }

        private bool DaftarkanNativeHost(string cabang)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(cabang + "\\" + NamaHost))
                {
                    if (k == null) return false;
                    k.SetValue(string.Empty, BerkasManifestHost, RegistryValueKind.String);
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Mendaftarkan ekstensi lewat berkas .crx setempat.
        ///
        /// Peramban membaca cabang ini saat dijalankan, memasang .crx-nya, lalu
        /// MENONAKTIFKANNYA sampai pengguna menyetujui lewat gelembung
        /// "Ekstensi baru ditambahkan". Itu satu klik yang tidak bisa
        /// dihilangkan tanpa kebijakan tingkat mesin (yang butuh admin).
        /// </summary>
        private bool DaftarkanEkstensi(string cabang, string id)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(cabang + "\\" + id))
                {
                    if (k == null) return false;

                    k.SetValue("path", BerkasCrx, RegistryValueKind.String);
                    k.SetValue("version", BacaVersiDariManifest(ManifestEkstensi), RegistryValueKind.String);
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ------------------------------------------------------------------
        // Membatalkan
        // ------------------------------------------------------------------

        public void BatalkanPendaftaran()
        {
            string id = null;
            try { var k = BacaKunciDariManifest(); if (k != null) id = HitungId(k); } catch (Exception) { }

            foreach (var p in Peramban)
            {
                try { Registry.CurrentUser.DeleteSubKeyTree(p.Item2 + "\\" + NamaHost, false); }
                catch (Exception) { }

                if (id == null) continue;

                try { Registry.CurrentUser.DeleteSubKeyTree(p.Item3 + "\\" + id, false); }
                catch (Exception) { }
            }

            try { if (Directory.Exists(FolderJembatan)) Directory.Delete(FolderJembatan, true); }
            catch (Exception) { }
        }
    }
}

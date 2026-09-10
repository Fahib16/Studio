using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JakRunner.Core
{
    /// <summary>
    /// Daftar automasi yang tersedia, dibaca LANGSUNG dari folder proyek.
    ///
    /// Tidak lewat Studio dan tidak lewat basis data. Tata letak proyek JakForge
    /// sudah cukup menjelaskan dirinya sendiri: satu folder per proyek, berkas
    /// .xaml di akarnya, dan project.json sebagai penanda bahwa foldernya memang
    /// sebuah proyek. Membacanya begitu saja membuat JakRunner bisa dijalankan
    /// di mesin yang tidak punya Studio sama sekali.
    /// </summary>
    public static class AutomationCatalog
    {
        /// <summary>
        /// Folder induk tempat proyek disimpan.
        ///
        /// Urutan pencariannya sama dengan yang dipakai Studio: %APPDATA%\OpenRPA
        /// kalau di situ ada settings.json, kalau tidak Documents\OpenRPA. Lalu
        /// satu tingkat lebih dalam — "offline" saat tidak tersambung ke
        /// orchestrator, atau nama host-nya saat tersambung.
        /// </summary>
        public static IEnumerable<string> ProjectRoots()
        {
            var documents = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "OpenRPA");
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenRPA");

            var bases = new List<string>();
            if (File.Exists(Path.Combine(appData, "settings.json"))) bases.Add(appData);
            bases.Add(documents);
            bases.Add(appData);

            foreach (var b in bases.Distinct())
            {
                if (!Directory.Exists(b)) continue;

                yield return b;

                foreach (var sub in Directory.GetDirectories(b))
                {
                    // Folder per instance orchestrator, atau "offline".
                    yield return sub;
                }
            }
        }

        /// <summary>
        /// Semua PROYEK yang bisa dijalankan, satu baris per proyek.
        ///
        /// Sebelumnya daftar ini berisi satu baris per berkas .xaml, dan
        /// ReFramework muncul sebagai tujuh baris terpisah — padahal enam di
        /// antaranya adalah sub-workflow yang tidak berarti apa-apa kalau
        /// dijalankan sendiri. Menjalankan InitAllSettings tanpa Main hanya
        /// membaca berkas setelan lalu berhenti.
        ///
        /// Yang dijalankan adalah TITIK MASUK proyeknya. Sub-workflow tetap
        /// dipanggil, tapi oleh Main lewat Invoke Workflow — sebagaimana
        /// dirancang.
        /// </summary>
        public static List<AutomationItem> Load()
        {
            var found = new Dictionary<string, AutomationItem>(StringComparer.OrdinalIgnoreCase);

            foreach (var root in ProjectRoots())
            {
                List<string> projectFolders;

                try
                {
                    projectFolders = Directory.GetDirectories(root).Where(IsProjectFolder).ToList();
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (var folder in projectFolders)
                {
                    string[] files;
                    try { files = Directory.GetFiles(folder, "*.xaml"); }
                    catch (Exception) { continue; }

                    if (files.Length == 0) continue;

                    var entry = EntryPointOf(folder, files);
                    if (entry == null) continue;

                    var projectName = ProjectNameOf(folder);

                    // Penyaringan kembar memakai NAMA proyek, bukan jalur folder.
                    //
                    // Folder yang sama bisa terlihat dari beberapa akar sekaligus
                    // — Documents\OpenRPA dan Documents\OpenRPA\offline keduanya
                    // dipindai — dan tanpa ini proyek yang sama muncul dua kali
                    // di daftar, dengan dua tombol Play yang menjalankan berkas
                    // yang persis sama.
                    if (found.ContainsKey(projectName)) continue;

                    var others = files.Length - 1;

                    found[projectName] = new AutomationItem
                    {
                        Name = projectName,
                        ProjectName = others > 0
                            ? Path.GetFileName(entry) + "  ·  " + others + " sub-workflow"
                            : Path.GetFileName(entry),
                        FilePath = entry,
                        ProjectFolder = folder,
                        LastModified = files.Max(File.GetLastWriteTime),
                    };
                }
            }

            return found.Values
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Berkas yang dijalankan saat proyek ini ditugaskan.
        ///
        /// Urutan pencariannya: main dari project.json, lalu Main.xaml, lalu —
        /// kalau proyeknya cuma punya satu berkas — berkas itu. Proyek dengan
        /// banyak berkas tapi tanpa titik masuk yang jelas TIDAK ditebak: menjalankan
        /// berkas yang kebetulan pertama menurut abjad adalah cara paling cepat
        /// membuat robot mengerjakan hal yang salah.
        /// </summary>
        private static string EntryPointOf(string folder, string[] files)
        {
            var declared = DeclaredEntryPoint(folder);

            if (!string.IsNullOrEmpty(declared))
            {
                var path = Path.Combine(folder, declared);
                if (File.Exists(path)) return path;
            }

            var main = files.FirstOrDefault(x =>
                string.Equals(Path.GetFileNameWithoutExtension(x), "Main", StringComparison.OrdinalIgnoreCase));

            if (main != null) return main;

            return files.Length == 1 ? files[0] : null;
        }

        private static string DeclaredEntryPoint(string folder)
        {
            try
            {
                var file = Path.Combine(folder, "project.json");
                if (!File.Exists(file)) return null;

                var o = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(file));

                // Dua ejaan yang dipakai: "main" bergaya UiPath, "entryPoint"
                // bergaya paket ForgeHub.
                var value = (string)o["main"] ?? (string)o["entryPoint"];

                return string.IsNullOrWhiteSpace(value) ? null : value.Replace('/', Path.DirectorySeparatorChar);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsProjectFolder(string folder)
        {
            return File.Exists(Path.Combine(folder, "project.json"))
                || File.Exists(Path.Combine(folder, "project.rpaproj"));
        }

        /// <summary>
        /// Nama proyek diambil dari project.json kalau ada; kalau tidak, dari
        /// nama foldernya. Nama folder saja tidak selalu benar — proyek boleh
        /// dinamai dengan karakter yang tidak sah untuk nama berkas.
        /// </summary>
        private static string ProjectNameOf(string folder)
        {
            try
            {
                var file = Path.Combine(folder, "project.json");
                if (!File.Exists(file)) file = Path.Combine(folder, "project.rpaproj");

                if (File.Exists(file))
                {
                    var o = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(file));
                    var name = (string)o["name"];
                    if (!string.IsNullOrWhiteSpace(name)) return name;
                }
            }
            catch (Exception)
            {
            }

            return Path.GetFileName(folder);
        }
    }
}

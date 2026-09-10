using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Custom.Orchestrator.Runtime
{
    /// <summary>
    /// Keterangan tentang proyek yang sedang dijalankan.
    ///
    /// Disetel program yang menjalankannya — Studio sebelum menekan Run,
    /// JakRunner sebelum memulai automasi — dan dibaca Invoke Workflow untuk
    /// menemukan berkas sub-workflow yang ditunjuk.
    /// </summary>
    public static class WorkflowContext
    {
        /// <summary>Folder tempat berkas .xaml proyek ini berada.</summary>
        public static string ProjectFolder;

        /// <summary>Nama proyeknya, dipakai untuk penandaan log.</summary>
        public static string ProjectName;

        /// <summary>
        /// Ubah jalur yang tertulis di berkas XAML menjadi jalur yang ada.
        ///
        /// Studio menuliskannya dengan nama proyek di depan —
        /// "ReFramework/Process.xaml" — padahal berkasnya ada langsung di dalam
        /// folder proyek. Jadi beberapa bentuk dicoba, dari yang paling harfiah
        /// ke yang paling longgar, dan yang pertama ada itulah yang dipakai.
        ///
        /// Mengembalikan null kalau tidak ada yang cocok; pemanggilnya yang
        /// menyusun pesan kesalahannya, karena ia tahu konteksnya.
        /// </summary>
        public static string Resolve(string requested)
        {
            if (string.IsNullOrWhiteSpace(requested)) return null;

            var cleaned = requested.Replace('\\', '/').Trim();

            if (Path.IsPathRooted(cleaned) && File.Exists(cleaned)) return cleaned;
            if (string.IsNullOrEmpty(ProjectFolder)) return null;

            var candidates = new List<string>
            {
                Path.Combine(ProjectFolder, cleaned.Replace('/', Path.DirectorySeparatorChar)),
                Path.Combine(ProjectFolder, Path.GetFileName(cleaned)),
            };

            if (!cleaned.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                candidates.Add(Path.Combine(ProjectFolder, Path.GetFileName(cleaned) + ".xaml"));

            return candidates.FirstOrDefault(File.Exists);
        }

        /// <summary>Semua workflow di proyek ini, untuk daftar pilihan di perancang.</summary>
        public static string[] AvailableWorkflows()
        {
            if (string.IsNullOrEmpty(ProjectFolder) || !Directory.Exists(ProjectFolder))
                return new string[0];

            try
            {
                return Directory.GetFiles(ProjectFolder, "*.xaml")
                    .Select(Path.GetFileName)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception)
            {
                return new string[0];
            }
        }
    }
}

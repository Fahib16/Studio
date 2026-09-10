using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpenRPA.Interfaces;

namespace OpenRPA.Templates
{
    /// <summary>Satu pilihan di galeri template.</summary>
    public class ProjectTemplate
    {
        public string Name { get; set; }
        public string Description { get; set; }

        /// <summary>
        /// Menuliskan isi template ke folder proyek yang baru dibuat, lalu
        /// mengembalikan berkas .xaml yang dihasilkan — berurutan, yang pertama
        /// dianggap workflow utama.
        /// </summary>
        public Func<string, string, List<string>> Write { get; set; }
    }

    /// <summary>
    /// Galeri template proyek.
    ///
    /// Sebelumnya menu Template di layar Home hanya memunculkan pesan bahwa
    /// galerinya belum ada. Sekarang isinya nyata, dan menambah template baru
    /// cukup menambahkan satu entri di <see cref="All"/>.
    /// </summary>
    public static class ProjectTemplates
    {
        public static IReadOnlyList<ProjectTemplate> All { get; } = new List<ProjectTemplate>
        {
            new ProjectTemplate
            {
                Name = ReFrameworkTemplate.TemplateName,
                Description = ReFrameworkTemplate.TemplateDescription,
                Write = ReFrameworkTemplate.WriteTo,
            },
            new ProjectTemplate
            {
                Name = "Proyek kosong",
                Description = "Satu workflow kosong. Mulai dari nol.",
                Write = null,
            },
        };

        /// <summary>
        /// Membuat proyek baru dari sebuah template dan mengembalikan workflow
        /// yang sebaiknya dibuka lebih dulu.
        ///
        /// Berkas template ditulis ke folder SEMENTARA lalu didaftarkan lewat
        /// <see cref="Project.LoadFileFromDisk"/>. Menulisnya langsung ke folder
        /// proyek terdengar lebih singkat, tapi penyimpanan folder-per-proyek
        /// yang menentukan nama berkas akhirnya — menaruh berkas di sana lebih
        /// dulu hanya akan menghasilkan dua salinan.
        /// </summary>
        public static async Task<IWorkflow> CreateProject(ProjectTemplate template, string projectName)
        {
            if (string.IsNullOrWhiteSpace(projectName)) throw new ArgumentException("Nama proyek kosong.");

            var project = await Project.Create(Interfaces.Extensions.ProjectsDirectory, projectName);

            if (template == null || template.Write == null)
            {
                return await project.AddDefaultWorkflow();
            }

            var staging = Path.Combine(Path.GetTempPath(), "JakForgeTemplate_" + Guid.NewGuid().ToString("N"));

            try
            {
                var files = template.Write(staging, SafeFolderName(projectName));

                IWorkflow first = null;

                foreach (var file in files)
                {
                    await project.LoadFileFromDisk(file);

                    if (first == null)
                    {
                        var stem = Path.GetFileNameWithoutExtension(file);
                        first = project.Workflows.FirstOrDefault(
                            w => string.Equals(w.name, stem, StringComparison.OrdinalIgnoreCase));
                    }
                }

                CopyExtras(staging, project);

                if (first == null) first = project.Workflows.FirstOrDefault();
                if (first == null) first = await project.AddDefaultWorkflow();

                return first;
            }
            finally
            {
                try { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
                catch (Exception) { }
            }
        }

        /// <summary>
        /// Salin berkas pendamping (setelan, data, catatan) ke folder proyek.
        ///
        /// Hanya berkas .xaml yang menjadi entity Workflow; sisanya berkas biasa
        /// yang tetap harus ikut, kalau tidak InitAllSettings tidak punya apa-apa
        /// untuk dibaca.
        /// </summary>
        private static void CopyExtras(string staging, Project project)
        {
            try
            {
                var target = ProjectFolderOnDisk(project);
                if (string.IsNullOrEmpty(target)) return;

                foreach (var file in Directory.GetFiles(staging, "*", SearchOption.AllDirectories))
                {
                    if (file.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)) continue;

                    var relative = file.Substring(staging.Length).TrimStart('\\', '/');
                    var destination = Path.Combine(target, relative);

                    var folder = Path.GetDirectoryName(destination);
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    File.Copy(file, destination, true);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("Template: gagal menyalin berkas pendamping: " + ex.Message);
            }
        }

        /// <summary>
        /// Folder proyek yang benar-benar ada di disk. Sama seperti di panel
        /// Project: Project.Path menunjuk satu tingkat lebih dangkal daripada
        /// tempat penyimpanan folder-per-proyek menaruh isinya.
        /// </summary>
        internal static string ProjectFolderOnDisk(Project project)
        {
            var root = Interfaces.Extensions.ProjectsDirectory;
            if (string.IsNullOrEmpty(root) || project == null) return null;

            if (!string.IsNullOrEmpty(project.Path) && Directory.Exists(project.Path)) return project.Path;

            // Tidak ada lagi tingkat "offline"/nama-host di antara akar dan
            // nama project; lihat BasePath() di provider penyimpanannya.
            var direct = Path.Combine(root, project.name ?? "");
            if (Directory.Exists(direct)) return direct;

            if (Directory.Exists(root))
            {
                // Pencocokan tanpa peduli besar-kecil huruf: Windows memang
                // begitu, dan nama yang diketik pengguna tidak selalu sama
                // persis dengan nama folder yang dulu dibuat.
                var match = Directory.GetDirectories(root).FirstOrDefault(
                    d => string.Equals(Path.GetFileName(d), project.name, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            return null;
        }

        /// <summary>Nama folder yang dipakai penyimpanan; ini yang disebut InvokeOpenRPA.</summary>
        private static string SafeFolderName(string name)
        {
            var invalid = new string(Path.GetInvalidFileNameChars());
            var cleaned = new string(name.Where(c => invalid.IndexOf(c) < 0).ToArray()).Trim();
            return cleaned.Length == 0 ? "Project" : cleaned;
        }
    }
}

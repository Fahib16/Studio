using System;
using System.Activities;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;

namespace Custom.Files
{
    /// <summary>
    /// Membuat berkas .zip dari sekumpulan berkas dan/atau folder.
    ///
    /// Tidak menambah dependensi baru: System.IO.Compression sudah ada di
    /// .NET Framework.
    ///
    /// Setiap entri di Files boleh berupa berkas ATAU folder. Folder dimasukkan
    /// beserta isinya, dengan nama foldernya sendiri sebagai awalan di dalam
    /// zip — supaya isi dua folder berbeda tidak saling menimpa hanya karena
    /// ada berkas bernama sama.
    /// </summary>
    [Designer(typeof(Design.ZipFilesDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Zip Files")]
    [Description("Memampatkan berkas dan folder ke dalam satu berkas .zip.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.zipfiles.png")]
    public sealed class ZipFiles : CodeActivity
    {
        public ZipFiles()
        {
            DisplayName = "Zip Files";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Files")]
        [Description("Daftar path yang dimasukkan ke zip. Setiap entri boleh berkas atau folder.")]
        public InArgument<string[]> Files { get; set; }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Zip Path")]
        [Description("Berkas .zip yang dihasilkan.")]
        public InArgument<string> ZipPath { get; set; }

        [Category("Options")]
        [DisplayName("Overwrite")]
        [Description("Timpa berkas .zip kalau sudah ada (default false).")]
        public InArgument<bool> Overwrite { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [Description("Kalau true, kegagalan activity ini tidak menghentikan workflow (default false).")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = ContinueOnError != null && ContinueOnError.Get(context);

            try
            {
                var entries = Files.Get(context);
                if (entries == null || entries.Length == 0) throw new ArgumentException("Files kosong.");

                var zipPath = ZipPath.Get(context);
                if (string.IsNullOrWhiteSpace(zipPath)) throw new ArgumentException("Zip Path kosong.");

                var overwrite = Overwrite != null && Overwrite.Get(context);

                if (File.Exists(zipPath))
                {
                    if (!overwrite)
                        throw new IOException("Berkas zip sudah ada dan Overwrite bernilai false: " + zipPath);
                    File.Delete(zipPath);
                }

                var folder = System.IO.Path.GetDirectoryName(zipPath);
                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder)) Directory.CreateDirectory(folder);

                using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    foreach (var entry in entries)
                    {
                        if (string.IsNullOrWhiteSpace(entry)) continue;

                        if (Directory.Exists(entry))
                        {
                            var root = new DirectoryInfo(entry.TrimEnd('\\', '/'));
                            foreach (var file in root.GetFiles("*", SearchOption.AllDirectories))
                            {
                                var relative = file.FullName.Substring(root.FullName.Length).TrimStart('\\', '/');
                                zip.CreateEntryFromFile(file.FullName,
                                                        root.Name + "/" + relative.Replace('\\', '/'));
                            }
                            continue;
                        }

                        if (File.Exists(entry))
                        {
                            zip.CreateEntryFromFile(entry, System.IO.Path.GetFileName(entry));
                            continue;
                        }

                        throw new FileNotFoundException("Tidak ditemukan: " + entry, entry);
                    }
                }
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

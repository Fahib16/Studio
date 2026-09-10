using System;
using System.Activities;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;

namespace Custom.Files
{
    /// <summary>
    /// Mengekstrak isi berkas .zip ke sebuah folder.
    ///
    /// Ekstraksi dilakukan entri per entri (bukan ZipFile.ExtractToDirectory)
    /// karena metode itu selalu gagal kalau ada berkas yang sudah ada di
    /// tujuan, tanpa pilihan menimpa — padahal "jalankan ulang workflow yang
    /// sama" adalah keadaan paling umum.
    ///
    /// Path setiap entri diperiksa supaya hasil ekstraksi tidak bisa keluar
    /// dari folder tujuan (zip yang berisi entri seperti ..\..\file bisa
    /// menimpa berkas di luar folder tujuan kalau tidak diperiksa).
    /// </summary>
    [Designer(typeof(Design.UnzipDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Unzip")]
    [Description("Mengekstrak berkas .zip ke sebuah folder.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.unzip.png")]
    public sealed class Unzip : CodeActivity
    {
        public Unzip()
        {
            DisplayName = "Unzip";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Zip Path")]
        [Description("Berkas .zip yang mau diekstrak.")]
        public InArgument<string> ZipPath { get; set; }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Destination Folder")]
        [Description("Folder tujuan. Dibuat otomatis kalau belum ada.")]
        public InArgument<string> DestinationFolder { get; set; }

        [Category("Options")]
        [DisplayName("Overwrite")]
        [Description("Timpa berkas yang sudah ada di folder tujuan (default false).")]
        public InArgument<bool> Overwrite { get; set; }

        [Category("Output")]
        [DisplayName("Files")]
        [Description("Path lengkap setiap berkas hasil ekstraksi.")]
        public OutArgument<string[]> Files { get; set; }

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
                var zipPath = ZipPath.Get(context);
                if (string.IsNullOrWhiteSpace(zipPath)) throw new ArgumentException("Zip Path kosong.");
                if (!File.Exists(zipPath)) throw new FileNotFoundException("Berkas zip tidak ditemukan: " + zipPath, zipPath);

                var destination = DestinationFolder.Get(context);
                if (string.IsNullOrWhiteSpace(destination)) throw new ArgumentException("Destination Folder kosong.");

                var overwrite = Overwrite != null && Overwrite.Get(context);

                Directory.CreateDirectory(destination);
                var root = System.IO.Path.GetFullPath(destination);
                if (!root.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                    root += System.IO.Path.DirectorySeparatorChar;

                var extracted = new System.Collections.Generic.List<string>();

                using (var zip = ZipFile.OpenRead(zipPath))
                {
                    foreach (var entry in zip.Entries)
                    {
                        var target = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, entry.FullName));

                        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                            throw new IOException("Entri zip menunjuk ke luar folder tujuan: " + entry.FullName);

                        // Entri folder di zip namanya berakhiran pemisah path dan tidak punya isi.
                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            Directory.CreateDirectory(target);
                            continue;
                        }

                        var folder = System.IO.Path.GetDirectoryName(target);
                        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

                        if (File.Exists(target) && !overwrite)
                            throw new IOException("Berkas tujuan sudah ada dan Overwrite bernilai false: " + target);

                        entry.ExtractToFile(target, overwrite);
                        extracted.Add(target);
                    }
                }

                if (Files != null) Files.Set(context, extracted.ToArray());
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

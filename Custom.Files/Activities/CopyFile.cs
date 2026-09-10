using System;
using System.Activities;
using System.ComponentModel;
using System.IO;

namespace Custom.Files
{
    /// <summary>
    /// Menyalin satu berkas.
    ///
    /// Folder tujuan dibuat otomatis kalau belum ada — kalau tidak, activity
    /// ini akan gagal karena alasan yang hampir selalu bukan yang dimaksud
    /// user (mereka ingin berkasnya tersalin, bukan diingatkan bahwa folder
    /// output belum dibuat).
    /// </summary>
    [Designer(typeof(Design.CopyFileDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Copy File")]
    [Description("Menyalin berkas ke lokasi lain.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.copyfile.png")]
    public sealed class CopyFile : CodeActivity
    {
        public CopyFile()
        {
            DisplayName = "Copy File";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Path")]
        [Description("Berkas asal yang mau disalin.")]
        public InArgument<string> Path { get; set; }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Destination")]
        [Description("Tujuan. Boleh berupa nama berkas lengkap, atau folder " +
                     "(nama berkas asal dipertahankan).")]
        public InArgument<string> Destination { get; set; }

        [Category("Options")]
        [DisplayName("Overwrite")]
        [Description("Timpa berkas tujuan kalau sudah ada (default false).")]
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
                var source = Path.Get(context);
                var destination = Destination.Get(context);
                var overwrite = Overwrite != null && Overwrite.Get(context);

                FileOps.CopyOrMove(source, destination, overwrite, move: false);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }

    internal static class FileOps
    {
        /// <summary>
        /// Bagian yang dipakai bersama Copy File dan Move File: menerjemahkan
        /// tujuan berupa folder menjadi nama berkas, menyiapkan foldernya, lalu
        /// menyalin/memindahkan.
        /// </summary>
        public static void CopyOrMove(string source, string destination, bool overwrite, bool move)
        {
            if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Path kosong.");
            if (string.IsNullOrWhiteSpace(destination)) throw new ArgumentException("Destination kosong.");
            if (!File.Exists(source)) throw new FileNotFoundException("Berkas asal tidak ditemukan: " + source, source);

            var target = destination;

            // Tujuan dianggap folder kalau memang sudah berupa folder, atau
            // kalau ditulis dengan pemisah path di akhir.
            if (Directory.Exists(destination) ||
                destination.EndsWith("\\", StringComparison.Ordinal) ||
                destination.EndsWith("/", StringComparison.Ordinal))
            {
                target = System.IO.Path.Combine(destination, System.IO.Path.GetFileName(source));
            }

            var folder = System.IO.Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder)) Directory.CreateDirectory(folder);

            if (File.Exists(target))
            {
                if (!overwrite)
                    throw new IOException("Berkas tujuan sudah ada dan Overwrite bernilai false: " + target);

                // File.Move tidak punya parameter overwrite di .NET Framework,
                // jadi berkas lama dihapus dulu.
                if (move) File.Delete(target);
            }

            if (move) File.Move(source, target);
            else File.Copy(source, target, overwrite);
        }
    }
}

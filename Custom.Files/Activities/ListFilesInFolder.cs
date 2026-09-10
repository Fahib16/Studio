using System;
using System.Activities;
using System.ComponentModel;
using System.IO;

namespace Custom.Files
{
    /// <summary>
    /// Mengambil daftar berkas dalam sebuah folder.
    ///
    /// Hasilnya berupa string[] berisi path lengkap, bukan nama berkas saja,
    /// supaya bisa langsung dipakai activity berikutnya (Copy File, Read Text
    /// File, dsb) tanpa harus digabung ulang dengan nama foldernya.
    /// </summary>
    [Designer(typeof(Design.ListFilesInFolderDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("List Files In Folder")]
    [Description("Mengambil daftar path berkas di dalam sebuah folder.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.listfiles.png")]
    public sealed class ListFilesInFolder : CodeActivity
    {
        public ListFilesInFolder()
        {
            DisplayName = "List Files In Folder";
            Pattern = new InArgument<string>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<string>("\"*.*\""));
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Path")]
        [Description("Folder yang isinya mau didaftar.")]
        public InArgument<string> Path { get; set; }

        [Category("Options")]
        [DisplayName("Pattern")]
        [Description("Pola nama berkas, mis. *.pdf (default *.*).")]
        public InArgument<string> Pattern { get; set; }

        [Category("Options")]
        [DisplayName("Recursive")]
        [Description("Kalau true, subfolder ikut ditelusuri (default false).")]
        public InArgument<bool> Recursive { get; set; }

        [Category("Output")]
        [DisplayName("Files")]
        [Description("Path lengkap setiap berkas yang cocok.")]
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
                var path = Path.Get(context);
                if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path kosong.");
                if (!Directory.Exists(path)) throw new DirectoryNotFoundException("Folder tidak ditemukan: " + path);

                var pattern = Pattern != null ? Pattern.Get(context) : null;
                if (string.IsNullOrWhiteSpace(pattern)) pattern = "*.*";

                var option = (Recursive != null && Recursive.Get(context))
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;

                var files = Directory.GetFiles(path, pattern, option);
                if (Files != null) Files.Set(context, files);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

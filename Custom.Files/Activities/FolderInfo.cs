using System;
using System.Activities;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace Custom.Files
{
    /// <summary>
    /// Mengambil ringkasan isi sebuah folder.
    ///
    /// Folder yang tidak ada TIDAK melempar error: Exists diisi false dan
    /// angka-angka lainnya nol. Activity ini memang dipakai untuk MEMERIKSA
    /// keadaan, jadi "tidak ada" adalah salah satu jawabannya, bukan kegagalan.
    ///
    /// Semua output diisi di Properties panel karena jumlahnya enam — kartu
    /// canvas hanya memuat Path supaya tetap ringkas.
    /// </summary>
    [Designer(typeof(Design.FolderInfoDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Folder Info")]
    [Description("Mengambil jumlah berkas, jumlah subfolder, total ukuran, dan tanggal sebuah folder.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.folderinfo.png")]
    public sealed class FolderInfo : CodeActivity
    {
        public FolderInfo()
        {
            DisplayName = "Folder Info";
            Recursive = new InArgument<bool>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<bool>("True"));
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Path")]
        [Description("Folder yang mau diperiksa.")]
        public InArgument<string> Path { get; set; }

        [Category("Options")]
        [DisplayName("Recursive")]
        [Description("Kalau true (default), isi subfolder ikut dihitung.")]
        public InArgument<bool> Recursive { get; set; }

        [Category("Output")]
        [DisplayName("Exists")]
        [Description("True kalau foldernya ada.")]
        public OutArgument<bool> Exists { get; set; }

        [Category("Output")]
        [DisplayName("File Count")]
        public OutArgument<int> FileCount { get; set; }

        [Category("Output")]
        [DisplayName("Folder Count")]
        public OutArgument<int> FolderCount { get; set; }

        [Category("Output")]
        [DisplayName("Total Size")]
        [Description("Total ukuran seluruh berkas, dalam byte.")]
        public OutArgument<long> TotalSize { get; set; }

        [Category("Output")]
        [DisplayName("Created At")]
        public OutArgument<DateTime> CreatedAt { get; set; }

        [Category("Output")]
        [DisplayName("Modified At")]
        public OutArgument<DateTime> ModifiedAt { get; set; }

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
                var exists = !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);

                if (Exists != null) Exists.Set(context, exists);
                if (!exists)
                {
                    if (FileCount != null) FileCount.Set(context, 0);
                    if (FolderCount != null) FolderCount.Set(context, 0);
                    if (TotalSize != null) TotalSize.Set(context, 0L);
                    return;
                }

                var option = (Recursive == null || Recursive.Get(context))
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;

                var info = new DirectoryInfo(path);
                var files = info.GetFiles("*", option);

                if (FileCount != null) FileCount.Set(context, files.Length);
                if (FolderCount != null) FolderCount.Set(context, info.GetDirectories("*", option).Length);
                if (TotalSize != null) TotalSize.Set(context, files.Sum(f => f.Length));
                if (CreatedAt != null) CreatedAt.Set(context, info.CreationTime);
                if (ModifiedAt != null) ModifiedAt.Set(context, info.LastWriteTime);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

using System;
using System.Activities;
using System.ComponentModel;
using System.IO;

namespace Custom.Files
{
    /// <summary>
    /// Menghapus folder.
    ///
    /// Recursive default TRUE: menghapus folder yang masih berisi adalah yang
    /// hampir selalu dimaksud. Kalau di-set false, folder yang tidak kosong
    /// akan melempar IOException dari .NET apa adanya, dan itu memang
    /// peringatan yang tepat.
    ///
    /// Folder yang memang sudah tidak ada TIDAK dianggap gagal, dengan alasan
    /// yang sama seperti Delete File.
    /// </summary>
    [Designer(typeof(Design.DeleteFolderDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Delete Folder")]
    [Description("Menghapus folder beserta isinya.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.deletefolder.png")]
    public sealed class DeleteFolder : CodeActivity
    {
        public DeleteFolder()
        {
            DisplayName = "Delete Folder";
            Recursive = new InArgument<bool>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<bool>("True"));
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Path")]
        [Description("Folder yang mau dihapus.")]
        public InArgument<string> Path { get; set; }

        [Category("Options")]
        [DisplayName("Recursive")]
        [Description("Kalau true (default), isi folder ikut dihapus.")]
        public InArgument<bool> Recursive { get; set; }

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
                if (!Directory.Exists(path)) return;

                var recursive = Recursive == null || Recursive.Get(context);
                Directory.Delete(path, recursive);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

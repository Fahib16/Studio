using System;
using System.Activities;
using System.ComponentModel;

namespace Custom.Files
{
    /// <summary>
    /// Memindahkan (atau mengganti nama) satu berkas. Aturan tujuannya sama
    /// persis dengan Copy File: tujuan boleh folder, dan foldernya dibuat
    /// otomatis kalau belum ada.
    /// </summary>
    [Designer(typeof(Design.MoveFileDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Move File")]
    [Description("Memindahkan atau mengganti nama berkas.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.movefile.png")]
    public sealed class MoveFile : CodeActivity
    {
        public MoveFile()
        {
            DisplayName = "Move File";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Path")]
        [Description("Berkas asal yang mau dipindahkan.")]
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
                FileOps.CopyOrMove(Path.Get(context), Destination.Get(context),
                                   Overwrite != null && Overwrite.Get(context), move: true);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

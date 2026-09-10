using System;
using System.Activities;
using System.ComponentModel;
using System.IO;

namespace Custom.Files
{
    /// <summary>
    /// Memeriksa keberadaan satu berkas. Path kosong dianggap "tidak ada",
    /// bukan error, supaya activity ini aman dipakai untuk memeriksa hasil
    /// activity sebelumnya yang mungkin tidak menghasilkan apa-apa.
    /// </summary>
    [Designer(typeof(Design.FileExistsDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("File Exists")]
    [Description("Mengecek apakah sebuah berkas ada.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.fileexists.png")]
    public sealed class FileExists : CodeActivity
    {
        public FileExists()
        {
            DisplayName = "File Exists";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Path")]
        [Description("Berkas yang mau dicek.")]
        public InArgument<string> Path { get; set; }

        [Category("Output")]
        [DisplayName("Exists")]
        [Description("True kalau berkasnya ada.")]
        public OutArgument<bool> Exists { get; set; }

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
                var exists = !string.IsNullOrWhiteSpace(path) && File.Exists(path);
                if (Exists != null) Exists.Set(context, exists);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

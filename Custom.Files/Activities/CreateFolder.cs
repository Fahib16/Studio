using System;
using System.Activities;
using System.ComponentModel;
using System.IO;

namespace Custom.Files
{
    /// <summary>
    /// Membuat folder, termasuk seluruh folder induknya yang belum ada.
    /// Folder yang sudah ada bukan kegagalan — Directory.CreateDirectory
    /// memang bersifat "pastikan ada".
    /// </summary>
    [Designer(typeof(Design.CreateFolderDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Create Folder")]
    [Description("Membuat folder beserta folder induknya kalau belum ada.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.createfolder.png")]
    public sealed class CreateFolder : CodeActivity
    {
        public CreateFolder()
        {
            DisplayName = "Create Folder";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Path")]
        [Description("Folder yang mau dibuat.")]
        public InArgument<string> Path { get; set; }

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

                Directory.CreateDirectory(path);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

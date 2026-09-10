using System;
using System.Activities;
using System.ComponentModel;
using System.IO;

namespace Custom.Files
{
    /// <summary>
    /// Menghapus satu berkas.
    ///
    /// ContinueIfMissing default TRUE karena maksud orang menghapus berkas
    /// hampir selalu "pastikan berkas ini tidak ada" — dan kalau memang sudah
    /// tidak ada, itu bukan kegagalan.
    /// </summary>
    [Designer(typeof(Design.DeleteFileDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Delete File")]
    [Description("Menghapus berkas.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.deletefile.png")]
    public sealed class DeleteFile : CodeActivity
    {
        public DeleteFile()
        {
            DisplayName = "Delete File";

            // Ditulis sebagai ekspresi VB, bukan Literal, supaya nilainya
            // kelihatan di kotak ekspresi (Literal tidak bisa ditampilkan
            // ExpressionTextBox sehingga kotaknya terlihat kosong).
            ContinueIfMissing = new InArgument<bool>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<bool>("True"));
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Path")]
        [Description("Berkas yang mau dihapus.")]
        public InArgument<string> Path { get; set; }

        [Category("Options")]
        [DisplayName("Continue If Missing")]
        [Description("Kalau true (default), berkas yang memang tidak ada dianggap sudah terhapus.")]
        public InArgument<bool> ContinueIfMissing { get; set; }

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

                var continueIfMissing = ContinueIfMissing == null || ContinueIfMissing.Get(context);

                if (!File.Exists(path))
                {
                    if (continueIfMissing) return;
                    throw new FileNotFoundException("Berkas tidak ditemukan: " + path, path);
                }

                File.Delete(path);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

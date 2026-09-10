using System;
using System.Activities;
using System.ComponentModel;
using System.IO;

namespace Custom.Files
{
    /// <summary>
    /// Menulis satu string ke berkas teks, menimpa isi lama.
    /// Foldernya dibuat otomatis kalau belum ada, dengan alasan yang sama
    /// seperti Copy File.
    ///
    /// Untuk menambah di akhir berkas, pakai Append Line.
    /// </summary>
    [Designer(typeof(Design.WriteTextFileDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Write Text File")]
    [Description("Menulis teks ke berkas, menimpa isi lama.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.writetextfile.png")]
    public sealed class WriteTextFile : CodeActivity
    {
        public WriteTextFile()
        {
            DisplayName = "Write Text File";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Path")]
        [Description("Berkas tujuan.")]
        public InArgument<string> Path { get; set; }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Content")]
        [Description("Teks yang ditulis.")]
        public InArgument<string> Content { get; set; }

        [Category("Options")]
        [DisplayName("Encoding")]
        [Description("Default berarti UTF-8 tanpa BOM.")]
        [DefaultValue(TextFileEncoding.Default)]
        public TextFileEncoding Encoding { get; set; } = TextFileEncoding.Default;

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

                var content = Content != null ? Content.Get(context) : null;
                if (content == null) content = "";

                var folder = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder)) Directory.CreateDirectory(folder);

                var encoding = TextFileEncodings.Resolve(Encoding);
                if (encoding == null) File.WriteAllText(path, content);
                else File.WriteAllText(path, content, encoding);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

using System;
using System.Activities;
using System.ComponentModel;
using System.IO;

namespace Custom.Files
{
    /// <summary>
    /// Menambahkan satu baris di akhir berkas teks. Berkas dan foldernya
    /// dibuat dulu kalau belum ada, karena pemakaian paling umum activity ini
    /// adalah menulis log yang berkasnya belum tentu sudah ada.
    /// </summary>
    [Designer(typeof(Design.AppendLineDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Append Line")]
    [Description("Menambahkan satu baris di akhir berkas teks.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.appendline.png")]
    public sealed class AppendLine : CodeActivity
    {
        public AppendLine()
        {
            DisplayName = "Append Line";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Path")]
        [Description("Berkas tujuan.")]
        public InArgument<string> Path { get; set; }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Content")]
        [Description("Baris yang ditambahkan (pemisah baris ditambahkan otomatis).")]
        public InArgument<string> Content { get; set; }

        [Category("Options")]
        [DisplayName("Encoding")]
        [Description("Default berarti UTF-8 tanpa BOM. Hanya berpengaruh saat berkas baru dibuat.")]
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
                if (encoding == null) File.AppendAllText(path, content + Environment.NewLine);
                else File.AppendAllText(path, content + Environment.NewLine, encoding);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

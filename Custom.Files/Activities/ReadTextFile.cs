using System;
using System.Activities;
using System.ComponentModel;
using System.IO;

namespace Custom.Files
{
    /// <summary>
    /// Membaca seluruh isi berkas teks menjadi satu string.
    /// </summary>
    [Designer(typeof(Design.ReadTextFileDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Read Text File")]
    [Description("Membaca isi berkas teks.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.readtextfile.png")]
    public sealed class ReadTextFile : CodeActivity
    {
        public ReadTextFile()
        {
            DisplayName = "Read Text File";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Path")]
        [Description("Berkas teks yang mau dibaca.")]
        public InArgument<string> Path { get; set; }

        /// <summary>
        /// Plain property, bukan InArgument: pilihannya ditentukan saat mendesain
        /// workflow dan muncul sebagai dropdown di Properties panel.
        /// </summary>
        [Category("Options")]
        [DisplayName("Encoding")]
        [Description("Default berarti UTF-8 dengan deteksi BOM.")]
        [DefaultValue(TextFileEncoding.Default)]
        public TextFileEncoding Encoding { get; set; } = TextFileEncoding.Default;

        [Category("Output")]
        [DisplayName("Content")]
        [Description("Isi berkas sebagai satu string.")]
        public OutArgument<string> Content { get; set; }

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

                var encoding = TextFileEncodings.Resolve(Encoding);
                var text = encoding == null ? File.ReadAllText(path) : File.ReadAllText(path, encoding);

                if (Content != null) Content.Set(context, text);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

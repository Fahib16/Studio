using System;
using System.Activities;
using System.ComponentModel;
using System.Data;

namespace Custom.Data
{
    /// <summary>
    /// Menulis DataTable ke berkas CSV. Folder tujuan dibuat otomatis kalau
    /// belum ada.
    ///
    /// Nilai yang mengandung pemisah, kutip, atau baris baru otomatis dikutip
    /// sesuai aturan CSV, jadi hasilnya tetap bisa dibaca ulang oleh Read CSV
    /// maupun Excel.
    /// </summary>
    [Designer(typeof(Design.WriteCsvDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Write CSV")]
    [Description("Menulis DataTable ke berkas CSV.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.writecsv.png")]
    public sealed class WriteCsv : CodeActivity
    {
        public WriteCsv()
        {
            DisplayName = "Write CSV";
            HasHeaders = new InArgument<bool>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<bool>("True"));
            Delimiter = new InArgument<string>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<string>("\",\""));
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Data Table")]
        public InArgument<DataTable> DataTable { get; set; }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Path")]
        [Description("Berkas CSV yang dihasilkan.")]
        public InArgument<string> Path { get; set; }

        [Category("Options")]
        [DisplayName("Delimiter")]
        [Description("Pemisah kolom, satu karakter (default koma).")]
        public InArgument<string> Delimiter { get; set; }

        [Category("Options")]
        [DisplayName("Has Headers")]
        [Description("True (default): nama kolom ditulis sebagai baris pertama.")]
        public InArgument<bool> HasHeaders { get; set; }

        [Category("Options")]
        [DisplayName("Append")]
        [Description("Kalau true, isi ditambahkan di akhir berkas yang sudah ada " +
                     "(baris judul dilewati kalau berkasnya sudah berisi). Default false: berkas ditimpa.")]
        public InArgument<bool> Append { get; set; }

        [Category("Options")]
        [DisplayName("Encoding")]
        [Description("Default berarti UTF-8 tanpa BOM. Pakai Utf8WithBom kalau hasilnya dibuka di Excel.")]
        [DefaultValue(CsvEncoding.Default)]
        public CsvEncoding Encoding { get; set; } = CsvEncoding.Default;

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
                var table = DataTable.Get(context);
                if (table == null) throw new ArgumentException("Data Table kosong.");

                var path = Path.Get(context);
                if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path kosong.");

                var delimiterText = Delimiter != null ? Delimiter.Get(context) : null;
                var delimiter = string.IsNullOrEmpty(delimiterText) ? ',' : delimiterText[0];

                var hasHeaders = HasHeaders == null || HasHeaders.Get(context);
                var append = Append != null && Append.Get(context);

                var fileHasContent = append && System.IO.File.Exists(path) &&
                                     new System.IO.FileInfo(path).Length > 0;

                // Baris judul tidak ditulis ulang saat menambahkan ke berkas
                // yang sudah berisi — kalau ditulis, hasilnya berkas dengan
                // judul terselip di tengah data.
                var content = CsvFile.Write(table, delimiter, hasHeaders && !fileHasContent);

                if (append)
                {
                    var e = CsvFile.Resolve(Encoding);
                    var folder = System.IO.Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(folder) && !System.IO.Directory.Exists(folder))
                        System.IO.Directory.CreateDirectory(folder);

                    if (e == null) System.IO.File.AppendAllText(path, content);
                    else System.IO.File.AppendAllText(path, content, e);
                    return;
                }

                CsvFile.WriteAllText(path, content, Encoding);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

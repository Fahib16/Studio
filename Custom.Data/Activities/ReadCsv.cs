using System;
using System.Activities;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;

namespace Custom.Data
{
    /// <summary>
    /// Membaca berkas CSV menjadi DataTable.
    ///
    /// Semua kolom bertipe String. Menebak tipe kolom sengaja TIDAK dilakukan:
    /// tebakan salah pada satu baris (mis. kode pos 0812 menjadi angka 812,
    /// atau tanggal 03/04 yang tertukar bulan-tanggal) merusak data tanpa
    /// terlihat. Konversi tipe lebih baik dilakukan sadar-sadar di ekspresi.
    ///
    /// Baris yang selnya lebih sedikit dari jumlah kolom diisi string kosong,
    /// dan sel berlebih dibuang — berkas CSV nyata sering tidak rapi, dan
    /// menggagalkan seluruh pembacaan karena satu baris cacat jarang membantu.
    /// </summary>
    [Designer(typeof(Design.ReadCsvDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Read CSV")]
    [Description("Membaca berkas CSV menjadi DataTable (semua kolom String).")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.readcsv.png")]
    public sealed class ReadCsv : CodeActivity
    {
        public ReadCsv()
        {
            DisplayName = "Read CSV";
            HasHeaders = new InArgument<bool>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<bool>("True"));
            Delimiter = new InArgument<string>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<string>("\",\""));
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Path")]
        [Description("Berkas CSV yang dibaca.")]
        public InArgument<string> Path { get; set; }

        [Category("Options")]
        [DisplayName("Delimiter")]
        [Description("Pemisah kolom, satu karakter (default koma). Pakai ; untuk CSV gaya Eropa.")]
        public InArgument<string> Delimiter { get; set; }

        [Category("Options")]
        [DisplayName("Has Headers")]
        [Description("True (default): baris pertama dipakai sebagai nama kolom.")]
        public InArgument<bool> HasHeaders { get; set; }

        [Category("Options")]
        [DisplayName("Encoding")]
        [Description("Default berarti UTF-8 dengan deteksi BOM.")]
        [DefaultValue(CsvEncoding.Default)]
        public CsvEncoding Encoding { get; set; } = CsvEncoding.Default;

        [Category("Output")]
        [DisplayName("Data Table")]
        public OutArgument<DataTable> DataTable { get; set; }

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
                if (!File.Exists(path)) throw new FileNotFoundException("Berkas CSV tidak ditemukan: " + path, path);

                var delimiterText = Delimiter != null ? Delimiter.Get(context) : null;
                var delimiter = string.IsNullOrEmpty(delimiterText) ? ',' : delimiterText[0];
                var hasHeaders = HasHeaders == null || HasHeaders.Get(context);

                var text = CsvFile.ReadAllText(path, Encoding);
                var rows = CsvFile.Parse(text, delimiter);

                var table = new DataTable(System.IO.Path.GetFileNameWithoutExtension(path));

                if (rows.Count == 0)
                {
                    if (DataTable != null) DataTable.Set(context, table);
                    return;
                }

                var columnCount = rows.Max(r => r.Count);

                if (hasHeaders)
                {
                    var header = rows[0];
                    for (int i = 0; i < columnCount; i++)
                        table.Columns.Add(CsvFile.SafeColumnName(table, i < header.Count ? header[i] : null, i),
                                          typeof(string));
                    rows.RemoveAt(0);
                }
                else
                {
                    for (int i = 0; i < columnCount; i++)
                        table.Columns.Add(CsvFile.SafeColumnName(table, null, i), typeof(string));
                }

                foreach (var row in rows)
                {
                    var values = new object[columnCount];
                    for (int i = 0; i < columnCount; i++) values[i] = i < row.Count ? row[i] : "";
                    table.Rows.Add(values);
                }

                if (DataTable != null) DataTable.Set(context, table);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

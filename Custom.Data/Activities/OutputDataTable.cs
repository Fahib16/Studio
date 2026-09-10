using System;
using System.Activities;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;

namespace Custom.Data
{
    public enum DataTableTextFormat
    {
        /// <summary>Kolom disejajarkan dengan spasi — enak dibaca di panel log.</summary>
        Aligned,

        /// <summary>Dipisah koma dengan aturan kutip CSV — enak ditempel ke Excel.</summary>
        Csv
    }

    /// <summary>
    /// Mengubah DataTable menjadi teks, untuk ditulis ke log atau ditampilkan.
    ///
    /// Bentuk Aligned dipakai sebagai default karena tujuan utama activity ini
    /// adalah MEMBACA isi tabel saat menelusuri workflow; kalau yang dibutuhkan
    /// berkas, pakai Write CSV yang menulis langsung ke disk.
    /// </summary>
    [Designer(typeof(Design.OutputDataTableDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Output Data Table")]
    [Description("Mengubah DataTable menjadi teks untuk logging.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.outputdatatable.png")]
    public sealed class OutputDataTable : CodeActivity
    {
        public OutputDataTable()
        {
            DisplayName = "Output Data Table";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Data Table")]
        public InArgument<DataTable> DataTable { get; set; }

        [Category("Options")]
        [DisplayName("Format")]
        [Description("Aligned (default) atau Csv.")]
        [DefaultValue(DataTableTextFormat.Aligned)]
        public DataTableTextFormat Format { get; set; } = DataTableTextFormat.Aligned;

        [Category("Options")]
        [DisplayName("Max Rows")]
        [Description("Batas jumlah baris yang ikut ditulis; 0 (default) berarti semua. " +
                     "Berguna supaya log tidak dibanjiri tabel besar.")]
        public InArgument<int> MaxRows { get; set; }

        [Category("Output")]
        [DisplayName("Text")]
        public OutArgument<string> Text { get; set; }

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

                var max = MaxRows != null ? MaxRows.Get(context) : 0;
                var rows = table.Rows.Cast<DataRow>();
                if (max > 0) rows = rows.Take(max);
                var list = rows.ToList();

                var headers = table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray();
                var text = Format == DataTableTextFormat.Csv
                    ? Csv(headers, list)
                    : Aligned(headers, list);

                if (max > 0 && table.Rows.Count > max)
                    text += Environment.NewLine + "... (" + (table.Rows.Count - max) + " baris lagi)";

                if (Text != null) Text.Set(context, text);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }

        private static string Cell(object value)
        {
            return value == null || value == DBNull.Value ? "" : value.ToString();
        }

        private static string Aligned(string[] headers, System.Collections.Generic.List<DataRow> rows)
        {
            var widths = headers.Select(h => h.Length).ToArray();

            foreach (var row in rows)
                for (int i = 0; i < headers.Length; i++)
                    widths[i] = Math.Max(widths[i], Cell(row[i]).Length);

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(" | ", headers.Select((h, i) => h.PadRight(widths[i]))));
            sb.AppendLine(string.Join("-+-", widths.Select(w => new string('-', w))));

            foreach (var row in rows)
                sb.AppendLine(string.Join(" | ",
                    headers.Select((h, i) => Cell(row[i]).PadRight(widths[i]))));

            return sb.ToString().TrimEnd();
        }

        private static string Csv(string[] headers, System.Collections.Generic.List<DataRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", headers.Select(CsvField)));

            foreach (var row in rows)
                sb.AppendLine(string.Join(",", headers.Select((h, i) => CsvField(Cell(row[i])))));

            return sb.ToString().TrimEnd();
        }

        private static string CsvField(string value)
        {
            if (value == null) return "";
            var needsQuote = value.IndexOf(',') >= 0 || value.IndexOf('"') >= 0 ||
                             value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0;
            if (!needsQuote) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}

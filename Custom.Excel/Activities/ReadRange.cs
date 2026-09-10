using System;
using System.Activities;
using System.ComponentModel;
using System.Data;
using ClosedXML.Excel;

namespace Custom.Excel
{
    /// <summary>
    /// Membaca sebuah range menjadi DataTable.
    ///
    /// Range kosong berarti seluruh bagian sheet yang terpakai (RangeUsed) —
    /// itu yang orang maksud dengan "baca sheet ini".
    ///
    /// Semua kolom bertipe String, berisi teks seperti yang TERLIHAT di Excel
    /// (mengikuti format sel). Alasannya sama dengan Read CSV di Custom.Data:
    /// menebak tipe merusak data tanpa terlihat, dan angka seri tanggal
    /// (45900) bukan yang diharapkan orang yang melihat "03/09/2026" di layar.
    /// </summary>
    [Designer(typeof(Design.ReadRangeDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Read Range")]
    [Description("Membaca range Excel menjadi DataTable (semua kolom String).")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.readrange.png")]
    public sealed class ReadRange : ExcelActivityBase
    {
        public ReadRange()
        {
            DisplayName = "Read Range";
            AddHeaders = new InArgument<bool>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<bool>("True"));
        }

        [Category("Input")]
        [DisplayName("Range")]
        [Description("Mis. A1:D20. Kosong berarti seluruh bagian sheet yang terpakai.")]
        public InArgument<string> Range { get; set; }

        [Category("Options")]
        [DisplayName("Add Headers")]
        [Description("True (default): baris pertama dipakai sebagai nama kolom.")]
        public InArgument<bool> AddHeaders { get; set; }

        [Category("Output")]
        [DisplayName("Data Table")]
        public OutArgument<DataTable> DataTable { get; set; }

        protected override void Execute(NativeActivityContext context)
        {
            var continueOnError = GetContinueOnError(context);

            try
            {
                var rangeText = Range != null ? Range.Get(context) : null;
                var addHeaders = AddHeaders == null || AddHeaders.Get(context);

                DataTable table = null;

                WithWorksheet(context, writes: false, createSheetIfMissing: false, action: sheet =>
                {
                    var range = string.IsNullOrWhiteSpace(rangeText) ? sheet.RangeUsed() : sheet.Range(rangeText);
                    table = ToDataTable(range, addHeaders, sheet.Name);
                });

                if (DataTable != null) DataTable.Set(context, table);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }

        internal static DataTable ToDataTable(IXLRange range, bool addHeaders, string sheetName)
        {
            var table = new DataTable(sheetName ?? "Sheet1");

            // RangeUsed() mengembalikan null untuk sheet yang benar-benar
            // kosong; itu bukan error, hasilnya tabel kosong.
            if (range == null) return table;

            var rows = range.RowCount();
            var cols = range.ColumnCount();
            var firstDataRow = 1;

            if (addHeaders && rows > 0)
            {
                for (int c = 1; c <= cols; c++)
                    table.Columns.Add(
                        ExcelHelpers.SafeColumnName(table, ExcelHelpers.CellText(range.Cell(1, c)), c - 1),
                        typeof(string));
                firstDataRow = 2;
            }
            else
            {
                for (int c = 1; c <= cols; c++)
                    table.Columns.Add(ExcelHelpers.SafeColumnName(table, null, c - 1), typeof(string));
            }

            for (int r = firstDataRow; r <= rows; r++)
            {
                var values = new object[cols];
                for (int c = 1; c <= cols; c++) values[c - 1] = ExcelHelpers.CellText(range.Cell(r, c));
                table.Rows.Add(values);
            }

            return table;
        }
    }
}

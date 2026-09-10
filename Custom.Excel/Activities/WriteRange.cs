using System;
using System.Activities;
using System.ComponentModel;
using System.Data;
using ClosedXML.Excel;

namespace Custom.Excel
{
    /// <summary>
    /// Menulis DataTable ke worksheet mulai dari satu sel.
    ///
    /// Sel yang ditimpa hanya seluas isi DataTable — sisa sheet tidak
    /// dibersihkan. Kalau tabel baru lebih pendek dari data lama, sisa baris
    /// lama akan tetap ada; itu perilaku yang sama dengan Write Range UiPath,
    /// dan membersihkan seluruh sheet diam-diam justru berbahaya.
    ///
    /// Nilai ditulis dengan tipe aslinya (angka tetap angka, tanggal tetap
    /// tanggal) supaya hasilnya bisa dihitung di Excel.
    /// </summary>
    [Designer(typeof(Design.WriteRangeDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Write Range")]
    [Description("Menulis DataTable ke worksheet mulai dari sel tertentu.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.writerange.png")]
    public sealed class WriteRange : ExcelActivityBase
    {
        public WriteRange()
        {
            DisplayName = "Write Range";
            StartCell = new InArgument<string>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<string>("\"A1\""));
            AddHeaders = new InArgument<bool>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<bool>("True"));
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Data Table")]
        public InArgument<DataTable> DataTable { get; set; }

        [Category("Input")]
        [DisplayName("Start Cell")]
        [Description("Sel kiri-atas tujuan (default A1).")]
        public InArgument<string> StartCell { get; set; }

        [Category("Options")]
        [DisplayName("Add Headers")]
        [Description("True (default): nama kolom ditulis sebagai baris pertama.")]
        public InArgument<bool> AddHeaders { get; set; }

        protected override void Execute(NativeActivityContext context)
        {
            var continueOnError = GetContinueOnError(context);

            try
            {
                var table = DataTable.Get(context);
                if (table == null) throw new ArgumentException("Data Table kosong.");

                var startCell = StartCell != null ? StartCell.Get(context) : null;
                if (string.IsNullOrWhiteSpace(startCell)) startCell = "A1";

                var addHeaders = AddHeaders == null || AddHeaders.Get(context);

                WithWorksheet(context, writes: true, createSheetIfMissing: true, action: sheet =>
                {
                    var anchor = sheet.Cell(startCell);
                    Write(sheet, anchor.Address.RowNumber, anchor.Address.ColumnNumber, table, addHeaders);
                });
            }
            catch (Exception) when (continueOnError)
            {
            }
        }

        internal static void Write(IXLWorksheet sheet, int firstRow, int firstColumn, DataTable table, bool addHeaders)
        {
            var row = firstRow;

            if (addHeaders)
            {
                for (int c = 0; c < table.Columns.Count; c++)
                    sheet.Cell(row, firstColumn + c).SetValue(table.Columns[c].ColumnName);
                row++;
            }

            foreach (DataRow dataRow in table.Rows)
            {
                for (int c = 0; c < table.Columns.Count; c++)
                    ExcelHelpers.SetCell(sheet.Cell(row, firstColumn + c), dataRow[c]);
                row++;
            }
        }
    }
}

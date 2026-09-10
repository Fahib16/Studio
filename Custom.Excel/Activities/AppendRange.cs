using System;
using System.Activities;
using System.ComponentModel;
using System.Data;

namespace Custom.Excel
{
    /// <summary>
    /// Menambahkan isi DataTable di bawah data yang sudah ada di worksheet.
    ///
    /// Baris judul HANYA ditulis kalau sheet-nya masih benar-benar kosong.
    /// Kalau tidak, judul akan terselip di tengah data — dan itu jenis
    /// kerusakan yang baru ketahuan setelah berkasnya dipakai orang lain.
    /// </summary>
    [Designer(typeof(Design.AppendRangeDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Append Range")]
    [Description("Menambahkan DataTable di bawah data yang sudah ada.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.appendrange.png")]
    public sealed class AppendRange : ExcelActivityBase
    {
        public AppendRange()
        {
            DisplayName = "Append Range";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Data Table")]
        public InArgument<DataTable> DataTable { get; set; }

        protected override void Execute(NativeActivityContext context)
        {
            var continueOnError = GetContinueOnError(context);

            try
            {
                var table = DataTable.Get(context);
                if (table == null) throw new ArgumentException("Data Table kosong.");

                WithWorksheet(context, writes: true, createSheetIfMissing: true, action: sheet =>
                {
                    var lastRow = sheet.LastRowUsed();
                    var isEmpty = lastRow == null;
                    var firstRow = isEmpty ? 1 : lastRow.RowNumber() + 1;

                    WriteRange.Write(sheet, firstRow, 1, table, addHeaders: isEmpty);
                });
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

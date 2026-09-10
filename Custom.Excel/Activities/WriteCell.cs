using System;
using System.Activities;
using System.ComponentModel;

namespace Custom.Excel
{
    /// <summary>
    /// Menulis satu nilai ke satu sel.
    ///
    /// Value bertipe Object, bukan String, supaya angka tetap tersimpan
    /// sebagai angka dan tanggal sebagai tanggal — kalau semuanya ditulis
    /// sebagai teks, hasilnya tidak bisa dijumlahkan atau diurutkan di Excel.
    /// </summary>
    [Designer(typeof(Design.WriteCellDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Write Cell")]
    [Description("Menulis nilai ke satu sel Excel.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.writecell.png")]
    public sealed class WriteCell : ExcelActivityBase
    {
        public WriteCell()
        {
            DisplayName = "Write Cell";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Cell")]
        [Description("Alamat sel, mis. B4.")]
        public InArgument<string> Cell { get; set; }

        [Category("Input")]
        [DisplayName("Value")]
        [Description("Nilai yang ditulis. Tipe aslinya dipertahankan.")]
        public InArgument<object> Value { get; set; }

        protected override void Execute(NativeActivityContext context)
        {
            var continueOnError = GetContinueOnError(context);

            try
            {
                var address = Cell.Get(context);
                if (string.IsNullOrWhiteSpace(address)) throw new ArgumentException("Cell kosong.");

                var value = Value != null ? Value.Get(context) : null;

                WithWorksheet(context, writes: true, createSheetIfMissing: true,
                              action: sheet => ExcelHelpers.SetCell(sheet.Cell(address), value));
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

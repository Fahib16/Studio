using System;
using System.Activities;
using System.ComponentModel;

namespace Custom.Excel
{
    /// <summary>
    /// Membaca isi satu sel sebagai teks — sama seperti yang terlihat di Excel
    /// (mengikuti format sel), dengan alasan yang sama seperti Read Range.
    /// Untuk nilai mentahnya, baca lewat Read Range lalu konversi sadar-sadar
    /// di ekspresi.
    /// </summary>
    [Designer(typeof(Design.ReadCellDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Read Cell")]
    [Description("Membaca isi satu sel Excel.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.readcell.png")]
    public sealed class ReadCell : ExcelActivityBase
    {
        public ReadCell()
        {
            DisplayName = "Read Cell";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Cell")]
        [Description("Alamat sel, mis. B4.")]
        public InArgument<string> Cell { get; set; }

        [Category("Output")]
        [DisplayName("Value")]
        public OutArgument<string> Value { get; set; }

        protected override void Execute(NativeActivityContext context)
        {
            var continueOnError = GetContinueOnError(context);

            try
            {
                var address = Cell.Get(context);
                if (string.IsNullOrWhiteSpace(address)) throw new ArgumentException("Cell kosong.");

                string text = null;
                WithWorksheet(context, writes: false, createSheetIfMissing: false,
                              action: sheet => text = ExcelHelpers.CellText(sheet.Cell(address)));

                if (Value != null) Value.Set(context, text ?? "");
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

using System;
using System.Activities;
using System.ComponentModel;
using System.Data;
using ClosedXML.Excel;

namespace Custom.Excel
{
    /// <summary>
    /// Membaca rentang sel dari berkas .xlsx menjadi DataTable, tanpa
    /// memerlukan Microsoft Excel terpasang.
    ///
    /// PINDAHAN dari project OpenRPA (OpenRPA.Activities.ReadRangeWorkbook) ke
    /// kategori Custom.Excel. Isi Execute-nya dipertahankan apa adanya —
    /// termasuk penjagaan berkas ber-password dan penamaan kolom kembar —
    /// karena bagian itu sudah terbukti dipakai. Yang berubah: nama tipenya,
    /// kartunya (kini ada tombol pemilih berkas), dan tambahan
    /// ContinueOnError supaya seragam dengan activity lain.
    ///
    /// BEDANYA DENGAN "Read Range" di kategori yang sama:
    ///   Read Range           bisa memakai Excel Application Scope (satu berkas
    ///                        dibuka sekali untuk banyak operasi), dan
    ///                        mengembalikan teks sesuai format sel.
    ///   Read Range Workbook  selalu membuka berkasnya sendiri, dan sengaja
    ///                        menolak berkas ber-password dengan pesan yang
    ///                        menjelaskan.
    /// Pasangan ini mengikuti pembagian yang sama di UiPath (keluarga Excel vs
    /// keluarga Workbook), jadi yang terbiasa di sana tidak perlu belajar ulang.
    /// </summary>
    [Designer(typeof(Design.ReadRangeWorkbookDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Read Range Workbook")]
    [Description("Membaca rentang sel dari berkas Excel (.xlsx) tanpa memerlukan Microsoft Excel.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.readrangeworkbook.png")]
    public class ReadRangeWorkbook : CodeActivity
    {
        public ReadRangeWorkbook()
        {
            DisplayName = "Read Range Workbook";

            SheetName = new InArgument<string>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<string>("\"Sheet1\""));
            AddHeaders = true;
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Workbook Path")]
        [Description("Lokasi berkas Excel (.xlsx) yang akan dibaca.")]
        public InArgument<string> WorkbookPath { get; set; }

        [Category("Input")]
        [DisplayName("Sheet Name")]
        [Description("Nama worksheet yang dibaca. Default \"Sheet1\".")]
        public InArgument<string> SheetName { get; set; }

        [Category("Input")]
        [DisplayName("Range")]
        [Description("Rentang sel yang dibaca (mis. A1:D10). Kosongkan untuk seluruh isi sheet.")]
        public InArgument<string> Range { get; set; }

        [Category("Options")]
        [DisplayName("Add Headers")]
        [Description("Centang kalau baris pertama berisi nama kolom.")]
        [DefaultValue(true)]
        public bool AddHeaders { get; set; }

        [Category("Options")]
        [DisplayName("Password")]
        [Description("ClosedXML tidak bisa membuka berkas berproteksi password. Kalau diisi, " +
                     "activity ini menolak dengan pesan yang menjelaskan — bukan gagal dengan error acak.")]
        public InArgument<string> Password { get; set; }

        [Category("Output")]
        [RequiredArgument]
        [DisplayName("DataTable")]
        [Description("Variabel DataTable penampung hasil pembacaan.")]
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
                string path = WorkbookPath.Get(context);
                string sheet = SheetName != null ? SheetName.Get(context) : null;
                if (string.IsNullOrWhiteSpace(sheet)) sheet = "Sheet1";

                string rangeStr = Range != null ? (Range.Get(context) ?? "") : "";
                string pass = Password != null ? Password.Get(context) : null;

                if (string.IsNullOrWhiteSpace(path))
                    throw new ArgumentException("Workbook Path tidak boleh kosong.");

                if (!System.IO.File.Exists(path))
                    throw new System.IO.FileNotFoundException("Berkas Excel tidak ditemukan: " + path, path);

                if (!string.IsNullOrEmpty(pass))
                    throw new NotSupportedException(
                        "ClosedXML belum mendukung pembacaan berkas Excel yang diproteksi password. " +
                        "Hapus dulu passwordnya, atau kosongkan properti Password.");

                var dt = new DataTable();

                using (var wb = new XLWorkbook(path))
                {
                    IXLWorksheet ws;
                    if (!wb.Worksheets.TryGetWorksheet(sheet, out ws))
                        throw new Exception("Worksheet \"" + sheet + "\" tidak ada di dalam berkas.");

                    var targetRange = string.IsNullOrEmpty(rangeStr) ? ws.RangeUsed() : ws.Range(rangeStr);

                    if (targetRange == null || targetRange.RowCount() == 0)
                    {
                        if (DataTable != null) DataTable.Set(context, dt);
                        return;
                    }

                    int colCount = targetRange.ColumnCount();
                    var firstRow = targetRange.FirstRow();

                    for (int i = 1; i <= colCount; i++)
                    {
                        if (AddHeaders)
                        {
                            var cellValue = firstRow.Cell(i).Value;
                            string colName = !cellValue.IsBlank ? cellValue.ToString() : "Column" + i;
                            if (string.IsNullOrWhiteSpace(colName)) colName = "Column" + i;

                            var baseColName = colName;
                            var suffix = 1;
                            while (dt.Columns.Contains(colName))
                            {
                                colName = baseColName + "_" + suffix;
                                suffix++;
                            }
                            dt.Columns.Add(colName);
                        }
                        else
                        {
                            dt.Columns.Add("Column" + i);
                        }
                    }

                    int startRow = AddHeaders ? 2 : 1;
                    for (int r = startRow; r <= targetRange.RowCount(); r++)
                    {
                        var excelRow = targetRange.Row(r);
                        var dtRow = dt.NewRow();

                        for (int c = 1; c <= colCount; c++)
                        {
                            var cellData = excelRow.Cell(c).Value;
                            dtRow[c - 1] = cellData.IsBlank ? (object)DBNull.Value : cellData.ToString();
                        }
                        dt.Rows.Add(dtRow);
                    }
                }

                if (DataTable != null) DataTable.Set(context, dt);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

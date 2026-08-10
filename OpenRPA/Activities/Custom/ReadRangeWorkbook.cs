using System;
using System.Activities;
using System.ComponentModel;
using System.Data;
using System.Linq;
using ClosedXML.Excel;

namespace OpenRPA.Activities
{
    [Designer(typeof(ReadRangeWorkbookDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [System.Drawing.ToolboxBitmap(typeof(ReadRangeWorkbook), "Resources.toolbox.readrangeworkbook.png")]
    [DisplayName("Read Range Workbook")]
    [Description("Membaca rentang sel dari file Excel (.xlsx) dan menyimpannya ke dalam DataTable tanpa memerlukan Microsoft Excel.")]
    public class ReadRangeWorkbook : CodeActivity
    {
        public ReadRangeWorkbook()
        {
            SheetName = new InArgument<string>("\"Sheet1\"");
            Range = new InArgument<string>("\"\"");
            AddHeaders = true; // Default bawaan
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Workbook Path")]
        [Description("Lokasi file Excel (.xlsx) yang akan dibaca.")]
        public InArgument<string> WorkbookPath { get; set; }

        [Category("Input")]
        [DisplayName("Sheet Name")]
        [Description("Nama worksheet yang akan dibaca. Default adalah \"Sheet1\".")]
        public InArgument<string> SheetName { get; set; }

        [Category("Input")]
        [DisplayName("Range")]
        [Description("Rentang sel yang akan dibaca (misal: \"A1:D10\"). Kosongkan \"\" untuk membaca seluruh data yang ada di sheet.")]
        public InArgument<string> Range { get; set; }

        [Category("Options")]
        [DisplayName("Add Headers")]
        [Description("Centang jika baris pertama dari rentang yang dibaca berisi nama kolom.")]
        public bool AddHeaders { get; set; }

        [Category("Options")]
        [DisplayName("Password")]
        [Description("Catatan: Library saat ini tidak mendukung dekripsi file. Kosongkan parameter ini.")]
        public InArgument<string> Password { get; set; }

        [Category("Output")]
        [RequiredArgument]
        [DisplayName("DataTable")]
        [Description("Variabel DataTable untuk menyimpan hasil pembacaan.")]
        public OutArgument<DataTable> DataTable { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            string path = WorkbookPath.Get(context);
            string sheet = SheetName.Get(context) ?? "Sheet1";
            string rangeStr = Range.Get(context) ?? "";
            string pass = Password.Get(context);

            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("WorkbookPath tidak boleh kosong.");

            if (!System.IO.File.Exists(path))
                throw new System.IO.FileNotFoundException($"File Excel tidak ditemukan di jalur: {path}");

            // Mencegah crash jika user mencoba membuka file ber-password menggunakan ClosedXML
            if (!string.IsNullOrEmpty(pass))
            {
                throw new NotSupportedException("Library ClosedXML yang digunakan pada Activity ini belum mendukung pembacaan file Excel yang diproteksi password. Silakan hapus password dari file Excel Anda terlebih dahulu atau kosongkan kolom Password.");
            }

            DataTable dt = new DataTable();

            // Buka workbook dengan cara standar tanpa argumen LoadOptions
            using (var wb = new XLWorkbook(path))
            {
                if (!wb.Worksheets.TryGetWorksheet(sheet, out IXLWorksheet ws))
                    throw new Exception($"Worksheet dengan nama '{sheet}' tidak ditemukan di dalam file.");

                IXLRange targetRange;

                if (string.IsNullOrEmpty(rangeStr))
                {
                    targetRange = ws.RangeUsed();
                }
                else
                {
                    targetRange = ws.Range(rangeStr);
                }

                if (targetRange == null || targetRange.RowCount() == 0)
                {
                    DataTable.Set(context, dt);
                    return;
                }

                int colCount = targetRange.ColumnCount();
                var firstRow = targetRange.FirstRow();

                for (int i = 1; i <= colCount; i++)
                {
                    if (AddHeaders)
                    {
                        var cellValue = firstRow.Cell(i).Value;
                        string colName = !cellValue.IsBlank ? cellValue.ToString() : $"Column{i}";

                        if (string.IsNullOrWhiteSpace(colName)) colName = $"Column{i}";

                        string baseColName = colName;
                        int suffix = 1;
                        while (dt.Columns.Contains(colName))
                        {
                            colName = $"{baseColName}_{suffix}";
                            suffix++;
                        }
                        dt.Columns.Add(colName);
                    }
                    else
                    {
                        dt.Columns.Add($"Column{i}");
                    }
                }

                int startRow = AddHeaders ? 2 : 1;
                for (int r = startRow; r <= targetRange.RowCount(); r++)
                {
                    var excelRow = targetRange.Row(r);
                    DataRow dtRow = dt.NewRow();

                    for (int c = 1; c <= colCount; c++)
                    {
                        var cellData = excelRow.Cell(c).Value;
                        dtRow[c - 1] = cellData.IsBlank ? (object)DBNull.Value : cellData.ToString();
                    }
                    dt.Rows.Add(dtRow);
                }
            }

            DataTable.Set(context, dt);
        }
    }
}
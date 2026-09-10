using System;
using System.Data;
using System.Globalization;
using System.IO;
using ClosedXML.Excel;

namespace Custom.Excel
{
    /// <summary>
    /// Workbook yang sedang terbuka di dalam sebuah Excel Application Scope.
    ///
    /// Dipasang sebagai execution property di NativeActivityContext dengan nama
    /// <see cref="PropertyName"/>, lalu dicari activity anak lewat
    /// context.Properties.Find. Itu mekanisme bawaan WF untuk "sesuatu yang
    /// berlaku selama scope berjalan" — sama seperti transaksi pada
    /// TransactionScope — jadi tidak perlu variabel global apa pun.
    /// </summary>
    public class ExcelWorkbookHandle : IDisposable
    {
        public const string PropertyName = "Custom.Excel.CurrentWorkbook";

        public ExcelWorkbookHandle(XLWorkbook workbook, string path, bool autoSave)
        {
            Workbook = workbook;
            Path = path;
            AutoSave = autoSave;
        }

        public XLWorkbook Workbook { get; private set; }
        public string Path { get; private set; }
        public bool AutoSave { get; private set; }

        public void Save()
        {
            if (Workbook == null) return;

            // SaveAs dipakai (bukan Save) supaya workbook yang dibuat baru —
            // yang belum punya berkas sama sekali — juga tersimpan.
            Workbook.SaveAs(Path);
        }

        public void Dispose()
        {
            if (Workbook == null) return;
            Workbook.Dispose();
            Workbook = null;
        }
    }

    internal static class ExcelHelpers
    {
        /// <summary>
        /// Buka berkas, atau buat workbook baru kalau berkasnya belum ada.
        /// Membuat baru adalah perilaku yang diinginkan untuk berkas keluaran;
        /// untuk berkas yang seharusnya sudah ada, kesalahan ketik pada path
        /// akan terlihat dari isinya yang kosong, bukan dari error.
        /// </summary>
        public static XLWorkbook OpenOrCreate(string path, bool createIfMissing)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Workbook Path kosong.");

            if (File.Exists(path)) return new XLWorkbook(path);

            if (!createIfMissing)
                throw new FileNotFoundException("Berkas Excel tidak ditemukan: " + path, path);

            var folder = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder)) Directory.CreateDirectory(folder);

            return new XLWorkbook();
        }

        /// <summary>
        /// Ambil worksheet berdasarkan nama; kalau nama kosong, pakai yang
        /// pertama. Membuat worksheet baru HANYA dilakukan kalau diminta
        /// (createIfMissing), supaya salah ketik nama sheet saat MEMBACA tidak
        /// diam-diam menghasilkan sheet kosong.
        /// </summary>
        public static IXLWorksheet GetWorksheet(XLWorkbook workbook, string name, bool createIfMissing)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                if (workbook.Worksheets.Count == 0)
                {
                    if (!createIfMissing) throw new InvalidOperationException("Workbook tidak punya worksheet.");
                    return workbook.Worksheets.Add("Sheet1");
                }
                return workbook.Worksheet(1);
            }

            IXLWorksheet sheet;
            if (workbook.TryGetWorksheet(name, out sheet)) return sheet;

            if (!createIfMissing)
                throw new ArgumentException("Worksheet tidak ditemukan: " + name);

            return workbook.Worksheets.Add(name);
        }

        /// <summary>
        /// Isi sel sebagai teks seperti yang TERLIHAT di Excel (mengikuti
        /// format sel), bukan nilai mentahnya. Yang dilihat orang saat membuka
        /// berkasnya itulah yang mereka harapkan masuk ke DataTable — tanggal
        /// sebagai tanggal, bukan angka seri 45900.
        /// </summary>
        public static string CellText(IXLCell cell)
        {
            if (cell == null) return "";
            try
            {
                return cell.GetFormattedString(CultureInfo.CurrentCulture) ?? "";
            }
            catch (Exception)
            {
                // Sel rumus yang belum pernah dihitung Excel bisa melempar;
                // teks apa adanya lebih berguna daripada menggagalkan pembacaan
                // seluruh range karena satu sel.
                return cell.GetString() ?? "";
            }
        }

        /// <summary>
        /// Tulis nilai apa pun ke sel. XLCellValue.FromObject menjaga tipe
        /// aslinya (angka tetap angka, tanggal tetap tanggal) — kalau semuanya
        /// ditulis sebagai string, hasilnya tidak bisa dijumlahkan di Excel.
        /// </summary>
        public static void SetCell(IXLCell cell, object value)
        {
            if (value == null || value == DBNull.Value)
            {
                cell.Clear(XLClearOptions.Contents);
                return;
            }

            cell.SetValue(XLCellValue.FromObject(value, CultureInfo.CurrentCulture));
        }

        /// <summary>Nama kolom yang aman dan tidak kembar untuk DataTable.</summary>
        public static string SafeColumnName(DataTable table, string name, int index)
        {
            if (string.IsNullOrWhiteSpace(name)) name = "Column" + (index + 1);
            if (!table.Columns.Contains(name)) return name;

            var i = 1;
            while (table.Columns.Contains(name + "_" + i)) i++;
            return name + "_" + i;
        }
    }
}

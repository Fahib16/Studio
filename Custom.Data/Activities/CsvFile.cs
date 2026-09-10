using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;

namespace Custom.Data
{
    /// <summary>
    /// Pilihan encoding untuk Read CSV / Write CSV.
    ///
    /// Enum ini SENGAJA berdiri sendiri, tidak memakai milik Custom.Files,
    /// supaya project ini tidak bergantung pada project kategori lain hanya
    /// untuk satu dropdown. Nilainya tidak pernah berpindah antar activity
    /// (cuma pilihan saat mendesain), jadi duplikasi ini tidak menimbulkan
    /// masalah kecocokan tipe.
    /// </summary>
    public enum CsvEncoding
    {
        /// <summary>Baca: UTF-8 dengan deteksi BOM. Tulis: UTF-8 tanpa BOM.</summary>
        Default,
        Utf8,

        /// <summary>UTF-8 dengan BOM — dibutuhkan Excel supaya huruf beraksen tidak rusak.</summary>
        Utf8WithBom,
        Unicode,
        Windows1252
    }

    /// <summary>
    /// Pembaca dan penulis CSV yang menangani kutip ganda, pemisah di dalam
    /// nilai, dan baris baru di dalam nilai.
    ///
    /// Ditulis sendiri, bukan String.Split, karena Split merusak data pada
    /// kasus paling umum sekalipun (alamat yang mengandung koma) — dan bukan
    /// dengan menambah pustaka CSV baru, karena aturannya cukup sedikit untuk
    /// ditulis dan diuji langsung.
    /// </summary>
    internal static class CsvFile
    {
        public static Encoding Resolve(CsvEncoding value)
        {
            switch (value)
            {
                case CsvEncoding.Utf8: return new UTF8Encoding(false);
                case CsvEncoding.Utf8WithBom: return new UTF8Encoding(true);
                case CsvEncoding.Unicode: return Encoding.Unicode;
                case CsvEncoding.Windows1252: return Encoding.GetEncoding(1252);
                default: return null;
            }
        }

        /// <summary>Pecah seluruh isi CSV menjadi daftar baris berisi daftar sel.</summary>
        public static List<List<string>> Parse(string text, char delimiter)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();

            var inQuotes = false;
            var cellStarted = false;

            for (int i = 0; i < text.Length; i++)
            {
                var c = text[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        // Dua kutip berurutan di dalam kutip berarti satu kutip literal.
                        if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else cell.Append(c);
                    continue;
                }

                if (c == '"' && !cellStarted) { inQuotes = true; cellStarted = true; continue; }

                if (c == delimiter)
                {
                    row.Add(cell.ToString());
                    cell.Length = 0;
                    cellStarted = false;
                    continue;
                }

                if (c == '\r') continue;   // CRLF ditangani lewat '\n'

                if (c == '\n')
                {
                    row.Add(cell.ToString());
                    cell.Length = 0;
                    cellStarted = false;
                    rows.Add(row);
                    row = new List<string>();
                    continue;
                }

                cell.Append(c);
                cellStarted = true;
            }

            // Sel/baris terakhir tanpa baris baru penutup.
            if (cell.Length > 0 || cellStarted || row.Count > 0)
            {
                row.Add(cell.ToString());
                rows.Add(row);
            }

            return rows;
        }

        public static string Field(string value, char delimiter)
        {
            if (value == null) return "";

            var needsQuote = value.IndexOf(delimiter) >= 0 || value.IndexOf('"') >= 0 ||
                             value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0;

            return needsQuote ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }

        public static string Write(DataTable table, char delimiter, bool includeHeaders)
        {
            var sb = new StringBuilder();

            if (includeHeaders)
            {
                var names = new List<string>();
                foreach (DataColumn c in table.Columns) names.Add(Field(c.ColumnName, delimiter));
                sb.AppendLine(string.Join(delimiter.ToString(), names));
            }

            foreach (DataRow row in table.Rows)
            {
                var cells = new List<string>();
                for (int i = 0; i < table.Columns.Count; i++)
                {
                    var v = row[i];
                    cells.Add(Field(v == null || v == DBNull.Value ? "" : v.ToString(), delimiter));
                }
                sb.AppendLine(string.Join(delimiter.ToString(), cells));
            }

            return sb.ToString();
        }

        public static string ReadAllText(string path, CsvEncoding encoding)
        {
            var e = Resolve(encoding);
            return e == null ? File.ReadAllText(path) : File.ReadAllText(path, e);
        }

        public static void WriteAllText(string path, string content, CsvEncoding encoding)
        {
            var folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder)) Directory.CreateDirectory(folder);

            var e = Resolve(encoding);
            if (e == null) File.WriteAllText(path, content);
            else File.WriteAllText(path, content, e);
        }

        /// <summary>
        /// Nama kolom yang aman: yang kosong diberi nama Column1, Column2, ...
        /// dan yang kembar diberi akhiran _1 supaya DataTable tidak menolak.
        /// </summary>
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

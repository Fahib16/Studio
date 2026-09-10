using System;
using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Custom.Data
{
    /// <summary>
    /// Mengambil isi tabel menjadi DataTable, dari DUA sumber yang mungkin:
    ///
    /// 1. Selector — tabel di halaman web yang sedang terbuka, diambil lewat
    ///    Studio Bridge (aksi baru "extractTable" di background.js, mengikuti
    ///    pola pageOpsFn yang sudah ada). Ini jalur utamanya.
    /// 2. Html — potongan HTML yang sudah ada di tangan (mis. hasil Get
    ///    Attribute outerHTML, atau berkas HTML yang dibaca Read Text File).
    ///    Jalur ini tidak memerlukan browser sama sekali.
    ///
    /// Kalau keduanya diisi, Selector yang dipakai — sumber langsung dari
    /// halaman selalu lebih baru daripada HTML yang sudah tersimpan.
    ///
    /// Semua kolom bertipe String, dengan alasan yang sama seperti Read CSV:
    /// menebak tipe merusak data (kode pos, nomor telepon berawalan 0) tanpa
    /// terlihat.
    ///
    /// Penguraian HTML memakai regex, bukan parser HTML lengkap, mengikuti
    /// keputusan yang sama di background.js: yang dibutuhkan hanya baris dan
    /// sel, dan menambah pustaka parser HTML baru tidak sebanding untuk itu.
    /// Konsekuensinya sel yang memuat &lt;table&gt; bersarang tidak diurai
    /// dengan benar — kalau ketemu kasus itu, pakai jalur Selector.
    /// </summary>
    [Designer(typeof(Design.ExtractDataTableDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Extract Data Table")]
    [Description("Mengambil tabel dari halaman web (lewat Selector) atau dari HTML menjadi DataTable.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.extractdatatable.png")]
    public sealed class ExtractDataTable : CodeActivity
    {
        public ExtractDataTable()
        {
            DisplayName = "Extract Data Table";
            AddHeaders = new InArgument<bool>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<bool>("True"));
        }

        [Category("Target")]
        [DisplayName("Selector")]
        [Description("Selector web yang menunjuk tabel (atau elemen di dalamnya). " +
                     "Kosongkan kalau memakai Html.")]
        public InArgument<string> Selector { get; set; }

        [Category("Target")]
        [DisplayName("TabId")]
        [Description("Opsional. Kosongkan untuk memakai tab yang sedang aktif.")]
        public InArgument<int?> TabId { get; set; }

        [Category("Input")]
        [DisplayName("Html")]
        [Description("Alternatif Selector: HTML yang memuat elemen table.")]
        public InArgument<string> Html { get; set; }

        [Category("Options")]
        [DisplayName("Table Index")]
        [Description("Hanya untuk jalur Html: tabel ke berapa yang diambil, mulai dari 0 (default 0).")]
        public InArgument<int> TableIndex { get; set; }

        [Category("Options")]
        [DisplayName("Add Headers")]
        [Description("True (default): baris pertama dipakai sebagai nama kolom.")]
        public InArgument<bool> AddHeaders { get; set; }

        [Category("Options")]
        [DisplayName("Timeout")]
        [Description("Hanya untuk jalur Selector: berapa lama menunggu (default 10 detik kalau kosong).")]
        public InArgument<TimeSpan> Timeout { get; set; }

        [Category("Output")]
        [DisplayName("Data Table")]
        public OutArgument<DataTable> DataTable { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [Description("Kalau true, kegagalan activity ini tidak menghentikan workflow (default false).")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        [Browsable(false)]
        public string ScreenshotBase64 { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = ContinueOnError != null && ContinueOnError.Get(context);

            try
            {
                var selector = Selector != null ? Selector.Get(context) : null;
                var html = Html != null ? Html.Get(context) : null;
                var addHeaders = AddHeaders == null || AddHeaders.Get(context);

                List<List<string>> rows;

                if (!string.IsNullOrWhiteSpace(selector))
                {
                    rows = FromBrowser(context, selector);
                }
                else if (!string.IsNullOrWhiteSpace(html))
                {
                    var index = TableIndex != null ? TableIndex.Get(context) : 0;
                    rows = FromHtml(html, index);
                }
                else
                {
                    throw new ArgumentException("Isi salah satu dari Selector atau Html.");
                }

                var table = ToDataTable(rows, addHeaders);
                if (DataTable != null) DataTable.Set(context, table);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }

        private List<List<string>> FromBrowser(CodeActivityContext context, string selector)
        {
            var timeout = Timeout != null ? Timeout.Get(context) : TimeSpan.Zero;
            if (timeout <= TimeSpan.Zero) timeout = TimeSpan.FromSeconds(10);
            var timeoutMs = (int)timeout.TotalMilliseconds;

            var request = new JObject { ["selector"] = selector, ["timeoutMs"] = timeoutMs };

            var tabId = TabId != null ? TabId.Get(context) : null;
            if (tabId.HasValue) request["tabId"] = tabId.Value;

            var result = Custom.StudioBridge.StudioPipeClient.SendCommand(
                "extractTable", request, responseTimeoutMs: timeoutMs + 15000);

            var array = result?["rows"] as JArray;
            if (array == null) throw new InvalidOperationException("Studio Bridge tidak mengembalikan baris tabel.");

            return array
                .Select(r => (r as JArray)?.Select(c => c?.Value<string>() ?? "").ToList() ?? new List<string>())
                .ToList();
        }

        // ---------- Penguraian HTML ----------

        private static readonly Regex TableRe =
            new Regex(@"<table\b[^>]*>(.*?)</table\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex RowRe =
            new Regex(@"<tr\b[^>]*>(.*?)</tr\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex CellRe =
            new Regex(@"<t([hd])\b([^>]*)>(.*?)</t\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex ColspanRe =
            new Regex(@"colspan\s*=\s*[""']?(\d+)", RegexOptions.IgnoreCase);

        private static readonly Regex TagRe = new Regex(@"<[^>]+>", RegexOptions.Singleline);

        internal static List<List<string>> FromHtml(string html, int tableIndex)
        {
            var tables = TableRe.Matches(html).Cast<Match>().ToList();
            if (tables.Count == 0) throw new InvalidOperationException("Tidak ada elemen table di dalam Html.");
            if (tableIndex < 0 || tableIndex >= tables.Count)
                throw new ArgumentException("Table Index " + tableIndex + " di luar jumlah tabel yang ada (" +
                                            tables.Count + ").");

            var rows = new List<List<string>>();

            foreach (Match tr in RowRe.Matches(tables[tableIndex].Groups[1].Value))
            {
                var cells = new List<string>();

                foreach (Match td in CellRe.Matches(tr.Groups[1].Value))
                {
                    var text = CleanCell(td.Groups[3].Value);

                    var span = 1;
                    var m = ColspanRe.Match(td.Groups[2].Value);
                    if (m.Success) int.TryParse(m.Groups[1].Value, out span);
                    if (span < 1) span = 1;

                    for (int i = 0; i < span; i++) cells.Add(text);
                }

                if (cells.Count > 0) rows.Add(cells);
            }

            if (rows.Count == 0) throw new InvalidOperationException("Tabel ditemukan tapi tidak ada barisnya.");
            return rows;
        }

        private static string CleanCell(string inner)
        {
            var text = TagRe.Replace(inner, " ");
            text = System.Net.WebUtility.HtmlDecode(text);
            text = text.Replace(' ', ' ');            // &nbsp;
            text = Regex.Replace(text, @"\s+", " ");
            return text.Trim();
        }

        internal static DataTable ToDataTable(List<List<string>> rows, bool addHeaders)
        {
            var table = new DataTable("ExtractedTable");
            if (rows == null || rows.Count == 0) return table;

            var columnCount = rows.Max(r => r.Count);

            if (addHeaders)
            {
                var header = rows[0];
                for (int i = 0; i < columnCount; i++)
                    table.Columns.Add(SafeName(table, i < header.Count ? header[i] : null, i), typeof(string));
                rows = rows.Skip(1).ToList();
            }
            else
            {
                for (int i = 0; i < columnCount; i++)
                    table.Columns.Add(SafeName(table, null, i), typeof(string));
            }

            foreach (var row in rows)
            {
                var values = new object[columnCount];
                for (int i = 0; i < columnCount; i++) values[i] = i < row.Count ? row[i] : "";
                table.Rows.Add(values);
            }

            return table;
        }

        private static string SafeName(DataTable table, string name, int index)
        {
            if (string.IsNullOrWhiteSpace(name)) name = "Column" + (index + 1);
            if (!table.Columns.Contains(name)) return name;

            var i = 1;
            while (table.Columns.Contains(name + "_" + i)) i++;
            return name + "_" + i;
        }
    }
}

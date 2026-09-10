using System;
using System.Activities;
using System.ComponentModel;
using System.Data;
using System.Linq;

namespace Custom.Data
{
    /// <summary>
    /// Menyaring baris DataTable, dan opsional memilih kolom mana saja yang
    /// ikut ke hasil.
    ///
    /// Penyaringan memakai sintaks DataColumn.Expression bawaan .NET
    /// (dipakai lewat DataView.RowFilter), mis:
    ///     Umur &gt; 30 AND Kota = 'Jakarta'
    ///     Nama LIKE 'Budi%'
    /// Sintaks itu dipilih karena sudah ada, terdokumentasi Microsoft, dan
    /// tidak menuntut kita menulis parser ekspresi sendiri yang pasti akan
    /// berbeda perilakunya dari yang diharapkan orang.
    ///
    /// DataTable sumber TIDAK diubah — hasilnya selalu tabel baru.
    /// </summary>
    [Designer(typeof(Design.FilterDataTableDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Filter Data Table")]
    [Description("Menyaring baris DataTable dengan ekspresi, hasilnya tabel baru.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.filterdatatable.png")]
    public sealed class FilterDataTable : CodeActivity
    {
        public FilterDataTable()
        {
            DisplayName = "Filter Data Table";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Data Table")]
        [Description("Tabel sumber. Tidak diubah.")]
        public InArgument<DataTable> DataTable { get; set; }

        [Category("Input")]
        [DisplayName("Filter Expression")]
        [Description("Sintaks DataView.RowFilter, mis. Umur > 30 AND Kota = 'Jakarta'. " +
                     "Kosong berarti semua baris ikut.")]
        public InArgument<string> FilterExpression { get; set; }

        [Category("Options")]
        [DisplayName("Select Columns")]
        [Description("Opsional. Nama kolom yang ikut ke hasil, sesuai urutan yang ditulis. " +
                     "Kosong berarti semua kolom ikut.")]
        public InArgument<string[]> SelectColumns { get; set; }

        [Category("Options")]
        [DisplayName("Sort")]
        [Description("Opsional. Sintaks DataView.Sort, mis. Umur DESC.")]
        public InArgument<string> Sort { get; set; }

        [Category("Output")]
        [DisplayName("Result")]
        public OutArgument<DataTable> Result { get; set; }

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
                var source = DataTable.Get(context);
                if (source == null) throw new ArgumentException("Data Table kosong.");

                var view = new DataView(source);

                var filter = FilterExpression != null ? FilterExpression.Get(context) : null;
                if (!string.IsNullOrWhiteSpace(filter)) view.RowFilter = filter;

                var sort = Sort != null ? Sort.Get(context) : null;
                if (!string.IsNullOrWhiteSpace(sort)) view.Sort = sort;

                var columns = SelectColumns != null ? SelectColumns.Get(context) : null;
                columns = columns != null ? columns.Where(c => !string.IsNullOrWhiteSpace(c)).ToArray() : null;

                var result = (columns != null && columns.Length > 0)
                    ? view.ToTable(false, columns)
                    : view.ToTable();

                result.TableName = source.TableName;

                if (Result != null) Result.Set(context, result);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

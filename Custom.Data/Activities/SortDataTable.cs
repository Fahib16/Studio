using System;
using System.Activities;
using System.ComponentModel;
using System.Data;

namespace Custom.Data
{
    public enum SortOrder
    {
        Ascending,
        Descending
    }

    /// <summary>
    /// Mengurutkan DataTable berdasarkan satu kolom. Tabel sumber tidak
    /// diubah — hasilnya tabel baru.
    ///
    /// Pengurutan memakai DataView.Sort, jadi urutannya mengikuti TIPE kolom
    /// (angka diurutkan sebagai angka, tanggal sebagai tanggal), bukan sebagai
    /// teks. Ini yang membedakannya dari mengurutkan hasil ToString.
    /// </summary>
    [Designer(typeof(Design.SortDataTableDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Sort Data Table")]
    [Description("Mengurutkan DataTable berdasarkan satu kolom.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.sortdatatable.png")]
    public sealed class SortDataTable : CodeActivity
    {
        public SortDataTable()
        {
            DisplayName = "Sort Data Table";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Data Table")]
        [Description("Tabel sumber. Tidak diubah.")]
        public InArgument<DataTable> DataTable { get; set; }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Column")]
        [Description("Nama kolom yang dipakai untuk mengurutkan.")]
        public InArgument<string> Column { get; set; }

        [Category("Options")]
        [DisplayName("Order")]
        [Description("Ascending (default) atau Descending.")]
        [DefaultValue(SortOrder.Ascending)]
        public SortOrder Order { get; set; } = SortOrder.Ascending;

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

                var column = Column.Get(context);
                if (string.IsNullOrWhiteSpace(column)) throw new ArgumentException("Column kosong.");
                if (!source.Columns.Contains(column))
                    throw new ArgumentException("Kolom tidak ada di tabel: " + column);

                // Nama kolom dikurung [] supaya kolom yang mengandung spasi
                // atau tanda baca tetap bisa diurutkan.
                var view = new DataView(source)
                {
                    Sort = "[" + column + "] " + (Order == SortOrder.Descending ? "DESC" : "ASC")
                };

                var result = view.ToTable();
                result.TableName = source.TableName;

                if (Result != null) Result.Set(context, result);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

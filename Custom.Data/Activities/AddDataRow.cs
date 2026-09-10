using System;
using System.Activities;
using System.ComponentModel;
using System.Data;

namespace Custom.Data
{
    /// <summary>
    /// Menambahkan satu baris ke DataTable.
    ///
    /// Isi baris bisa diberikan lewat ArrayRow (Object() sesuai urutan kolom)
    /// ATAU DataRow (mis. hasil Get Row dari tabel lain). Kalau keduanya diisi,
    /// ArrayRow yang dipakai — bukan digabung, karena menggabungkan dua sumber
    /// data baris hanya akan menghasilkan tebakan.
    ///
    /// Tabel DIUBAH di tempat; tidak ada output tabel baru, mengikuti Add Data
    /// Row di UiPath.
    /// </summary>
    [Designer(typeof(Design.AddDataRowDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Add Data Row")]
    [Description("Menambahkan satu baris ke DataTable.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.adddatarow.png")]
    public sealed class AddDataRow : CodeActivity
    {
        public AddDataRow()
        {
            DisplayName = "Add Data Row";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Data Table")]
        [Description("Tabel tujuan. Diubah di tempat.")]
        public InArgument<DataTable> DataTable { get; set; }

        [Category("Input")]
        [DisplayName("Array Row")]
        [Description("Isi baris sesuai urutan kolom, mis. {\"Budi\", 30}.")]
        public InArgument<object[]> ArrayRow { get; set; }

        [Category("Input")]
        [DisplayName("Data Row")]
        [Description("Alternatif ArrayRow: baris dari tabel lain. Hanya kolom dengan nama " +
                     "yang sama yang disalin.")]
        public InArgument<DataRow> DataRow { get; set; }

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
                var table = DataTable.Get(context);
                if (table == null) throw new ArgumentException("Data Table kosong.");

                var array = ArrayRow != null ? ArrayRow.Get(context) : null;
                if (array != null)
                {
                    if (array.Length > table.Columns.Count)
                        throw new ArgumentException("Array Row punya " + array.Length +
                                                    " nilai, sedangkan tabel hanya punya " +
                                                    table.Columns.Count + " kolom.");
                    table.Rows.Add(array);
                    return;
                }

                var row = DataRow != null ? DataRow.Get(context) : null;
                if (row == null) throw new ArgumentException("Isi salah satu dari Array Row atau Data Row.");

                var target = table.NewRow();
                foreach (DataColumn c in table.Columns)
                {
                    if (row.Table.Columns.Contains(c.ColumnName))
                        target[c.ColumnName] = row[c.ColumnName];
                }
                table.Rows.Add(target);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

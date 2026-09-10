using System;
using System.Activities;
using System.ComponentModel;
using System.Data;

namespace Custom.Data
{
    /// <summary>
    /// Mengambil satu nilai dari sebuah DataRow.
    ///
    /// Kolom bisa ditunjuk lewat NAMA atau INDEKS. Nama diutamakan kalau
    /// keduanya diisi, karena nama tidak ikut berubah saat urutan kolom
    /// berubah — sedangkan indeks diam-diam menunjuk kolom lain.
    ///
    /// Output-nya Object (bukan String) supaya tipe aslinya tidak hilang;
    /// pakai .ToString di ekspresi kalau memang mau teksnya. Untuk memudahkan,
    /// tersedia juga output Text yang sudah dikonversi.
    /// </summary>
    [Designer(typeof(Design.GetRowItemDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Get Row Item")]
    [Description("Mengambil satu nilai dari DataRow berdasarkan nama atau indeks kolom.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.getrowitem.png")]
    public sealed class GetRowItem : CodeActivity
    {
        public GetRowItem()
        {
            DisplayName = "Get Row Item";
            ColumnIndex = new InArgument<int>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<int>("-1"));
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Row")]
        [Description("Baris sumber, mis. variabel dari For Each Row.")]
        public InArgument<DataRow> Row { get; set; }

        [Category("Input")]
        [DisplayName("Column Name")]
        [Description("Nama kolom. Diutamakan kalau Column Index juga diisi.")]
        public InArgument<string> ColumnName { get; set; }

        [Category("Input")]
        [DisplayName("Column Index")]
        [Description("Indeks kolom mulai dari 0. Dipakai hanya kalau Column Name kosong. " +
                     "Default -1 berarti tidak dipakai.")]
        public InArgument<int> ColumnIndex { get; set; }

        [Category("Output")]
        [DisplayName("Value")]
        [Description("Nilai apa adanya, tipe aslinya dipertahankan.")]
        public OutArgument<object> Value { get; set; }

        [Category("Output")]
        [DisplayName("Text")]
        [Description("Nilai yang sama dalam bentuk teks; sel kosong menjadi string kosong.")]
        public OutArgument<string> Text { get; set; }

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
                var row = Row.Get(context);
                if (row == null) throw new ArgumentException("Row kosong.");

                var name = ColumnName != null ? ColumnName.Get(context) : null;
                object value;

                if (!string.IsNullOrWhiteSpace(name))
                {
                    if (!row.Table.Columns.Contains(name))
                        throw new ArgumentException("Kolom tidak ada di baris ini: " + name);
                    value = row[name];
                }
                else
                {
                    var index = ColumnIndex != null ? ColumnIndex.Get(context) : -1;
                    if (index < 0)
                        throw new ArgumentException("Isi salah satu dari Column Name atau Column Index.");
                    if (index >= row.Table.Columns.Count)
                        throw new ArgumentException("Column Index " + index + " di luar jumlah kolom (" +
                                                    row.Table.Columns.Count + ").");
                    value = row[index];
                }

                if (value == DBNull.Value) value = null;

                if (Value != null) Value.Set(context, value);
                if (Text != null) Text.Set(context, value == null ? "" : value.ToString());
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

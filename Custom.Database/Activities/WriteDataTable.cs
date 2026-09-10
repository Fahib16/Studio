using System;
using System.Activities;
using System.ComponentModel;
using System.Data;

namespace CustomDatabase
{
    /// <summary>
    /// Menulis balik perubahan sebuah DataTable ke tabelnya di basis data.
    ///
    /// Pengganti Update From DataTable milik OpenRPA.
    ///
    /// Yang ditulis hanya baris yang BERUBAH — ditambah, diubah, atau dihapus.
    /// DataTable menyimpan sendiri keadaan tiap barisnya, jadi tabel yang baru
    /// dibaca lalu diserahkan ke sini tanpa disentuh tidak menghasilkan
    /// perintah apa pun. Itu perilaku yang benar, tapi sering disangka
    /// kegagalan; karena itu jumlahnya dikembalikan lewat Affected Rows.
    /// </summary>
    [Designer(typeof(Design.WriteDataTableDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Write DataTable To Database")]
    [Description("Menulis balik baris yang ditambah, diubah, atau dihapus dari sebuah DataTable.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.dbwrite.png")]
    public sealed class WriteDataTable : CodeActivity
    {
        public WriteDataTable()
        {
            DisplayName = "Write DataTable To Database";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Database")]
        [Description("Koneksi dari Database Scope; biasanya ekspresi \"db\".")]
        public InArgument<DatabaseHandle> Database { get; set; }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Table Name")]
        [Description("Nama tabel di basis data. Tabelnya harus punya kunci primer.")]
        public InArgument<string> TableName { get; set; }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Data Table")]
        [Description("DataTable yang perubahannya ditulis balik.")]
        public InArgument<DataTable> DataTable { get; set; }

        [Category("Input")]
        [DisplayName("Timeout")]
        [Description("Batas waktu perintah; default 30 detik.")]
        public InArgument<TimeSpan> Timeout { get; set; }

        [Category("Output")]
        [DisplayName("Affected Rows")]
        [Description("Jumlah baris yang benar-benar ditulis. Nol berarti tidak ada yang berubah.")]
        public OutArgument<int> AffectedRows { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [Description("Kalau true, kegagalan tidak menghentikan workflow (default false).")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = ContinueOnError != null && ContinueOnError.Get(context);

            try
            {
                var db = Database.Get(context);
                if (db == null)
                {
                    throw new InvalidOperationException(
                        "Write DataTable To Database: Database kosong. Activity ini harus berada " +
                        "di dalam Database Scope, dan properti Database diisi ekspresi \"db\".");
                }

                var detik = Timeout != null ? Timeout.Get(context) : TimeSpan.Zero;
                var batas = detik > TimeSpan.Zero ? (int)detik.TotalSeconds : 30;

                var jumlah = db.WriteBack(TableName.Get(context), DataTable.Get(context), batas);
                if (AffectedRows != null) AffectedRows.Set(context, jumlah);
            }
            catch (Exception) when (continueOnError)
            {
                if (AffectedRows != null) AffectedRows.Set(context, 0);
            }
        }
    }
}

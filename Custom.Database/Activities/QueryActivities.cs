using System;
using System.Activities;
using System.Collections;
using System.ComponentModel;
using System.Data;

namespace CustomDatabase
{
    /// <summary>
    /// Dasar bersama untuk activity yang menjalankan satu perintah SQL.
    ///
    /// Yang berbeda antar activity hanyalah APA yang dikembalikan; sambungan,
    /// parameter, jenis perintah, dan batas waktunya sama persis. Menaruhnya di
    /// satu tempat berarti perbaikan pada penanganan parameter berlaku untuk
    /// semuanya sekaligus.
    /// </summary>
    public abstract class DatabaseCommandActivity : CodeActivity
    {
        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Database")]
        [Description("Koneksi dari Database Scope; biasanya ekspresi \"db\".")]
        public InArgument<DatabaseHandle> Database { get; set; }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Query")]
        [Description("Perintah SQL. Pakai parameter (@nama), JANGAN menyambung nilai ke dalam teksnya.")]
        public InArgument<string> Query { get; set; }

        [Category("Input")]
        [DisplayName("Parameters")]
        [Description("Nilai untuk parameter di dalam Query, mis. " +
                     "new Dictionary(Of String, Object) From {{\"@id\", 7}}.")]
        public InArgument<IDictionary> Parameters { get; set; }

        [Category("Input")]
        [DisplayName("Command Type")]
        [Description("Text untuk SQL biasa, StoredProcedure untuk memanggil prosedur tersimpan.")]
        public CommandType CommandType { get; set; } = CommandType.Text;

        [Category("Input")]
        [DisplayName("Timeout")]
        [Description("Batas waktu perintah; default 30 detik.")]
        public InArgument<TimeSpan> Timeout { get; set; }

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
                        DisplayName + ": Database kosong. Activity ini harus berada di dalam " +
                        "Database Scope, dan properti Database diisi ekspresi \"db\".");
                }

                var detik = Timeout != null ? Timeout.Get(context) : TimeSpan.Zero;
                var batas = detik > TimeSpan.Zero ? (int)detik.TotalSeconds : 30;

                Jalankan(context, db, Query.Get(context), batas,
                         Parameters != null ? Parameters.Get(context) : null);
            }
            catch (Exception) when (continueOnError)
            {
                SaatGagal(context);
            }
        }

        protected abstract void Jalankan(CodeActivityContext context, DatabaseHandle db,
                                         string sql, int timeoutDetik, IDictionary parameters);

        /// <summary>Nilai keluaran saat kegagalan ditelan ContinueOnError.</summary>
        protected virtual void SaatGagal(CodeActivityContext context) { }
    }

    // ==================================================================

    /// <summary>
    /// Menjalankan SELECT dan mengembalikan hasilnya sebagai DataTable.
    /// </summary>
    [Designer(typeof(Design.ExecuteQueryDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Execute Query")]
    [Description("Menjalankan SELECT dan mengembalikan hasilnya sebagai DataTable.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.dbquery.png")]
    public sealed class ExecuteQuery : DatabaseCommandActivity
    {
        public ExecuteQuery()
        {
            DisplayName = "Execute Query";
        }

        [Category("Output")]
        [DisplayName("Data Table")]
        [Description("Hasil kuerinya.")]
        public OutArgument<DataTable> DataTable { get; set; }

        protected override void Jalankan(CodeActivityContext context, DatabaseHandle db,
                                         string sql, int timeoutDetik, IDictionary parameters)
        {
            var hasil = db.Query(sql, CommandType, timeoutDetik, parameters);
            if (DataTable != null) DataTable.Set(context, hasil);
        }

        protected override void SaatGagal(CodeActivityContext context)
        {
            // Tabel KOSONG, bukan null: workflow yang meneruskannya ke For Each
            // Data Row tidak boleh gagal untuk alasan kedua.
            if (DataTable != null) DataTable.Set(context, new DataTable());
        }
    }

    // ==================================================================

    /// <summary>
    /// Menjalankan INSERT, UPDATE, DELETE, atau DDL.
    /// </summary>
    [Designer(typeof(Design.ExecuteNonQueryDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Execute Non Query")]
    [Description("Menjalankan INSERT, UPDATE, DELETE, atau perintah lain yang tidak mengembalikan baris.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.dbnonquery.png")]
    public sealed class ExecuteNonQuery : DatabaseCommandActivity
    {
        public ExecuteNonQuery()
        {
            DisplayName = "Execute Non Query";
        }

        [Category("Output")]
        [DisplayName("Affected Rows")]
        [Description("Jumlah baris yang terpengaruh.")]
        public OutArgument<int> AffectedRows { get; set; }

        protected override void Jalankan(CodeActivityContext context, DatabaseHandle db,
                                         string sql, int timeoutDetik, IDictionary parameters)
        {
            var jumlah = db.NonQuery(sql, CommandType, timeoutDetik, parameters);
            if (AffectedRows != null) AffectedRows.Set(context, jumlah);
        }

        protected override void SaatGagal(CodeActivityContext context)
        {
            if (AffectedRows != null) AffectedRows.Set(context, 0);
        }
    }

    // ==================================================================

    /// <summary>
    /// Menjalankan kueri yang mengembalikan SATU nilai.
    /// </summary>
    [Designer(typeof(Design.ExecuteScalarDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Execute Scalar")]
    [Description("Menjalankan kueri yang mengembalikan satu nilai, mis. COUNT(*).")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.dbscalar.png")]
    public sealed class ExecuteScalar : DatabaseCommandActivity
    {
        public ExecuteScalar()
        {
            DisplayName = "Execute Scalar";
        }

        [Category("Output")]
        [DisplayName("Result")]
        [Description("Nilainya. Kosong (Nothing) kalau kuerinya tidak mengembalikan apa-apa atau NULL.")]
        public OutArgument<object> Result { get; set; }

        protected override void Jalankan(CodeActivityContext context, DatabaseHandle db,
                                         string sql, int timeoutDetik, IDictionary parameters)
        {
            var nilai = db.Scalar(sql, CommandType, timeoutDetik, parameters);
            if (Result != null) Result.Set(context, nilai);
        }

        protected override void SaatGagal(CodeActivityContext context)
        {
            if (Result != null) Result.Set(context, null);
        }
    }
}

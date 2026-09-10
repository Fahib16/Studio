using System;
using System.Activities;
using System.ComponentModel;

namespace CustomDatabase
{
    /// <summary>
    /// Membuka koneksi basis data dan menjalankan activity di dalamnya.
    ///
    /// Pola yang sama dengan Terminal Session dan Excel Application Scope:
    /// sambung sekali di awal, activity anak memakai koneksi yang sama lewat
    /// argumen "db", tutup di akhir — juga kalau isinya gagal di tengah jalan.
    /// Tanpa itu, koneksi menggantung sampai prosesnya berakhir, dan basis data
    /// dengan batas jumlah koneksi akan menolak jalan berikutnya.
    ///
    /// Kalau Use Transaction dinyalakan, seluruh isi Body berada dalam satu
    /// transaksi: berhasil semua, atau tidak sama sekali. Itu yang membuat
    /// robot aman diulang — jalan yang gagal di tengah tidak meninggalkan
    /// separuh perubahan.
    /// </summary>
    [Designer(typeof(Design.DatabaseScopeDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Database Scope")]
    [Description("Membuka koneksi basis data untuk activity di dalamnya, dan menutupnya di akhir.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.dbscope.png")]
    public sealed class DatabaseScope : NativeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public DatabaseScope()
        {
            DisplayName = "Database Scope";
        }

        [Category("Connection")]
        [RequiredArgument]
        [DisplayName("Connection String")]
        [Description("Mis. Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;")]
        public InArgument<string> ConnectionString { get; set; }

        [Category("Connection")]
        [DisplayName("Provider Name")]
        [Description("Nama invarian provider ADO.NET. Kosong berarti System.Data.SqlClient.")]
        public InArgument<string> ProviderName { get; set; }

        [Category("Connection")]
        [DisplayName("Use Transaction")]
        [Description("Kalau true, seluruh isi Body berada dalam satu transaksi: " +
                     "di-commit kalau selesai, dibatalkan kalau gagal (default false).")]
        public InArgument<bool> UseTransaction { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [Description("Kalau true, kegagalan menyambung tidak menghentikan workflow (default false).")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        [Browsable(false)]
        public ActivityAction<DatabaseHandle> Body { get; set; }

        private readonly Variable<DatabaseHandle> _handle = new Variable<DatabaseHandle>("_handle");
        private readonly Variable<bool> _transaksi = new Variable<bool>("_transaksi");

        /// <summary>
        /// Bentuk awal saat ditarik dari toolbox: Body-nya sudah berisi Sequence
        /// bernama "Do" dengan argumen "db", supaya activity berikutnya bisa
        /// langsung ditaruh di dalamnya.
        /// </summary>
        public Activity Create(System.Windows.DependencyObject target)
        {
            var instance = new DatabaseScope();

            instance.Body = new ActivityAction<DatabaseHandle>
            {
                Argument = new DelegateInArgument<DatabaseHandle> { Name = "db" },
                Handler = new System.Activities.Statements.Sequence { DisplayName = "Do" },
            };

            return instance;
        }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);

            metadata.AddImplementationVariable(_handle);
            metadata.AddImplementationVariable(_transaksi);
        }

        protected override void Execute(NativeActivityContext context)
        {
            var continueOnError = ContinueOnError != null && ContinueOnError.Get(context);

            DatabaseHandle handle = null;

            try
            {
                handle = new DatabaseHandle(
                    ProviderName != null ? ProviderName.Get(context) : null,
                    ConnectionString.Get(context));

                handle.Open();

                var pakaiTransaksi = UseTransaction != null && UseTransaction.Get(context);
                if (pakaiTransaksi) handle.BeginTransaction();

                context.SetValue(_handle, handle);
                context.SetValue(_transaksi, pakaiTransaksi);

                if (Body == null)
                {
                    Bereskan(handle, pakaiTransaksi, true);
                    return;
                }

                context.ScheduleAction(Body, handle, OnSelesai, OnGagal);
            }
            catch (Exception) when (continueOnError)
            {
                if (handle != null) handle.Dispose();
            }
        }

        private void OnSelesai(NativeActivityContext context, ActivityInstance completed)
        {
            Bereskan(context.GetValue(_handle), context.GetValue(_transaksi), true);
        }

        private void OnGagal(NativeActivityFaultContext faultContext, Exception propagated, ActivityInstance from)
        {
            // Transaksinya DIBATALKAN, bukan di-commit: isi Body gagal di tengah,
            // jadi separuh perubahan yang sudah terjadi tidak boleh bertahan.
            //
            // Galat aslinya sengaja tidak ditangani di sini supaya Try Catch di
            // tingkat workflow tetap bisa menanganinya.
            Bereskan(_handle.Get(faultContext), _transaksi.Get(faultContext), false);
        }

        private static void Bereskan(DatabaseHandle handle, bool pakaiTransaksi, bool berhasil)
        {
            if (handle == null) return;

            if (pakaiTransaksi)
            {
                if (berhasil) handle.Commit();
                else handle.Rollback();
            }

            handle.Dispose();
        }
    }
}

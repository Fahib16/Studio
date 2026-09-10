using System;
using System.Activities;
using System.ComponentModel;

namespace Custom.Excel
{
    /// <summary>
    /// Membuka satu berkas Excel, menjalankan activity di dalamnya, lalu
    /// menyimpan dan menutupnya.
    ///
    /// PENTING — project ini memakai ClosedXML (berkas .xlsx langsung), BUKAN
    /// Excel Interop. Konsekuensinya:
    ///
    /// - Excel TIDAK PERLU terpasang, dan tidak ada jendela Excel yang terbuka.
    ///   Karena itu properti "Visible" milik UiPath TIDAK ditiru: tidak ada
    ///   aplikasi yang bisa ditampilkan, jadi properti itu hanya akan jadi
    ///   hiasan yang menyesatkan. Kalau yang dibutuhkan adalah mengendalikan
    ///   Excel yang sedang terbuka di layar, pakai activity Excel milik
    ///   OpenRPA.Office yang memang berbasis Interop.
    /// - Berkas yang sedang DIBUKA di Excel tidak boleh ditulis oleh activity
    ///   ini; tutup dulu berkasnya di Excel.
    /// - Berkas .xls lama (format biner) tidak didukung ClosedXML, hanya .xlsx.
    ///
    /// Workbook yang terbuka dibagikan ke activity anak lewat execution
    /// property WF (lihat ExcelWorkbookHandle), jadi Read Range/Write Range di
    /// dalam scope cukup mengosongkan Workbook Path.
    /// </summary>
    [Designer(typeof(Design.ExcelApplicationScopeDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [System.Windows.Markup.ContentProperty("Body")]
    [DisplayName("Excel Application Scope")]
    [Description("Membuka berkas Excel untuk dipakai activity di dalamnya.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.excelscope.png")]
    public sealed class ExcelApplicationScope : NativeActivity
    {
        private readonly Variable<ExcelWorkbookHandle> handle =
            new Variable<ExcelWorkbookHandle>("excelWorkbookHandle");

        public ExcelApplicationScope()
        {
            DisplayName = "Excel Application Scope";
            AutoSave = new InArgument<bool>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<bool>("True"));
            CreateIfMissing = new InArgument<bool>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<bool>("True"));
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Workbook Path")]
        [Description("Berkas .xlsx yang dibuka.")]
        public InArgument<string> WorkbookPath { get; set; }

        [Category("Options")]
        [DisplayName("Auto Save")]
        [Description("True (default): workbook disimpan saat scope selesai tanpa error.")]
        public InArgument<bool> AutoSave { get; set; }

        [Category("Options")]
        [DisplayName("Create If Missing")]
        [Description("True (default): berkas dibuat baru kalau belum ada.")]
        public InArgument<bool> CreateIfMissing { get; set; }

        [Browsable(false)]
        public Activity Body { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [Description("Kalau true, kegagalan di dalam scope tidak menghentikan workflow (default false). " +
                     "Workbook TIDAK disimpan kalau isinya gagal — lihat catatan di README.")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);
            metadata.AddImplementationVariable(handle);
        }

        protected override void Execute(NativeActivityContext context)
        {
            var path = WorkbookPath.Get(context);
            var autoSave = AutoSave == null || AutoSave.Get(context);
            var createIfMissing = CreateIfMissing == null || CreateIfMissing.Get(context);

            var opened = new ExcelWorkbookHandle(
                ExcelHelpers.OpenOrCreate(path, createIfMissing), path, autoSave);

            handle.Set(context, opened);
            context.Properties.Add(ExcelWorkbookHandle.PropertyName, opened);

            if (Body == null)
            {
                Finish(context, saved: true);
                return;
            }

            context.ScheduleActivity(Body, OnBodyCompleted, OnBodyFaulted);
        }

        private void OnBodyCompleted(NativeActivityContext context, ActivityInstance completedInstance)
        {
            // Disimpan hanya kalau isi scope benar-benar selesai baik. Menyimpan
            // workbook yang prosesnya gagal di tengah jalan berarti menuliskan
            // keadaan setengah jadi ke berkas yang mungkin sudah berisi data
            // penting.
            Finish(context, saved: completedInstance.State == ActivityInstanceState.Closed);
        }

        private void OnBodyFaulted(NativeActivityFaultContext faultContext, Exception propagatedException,
                                   ActivityInstance propagatedFrom)
        {
            var continueOnError = ContinueOnError != null && ContinueOnError.Get(faultContext);

            var opened = handle.Get(faultContext);
            if (opened != null) opened.Dispose();
            handle.Set(faultContext, null);

            if (!continueOnError) return;   // exception dibiarkan naik apa adanya

            faultContext.CancelChild(propagatedFrom);
            faultContext.HandleFault();
        }

        private void Finish(NativeActivityContext context, bool saved)
        {
            var opened = handle.Get(context);
            if (opened == null) return;

            try
            {
                if (saved && opened.AutoSave) opened.Save();
            }
            finally
            {
                opened.Dispose();
                handle.Set(context, null);
            }
        }
    }
}

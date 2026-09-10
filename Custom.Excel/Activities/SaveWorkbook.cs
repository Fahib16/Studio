using System;
using System.Activities;
using System.ComponentModel;

namespace Custom.Excel
{
    /// <summary>
    /// Menyimpan workbook yang sedang dibuka Excel Application Scope, tanpa
    /// menunggu scope selesai.
    ///
    /// Berguna untuk proses panjang: hasil sebagian sudah aman di disk kalau
    /// workflow berhenti di tengah jalan. Kalau Save As Path diisi, workbook
    /// disimpan ke berkas LAIN dan scope tetap melanjutkan pada berkas
    /// aslinya — itu cara membuat salinan tanpa menutup apa pun.
    ///
    /// HANYA bisa dipakai di dalam scope: di luar scope tidak ada workbook
    /// terbuka yang bisa disimpan (activity lain menyimpan sendiri setiap kali
    /// selesai menulis).
    /// </summary>
    [Designer(typeof(Design.SaveWorkbookDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Save Workbook")]
    [Description("Menyimpan workbook yang sedang dibuka Excel Application Scope.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.saveworkbook.png")]
    public sealed class SaveWorkbook : NativeActivity
    {
        public SaveWorkbook()
        {
            DisplayName = "Save Workbook";
        }

        [Category("Input")]
        [DisplayName("Save As Path")]
        [Description("Opsional. Kalau diisi, workbook disimpan ke berkas lain dan " +
                     "scope tetap melanjutkan pada berkas aslinya.")]
        public InArgument<string> SaveAsPath { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [Description("Kalau true, kegagalan activity ini tidak menghentikan workflow (default false).")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        protected override void Execute(NativeActivityContext context)
        {
            var continueOnError = ContinueOnError != null && ContinueOnError.Get(context);

            try
            {
                var handle = context.Properties.Find(ExcelWorkbookHandle.PropertyName) as ExcelWorkbookHandle;
                if (handle == null)
                    throw new InvalidOperationException(
                        "Save Workbook hanya bisa dipakai di dalam Excel Application Scope.");

                var saveAs = SaveAsPath != null ? SaveAsPath.Get(context) : null;

                if (string.IsNullOrWhiteSpace(saveAs)) handle.Save();
                else handle.Workbook.SaveAs(saveAs);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

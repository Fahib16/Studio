using System;
using System.Activities;
using System.ComponentModel;
using ClosedXML.Excel;

namespace Custom.Excel
{
    /// <summary>
    /// Kelas dasar activity Excel.
    ///
    /// Setiap activity bisa dipakai DUA CARA:
    ///
    /// 1. Di dalam Excel Application Scope — Workbook Path dikosongkan, dan
    ///    workbook yang sedang terbuka dipakai bersama. Ini yang efisien untuk
    ///    banyak operasi pada satu berkas: berkasnya dibuka dan disimpan sekali.
    /// 2. Berdiri sendiri — Workbook Path diisi. Berkas dibuka, dikerjakan,
    ///    lalu (untuk operasi tulis) disimpan dan ditutup lagi.
    ///
    /// Dua cara ini disediakan karena memaksa scope untuk satu operasi tunggal
    /// membuat workflow sederhana jadi bertele-tele, sedangkan tanpa scope,
    /// sepuluh operasi berturut-turut berarti sepuluh kali buka-tutup berkas.
    ///
    /// Kelas ini abstract, jadi tidak ikut muncul di toolbox.
    /// </summary>
    public abstract class ExcelActivityBase : NativeActivity
    {
        [Category("Input")]
        [DisplayName("Workbook Path")]
        [Description("Kosongkan kalau activity ini berada di dalam Excel Application Scope.")]
        public InArgument<string> WorkbookPath { get; set; }

        [Category("Input")]
        [DisplayName("Sheet")]
        [Description("Nama worksheet. Kosong berarti worksheet pertama.")]
        public InArgument<string> Sheet { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [Description("Kalau true, kegagalan activity ini tidak menghentikan workflow (default false).")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        protected bool GetContinueOnError(NativeActivityContext context)
        {
            return ContinueOnError != null && ContinueOnError.Get(context);
        }

        /// <summary>
        /// Jalankan aksi terhadap worksheet yang tepat, tanpa activity turunan
        /// perlu tahu apakah workbook-nya milik scope atau dibuka sendiri.
        ///
        /// writes = true berarti hasilnya perlu disimpan: pada jalur berdiri
        /// sendiri berkas langsung disimpan, sedangkan di dalam scope
        /// penyimpanan diserahkan ke scope (AutoSave) supaya sepuluh penulisan
        /// tidak berarti sepuluh kali menulis berkas ke disk.
        /// </summary>
        protected void WithWorksheet(NativeActivityContext context, bool writes, bool createSheetIfMissing,
                                     Action<IXLWorksheet> action)
        {
            var sheetName = Sheet != null ? Sheet.Get(context) : null;
            var path = WorkbookPath != null ? WorkbookPath.Get(context) : null;

            if (string.IsNullOrWhiteSpace(path))
            {
                var handle = context.Properties.Find(ExcelWorkbookHandle.PropertyName) as ExcelWorkbookHandle;
                if (handle == null)
                    throw new InvalidOperationException(
                        "Workbook Path kosong dan activity ini tidak berada di dalam Excel Application Scope. " +
                        "Isi Workbook Path, atau letakkan activity ini di dalam scope.");

                action(ExcelHelpers.GetWorksheet(handle.Workbook, sheetName, createSheetIfMissing));
                return;
            }

            using (var workbook = ExcelHelpers.OpenOrCreate(path, createIfMissing: writes))
            {
                action(ExcelHelpers.GetWorksheet(workbook, sheetName, createSheetIfMissing));
                if (writes) workbook.SaveAs(path);
            }
        }
    }
}

using System;
using System.Activities;
using System.ComponentModel;

namespace CustomSystem
{
    /// <summary>
    /// Menjalankan ulang isi Body sampai berhasil.
    ///
    /// Satu percobaan dianggap GAGAL kalau Body melempar exception, ATAU kalau
    /// Condition diisi dan hasilnya False setelah Body selesai. Kedua bentuk
    /// kegagalan itu diperlakukan sama supaya activity ini juga bisa dipakai
    /// untuk pola "ulangi sampai elemen muncul", bukan hanya "ulangi kalau error".
    ///
    /// Catatan implementasi: penjadwalan ulang TIDAK bisa dilakukan di dalam
    /// fault handler (NativeActivityFaultContext tidak punya ScheduleActivity).
    /// Karena itu fault handler hanya menandai kegagalan lalu memanggil
    /// HandleFault, dan percobaan berikutnya dijadwalkan dari completion
    /// callback — yang tetap dipanggil setelah child dibatalkan.
    ///
    /// Jeda antar percobaan memakai System.Activities.Statements.Delay sebagai
    /// implementation child, bukan Thread.Sleep, supaya thread workflow tidak
    /// diblokir dan Stop tetap bisa memutus penungguan.
    /// </summary>
    [Designer(typeof(Design.RetryScopeDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [System.Windows.Markup.ContentProperty("Body")]
    [DisplayName("Retry Scope")]
    [Description("Mengulang Body saat gagal (exception) atau saat Condition bernilai False.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.retryscope.png")]
    public sealed class RetryScope : NativeActivity
    {
        private readonly Variable<int> attempts = new Variable<int>("retryAttempts");
        private readonly Variable<bool> giveUp = new Variable<bool>("retryGiveUp");
        private readonly Variable<TimeSpan> waitInterval = new Variable<TimeSpan>("retryWaitInterval");
        private readonly System.Activities.Statements.Delay retryDelay;

        public RetryScope()
        {
            DisplayName = "Retry Scope";

            // Nilai awal ditulis sebagai ekspresi VB, BUKAN Literal. Literal
            // memang tersimpan benar, tapi ExpressionTextBox tidak bisa
            // menampilkannya (Literal bukan ITextExpression) sehingga kotaknya
            // terlihat KOSONG padahal isinya 3 — persis jenis kebingungan yang
            // bikin orang mengisi ulang nilai yang sudah benar.
            NumberOfRetries = new InArgument<int>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<int>("3"));
            RetryInterval = new InArgument<TimeSpan>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<TimeSpan>("TimeSpan.FromSeconds(5)"));

            retryDelay = new System.Activities.Statements.Delay
            {
                DisplayName = "RetryInterval",
                Duration = new InArgument<TimeSpan>(waitInterval)
            };
        }

        [Category("Options")]
        [DisplayName("Number Of Retries")]
        [Description("Berapa kali Body diulang setelah percobaan pertama gagal (default 3).")]
        public InArgument<int> NumberOfRetries { get; set; }

        [Category("Options")]
        [DisplayName("Retry Interval")]
        [Description("Jeda sebelum percobaan berikutnya (default 00:00:05).")]
        public InArgument<TimeSpan> RetryInterval { get; set; }

        [Category("Options")]
        [DisplayName("Condition")]
        [Description("Opsional. Kalau diisi, percobaan dianggap berhasil hanya bila ekspresi ini " +
                     "bernilai True setelah Body selesai. Dievaluasi ulang setiap percobaan.")]
        public Activity<bool> Condition { get; set; }

        [Browsable(false)]
        public Activity Body { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [Description("Kalau true, kegagalan yang masih tersisa setelah percobaan terakhir tidak " +
                     "menghentikan workflow (default false).")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            // base menambahkan Body dan Condition sebagai child serta seluruh
            // InArgument publik lewat refleksi; hanya milik internal yang perlu
            // didaftarkan sendiri.
            base.CacheMetadata(metadata);
            metadata.AddImplementationVariable(attempts);
            metadata.AddImplementationVariable(giveUp);
            metadata.AddImplementationVariable(waitInterval);
            metadata.AddImplementationChild(retryDelay);
        }

        protected override void Execute(NativeActivityContext context)
        {
            if (Body == null) return;

            attempts.Set(context, 0);
            giveUp.Set(context, false);

            var interval = RetryInterval != null ? RetryInterval.Get(context) : TimeSpan.Zero;
            if (interval < TimeSpan.Zero) interval = TimeSpan.Zero;
            waitInterval.Set(context, interval);

            ScheduleBody(context);
        }

        private int MaxRetries(ActivityContext context)
        {
            var value = NumberOfRetries != null ? NumberOfRetries.Get(context) : 0;
            return value < 0 ? 0 : value;
        }

        private void ScheduleBody(NativeActivityContext context)
        {
            context.ScheduleActivity(Body, OnBodyCompleted, OnBodyFaulted);
        }

        private void ScheduleRetry(NativeActivityContext context)
        {
            if (waitInterval.Get(context) > TimeSpan.Zero)
            {
                context.ScheduleActivity(retryDelay, OnDelayCompleted);
                return;
            }

            ScheduleBody(context);
        }

        private void OnDelayCompleted(NativeActivityContext context, ActivityInstance completedInstance)
        {
            if (context.IsCancellationRequested) return;
            ScheduleBody(context);
        }

        private void OnBodyFaulted(NativeActivityFaultContext faultContext, Exception propagatedException,
                                   ActivityInstance propagatedFrom)
        {
            var done = attempts.Get(faultContext);

            if (done < MaxRetries(faultContext))
            {
                attempts.Set(faultContext, done + 1);
                faultContext.CancelChild(propagatedFrom);
                faultContext.HandleFault();
                return;
            }

            var continueOnError = ContinueOnError != null && ContinueOnError.Get(faultContext);
            if (continueOnError)
            {
                giveUp.Set(faultContext, true);
                faultContext.CancelChild(propagatedFrom);
                faultContext.HandleFault();
                return;
            }

            // Percobaan habis dan tidak diminta lanjut: exception dibiarkan naik
            // ke pemanggil apa adanya, supaya pesan error aslinya tidak hilang.
        }

        private void OnBodyCompleted(NativeActivityContext context, ActivityInstance completedInstance)
        {
            if (context.IsCancellationRequested) return;
            if (giveUp.Get(context)) return;

            if (completedInstance.State != ActivityInstanceState.Closed)
            {
                // Body gagal dan sudah ditangani di OnBodyFaulted.
                ScheduleRetry(context);
                return;
            }

            if (Condition == null) return;

            context.ScheduleActivity(Condition, OnConditionCompleted);
        }

        private void OnConditionCompleted(NativeActivityContext context, ActivityInstance completedInstance,
                                          bool result)
        {
            if (result || context.IsCancellationRequested) return;

            var done = attempts.Get(context);
            if (done < MaxRetries(context))
            {
                attempts.Set(context, done + 1);
                ScheduleRetry(context);
                return;
            }

            var continueOnError = ContinueOnError != null && ContinueOnError.Get(context);
            if (continueOnError) return;

            throw new InvalidOperationException(
                "Retry Scope: Condition masih False setelah " + done + " percobaan ulang.");
        }
    }
}

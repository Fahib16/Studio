using System;
using System.Activities;
using System.ComponentModel;

namespace CustomSystem
{
    /// <summary>
    /// Menunda eksekusi selama jangka waktu tertentu.
    ///
    /// System.Activities.Statements.Delay bawaan .NET sudah muncul di toolbox
    /// (kategori "System.Activities"). Yang ini dibuat supaya ada padanan di
    /// kategori Custom.System dengan kartu yang seragam, dan EKSEKUSINYA
    /// DIDELEGASIKAN ke Delay bawaan itu — bukan Thread.Sleep — supaya thread
    /// workflow tidak diblokir dan Stop/Cancel tetap bisa memutus penungguan.
    /// </summary>
    [Designer(typeof(Design.DelayDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Delay")]
    [Description("Menunggu selama jangka waktu tertentu sebelum melanjutkan.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.delay.png")]
    public sealed class Delay : NativeActivity
    {
        private readonly Variable<TimeSpan> resolvedDuration = new Variable<TimeSpan>("resolvedDuration");
        private readonly System.Activities.Statements.Delay inner;

        public Delay()
        {
            DisplayName = "Delay";

            // Duration Delay bawaan diikat ke variable implementasi, karena nilai
            // yang sesungguhnya baru diketahui saat Execute (setelah ekspresi
            // Duration dievaluasi dan divalidasi).
            inner = new System.Activities.Statements.Delay
            {
                DisplayName = "InnerDelay",
                Duration = new InArgument<TimeSpan>(resolvedDuration)
            };
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Duration")]
        [Description("Lama penundaan, mis. 00:00:05 untuk 5 detik.")]
        public InArgument<TimeSpan> Duration { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [Description("Kalau true, kegagalan menghitung Duration tidak menghentikan " +
                     "workflow — penundaan dilewati begitu saja (default false).")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            // base menambahkan argumen publik (Duration, ContinueOnError) lewat
            // refleksi; sisanya milik kita sendiri.
            base.CacheMetadata(metadata);
            metadata.AddImplementationVariable(resolvedDuration);
            metadata.AddImplementationChild(inner);
        }

        protected override void Execute(NativeActivityContext context)
        {
            var continueOnError = ContinueOnError != null && ContinueOnError.Get(context);

            TimeSpan duration;
            try
            {
                duration = Duration.Get(context);
            }
            catch (Exception) when (continueOnError)
            {
                return;
            }

            if (duration <= TimeSpan.Zero) return;

            resolvedDuration.Set(context, duration);
            context.ScheduleActivity(inner);
        }
    }
}

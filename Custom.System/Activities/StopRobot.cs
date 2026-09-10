using System;
using System.Activities;
using System.ComponentModel;
using Custom.Shared;

namespace CustomSystem
{
    /// <summary>
    /// Menghentikan jalan yang sedang berlangsung, dari dalam workflow.
    ///
    /// Pengganti Stop OpenRPA milik OpenRPA.
    ///
    /// Dipakai ketika workflow SENDIRI yang menyimpulkan tidak ada gunanya
    /// melanjutkan: berkas masukan kosong, antrean habis, atau prasyarat yang
    /// tidak terpenuhi. Bedanya dengan melempar pengecualian — ini berhenti
    /// TANPA menandai jalannya gagal, karena berhenti lebih awal bukan
    /// kegagalan.
    ///
    /// Kalau tidak ada host yang bisa menghentikan (mis. workflow dijalankan
    /// lewat WorkflowInvoker polos), permintaannya TIDAK diabaikan diam-diam:
    /// pengecualian dilemparkan supaya jalannya tetap berhenti. Robot yang
    /// diminta berhenti lalu terus berjalan adalah hasil paling buruk dari
    /// ketiga kemungkinan.
    /// </summary>
    [Designer(typeof(Design.StopRobotDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Stop Robot")]
    [Description("Menghentikan jalan yang sedang berlangsung, dari dalam workflow.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.stoprobot.png")]
    public sealed class StopRobot : CodeActivity
    {
        public StopRobot()
        {
            DisplayName = "Stop Robot";
        }

        [Category("Input")]
        [DisplayName("Reason")]
        [Description("Alasan berhenti; muncul di log dan di dasbor ForgeHub.")]
        public InArgument<string> Reason { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var alasan = Reason != null ? (Reason.Get(context) ?? "") : "";

            if (string.IsNullOrWhiteSpace(alasan))
                alasan = "Dihentikan oleh workflow.";

            RobotLog.Info(alasan, DisplayName);

            if (!RobotControl.RequestStop(alasan))
            {
                throw new OperationCanceledException(
                    "Stop Robot: " + alasan +
                    " (tidak ada host yang bisa menghentikan, jadi jalannya dibatalkan di sini.)");
            }
        }
    }
}

using System;
using System.Activities;
using System.ComponentModel;
using Custom.Shared;

namespace Custom.Orchestrator.Activities
{
    /// <summary>
    /// Membaca keterangan tentang jalan yang SEDANG berlangsung.
    ///
    /// Pengganti Get Workflow Instance milik OpenRPA. Namanya diganti karena
    /// yang dibaca bukan objek instans workflow-nya, melainkan keterangan
    /// pekerjaannya: nomor pekerjaan ForgeHub, nama proses, mesin, dan siapa
    /// yang menjalankannya.
    ///
    /// Gunanya menandai jejak. Berkas yang dihasilkan robot, baris yang
    /// ditulisnya ke basis data, atau surel yang dikirimnya bisa memuat nomor
    /// pekerjaan — sehingga ketika ada yang salah, satu jalan tertentu bisa
    /// ditemukan kembali di antara ratusan.
    ///
    /// Semua keluarannya boleh KOSONG. Workflow yang dijalankan dari Studio
    /// memang tidak punya nomor pekerjaan, dan itu bukan kesalahan — jadi
    /// activity ini tidak pernah melempar hanya karena dijalankan di luar
    /// ForgeHub.
    /// </summary>
    [Designer(typeof(GetJobInfoDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Get Job Info")]
    [Description("Membaca nomor pekerjaan, nama proses, dan mesin dari jalan yang sedang berlangsung.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.getjobinfo.png")]
    public sealed class GetJobInfo : CodeActivity
    {
        public GetJobInfo()
        {
            DisplayName = "Get Job Info";
        }

        [Category("Output")]
        [DisplayName("Job Id")]
        [Description("Nomor pekerjaan ForgeHub. Kosong kalau dijalankan dari Studio.")]
        public OutArgument<string> JobId { get; set; }

        [Category("Output")]
        [DisplayName("Process Name")]
        [Description("Nama proses yang sedang dijalankan.")]
        public OutArgument<string> ProcessName { get; set; }

        [Category("Output")]
        [DisplayName("Project Name")]
        [Description("Nama proyek tempat workflow ini berada.")]
        public OutArgument<string> ProjectName { get; set; }

        [Category("Output")]
        [DisplayName("Project Folder")]
        [Description("Folder proyeknya di mesin ini.")]
        public OutArgument<string> ProjectFolder { get; set; }

        [Category("Output")]
        [DisplayName("Machine Name")]
        [Description("Nama komputer tempat robot berjalan.")]
        public OutArgument<string> MachineName { get; set; }

        [Category("Output")]
        [DisplayName("User Name")]
        [Description("Akun Windows yang menjalankan robot.")]
        public OutArgument<string> UserName { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            // Tidak ada ContinueOnError di sini dengan sengaja: membaca nilai
            // yang sudah ada di memori tidak punya cara untuk gagal, dan
            // menyediakan tombolnya hanya akan menyiratkan sebaliknya.
            if (JobId != null) JobId.Set(context, RobotLog.JobId ?? "");
            if (ProcessName != null) ProcessName.Set(context, RobotLog.ProcessName ?? "");
            if (ProjectName != null) ProjectName.Set(context, Runtime.WorkflowContext.ProjectName ?? "");
            if (ProjectFolder != null) ProjectFolder.Set(context, Runtime.WorkflowContext.ProjectFolder ?? "");
            if (MachineName != null) MachineName.Set(context, Environment.MachineName);
            if (UserName != null) UserName.Set(context, Environment.UserName);
        }
    }
}

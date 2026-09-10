using System;
using System.Activities;
using System.ComponentModel;
using System.Diagnostics;

namespace CustomSystem
{
    /// <summary>
    /// Menutup sebuah program.
    ///
    /// Pengganti Close Application milik OpenRPA.
    ///
    /// Menutup dengan SOPAN lebih dulu — CloseMainWindow, yang memberi program
    /// kesempatan menyimpan pekerjaannya. Kill baru dipakai kalau diminta
    /// tegas, atau kalau permintaan sopan itu tidak digubris sampai batas
    /// waktu. Membunuh proses lebih dulu berarti kehilangan data tanpa alasan.
    /// </summary>
    [Designer(typeof(Design.CloseApplicationDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Close Application")]
    [Description("Menutup program berdasarkan nomor proses atau namanya.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.closeapp.png")]
    public sealed class CloseApplication : CodeActivity
    {
        public CloseApplication()
        {
            DisplayName = "Close Application";
        }

        [Category("Input")]
        [DisplayName("Process Id")]
        [Description("Nomor proses yang ditutup. Isi ini ATAU Process Name.")]
        public InArgument<int> ProcessId { get; set; }

        [Category("Input")]
        [DisplayName("Process Name")]
        [Description("Nama proses tanpa .exe, mis. notepad. Semua proses dengan nama ini ditutup.")]
        public InArgument<string> ProcessName { get; set; }

        [Category("Input")]
        [DisplayName("Force")]
        [Description("Kalau true, proses langsung dibunuh tanpa diminta menutup diri lebih dulu (default false).")]
        public InArgument<bool> Force { get; set; }

        [Category("Input")]
        [DisplayName("Timeout")]
        [Description("Lama menunggu program menutup diri sebelum dibunuh paksa; default 10 detik.")]
        public InArgument<TimeSpan> Timeout { get; set; }

        [Category("Output")]
        [DisplayName("Closed Count")]
        [Description("Berapa proses yang benar-benar ditutup.")]
        public OutArgument<int> ClosedCount { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [Description("Kalau true, kegagalan tidak menghentikan workflow (default false).")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = ContinueOnError != null && ContinueOnError.Get(context);
            var ditutup = 0;

            try
            {
                var id = ProcessId != null ? ProcessId.Get(context) : 0;
                var nama = ProcessName != null ? ProcessName.Get(context) : null;

                if (id <= 0 && string.IsNullOrWhiteSpace(nama))
                {
                    throw new InvalidOperationException(
                        "Close Application: Process Id dan Process Name dua-duanya kosong.");
                }

                var paksa = Force != null && Force.Get(context);

                var batas = Timeout != null ? Timeout.Get(context) : TimeSpan.Zero;
                if (batas <= TimeSpan.Zero) batas = TimeSpan.FromSeconds(10);

                Process[] daftar;

                if (id > 0)
                {
                    try
                    {
                        daftar = new[] { Process.GetProcessById(id) };
                    }
                    catch (ArgumentException)
                    {
                        // Sudah tidak ada: itu justru HASIL yang diinginkan.
                        daftar = new Process[0];
                    }
                }
                else
                {
                    daftar = Process.GetProcessesByName(nama.Replace(".exe", ""));
                }

                foreach (var p in daftar)
                {
                    if (Tutup(p, paksa, batas)) ditutup++;
                }
            }
            catch (Exception) when (continueOnError)
            {
            }
            finally
            {
                if (ClosedCount != null) ClosedCount.Set(context, ditutup);
            }
        }

        private static bool Tutup(Process p, bool paksa, TimeSpan batas)
        {
            try
            {
                if (p.HasExited) return false;

                if (!paksa && p.MainWindowHandle != IntPtr.Zero)
                {
                    p.CloseMainWindow();
                    if (p.WaitForExit((int)batas.TotalMilliseconds)) return true;
                }

                if (!p.HasExited)
                {
                    p.Kill();
                    p.WaitForExit(5000);
                }

                return true;
            }
            catch (InvalidOperationException)
            {
                // Prosesnya keburu berhenti sendiri di tengah jalan.
                return false;
            }
        }
    }
}

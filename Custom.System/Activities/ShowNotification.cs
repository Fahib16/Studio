using System;
using System.Activities;
using System.ComponentModel;
using System.Threading;

namespace CustomSystem
{
    /// <summary>
    /// Menampilkan pemberitahuan kecil di pojok layar.
    ///
    /// Pengganti Show Balloon Tip milik OpenRPA.
    ///
    /// Ikon baki dibuat dan DIBUANG lagi di activity ini juga. Meninggalkannya
    /// hidup akan menumpuk ikon di baki sistem setiap kali workflow berjalan,
    /// dan ikon yatim itu baru hilang saat prosesnya berakhir.
    ///
    /// Pemberitahuannya TIDAK memblokir: robot yang berhenti menunggu orang
    /// menutup balon bukan robot yang tak terjaga. Kalau memang perlu ditunggu,
    /// pakai Delay sesudahnya.
    /// </summary>
    [Designer(typeof(Design.ShowNotificationDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Show Notification")]
    [Description("Menampilkan pemberitahuan kecil di pojok layar, tanpa menunggu.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.balloon.png")]
    public sealed class ShowNotification : CodeActivity
    {
        public ShowNotification()
        {
            DisplayName = "Show Notification";
        }

        [Category("Input")]
        [DisplayName("Title")]
        [Description("Judul pemberitahuan.")]
        public InArgument<string> Title { get; set; }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Message")]
        [Description("Isi pemberitahuan.")]
        public InArgument<string> Message { get; set; }

        [Category("Input")]
        [DisplayName("Level")]
        [Description("Info, Warning, atau Error. Menentukan ikon yang tampil.")]
        public InArgument<string> Level { get; set; }

        [Category("Input")]
        [DisplayName("Duration")]
        [Description("Lama pemberitahuan tampil; default 5 detik. Windows boleh " +
                     "memendekkan atau memanjangkannya sesuai setelan sistem.")]
        public InArgument<TimeSpan> Duration { get; set; }

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
                var judul = Title != null ? (Title.Get(context) ?? "") : "";
                var pesan = Message.Get(context) ?? "";
                var tingkat = Level != null ? (Level.Get(context) ?? "") : "";

                var lama = Duration != null ? Duration.Get(context) : TimeSpan.Zero;
                if (lama <= TimeSpan.Zero) lama = TimeSpan.FromSeconds(5);

                var ikon = System.Windows.Forms.ToolTipIcon.Info;
                if (tingkat.Equals("Warning", StringComparison.OrdinalIgnoreCase))
                    ikon = System.Windows.Forms.ToolTipIcon.Warning;
                else if (tingkat.Equals("Error", StringComparison.OrdinalIgnoreCase))
                    ikon = System.Windows.Forms.ToolTipIcon.Error;

                Tampilkan(string.IsNullOrEmpty(judul) ? "JakForge" : judul, pesan, ikon, lama);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }

        private static void Tampilkan(string judul, string pesan,
                                      System.Windows.Forms.ToolTipIcon ikon, TimeSpan lama)
        {
            // Thread sendiri, dan ikonnya dibuang di thread yang sama.
            //
            // NotifyIcon menuntut pompa pesan; tanpa itu balonnya tidak pernah
            // digambar. Threadnya latar, jadi ia tidak menahan proses kalau
            // workflow selesai lebih dulu.
            var t = new Thread(() =>
            {
                using (var ikonBaki = new System.Windows.Forms.NotifyIcon())
                {
                    ikonBaki.Icon = System.Drawing.SystemIcons.Application;
                    ikonBaki.Visible = true;
                    ikonBaki.ShowBalloonTip((int)lama.TotalMilliseconds, judul, pesan, ikon);

                    var batas = DateTime.UtcNow + lama;
                    while (DateTime.UtcNow < batas)
                    {
                        System.Windows.Forms.Application.DoEvents();
                        Thread.Sleep(50);
                    }

                    ikonBaki.Visible = false;
                }
            });

            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
        }
    }
}

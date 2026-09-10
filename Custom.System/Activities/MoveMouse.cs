using System;
using System.Activities;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace CustomSystem
{
    /// <summary>
    /// Menggerakkan kursor mouse ke koordinat layar.
    ///
    /// Pengganti Move Mouse milik OpenRPA.
    ///
    /// Gerakannya bisa BERTAHAP, bukan lompat seketika. Sebagian antarmuka
    /// hanya memunculkan menu atau tooltip kalau kursornya benar-benar
    /// melintas — lompatan satu langkah tidak menghasilkan event mousemove di
    /// jalurnya, dan menu yang ditunggu tidak pernah muncul.
    /// </summary>
    [Designer(typeof(Design.MoveMouseDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Move Mouse")]
    [Description("Menggerakkan kursor mouse ke koordinat layar tertentu.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.movemouse.png")]
    public sealed class MoveMouse : CodeActivity
    {
        public MoveMouse()
        {
            DisplayName = "Move Mouse";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("X")]
        [Description("Koordinat X di layar, dihitung dari tepi kiri.")]
        public InArgument<int> X { get; set; }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Y")]
        [Description("Koordinat Y di layar, dihitung dari tepi atas.")]
        public InArgument<int> Y { get; set; }

        [Category("Input")]
        [DisplayName("Smooth")]
        [Description("Kalau true, kursor digerakkan bertahap supaya halaman yang " +
                     "menunggu mousemove ikut bereaksi (default false).")]
        public InArgument<bool> Smooth { get; set; }

        [Category("Input")]
        [DisplayName("Duration")]
        [Description("Lama gerakan bertahap. Hanya dipakai kalau Smooth true; " +
                     "default 300 milidetik.")]
        public InArgument<TimeSpan> Duration { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [Description("Kalau true, kegagalan tidak menghentikan workflow (default false).")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT point);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = ContinueOnError != null && ContinueOnError.Get(context);

            try
            {
                var tujuanX = X.Get(context);
                var tujuanY = Y.Get(context);
                var halus = Smooth != null && Smooth.Get(context);

                if (!halus)
                {
                    if (!SetCursorPos(tujuanX, tujuanY))
                        throw new InvalidOperationException("Kursor tidak bisa dipindahkan ke " + tujuanX + "," + tujuanY + ".");
                    return;
                }

                var lama = Duration != null ? Duration.Get(context) : TimeSpan.Zero;
                if (lama <= TimeSpan.Zero) lama = TimeSpan.FromMilliseconds(300);

                POINT awal;
                if (!GetCursorPos(out awal))
                {
                    SetCursorPos(tujuanX, tujuanY);
                    return;
                }

                // Satu langkah per ~10 ms: cukup rapat untuk memicu mousemove,
                // cukup jarang untuk tidak membanjiri antrean pesan.
                var langkah = Math.Max(1, (int)(lama.TotalMilliseconds / 10));

                for (var i = 1; i <= langkah; i++)
                {
                    var bagian = (double)i / langkah;
                    SetCursorPos(
                        (int)Math.Round(awal.X + (tujuanX - awal.X) * bagian),
                        (int)Math.Round(awal.Y + (tujuanY - awal.Y) * bagian));
                    Thread.Sleep(10);
                }

                SetCursorPos(tujuanX, tujuanY);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

using System;
using System.Activities;
using System.ComponentModel;

namespace Custom.Window
{
    /// <summary>
    /// Mengatur posisi dan/atau ukuran window.
    ///
    /// Satu activity untuk keduanya, bukan dua activity terpisah, karena
    /// Windows hanya punya SATU panggilan yang mengubah keduanya sekaligus
    /// (MoveWindow) — memisahkannya berarti activity "Set Position" tetap
    /// harus membaca ukuran lama lalu menuliskannya kembali, dan itu justru
    /// menambah kemungkinan salah.
    ///
    /// Nilai yang DIKOSONGKAN tidak diubah: isi X dan Y saja untuk memindahkan
    /// tanpa mengubah ukuran, atau Width dan Height saja untuk mengubah ukuran
    /// di tempat. Karena 0 adalah koordinat yang sah, "kosong" diwakili -1.
    ///
    /// Window yang sedang maximize dikembalikan dulu (restore) sebelum
    /// dipindahkan — MoveWindow pada window maximize tidak berpengaruh apa-apa
    /// dan itu terlihat seperti activity yang gagal diam-diam.
    /// </summary>
    [Designer(typeof(Design.SetWindowBoundsDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Set Window Bounds")]
    [Description("Mengatur posisi dan/atau ukuran window (padanan Set Window Position/Size).")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.setwindowbounds.png")]
    public sealed class SetWindowBounds : WindowActivityBase
    {
        public SetWindowBounds()
        {
            DisplayName = "Set Window Bounds";

            X = Unset();
            Y = Unset();
            Width = Unset();
            Height = Unset();
        }

        private static InArgument<int> Unset()
        {
            return new InArgument<int>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<int>("-1"));
        }

        [Category("Input")]
        [DisplayName("X")]
        [Description("Posisi kiri, dalam piksel. -1 berarti tidak diubah.")]
        public InArgument<int> X { get; set; }

        [Category("Input")]
        [DisplayName("Y")]
        [Description("Posisi atas, dalam piksel. -1 berarti tidak diubah.")]
        public InArgument<int> Y { get; set; }

        [Category("Input")]
        [DisplayName("Width")]
        [Description("Lebar, dalam piksel. -1 berarti tidak diubah.")]
        public InArgument<int> Width { get; set; }

        [Category("Input")]
        [DisplayName("Height")]
        [Description("Tinggi, dalam piksel. -1 berarti tidak diubah.")]
        public InArgument<int> Height { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = GetContinueOnError(context);

            try
            {
                var hWnd = FindWindow(context);

                WindowFinder.RECT rect;
                if (!WindowFinder.Bounds(hWnd, out rect))
                    throw new InvalidOperationException("Tidak bisa membaca posisi window.");

                var x = Value(X, context, rect.Left);
                var y = Value(Y, context, rect.Top);
                var width = Value(Width, context, rect.Right - rect.Left);
                var height = Value(Height, context, rect.Bottom - rect.Top);

                // Window maximize tidak bisa dipindahkan; kembalikan dulu.
                WindowFinder.Show(hWnd, WindowFinder.SW_RESTORE);

                if (!WindowFinder.Move(hWnd, x, y, width, height))
                    throw new InvalidOperationException("Gagal memindahkan window.");
            }
            catch (Exception) when (continueOnError)
            {
            }
        }

        private static int Value(InArgument<int> argument, CodeActivityContext context, int fallback)
        {
            if (argument == null) return fallback;
            var value = argument.Get(context);
            return value < 0 ? fallback : value;
        }
    }
}

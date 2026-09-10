using System;
using System.Activities;
using System.ComponentModel;
using Open3270;

namespace Custom.Terminal
{
    /// <summary>
    /// Activity kustom: Type Into Terminal ala UiPath untuk OpenRPA.
    /// Mengetik teks di posisi kursor saat ini pada layar terminal.
    /// Untuk pindah posisi kursor dulu, kirim Tab/arrow key lewat
    /// SendTerminalKey sebelum activity ini, atau isi Row/Column kalau mau
    /// activity ini yang urus posisinya (opsional, lihat properti di bawah).
    /// </summary>
    [Designer(typeof(Design.TypeIntoTerminalDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.typeintoterminal.png")]
    [DisplayName("Type Into Terminal")]
    [Description("Mengetik teks di layar terminal.")]
    public class TypeIntoTerminal : CodeActivity
    {
        public TypeIntoTerminal()
        {
            DisplayName = "Type Into Terminal";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Session")]
        public InArgument<TNEmulator> Session { get; set; }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Text")]
        public InArgument<string> Text { get; set; }

        [Category("Target")]
        [DisplayName("Row")]
        [Description("Opsional, berbasis 1 (baris teratas = 1). Kalau diisi bersama Column, kursor " +
                     "dipindahkan ke sana dulu sebelum mengetik. Kosongkan (0) untuk mengetik di " +
                     "posisi kursor saat ini.")]
        public InArgument<int> Row { get; set; }

        [Category("Target")]
        [DisplayName("Column")]
        [Description("Opsional, berbasis 1 (kolom paling kiri = 1).")]
        public InArgument<int> Column { get; set; }

        [Category("Common")]
        [DisplayName("Post Wait")]
        public InArgument<TimeSpan> PostWait { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var session = Session.Get(context);
            if (session == null) throw new ArgumentException("Session tidak boleh kosong");

            var text = Text.Get(context) ?? string.Empty;

            // SEKARANG PRESISI. Catatan lama ("Open3270 tidak punya SetCursor
            // yang terkonfirmasi, jadi cuma kirim Home") sudah tidak berlaku:
            // TNEmulator.SetCursor(int x, int y) memang ada, dan urutan
            // parameternya sudah dipastikan dari source Open3270 di repo ini —
            // Controller.MoveCursor menghitung alamat sebagai
            // ((y * columnCount) + x), jadi x = KOLOM dan y = BARIS, keduanya
            // berbasis 0. Row/Column di activity ini berbasis 1 (0 = tidak
            // diisi); konversinya di TerminalCoordinates.
            var row = Row != null ? Row.Get(context) : 0;
            var col = Column != null ? Column.Get(context) : 0;

            if (TerminalCoordinates.HasPosition(row, col))
            {
                session.SetCursor(TerminalCoordinates.ToX(col), TerminalCoordinates.ToY(row));
            }

            // Signature dikonfirmasi dari TheDemo.cs: SendText(string)
            session.SendText(text);

            var postwait = PostWait != null ? PostWait.Get(context) : TimeSpan.Zero;
            if (postwait != TimeSpan.Zero) System.Threading.Thread.Sleep(postwait);
        }
    }
}

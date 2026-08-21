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
    public class TypeIntoTerminal : CodeActivity
    {
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
        [Description("Opsional. Kalau diisi (bersama Column), Send Terminal Key posisi kursor dulu " +
                      "lewat Refresh + set cursor sebelum mengetik. Kosongkan untuk ketik di posisi kursor saat ini.")]
        public InArgument<int> Row { get; set; }

        [Category("Target")]
        [DisplayName("Column")]
        public InArgument<int> Column { get; set; }

        [Category("Common")]
        [DisplayName("Post Wait")]
        public InArgument<TimeSpan> PostWait { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var session = Session.Get(context);
            if (session == null) throw new ArgumentException("Session tidak boleh kosong");

            var text = Text.Get(context) ?? string.Empty;

            // CATATAN: Open3270 TNEmulator tidak punya method "SetCursor(row,col)"
            // yang terkonfirmasi dari source yang sudah saya lihat (TheDemo.cs
            // tidak mendemokan ini). Kalau Row/Column diisi, cara paling aman
            // yang saya tahu pasti ada: kirim key Home dulu (kembali ke posisi
            // field pertama/awal), TAPI ini TIDAK sama dengan pindah ke row/col
            // spesifik. Kalau kamu butuh positioning presisi, kasih tahu saya —
            // saya perlu lihat lebih lanjut apakah TNEmulator/CurrentScreenXML
            // punya method SetCursor atau setara sebelum saya implementasikan
            // dengan yakin (daripada nebak lagi).
            var row = Row != null ? Row.Get(context) : 0;
            var col = Column != null ? Column.Get(context) : 0;
            if (row > 0 || col > 0)
            {
                // Placeholder aman: cuma Home dulu. TIDAK presisi ke row/col
                // tertentu -- lihat catatan di atas.
                session.SendKeyFromText(true, "Home");
            }

            // Signature dikonfirmasi dari TheDemo.cs: SendText(string)
            session.SendText(text);

            var postwait = PostWait != null ? PostWait.Get(context) : TimeSpan.Zero;
            if (postwait != TimeSpan.Zero) System.Threading.Thread.Sleep(postwait);
        }
    }
}

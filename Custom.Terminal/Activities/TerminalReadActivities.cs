using System;
using System.Activities;
using System.ComponentModel;
using Open3270;

namespace Custom.Terminal
{
    /// <summary>
    /// Activity kustom: Wait For Terminal Text ala UiPath untuk OpenRPA.
    /// Signature dikonfirmasi dari TheDemo.cs: WaitForText(row, col, text, timeoutMs) -> bool
    /// </summary>
    [Designer(typeof(Design.WaitForTerminalTextDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.waitterminaltext.png")]
    [DisplayName("Wait For Terminal Text")]
    [Description("Menunggu teks tertentu muncul di layar terminal.")]
    public class WaitForTerminalText : CodeActivity
    {
        public WaitForTerminalText()
        {
            DisplayName = "Wait For Terminal Text";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Session")]
        public InArgument<TNEmulator> Session { get; set; }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Text")]
        [Description("Teks yang ditunggu muncul di posisi Row/Column")]
        public InArgument<string> Text { get; set; }

        [Category("Target")]
        [DisplayName("Row")]
        public InArgument<int> Row { get; set; } = 0;

        [Category("Target")]
        [DisplayName("Column")]
        public InArgument<int> Column { get; set; } = 0;

        [Category("Target")]
        [DisplayName("Timeout")]
        [Description("Default 20 detik kalau kosong")]
        public InArgument<TimeSpan> Timeout { get; set; }

        [Category("Common")]
        [DisplayName("Throw On Timeout")]
        [Description("True (default): lempar exception kalau timeout. False: cuma isi Found = false.")]
        [DefaultValue(true)]
        public bool ThrowOnTimeout { get; set; } = true;

        [Category("Output")]
        [DisplayName("Found")]
        public OutArgument<bool> Found { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var session = Session.Get(context);
            if (session == null) throw new ArgumentException("Session tidak boleh kosong");

            var text = Text.Get(context) ?? string.Empty;
            var row = Row != null ? Row.Get(context) : 0;
            var col = Column != null ? Column.Get(context) : 0;
            var timeout = Timeout != null ? Timeout.Get(context) : TimeSpan.Zero;
            if (timeout == TimeSpan.Zero) timeout = TimeSpan.FromSeconds(20);

            // PERBAIKAN: sebelumnya dipanggil WaitForText(row, col, ...) —
            // TERBALIK. Signature Open3270 adalah WaitForText(int x, int y, ...)
            // dengan x = KOLOM dan y = BARIS, keduanya berbasis 0 (lihat
            // TerminalCoordinates untuk buktinya dari source Open3270).
            // Akibat urutan lama, penungguan di baris 5 kolom 20 sebenarnya
            // memeriksa baris 20 kolom 5.
            bool found = session.WaitForText(
                TerminalCoordinates.ToX(col), TerminalCoordinates.ToY(row),
                text, (int)timeout.TotalMilliseconds);

            if (Found != null) Found.Set(context, found);

            if (!found && ThrowOnTimeout)
            {
                throw new TimeoutException(
                    $"Wait For Terminal Text: \"{text}\" tidak muncul di (row {row}, col {col}) dalam {timeout.TotalSeconds} detik.");
            }
        }
    }

    /// <summary>
    /// Activity kustom: Get Terminal Text ala UiPath untuk OpenRPA.
    ///
    /// SEKARANG BISA PER-REGION. Catatan lama ("belum ada ekstraksi per-region
    /// row/col/length yang terkonfirmasi") sudah tidak berlaku:
    /// IXMLScreen.GetText(x, y, length) dan GetRow(y) memang ada, dan urutan
    /// parameternya sudah dipastikan dari source Open3270 di repo ini (lihat
    /// TerminalCoordinates).
    ///
    /// Aturan pemakaian:
    ///   Row = 0            -> seluruh layar (perilaku lama, tetap default)
    ///   Row diisi, Length 0 -> seluruh baris itu
    ///   Row + Column + Length -> potongan sepanjang Length dari posisi itu
    /// </summary>
    [Designer(typeof(Design.GetTerminalTextDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.getterminaltext.png")]
    [DisplayName("Get Terminal Text")]
    [Description("Membaca teks dari layar terminal: seluruh layar, satu baris, atau potongan.")]
    public class GetTerminalText : CodeActivity
    {
        public GetTerminalText()
        {
            DisplayName = "Get Terminal Text";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Session")]
        public InArgument<TNEmulator> Session { get; set; }

        [Category("Target")]
        [DisplayName("Row")]
        [Description("Opsional, berbasis 1. Kosong (0) berarti seluruh layar.")]
        public InArgument<int> Row { get; set; }

        [Category("Target")]
        [DisplayName("Column")]
        [Description("Opsional, berbasis 1. Hanya dipakai bersama Length.")]
        public InArgument<int> Column { get; set; }

        [Category("Target")]
        [DisplayName("Length")]
        [Description("Jumlah karakter yang diambil. 0 berarti sampai akhir baris.")]
        public InArgument<int> Length { get; set; }

        [Category("Options")]
        [DisplayName("Trim")]
        [Description("True (default): buang spasi di ujung hasil. Layar 3270 selalu " +
                     "dipenuhi spasi sampai batas kolom, dan spasi itu hampir tidak pernah diinginkan.")]
        [DefaultValue(true)]
        public bool Trim { get; set; } = true;

        [Category("Output")]
        [DisplayName("Text")]
        [Description("Isi layar, baris, atau potongan yang diminta.")]
        public OutArgument<string> Text { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var session = Session.Get(context);
            if (session == null) throw new ArgumentException("Session tidak boleh kosong");

            var row = Row != null ? Row.Get(context) : 0;
            var column = Column != null ? Column.Get(context) : 0;
            var length = Length != null ? Length.Get(context) : 0;

            string result;

            if (row <= 0)
            {
                // Seluruh layar: Dump() dipertahankan supaya hasilnya sama
                // persis dengan versi sebelumnya bagi workflow yang sudah ada.
                result = session.CurrentScreenXML?.Dump() ?? string.Empty;
                if (Text != null) Text.Set(context, result);
                return;
            }

            var screen = TerminalCoordinates.Screen(session);

            if (length > 0)
            {
                result = screen.GetText(TerminalCoordinates.ToX(column),
                                        TerminalCoordinates.ToY(row), length) ?? string.Empty;
            }
            else
            {
                result = screen.GetRow(TerminalCoordinates.ToY(row)) ?? string.Empty;

                // Kolom diisi tanpa Length berarti "dari kolom itu sampai
                // akhir baris".
                var start = TerminalCoordinates.ToX(column);
                if (start > 0 && start < result.Length) result = result.Substring(start);
            }

            if (Trim) result = result.TrimEnd();

            if (Text != null) Text.Set(context, result);
        }
    }
}

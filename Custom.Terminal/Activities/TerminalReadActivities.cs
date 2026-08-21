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
    public class WaitForTerminalText : CodeActivity
    {
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

            bool found = session.WaitForText(row, col, text, (int)timeout.TotalMilliseconds);

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
    /// Signature dikonfirmasi dari TheDemo.cs: CurrentScreenXML.Dump() -> string
    /// (dump seluruh layar). Belum ada ekstraksi per-region row/col/length yang
    /// terkonfirmasi -- kalau kamu butuh itu, saya perlu lihat lebih lanjut
    /// struktur TnXMLScreen/Field milik Open3270 sebelum implementasi presisi.
    /// </summary>
    [Designer(typeof(Design.GetTerminalTextDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    public class GetTerminalText : CodeActivity
    {
        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Session")]
        public InArgument<TNEmulator> Session { get; set; }

        [Category("Output")]
        [DisplayName("Text")]
        [Description("Seluruh isi layar terminal saat ini (dump text)")]
        public OutArgument<string> Text { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var session = Session.Get(context);
            if (session == null) throw new ArgumentException("Session tidak boleh kosong");

            var screenText = session.CurrentScreenXML?.Dump() ?? string.Empty;

            if (Text != null) Text.Set(context, screenText);
        }
    }
}

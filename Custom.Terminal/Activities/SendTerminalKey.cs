using System;
using System.Activities;
using System.ComponentModel;
using Open3270;

namespace Custom.Terminal
{
    /// <summary>
    /// Daftar key PERSIS diambil dari komentar TheDemo.cs (Open3270 official sample):
    /// "key is one of: Attn, Backspace, BackTab, CircumNot, Clear, CursorSelect,
    /// Delete, DeleteField, DeleteWord, Down, Dup, Enter, Erase, EraseEOF,
    /// EraseInput, FieldEnd, FieldMark, FieldExit, Home, Insert, Interrupt, Key,
    /// Left, Left2, Newline, NextWord, PAnn, PFnn, PreviousWord, Reset, Right,
    /// Right2, SysReq, Tab, Toggle, ToggleInsert, ToggleReverse, Up"
    /// PFnn/PAnn diwakili terpisah lewat property PfNumber/PaNumber di bawah,
    /// bukan sebagai enum member (karena nn = angka 1-24 utk PF, 1-3 utk PA).
    /// </summary>
    public enum TerminalKey
    {
        Attn, Backspace, BackTab, CircumNot, Clear, CursorSelect,
        Delete, DeleteField, DeleteWord, Down, Dup, Enter, Erase, EraseEOF,
        EraseInput, FieldEnd, FieldMark, FieldExit, Home, Insert, Interrupt,
        Left, Left2, Newline, NextWord, PreviousWord, Reset, Right,
        Right2, SysReq, Tab, Toggle, ToggleInsert, ToggleReverse, Up,
        PF, // pakai bareng PfNumber (1-24)
        PA  // pakai bareng PaNumber (1-3)
    }

    /// <summary>
    /// Activity kustom: Send Terminal Key ala UiPath untuk OpenRPA.
    /// Mengirim AID key (bukan mengetik teks biasa — pakai Type Into Terminal
    /// untuk itu) ke Terminal Session yang sedang aktif.
    /// </summary>
    [Designer(typeof(Design.TerminalKeyDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    public class SendTerminalKey : CodeActivity
    {
        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Session")]
        [Description("Session dari Terminal Session (biasanya expression \"session\" kalau ditaruh di dalam Body-nya)")]
        public InArgument<TNEmulator> Session { get; set; }

        [Category("Input")]
        [DisplayName("Key")]
        [Description("AID key yang dikirim. Pilih PF/PA untuk isi nomor PF1-24 / PA1-3 di bawah.")]
        public TerminalKey Key { get; set; } = TerminalKey.Enter;

        [Category("Input")]
        [DisplayName("PF Number")]
        [Description("1-24, dipakai hanya kalau Key = PF")]
        public InArgument<int> PfNumber { get; set; } = 1;

        [Category("Input")]
        [DisplayName("PA Number")]
        [Description("1-3, dipakai hanya kalau Key = PA")]
        public InArgument<int> PaNumber { get; set; } = 1;

        [Category("Common")]
        [DisplayName("Post Wait")]
        public InArgument<TimeSpan> PostWait { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var session = Session.Get(context);
            if (session == null) throw new ArgumentException("Session tidak boleh kosong");

            string keyName;
            if (Key == TerminalKey.PF)
            {
                var n = PfNumber != null ? PfNumber.Get(context) : 1;
                keyName = "PF" + n;
            }
            else if (Key == TerminalKey.PA)
            {
                var n = PaNumber != null ? PaNumber.Get(context) : 1;
                keyName = "PA" + n;
            }
            else
            {
                keyName = Key.ToString();
            }

            // Signature dikonfirmasi dari TheDemo.cs: SendKeyFromText(bool, string)
            session.SendKeyFromText(true, keyName);

            var postwait = PostWait != null ? PostWait.Get(context) : TimeSpan.Zero;
            if (postwait != TimeSpan.Zero) System.Threading.Thread.Sleep(postwait);
        }
    }
}

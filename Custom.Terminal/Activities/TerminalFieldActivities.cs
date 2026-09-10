using System;
using System.Activities;
using System.ComponentModel;
using Open3270;

namespace Custom.Terminal
{
    /// <summary>
    /// Membaca isi satu FIELD di layar terminal.
    ///
    /// Field adalah bagian layar yang dikelola host — label dan kotak isian.
    /// Membaca lewat field lebih tahan banting daripada lewat koordinat mentah:
    /// kalau tata letak layar bergeser satu baris, koordinat langsung salah,
    /// sedangkan urutan field biasanya tetap.
    ///
    /// Tiga cara menunjuk field, dipakai sesuai yang diisi (urutan prioritas):
    ///   1. Index      — nomor urut field di layar, mulai 0
    ///   2. Label      — cari field yang teksnya memuat label ini, ambil field
    ///                   BERIKUTNYA (pola "Nama:" lalu kotak isiannya)
    ///   3. Row/Column — field yang memuat posisi itu (berbasis 1)
    /// </summary>
    [Designer(typeof(Design.GetTerminalFieldDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Get Terminal Field")]
    [Description("Membaca isi satu field di layar terminal.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.getterminalfield.png")]
    public class GetTerminalField : CodeActivity
    {
        public GetTerminalField()
        {
            DisplayName = "Get Terminal Field";
            Index = new InArgument<int>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<int>("-1"));
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Session")]
        public InArgument<TNEmulator> Session { get; set; }

        [Category("Target")]
        [DisplayName("Index")]
        [Description("Nomor urut field mulai 0. -1 (default) berarti tidak dipakai.")]
        public InArgument<int> Index { get; set; }

        [Category("Target")]
        [DisplayName("Label")]
        [Description("Cari field yang teksnya memuat ini, lalu ambil field berikutnya.")]
        public InArgument<string> Label { get; set; }

        [Category("Target")]
        [DisplayName("Row")]
        [Description("Berbasis 1. Dipakai bersama Column kalau Index dan Label kosong.")]
        public InArgument<int> Row { get; set; }

        [Category("Target")]
        [DisplayName("Column")]
        [Description("Berbasis 1.")]
        public InArgument<int> Column { get; set; }

        [Category("Options")]
        [DisplayName("Trim")]
        [Description("True (default): buang spasi di ujung hasil.")]
        [DefaultValue(true)]
        public bool Trim { get; set; } = true;

        [Category("Output")]
        [DisplayName("Text")]
        public OutArgument<string> Text { get; set; }

        [Category("Output")]
        [DisplayName("Field Index")]
        [Description("Nomor urut field yang benar-benar terbaca — berguna untuk dipakai ulang " +
                     "oleh Set Terminal Field tanpa mencari lagi.")]
        public OutArgument<int> FieldIndex { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [Description("Kalau true, kegagalan activity ini tidak menghentikan workflow (default false).")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = ContinueOnError != null && ContinueOnError.Get(context);

            try
            {
                var session = Session.Get(context);
                var fields = TerminalCoordinates.Fields(session);

                var index = TerminalFieldLocator.Locate(
                    fields,
                    Index != null ? Index.Get(context) : -1,
                    Label != null ? Label.Get(context) : null,
                    Row != null ? Row.Get(context) : 0,
                    Column != null ? Column.Get(context) : 0);

                var text = fields[index].Text ?? "";
                if (Trim) text = text.TrimEnd();

                if (Text != null) Text.Set(context, text);
                if (FieldIndex != null) FieldIndex.Set(context, index);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }

    /// <summary>
    /// Mengisi satu FIELD di layar terminal.
    ///
    /// Memakai TNEmulator.SetField(index, text) — cara host sendiri mengisi
    /// field, sehingga tidak bergantung pada posisi kursor saat itu. Ini yang
    /// membedakannya dari Type Into Terminal: mengetik memerlukan kursor sudah
    /// berada di tempat yang benar, sedangkan mengisi field tidak.
    ///
    /// Cara menunjuk field-nya sama persis dengan Get Terminal Field.
    /// </summary>
    [Designer(typeof(Design.SetTerminalFieldDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Set Terminal Field")]
    [Description("Mengisi satu field di layar terminal.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.setterminalfield.png")]
    public class SetTerminalField : CodeActivity
    {
        public SetTerminalField()
        {
            DisplayName = "Set Terminal Field";
            Index = new InArgument<int>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<int>("-1"));
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Session")]
        public InArgument<TNEmulator> Session { get; set; }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Text")]
        [Description("Isi yang dimasukkan ke field.")]
        public InArgument<string> Text { get; set; }

        [Category("Target")]
        [DisplayName("Index")]
        [Description("Nomor urut field mulai 0. -1 (default) berarti tidak dipakai.")]
        public InArgument<int> Index { get; set; }

        [Category("Target")]
        [DisplayName("Label")]
        [Description("Cari field yang teksnya memuat ini, lalu isi field berikutnya.")]
        public InArgument<string> Label { get; set; }

        [Category("Target")]
        [DisplayName("Row")]
        [Description("Berbasis 1. Dipakai bersama Column kalau Index dan Label kosong.")]
        public InArgument<int> Row { get; set; }

        [Category("Target")]
        [DisplayName("Column")]
        [Description("Berbasis 1.")]
        public InArgument<int> Column { get; set; }

        [Category("Common")]
        [DisplayName("Post Wait")]
        [Description("Jeda setelah mengisi, untuk memberi host waktu memperbarui layar.")]
        public InArgument<TimeSpan> PostWait { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [Description("Kalau true, kegagalan activity ini tidak menghentikan workflow (default false).")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = ContinueOnError != null && ContinueOnError.Get(context);

            try
            {
                var session = Session.Get(context);
                var fields = TerminalCoordinates.Fields(session);

                var index = TerminalFieldLocator.Locate(
                    fields,
                    Index != null ? Index.Get(context) : -1,
                    Label != null ? Label.Get(context) : null,
                    Row != null ? Row.Get(context) : 0,
                    Column != null ? Column.Get(context) : 0);

                session.SetField(index, Text.Get(context) ?? "");

                var postWait = PostWait != null ? PostWait.Get(context) : TimeSpan.Zero;
                if (postWait > TimeSpan.Zero) System.Threading.Thread.Sleep(postWait);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }

    /// <summary>
    /// Penentu field yang dipakai bersama Get dan Set Terminal Field, supaya
    /// keduanya tidak mungkin punya aturan prioritas yang berbeda.
    /// </summary>
    internal static class TerminalFieldLocator
    {
        public static int Locate(Open3270.TN3270.XMLScreenField[] fields,
                                 int index, string label, int row, int column)
        {
            if (fields.Length == 0)
                throw new InvalidOperationException("Layar terminal tidak punya field sama sekali.");

            if (index >= 0)
            {
                if (index >= fields.Length)
                    throw new ArgumentException("Index " + index + " di luar jumlah field yang ada (" +
                                                fields.Length + ").");
                return index;
            }

            if (!string.IsNullOrWhiteSpace(label))
            {
                var byLabel = TerminalCoordinates.FieldIndexAfterLabel(fields, label);
                if (byLabel < 0)
                    throw new ArgumentException(
                        "Tidak ada field setelah label \"" + label + "\" di layar ini.");
                return byLabel;
            }

            if (TerminalCoordinates.HasPosition(row, column))
            {
                var byPosition = TerminalCoordinates.FieldIndexAt(fields, row, column);
                if (byPosition < 0)
                    throw new ArgumentException(
                        "Tidak ada field di baris " + row + " kolom " + column + ".");
                return byPosition;
            }

            throw new ArgumentException("Isi salah satu dari Index, Label, atau Row+Column.");
        }
    }
}

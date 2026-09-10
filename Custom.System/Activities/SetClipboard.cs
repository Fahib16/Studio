using System;
using System.Activities;
using System.ComponentModel;
using System.Threading;

namespace CustomSystem
{
    /// <summary>
    /// Menaruh teks ke papan klip Windows.
    ///
    /// Pengganti Copy Clipboard milik OpenRPA.
    ///
    /// Papan klip HANYA bisa disentuh dari thread STA, sementara workflow
    /// berjalan di thread pool yang MTA. Karena itu operasinya dijalankan di
    /// thread STA tersendiri — bukan lewat Dispatcher aplikasi, supaya activity
    /// ini tetap bekerja di JakRunner yang jendelanya bisa saja sedang tidak
    /// ada.
    ///
    /// Papan klip juga bisa sedang dikunci proses lain (Office, peramban,
    /// perkakas cuplikan layar). Kegagalan seperti itu sesaat, jadi dicoba
    /// beberapa kali sebelum menyerah.
    /// </summary>
    [Designer(typeof(Design.SetClipboardDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Copy To Clipboard")]
    [Description("Menaruh teks ke papan klip Windows.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.clipboard.png")]
    public sealed class SetClipboard : CodeActivity
    {
        public SetClipboard()
        {
            DisplayName = "Copy To Clipboard";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Text")]
        [Description("Teks yang ditaruh ke papan klip.")]
        public InArgument<string> Text { get; set; }

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
                var text = Text.Get(context) ?? "";
                ClipboardHelper.SetText(text);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }

    /// <summary>
    /// Akses papan klip yang aman dipanggil dari thread workflow.
    ///
    /// Dipakai bersama oleh Copy To Clipboard dan Get Clipboard Text, supaya
    /// aturan STA dan percobaan ulangnya hanya ditulis satu kali.
    /// </summary>
    internal static class ClipboardHelper
    {
        private const int PercobaanMaks = 5;
        private const int JedaMs = 80;

        public static void SetText(string text)
        {
            Jalankan(() =>
            {
                // Teks kosong TIDAK boleh lewat SetText: Windows melemparkan
                // ArgumentNullException untuknya. Yang setara dan benar adalah
                // mengosongkan papan klipnya.
                if (string.IsNullOrEmpty(text)) System.Windows.Clipboard.Clear();
                else System.Windows.Clipboard.SetText(text);
            });
        }

        public static string GetText()
        {
            string hasil = "";
            Jalankan(() =>
            {
                hasil = System.Windows.Clipboard.ContainsText()
                    ? System.Windows.Clipboard.GetText()
                    : "";
            });
            return hasil;
        }

        private static void Jalankan(Action aksi)
        {
            Exception terakhir = null;

            var t = new Thread(() =>
            {
                for (var i = 0; i < PercobaanMaks; i++)
                {
                    try { aksi(); terakhir = null; return; }
                    catch (Exception ex) { terakhir = ex; Thread.Sleep(JedaMs); }
                }
            });

            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
            t.Join();

            if (terakhir != null)
            {
                throw new InvalidOperationException(
                    "Papan klip tidak bisa diakses setelah " + PercobaanMaks +
                    " percobaan. Biasanya ada program lain yang sedang menguncinya. " +
                    terakhir.Message, terakhir);
            }
        }
    }
}

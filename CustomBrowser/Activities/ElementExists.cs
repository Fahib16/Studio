using System;
using System.Activities;
using System.ComponentModel;

namespace Custom.Browser
{
    /// <summary>
    /// Memeriksa apakah sebuah elemen ada, tanpa menggagalkan workflow kalau
    /// ternyata tidak ada — itu memang jawabannya, bukan kegagalan.
    ///
    /// Timeout di sini berarti "tunggu sampai SEGINI kalau belum muncul",
    /// bukan "harus muncul": begitu waktu habis, Exists diisi false dan
    /// workflow lanjut. Isi 0 kalau ingin memeriksa keadaan saat ini saja.
    ///
    /// Bekerja untuk selector web maupun desktop.
    /// </summary>
    [Designer(typeof(Design.ElementExistsDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Element Exists")]
    [Description("Mengecek keberadaan elemen web atau desktop.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.elementexists.png")]
    public sealed class ElementExists : ElementActivityBase
    {
        public ElementExists()
        {
            DisplayName = "Element Exists";
        }

        [Category("Output")]
        [DisplayName("Exists")]
        public OutArgument<bool> Exists { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = GetContinueOnError(context);

            try
            {
                var selector = GetSelector(context);

                var timeout = Timeout != null ? Timeout.Get(context) : TimeSpan.Zero;
                if (timeout < TimeSpan.Zero) timeout = TimeSpan.Zero;

                var exists = PollUntil(context, selector, wantedExists: true, timeout: timeout);
                if (Exists != null) Exists.Set(context, exists);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

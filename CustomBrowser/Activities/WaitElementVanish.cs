using System;
using System.Activities;
using System.ComponentModel;

namespace Custom.Browser
{
    /// <summary>
    /// Menunggu sampai sebuah elemen HILANG — kebalikan Element Exists.
    ///
    /// Pemakaian utamanya menunggu indikator "sedang memuat" lenyap sebelum
    /// melanjutkan. Elemen yang memang sudah tidak ada sejak awal langsung
    /// dianggap berhasil.
    ///
    /// Berbeda dari Element Exists, di sini timeout adalah TUNTUTAN: kalau
    /// elemennya masih ada setelah waktu habis, activity melempar (kecuali
    /// Throw On Timeout dimatikan), karena melanjutkan saat halaman masih
    /// sibuk hampir selalu berakhir salah.
    /// </summary>
    [Designer(typeof(Design.WaitElementVanishDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Wait Element Vanish")]
    [Description("Menunggu sampai elemen web atau desktop hilang.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.waitelementvanish.png")]
    public sealed class WaitElementVanish : ElementActivityBase
    {
        public WaitElementVanish()
        {
            DisplayName = "Wait Element Vanish";
        }

        [Category("Options")]
        [DisplayName("Throw On Timeout")]
        [Description("True (default): lempar exception kalau elemen masih ada saat waktu habis.")]
        [DefaultValue(true)]
        public bool ThrowOnTimeout { get; set; } = true;

        [Category("Output")]
        [DisplayName("Vanished")]
        public OutArgument<bool> Vanished { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = GetContinueOnError(context);

            try
            {
                var selector = GetSelector(context);
                var timeout = GetTimeout(context);

                var vanished = PollUntil(context, selector, wantedExists: false, timeout: timeout);
                if (Vanished != null) Vanished.Set(context, vanished);

                if (!vanished && ThrowOnTimeout)
                    throw new TimeoutException(
                        "Wait Element Vanish: elemen masih ada setelah " + timeout.TotalSeconds +
                        " detik. Selector: " + selector);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

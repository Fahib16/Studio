using System;
using System.Activities;
using System.ComponentModel;
using Custom.StudioBridge;
using Newtonsoft.Json.Linq;

namespace Custom.Browser
{
    public enum CheckAction
    {
        Check,
        Uncheck,

        /// <summary>Balik keadaan sekarang.</summary>
        Toggle
    }

    /// <summary>
    /// Mencentang / menghapus centang checkbox atau radio button.
    ///
    /// Untuk target WEB, elemen DIKLIK lebih dulu (bukan langsung diisi
    /// properti checked): banyak halaman baru bereaksi pada event klik, dan
    /// mengisi propertinya saja membuat centang berubah di layar tapi tidak di
    /// data halaman. Properti baru dipakai kalau klik tidak berhasil mengubah
    /// keadaannya.
    ///
    /// Radio button tidak bisa di-Uncheck — itu batasan HTML, bukan batasan
    /// activity ini, dan dilaporkan sebagai error daripada diam-diam tidak
    /// melakukan apa-apa.
    /// </summary>
    [Designer(typeof(Design.CheckElementDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Check / Uncheck")]
    [Description("Mencentang atau menghapus centang checkbox web/desktop.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.checkelement.png")]
    public sealed class CheckElement : ElementActivityBase
    {
        public CheckElement()
        {
            DisplayName = "Check / Uncheck";
        }

        [Category("Input")]
        [DisplayName("Action")]
        [Description("Check (default), Uncheck, atau Toggle.")]
        [DefaultValue(CheckAction.Check)]
        public CheckAction Action { get; set; } = CheckAction.Check;

        [Category("Output")]
        [DisplayName("Checked")]
        [Description("Keadaan centang setelah activity dijalankan.")]
        public OutArgument<bool> Checked { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = GetContinueOnError(context);

            try
            {
                var selector = GetSelector(context);
                var timeout = GetTimeout(context);
                var mode = Action.ToString().ToLowerInvariant();

                bool result;

                if (IsDesktop(selector))
                {
                    result = DesktopActions.SetCheck(selector, mode, timeout);
                }
                else
                {
                    var response = SendWeb(context, "setCheck", selector, timeout,
                                           request => request["mode"] = mode);
                    result = response?["checked"]?.Value<bool>() ?? false;
                }

                if (Checked != null) Checked.Set(context, result);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

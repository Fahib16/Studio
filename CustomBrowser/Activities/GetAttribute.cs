using System;
using System.Activities;
using System.ComponentModel;
using Custom.StudioBridge;
using Newtonsoft.Json.Linq;

namespace Custom.Browser
{
    /// <summary>
    /// Membaca satu atribut/properti sebuah elemen.
    ///
    /// Untuk elemen WEB, beberapa nama yang paling sering diminta sebenarnya
    /// properti DOM, bukan atribut HTML — dan itu memang yang diharapkan orang:
    ///   value      isi yang sedang ada di kotak isian (bukan atribut value
    ///              di HTML, yang tidak berubah saat user mengetik)
    ///   innerText  teks yang terlihat
    ///   outerHTML  seluruh HTML elemen; berguna dipasangkan dengan
    ///              Extract Data Table jalur Html
    ///   checked, selected, disabled, href, src, tag
    /// Nama lain dibaca apa adanya lewat getAttribute.
    ///
    /// Untuk elemen DESKTOP, nama propertinya disamakan dengan yang dipakai di
    /// selector: name, automationid, classname, controltype, isenabled,
    /// isoffscreen, checked, value.
    /// </summary>
    [Designer(typeof(Design.GetAttributeDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Get Attribute")]
    [Description("Membaca atribut atau properti elemen web/desktop.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.getattribute.png")]
    public sealed class GetAttribute : ElementActivityBase
    {
        public GetAttribute()
        {
            DisplayName = "Get Attribute";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Attribute Name")]
        [Description("Nama atribut/properti, mis. value, innerText, href, class.")]
        public InArgument<string> AttributeName { get; set; }

        [Category("Output")]
        [DisplayName("Value")]
        public OutArgument<string> Value { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = GetContinueOnError(context);

            try
            {
                var selector = GetSelector(context);
                var timeout = GetTimeout(context);

                var name = AttributeName.Get(context);
                if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Attribute Name kosong.");

                string value;

                if (IsDesktop(selector))
                {
                    value = DesktopActions.GetProperty(selector, name, timeout);
                }
                else
                {
                    var result = SendWeb(context, "getAttribute", selector, timeout,
                                         request => request["name"] = name);
                    value = result?["value"]?.Value<string>() ?? "";
                }

                if (Value != null) Value.Set(context, value);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

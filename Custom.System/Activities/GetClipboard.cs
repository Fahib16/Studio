using System;
using System.Activities;
using System.ComponentModel;

namespace CustomSystem
{
    /// <summary>
    /// Membaca teks dari papan klip Windows.
    ///
    /// Pengganti Insert Clipboard milik OpenRPA. Namanya sengaja BERBEDA:
    /// "Insert" menyiratkan menempel ke suatu tempat, padahal yang dikerjakan
    /// hanyalah membaca isinya. Menempelkannya dilakukan dengan Type Into atau
    /// Send Hotkey, dan memisahkan keduanya membuat workflow-nya bisa dibaca.
    /// </summary>
    [Designer(typeof(Design.GetClipboardDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Get Clipboard Text")]
    [Description("Membaca teks yang sedang ada di papan klip Windows.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.clipboardpaste.png")]
    public sealed class GetClipboard : CodeActivity
    {
        public GetClipboard()
        {
            DisplayName = "Get Clipboard Text";
        }

        [Category("Output")]
        [DisplayName("Text")]
        [Description("Teks yang terbaca. Kosong kalau papan klip tidak berisi teks.")]
        public OutArgument<string> Text { get; set; }

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
                if (Text != null) Text.Set(context, ClipboardHelper.GetText());
            }
            catch (Exception) when (continueOnError)
            {
                if (Text != null) Text.Set(context, "");
            }
        }
    }
}

using System;
using System.Activities;
using System.ComponentModel;
using OpenRPA.Interfaces;

namespace CustomSystem
{
    /// <summary>
    /// Tingkat log yang bisa dipilih di kartu activity.
    ///
    /// Hanya empat tingkat ini yang disediakan supaya pilihannya tetap
    /// gampang dibedakan; masing-masing dipetakan ke method Log yang
    /// benar-benar ada di OpenRPA.Interfaces.Log (lihat LogMessage.Execute).
    /// </summary>
    public enum LogMessageLevel
    {
        Trace,
        Info,
        Warn,
        Error
    }

    /// <summary>
    /// Menulis satu baris ke log OpenRPA (panel Output dan file log), bukan ke
    /// Console. OpenRPA.Script sudah punya "Custom Log Message", tapi activity
    /// itu hanya Console.WriteLine sehingga pesannya tidak masuk ke log OpenRPA
    /// dan tidak bisa disaring per level — karena itu yang ini dibuat memakai
    /// OpenRPA.Interfaces.Log.
    /// </summary>
    [Designer(typeof(Design.LogMessageDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Log Message")]
    [Description("Menulis pesan ke log OpenRPA pada tingkat yang dipilih.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.logmessage.png")]
    public class LogMessage : CodeActivity
    {
        public LogMessage()
        {
            DisplayName = "Log Message";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Message")]
        [Description("Pesan yang ditulis ke log.")]
        public InArgument<string> Message { get; set; }

        /// <summary>
        /// Plain property, bukan InArgument: nilainya dipilih dari dropdown saat
        /// mendesain workflow dan tidak pernah berubah saat runtime, sama seperti
        /// Log Level di UiPath.
        /// </summary>
        [Category("Input")]
        [DisplayName("Level")]
        [Description("Tingkat log: Trace, Info (default), Warn, atau Error.")]
        [DefaultValue(LogMessageLevel.Info)]
        public LogMessageLevel Level { get; set; } = LogMessageLevel.Info;

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
                var message = Message != null ? Message.Get(context) : null;
                if (message == null) message = "";

                // Kategori Trace milik OpenRPA. Pesan dikirim LANGSUNG ke
                // System.Diagnostics.Trace, bukan lewat Log.Verbose/Warning/
                // Error, karena method-method itu disaring pengaturan global
                // (log_verbose bawaannya mati). Akibat penyaringan itu, dulu
                // hanya level Info yang benar-benar muncul di panel Output —
                // padahal orang yang memilih level Warn di activity ini jelas
                // sedang meminta pesannya tampil.
                string category;
                switch (Level)
                {
                    case LogMessageLevel.Trace: category = "Verbose"; break;
                    case LogMessageLevel.Warn: category = "Warning"; break;
                    case LogMessageLevel.Error: category = "Error"; break;
                    default: category = "Output"; break;
                }

                System.Diagnostics.Trace.WriteLine(message, category);

                // Tetap ikut tercatat di berkas log seperti pesan lain.
                Log.LogLine(message, category);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

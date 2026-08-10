using System;
using System.Activities;
using System.ComponentModel;
using System.Linq;

namespace OpenRPA.Script.Activities // Sesuaikan dengan namespace proyek ini
{
    // PENTING - pola attribute di bawah ini disamakan dengan InvokeCode.cs & PipInstall.cs
    // (activity lain di project ini yang SUDAH terbukti muncul di Toolbox):
    //   1. [Designer] pakai 2 parameter (bukan 1) - sertakan typeof(IDesigner)
    //   2. [ToolboxBitmap] WAJIB ada supaya item ini ke-scan oleh Toolbox mereka
    //      (sementara pinjam ikon InvokeCode yang sudah pasti ada di project ini,
    //      supaya tidak gagal gara-gara resource .png belum ada)
    [Designer(typeof(Designers.CustomLogMessageDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [System.Drawing.ToolboxBitmap(typeof(InvokeCode), "Resources.toolbox.invokecode.png")]
    [Category("Custom Tools")] // Akan muncul di kategori ini di Toolbox
    [DisplayName("Custom Log Message")]
    [Description("Log message buatan sendiri")]
    public class CustomLogMessage : CodeActivity
    {
        [RequiredArgument]
        [Category("Input")]
        [DisplayName("Pesan")]
        public InArgument<string> Message { get; set; }

        /// <summary>
        /// Level log yang dipilih di dropdown "Log Level" pada Designer.
        /// Sengaja bertipe string biasa (bukan enum custom) supaya XAML Designer
        /// tidak perlu mereferensikan tipe custom apa pun.
        /// Nilai yang valid: "Trace", "Debug", "Info", "Warn", "Error", "Fatal".
        /// </summary>
        [Category("Input")]
        [DisplayName("Level Log")]
        [DefaultValue("Info")]
        public string LogLevel { get; set; } = "Info";

        protected override void Execute(CodeActivityContext context)
        {
            string logText = Message.Get(context);

            // Mencetak ke panel output/log OpenRPA
            Console.WriteLine($"[CUSTOM LOG] [{LogLevel}] {DateTime.Now} - {logText}");
        }

        // Fallback DisplayName sama seperti pola di InvokeCode.cs / PipInstall.cs,
        // supaya nama yang tampil di canvas/Toolbox konsisten dengan attribute DisplayName di atas.
        public new string DisplayName
        {
            get
            {
                var displayName = base.DisplayName;
                if (displayName == this.GetType().Name)
                {
                    var displayNameAttribute = this.GetType()
                        .GetCustomAttributes(typeof(DisplayNameAttribute), true)
                        .FirstOrDefault() as DisplayNameAttribute;
                    if (displayNameAttribute != null) displayName = displayNameAttribute.DisplayName;
                }
                return displayName;
            }
            set
            {
                base.DisplayName = value;
            }
        }
    }
}

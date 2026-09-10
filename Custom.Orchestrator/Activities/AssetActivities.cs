using System;
using System.Activities;
using System.ComponentModel;
using Newtonsoft.Json.Linq;
using Custom.Orchestrator.Runtime;
using Custom.Shared;

namespace Custom.Orchestrator.Activities
{
    /// <summary>
    /// Baca nilai aset dari ForgeHub.
    ///
    /// Aset adalah nilai yang boleh berubah tanpa mengubah workflow: alamat
    /// folder, ambang batas, alamat surel penerima laporan. Menaruhnya di
    /// ForgeHub berarti mengubahnya cukup lewat dasbor — tanpa membuka Studio,
    /// tanpa menerbitkan ulang, tanpa menghentikan robot.
    /// </summary>
    [Designer(typeof(GetAssetDesigner))]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.getasset.png")]
    [DisplayName("Get Asset")]
    [Description("Baca nilai sebuah aset dari ForgeHub.")]
    public class GetAsset : CodeActivity
    {
        [RequiredArgument, Category("Input")]
        public InArgument<string> AssetName { get; set; }

        [Category("Output")]
        public OutArgument<string> Value { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var name = AssetName.Get(context);

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Nama aset wajib diisi.");

            var response = HubConnection.Send("GET",
                "/api/assets/" + Uri.EscapeDataString(name) + "/value", null);

            var value = response == null ? null : (string)response["value"];
            Value.Set(context, value);

            // Nilainya sendiri TIDAK dicatat: aset bertipe Secret dan Credential
            // melewati jalur yang sama, dan log adalah tempat yang paling mudah
            // dibaca orang lain.
            RobotLog.Info("Membaca aset '" + name + "'.", DisplayName);
        }
    }

    // ==================================================================

    /// <summary>
    /// Baca nama pengguna dan kata sandi dari ForgeHub.
    ///
    /// Terpisah dari Get Asset karena keluarannya dua, dan karena memisahkannya
    /// membuat jelas di kanvas mana yang menyentuh kata sandi — hal yang berguna
    /// saat menelaah workflow orang lain.
    /// </summary>
    [Designer(typeof(GetCredentialDesigner))]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.getcredential.png")]
    [DisplayName("Get Credential")]
    [Description("Baca nama pengguna dan kata sandi dari ForgeHub.")]
    public class GetCredential : CodeActivity
    {
        [RequiredArgument, Category("Input")]
        public InArgument<string> CredentialName { get; set; }

        [Category("Output")]
        public OutArgument<string> Username { get; set; }

        [Category("Output")]
        public OutArgument<string> Password { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var name = CredentialName.Get(context);

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Nama kredensial wajib diisi.");

            var response = HubConnection.Send("GET",
                "/api/credentials/" + Uri.EscapeDataString(name) + "/value", null);

            if (response == null)
                throw new InvalidOperationException("Kredensial '" + name + "' tidak ada di ForgeHub.");

            Username.Set(context, (string)response["username"]);
            Password.Set(context, (string)response["password"]);

            RobotLog.Info("Membaca kredensial '" + name + "'.", DisplayName);
        }
    }

    // ==================================================================

    /// <summary>
    /// Tulis satu baris ke log robot.
    ///
    /// Berbeda dari Log Message di Custom.System, yang menulis ke catatan
    /// Studio: yang ini menulis ke saluran log ROBOT, sehingga barisnya muncul
    /// di panel JakRunner dan terkirim ke ForgeHub — terlihat di dasbor saat
    /// robotnya sedang berjalan, dari komputer mana pun.
    /// </summary>
    [Designer(typeof(WriteRobotLogDesigner))]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.writerobotlog.png")]
    [DisplayName("Write Robot Log")]
    [Description("Tulis satu baris ke log robot; terlihat di JakRunner dan ForgeHub.")]
    public class WriteRobotLog : CodeActivity
    {
        [RequiredArgument, Category("Input")]
        public InArgument<string> Message { get; set; }

        [Category("Input")]
        [Description("Trace, Debug, Info, Warning, atau Error. Kosong berarti Info.")]
        public InArgument<string> Level { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var message = Message.Get(context);
            if (string.IsNullOrEmpty(message)) return;

            var level = RobotLogLevel.Info;
            var requested = Level.Get(context);

            if (!string.IsNullOrWhiteSpace(requested))
            {
                // Tingkat yang salah eja tidak menjatuhkan robot; barisnya tetap
                // tercatat sebagai Info, karena kehilangan catatan lebih buruk
                // daripada mencatatnya pada tingkat yang keliru.
                try
                {
                    level = (RobotLogLevel)Enum.Parse(typeof(RobotLogLevel), requested.Trim(), true);
                }
                catch (Exception)
                {
                    RobotLog.Warning("Tingkat log '" + requested + "' tidak dikenal; dicatat sebagai Info.", DisplayName);
                }
            }

            RobotLog.Write(level, message, DisplayName);
        }
    }
}

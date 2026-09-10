using System;
using System.Activities;
using System.ComponentModel;
using MailKit;
using MailKit.Net.Imap;

namespace Custom.Mail
{
    /// <summary>
    /// Memindahkan satu email ke folder IMAP lain.
    ///
    /// HANYA untuk IMAP: POP3 tidak mengenal folder sama sekali, jadi tidak
    /// ada padanan yang jujur di sana. Kalau MailItem-nya berasal dari Get
    /// Mail (POP3), activity ini akan menolak dengan pesan yang menyebut
    /// alasannya, bukan gagal dengan error protokol yang membingungkan.
    ///
    /// Folder tujuan dibuat kalau belum ada (Create Folder If Missing, default
    /// true) — memindahkan ke folder arsip yang belum pernah dibuat adalah
    /// kejadian yang wajar pada pemakaian pertama.
    /// </summary>
    [Designer(typeof(Design.MoveMailToFolderDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Move Mail To Folder")]
    [Description("Memindahkan email ke folder IMAP lain.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.movemail.png")]
    public sealed class MoveMailToFolder : MailServerActivity
    {
        private const int DefaultPort = 143;
        private const int SslPort = 993;

        public MoveMailToFolder()
        {
            DisplayName = "Move Mail To Folder";
            Port = new InArgument<int>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<int>("993"));
            CreateFolderIfMissing = new InArgument<bool>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<bool>("True"));
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Message")]
        [Description("Email yang dipindahkan, dari output Get Mail (IMAP).")]
        public InArgument<MailItem> Message { get; set; }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Destination Folder")]
        [Description("Nama folder tujuan, mis. Arsip atau Processed.")]
        public InArgument<string> DestinationFolder { get; set; }

        [Category("Options")]
        [DisplayName("Create Folder If Missing")]
        [Description("Kalau true (default), folder tujuan dibuat kalau belum ada.")]
        public InArgument<bool> CreateFolderIfMissing { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = GetContinueOnError(context);

            try
            {
                var item = Message.Get(context);
                if (item == null) throw new ArgumentException("Message kosong.");

                if (string.IsNullOrWhiteSpace(item.Folder))
                    throw new ArgumentException(
                        "Email ini tidak berasal dari IMAP (tidak punya folder asal). " +
                        "Move Mail To Folder hanya bisa dipakai untuk email hasil Get Mail (IMAP).");

                UniqueId uid;
                if (!UniqueId.TryParse(item.Id, out uid))
                    throw new ArgumentException("Id email tidak dikenali sebagai UID IMAP: " + item.Id);

                var destinationName = DestinationFolder.Get(context);
                if (string.IsNullOrWhiteSpace(destinationName))
                    throw new ArgumentException("Destination Folder kosong.");

                var createIfMissing = CreateFolderIfMissing == null || CreateFolderIfMissing.Get(context);

                var host = GetHost(context);
                var port = GetPort(context, DefaultPort);

                using (var client = new ImapClient())
                {
                    client.Timeout = GetTimeoutMs(context);
                    client.Connect(host, port, GetSecurity(port, SslPort));
                    AuthenticateIfNeeded(context, client);

                    var source = client.GetFolder(item.Folder);
                    source.Open(FolderAccess.ReadWrite);

                    var destination = FindOrCreate(client, destinationName, createIfMissing);
                    source.MoveTo(uid, destination);

                    client.Disconnect(true);
                }
            }
            catch (Exception) when (continueOnError)
            {
            }
        }

        private static IMailFolder FindOrCreate(ImapClient client, string name, bool createIfMissing)
        {
            try
            {
                return client.GetFolder(name);
            }
            catch (FolderNotFoundException)
            {
                if (!createIfMissing)
                    throw new InvalidOperationException("Folder tujuan tidak ada: " + name);

                // Folder baru dibuat di bawah namespace pribadi akun, bukan di
                // bawah folder asal — itu yang orang maksud saat menulis nama
                // folder polos seperti "Arsip".
                var root = client.GetFolder(client.PersonalNamespaces[0]);
                return root.Create(name, true);
            }
        }
    }
}

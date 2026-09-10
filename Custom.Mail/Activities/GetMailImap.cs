using System;
using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;

namespace Custom.Mail
{
    /// <summary>
    /// Mengambil email lewat IMAP.
    ///
    /// Pesan dikembalikan sebagai MailItem, bukan MimeMessage mentah, karena
    /// MailItem membawa UID dan nama folder — tanpa itu Move Mail To Folder
    /// tidak punya cara menunjuk pesan mana yang dimaksud.
    ///
    /// Urutannya dari yang TERBARU: kalau Top dibatasi, yang orang maksud
    /// hampir selalu "email terbaru", bukan email paling lama di kotak masuk.
    ///
    /// Mark As Read dijalankan SETELAH seluruh pesan berhasil diunduh, bukan
    /// per pesan, supaya kegagalan di tengah jalan tidak meninggalkan sebagian
    /// email tertandai sudah dibaca padahal workflow-nya belum memprosesnya.
    /// </summary>
    [Designer(typeof(Design.GetMailImapDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Get Mail (IMAP)")]
    [Description("Mengambil email dari server IMAP.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.getmailimap.png")]
    public sealed class GetMailImap : MailServerActivity
    {
        private const int DefaultPort = 143;
        private const int SslPort = 993;

        public GetMailImap()
        {
            DisplayName = "Get Mail (IMAP)";
            Port = new InArgument<int>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<int>("993"));
            Folder = new InArgument<string>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<string>("\"INBOX\""));
            Top = new InArgument<int>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<int>("10"));
        }

        [Category("Input")]
        [DisplayName("Folder")]
        [Description("Nama folder IMAP (default INBOX).")]
        public InArgument<string> Folder { get; set; }

        [Category("Options")]
        [DisplayName("Top")]
        [Description("Jumlah email terbaru yang diambil (default 10). 0 berarti semua yang cocok.")]
        public InArgument<int> Top { get; set; }

        [Category("Options")]
        [DisplayName("Only Unread")]
        [Description("Kalau true, hanya email yang belum dibaca (default false).")]
        public InArgument<bool> OnlyUnread { get; set; }

        [Category("Options")]
        [DisplayName("Mark As Read")]
        [Description("Kalau true, email yang diambil ditandai sudah dibaca (default false).")]
        public InArgument<bool> MarkAsRead { get; set; }

        [Category("Output")]
        [DisplayName("Messages")]
        public OutArgument<MailItem[]> Messages { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = GetContinueOnError(context);

            try
            {
                var host = GetHost(context);
                var port = GetPort(context, DefaultPort);
                var folderName = Folder != null ? Folder.Get(context) : null;
                if (string.IsNullOrWhiteSpace(folderName)) folderName = "INBOX";

                var top = Top != null ? Top.Get(context) : 0;
                var onlyUnread = OnlyUnread != null && OnlyUnread.Get(context);
                var markAsRead = MarkAsRead != null && MarkAsRead.Get(context);

                var items = new List<MailItem>();

                using (var client = new ImapClient())
                {
                    client.Timeout = GetTimeoutMs(context);
                    client.Connect(host, port, GetSecurity(port, SslPort));
                    AuthenticateIfNeeded(context, client);

                    var folder = string.Equals(folderName, "INBOX", StringComparison.OrdinalIgnoreCase)
                        ? client.Inbox
                        : client.GetFolder(folderName);

                    folder.Open(markAsRead ? FolderAccess.ReadWrite : FolderAccess.ReadOnly);

                    var uids = folder.Search(onlyUnread ? SearchQuery.NotSeen : SearchQuery.All);

                    // Terbaru dulu, lalu dipotong sebanyak Top.
                    var selected = uids.Reverse().ToList();
                    if (top > 0) selected = selected.Take(top).ToList();

                    foreach (var uid in selected)
                    {
                        var message = folder.GetMessage(uid);
                        items.Add(new MailItem(uid.ToString(), folder.FullName, message, seen: !onlyUnread));
                    }

                    if (markAsRead && selected.Count > 0)
                        folder.AddFlags(selected, MessageFlags.Seen, true);

                    client.Disconnect(true);
                }

                if (Messages != null) Messages.Set(context, items.ToArray());
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

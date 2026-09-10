using System;
using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;
using MailKit.Net.Pop3;

namespace Custom.Mail
{
    /// <summary>
    /// Mengambil email lewat POP3.
    ///
    /// POP3 TIDAK punya folder, tidak punya penanda sudah/belum dibaca, dan
    /// tidak punya UID yang stabil antar sesi seperti IMAP. Karena itu:
    ///
    /// - Only Unread dan Mark As Read TIDAK disediakan di sini. Protokolnya
    ///   memang tidak menyimpan status itu; menyediakan propertinya hanya akan
    ///   jadi hiasan yang menyesatkan. Kalau butuh dua hal itu, pakai IMAP.
    /// - Delete After Download disediakan sebagai gantinya, karena itulah cara
    ///   POP3 menandai "sudah diproses". Berjalan setelah SELURUH pesan
    ///   berhasil diunduh, supaya kegagalan di tengah jalan tidak menghapus
    ///   email yang belum sempat diproses workflow.
    /// - Id pada MailItem berisi NOMOR URUT pesan dalam sesi ini, bukan UID
    ///   yang bisa dipakai lagi nanti.
    /// </summary>
    [Designer(typeof(Design.GetMailPop3Designer), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Get Mail (POP3)")]
    [Description("Mengambil email dari server POP3.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.getmailpop3.png")]
    public sealed class GetMailPop3 : MailServerActivity
    {
        private const int DefaultPort = 110;
        private const int SslPort = 995;

        public GetMailPop3()
        {
            DisplayName = "Get Mail (POP3)";
            Port = new InArgument<int>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<int>("995"));
            Top = new InArgument<int>(
                new Microsoft.VisualBasic.Activities.VisualBasicValue<int>("10"));
        }

        [Category("Options")]
        [DisplayName("Top")]
        [Description("Jumlah email terbaru yang diambil (default 10). 0 berarti semua.")]
        public InArgument<int> Top { get; set; }

        [Category("Options")]
        [DisplayName("Delete After Download")]
        [Description("Kalau true, email dihapus dari server setelah semuanya berhasil diunduh " +
                     "(default false). Ini satu-satunya cara POP3 menandai email sudah diproses.")]
        public InArgument<bool> DeleteAfterDownload { get; set; }

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
                var top = Top != null ? Top.Get(context) : 0;
                var delete = DeleteAfterDownload != null && DeleteAfterDownload.Get(context);

                var items = new List<MailItem>();

                using (var client = new Pop3Client())
                {
                    client.Timeout = GetTimeoutMs(context);
                    client.Connect(host, port, GetSecurity(port, SslPort));
                    AuthenticateIfNeeded(context, client);

                    var count = client.Count;
                    var take = (top > 0 && top < count) ? top : count;

                    // Pesan paling baru ada di indeks terakhir, jadi diambil
                    // mundur supaya hasilnya urut dari yang terbaru.
                    var indexes = new List<int>();
                    for (int i = count - 1; i >= count - take; i--) indexes.Add(i);

                    foreach (var i in indexes)
                    {
                        var message = client.GetMessage(i);
                        items.Add(new MailItem(i.ToString(), "", message, seen: true));
                    }

                    if (delete && indexes.Count > 0)
                    {
                        client.DeleteMessages(indexes);
                        client.Disconnect(true);   // penghapusan POP3 baru berlaku saat QUIT
                    }
                    else
                    {
                        client.Disconnect(true);
                    }
                }

                if (Messages != null) Messages.Set(context, items.ToArray());
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}

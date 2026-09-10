using System;
using System.Activities;
using System.ComponentModel;
using System.IO;
using System.Linq;
using MailKit.Net.Smtp;
using MimeKit;

namespace Custom.Mail
{
    /// <summary>
    /// Mengirim email lewat SMTP.
    ///
    /// Pengaturan server (Host, Port, Security, Username, Password, Timeout)
    /// diwarisi dari MailServerActivity supaya sama persis dengan activity
    /// email lainnya.
    ///
    /// Password bertipe SecureString, bukan String, supaya bisa langsung
    /// disambung dari activity Get Credentials milik OpenRPA.Utilities yang
    /// memang mengeluarkan SecureString — jadi kata sandi tidak perlu singgah
    /// di variabel workflow biasa.
    /// </summary>
    [Designer(typeof(Design.SendMailDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Send Mail (SMTP)")]
    [Description("Mengirim email lewat server SMTP.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.sendmail.png")]
    public sealed class SendMail : MailServerActivity
    {
        /// <summary>Port SMTP submission; dipakai kalau Port dikosongkan.</summary>
        private const int DefaultPort = 587;

        /// <summary>Port SMTP yang secara historis berarti TLS sejak koneksi dibuka.</summary>
        private const int SslPort = 465;

        public SendMail()
        {
            DisplayName = "Send Mail (SMTP)";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("From")]
        [Description("Alamat pengirim.")]
        public InArgument<string> From { get; set; }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("To")]
        [Description("Satu atau beberapa alamat tujuan, dipisah koma atau titik koma.")]
        public InArgument<string> To { get; set; }

        [Category("Input")]
        [DisplayName("Cc")]
        public InArgument<string> Cc { get; set; }

        [Category("Input")]
        [DisplayName("Bcc")]
        public InArgument<string> Bcc { get; set; }

        [Category("Input")]
        [DisplayName("Subject")]
        public InArgument<string> Subject { get; set; }

        [Category("Input")]
        [DisplayName("Body")]
        public InArgument<string> Body { get; set; }

        [Category("Options")]
        [DisplayName("Is Body Html")]
        [Description("True kalau Body berisi HTML (default false).")]
        public InArgument<bool> IsBodyHtml { get; set; }

        [Category("Options")]
        [DisplayName("Attachments")]
        [Description("Path berkas yang dilampirkan.")]
        public InArgument<string[]> Attachments { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = GetContinueOnError(context);

            try
            {
                var message = BuildMessage(context);

                var host = GetHost(context);
                var port = GetPort(context, DefaultPort);

                using (var client = new SmtpClient())
                {
                    client.Timeout = GetTimeoutMs(context);
                    client.Connect(host, port, GetSecurity(port, SslPort));

                    AuthenticateIfNeeded(context, client);

                    client.Send(message);
                    client.Disconnect(true);
                }
            }
            catch (Exception) when (continueOnError)
            {
            }
        }

        /// <summary>
        /// Susun pesannya. Dipisah dari Execute supaya bisa diuji tanpa server
        /// SMTP — bagian inilah yang paling mungkin salah (alamat, lampiran,
        /// HTML atau teks), sedangkan pengirimannya cuma satu panggilan.
        /// </summary>
        internal MimeMessage BuildMessage(CodeActivityContext context)
        {
            var message = new MimeMessage();

            var from = From.Get(context);
            if (string.IsNullOrWhiteSpace(from)) throw new ArgumentException("From kosong.");
            message.From.Add(MailboxAddress.Parse(from));

            AddAll(message.To, To != null ? To.Get(context) : null);
            AddAll(message.Cc, Cc != null ? Cc.Get(context) : null);
            AddAll(message.Bcc, Bcc != null ? Bcc.Get(context) : null);

            if (message.To.Count == 0 && message.Cc.Count == 0 && message.Bcc.Count == 0)
                throw new ArgumentException("Tidak ada alamat tujuan di To, Cc, maupun Bcc.");

            message.Subject = Subject != null ? (Subject.Get(context) ?? "") : "";

            var isHtml = IsBodyHtml != null && IsBodyHtml.Get(context);
            var body = Body != null ? (Body.Get(context) ?? "") : "";

            var builder = new BodyBuilder();
            if (isHtml) builder.HtmlBody = body; else builder.TextBody = body;

            var attachments = Attachments != null ? Attachments.Get(context) : null;
            if (attachments != null)
            {
                foreach (var path in attachments.Where(p => !string.IsNullOrWhiteSpace(p)))
                {
                    if (!File.Exists(path))
                        throw new FileNotFoundException("Lampiran tidak ditemukan: " + path, path);
                    builder.Attachments.Add(path);
                }
            }

            message.Body = builder.ToMessageBody();
            return message;
        }

        /// <summary>
        /// Alamat dipisah koma ATAU titik koma: dua-duanya lazim ditulis orang,
        /// dan menolak salah satunya hanya akan jadi kejutan.
        /// </summary>
        private static void AddAll(InternetAddressList list, string addresses)
        {
            if (string.IsNullOrWhiteSpace(addresses)) return;

            foreach (var one in addresses.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = one.Trim();
                if (trimmed.Length == 0) continue;
                list.Add(MailboxAddress.Parse(trimmed));
            }
        }
    }
}

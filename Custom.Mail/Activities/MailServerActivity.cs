using System;
using System.Activities;
using System.ComponentModel;
using System.Security;
using MailKit.Security;

namespace Custom.Mail
{
    public enum MailSecurity
    {
        /// <summary>Pilih dari nomor port: port SSL baku memakai TLS langsung, selain itu STARTTLS bila didukung.</summary>
        Auto,

        /// <summary>Tanpa enkripsi sama sekali. Hanya masuk akal untuk server internal.</summary>
        None,

        /// <summary>Mulai polos lalu naik ke TLS.</summary>
        StartTls,

        /// <summary>TLS sejak koneksi dibuka.</summary>
        SslOnConnect
    }

    /// <summary>
    /// Bagian yang sama untuk semua activity yang menghubungi server email:
    /// alamat server, kredensial, dan batas waktu.
    ///
    /// Dibuat sebagai kelas dasar supaya lima activity tidak masing-masing
    /// punya salinan properti yang sama — kalau disalin, cepat atau lambat
    /// salah satunya menyimpang (mis. default port berbeda) dan itu jenis
    /// perbedaan yang tidak kelihatan sampai ada yang kebingungan.
    ///
    /// Kelas ini abstract, jadi tidak ikut muncul di toolbox
    /// (wfToolbox.xaml.cs menyaring tipe abstract).
    /// </summary>
    public abstract class MailServerActivity : CodeActivity
    {
        [Category("Server")]
        [RequiredArgument]
        [DisplayName("Host")]
        [Description("Alamat server, mis. imap.gmail.com.")]
        public InArgument<string> Host { get; set; }

        [Category("Server")]
        [DisplayName("Port")]
        [Description("Kosong atau 0 berarti port baku protokolnya.")]
        public InArgument<int> Port { get; set; }

        [Category("Server")]
        [DisplayName("Security")]
        [Description("Auto (default) memilih dari nomor port.")]
        [DefaultValue(MailSecurity.Auto)]
        public MailSecurity Security { get; set; } = MailSecurity.Auto;

        [Category("Server")]
        [DisplayName("Username")]
        public InArgument<string> Username { get; set; }

        [Category("Server")]
        [DisplayName("Password")]
        [Description("SecureString. Bisa diambil dari activity Get Credentials milik OpenRPA.Utilities, atau " +
                     "New System.Net.NetworkCredential(\"\", \"rahasia\").SecurePassword")]
        public InArgument<SecureString> Password { get; set; }

        [Category("Options")]
        [DisplayName("Timeout")]
        [Description("Batas waktu koneksi dan operasi (default 60 detik kalau kosong).")]
        public InArgument<TimeSpan> Timeout { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [Description("Kalau true, kegagalan activity ini tidak menghentikan workflow (default false).")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        protected bool GetContinueOnError(CodeActivityContext context)
        {
            return ContinueOnError != null && ContinueOnError.Get(context);
        }

        protected string GetHost(CodeActivityContext context)
        {
            var host = Host.Get(context);
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host kosong.");
            return host;
        }

        protected int GetPort(CodeActivityContext context, int fallback)
        {
            var port = Port != null ? Port.Get(context) : 0;
            return port > 0 ? port : fallback;
        }

        protected int GetTimeoutMs(CodeActivityContext context)
        {
            var timeout = Timeout != null ? Timeout.Get(context) : TimeSpan.Zero;
            if (timeout <= TimeSpan.Zero) timeout = TimeSpan.FromSeconds(60);
            return (int)timeout.TotalMilliseconds;
        }

        /// <summary>
        /// Terjemahkan pilihan Security. Pada Auto, port SSL baku protokolnya
        /// (465 SMTP, 993 IMAP, 995 POP3) berarti TLS sejak koneksi dibuka;
        /// port lain memakai STARTTLS kalau server mengiklankannya.
        /// </summary>
        protected SecureSocketOptions GetSecurity(int port, int sslPort)
        {
            switch (Security)
            {
                case MailSecurity.None: return SecureSocketOptions.None;
                case MailSecurity.StartTls: return SecureSocketOptions.StartTls;
                case MailSecurity.SslOnConnect: return SecureSocketOptions.SslOnConnect;
                default:
                    return port == sslPort
                        ? SecureSocketOptions.SslOnConnect
                        : SecureSocketOptions.StartTlsWhenAvailable;
            }
        }

        /// <summary>
        /// Login kalau Username diisi. Username kosong berarti server tidak
        /// meminta autentikasi (relay internal) — keadaan yang sah, bukan error.
        /// </summary>
        protected void AuthenticateIfNeeded(CodeActivityContext context, MailKit.MailService client)
        {
            var username = Username != null ? Username.Get(context) : null;
            if (string.IsNullOrWhiteSpace(username)) return;

            var password = Password != null ? Password.Get(context) : null;
            client.Authenticate(username, SecureStrings.ToPlain(password));
        }
    }
}

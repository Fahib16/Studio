using System;
using System.Runtime.Serialization;

namespace OpenRPA.Interfaces
{
    /// <summary>
    /// Kesalahan yang berasal dari DATA atau aturan bisnis, bukan dari sistem.
    ///
    /// Bedanya dengan Exception biasa menentukan apa yang dilakukan robot
    /// sesudahnya, jadi pemisahan ini bukan sekadar rapi-rapi:
    ///
    ///   BusinessRuleException  transaksi itu dilewati, robot lanjut ke
    ///                          transaksi berikutnya. TIDAK diulang, karena
    ///                          mengulang data yang sama akan gagal lagi.
    ///
    ///   Exception biasa        dianggap gangguan sistem (aplikasi tidak
    ///                          merespons, jaringan putus). Robot kembali ke
    ///                          Initialization dan mencoba lagi.
    ///
    /// Dipakai template Robotic Enterprise Framework, tapi berguna di workflow
    /// mana pun yang perlu membedakan keduanya.
    /// </summary>
    [Serializable]
    public class BusinessRuleException : Exception
    {
        public BusinessRuleException() { }

        public BusinessRuleException(string message) : base(message) { }

        public BusinessRuleException(string message, Exception innerException)
            : base(message, innerException) { }

        protected BusinessRuleException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}

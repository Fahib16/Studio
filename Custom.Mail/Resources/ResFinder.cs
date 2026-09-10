namespace Custom.Mail
{
    /// <summary>
    /// Penanda lokasi resource untuk [ToolboxBitmap].
    ///
    /// ToolboxBitmapAttribute mencari gambarnya lewat
    /// Assembly.GetManifestResourceStream(type, name), yang menempelkan
    /// NAMESPACE tipe ini di depan nama yang diberikan. Jadi kelas kosong ini
    /// ada semata-mata supaya nama resource yang dicari menjadi
    /// "Custom.Mail.Resources.<nama>.png" — sama persis dengan LogicalName
    /// yang ditulis di berkas project. Pola yang sama dipakai OpenRPA sendiri
    /// (lihat OpenRPA/Resources/ResFinder.cs).
    /// </summary>
    public class ResFinder
    {
    }
}

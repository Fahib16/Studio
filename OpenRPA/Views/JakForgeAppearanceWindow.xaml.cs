using System.Windows;
using Custom.Shared;

namespace OpenRPA.Views
{
    /// <summary>
    /// Pemilih bahasa dan tema.
    ///
    /// SATU-SATUNYA tempat kedua setelan itu diubah. JakRunner tidak punya
    /// pemilihnya sendiri: ia membaca setelan yang sama, sesuai permintaan
    /// bahwa asisten mengikuti Studio. Dua pemilih untuk satu nilai hanya
    /// menciptakan pertanyaan "yang mana yang menang".
    ///
    /// Temanya diterapkan SEKETIKA saat disimpan, bukan menunggu Studio
    /// dijalankan ulang — orang yang baru saja memilih gelap perlu melihat
    /// hasilnya untuk tahu pilihannya sudah masuk.
    /// </summary>
    public partial class JakForgeAppearanceWindow : Window
    {
        private readonly UiLanguage[] _bahasa =
        {
            UiLanguage.Indonesia, UiLanguage.English, UiLanguage.Jawa,
        };

        private readonly UiTheme[] _tema = { UiTheme.Light, UiTheme.Dark };

        public JakForgeAppearanceWindow()
        {
            InitializeComponent();

            CmbLang.ItemsSource = new[]
            {
                JakForgeText.T("Indonesia"),
                JakForgeText.T("Inggris"),
                JakForgeText.T("Jawa"),
            };

            CmbTheme.ItemsSource = new[]
            {
                JakForgeText.T("Terang"),
                JakForgeText.T("Gelap"),
            };

            CmbLang.SelectedIndex = System.Array.IndexOf(_bahasa, JakForgeUi.Language);
            CmbTheme.SelectedIndex = System.Array.IndexOf(_tema, JakForgeUi.Theme);

            TerjemahkanSendiri();
        }

        /// <summary>
        /// Jendela ini ikut berbahasa yang sedang dipakai.
        ///
        /// Kalau tidak, orang yang sudah mengganti ke Inggris tetap membaca
        /// "Simpan" dan "Batal" di satu-satunya jendela yang seharusnya
        /// membuktikan pilihannya berhasil.
        /// </summary>
        private void TerjemahkanSendiri()
        {
            Title = JakForgeText.T("Tampilan");
            TxtTitle.Text = JakForgeText.T("Tampilan");
            TxtLead.Text = JakForgeText.T("Setelan ini dipakai bersama Studio dan JakRunner.");
            TxtLang.Text = JakForgeText.T("Bahasa");
            TxtTheme.Text = JakForgeText.T("Tema");
            BtnSave.Content = JakForgeText.T("Simpan");
            BtnCancel.Content = JakForgeText.T("Batal");
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var bahasa = _bahasa[CmbLang.SelectedIndex < 0 ? 0 : CmbLang.SelectedIndex];
            var tema = _tema[CmbTheme.SelectedIndex < 0 ? 0 : CmbTheme.SelectedIndex];

            JakForgeUi.Set(bahasa, tema);
            JakForgeTheming.Apply(tema);

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

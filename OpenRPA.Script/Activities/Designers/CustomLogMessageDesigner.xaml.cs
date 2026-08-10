using System.Activities.Presentation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

// Namespace ini HARUS SAMA dengan bagian depan x:Class di atas
namespace OpenRPA.Script.Activities.Designers
{
    // Kata 'partial' WAJIB ada di sini
    public partial class CustomLogMessageDesigner : ActivityDesigner
    {
        private bool _isExpanded = true;

        public CustomLogMessageDesigner()
        {
            InitializeComponent(); // Error ini akan hilang jika namespace dan partial sudah benar
        }

        // Dipanggil saat tombol chevron (panah collapse/expand) diklik.
        // Menyembunyikan / menampilkan bagian body (Log Level & Message)
        // dan memutar arah panah chevron.
        private void ToggleExpand_Click(object sender, RoutedEventArgs e)
        {
            _isExpanded = !_isExpanded;

            BodyPanel.Visibility = _isExpanded ? Visibility.Visible : Visibility.Collapsed;

            // Putar ikon chevron: menghadap ke atas saat expanded, ke bawah saat collapsed
            var rotate = new RotateTransform(_isExpanded ? 0 : 180);
            ChevronIcon.RenderTransform = rotate;
            ChevronIcon.RenderTransformOrigin = new Point(0.5, 0.5);
        }
    }
}

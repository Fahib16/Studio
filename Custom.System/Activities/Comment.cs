using System;
using System.Activities;
using System.ComponentModel;

namespace CustomSystem
{
    /// <summary>
    /// Catatan untuk pembaca workflow. Tidak melakukan apa pun saat runtime.
    ///
    /// Berbeda dari CommentOut milik OpenRPA yang MENONAKTIFKAN activity di
    /// dalamnya: yang ini murni teks, dipakai untuk menjelaskan langkah di
    /// sebelahnya.
    ///
    /// Tidak punya ContinueOnError karena Execute-nya kosong — tidak ada yang
    /// bisa gagal, jadi properti itu hanya akan jadi hiasan.
    /// </summary>
    [Designer(typeof(Design.CommentDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Comment")]
    [Description("Catatan bebas di canvas. Tidak dieksekusi.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.comment.png")]
    public sealed class Comment : CodeActivity
    {
        public Comment()
        {
            DisplayName = "Comment";
        }

        /// <summary>
        /// Plain string, bukan InArgument: isinya selalu teks tetap yang ditulis
        /// saat mendesain workflow, dan tidak pernah dievaluasi saat runtime.
        /// </summary>
        [Category("Input")]
        [DisplayName("Text")]
        [Description("Isi catatan.")]
        public string Text { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            // Sengaja kosong: activity ini hanya dokumentasi di canvas.
        }
    }
}

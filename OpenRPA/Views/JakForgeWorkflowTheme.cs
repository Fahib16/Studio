using System;
using System.Activities.Presentation;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using OpenRPA.Interfaces;

namespace OpenRPA.Views
{
    /// <summary>
    /// Menyelaraskan warna bagian kanvas yang digambar oleh perancang alur
    /// kerja .NET dengan tema JakForge.
    ///
    /// Kenapa tidak lewat ResourceDictionary seperti kontrol lain: warna
    /// perancang itu diambil dari properti statis WorkflowDesignerColors yang
    /// bertipe Color dan HANYA BISA DIBACA. Tidak ada kunci resource yang bisa
    /// ditimpa, jadi satu-satunya jalan yang tersedia ada dua:
    ///
    ///   1. Panel Properties: WorkflowDesigner.PropertyInspectorFontAndColorData
    ///      memang disediakan untuk keperluan ini, memakai kunci string
    ///      PropertyInspector*BrushKey.
    ///
    ///   2. Bilah bawah (Variables / Arguments / Imports) dan latar kanvas:
    ///      warnanya sudah terlanjur terpasang di pohon visual, jadi dicat
    ///      ulang setelah tampilan selesai dimuat. Pencocokan dilakukan
    ///      terhadap NILAI warna bawaan yang dibaca langsung dari
    ///      WorkflowDesignerColors, bukan terhadap angka yang ditulis ulang di
    ///      sini, sehingga tetap benar kalau .NET mengubah rona birunya.
    /// </summary>
    internal static class JakForgeWorkflowTheme
    {
        private static Color C(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }

        private static readonly Color Cream = C("#FAF7EE");
        private static readonly Color Surface = C("#FFFDF8");
        private static readonly Color Panel = C("#F5F0E6");
        private static readonly Color Sidebar = C("#E8DFCE");
        private static readonly Color Border = C("#E2D8C6");
        private static readonly Color Brown900 = C("#3E3226");
        private static readonly Color Brown700 = C("#4A3927");
        private static readonly Color Accent = C("#FFEBD3");
        private static readonly Color Tan = C("#B08046");

        /// <summary>Pasangan warna bawaan perancang dan penggantinya.</summary>
        private static Dictionary<Color, Color> Map()
        {
            var map = new Dictionary<Color, Color>();

            Action<Color, Color> add = (from, to) => { if (!map.ContainsKey(from)) map.Add(from, to); };

            add(WorkflowDesignerColors.DesignerViewShellBarColorGradientBeginColor, Panel);
            add(WorkflowDesignerColors.DesignerViewShellBarColorGradientEndColor, Sidebar);
            add(WorkflowDesignerColors.DesignerViewShellBarControlBackgroundColor, Sidebar);
            add(WorkflowDesignerColors.DesignerViewShellBarSelectedColorGradientBeginColor, Brown700);
            add(WorkflowDesignerColors.DesignerViewShellBarSelectedColorGradientEndColor, Brown700);
            add(WorkflowDesignerColors.DesignerViewShellBarHoverColorGradientBeginColor, Accent);
            add(WorkflowDesignerColors.DesignerViewShellBarHoverColorGradientEndColor, Accent);
            add(WorkflowDesignerColors.DesignerViewShellBarCaptionColor, Brown900);
            add(WorkflowDesignerColors.DesignerViewShellBarCaptionActiveColor, Cream);
            add(WorkflowDesignerColors.DesignerViewStatusBarBackgroundColor, Sidebar);

            // Warna kanvas dan kotak activity SENGAJA tidak dipetakan di sini.
            // Sebagian nilainya sama dengan warna bilah, dan karena pencocokan
            // memakai nilai warna, memetakannya akan ikut menggelapkan latar
            // kanvas. Kartu activity sudah bertema lewat XAML-nya sendiri.

            return map;
        }

        /// <summary>
        /// Warna panel Properties.
        ///
        /// Yang diminta PropertyInspectorFontAndColorData bukan sembarang XAML,
        /// melainkan XAML sebuah <see cref="System.Collections.Hashtable"/>.
        /// Menyerahkan ResourceDictionary — bentuk yang paling wajar dikira —
        /// membuat setter-nya gagal dengan "Unable to cast object of type
        /// 'System.Windows.ResourceDictionary' to type
        /// 'System.Collections.Hashtable'", dan karena kegagalannya ditangkap,
        /// panel Properties diam-diam tidak pernah ikut bertema.
        ///
        /// Kuncinya objek ComponentResourceKey milik WorkflowDesignerColors,
        /// jadi tabelnya diserialkan apa adanya lewat XamlWriter.
        /// </summary>
        public static void ApplyPropertyInspector(WorkflowDesigner designer)
        {
            if (designer == null) return;

            try
            {
                var table = new System.Collections.Hashtable
                {
                    { WorkflowDesignerColors.PropertyInspectorBackgroundBrushKey, new SolidColorBrush(Surface) },
                    { WorkflowDesignerColors.PropertyInspectorBorderBrushKey, new SolidColorBrush(Border) },
                    { WorkflowDesignerColors.PropertyInspectorTextBrushKey, new SolidColorBrush(Brown900) },
                    { WorkflowDesignerColors.PropertyInspectorCategoryCaptionTextBrushKey, new SolidColorBrush(Brown700) },
                    { WorkflowDesignerColors.PropertyInspectorPaneBrushKey, new SolidColorBrush(Panel) },
                    { WorkflowDesignerColors.PropertyInspectorPopupBrushKey, new SolidColorBrush(Surface) },
                    { WorkflowDesignerColors.PropertyInspectorSelectedBackgroundBrushKey, new SolidColorBrush(Brown700) },
                    { WorkflowDesignerColors.PropertyInspectorSelectedForegroundBrushKey, new SolidColorBrush(Cream) },
                    { WorkflowDesignerColors.PropertyInspectorToolBarBackgroundBrushKey, new SolidColorBrush(Sidebar) },
                    { WorkflowDesignerColors.PropertyInspectorToolBarItemHoverBackgroundBrushKey, new SolidColorBrush(Accent) },
                    { WorkflowDesignerColors.PropertyInspectorToolBarItemHoverBorderBrushKey, new SolidColorBrush(Tan) },
                    { WorkflowDesignerColors.PropertyInspectorToolBarItemSelectedBackgroundBrushKey, new SolidColorBrush(Accent) },
                    { WorkflowDesignerColors.PropertyInspectorToolBarItemSelectedBorderBrushKey, new SolidColorBrush(Tan) },
                    { WorkflowDesignerColors.PropertyInspectorToolBarSeparatorBrushKey, new SolidColorBrush(Border) },
                    { WorkflowDesignerColors.PropertyInspectorToolBarTextBoxBorderBrushKey, new SolidColorBrush(Border) },
                };

                designer.PropertyInspectorFontAndColorData = XamlWriter.Save(table);
            }
            catch (Exception ex)
            {
                Log.Error("JakForge property inspector: " + ex.ToString());
            }
        }
        ///
        /// Pengecatan SENGAJA dibatasi pada bilah itu saja. Pencocokan
        /// dilakukan berdasarkan nilai warna, dan beberapa warna bawaan dipakai
        /// juga oleh latar kanvas; kalau seluruh pohon ikut dicat, kanvasnya
        /// ikut berubah gelap. Bilah dikenali dari isinya sendiri: satu-satunya
        /// wadah yang memuat tulisan "Variables" sekaligus "Imports".
        /// </summary>
        public static void Repaint(DependencyObject root)
        {
            if (root == null) return;

            try
            {
                var bar = FindShellBar(root);
                if (bar == null)
                {
                    Log.Debug("JakForge repaint: bilah bawah perancang tidak ditemukan, dilewati.");
                    return;
                }

                Walk(bar, Map());
            }
            catch (Exception ex)
            {
                Log.Debug("JakForge repaint: " + ex.Message);
            }
        }

        /// <summary>
        /// Wadah TERKECIL yang memuat tulisan "Variables" dan "Imports"
        /// sekaligus. Dicari dari bawah ke atas supaya yang terpilih benar-benar
        /// bilahnya, bukan seluruh tampilan perancang.
        /// </summary>
        private static DependencyObject FindShellBar(DependencyObject root)
        {
            var variables = FindText(root, "Variables");
            if (variables == null) return null;

            DependencyObject node = variables;
            while (node != null)
            {
                if (FindText(node, "Imports") != null && FindText(node, "Arguments") != null) return node;
                node = VisualTreeHelper.GetParent(node);
            }
            return null;
        }

        private static DependencyObject FindText(DependencyObject node, string text)
        {
            if (node == null) return null;

            var block = node as TextBlock;
            if (block != null && string.Equals(block.Text, text, StringComparison.Ordinal)) return block;

            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++)
            {
                var found = FindText(VisualTreeHelper.GetChild(node, i), text);
                if (found != null) return found;
            }
            return null;
        }

        private static void Walk(DependencyObject node, Dictionary<Color, Color> map)
        {
            if (node == null) return;

            var panel = node as System.Windows.Controls.Panel;
            if (panel != null) panel.Background = Swap(panel.Background, map);

            var border = node as System.Windows.Controls.Border;
            if (border != null)
            {
                border.Background = Swap(border.Background, map);
                border.BorderBrush = Swap(border.BorderBrush, map);
            }

            var control = node as Control;
            if (control != null)
            {
                control.Background = Swap(control.Background, map);
                control.BorderBrush = Swap(control.BorderBrush, map);
                control.Foreground = Swap(control.Foreground, map);
            }

            var text = node as TextBlock;
            if (text != null) text.Foreground = Swap(text.Foreground, map);

            var shape = node as Shape;
            if (shape != null)
            {
                shape.Fill = Swap(shape.Fill, map);
                shape.Stroke = Swap(shape.Stroke, map);
            }

            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++) Walk(VisualTreeHelper.GetChild(node, i), map);
        }

        /// <summary>
        /// Menukar kuas kalau warnanya termasuk warna bawaan perancang.
        /// Kuas lain dikembalikan apa adanya, jadi warna yang memang sudah
        /// bertema JakForge tidak ikut tersentuh.
        /// </summary>
        private static Brush Swap(Brush brush, Dictionary<Color, Color> map)
        {
            var solid = brush as SolidColorBrush;
            if (solid != null)
            {
                Color replacement;
                if (map.TryGetValue(solid.Color, out replacement)) return new SolidColorBrush(replacement);
                return brush;
            }

            var gradient = brush as GradientBrush;
            if (gradient != null)
            {
                var touched = false;
                foreach (var stop in gradient.GradientStops)
                {
                    if (map.ContainsKey(stop.Color)) { touched = true; break; }
                }
                if (!touched) return brush;

                var clone = gradient.Clone();
                foreach (var stop in clone.GradientStops)
                {
                    Color replacement;
                    if (map.TryGetValue(stop.Color, out replacement)) stop.Color = replacement;
                }
                clone.Freeze();
                return clone;
            }

            return brush;
        }
    }
}

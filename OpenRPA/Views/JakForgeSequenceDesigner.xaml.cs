using System;
using System.Activities;
using System.Activities.Presentation;
using System.Activities.Presentation.Model;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OpenRPA.Interfaces;

namespace OpenRPA.Views
{
    /// <summary>
    /// Designer Sequence versi JakForge, lengkap dengan tombol penyisip "+"
    /// di antara activity.
    /// </summary>
    public partial class JakForgeSequenceDesigner : ActivityDesigner
    {
        public JakForgeSequenceDesigner()
        {
            InitializeComponent();
        }

        private void InsertHere_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            var button = sender as Button;
            if (button == null) return;

            try
            {
                var index = IndexOfSpacer(button);
                if (index < 0) return;

                var type = JakForgeActivityPicker.Pick(Window.GetWindow(this));
                if (type == null) return;

                var instance = CreateActivity(type);
                if (instance == null) return;

                var collection = ModelItem.Properties["Activities"].Collection;
                if (index > collection.Count) index = collection.Count;
                collection.Insert(index, instance);
            }
            catch (Exception ex)
            {
                Log.Error("JakForge sisip activity: " + ex.ToString());
            }
        }

        /// <summary>
        /// Menghitung posisi sisip dari tombol "+" yang diklik.
        ///
        /// Versi sebelumnya naik dari tombol sampai menemukan Panel PERTAMA,
        /// lalu menghitung activity sebelum posisi itu. Panel pertama yang
        /// ditemui ternyata Grid milik template tombolnya sendiri — bukan panel
        /// daftar isi Sequence — sehingga hitungannya SELALU 0 dan setiap "+"
        /// menyisipkan di paling atas, di mana pun tombolnya diklik.
        ///
        /// Sekarang naiknya diteruskan sampai WorkflowItemsPresenter, dan panel
        /// TERAKHIR sebelum itu yang dipakai. Posisinya pun tidak lagi dihitung
        /// dari jumlah anak panel, melainkan dari ModelItem activity pertama
        /// yang berada SESUDAH penyisip — jadi tetap benar walaupun susunan
        /// visual WF Designer berubah.
        /// </summary>
        private int IndexOfSpacer(DependencyObject button)
        {
            DependencyObject node = button;
            Panel itemsPanel = null;
            DependencyObject spacer = null;

            while (node != null)
            {
                var parent = VisualTreeHelper.GetParent(node);
                if (parent == null) break;
                if (parent is WorkflowItemsPresenter) break;

                var panel = parent as Panel;
                if (panel != null) { itemsPanel = panel; spacer = node; }

                node = parent;
            }

            if (itemsPanel == null || spacer == null) return -1;

            var position = itemsPanel.Children.IndexOf(spacer as System.Windows.UIElement);
            if (position < 0) return -1;

            var collection = ModelItem.Properties["Activities"].Collection;

            for (var i = position + 1; i < itemsPanel.Children.Count; i++)
            {
                var view = FindActivityView(itemsPanel.Children[i]);
                if (view == null) continue;

                var at = IndexInCollection(collection, view.ModelItem);
                if (at >= 0) return at;
            }

            // Tidak ada activity sesudahnya: ini penyisip paling bawah.
            return collection.Count;
        }

        /// <summary>Kartu activity pertama di dalam sebuah simpul visual.</summary>
        private static WorkflowViewElement FindActivityView(DependencyObject node)
        {
            if (node == null) return null;

            var view = node as WorkflowViewElement;
            if (view != null) return view;

            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++)
            {
                var found = FindActivityView(VisualTreeHelper.GetChild(node, i));
                if (found != null) return found;
            }
            return null;
        }

        private static int IndexInCollection(ModelItemCollection collection, ModelItem item)
        {
            if (collection == null || item == null) return -1;

            for (var i = 0; i < collection.Count; i++)
            {
                if (ReferenceEquals(collection[i], item)) return i;
            }
            return -1;
        }

        /// <summary>
        /// Membuat activity dari tipenya. Sebagian entri toolbox bukan activity
        /// melainkan pabrik template (IActivityTemplateFactory), mis. ForEach,
        /// yang harus dipanggil Create()-nya supaya isinya ikut terbentuk.
        /// </summary>
        internal static object CreateActivity(Type type)
        {
            if (type == null) return null;

            var instance = Activator.CreateInstance(type);

            var factory = instance as IActivityTemplateFactory;
            if (factory != null) return factory.Create(null);

            return instance;
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.AutomationElements.Infrastructure;

namespace Custom.StudioBridge.Design
{
    /// <summary>
    /// Satu node di Visual Tree untuk aplikasi desktop.
    ///
    /// Nama properti (Display, Children, IsExpanded, IsSelected) SENGAJA
    /// disamakan dengan DomNode milik sisi web: TreeView di UI Explorer
    /// mengikat berdasarkan nama properti, jadi satu template yang sama bisa
    /// menampilkan pohon DOM maupun pohon UI Automation tanpa perlu panel
    /// terpisah.
    /// </summary>
    public class DesktopTreeNode : INotifyPropertyChanged
    {
        public AutomationElement Element { get; set; }
        public DesktopTreeNode Parent { get; set; }
        public List<DesktopTreeNode> Children { get; } = new List<DesktopTreeNode>();

        public string ControlType { get; set; }
        public string Name { get; set; }
        public string AutomationId { get; set; }
        public string ClassName { get; set; }

        public string Display
        {
            get
            {
                var sb = new StringBuilder(string.IsNullOrEmpty(ControlType) ? "?" : ControlType);
                if (!string.IsNullOrEmpty(Name)) sb.Append("  \"").Append(Name).Append("\"");
                else if (!string.IsNullOrEmpty(AutomationId)) sb.Append("  #").Append(AutomationId);
                else if (!string.IsNullOrEmpty(ClassName)) sb.Append("  .").Append(ClassName);
                return sb.ToString();
            }
        }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get { return _isExpanded; }
            set { _isExpanded = value; Notify("IsExpanded"); }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get { return _isSelected; }
            set { _isSelected = value; Notify("IsSelected"); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify(string p)
        {
            var h = PropertyChanged;
            if (h != null) h(this, new PropertyChangedEventArgs(p));
        }

        public void ExpandAncestors()
        {
            var cur = Parent;
            while (cur != null) { cur.IsExpanded = true; cur = cur.Parent; }
        }

        /// <summary>
        /// Cari node yang membungkus elemen tertentu, untuk menyorot elemen
        /// yang sedang dipakai activity begitu pohon selesai dimuat.
        /// </summary>
        public DesktopTreeNode Find(AutomationElement target)
        {
            if (target == null) return null;

            try { if (Element != null && Element.Equals(target)) return this; }
            catch (Exception) { }

            foreach (var c in Children)
            {
                var hit = c.Find(target);
                if (hit != null) return hit;
            }

            return null;
        }
    }
}

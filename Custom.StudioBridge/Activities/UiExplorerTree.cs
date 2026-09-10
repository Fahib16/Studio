using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Custom.StudioBridge.Design
{
    /// <summary>
    /// Satu node di panel Visual Tree. Parent disimpan supaya jalur ke atas
    /// bisa ditelusuri tanpa bertanya lagi ke browser.
    /// </summary>
    public class DomNode : INotifyPropertyChanged
    {
        public string Tag { get; set; }
        public string Id { get; set; }
        public string Class { get; set; }
        public string Name { get; set; }
        public string Text { get; set; }
        public int NthOfType { get; set; }

        public DomNode Parent { get; set; }
        public List<DomNode> Children { get; } = new List<DomNode>();

        public string Display
        {
            get
            {
                var sb = new StringBuilder(Tag);
                if (!string.IsNullOrEmpty(Id)) sb.Append(" #").Append(Id);
                else if (!string.IsNullOrEmpty(Name)) sb.Append(" [").Append(Name).Append("]");
                if (!string.IsNullOrEmpty(Text)) sb.Append("  \"").Append(Text).Append("\"");
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
        /// Susun path CSS ke node ini. Berhenti pada leluhur ber-id: id sudah
        /// unik, jadi meneruskan ke atas hanya memperpanjang path.
        /// </summary>
        public string BuildPathSelector()
        {
            var parts = new List<string>();
            var cur = this;

            while (cur != null && !string.Equals(cur.Tag, "body", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(cur.Id)) { parts.Insert(0, "#" + cur.Id); break; }

                var part = cur.Tag;
                if (cur.NthOfType > 0) part += ":nth-of-type(" + cur.NthOfType + ")";
                parts.Insert(0, part);

                cur = cur.Parent;
            }

            if (parts.Count == 0) return "body";
            if (!parts[0].StartsWith("#")) parts.Insert(0, "body");
            return string.Join(" > ", parts);
        }

        public static DomNode FromJson(JToken json, DomNode parent = null)
        {
            if (json == null) return null;

            var node = new DomNode
            {
                Tag = json["t"]?.Value<string>() ?? "?",
                Id = json["i"]?.Value<string>(),
                Class = json["c"]?.Value<string>(),
                Name = json["n"]?.Value<string>(),
                Text = json["x"]?.Value<string>(),
                NthOfType = json["k"]?.Value<int>() ?? 0,
                Parent = parent
            };

            var children = json["ch"] as JArray;
            if (children != null)
                foreach (var c in children)
                {
                    var child = FromJson(c, node);
                    if (child != null) node.Children.Add(child);
                }

            return node;
        }

        public DomNode Descend(IEnumerable<int> path)
        {
            var cur = this;
            foreach (var idx in path)
            {
                if (cur == null || idx < 0 || idx >= cur.Children.Count) return null;
                cur = cur.Children[idx];
            }
            return cur;
        }
    }
}

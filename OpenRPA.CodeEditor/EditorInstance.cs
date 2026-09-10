using OpenRPA.Interfaces;
using System;
using System.Activities.Presentation.View;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace OpenRPA.CodeEditor
{
    public class EditorInstance : CodeEditor, IExpressionEditorInstance
    {
        // public new event EventHandler TextChanged;
        public event EventHandler LostAggregateFocus;
        public event EventHandler GotAggregateFocus;
        public event EventHandler Closing;
        public Type expressionType = null;
        public IDesigner designer = null;
        /// <summary>
        /// Creates a new instance of the <see cref="EditorInstance" /> class.
        /// </summary>
        public EditorInstance()
        {
            Id = Guid.NewGuid();
            var lang = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.HighlightingDefinitions.Where(x => x.Name == "VB").FirstOrDefault();
            SyntaxHighlighting = lang;
            GotFocus += Editor_GotFocus;
            LostFocus += Editor_LostFocus;
            Unloaded += Editor_Unloaded;
            IsKeyboardFocusWithinChanged += EditorInstance_IsKeyboardFocusWithinChanged;
            KeyUp += Editor_KeyUp;
        }
        /// <summary>
        /// Ctrl+K: buat variabel baru untuk kotak ekspresi ini.
        ///
        /// Dua hal yang diperbaiki dari versi sebelumnya:
        ///
        /// 1. TIPE. Dulu selalu jatuh ke String setiap kali expressionType
        ///    kosong — dan itu terjadi pada HAMPIR SEMUA kotak, karena designer
        ///    kita tidak menyetel ExpressionType di XAML-nya. Akibatnya kotak
        ///    keluaran Read Range Workbook pun menghasilkan variabel String,
        ///    bukan DataTable. Sekarang tipenya disimpulkan dari properti
        ///    activity yang sedang disunting.
        ///
        /// 2. URUTAN. Dulu nama harus diketik lebih dulu ke dalam kotak, baru
        ///    Ctrl+K ditekan. Sekarang Ctrl+K yang lebih dulu, lalu namanya
        ///    ditanyakan lewat kotak kecil yang sekalian menampilkan tipenya.
        /// </summary>
        private void Editor_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.KeyboardDevice.Modifiers != System.Windows.Input.ModifierKeys.Control || e.Key != System.Windows.Input.Key.K) return;
            if (designer == null) return;

            e.Handled = true;

            try
            {
                var type = ResolveVariableType();
                var name = VariableNameWindow.Ask(type, SuggestName(Text, type));
                if (string.IsNullOrEmpty(name)) return;

                // Kalau namanya ternyata argumen workflow, itu yang dipakai —
                // membuat variabel bernama sama hanya akan saling menutupi.
                var arg = designer.GetArgument(name, false, type);
                if (arg == null) designer.GetVariable(name, type);

                Text = name;
                CaretOffset = Text.Length;
            }
            catch (Exception ex)
            {
                Log.Error("Ctrl+K: " + ex.ToString());
            }
        }

        /// <summary>
        /// Tipe yang seharusnya dipakai variabel baru.
        ///
        /// Urutan sumbernya: expressionType yang diberikan WF, lalu properti
        /// activity yang sedang disunting (dibaca dari binding kotaknya), lalu
        /// ExpressionType yang disetel designer. String hanya dipakai kalau
        /// benar-benar tidak ada satu pun yang bisa disimpulkan.
        /// </summary>
        private Type ResolveVariableType()
        {
            var direct = Unwrap(expressionType);
            if (direct != null && direct != typeof(object)) return direct;

            var box = FindExpressionTextBox();
            if (box != null)
            {
                var fromBinding = TypeFromBinding(box);
                if (fromBinding != null) return fromBinding;

                var declared = Unwrap(box.ExpressionType);
                if (declared != null && declared != typeof(object)) return declared;
            }

            return direct ?? typeof(string);
        }

        private ExpressionTextBox FindExpressionTextBox()
        {
            DependencyObject node = this;
            var hops = 0;

            while (node != null && hops++ < 40)
            {
                var box = node as ExpressionTextBox;
                if (box != null) return box;

                var parent = System.Windows.Media.VisualTreeHelper.GetParent(node);
                if (parent == null)
                {
                    var element = node as FrameworkElement;
                    parent = element != null ? element.Parent : null;
                }
                node = parent;
            }

            return null;
        }

        /// <summary>
        /// Tipe properti activity yang di-bind ke kotak ini.
        ///
        /// Semua designer kita memakai bentuk Path=ModelItem.NamaProperti, jadi
        /// nama properti diambil dari potongan terakhir path itu lalu dicari di
        /// tipe activity-nya lewat refleksi.
        /// </summary>
        private static Type TypeFromBinding(ExpressionTextBox box)
        {
            try
            {
                var binding = System.Windows.Data.BindingOperations.GetBinding(box, ExpressionTextBox.ExpressionProperty);
                if (binding == null || binding.Path == null) return null;

                var path = binding.Path.Path;
                if (string.IsNullOrEmpty(path)) return null;

                var dot = path.LastIndexOf('.');
                var propertyName = dot >= 0 ? path.Substring(dot + 1) : path;
                if (string.IsNullOrEmpty(propertyName)) return null;

                var owner = box.OwnerActivity;
                if (owner == null) return null;

                var property = owner.ItemType.GetProperty(propertyName);
                if (property == null) return null;

                return Unwrap(property.PropertyType);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Lepaskan pembungkus WF sampai ketemu tipe nilainya: InArgument(Of T),
        /// OutArgument(Of T), Location(Of T), dan Activity(Of T) semuanya
        /// membungkus T yang sesungguhnya.
        /// </summary>
        private static Type Unwrap(Type type)
        {
            if (type == null) return null;

            if (type.IsGenericType)
            {
                var definition = type.GetGenericTypeDefinition();
                if (definition == typeof(System.Activities.InArgument<>)
                    || definition == typeof(System.Activities.OutArgument<>)
                    || definition == typeof(System.Activities.InOutArgument<>)
                    || definition == typeof(System.Activities.Location<>)
                    || definition == typeof(System.Activities.Activity<>))
                {
                    return Unwrap(type.GetGenericArguments()[0]);
                }
            }

            return type;
        }

        /// <summary>
        /// Nama usulan: isi kotak kalau sudah berupa nama yang sah, kalau tidak
        /// diturunkan dari nama tipenya (DataTable menjadi dataTable).
        /// </summary>
        private static string SuggestName(string current, Type type)
        {
            current = (current ?? "").Trim();

            if (current.Length > 0 && (char.IsLetter(current[0]) || current[0] == '_'))
            {
                var ok = true;
                foreach (var c in current)
                {
                    if (!char.IsLetterOrDigit(c) && c != '_') { ok = false; break; }
                }
                if (ok) return current;
            }

            var name = type != null ? type.Name : "nilai";
            var tick = name.IndexOf('`');
            if (tick > 0) name = name.Substring(0, tick);

            return name.Length > 0 ? char.ToLowerInvariant(name[0]) + name.Substring(1) : "nilai";
        }
        private void EditorInstance_IsKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsKeyboardFocusWithin)
            {
                // Dispatch to ensure that the visual is available before trying to move focus to the view
                Dispatcher.BeginInvoke(DispatcherPriority.Send, (DispatcherOperationCallback)delegate (object arg) {
                    if (!TextArea.IsKeyboardFocusWithin)
                    {
                        try
                        {
                            //TextArea.Focus();
                            //Focus();
                            //(sender as ICSharpCode.AvalonEdit.TextEditor).TextArea.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next));
                            TextArea.Focus();
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex.ToString());
                        }
                    }
                    return null;
                }, null);

                // Raise an event
                GotAggregateFocus?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                try
                {
                    // LostAggregateFocus?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    Log.Debug(ex.ToString());
                }
                // Raise an event
            }
        }
        private void Editor_Unloaded(object sender, RoutedEventArgs e)
        {
            Closing?.Invoke(sender, EventArgs.Empty);
        }
        private void Editor_GotFocus(object sender, RoutedEventArgs e)
        {
            GotAggregateFocus?.Invoke(sender, e);
        }
        /// <summary>
        /// Gets the unique ID of the editor instance.
        /// </summary>
        public Guid Id { get; private set; }
        public System.Windows.Controls.Control HostControl
        {
            get { return this; }
        }
        public int MinLines
        {
            get { return 1; }
            set { _ = value; }
        }
        public int MaxLines
        {
            get { return 10; }
            set { _ = value; }
        }
        public bool HasAggregateFocus => true;
        public bool AcceptsReturn
        {
            get { return true; }
            set { _ = value; }
        }
        public bool AcceptsTab
        {
            get { 
                return true; 
            }
            set { _ = value; }
        }
        private void Editor_LostFocus(object sender, RoutedEventArgs e)
        {
            LostAggregateFocus?.Invoke(sender, EventArgs.Empty);
        }
        public void Close()
        {
        }
        public void ClearSelection()
        {
        }
        public bool CompleteWord()
        {
            return true;
        }
        public bool GlobalIntellisense()
        {
            return false;
        }
        public bool ParameterInfo()
        {
            return true;
        }
        public bool QuickInfo()
        {
            return true;
        }
        public bool IncreaseFilterLevel()
        {
            return false;
        }
        public bool DecreaseFilterLevel()
        {
            return false;
        }
        public bool CanCut()
        {
            return true;
        }
        public bool CanCopy()
        {
            return true;
        }
        public bool CanPaste()
        {
            return true;
        }
        public bool CanCompleteWord()
        {
            return true;
        }
        public bool CanGlobalIntellisense()
        {
            return false;
        }
        public bool CanParameterInfo()
        {
            return false;
        }
        public bool CanQuickInfo()
        {
            return false;
        }
        public bool CanIncreaseFilterLevel()
        {
            return false;
        }
        public bool CanDecreaseFilterLevel()
        {
            return false;
        }
        public string GetCommittedText()
        {
            return Text;
        }

        void IExpressionEditorInstance.Focus()
        {
            try
            {
                // Dispatch to ensure that the visual is available before trying to move focus to the view
                Dispatcher.BeginInvoke(DispatcherPriority.Send, (DispatcherOperationCallback)delegate (object arg) {
                    if (!TextArea.IsKeyboardFocusWithin)
                    {
                        try
                        {
                            //TextArea.Focus();
                            //Focus();
                            //TextArea.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next));
                            TextArea.Focus();
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex.ToString());
                        }
                    }
                    return null;
                }, null);

                // Raise an event
                GotAggregateFocus?.Invoke(this, EventArgs.Empty);

            }
            catch (Exception)
            {
                throw;
            }            
        }

        bool IExpressionEditorInstance.Cut()
        {
            Cut();
            return true;
        }

        bool IExpressionEditorInstance.Copy()
        {
            Copy();
            return true;
        }

        bool IExpressionEditorInstance.Paste()
        {
            Paste();
            return true;
        }

        bool IExpressionEditorInstance.CanUndo()
        {
            return CanUndo;
        }

        bool IExpressionEditorInstance.CanRedo()
        {
            return CanRedo;
        }
    }
}

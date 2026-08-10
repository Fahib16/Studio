using System;
using System.Activities;
using OpenRPA.Interfaces;
using OpenRPA.Windows;
using OpenRPA.NM;
using System.Activities.Presentation.PropertyEditing;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenRPA.Activities
{
    public enum TargetTechnology
    {
        Windows,
        Web
    }

    [Designer(typeof(TypeIntoDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [System.Drawing.ToolboxBitmap(typeof(TypeInto), "Resources.toolbox.typeinto.png")]
    [LocalizedToolboxTooltip("activity_typeinto_tooltip", typeof(Resources.strings))]
    [LocalizedDisplayName("activity_typeinto", typeof(Resources.strings))]
    [LocalizedHelpURL("activity_typeinto_helpurl", typeof(Resources.strings))]
    public class TypeInto : CodeActivity
    {
        public TypeInto()
        {
            EmptyField = true;
            ClickBeforeTyping = true;
            SimulateKeystrokes = false;
            DelayBetweenKeysMs = 10;
            PostWait = Config.local.use_postwait;
            Technology = TargetTechnology.Windows;
        }

        // ================= TARGET =================
        [Category("Target")]
        [DisplayName("Technology")]
        [Description("Pilih apakah target elemen berada di aplikasi Desktop (Windows) atau Browser (Web/NM)")]
        public TargetTechnology Technology { get; set; }

        [Category("Target")]
        [LocalizedDisplayName("activity_selector", typeof(Resources.strings)), LocalizedDescription("activity_selector_help", typeof(Resources.strings))]
        public InArgument<string> Selector { get; set; }

        [Category("Target")]
        [LocalizedDisplayName("activity_element", typeof(Resources.strings)), LocalizedDescription("activity_element_help", typeof(Resources.strings))]
        public InArgument<IElement> Element { get; set; }

        [Browsable(false)]
        public string Image { get; set; }

        // ================= INPUT =================
        [Category("Input")]
        [RequiredArgument, LocalizedDisplayName("activity_text", typeof(Resources.strings)), LocalizedDescription("activity_text_help", typeof(Resources.strings))]
        public InArgument<string> Text { get; set; }

        // ================= OPTIONS =================
        [Category("Options")]
        [RequiredArgument, LocalizedDisplayName("activity_emptyfield", typeof(Resources.strings)), LocalizedDescription("activity_emptyfield_help", typeof(Resources.strings))]
        public InArgument<bool> EmptyField { get; set; } = true;

        [Category("Options")]
        [RequiredArgument, LocalizedDisplayName("activity_clickbeforetyping", typeof(Resources.strings)), LocalizedDescription("activity_clickbeforetyping_help", typeof(Resources.strings))]
        public InArgument<bool> ClickBeforeTyping { get; set; } = true;

        [Category("Options")]
        [LocalizedDisplayName("activity_simulatekeystrokes", typeof(Resources.strings)), LocalizedDescription("activity_simulatekeystrokes_help", typeof(Resources.strings))]
        public InArgument<bool> SimulateKeystrokes { get; set; } = false;

        [Category("Options")]
        [LocalizedDisplayName("activity_delaybetweenkeys", typeof(Resources.strings)), LocalizedDescription("activity_delaybetweenkeys_help", typeof(Resources.strings))]
        public InArgument<int> DelayBetweenKeysMs { get; set; } = 10;

        [Category("Options")]
        [LocalizedDisplayName("activity_postwait", typeof(Resources.strings)), LocalizedDescription("activity_postwait_help", typeof(Resources.strings))]
        public InArgument<TimeSpan> PostWait { get; set; }

        [Category("Options")]
        [Editor(typeof(KeyModifiersOptionsEditor), typeof(System.Activities.Presentation.PropertyEditing.ExtendedPropertyValueEditor))]
        [LocalizedDisplayName("activity_keymodifiers", typeof(Resources.strings)), LocalizedDescription("activity_keymodifiers_help", typeof(Resources.strings))]
        public InArgument<string> KeyModifiers { get; set; }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            if (Technology == TargetTechnology.Windows)
            {
                metadata.RequireExtension<WindowsCacheExtension>();
            }
            base.CacheMetadata(metadata);
        }

        protected override void Execute(CodeActivityContext context)
        {
            var selectorStr = Selector != null ? Selector.Get(context) : null;
            IElement el;

            if (!string.IsNullOrEmpty(selectorStr))
            {
                selectorStr = OpenRPA.Interfaces.Selector.Selector.ReplaceVariables(selectorStr, context.DataContext);

                if (Technology == TargetTechnology.Web)
                {
                    var sel = new NMSelector(selectorStr);
                    var elements = NMSelector.GetElementsWithuiSelector(sel, null, 1);
                    if (elements == null || elements.Length == 0)
                        throw new ElementNotFoundException("TypeInto: Elemen Web tidak ditemukan untuk Selector yang diisi");
                    el = elements[0];
                }
                else
                {
                    var sel = new WindowsSelector(selectorStr);
                    var ext = context.GetExtension<WindowsCacheExtension>();
                    var elements = WindowsSelector.GetElementsWithuiSelector(sel, null, 1, ext);
                    if (elements == null || elements.Length == 0)
                        throw new ElementNotFoundException("TypeInto: Elemen Windows tidak ditemukan untuk Selector yang diisi");
                    el = elements[0];
                }
            }
            else
            {
                el = Element.Get(context);
                if (el == null) throw new ArgumentException("Element atau Selector harus diisi salah satu");
            }

            var text = Text.Get(context) ?? string.Empty;

            var emptyField = true;
            if (EmptyField != null) emptyField = EmptyField.Get(context);
            var clickBeforeTyping = true;
            if (ClickBeforeTyping != null) clickBeforeTyping = ClickBeforeTyping.Get(context);
            var simulateKeystrokes = false;
            if (SimulateKeystrokes != null) simulateKeystrokes = SimulateKeystrokes.Get(context);
            var delay = 10;
            if (DelayBetweenKeysMs != null) delay = DelayBetweenKeysMs.Get(context);
            var keymodifiers = "";
            if (KeyModifiers != null) keymodifiers = KeyModifiers.Get(context);

            Log.Selector(string.Format("Activities.TypeInto::begin, element={0}", el));

            if (clickBeforeTyping)
            {
                el.Click(true, Input.MouseButton.Left, 5, 5, false, false);
            }
            el.Focus();
            el.Refresh();

            if (emptyField)
            {
                el.Value = string.Empty;
            }

            if (simulateKeystrokes && Technology == TargetTechnology.Windows)
            {
                var disposes = new List<IDisposable>();
                var keys = TypeText.GetKeys(keymodifiers);
                foreach (var vk in keys) disposes.Add(FlaUI.Core.Input.Keyboard.Pressing(vk));

                TypeText.TypeString(text, TimeSpan.FromMilliseconds(delay));

                disposes.ForEach(x => { x.Dispose(); });
            }
            else
            {
                el.Value = text;
            }

            TimeSpan postwait = TimeSpan.Zero;
            if (PostWait != null) { postwait = PostWait.Get(context); }
            if (postwait != TimeSpan.Zero)
            {
                System.Threading.Thread.Sleep(postwait);
            }

            Log.Selector("Activities.TypeInto::end");
        }

        [Browsable(false)]
        public new string DisplayName
        {
            get
            {
                var displayName = base.DisplayName;
                if (displayName == this.GetType().Name)
                {
                    var displayNameAttribute = this.GetType().GetCustomAttributes(typeof(DisplayNameAttribute), true).FirstOrDefault() as DisplayNameAttribute;
                    if (displayNameAttribute != null) displayName = displayNameAttribute.DisplayName;
                }
                return displayName;
            }
            set
            {
                base.DisplayName = value;
            }
        }
    }
}
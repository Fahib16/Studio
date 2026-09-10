using System;
using System.Activities;
using OpenRPA.Interfaces;
using System.Activities.Presentation.PropertyEditing;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenRPA.Interfaces.Input;

namespace OpenRPA.Activities
{
    [System.ComponentModel.Designer(typeof(TypeTextDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.toolbox.typetext.png")]
    [LocalizedToolboxTooltip("activity_typetext_tooltip", typeof(Resources.strings))]
    [LocalizedDisplayName("activity_typetext", typeof(Resources.strings))]
    [LocalizedHelpURL("activity_typetext_helpurl", typeof(Resources.strings))]
    public class TypeText : CodeActivity
    {
        [RequiredArgument, LocalizedDisplayName("activity_text", typeof(Resources.strings)), LocalizedDescription("activity_displayname_help", typeof(Resources.strings))]
        public InArgument<string> Text { get; set; }
        [LocalizedDisplayName("activity_clickdelay", typeof(Resources.strings)), LocalizedDescription("activity_clickdelay_help", typeof(Resources.strings))]
        public InArgument<TimeSpan> ClickDelay { get; set; }
        protected override void Execute(CodeActivityContext context)
        {
            var text = Text.Get<string>(context);
            if (string.IsNullOrEmpty(text)) return;

            var clickdelay = ClickDelay.Get(context);
            var predelay = TimeSpan.FromMilliseconds(0);
            var postdelay = TimeSpan.FromMilliseconds(100);
            System.Threading.Thread.Sleep(predelay);

            TypeString(text, clickdelay);

            System.Threading.Thread.Sleep(postdelay);
        }

        /// <summary>
        /// Logika inti pengetikan (parser escape code + FlaUI keyboard events),
        /// diekstrak dari Execute() supaya bisa dipakai ulang oleh activity lain
        /// (mis. TypeInto) tanpa butuh CodeActivityContext.
        /// </summary>
        public static void TypeString(string text, TimeSpan clickdelay)
        {
            // Implementasinya DIPINDAHKAN ke OpenRPA.Interfaces.KeyboardInput
            // supaya activity di project Custom.* bisa memakainya juga —
            // project itu tidak boleh mereferensi project OpenRPA (referensi
            // melingkar). Method ini dibiarkan ada dan meneruskan, jadi
            // seluruh pemanggil lama tetap jalan tanpa diubah.
            OpenRPA.Interfaces.KeyboardInput.TypeString(text, clickdelay);
        }

        public static List<FlaUI.Core.WindowsAPI.VirtualKeyShort> GetKeys(string text)
        {
            return OpenRPA.Interfaces.KeyboardInput.GetKeys(text);
        }
        internal List<vKey> _keys = new List<vKey>();
        internal string result;
        private List<vKey> _downkeys = new List<vKey>();
        public int keysdown
        {
            get
            {
                return _downkeys.Count;
            }
        }
        public void AddKey(vKey _key, System.Activities.Presentation.Model.ModelItem lastinsertedmodel)
        {
            if (_keys == null) _keys = new List<vKey>();
            if (!_key.up)
            {
                var isdown = _downkeys.Where(x => x.KeyCode == _key.KeyCode).FirstOrDefault();
                if (isdown != null) return;
                _downkeys.Add(_key);
            }
            else
            {
                var isdown = _downkeys.Where(x => x.KeyCode == _key.KeyCode).FirstOrDefault();
                if (isdown == null) return;
                _downkeys.Remove(isdown);

            }

            _keys.Add(_key);
            result = "";
            for (var i = 0; i < _keys.Count; i++)
            {
                string val = "";
                var key = _keys[i];
                if (key.up == false && (i + 1) < _keys.Count)
                {
                    if (key.KeyCode == _keys[i + 1].KeyCode && _keys[i + 1].up)
                    {
                        i++;
                        val = "{" + key.KeyCode.ToString() + "}";
                        if (key.KeyCode.ToString().StartsWith("KEY_"))
                        {
                            val = key.KeyCode.ToString().Substring(4).ToLower();
                        }
                        if (key.KeyCode == FlaUI.Core.WindowsAPI.VirtualKeyShort.SPACE)
                        {
                            val = " ";
                        }
                    }
                }
                if (string.IsNullOrEmpty(val))
                {
                    if (key.up == false)
                    {
                        val = "{" + key.KeyCode.ToString() + " down}";
                    }
                    else
                    {
                        val = "{" + key.KeyCode.ToString() + " up}";
                    }

                }
                result += val;
            }
            if (result == null) result = "";
            if (lastinsertedmodel != null) lastinsertedmodel.Properties["Text"].SetValue(new InArgument<string>(result));
        }
        public void UpdateModel(System.Activities.Presentation.Model.ModelItem lastinsertedmodel)
        {
            if (_keys == null) return;
            result = "";
            for (var i = 0; i < _keys.Count; i++)
            {
                string val = "";
                var key = _keys[i];
                if (key.up == false && (i + 1) < _keys.Count)
                {
                    if (key.KeyCode == _keys[i + 1].KeyCode && _keys[i + 1].up)
                    {
                        i++;
                        val = "{" + key.KeyCode.ToString() + "}";
                        if (key.KeyCode.ToString().StartsWith("KEY_"))
                        {
                            val = key.KeyCode.ToString().Substring(4).ToLower();
                        }
                        if (key.KeyCode == FlaUI.Core.WindowsAPI.VirtualKeyShort.SPACE)
                        {
                            val = " ";
                        }
                    }
                }
                if (string.IsNullOrEmpty(val))
                {
                    if (key.up == false)
                    {
                        val = "{" + key.KeyCode.ToString() + " down}";
                    }
                    else
                    {
                        val = "{" + key.KeyCode.ToString() + " up}";
                    }

                }
                result += val;
            }
            if (result == null) result = "";
            lastinsertedmodel.Properties["Text"].SetValue(new InArgument<string>(result));
        }
        [LocalizedDisplayName("activity_displayname", typeof(Resources.strings)), LocalizedDescription("activity_displayname_help", typeof(Resources.strings))]
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

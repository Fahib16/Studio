using System;
using System.Activities;
using System.ComponentModel;
using Newtonsoft.Json.Linq;

namespace Custom.StudioBridge
{
    /// <summary>
    /// Activity: Studio Activate Tab. Pilih/fokuskan tab tertentu (berdasarkan
    /// TabId, biasanya diambil dari Output StudioOpenTab atau StudioListTabs).
    /// </summary>
    [Designer(typeof(Design.StudioActivateTabDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.activatetab.png")]
    [DisplayName("Activate Tab")]
    [Description("Menjadikan sebuah tab Chrome sebagai tab aktif.")]
    public class StudioActivateTab : CodeActivity
    {
        public StudioActivateTab()
        {
            DisplayName = "Activate Tab";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("TabId")]
        public InArgument<int> TabId { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = ContinueOnError != null ? ContinueOnError.Get(context) : false;

            try
            {
                var tabId = TabId.Get(context);

                StudioPipeClient.SendCommand("activateTab", new JObject
                {
                    ["tabId"] = tabId
                });
            }
            catch (Exception) when (continueOnError)
            {
                // Telan error kalau ContinueOnError = true
            }
        }
    }
}

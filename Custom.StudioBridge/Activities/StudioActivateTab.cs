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
    public class StudioActivateTab : CodeActivity
    {
        [Category("Input")]
        [RequiredArgument]
        [DisplayName("TabId")]
        public InArgument<int> TabId { get; set; }

        [Category("Common")]
        [DisplayName("Continue On Error")]
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

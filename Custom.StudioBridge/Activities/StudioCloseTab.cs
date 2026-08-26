using System;
using System.Activities;
using System.ComponentModel;
using Newtonsoft.Json.Linq;

namespace Custom.StudioBridge
{
    /// <summary>
    /// Activity: Studio Close Tab. Tutup tab tertentu (berdasarkan TabId).
    /// </summary>
    [Designer(typeof(Design.StudioCloseTabDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    public class StudioCloseTab : CodeActivity
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

                StudioPipeClient.SendCommand("closeTab", new JObject
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

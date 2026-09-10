using OpenRPA.Interfaces;
using System;
using System.Activities;
using System.Activities.Core.Presentation;
using System.Activities.Presentation;
using System.Activities.Presentation.Toolbox;
using System.Activities.Statements;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace OpenRPA.Views
{
    /// <summary>
    /// Interaction logic for wfToolbox.xaml
    /// </summary>
    public partial class WFToolbox : UserControl
    {
        // public ToolboxControl toolbox { get; set; } = null;
        public WFToolbox()
        {
            Log.FunctionIndent("WFToolbox", "WFToolbox");
            InitializeComponent();
            DataContext = this;
            // toolborder.Child = InitializeActivitiesToolbox();
            InitializeActivitiesToolbox();
            Log.FunctionOutdent("WFToolbox", "WFToolbox");
            Instance = this;
        }
        public static WFToolbox Instance = null;
        private string getDisplayName(Type type)
        {
            string displayName = type.Name;
            string[] splitName = displayName.Split('`');
            displayName = splitName[0];
            var displayNameAttribute = type.GetCustomAttributes(typeof(System.ComponentModel.DisplayNameAttribute), true).FirstOrDefault() as System.ComponentModel.DisplayNameAttribute;
            if (displayNameAttribute != null) displayName = displayNameAttribute.DisplayName;
            if (splitName.Length > 1) displayName = string.Format("{0}<>", displayName);
            return displayName;
        }
        /// <summary>
        /// Seluruh isi toolbox sebagai daftar (kelompok, nama tampilan, tipe).
        ///
        /// Dipakai kotak cari activity pada tombol "+" di kanvas. Sengaja
        /// membaca kategori yang SUDAH terbentuk, bukan memindai assembly
        /// ulang, supaya isinya tidak mungkin berbeda dengan panel Aktivitas —
        /// termasuk activity bawaan yang sengaja disembunyikan.
        /// </summary>
        public static List<Tuple<string, string, Type>> AllTools()
        {
            var result = new List<Tuple<string, string, Type>>();
            if (Instance == null || Instance.tb == null) return result;

            foreach (var category in Instance.tb.Categories)
            {
                foreach (var tool in category.Tools)
                {
                    try
                    {
                        var type = Type.GetType(tool.ToolName + ", " + tool.AssemblyName);
                        if (type == null) continue;
                        if (type.IsAbstract || type.ContainsGenericParameters) continue;
                        if (type.GetConstructor(Type.EmptyTypes) == null) continue;

                        result.Add(new Tuple<string, string, Type>(category.CategoryName, tool.DisplayName, type));
                    }
                    catch (Exception ex)
                    {
                        Log.Debug("AllTools: " + ex.Message);
                    }
                }
            }
            return result;
        }

        public void InitializeActivitiesToolbox()
        {
            Log.FunctionIndent("WFToolbox", "InitializeActivitiesToolbox");
            try
            {
                // var tb = new ToolboxControl();
                // get all loaded assemblies
                IEnumerable<System.Reflection.Assembly> appAssemblies = AppDomain.CurrentDomain.GetAssemblies().OrderBy(a => a.GetName().Name)
                    .Where(a => a.GetName().Name != "System.ServiceModel.Activities");

                // check if assemblies contain activities
                int activitiesCount = 0;
                Type scriptActivitiesType = null;

                // Activity dikumpulkan dulu per KELOMPOK FUNGSI, baru sesudahnya
                // dijadikan kategori toolbox. Bawaan OpenRPA membuat satu
                // kategori per assembly, sehingga activity yang berkaitan bisa
                // terpisah hanya karena berada di project yang berbeda.
                var grouped = new Dictionary<string, List<Type>>();
                foreach (System.Reflection.Assembly activityLibrary in appAssemblies.Where(p => !p.IsDynamic))
                {
                    try
                    {
                        string[] excludeActivities = { "AddValidationError", "AndAlso", "AssertValidation", "CreateBookmarkScope", "DeleteBookmarkScope", "DynamicActivity",
                            "CancellationScope", "CompensableActivity", "Compensate", "Confirm", "GetChildSubtree", "GetParentChain", "GetWorkflowTree", "Add`3",  "And`3", "As`2", "Cast`2",
                        "Cast`2", "ArgumentValue`1", "ArrayItemReference`1", "ArrayItemValue`1", "Assign`1", "Constraint`1","CSharpReference`1", "CSharpValue`1", "DelegateArgumentReference`1",
                            "DelegateArgumentValue`1", "Divide`3", "DynamicActivity`1", "Equal`3", "FieldReference`2", "FieldValue`2", "ForEach`1", "InvokeAction", "InvokeDelegate",
                        "ArgumentReference`1", "VariableReference`1", "VariableValue`1", "VisualBasicReference`1", "VisualBasicValue`1", "InvokeMethod`1",
                        "StateMachineWithInitialStateFactory", "ParallelForEach`1", "ForEachWithBodyFactory`1"
                        };
                        // , "ParallelForEach", "ParallelForEachWithBodyFactory", "ForEachWithBodyFactory"

                        var actvities = from
                                            activityType in activityLibrary.GetExportedTypes()
                                        where
                                            (activityType.IsSubclassOf(typeof(Activity))
                                            || activityType.IsSubclassOf(typeof(NativeActivity))
                                            || activityType.IsSubclassOf(typeof(DynamicActivity))
                                            || activityType.IsSubclassOf(typeof(ActivityWithResult))
                                            || activityType.IsSubclassOf(typeof(AsyncCodeActivity))
                                            || activityType.IsSubclassOf(typeof(CodeActivity))
                                            || activityType.IsSubclassOf(typeof(FlowNode))
                                            || activityType == typeof(State)
                                            || activityType == typeof(FinalState)
                                            || activityType.GetInterfaces().Contains(typeof(IActivityTemplateFactory))
                                            )
                                            && !activityType.Assembly.CodeBase.Contains("Snippets.dll")
                                            && activityType.IsVisible
                                            && activityType.IsPublic
                                            && !activityType.IsNested
                                            && !activityType.IsAbstract
                                            && (activityType.GetConstructor(Type.EmptyTypes) != null)
                                            && !excludeActivities.Contains(activityType.Name)
                                            && !activityType.Name.StartsWith("InvokeAction`")
                                            && !activityType.Name.StartsWith("InvokeFunc`")
                                            && !activityType.Name.StartsWith("Subtract`")
                                            && !activityType.Name.StartsWith("GreaterThan`")
                                            && !activityType.Name.StartsWith("GreaterThanOrEqual`")
                                            && !activityType.Name.StartsWith("LessThan`")
                                            && !activityType.Name.StartsWith("LessThanOrEqual`")
                                            && !activityType.Name.StartsWith("Literal`")
                                            && !activityType.Name.StartsWith("MultidimensionalArrayItemReference`")
                                            && !activityType.Name.StartsWith("Multiply`")
                                            && !activityType.Name.StartsWith("New`")
                                            && !activityType.Name.StartsWith("NewArray`")
                                            && !activityType.Name.StartsWith("Or`")
                                            && !activityType.Name.StartsWith("OrElse")
                                            && !activityType.Name.EndsWith("`2")
                                            && !activityType.Name.EndsWith("`3")
                                            && activityType.Name != "ExcelActivity"
                                            && activityType.Name != "ExcelActivityOf`1"
                                            && !activityType.FullName.EndsWith("Statements.DoWhile")
                                            && !activityType.FullName.EndsWith("Statements.While")
                                            && !JakForgeToolbox.Hidden(activityType)
                                        orderby
                                            activityType.Name
                                        select
                                            activityType;

                        var assemblyName = activityLibrary.GetName().Name;
                        foreach (var activityType in actvities)
                        {
                            var category = JakForgeToolbox.CategoryOf(activityType, assemblyName);
                            List<Type> bucket;
                            if (!grouped.TryGetValue(category, out bucket))
                            {
                                bucket = new List<Type>();
                                grouped[category] = bucket;
                            }
                            bucket.Add(activityType);
                        }

                        if (scriptActivitiesType == null && activityLibrary.GetName().Name == "OpenRPA.Script")
                        { 
                            foreach (var type in activityLibrary.GetExportedTypes())
                            {
                                if(type.Name== "ScriptActivities")
                                {
                                    scriptActivitiesType = type;
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex.ToString());
                    }
                }

                // Kelompok dijadikan kategori toolbox, urutannya ditentukan
                // JakForgeToolbox: kelompok buatan sendiri lebih dulu, sisanya
                // urut abjad.
                foreach (var name in JakForgeToolbox.Sort(grouped.Keys))
                {
                    var category = new ToolboxCategory(name);
                    foreach (var activityType in grouped[name].OrderBy(t => getDisplayName(t)))
                    {
                        // CATATAN PENTING: JANGAN memakai konstruktor
                        // ToolboxItemWrapper yang menerima nama bitmap.
                        // Mengoper pack URI ke sana membuat proses mati dengan
                        // StackOverflowException saat memuat toolbox (sudah
                        // terbukti: dengan bitmap -> crash, tanpa bitmap ->
                        // normal). Ikon bertema untuk activity milik .NET
                        // dipasang lewat template item di wfToolbox.xaml,
                        // yang seluruhnya di bawah kendali kita.
                        category.Add(new ToolboxItemWrapper(activityType, getDisplayName(activityType)));
                    }

                    if (category.Tools.Count == 0) continue;
                    tb.Categories.Add(category);
                    activitiesCount += category.Tools.Count;
                }

                if(scriptActivitiesType != null)
                {// load uniplore dynamic script activities
                    List<ToolboxCategory> finalList = new List<ToolboxCategory>();
                    List<ToolboxCategory> saCategories = (List<ToolboxCategory>)scriptActivitiesType.GetMethod("LoadScriptActivities").Invoke(null, new object[] { });
                    foreach (var saCategory in saCategories)
                    {
                        if (saCategory.Tools.Count > 0)
                        {
                            finalList.Add(saCategory);
                            activitiesCount += saCategory.Tools.Count;
                        }
                    }

                    tb.Categories.ToList().ForEach(finalList.Add);
                    tb.Categories.Clear();
                    finalList.ForEach(tb.Categories.Add);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                MessageBox.Show("InitializeActivitiesToolbox: " + ex.Message);
            }
            Log.FunctionOutdent("WFToolbox", "InitializeActivitiesToolbox");
        }
    }
}

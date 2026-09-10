using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.Patterns.Infrastructure;
using FlaUI.Core.WindowsAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenRPA.Interfaces
{
    public class OtherExtensions : MarshalByRefObject
    {
        public IEnumerable<System.Globalization.CultureInfo> GetAvailableCultures(Type type)
        {
            List<System.Globalization.CultureInfo> result = new List<System.Globalization.CultureInfo>();
            var rm = new System.Resources.ResourceManager(type);
            System.Globalization.CultureInfo[] cultures = System.Globalization.CultureInfo.GetCultures(System.Globalization.CultureTypes.AllCultures);
            foreach (System.Globalization.CultureInfo culture in cultures)
            {
                try
                {
                    if (culture.Equals(System.Globalization.CultureInfo.InvariantCulture)) continue; //do not use "==", won't work

                    var rs = rm.GetResourceSet(culture, true, false);
                    if (rs != null)
                        result.Add(culture);
                    if (culture.TwoLetterISOLanguageName == "zh")
                    {
                        Console.WriteLine(culture.Name);
                    }
                }
                catch (System.Globalization.CultureNotFoundException)
                {
                    //NOP
                }
                rm.ReleaseAllResources();
            }
            return result;
        }
    }
    public static class Extensions
    {
        public static T GetSpecifiedPattern<T>(this System.Windows.Automation.AutomationElement element) where T : System.Windows.Automation.BasePattern
        {
            System.Windows.Automation.AutomationPattern[] supportedPattern = element.GetSupportedPatterns();

            foreach (System.Windows.Automation.AutomationPattern pattern in supportedPattern)
            {
                if (pattern.ProgrammaticName is T res)
                    return res;
            }
            return null;
        }
        //public static System.Windows.Automation.AutomationPattern GetSpecifiedPattern<T of >(this System.Windows.Automation.AutomationElement element, string patternName)
        //{
        //    System.Windows.Automation.AutomationPattern[] supportedPattern = element.GetSupportedPatterns();

        //    foreach (System.Windows.Automation.AutomationPattern pattern in supportedPattern)
        //    {
        //        if (pattern.ProgrammaticName == patternName)
        //            return pattern;
        //    }

        //    return null;
        //}
        public static System.Windows.Automation.AutomationElement GetParent(this System.Windows.Automation.AutomationElement el)
        {
            if (el == null)
            {
                return null;
            }
            return System.Windows.Automation.TreeWalker.ContentViewWalker.GetParent(el);
        }
        public static void SetForeground(this System.Windows.Automation.AutomationElement element)
        {
            if (element.Current.NativeWindowHandle > 0)
            {
                NativeMethods.SetForegroundWindow(new IntPtr(element.Current.NativeWindowHandle));
            }
        }
        public static void FocusNative(this System.Windows.Automation.AutomationElement element)
        {
            if (element.Current.NativeWindowHandle > 0)
            {
                var windowHandle = new IntPtr(element.Current.NativeWindowHandle);
                uint windowThreadId = User32.GetWindowThreadProcessId(windowHandle, out _);
                uint currentThreadId = Kernel32.GetCurrentThreadId();

                // attach window to the calling thread's message queue
                User32.AttachThreadInput(currentThreadId, windowThreadId, true);
                User32.SetFocus(windowHandle);
                // detach the window from the calling thread's message queue
                User32.AttachThreadInput(currentThreadId, windowThreadId, false);
                return;
            }
            // Fallback to the UIA Version
            element.SetFocus();
        }
        public static T FindById<T>(this System.Collections.ObjectModel.ObservableCollection<T> collection, string id) where T : IBase
        {
            return collection.Where(x => x._id == id).FirstOrDefault();
        }
        public static void UpdateCollection<T>(this System.Collections.ObjectModel.ObservableCollection<T> collection, IEnumerable<T> newCollection) 
        {
            IEnumerator<T> newCollectionEnumerator = newCollection.GetEnumerator();
            IEnumerator<T> collectionEnumerator = collection.GetEnumerator();

            var itemsToDelete = new System.Collections.ObjectModel.Collection<T>();
            while (collectionEnumerator.MoveNext())
            {
                T item = collectionEnumerator.Current;

                // Store item to delete (we can't do it while parse collection.
                if (!newCollection.Contains(item))
                {
                    itemsToDelete.Add(item);
                }
            }

            // Handle item to delete.
            foreach (T itemToDelete in itemsToDelete)
            {
                collection.Remove(itemToDelete);
            }

            var i = 0;
            while (newCollectionEnumerator.MoveNext())
            {
                T item = newCollectionEnumerator.Current;

                // Handle new item.
                if (!collection.Contains(item))
                {
                    if (collection.Count > i) { collection.Insert(i, item); } else { collection.Add(item); }
                }
                

                // Handle existing item, move at the good index.
                if (collection.Contains(item))
                {
                    int oldIndex = collection.IndexOf(item);
                    if (oldIndex != i)
                    {
                        // Items.Move(oldIndex, i);
                        
                        if(collection.Count > oldIndex && collection.Count > i) collection.Move(oldIndex, i);
                    }
                }

                i++;
            }
        }
        public static void UpdateItem<T>(this System.Collections.ObjectModel.ObservableCollection<T> collection, T Item, T NewItem)
        {
            T originalItem = Item;
            var index = collection.IndexOf(Item);
            NewItem.CopyPropertiesTo(Item, false);
            return;
        }
        public static void AddRange<T>(this System.Collections.ObjectModel.ObservableCollection<T> collection, IEnumerable<T> range)
        {
            foreach(var item in range) collection.Add(item);
        }
        public static void Sort<T>(this System.Collections.ObjectModel.ObservableCollection<T> collection, Comparison<T> comparison)
        {
            var sortableList = new List<T>(collection);
            sortableList.Sort(comparison);

            for (int i = 0; i < sortableList.Count; i++)
            {
                if (collection.Count != sortableList.Count) return;
                collection.Move(collection.IndexOf(sortableList[i]), i);
            }
        }
        public static string Base64Encode(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) plainText = "";
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
        public static string Base64Decode(string base64EncodedData)
        {
            if (string.IsNullOrEmpty(base64EncodedData)) return null;
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }
        static public string GetStringFromResource(string resourceName)
        {
            return GetStringFromResource(typeof(Extensions), resourceName);
        }
        static public string GetStringFromResource(Type t, string resourceName)
        {
            string[] names = t.Assembly.GetManifestResourceNames();
            foreach (var name in names)
            {
                if (name.EndsWith(resourceName))
                {
                    using (var stream = t.Assembly.GetManifestResourceStream(name))
                    using (var reader = new System.IO.StreamReader(stream))
                    {
                        string result = reader.ReadToEnd();
                        return result;
                    }
                }
            }
            return null;
        }
        public static Type FindType(string qualifiedTypeName)
        {
            Type t = Type.GetType(qualifiedTypeName);

            if (t != null)
            {
                return t;
            }
            else
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = asm.GetType(qualifiedTypeName);
                    if (t != null)
                        return t;
                }
                return null;
            }
        }
        public static System.Data.DataTable ToDataTable(this Newtonsoft.Json.Linq.JArray jArray)
        {
            var result = new System.Data.DataTable();
            foreach (var row in jArray)
            {
                foreach (var jToken in row)
                {
                    var jproperty = jToken as Newtonsoft.Json.Linq.JProperty;
                    if (jproperty == null) continue;
                    if (result.Columns[jproperty.Name] == null)
                        result.Columns.Add(jproperty.Name, typeof(string));
                }
            }
            foreach (var row in jArray)
            {
                var datarow = result.NewRow();
                foreach (var jToken in row)
                {
                    var jProperty = jToken as Newtonsoft.Json.Linq.JProperty;
                    if (jProperty == null) continue;
                    datarow[jProperty.Name] = jProperty.Value.ToString();
                }
                result.Rows.Add(datarow);
            }
            result.AcceptChanges();
            return result;
        }
        public static Newtonsoft.Json.Linq.JArray ToJArray(this System.Data.DataTable dt)
        {
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(dt);
            return Newtonsoft.Json.Linq.JArray.Parse(json);
        }
        public static IEnumerable<T> GetMyCustomAttributes<T>(this Type type, bool inherit)
        {
            if (type == null) return default(IEnumerable<T>);
            return type
                .GetCustomAttributes(typeof(T), inherit)
                .Cast<T>();
        }
        public static IEnumerable<System.Globalization.CultureInfo> GetAvailableCultures(Type type)
        {
            AppDomain otherDomain = null;
            try
            {
                otherDomain = AppDomain.CreateDomain("other domain");
                var otherType = typeof(OtherExtensions);
                var obj = otherDomain.CreateInstanceAndUnwrap(
                                         otherType.Assembly.FullName,
                                         otherType.FullName) as OtherExtensions;
                return obj.GetAvailableCultures(type);
            }
            catch (Exception)
            {

                throw;
            }
            finally
            {
                if (otherDomain != null) AppDomain.Unload(otherDomain);
            }
        }
        public static bool GetIsEmpty<T>(this System.Activities.OutArgument<T> src)
        {
            return (bool)src.GetType().GetProperty("IsEmpty", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(src, null);
        }
        public static string MyVideos
        {
            get
            {
                var dir = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
                if (!System.IO.Directory.Exists(System.IO.Path.Combine(dir)))
                    System.IO.Directory.CreateDirectory(dir);
                return dir;
            }
        }
        public static string MyPictures
        {
            get
            {
                var dir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                if (!System.IO.Directory.Exists(System.IO.Path.Combine(dir)))
                    System.IO.Directory.CreateDirectory(dir);
                return dir;
            }
        }
        public static string UserDirectory
        {
            get
            {
                var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenRPA");
                if (!System.IO.Directory.Exists(System.IO.Path.Combine(dir)))
                    System.IO.Directory.CreateDirectory(dir);
                return dir;
            }
        }
        /// <summary>
        /// Folder tempat program ini berada.
        ///
        /// Dipakai untuk mode portabel dan untuk mencari berkas benih setelan.
        /// </summary>
        public static string ProgramDirectory
        {
            get
            {
                return System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetEntryAssembly() != null
                        ? System.Reflection.Assembly.GetEntryAssembly().Location
                        : System.Reflection.Assembly.GetExecutingAssembly().Location);
            }
        }

        /// <summary>
        /// Nama berkas penanda mode portabel.
        ///
        /// Kalau berkas ini ada di sebelah program, SELURUH data — setelan,
        /// basis data offline, tata letak, catatan — pindah ke folder
        /// "jakforge-data" di sebelahnya, bukan ke Documents.
        ///
        /// Gunanya satu: menyalin folder programnya berarti menyalin seluruh
        /// pemasangannya. Tidak ada lagi "sudah dipasang tapi kosong karena
        /// datanya tertinggal di Documents komputer lama".
        /// </summary>
        public const string PortableMarker = "portable.txt";

        /// <summary>Benar kalau program ini berjalan dalam mode portabel.</summary>
        public static bool IsPortable
        {
            get
            {
                try
                {
                    var dir = ProgramDirectory;
                    if (string.IsNullOrEmpty(dir)) return false;

                    return System.IO.File.Exists(System.IO.Path.Combine(dir, PortableMarker));
                }
                catch (Exception)
                {
                    // Program yang gagal menentukan letaknya sendiri tetap harus
                    // bisa jalan; yang hilang cuma mode portabelnya.
                    return false;
                }
            }
        }

        /// <summary>Nama folder merek, dipakai di Documents dan LocalAppData.</summary>
        public const string NamaMerek = "JakForge";

        private static string _DataDirectory = null;

        /// <summary>
        /// Tempat SEGALA yang bukan pekerjaan pengguna: settings.json, basis
        /// data offline, tata letak jendela, plugin, tessdata, catatan galat.
        ///
        /// Dipisah dari ProjectsDirectory dengan sengaja. Sebelumnya keduanya
        /// satu folder di Documents, dan akibatnya Documents dipenuhi berkas
        /// yang tidak pernah dibuka siapa pun — sementara project, satu-satunya
        /// isi yang benar-benar milik pengguna, tenggelam di antaranya.
        ///
        /// Di %LOCALAPPDATA%, bukan %APPDATA%: isinya khusus mesin ini
        /// (basis data, tata letak, cache) dan tidak ada gunanya ikut
        /// roaming profile ke komputer lain.
        /// </summary>
        public static string DataDirectory
        {
            get
            {
                if (!string.IsNullOrEmpty(_DataDirectory)) return _DataDirectory;

                // Mode portabel diperiksa PALING DULU. Kalau penandanya ada,
                // Documents dan LocalAppData tidak dilirik sama sekali — bukan
                // dijadikan cadangan, karena "kadang di sini kadang di sana"
                // adalah perilaku yang tidak bisa dijelaskan kepada siapa pun.
                if (IsPortable)
                {
                    _DataDirectory = System.IO.Path.Combine(ProgramDirectory, "jakforge-data");
                }
                else
                {
                    _DataDirectory = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        NamaMerek, "Studio");
                }

                try
                {
                    if (!System.IO.Directory.Exists(_DataDirectory))
                        System.IO.Directory.CreateDirectory(_DataDirectory);
                }
                catch (Exception)
                {
                    // Folder yang gagal dibuat tetap dikembalikan apa adanya;
                    // pemanggil yang menulis ke sana akan gagal dengan pesan
                    // yang menyebut jalurnya, dan itu jauh lebih berguna
                    // daripada jalur kosong yang menyesatkan.
                }

                return _DataDirectory;
            }
            set { _DataDirectory = value; }
        }

        private static bool _sudahPindahDariOpenRPA = false;

        /// <summary>
        /// Memindahkan data dari tata letak lama (Documents\OpenRPA) ke tata
        /// letak baru, sekali saja, dan dengan MENYALIN — bukan memindahkan.
        ///
        /// Kenapa menyalin: kalau ada satu saja yang meleset dalam pemetaan
        /// ini, yang lama masih utuh dan pekerjaan orang tidak hilang. Ruang
        /// disk jauh lebih murah daripada project yang tidak bisa dikembalikan.
        ///
        /// Kenapa hanya sekali: penyalinan hanya terjadi kalau tujuannya masih
        /// kosong. Sesudah orang bekerja di tata letak baru, folder lama tidak
        /// pernah dilirik lagi — kalau tidak, perubahan hari ini akan tertimpa
        /// keadaan bulan lalu setiap kali program dijalankan.
        /// </summary>
        public static void PindahkanDataLamaKalauPerlu()
        {
            if (_sudahPindahDariOpenRPA) return;
            _sudahPindahDariOpenRPA = true;

            if (IsPortable) return;

            try
            {
                var lama = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "OpenRPA");

                if (!System.IO.Directory.Exists(lama)) return;

                // Project: Documents\OpenRPA\<sub>\<Nama> -> Documents\JakForge\<Nama>
                //
                // <sub> dulu berisi "offline" atau nama host orchestrator.
                // Tingkat itu sekarang hilang, jadi isinya dinaikkan satu
                // tingkat. Folder yang namanya sudah dipakai dilewati, bukan
                // ditimpa: menggabungkan dua project berbeda yang kebetulan
                // senama akan merusak keduanya.
                foreach (var sub in System.IO.Directory.GetDirectories(lama))
                {
                    var namaSub = System.IO.Path.GetFileName(sub);
                    if (namaSub.StartsWith("_")) continue;   // _shared, _orphans
                    if (string.Equals(namaSub, "extensions", StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.Equals(namaSub, "images", StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.Equals(namaSub, "tessdata", StringComparison.OrdinalIgnoreCase)) continue;

                    foreach (var project in System.IO.Directory.GetDirectories(sub))
                    {
                        var tujuan = System.IO.Path.Combine(
                            ProjectsDirectory, System.IO.Path.GetFileName(project));

                        if (System.IO.Directory.Exists(tujuan)) continue;
                        SalinFolder(project, tujuan);
                    }
                }

                // Config: berkas di akar Documents\OpenRPA -> DataDirectory
                foreach (var berkas in System.IO.Directory.GetFiles(lama))
                {
                    var tujuan = System.IO.Path.Combine(
                        DataDirectory, System.IO.Path.GetFileName(berkas));

                    if (!System.IO.File.Exists(tujuan))
                        System.IO.File.Copy(berkas, tujuan);
                }

                foreach (var nama in new[] { "extensions", "images", "tessdata", "_shared", "_orphans" })
                {
                    var asal = System.IO.Path.Combine(lama, nama);
                    var tujuan = System.IO.Path.Combine(DataDirectory, nama);

                    if (System.IO.Directory.Exists(asal) && !System.IO.Directory.Exists(tujuan))
                        SalinFolder(asal, tujuan);
                }
            }
            catch (Exception)
            {
                // Migrasi yang gagal TIDAK boleh menghentikan program. Yang
                // hilang cuma kenyamanan: datanya masih ada di tempat lama dan
                // bisa disalin tangan. Program yang menolak jalan karena
                // penyalinan gagal jauh lebih buruk daripada Studio kosong.
            }
        }

        private static void SalinFolder(string asal, string tujuan)
        {
            System.IO.Directory.CreateDirectory(tujuan);

            foreach (var berkas in System.IO.Directory.GetFiles(asal))
            {
                System.IO.File.Copy(
                    berkas, System.IO.Path.Combine(tujuan, System.IO.Path.GetFileName(berkas)), false);
            }

            foreach (var anak in System.IO.Directory.GetDirectories(asal))
            {
                SalinFolder(anak, System.IO.Path.Combine(tujuan, System.IO.Path.GetFileName(anak)));
            }
        }

        private static string _ProjectsDirectory = null;

        /// <summary>
        /// Tempat project pengguna, dan HANYA project.
        ///
        /// Bentuknya Documents\JakForge\&lt;Nama Project&gt; — satu folder per
        /// project, langsung terlihat, bisa disalin dan dibagikan apa adanya
        /// tanpa perlu tahu apa pun tentang Studio.
        /// </summary>
        public static string ProjectsDirectory
        {
            get
            {
                if (!string.IsNullOrEmpty(_ProjectsDirectory)) return _ProjectsDirectory;

                if (IsPortable)
                {
                    // Dalam mode portabel semuanya tetap satu folder yang bisa
                    // disalin bulat-bulat, tapi project tetap dipisah supaya
                    // tata letaknya sama seperti mode biasa.
                    _ProjectsDirectory = System.IO.Path.Combine(DataDirectory, "Projects");
                }
                else
                {
                    _ProjectsDirectory = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        NamaMerek);
                }

                try
                {
                    if (!System.IO.Directory.Exists(_ProjectsDirectory))
                        System.IO.Directory.CreateDirectory(_ProjectsDirectory);
                }
                catch (Exception)
                {
                }

                return _ProjectsDirectory;
            }
            set
            {
                _ProjectsDirectory = value;
            }
        }
        public static string PluginsDirectory
        {
            get
            {
                var asm = System.Reflection.Assembly.GetEntryAssembly();
                if (asm == null) asm = System.Reflection.Assembly.GetExecutingAssembly();
                var filepath = asm.CodeBase.Replace("file:///", "");
                var path = System.IO.Path.GetDirectoryName(filepath);
                return path;
            }
        }
        // DataDirectory yang DULU ada di sini mengembalikan UserDirectory,
        // yaitu %APPDATA%\OpenRPA — tempat ketiga yang menyimpan data selain
        // Documents\OpenRPA dan folder program, tanpa aturan yang bisa
        // dijelaskan tentang mana menyimpan apa. Definisinya sekarang satu,
        // di bagian atas berkas ini, bersama ProjectsDirectory.

        static public string ResourceAsString(this Type type, string resourceName)
        {
            // string[] names = typeof(Extensions).Assembly.GetManifestResourceNames();
            string[] names = type.Assembly.GetManifestResourceNames();
            foreach (var name in names)
            {
                if (name.EndsWith(resourceName))
                {
                    using (var s = type.Assembly.GetManifestResourceStream(name))
                    {
                        using (var reader = new System.IO.StreamReader(s))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
                else
                {
                    try
                    {
                        var set = new System.Resources.ResourceSet(type.Assembly.GetManifestResourceStream(names[0]));
                        foreach (System.Collections.DictionaryEntry resource in set)
                        {
                            if (((string)resource.Key).EndsWith(resourceName.ToLower()))
                            {
                                using (var reader = new System.IO.StreamReader(resource.Value as System.IO.Stream))
                                {
                                    return reader.ReadToEnd();
                                }

                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex.ToString());
                    }
                }
            }
            return null;
        }
        static public string ResourceAsString(string resourceName)
        {
            return ResourceAsString(typeof(Extensions), resourceName);
        }
        public static System.Windows.Media.Imaging.BitmapFrame GetImageSourceFromResource(string resourceName)
        {
            string[] names = typeof(Extensions).Assembly.GetManifestResourceNames();
            foreach (var name in names)
            {
                if (name.EndsWith(resourceName))
                {
                    return System.Windows.Media.Imaging.BitmapFrame.Create(typeof(Extensions).Assembly.GetManifestResourceStream(name));
                }
            }
            return null;
        }
        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (var item in source) action(item);
        }
        public static string JParse(this string o)
        {
            if (o == null) return null;
            string result = Newtonsoft.Json.JsonConvert.ToString(o);
            result = result.Substring(1, result.Length - 2);
            return result;
        }
        public static void AddCacheArgument(System.Activities.NativeActivityMetadata metadata, string name, System.Activities.Argument argument)
        {
            try
            {
                if (argument == null) return;
                Type ttype = argument.GetType().GetGenericArguments()[0];
                System.Activities.ArgumentDirection direction = System.Activities.ArgumentDirection.In;
                if (argument is System.Activities.InArgument) direction = System.Activities.ArgumentDirection.In;
                if (argument is System.Activities.InOutArgument) direction = System.Activities.ArgumentDirection.InOut;
                if (argument is System.Activities.OutArgument) direction = System.Activities.ArgumentDirection.Out;
                var ra = new System.Activities.RuntimeArgument(name, ttype, direction);
                metadata.Bind(argument, ra);
                metadata.AddArgument(ra);
            }
            catch (Exception)
            {
                throw;
            }
        }
        public static string ReplaceEnvironmentVariable(this string filename)
        {
            var USERPROFILE = Environment.GetEnvironmentVariable("USERPROFILE");
            var windir = Environment.GetEnvironmentVariable("windir");
            var SystemRoot = Environment.GetEnvironmentVariable("SystemRoot");
            var PUBLIC = Environment.GetEnvironmentVariable("PUBLIC");

            if (!string.IsNullOrEmpty(USERPROFILE)) filename = filename.Replace(USERPROFILE, "%USERPROFILE%");
            if (!string.IsNullOrEmpty(windir)) filename = filename.Replace(windir, "%windir%");
            if (!string.IsNullOrEmpty(SystemRoot)) filename = filename.Replace(SystemRoot, "%SystemRoot%");
            if (!string.IsNullOrEmpty(PUBLIC)) filename = filename.Replace(PUBLIC, "%PUBLIC%");

            var ProgramData = Environment.GetEnvironmentVariable("ProgramData");
            if (!string.IsNullOrEmpty(ProgramData)) filename = filename.Replace(ProgramData, "%ProgramData%");
            var ProgramFilesx86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            if (!string.IsNullOrEmpty(ProgramFilesx86)) filename = filename.Replace(ProgramFilesx86, "%ProgramFiles(x86)%");
            var ProgramFiles = Environment.GetEnvironmentVariable("ProgramFiles");
            if (!string.IsNullOrEmpty(ProgramFiles)) filename = filename.Replace(ProgramFiles, "%ProgramFiles%");
            var LOCALAPPDATA = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (!string.IsNullOrEmpty(LOCALAPPDATA)) filename = filename.Replace(LOCALAPPDATA, "%LOCALAPPDATA%");
            var APPDATA = Environment.GetEnvironmentVariable("APPDATA");
            if (!string.IsNullOrEmpty(APPDATA)) filename = filename.Replace(APPDATA, "%APPDATA%");


            //var = Environment.GetEnvironmentVariable("");
            //if (!string.IsNullOrEmpty()) filename = filename.Replace(, "%%");

            return filename;
        }
        //public static Task WaitOneAsync(this System.Threading.WaitHandle waitHandle, TimeSpan timeout)
        //{
        //    if (waitHandle == null) throw new ArgumentNullException("waitHandle");
        //    var Milliseconds = timeout.TotalMilliseconds;
        //    if (Milliseconds < 1) Milliseconds = -1;
        //    var tcs = new TaskCompletionSource<bool>();
        //    var rwh = System.Threading.ThreadPool.RegisterWaitForSingleObject(waitHandle, delegate { tcs.TrySetResult(true); }, null, (uint)Milliseconds, true);
        //    var t = tcs.Task;
        //    t.ContinueWith((antecedent) => rwh.Unregister(null));
        //    return t;
        //}
        public static async Task<bool> WaitOneAsync(this System.Threading.WaitHandle handle, int millisecondsTimeout, System.Threading.CancellationToken cancellationToken)
        {
            System.Threading.RegisteredWaitHandle registeredHandle = null;
            var tokenRegistration = default(System.Threading.CancellationTokenRegistration);
            try
            {
                var tcs = new TaskCompletionSource<bool>();
                registeredHandle = System.Threading.ThreadPool.RegisterWaitForSingleObject(
                    handle,
                    (state, timedOut) => ((TaskCompletionSource<bool>)state).TrySetResult(!timedOut),
                    tcs,
                    millisecondsTimeout,
                    true);
                tokenRegistration = cancellationToken.Register(
                    state => ((TaskCompletionSource<bool>)state).TrySetCanceled(),
                    tcs);
                return await tcs.Task;
            }
            finally
            {
                if (registeredHandle != null)
                    registeredHandle.Unregister(null);
                tokenRegistration.Dispose();
            }
        }
        public static Task<bool> WaitOneAsync(this System.Threading.WaitHandle handle, TimeSpan timeout, System.Threading.CancellationToken cancellationToken)
        {
            return handle.WaitOneAsync((int)timeout.TotalMilliseconds, cancellationToken);
        }

        public static Task<bool> WaitOneAsync(this System.Threading.WaitHandle handle, System.Threading.CancellationToken cancellationToken)
        {
            return handle.WaitOneAsync(System.Threading.Timeout.Infinite, cancellationToken);
        }
        public static bool TryCast<T>(this object obj, out T result)
        {
            if (obj is T)
            {
                result = (T)obj;
                return true;
            }
            if (obj is System.Activities.Expressions.Literal<T>)
            {
                result = ((System.Activities.Expressions.Literal<T>)obj).Value;
                return true;
            }

            result = default;
            return false;
        }
        public static T TryCast<T>(this object obj)
        {
            if (TryCast(obj, out T result))
                return result;
            return result;
        }
        public static T GetValue<T>(this System.Activities.Presentation.Model.ModelItem model, string name)
        {
            T result = default;
            if (model.Properties[name] != null)
            {
                if (model.Properties[name].Value == null) return result;
                if (model.Properties[name].Value.Properties["Expression"] != null)
                {
                    result = model.Properties[name].Value.Properties["Expression"].ComputedValue.TryCast<T>();
                    return result;
                }
                result = model.Properties[name].ComputedValue.TryCast<T>();
                return result;
            }
            return result;
        }
        public static void SetValue<T>(this System.Activities.Presentation.Model.ModelItem model, string name, T value)
        {
            if (model.Properties[name] != null)
            {
                model.Properties[name].SetValue(value);
            }
        }
        public static void SetValueInArg<T>(this System.Activities.Presentation.Model.ModelItem model, string name, T value)
        {
            model.SetValue(name, new System.Activities.InArgument<T>() { Expression = new System.Activities.Expressions.Literal<T>(value) });
        }
        public static void SetValueOutArg<T>(this System.Activities.Presentation.Model.ModelItem model, string name, string value)
        {
            model.SetValue(name, new System.Activities.OutArgument<T>() { Expression = new Microsoft.VisualBasic.Activities.VisualBasicReference<T>(value) });
            // model.SetValue(name, new System.Activities.OutArgument<T>() { Expression = new Microsoft.VisualBasic.Activities.VisualBasicValue<T>(value) });
        }
        public static bool CollectArguments = false;
        public static ProcessInfo GetProcessInfo(this AutomationElement element)
        {
            if (!element.Properties.ProcessId.IsSupported) return null;
            ProcessInfo result = new ProcessInfo();
            int processId = -1;
            IntPtr handle = IntPtr.Zero;
            try
            {
                processId = element.Properties.ProcessId.Value;
                using (var p = System.Diagnostics.Process.GetProcessById(processId))
                {
                    handle = p.Handle;
                    result.ProcessName = p.ProcessName;
                    result.Filename = p.MainModule.FileName.ReplaceEnvironmentVariable();
                }
            }
            catch (Exception)
            {
            }

            bool _isImmersiveProcess = false;
            try
            {
                if (handle != IntPtr.Zero) _isImmersiveProcess = NativeMethods.IsImmersiveProcess(handle);
            }
            catch (Exception)
            {
            }
            string ApplicationUserModelId = null;
            if (_isImmersiveProcess)
            {
                var automation = AutomationUtil.getAutomation();
                var pc = new FlaUI.Core.Conditions.PropertyCondition(automation.PropertyLibrary.Element.ClassName, "Windows.UI.Core.CoreWindow");
                var _el = element.FindFirstChild(pc);
                if (_el != null)
                {
                    processId = _el.Properties.ProcessId.Value;

                    IntPtr ptrProcess = OpenProcess(QueryLimitedInformation, false, processId);
                    if (IntPtr.Zero != ptrProcess)
                    {
                        uint cchLen = 130; // Currently APPLICATION_USER_MODEL_ID_MAX_LENGTH = 130
                        StringBuilder sbName = new StringBuilder((int)cchLen);
                        Int32 lResult = GetApplicationUserModelId(ptrProcess, ref cchLen, sbName);
                        if (APPMODEL_ERROR_NO_APPLICATION == lResult)
                        {
                            _isImmersiveProcess = false;
                        }
                        else if (ERROR_SUCCESS == lResult)
                        {
                            ApplicationUserModelId = sbName.ToString();
                        }
                        else if (ERROR_INSUFFICIENT_BUFFER == lResult)
                        {
                            sbName = new StringBuilder((int)cchLen);
                            if (ERROR_SUCCESS == GetApplicationUserModelId(ptrProcess, ref cchLen, sbName))
                            {
                                ApplicationUserModelId = sbName.ToString();
                            }
                        }
                        CloseHandle(ptrProcess);
                    }
                }
                else { _isImmersiveProcess = false; }


            }

            if(CollectArguments)
            {
                try
                {
                    var arguments = GetCommandLine(processId);
                    var arr = ParseCommandLine(arguments);

                    if (arr.Length == 0)
                    {

                    }
                    else if (arguments.Contains("\"" + arr[0] + "\""))
                    {
                        result.Arguments = arguments.Replace("\"" + arr[0] + "\"", "");
                    }
                    else
                    {
                        result.Arguments = arguments.Replace(arr[0], "");
                    }
                    if (result.Arguments != null) { result.Arguments = result.Arguments.ReplaceEnvironmentVariable(); }

                }
                catch (Exception)
                {
                }
            }
            result.ApplicationUserModelId = ApplicationUserModelId;
            result.IsImmersiveProcess = _isImmersiveProcess;
            return result;
        }
        public static string GetCommandLine(int processId)
        {
            string result = null;
            try
            {
                var thread = new System.Threading.Thread(() =>
                {
                    using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + processId))
                    using (ManagementObjectCollection objects = searcher.Get())
                    {
                        result = objects.Cast<ManagementBaseObject>().SingleOrDefault()?["CommandLine"]?.ToString();
                    }
                });
                thread.Start();
                thread.Join(); //wait for the thread to finish

            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                return null;
            }
            return result;
        }
        public const int QueryLimitedInformation = 0x1000;
        public const int ERROR_INSUFFICIENT_BUFFER = 0x7a;
        public const int ERROR_SUCCESS = 0x0;
        public const int APPMODEL_ERROR_NO_APPLICATION = 15703;
        public static string[] ParseCommandLine(string commandLine)
        {
            List<string> arguments = new List<string>();
            bool stringIsQuoted = false;
            string argString = "";
            if (commandLine != null)
            {
                for (int c = 0; c < commandLine.Length; c++)  //process string one character at a tie
                {
                    if (commandLine.Substring(c, 1) == "\"")
                    {
                        if (stringIsQuoted)  //end quote so populate next element of list with constructed argument
                        {
                            arguments.Add(argString);
                            argString = "";
                        }
                        else
                        {
                            stringIsQuoted = true; //beginning quote so flag and scip
                        }
                    }
                    else if (commandLine.Substring(c, 1) == "".PadRight(1))
                    {
                        if (stringIsQuoted)
                        {
                            argString += commandLine.Substring(c, 1); //blank is embedded in quotes, so preserve it
                        }
                        else if (argString.Length > 0)
                        {
                            arguments.Add(argString);  //non-quoted blank so add to list if the first consecutive blank
                        }
                    }
                    else
                    {
                        argString += commandLine.Substring(c, 1);  //non-blan character:  add it to the element being constructed
                    }
                }
            }
            return arguments.ToArray();
        }
        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);
        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr hHandle);
        [DllImport("kernel32.dll")]
        public static extern int GetApplicationUserModelId(
            IntPtr hProcess,
            ref uint AppModelIDLength,
            [MarshalAs(UnmanagedType.LPWStr)] StringBuilder sbAppUserModelID);
    }
    public class ProcessInfo
    {
        public string Filename { get; set; }
        public string ProcessName { get; set; }
        public string Arguments { get; set; }
        public string ApplicationUserModelId { get; set; }
        public bool IsImmersiveProcess { get; set; }
    }

}

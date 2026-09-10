using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenRPA.Interfaces;
using OpenRPA.Interfaces.entity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace OpenRPA.Storage.ProjectFolders
{
    /// <summary>
    /// Storage provider dengan tata letak SATU FOLDER PER PROJECT, mirip
    /// UiPath — supaya satu project bisa di-zip, di-copy ke mesin lain, atau
    /// dimasukkan Git sebagai satu kesatuan.
    ///
    ///   Documents\JakForge\                 &lt;- HANYA project, milik pengguna
    ///     Nama Project\
    ///       project.json                Project (JSON entity penuh)
    ///       Main.xaml                   XAML mentah, enak dibaca &amp; di-diff
    ///       .project\Main.workflow.json metadata workflow (tanpa Xaml)
    ///
    ///   %LOCALAPPDATA%\JakForge\Studio\     &lt;- isi mesin, bukan pekerjaan
    ///     settings.json
    ///     _shared\
    ///       openrpa_instances\{id}.json     tidak punya project
    ///       workitems\{id}.json
    ///     _orphans\
    ///
    /// KENAPA XAML DIPISAH DARI METADATA: kalau XAML ikut ditanam di dalam
    /// JSON (seperti provider Filesystem bawaan), isinya jadi satu baris
    /// raksasa penuh escape — tidak bisa dibaca manusia, dan diff Git-nya
    /// tidak berguna. Itu justru menghapus alasan utama pindah dari .db.
    ///
    /// KENAPA PAKAI INDEKS DI MEMORI: IStorage mencari objek berdasarkan _id,
    /// sedangkan tata letak ini menamai file berdasarkan nama yang enak
    /// dibaca manusia. Peta id-ke-path dibangun sekali saat Initialize() dan
    /// dipelihara setiap tulis/hapus.
    /// </summary>
    public class Instance : IStorage
    {
        public string Name { get; set; }

        /// <summary>
        /// Nama berkas metadata proyek. Sejak tata letaknya disamakan dengan
        /// UiPath, namanya <c>project.json</c>; proyek lama masih memakai
        /// <c>project.rpaproj</c> dan tetap bisa dibaca, lalu dimigrasi
        /// otomatis pada penyimpanan berikutnya.
        /// </summary>
        private const string ProjectFile = "project.json";
        private const string LegacyProjectFile = "project.rpaproj";

        /// <summary>
        /// Folder tempat metadata internal OpenRPA disimpan.
        ///
        /// Berkas <c>*.workflow.json</c>, <c>*.detector.json</c>, dan
        /// <c>*.queue.json</c> pindah ke sini supaya AKAR proyek hanya berisi
        /// hal yang memang dilihat orang: project.json, entry-points.json, dan
        /// berkas .xaml — sama seperti proyek UiPath.
        /// </summary>
        private const string MetaFolder = ".project";

        private const string EntryPointsFile = "entry-points.json";

        /// <summary>
        /// Folder baku yang selalu ada di sebuah proyek, mengikuti tata letak
        /// UiPath. Isinya boleh kosong; keberadaannya yang membuat proyek bisa
        /// dipindah, di-zip, atau dimasukkan Git sebagai satu kesatuan dengan
        /// tempat yang sudah jelas untuk tiap jenis berkas.
        /// </summary>
        private static readonly string[] StandardFolders =
        {
            ".entities",     // definisi entity
            ".local",        // cache lokal, tidak perlu masuk Git
            ".objects",      // object repository
            MetaFolder,      // metadata internal OpenRPA
            ".screenshots",  // tangkapan layar elemen
            ".settings",     // setelan khusus proyek
            ".templates",    // template workflow
            ".tmh",          // berkas sementara
        };

        private const string SharedFolder = "_shared";
        private const string OrphanFolder = "_orphans";

        /// <summary>projectid ke path folder project.</summary>
        private readonly Dictionary<string, string> _projectFolders =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>_id objek apa pun ke path file metadata-nya.</summary>
        private readonly Dictionary<string, string> _itemFiles =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private readonly object _lock = new object();

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        public async Task Initialize()
        {
            if (!PluginConfig.enabled) return;
            RebuildIndex();
        }

        // ------------------------------------------------------------------
        // Lokasi
        // ------------------------------------------------------------------

        private string BasePath()
        {
            // Langsung ProjectsDirectory: Documents\JakForge\<Nama Project>.
            //
            // Dua hal yang DULU ada di sini sengaja dibuang.
            //
            // Pertama, jalurnya diturunkan dari letak settings.json. Itu
            // menyatukan dua hal yang tidak berhubungan: memindahkan berkas
            // setelan berarti memindahkan seluruh project pengguna, diam-diam.
            //
            // Kedua, ada satu tingkat tambahan berisi "offline" atau nama host
            // orchestrator. Maksudnya baik — project dari dua orchestrator
            // tidak tercampur — tapi harganya dibayar setiap hari oleh setiap
            // orang: project yang dicari di Documents tidak ada di tempat yang
            // masuk akal, melainkan satu tingkat lebih dalam di folder bernama
            // "offline" yang tidak berarti apa pun bagi pemiliknya. Studio ini
            // dipakai satu orang dengan satu ForgeHub, jadi pemisahan itu
            // membayar untuk masalah yang tidak pernah terjadi.
            return Interfaces.Extensions.ProjectsDirectory;
        }

        /// <summary>
        /// Tempat isi yang BUKAN project: antrean workitem, detector, catatan
        /// instance, dan project yatim.
        ///
        /// Dulu semuanya folder bersaudara dengan project. Akibatnya Documents
        /// berisi "_shared" dan "_orphans" di sebelah pekerjaan pengguna —
        /// dua folder yang tidak pernah dibuka siapa pun tapi selalu terlihat,
        /// dan yang gampang ikut tersalin saat orang membagikan projectnya.
        /// </summary>
        private string DataPath()
        {
            return Interfaces.Extensions.DataDirectory;
        }

        /// <summary>Nama folder untuk sebuah type yang tidak bernaung di project.</summary>
        private string SharedCollection<T>() where T : class
        {
            var shared = Path.Combine(DataPath(), SharedFolder);

            if (typeof(IWorkflowInstance).IsAssignableFrom(typeof(T)))
                return Path.Combine(shared, "openrpa_instances");
            if (typeof(IWorkitem).IsAssignableFrom(typeof(T)))
                return Path.Combine(shared, "workitems");
            if (typeof(IWorkitemQueue).IsAssignableFrom(typeof(T)))
                return Path.Combine(shared, "workitemqueues");
            if (typeof(IDetector).IsAssignableFrom(typeof(T)))
                return Path.Combine(shared, "detectors");

            return Path.Combine(shared, "other");
        }

        private static bool IsProject<T>() { return typeof(IProject).IsAssignableFrom(typeof(T)); }
        private static bool IsWorkflow<T>() { return typeof(IWorkflow).IsAssignableFrom(typeof(T)); }
        private static bool IsDetector<T>() { return typeof(IDetector).IsAssignableFrom(typeof(T)); }
        private static bool IsQueue<T>() { return typeof(IWorkitemQueue).IsAssignableFrom(typeof(T)); }

        /// <summary>
        /// Baca projectid lewat refleksi, bukan lewat interface tertentu.
        /// Workflow, Detector, dan WorkitemQueue sama-sama punya properti ini,
        /// tapi lewat rantai interface yang berbeda-beda — refleksi membuat
        /// provider ini tidak ikut rusak kalau hierarki itu berubah di versi
        /// OpenRPA berikutnya.
        /// </summary>
        private static string GetProjectId(object item)
        {
            if (item == null) return null;
            var prop = item.GetType().GetProperty("projectid",
                BindingFlags.Public | BindingFlags.Instance);
            return prop?.GetValue(item) as string;
        }

        private static string GetStringProp(object item, string name)
        {
            var prop = item?.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            return prop?.GetValue(item) as string;
        }

        private static string SafeName(string raw, string fallback)
        {
            if (string.IsNullOrWhiteSpace(raw)) return fallback;
            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(raw.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
            return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned;
        }

        // ------------------------------------------------------------------
        // Indeks
        // ------------------------------------------------------------------

        private void RebuildIndex()
        {
            lock (_lock)
            {
                _projectFolders.Clear();
                _itemFiles.Clear();

                var basepath = BasePath();
                if (!Directory.Exists(basepath)) return;

                foreach (var dir in Directory.GetDirectories(basepath))
                {
                    var folderName = Path.GetFileName(dir);
                    if (folderName == SharedFolder) continue;

                    if (!HasProjectFile(dir)) continue;
                    var projectFile = ProjectFilePath(dir);

                    try
                    {
                        var o = JObject.Parse(File.ReadAllText(projectFile));
                        var id = (string)o["_id"];
                        if (string.IsNullOrEmpty(id)) continue;

                        _projectFolders[id] = dir;
                        _itemFiles[id] = projectFile;

                        foreach (var f in MetadataFilesOf(dir))
                            IndexMetadataFile(f);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("ProjectFolders: gagal membaca " + projectFile + ": " + ex.Message);
                    }
                }

                var shared = Path.Combine(basepath, SharedFolder);
                if (Directory.Exists(shared))
                {
                    foreach (var f in Directory.GetFiles(shared, "*.json", SearchOption.AllDirectories))
                        IndexMetadataFile(f);
                }
            }
        }

        private void IndexMetadataFile(string file)
        {
            try
            {
                var o = JObject.Parse(File.ReadAllText(file));
                var id = (string)o["_id"];
                if (!string.IsNullOrEmpty(id)) _itemFiles[id] = file;
            }
            catch (Exception ex)
            {
                Log.Warning("ProjectFolders: gagal membaca " + file + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Folder tujuan untuk objek milik sebuah project. Objek yang
        /// project-nya belum dikenal ditaruh di _orphans, BUKAN ditolak —
        /// urutan penyimpanan tidak selalu project dulu, dan menolaknya akan
        /// membuat data hilang diam-diam.
        /// </summary>
        private string FolderForProject(string projectid)
        {
            if (!string.IsNullOrEmpty(projectid))
            {
                lock (_lock)
                {
                    if (_projectFolders.TryGetValue(projectid, out var known) && Directory.Exists(known))
                        return known;
                }
            }

            var orphan = Path.Combine(DataPath(), OrphanFolder);
            Directory.CreateDirectory(orphan);
            return orphan;
        }

        private string EnsureProjectFolder(object project, string id)
        {
            lock (_lock)
            {
                var wanted = SafeName(GetStringProp(project, "name"), id);
                var target = Path.Combine(BasePath(), wanted);

                if (_projectFolders.TryGetValue(id, out var existing) && Directory.Exists(existing))
                {
                    if (string.Equals(existing, target, StringComparison.OrdinalIgnoreCase))
                        return existing;

                    // Project di-rename di dalam OpenRPA: ikut ganti nama
                    // foldernya, supaya nama folder tetap mencerminkan isinya.
                    // Kalau nama tujuan sudah dipakai project lain, biarkan
                    // folder lama — nama duplikat lebih baik daripada dua
                    // project saling menimpa isi.
                    if (!Directory.Exists(target))
                    {
                        try
                        {
                            Directory.Move(existing, target);
                            _projectFolders[id] = target;
                            RemapIndexAfterMove(existing, target);
                            return target;
                        }
                        catch (Exception ex)
                        {
                            Log.Warning("ProjectFolders: gagal rename folder project: " + ex.Message);
                            return existing;
                        }
                    }
                    return existing;
                }

                if (Directory.Exists(target) && !_projectFolders.ContainsValue(target))
                {
                    _projectFolders[id] = target;
                    return target;
                }

                if (Directory.Exists(target)) target = target + "_" + id.Substring(0, Math.Min(6, id.Length));

                Directory.CreateDirectory(target);
                _projectFolders[id] = target;
                return target;
            }
        }


        // ------------------------------------------------------------------
        // Tata letak folder proyek
        // ------------------------------------------------------------------

        /// <summary>
        /// Berkas metadata proyek di sebuah folder: <c>project.json</c> kalau
        /// ada, kalau tidak <c>project.rpaproj</c> milik tata letak lama.
        /// Untuk penulisan, yang dikembalikan selalu yang baru.
        /// </summary>
        private static string ProjectFilePath(string dir)
        {
            var modern = Path.Combine(dir, ProjectFile);
            if (File.Exists(modern)) return modern;

            var legacy = Path.Combine(dir, LegacyProjectFile);
            if (File.Exists(legacy)) return legacy;

            return modern;
        }

        private static bool HasProjectFile(string dir)
        {
            return File.Exists(Path.Combine(dir, ProjectFile))
                || File.Exists(Path.Combine(dir, LegacyProjectFile));
        }

        /// <summary>Folder metadata internal sebuah proyek, dibuat kalau belum ada.</summary>
        private static string MetaFolderOf(string projectFolder)
        {
            var meta = Path.Combine(projectFolder, MetaFolder);
            if (!Directory.Exists(meta)) Directory.CreateDirectory(meta);
            return meta;
        }

        /// <summary>
        /// Pastikan folder baku sebuah proyek ada. Dipanggil setiap folder
        /// proyek dipakai, jadi proyek lama ikut dilengkapi tanpa perlu
        /// langkah migrasi tersendiri.
        /// </summary>
        private static void EnsureStandardFolders(string projectFolder)
        {
            foreach (var name in StandardFolders)
            {
                try
                {
                    var path = Path.Combine(projectFolder, name);
                    if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                }
                catch (Exception ex)
                {
                    Log.Warning("ProjectFolders: gagal membuat folder " + name + ": " + ex.Message);
                }
            }
        }

        /// <summary>Semua berkas metadata sebuah proyek, dari folder .project maupun akar (tata letak lama).</summary>
        private static IEnumerable<string> MetadataFilesOf(string projectFolder)
        {
            var meta = Path.Combine(projectFolder, MetaFolder);
            if (Directory.Exists(meta))
            {
                foreach (var f in Directory.GetFiles(meta, "*.json")) yield return f;
            }

            foreach (var f in Directory.GetFiles(projectFolder, "*.json"))
            {
                // project.json bukan metadata anak, dan entry-points.json cuma
                // ringkasan yang dihasilkan ulang — keduanya jangan diindeks.
                var name = Path.GetFileName(f);
                if (string.Equals(name, ProjectFile, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(name, EntryPointsFile, StringComparison.OrdinalIgnoreCase)) continue;
                yield return f;
            }
        }

        private void RemapIndexAfterMove(string oldRoot, string newRoot)
        {
            var keys = _itemFiles.Where(kv => kv.Value.StartsWith(oldRoot, StringComparison.OrdinalIgnoreCase))
                                 .Select(kv => kv.Key).ToList();
            foreach (var k in keys)
                _itemFiles[k] = newRoot + _itemFiles[k].Substring(oldRoot.Length);
        }

        // ------------------------------------------------------------------
        // Serialisasi
        // ------------------------------------------------------------------

        private static JsonSerializerSettings Settings()
        {
            return new JsonSerializerSettings { ContractResolver = new DoNotIgnoreResolver() };
        }

        private static T Deserialize<T>(string json) where T : apibase
        {
            var o = JObject.Parse(json);

            bool isDirty = o.ContainsKey("isDirty") && (bool)o["isDirty"];
            bool isExpanded = o.ContainsKey("IsExpanded") && (bool)o["IsExpanded"];
            bool isSelected = o.ContainsKey("IsSelected") && (bool)o["IsSelected"];

            T item = JsonConvert.DeserializeObject<T>(json);

            // Deserialisasi menandai objek jadi dirty, jadi status aslinya
            // harus dikembalikan — kalau tidak, semua objek dianggap belum
            // tersinkron setiap kali OpenRPA dibuka.
            if (item is IWorkflow wf) { wf.IsExpanded = isExpanded; wf.IsSelected = isSelected; }
            if (item is IProject p) { p.IsExpanded = isExpanded; p.IsSelected = isSelected; }
            item.isDirty = isDirty;

            return item;
        }

        /// <summary>
        /// Path berkas .xaml pasangan dari sebuah berkas metadata workflow.
        ///
        /// Metadata tinggal di <c>.project\</c>, tapi XAML-nya tetap di AKAR
        /// proyek: berkas itulah yang dibuka, dibaca, dan di-diff orang, persis
        /// seperti Main.xaml di proyek UiPath.
        /// </summary>
        private static string XamlPathFor(string metadataPath)
        {
            var dir = Path.GetDirectoryName(metadataPath);
            var stem = Path.GetFileName(metadataPath);
            if (stem.EndsWith(".workflow.json", StringComparison.OrdinalIgnoreCase))
                stem = stem.Substring(0, stem.Length - ".workflow.json".Length);

            if (string.Equals(Path.GetFileName(dir), MetaFolder, StringComparison.OrdinalIgnoreCase))
                dir = Path.GetDirectoryName(dir);

            return Path.Combine(dir, stem + ".xaml");
        }

        private static string ReadWorkflowJson(string metadataPath)
        {
            var json = File.ReadAllText(metadataPath);

            var xamlPath = XamlPathFor(metadataPath);
            if (!File.Exists(xamlPath)) return json;

            var o = JObject.Parse(json);
            o["Xaml"] = File.ReadAllText(xamlPath);
            return o.ToString(Formatting.None);
        }

        // ------------------------------------------------------------------
        // IStorage
        // ------------------------------------------------------------------

        public async Task<T[]> FindAll<T>() where T : apibase
        {
            if (!PluginConfig.enabled) return Array.Empty<T>();

            var result = new List<T>();
            var basepath = BasePath();
            if (!Directory.Exists(basepath)) return result.ToArray();

            if (IsProject<T>())
            {
                foreach (var dir in Directory.GetDirectories(basepath))
                {
                    if (Path.GetFileName(dir) == SharedFolder) continue;
                    if (!HasProjectFile(dir)) continue;
                    var pf = ProjectFilePath(dir);
                    TryAdd(result, () => Deserialize<T>(File.ReadAllText(pf)), pf);
                }
                return result.ToArray();
            }

            string suffix = IsWorkflow<T>() ? ".workflow.json"
                          : IsDetector<T>() ? ".detector.json"
                          : IsQueue<T>() ? ".queue.json"
                          : null;

            if (suffix != null)
            {
                var folders = Directory.GetDirectories(basepath)
                    .Where(d => Path.GetFileName(d) != SharedFolder).ToList();

                var orphan = Path.Combine(basepath, OrphanFolder);
                if (Directory.Exists(orphan) && !folders.Contains(orphan)) folders.Add(orphan);

                foreach (var dir in folders)
                {
                    // Metadata dicari di folder .project MAUPUN di akar, supaya
                    // proyek yang belum dimigrasi tetap terbaca.
                    foreach (var f in MetadataFilesOf(dir))
                    {
                        if (!f.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;

                        var path = f;
                        TryAdd(result, () => Deserialize<T>(
                            IsWorkflow<T>() ? ReadWorkflowJson(path) : File.ReadAllText(path)), path);
                    }
                }
                return result.ToArray();
            }

            var sharedPath = SharedCollection<T>();
            if (!Directory.Exists(sharedPath)) return result.ToArray();

            foreach (var f in Directory.GetFiles(sharedPath, "*.json"))
            {
                var path = f;
                TryAdd(result, () => Deserialize<T>(File.ReadAllText(path)), path);
            }
            return result.ToArray();
        }

        /// <summary>
        /// Satu file rusak tidak boleh menggagalkan seluruh pemuatan: OpenRPA
        /// akan gagal start dan user kehilangan akses ke SEMUA project hanya
        /// karena satu file cacat. Yang rusak dilewati dan dicatat di log.
        /// </summary>
        private static void TryAdd<T>(List<T> list, Func<T> read, string path) where T : apibase
        {
            try { list.Add(read()); }
            catch (Exception ex) { Log.Warning("ProjectFolders: melewati " + path + ": " + ex.Message); }
        }

        public async Task<T> FindById<T>(string id) where T : apibase
        {
            if (!PluginConfig.enabled) return null;
            if (string.IsNullOrEmpty(id)) return null;

            string path;
            lock (_lock) { if (!_itemFiles.TryGetValue(id, out path)) path = null; }

            if (path == null || !File.Exists(path))
            {
                // Indeks bisa basi kalau file diubah dari luar OpenRPA —
                // itu justru hal yang ingin didukung tata letak ini.
                RebuildIndex();
                lock (_lock) { if (!_itemFiles.TryGetValue(id, out path)) return null; }
                if (!File.Exists(path)) return null;
            }

            var json = IsWorkflow<T>() ? ReadWorkflowJson(path) : File.ReadAllText(path);
            return Deserialize<T>(json);
        }

        public async Task<T> Insert<T>(T item) where T : apibase
        {
            if (!PluginConfig.enabled) return item;

            var json = JsonConvert.SerializeObject(item, Settings());
            var o = JObject.Parse(json);

            var id = (string)o["_id"];
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString();
                o["_id"] = id;
            }

            var path = PathFor<T>(item, id);
            if (PluginConfig.strict && File.Exists(path))
                throw new Exception("Object with " + id + " already exists!");

            Write<T>(path, o, id);
            return Deserialize<T>(o.ToString(Formatting.None));
        }

        public async Task<T> Update<T>(T item) where T : apibase
        {
            if (!PluginConfig.enabled) return item;

            var json = JsonConvert.SerializeObject(item, Settings());
            var o = JObject.Parse(json);

            var id = (string)o["_id"];
            if (string.IsNullOrEmpty(id)) throw new Exception("object is missing an _id");

            var path = PathFor<T>(item, id);

            string previous;
            lock (_lock) { _itemFiles.TryGetValue(id, out previous); }

            if (PluginConfig.strict && previous == null && !File.Exists(path))
                throw new Exception("Object with " + id + " does not exists!");

            // Nama berubah berarti nama file ikut berubah; file lama harus
            // dibuang, kalau tidak akan ada dua salinan objek yang sama
            // dengan _id identik dan FindAll mengembalikan duplikat.
            if (previous != null && !string.Equals(previous, path, StringComparison.OrdinalIgnoreCase))
                DeleteFiles<T>(previous);

            Write<T>(path, o, id);
            return item;
        }

        public async Task Delete<T>(string id) where T : apibase
        {
            if (!PluginConfig.enabled) return;

            string path;
            lock (_lock) { if (!_itemFiles.TryGetValue(id, out path)) path = null; }

            if (path == null || !File.Exists(path))
            {
                if (PluginConfig.strict) throw new Exception("Object with " + id + " does not exists!");
                return;
            }

            if (IsProject<T>())
            {
                var dir = Path.GetDirectoryName(path);
                try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
                catch (Exception ex) { Log.Warning("ProjectFolders: gagal hapus folder project: " + ex.Message); }

                lock (_lock)
                {
                    _projectFolders.Remove(id);
                    foreach (var k in _itemFiles.Where(kv => kv.Value.StartsWith(dir, StringComparison.OrdinalIgnoreCase))
                                                .Select(kv => kv.Key).ToList())
                        _itemFiles.Remove(k);
                }
                return;
            }

            DeleteFiles<T>(path);
            lock (_lock) { _itemFiles.Remove(id); }
        }

        private void DeleteFiles<T>(string metadataPath) where T : apibase
        {
            try
            {
                if (File.Exists(metadataPath)) File.Delete(metadataPath);
                if (IsWorkflow<T>())
                {
                    var xaml = XamlPathFor(metadataPath);
                    if (File.Exists(xaml)) File.Delete(xaml);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("ProjectFolders: gagal hapus " + metadataPath + ": " + ex.Message);
            }
        }

        private string PathFor<T>(T item, string id) where T : apibase
        {
            if (IsProject<T>())
            {
                var projectFolder = EnsureProjectFolder(item, id);
                EnsureStandardFolders(projectFolder);
                return Path.Combine(projectFolder, ProjectFile);
            }

            if (IsWorkflow<T>() || IsDetector<T>() || IsQueue<T>())
            {
                var folder = FolderForProject(GetProjectId(item));

                // Metadata anak masuk .project; akar proyek dibiarkan hanya
                // berisi project.json, entry-points.json, dan berkas .xaml.
                var meta = MetaFolderOf(folder);

                if (IsWorkflow<T>())
                {
                    // Utamakan Filename milik workflow (mis. "Main.xaml") supaya
                    // nama file di disk sama dengan yang dilihat user di OpenRPA.
                    var filename = GetStringProp(item, "Filename");
                    var stem = string.IsNullOrEmpty(filename)
                        ? SafeName(GetStringProp(item, "name"), id)
                        : Path.GetFileNameWithoutExtension(filename);
                    stem = SafeName(stem, id);
                    return Path.Combine(meta, stem + ".workflow.json");
                }

                var suffix = IsDetector<T>() ? ".detector.json" : ".queue.json";
                return Path.Combine(meta, id + suffix);
            }

            var shared = SharedCollection<T>();
            Directory.CreateDirectory(shared);
            return Path.Combine(shared, id + ".json");
        }

        private void Write<T>(string path, JObject o, string id) where T : apibase
        {
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            if (IsWorkflow<T>())
            {
                // XAML ditulis ke file sendiri lalu DIBUANG dari JSON, supaya
                // metadata tetap ringkas dan XAML-nya bisa dibaca/di-diff apa
                // adanya. FindById/FindAll menyatukannya kembali saat membaca.
                var xaml = (string)o["Xaml"];
                File.WriteAllText(XamlPathFor(path), xaml ?? "");
                o.Remove("Xaml");
            }

            if (IsProject<T>()) DescribeProject(o, dir);

            File.WriteAllText(path, o.ToString(Formatting.Indented));

            RemoveSupersededCopy<T>(path, id);

            lock (_lock)
            {
                _itemFiles[id] = path;
                if (IsProject<T>()) _projectFolders[id] = dir;
            }

            // Ringkasan titik masuk ditulis ulang setiap ada perubahan proyek
            // atau workflow, jadi isinya tidak pernah tertinggal dari kenyataan.
            var projectFolder = IsProject<T>() ? dir
                              : (string.Equals(Path.GetFileName(dir), MetaFolder, StringComparison.OrdinalIgnoreCase)
                                 ? Path.GetDirectoryName(dir) : dir);

            if (IsProject<T>() || IsWorkflow<T>()) WriteEntryPoints(projectFolder);
        }

        /// <summary>
        /// Hapus salinan berkas yang sama dari tata letak LAMA.
        ///
        /// Tanpa ini, proyek yang dimigrasi akan punya dua berkas untuk objek
        /// yang sama — satu di akar dan satu di .project — dan keduanya
        /// terindeks, sehingga workflow yang sama muncul dua kali di panel.
        /// </summary>
        private void RemoveSupersededCopy<T>(string path, string id) where T : apibase
        {
            try
            {
                string stale = null;

                if (IsProject<T>())
                {
                    var legacy = Path.Combine(Path.GetDirectoryName(path), LegacyProjectFile);
                    if (File.Exists(legacy)) stale = legacy;
                }
                else if (string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), MetaFolder,
                                       StringComparison.OrdinalIgnoreCase))
                {
                    // Berkas baru ada di .project; pasangan lamanya di akar.
                    var root = Path.GetDirectoryName(Path.GetDirectoryName(path));
                    var legacy = Path.Combine(root, Path.GetFileName(path));
                    if (File.Exists(legacy)) stale = legacy;
                }

                if (stale == null) return;

                File.Delete(stale);
                Log.Debug("ProjectFolders: berkas tata letak lama dihapus setelah migrasi: " + stale);
            }
            catch (Exception ex)
            {
                Log.Warning("ProjectFolders: gagal menghapus berkas lama: " + ex.Message);
            }
        }


        // ------------------------------------------------------------------
        // project.json dan entry-points.json
        // ------------------------------------------------------------------

        /// <summary>
        /// Lengkapi metadata proyek dengan medan bergaya UiPath.
        ///
        /// Medan milik OpenRPA TIDAK diubah satu pun — yang ditambahkan hanya
        /// medan baru di sebelahnya. Itu penting karena berkas yang sama dibaca
        /// balik menjadi entity Project; medan yang tidak dikenal diabaikan
        /// Json.NET, tapi medan yang ditimpa dengan bentuk berbeda akan
        /// menggagalkan pemuatan proyek.
        /// </summary>
        private static void DescribeProject(JObject o, string projectFolder)
        {
            try
            {
                var name = (string)o["name"];
                var id = (string)o["_id"];

                if (o["projectId"] == null) o["projectId"] = id ?? "";
                if (o["description"] == null) o["description"] = "";

                o["main"] = MainWorkflowOf(projectFolder);
                o["schemaVersion"] = "4.0";
                o["studioVersion"] = StudioVersion();
                o["expressionLanguage"] = "VisualBasic";
                o["targetFramework"] = "Legacy";

                if (o["projectVersion"] == null) o["projectVersion"] = "1.0.0";

                if (string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(id)) o["name"] = id;
            }
            catch (Exception ex)
            {
                Log.Warning("ProjectFolders: gagal melengkapi project.json: " + ex.Message);
            }
        }

        private static string StudioVersion()
        {
            try
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return "JakForge " + (v != null ? v.ToString() : "1.0");
            }
            catch (Exception)
            {
                return "JakForge";
            }
        }

        /// <summary>
        /// Workflow utama sebuah proyek. Main.xaml kalau ada — nama yang sama
        /// dipakai UiPath — kalau tidak, berkas .xaml pertama di akar proyek.
        /// </summary>
        private static string MainWorkflowOf(string projectFolder)
        {
            try
            {
                if (File.Exists(Path.Combine(projectFolder, "Main.xaml"))) return "Main.xaml";

                var first = Directory.GetFiles(projectFolder, "*.xaml").OrderBy(x => x).FirstOrDefault();
                return first == null ? "Main.xaml" : Path.GetFileName(first);
            }
            catch (Exception)
            {
                return "Main.xaml";
            }
        }

        /// <summary>
        /// Tulis entry-points.json: daftar workflow yang bisa dijalankan
        /// beserta argumen masuk dan keluarnya.
        ///
        /// Sumbernya metadata workflow, BUKAN hasil membaca XAML. Argumen
        /// sudah tercatat di sana sebagai Parameters, jadi tidak perlu
        /// menguraikan XAML — yang selain lambat juga rapuh terhadap perubahan
        /// format.
        /// </summary>
        private void WriteEntryPoints(string projectFolder)
        {
            try
            {
                if (string.IsNullOrEmpty(projectFolder) || !Directory.Exists(projectFolder)) return;

                var entries = new JArray();

                foreach (var file in MetadataFilesOf(projectFolder))
                {
                    if (!file.EndsWith(".workflow.json", StringComparison.OrdinalIgnoreCase)) continue;

                    JObject wf;
                    try { wf = JObject.Parse(File.ReadAllText(file)); }
                    catch (Exception) { continue; }

                    var filename = (string)wf["Filename"];
                    if (string.IsNullOrEmpty(filename))
                        filename = Path.GetFileNameWithoutExtension(XamlPathFor(file)) + ".xaml";

                    var input = new JArray();
                    var output = new JArray();

                    var parameters = wf["Parameters"] as JArray;
                    if (parameters != null)
                    {
                        foreach (var p in parameters.OfType<JObject>())
                        {
                            var argument = new JObject
                            {
                                ["name"] = (string)p["name"] ?? "",
                                ["type"] = (string)p["type"] ?? "System.String",
                            };

                            var direction = ((string)p["direction"] ?? "in").ToLowerInvariant();
                            if (direction.StartsWith("out")) output.Add(argument); else input.Add(argument);
                        }
                    }

                    entries.Add(new JObject
                    {
                        ["filePath"] = filename,
                        ["uniqueId"] = (string)wf["_id"] ?? "",
                        ["input"] = input,
                        ["output"] = output,
                    });
                }

                var doc = new JObject
                {
                    ["$schema"] = "https://jakforge.local/schema/entry-points.json",
                    ["$id"] = "entry-points.json",
                    ["entryPoints"] = entries,
                };

                File.WriteAllText(Path.Combine(projectFolder, EntryPointsFile), doc.ToString(Formatting.Indented));
            }
            catch (Exception ex)
            {
                Log.Warning("ProjectFolders: gagal menulis entry-points.json: " + ex.Message);
            }
        }

        public void Dispose() { }

#pragma warning restore CS1998
    }
}

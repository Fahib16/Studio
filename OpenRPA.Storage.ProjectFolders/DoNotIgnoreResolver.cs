using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;

namespace OpenRPA.Storage.ProjectFolders
{
    /// <summary>
    /// Saat menyimpan LOKAL kita justru ingin ikut menulis properti yang
    /// ditandai [JsonIgnore] untuk keperluan sinkronisasi ke OpenCore
    /// (isDirty, isLocalOnly, IsExpanded, dan sebagainya) — tanpa itu, status
    /// UI dan status "belum tersinkron" hilang setiap kali OpenRPA ditutup.
    ///
    /// Disalin apa adanya dari OpenRPA.Storage.Filesystem supaya format JSON
    /// yang dihasilkan kedua provider tetap sama, sehingga data masih bisa
    /// dipindah-pindah antar provider.
    /// </summary>
    public class DoNotIgnoreResolver : DefaultContractResolver
    {
        private static readonly string[] KeepThese =
        {
            "isLocalOnly", "isDirty", "isDeleted", "current_version",
            "RelativeFilename", "State", "IsExpanded", "IsSelected"
        };

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            foreach (var name in KeepThese)
            {
                if (property.PropertyName == name) { property.Ignored = false; break; }
            }
            return property;
        }
    }
}

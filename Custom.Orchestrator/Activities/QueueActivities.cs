using System;
using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json.Linq;
using Custom.Orchestrator.Runtime;
using Custom.Shared;

namespace Custom.Orchestrator.Activities
{
    /// <summary>
    /// Masukkan satu butir ke antrean ForgeHub.
    ///
    /// Dipakai proses "pengumpul": membaca sumber data, lalu menaruh satu butir
    /// per transaksi yang harus dikerjakan. Robot lain — bisa beberapa
    /// sekaligus — yang kemudian mengambilnya satu per satu.
    /// </summary>
    [Designer(typeof(AddQueueItemDesigner))]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.addqueueitem.png")]
    [DisplayName("Add Queue Item")]
    [Description("Tambahkan satu butir ke antrean di ForgeHub.")]
    public class AddQueueItem : CodeActivity
    {
        [RequiredArgument, Category("Input")]
        [Description("Nama antrean di ForgeHub.")]
        public InArgument<string> QueueName { get; set; }

        [Category("Input")]
        [Description("Penanda unik butir ini, misalnya nomor faktur. Dipakai ForgeHub untuk menolak kembar.")]
        public InArgument<string> Reference { get; set; }

        [Category("Input")]
        [Description("High, Normal, atau Low. Kosong berarti Normal.")]
        public InArgument<string> Priority { get; set; }

        [Category("Input")]
        [Description("Isi butir. Boleh Dictionary(Of String, Object) atau teks JSON.")]
        public InArgument<object> ItemData { get; set; }

        [Category("Output")]
        public OutArgument<string> ItemId { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var queue = QueueName.Get(context);

            if (string.IsNullOrWhiteSpace(queue))
                throw new ArgumentException("Nama antrean wajib diisi.");

            var body = new JObject
            {
                ["reference"] = Reference.Get(context),
                ["priority"] = Priority.Get(context) ?? "Normal",
                ["content"] = AsJsonText(ItemData.Get(context)),
            };

            var response = HubConnection.Send("POST",
                "/api/queues/" + Uri.EscapeDataString(queue) + "/items", body);

            var id = response == null ? null : (string)response["id"];
            ItemId.Set(context, id);

            RobotLog.Info("Butir '" + (Reference.Get(context) ?? id) + "' masuk antrean " + queue + ".", DisplayName);
        }

        /// <summary>
        /// Ubah apa pun yang diberikan pengguna menjadi teks JSON.
        ///
        /// Dictionary adalah bentuk yang paling nyaman ditulis di kanvas, tapi
        /// orang juga menempelkan JSON mentah dari sistem lain. Keduanya
        /// diterima, dan teks yang sudah berupa JSON tidak dibungkus dua kali.
        /// </summary>
        internal static string AsJsonText(object value)
        {
            if (value == null) return null;

            var text = value as string;

            if (text != null)
            {
                var trimmed = text.Trim();

                if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
                {
                    try
                    {
                        JToken.Parse(trimmed);
                        return trimmed;
                    }
                    catch (Exception)
                    {
                        // Terlihat seperti JSON tapi ternyata bukan; dibungkus
                        // sebagai nilai teks biasa di bawah.
                    }
                }

                return new JObject { ["value"] = text }.ToString();
            }

            var dictionary = value as IDictionary<string, object>;

            if (dictionary != null)
            {
                var result = new JObject();
                foreach (var entry in dictionary)
                    result[entry.Key] = entry.Value == null ? null : JToken.FromObject(entry.Value);

                return result.ToString();
            }

            return JToken.FromObject(value).ToString();
        }
    }

    // ==================================================================

    /// <summary>
    /// Ambil satu butir berikutnya dari antrean.
    ///
    /// ForgeHub menandainya "sedang diproses" dalam langkah yang sama, jadi dua
    /// robot yang bertanya bersamaan tidak akan mendapat butir yang sama.
    ///
    /// Kalau antreannya kosong, keluarannya Nothing — BUKAN kesalahan. Antrean
    /// yang habis adalah keadaan normal, dan itulah cara ReFramework tahu
    /// kapan harus berhenti.
    /// </summary>
    [Designer(typeof(GetQueueItemDesigner))]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.getqueueitem.png")]
    [DisplayName("Get Queue Item")]
    [Description("Ambil satu butir berikutnya dari antrean. Nothing kalau antreannya kosong.")]
    public class GetQueueItem : CodeActivity
    {
        [RequiredArgument, Category("Input")]
        public InArgument<string> QueueName { get; set; }

        [Category("Output")]
        [Description("Butir yang didapat, atau Nothing kalau antreannya kosong.")]
        public OutArgument<QueueItem> Item { get; set; }

        [Category("Output")]
        [Description("True kalau ada butir yang didapat. Jalan pintas untuk If.")]
        public OutArgument<bool> HasItem { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var queue = QueueName.Get(context);

            if (string.IsNullOrWhiteSpace(queue))
                throw new ArgumentException("Nama antrean wajib diisi.");

            var body = new JObject { ["robotName"] = HubConnection.RobotName };

            var response = HubConnection.Send("POST",
                "/api/queues/" + Uri.EscapeDataString(queue) + "/next", body);

            var payload = response == null ? null : response["item"];

            if (payload == null || payload.Type == JTokenType.Null)
            {
                Item.Set(context, null);
                HasItem.Set(context, false);

                RobotLog.Info("Antrean " + queue + " kosong.", DisplayName);
                return;
            }

            var item = new QueueItem
            {
                Id = (string)payload["id"],
                Reference = (string)payload["reference"],
                QueueName = queue,
                Priority = (string)payload["priority"],
                Retries = payload["retries"] == null ? 0 : (int)payload["retries"],
                Content = (string)payload["content"],
            };

            Item.Set(context, item);
            HasItem.Set(context, true);

            RobotLog.Info("Mengambil butir '" + item + "' dari antrean " + queue + ".", DisplayName);
        }
    }

    // ==================================================================

    /// <summary>
    /// Laporkan hasil pemrosesan satu butir antrean.
    ///
    /// Butir yang dilaporkan gagal dikembalikan ForgeHub ke antrean selama jatah
    /// percobaannya belum habis. Jadi kegagalan sementara — jaringan putus,
    /// aplikasi sedang sibuk — tidak menghilangkan transaksinya.
    /// </summary>
    [Designer(typeof(SetQueueItemStatusDesigner))]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.setqueueitemstatus.png")]
    [DisplayName("Set Queue Item Status")]
    [Description("Tandai butir antrean sebagai berhasil, gagal, atau ditinggalkan.")]
    public class SetQueueItemStatus : CodeActivity
    {
        [RequiredArgument, Category("Input")]
        public InArgument<QueueItem> Item { get; set; }

        [Category("Input")]
        [Description("SUCCESSFUL, FAILED, atau ABANDONED. Kosong berarti SUCCESSFUL.")]
        public InArgument<string> Status { get; set; }

        [Category("Input")]
        [Description("Hasil pemrosesan. Boleh Dictionary(Of String, Object) atau teks JSON.")]
        public InArgument<object> OutputData { get; set; }

        [Category("Input")]
        [Description("Keterangan kegagalan. Diisi hanya kalau Status = FAILED.")]
        public InArgument<string> ErrorMessage { get; set; }

        [Category("Output")]
        [Description("True kalau ForgeHub mengembalikan butir ini ke antrean untuk dicoba lagi.")]
        public OutArgument<bool> WillRetry { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var item = Item.Get(context);

            if (item == null || string.IsNullOrEmpty(item.Id))
                throw new ArgumentException("Butir antrean kosong — tidak ada yang bisa dilaporkan.");

            var status = (Status.Get(context) ?? "SUCCESSFUL").Trim().ToUpperInvariant();

            if (status != "SUCCESSFUL" && status != "FAILED" && status != "ABANDONED" && status != "RETRIED")
                throw new ArgumentException("Status '" + status + "' tidak dikenal. "
                    + "Pakai SUCCESSFUL, FAILED, atau ABANDONED.");

            var body = new JObject
            {
                ["status"] = status,
                ["output"] = AddQueueItem.AsJsonText(OutputData.Get(context)),
                ["exception"] = ErrorMessage.Get(context),
            };

            var response = HubConnection.Send("POST",
                "/api/queues/items/" + Uri.EscapeDataString(item.Id) + "/result", body);

            var retried = response != null && response["retried"] != null && (bool)response["retried"];
            WillRetry.Set(context, retried);

            if (retried)
                RobotLog.Warning("Butir '" + item + "' gagal dan dikembalikan ke antrean untuk dicoba lagi.", DisplayName);
            else
                RobotLog.Info("Butir '" + item + "' ditandai " + status + ".", DisplayName);
        }
    }
}

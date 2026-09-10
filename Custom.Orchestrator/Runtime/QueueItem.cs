using System;
using Newtonsoft.Json.Linq;

namespace Custom.Orchestrator.Runtime
{
    /// <summary>
    /// Satu butir antrean yang sudah menjadi milik robot ini.
    ///
    /// Disimpan di variabel workflow di antara "Get Queue Item" dan "Set Queue
    /// Item Status", jadi ia harus bisa dipegang sebagai nilai biasa — bukan
    /// sekadar nomor. Isinya dibaca lewat Get(nama), sehingga workflow tidak
    /// perlu tahu apa pun tentang JSON.
    /// </summary>
    [Serializable]
    public class QueueItem
    {
        public string Id { get; set; }
        public string Reference { get; set; }
        public string QueueName { get; set; }
        public string Priority { get; set; }
        public int Retries { get; set; }

        /// <summary>Isi butir sebagai teks JSON, apa adanya seperti yang disimpan.</summary>
        public string Content { get; set; }

        private JObject _parsed;
        private bool _parseFailed;

        /// <summary>
        /// Ambil satu medan dari isi butir.
        ///
        /// Mengembalikan null kalau medannya tidak ada — bukan melempar. Butir
        /// antrean datang dari sistem lain, dan medan yang kadang kosong adalah
        /// hal biasa; workflow yang memeriksanya dengan If jauh lebih mudah
        /// dibaca daripada yang membungkus tiap pembacaan dengan Try Catch.
        /// </summary>
        public object Get(string field)
        {
            if (string.IsNullOrEmpty(field)) return null;

            var parsed = Parse();
            if (parsed == null) return null;

            JToken value;
            if (!parsed.TryGetValue(field, StringComparison.OrdinalIgnoreCase, out value)) return null;

            switch (value.Type)
            {
                case JTokenType.Null:
                case JTokenType.Undefined: return null;
                case JTokenType.Integer: return (long)value;
                case JTokenType.Float: return (double)value;
                case JTokenType.Boolean: return (bool)value;
                case JTokenType.Date: return (DateTime)value;
                default: return value.ToString();
            }
        }

        /// <summary>Medan sebagai teks; jalan pintas yang paling sering dipakai.</summary>
        public string GetText(string field)
        {
            var value = Get(field);
            return value == null ? null : value.ToString();
        }

        public bool Has(string field)
        {
            var parsed = Parse();
            if (parsed == null) return false;

            JToken value;
            return parsed.TryGetValue(field, StringComparison.OrdinalIgnoreCase, out value);
        }

        private JObject Parse()
        {
            if (_parsed != null || _parseFailed) return _parsed;

            if (string.IsNullOrWhiteSpace(Content)) { _parseFailed = true; return null; }

            try
            {
                _parsed = JObject.Parse(Content);
            }
            catch (Exception)
            {
                // Isi yang bukan JSON tetap bisa dibaca lewat properti Content.
                _parseFailed = true;
            }

            return _parsed;
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Reference) ? (Id ?? "(butir antrean)") : Reference;
        }
    }
}

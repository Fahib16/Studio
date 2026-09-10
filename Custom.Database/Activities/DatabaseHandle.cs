using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace CustomDatabase
{
    /// <summary>
    /// Koneksi basis data yang dipakai bersama oleh activity di dalam satu
    /// Database Scope.
    ///
    /// Semua kueri lewat sini, dan semuanya BERPARAMETER. Itu perbedaan pokok
    /// dari activity basis data lama: di sana parameter tidak pernah didukung
    /// (barisnya ada tapi dikomentari), sehingga satu-satunya cara menyusun
    /// kueri adalah menyambung string. Menyambung string membuat nilai dengan
    /// tanda kutip merusak kuerinya, tanggal bergantung pada pengaturan
    /// wilayah, dan isian dari luar bisa menjadi perintah — tiga kegagalan
    /// yang semuanya hilang dengan parameter.
    /// </summary>
    public sealed class DatabaseHandle : IDisposable
    {
        private readonly DbProviderFactory _factory;
        private readonly DbConnection _connection;
        private DbTransaction _transaction;
        private bool _dibuang;

        public DatabaseHandle(string providerName, string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection String tidak boleh kosong.", "connectionString");

            ProviderName = string.IsNullOrWhiteSpace(providerName)
                ? "System.Data.SqlClient"
                : providerName;

            try
            {
                _factory = DbProviderFactories.GetFactory(ProviderName);
            }
            catch (Exception ex)
            {
                // Pesan bawaannya hanya menyebut "unable to find the requested
                // provider" tanpa memberi tahu apa yang SEBENARNYA tersedia di
                // mesin ini, dan itu pertanyaan pertama semua orang.
                throw new InvalidOperationException(
                    "Provider \"" + ProviderName + "\" tidak terdaftar di mesin ini. " +
                    "Yang tersedia: " + ProviderTersedia() + ".", ex);
            }

            _connection = _factory.CreateConnection();
            _connection.ConnectionString = connectionString;
        }

        public string ProviderName { get; private set; }

        public DbConnection Connection { get { return _connection; } }

        public void Open()
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
        }

        /// <summary>Mulai transaksi; semua perintah sesudahnya ikut di dalamnya.</summary>
        public void BeginTransaction()
        {
            if (_transaction == null) _transaction = _connection.BeginTransaction();
        }

        public void Commit()
        {
            if (_transaction == null) return;
            _transaction.Commit();
            _transaction.Dispose();
            _transaction = null;
        }

        public void Rollback()
        {
            if (_transaction == null) return;
            try { _transaction.Rollback(); }
            catch (Exception) { /* transaksinya bisa saja sudah selesai sendiri */ }
            _transaction.Dispose();
            _transaction = null;
        }

        public DataTable Query(string sql, CommandType type, int timeoutSeconds, IDictionary parameters)
        {
            var hasil = new DataTable();

            using (var cmd = Perintah(sql, type, timeoutSeconds, parameters))
            using (var adapter = _factory.CreateDataAdapter())
            {
                adapter.SelectCommand = cmd;
                adapter.Fill(hasil);
            }

            return hasil;
        }

        public int NonQuery(string sql, CommandType type, int timeoutSeconds, IDictionary parameters)
        {
            using (var cmd = Perintah(sql, type, timeoutSeconds, parameters))
            {
                return cmd.ExecuteNonQuery();
            }
        }

        public object Scalar(string sql, CommandType type, int timeoutSeconds, IDictionary parameters)
        {
            using (var cmd = Perintah(sql, type, timeoutSeconds, parameters))
            {
                var nilai = cmd.ExecuteScalar();

                // DBNull diubah menjadi null di sini, sekali, supaya activity
                // pemanggilnya tidak masing-masing perlu tahu bedanya.
                return nilai == DBNull.Value ? null : nilai;
            }
        }

        /// <summary>
        /// Menulis balik perubahan sebuah DataTable ke tabelnya.
        ///
        /// Perintah Insert/Update/Delete-nya disusun CommandBuilder dari bentuk
        /// tabelnya, jadi tabel yang dituju harus punya kunci primer — tanpa
        /// itu CommandBuilder tidak bisa menyusun klausa WHERE dan melempar
        /// pesan yang tidak menyebut kunci sama sekali.
        /// </summary>
        public int WriteBack(string tableName, DataTable table, int timeoutSeconds)
        {
            if (table == null) throw new ArgumentNullException("table");
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table Name tidak boleh kosong.", "tableName");

            using (var cmd = _factory.CreateCommand())
            {
                cmd.Connection = _connection;
                cmd.Transaction = _transaction;
                cmd.CommandTimeout = timeoutSeconds;
                cmd.CommandText = "SELECT * FROM " + tableName;

                using (var adapter = _factory.CreateDataAdapter())
                {
                    adapter.SelectCommand = cmd;

                    using (var builder = _factory.CreateCommandBuilder())
                    {
                        builder.DataAdapter = adapter;

                        try
                        {
                            adapter.InsertCommand = builder.GetInsertCommand(true);
                            adapter.UpdateCommand = builder.GetUpdateCommand(true);
                            adapter.DeleteCommand = builder.GetDeleteCommand(true);
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException(
                                "Tabel \"" + tableName + "\" tidak bisa ditulis balik. " +
                                "Biasanya karena tabelnya tidak punya kunci primer, " +
                                "sehingga baris yang mana yang harus diubah tidak bisa ditentukan. " +
                                ex.Message, ex);
                        }

                        return adapter.Update(table);
                    }
                }
            }
        }

        private DbCommand Perintah(string sql, CommandType type, int timeoutSeconds, IDictionary parameters)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("Query tidak boleh kosong.", "sql");

            var cmd = _factory.CreateCommand();
            cmd.Connection = _connection;
            cmd.Transaction = _transaction;
            cmd.CommandText = sql;
            cmd.CommandType = type;
            cmd.CommandTimeout = timeoutSeconds > 0 ? timeoutSeconds : 30;

            if (parameters != null)
            {
                foreach (DictionaryEntry entry in parameters)
                {
                    var p = cmd.CreateParameter();

                    // Nama boleh ditulis dengan atau tanpa penanda. Orang menulis
                    // "@nama" di kuerinya, jadi menuliskannya begitu juga di
                    // daftar parameter adalah hal yang wajar dilakukan.
                    var nama = Convert.ToString(entry.Key);
                    p.ParameterName = nama.StartsWith("@") || nama.StartsWith(":") ? nama.Substring(1) : nama;

                    p.Value = entry.Value ?? DBNull.Value;
                    cmd.Parameters.Add(p);
                }
            }

            return cmd;
        }

        private static string ProviderTersedia()
        {
            try
            {
                var nama = new List<string>();
                foreach (DataRow row in DbProviderFactories.GetFactoryClasses().Rows)
                {
                    nama.Add(Convert.ToString(row["InvariantName"]));
                }
                return nama.Count == 0 ? "(tidak ada)" : string.Join(", ", nama.ToArray());
            }
            catch (Exception)
            {
                return "(tidak bisa dibaca)";
            }
        }

        public void Dispose()
        {
            if (_dibuang) return;
            _dibuang = true;

            Rollback();

            try { if (_connection != null) _connection.Close(); }
            catch (Exception) { /* koneksi bisa saja sudah putus duluan */ }

            if (_connection != null) _connection.Dispose();
        }
    }
}

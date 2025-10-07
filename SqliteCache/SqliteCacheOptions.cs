using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using System;

namespace NeoSmart.Caching.Sqlite
{
    public record SqliteCacheOptions : IOptions<SqliteCacheOptions>
    {
        SqliteCacheOptions IOptions<SqliteCacheOptions>.Value => this;

        /// <summary>
        /// Configures SQLite to use a temporary (non-persistent) memory-backed database. Defaults to <c>false</c>.
        /// <br/>
        /// Takes precedence over <see cref="CachePath"/>
        /// </summary>
        public bool MemoryOnly { get; set; } = false;

        private string _cachePath = "SqliteCache.db";
        /// <summary>
        /// The path where the SQLite database should be persisted. Must have read/write permissions; does not need to already exist.
        /// <br/>
        /// Used only if <see cref="MemoryOnly" /> is <c>false</c>.
        /// </summary>
        public string CachePath
        {
            get => _cachePath;
            set
            {
                // User might have passed a connection string instead of a data source
                if (value.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
                {
                    value = value.Replace("Data Source=", "");
                }
                if (value.Contains("=") || value.Contains("\""))
                {
                    throw new ArgumentException("CachePath must be a path and not a connection string!");
                }
                _cachePath = value.Trim();
            }
        }

        /// <summary>
        /// Use this to specify a password for the SqliteConnection that is to be created.
        /// If no <see cref="SqliteEncryptionPassword"/> is set, the connectionStringBuilder will not use the "Password" option.
        /// </summary>
        public string? SqliteEncryptionPassword { get; set; } = null;

        /// <summary>
        /// Specifies how often expired items are removed in the background.
        /// Background eviction is disabled if set to <c>null</c>.
        /// </summary>
        public TimeSpan? CleanupInterval { get; set; } = TimeSpan.FromMinutes(30);

        internal string ConnectionString
        {
            get
            {
                var sb = new SqliteConnectionStringBuilder
                {
                    DataSource = MemoryOnly ? ":memory:" : CachePath,
                    Mode = MemoryOnly ? SqliteOpenMode.Memory : SqliteOpenMode.ReadWriteCreate,
                    Cache = SqliteCacheMode.Shared
                };

                // only set the password option if the user actually set a Password
                if (string.IsNullOrEmpty(SqliteEncryptionPassword))
                {
                    sb.Password = SqliteEncryptionPassword;
                }

                return sb.ConnectionString;
            }
        }
    }
}

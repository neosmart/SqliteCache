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
                if (value.StartsWith("Data Source=", StringComparison.InvariantCultureIgnoreCase))
                {
                    value = value.Replace("Data Source=", "");
                }
                value = value.Trim();
                if (value.Contains("=") || value.Contains("\""))
                {
                    throw new ArgumentException("CachePath must be a path and not a connection string!");
                }
                _cachePath = value;
            }
        }

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

                return sb.ConnectionString;
            }
        }
    }
}

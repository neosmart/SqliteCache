using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using System;

namespace NeoSmart.Caching.Sqlite
{
    public class SqliteCacheOptions : IOptions<SqliteCacheOptions>
    {
        SqliteCacheOptions IOptions<SqliteCacheOptions>.Value => this;

        /// <summary>
        /// Takes precedence over <see cref="CachePath"/>
        /// </summary>
        public bool MemoryOnly { get; set; } = false;

        /// <summary>
        /// Only if <see cref="MemoryOnly" is <c>false</c> />
        /// </summary>
        public string CachePath { get; set; } = "SqliteCache.db";

        /// <summary>
        /// Specifies how often expired items are removed in the background.
        /// Background eviction is disabled if set to <c>null</c>.
        /// </summary>
        public TimeSpan? CleanupInterval { get; set; } = TimeSpan.FromMinutes(30);

        internal string ConnectionString
        {
            get
            {
                var sb = new SqliteConnectionStringBuilder();
                sb.DataSource = MemoryOnly
                    ? ":memory:" : CachePath;
                sb.Mode = MemoryOnly
                    ? SqliteOpenMode.Memory : SqliteOpenMode.ReadWriteCreate;

                return sb.ConnectionString;
            }
        }
    }
}

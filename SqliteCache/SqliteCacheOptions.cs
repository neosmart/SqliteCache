using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoSmart.Caching.Sqlite
{
    public class SqliteCacheOptions : IOptions<SqliteCacheOptions>
    {
        SqliteCacheOptions IOptions<SqliteCacheOptions>.Value => this;
        public ILogger<SqliteCache> Logger { get; set; }

        /// <summary>
        /// Takes precedence over <see cref="CachePath"/>
        /// </summary>
        public bool MemoryOnly { get; set; } = false;

        /// <summary>
        /// Only if <see cref="MemoryOnly" is <c>false</c> />
        /// </summary>
        public string CachePath { get; set; } = "SqliteCache.db";

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

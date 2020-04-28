using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using System;

namespace NeoSmart.Caching.Sqlite
{
    public class SqliteCacheSession : IDisposable
    {
        private SqliteCache _sqliteCache;

        public SqliteCacheSession (IDistributedCache cache)
        {
            if (cache is SqliteCache sqliteCache)
            { 
                _sqliteCache = sqliteCache;
                _sqliteCache.Begin();
            }
        }

        public void Dispose()
        {
            _sqliteCache?.End();
        }
    }
}

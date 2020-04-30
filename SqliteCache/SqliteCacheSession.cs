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
                _sqliteCache = sqliteCache;
        }

        public SqliteCacheSession BeginSession()
        { 
            _sqliteCache?.Begin();
            return this;
        }

        public void EndSession()
        { 
            _sqliteCache?.End();
        }

        public void Dispose()
        {
            EndSession();
        }
    }
}

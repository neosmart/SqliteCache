using System;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NeoSmart.Caching.Sqlite.Tests
{

    [TestClass]
    public class ClearCacheTests : IDisposable
    {
        public static readonly Encoding DefaultEncoding = new UTF8Encoding(false);
        private readonly SqliteCacheOptions Configuration = new SqliteCacheOptions()
        {
            MemoryOnly = false,
            CachePath = $"ClearCache-{Guid.NewGuid()}.db",
        };

        public void Dispose()
        {
            var logger = new TestLogger<SqliteCache>();
            logger.LogInformation("Delete db at path {DbPath}", Configuration.CachePath);
            try
            {
                System.IO.File.Delete(Configuration.CachePath);
            }
            catch(Exception ex)
            {
                logger.LogWarning(ex, "Unable to delete db file at {DbPath}", Configuration.CachePath);
            }
        }

        private SqliteCache CreateDefault(bool persistent = false)
        {
            var logger = new TestLogger<SqliteCache>();
            logger.LogInformation("Creating a connection to db {DbPath}", Configuration.CachePath);
            var cacheDb = new SqliteCache(Configuration with { MemoryOnly = !persistent }, logger);

            return cacheDb;
        }

        [TestMethod]
        public void ClearCacheTest()
        {
            using (var cache = CreateDefault(true))
            {
                cache.Set("one", DefaultEncoding.GetBytes("foo"), new DistributedCacheEntryOptions().SetAbsoluteExpiration(DateTimeOffset.UtcNow.AddDays(1)));

                cache.Set("two", DefaultEncoding.GetBytes("bar"), new DistributedCacheEntryOptions().SetAbsoluteExpiration(DateTimeOffset.UtcNow.AddDays(1)));

                // Test and check
                cache.Clear();

                var item1 = cache.Get("one");
                Assert.IsNull(item1);

                var item2 = cache.Get("two");
                Assert.IsNull(item2);
            }

            // Check persistence
            using (var cache = CreateDefault(true))
            {
                var bytes = cache.Get("firstItem");
                Assert.IsNull(bytes);

                bytes = cache.Get("secondItem");
                Assert.IsNull(bytes);
            }
        }
    }
}


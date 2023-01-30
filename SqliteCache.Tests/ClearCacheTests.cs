using System;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeoSmart.Caching.Sqlite.Tests
{
    [TestClass]
    public class ClearCacheTests : IDisposable
    {
        private readonly SqliteCacheOptions Configuration = new SqliteCacheOptions()
        {
            MemoryOnly = false,
            CachePath = $"ClearCache-{Guid.NewGuid()}.db",
        };

        public void Dispose()
        {
            var logger = new TestLogger<ClearCacheTests>();
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
        public void ItemsRemovedAfterClear()
        {
            using (var cache = CreateDefault(true))
            {
                var expiry = new DistributedCacheEntryOptions().SetAbsoluteExpiration(DateTimeOffset.UtcNow.AddDays(1));
                cache.SetString("one", "foo", expiry);
                cache.SetString("two", "bar", expiry);

                Assert.AreEqual(cache.GetString("one"), "foo");
                Assert.AreEqual(cache.GetString("two"), "bar");

                // Test and check
                cache.Clear();

                var item1 = cache.Get("one");
                Assert.IsNull(item1);

                var item2 = cache.Get("two");
                Assert.IsNull(item2);
            }
        }
    }
}


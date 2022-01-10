using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeoSmart.Caching.Sqlite.Tests
{
    [TestClass]
    public class BulkInsertTests : IDisposable
    {
        public static readonly Encoding DefaultEncoding = new UTF8Encoding(false);
        private readonly SqliteCacheOptions Configuration = new SqliteCacheOptions()
        {
            MemoryOnly = false,
            CachePath = $"BulkInsert-{Guid.NewGuid()}.db",
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
        public async Task BasicBulkTests()
        {
            using (var cache = CreateDefault(true))
            {
                var item1 = cache.Get("firstItem");
                Assert.IsNull(item1);

                var item2 = cache.Get("secondItem");
                Assert.IsNull(item2);

                List<KeyValuePair<string, byte[]>> testObject = new List<KeyValuePair<string, byte[]>>
                {
                    new KeyValuePair<string, byte[]>("firstItem", DefaultEncoding.GetBytes("test one")),
                    new KeyValuePair<string, byte[]>("secondItem", DefaultEncoding.GetBytes("test two"))
                };

                await cache.SetBulkAsync(testObject, new DistributedCacheEntryOptions()
                {
                    AbsoluteExpiration = DateTimeOffset.UtcNow.AddDays(1)
                });

                item1 = cache.Get("firstItem");
                Assert.IsNotNull(item1);
                CollectionAssert.AreEqual(item1, DefaultEncoding.GetBytes("test one"));

                item2 = cache.Get("secondItem");
                Assert.IsNotNull(item2);
                CollectionAssert.AreEqual(item2, DefaultEncoding.GetBytes("test two"));
            }

            // Check persistence
            using (var cache = CreateDefault(true))
            {
                var bytes = await cache.GetAsync("firstItem");
                Assert.IsNotNull(bytes);
                CollectionAssert.AreEqual(bytes, DefaultEncoding.GetBytes("test one"));

                bytes = await cache.GetAsync("secondItem");
                Assert.IsNotNull(bytes);
                CollectionAssert.AreEqual(bytes, DefaultEncoding.GetBytes("test two"));
            }
        }

        [TestMethod]
        public async Task MultipleBulkCalls()
        {
            using (var cache = CreateDefault())
            {
                var item1 = cache.Get("firstItem");
                Assert.IsNull(item1);

                var item2 = cache.Get("secondItem");
                Assert.IsNull(item2);

                List<KeyValuePair<string, byte[]>> testObject = new List<KeyValuePair<string, byte[]>>
                {
                    new KeyValuePair<string, byte[]>("firstItem", DefaultEncoding.GetBytes("test one"))
                };

                await cache.SetBulkAsync(testObject, new DistributedCacheEntryOptions()
                {
                    AbsoluteExpiration = DateTimeOffset.UtcNow.AddDays(1)
                });

                testObject[0] = new KeyValuePair<string, byte[]>("secondItem", DefaultEncoding.GetBytes("test two"));

                await cache.SetBulkAsync(testObject, new DistributedCacheEntryOptions()
                {
                    AbsoluteExpiration = DateTimeOffset.UtcNow.AddDays(1)
                });

                item1 = cache.Get("firstItem");
                Assert.IsNotNull(item1);

                CollectionAssert.AreEqual(item1, DefaultEncoding.GetBytes("test one"));

                item2 = cache.Get("secondItem");
                Assert.IsNotNull(item2);

                CollectionAssert.AreEqual(item2, DefaultEncoding.GetBytes("test two"));
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeoSmart.Caching.Sqlite.Tests
{
    [TestClass]
    public class BulkInsertTests
    {
        public Encoding DefaultEncoding = new UTF8Encoding(false);
        SqliteCacheOptions Configuration => new SqliteCacheOptions()
        {
            MemoryOnly = false,
            CachePath = "bulktest.db",
        };

        public BulkInsertTests()
        {
        }

        private SqliteCache CreateDefault()
        {
            var logger = new TestLogger<SqliteCache>();
            var cacheDb = new SqliteCache(Configuration, logger);

            return cacheDb;
        }

        [TestMethod]
        public async Task BasicBulkTests()
        {
            System.IO.File.Delete(Configuration.CachePath);

            using (var cache = CreateDefault())
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
            using (var cache = CreateDefault())
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
            System.IO.File.Delete(Configuration.CachePath);

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

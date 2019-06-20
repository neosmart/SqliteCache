using Microsoft.Extensions.Caching.Distributed;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoSmart.SqliteCache;
using System;
using System.Text;
using System.Threading.Tasks;

namespace SqliteCacheTests
{
    [TestClass]
    public class UnitTest1
    {
        public Encoding DefaultEncoding = new UTF8Encoding(false);
        Configuration Configuration => new Configuration()
        {
            MemoryOnly = false,
            CachePath = "test.db",
        };

        public UnitTest1()
        {
            SQLitePCL.Batteries.Init();
        }

        private async Task<SqliteCache> CreateDefaultAsync()
        {
            var logger = new TestLogger<SqliteCache>();
            var cacheDb = new NeoSmart.SqliteCache.SqliteCache(Configuration, logger);
            await cacheDb.ConnectAsync(default);

            return cacheDb;
        }

        [TestMethod]
        public async Task BasicTests()
        {
            System.IO.File.Delete(Configuration.CachePath);

            using (var cache = await CreateDefaultAsync())
            {
                var bytes = cache.Get("hello");
                Assert.IsNull(bytes);

                cache.Set("hello", DefaultEncoding.GetBytes("hello"), new DistributedCacheEntryOptions()
                {
                    AbsoluteExpiration = DateTimeOffset.UtcNow.AddDays(1)
                });

                bytes = cache.Get("hello");
                Assert.IsNotNull(bytes);

                CollectionAssert.AreEqual(bytes, DefaultEncoding.GetBytes("hello"));
            }

            // Check persistence
            using (var cache = await CreateDefaultAsync())
            {
                await cache.ConnectAsync(default);

                var bytes = await cache.GetAsync("hello");
                CollectionAssert.AreEqual(bytes, DefaultEncoding.GetBytes("hello"));
            }
        }

        [TestMethod]
        public async Task ExpiredIgnored()
        {
            using (var cache = await CreateDefaultAsync())
            {
                cache.Set("hi there", DefaultEncoding.GetBytes("hello"),
                    new DistributedCacheEntryOptions()
                    .SetAbsoluteExpiration(DateTimeOffset.UtcNow.AddDays(-1)));

                Assert.IsNull(cache.Get("hi there"));
            }
        }

        [TestMethod]
        public async Task ExpiredRenewal()
        {
            using (var cache = await CreateDefaultAsync())
            {
                cache.Set("hi there", DefaultEncoding.GetBytes("hello"),
                    new DistributedCacheEntryOptions()
                    {
                        AbsoluteExpiration = DateTimeOffset.UtcNow.AddDays(-1),
                        SlidingExpiration = TimeSpan.FromDays(2),
                    });

                Assert.IsNotNull(cache.Get("hi there"));
            }
        }
    }
}

using global::NeoSmart.SqliteCache;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text;
using System.Threading.Tasks;

namespace NeoSmart.SqliteCache.Tests
{

    [TestClass]
    public class SqliteCacheTests
    {
        public Encoding DefaultEncoding = new UTF8Encoding(false);
        SqliteCacheOptions Configuration => new SqliteCacheOptions()
        {
            MemoryOnly = false,
            CachePath = "test.db",
        };

        public SqliteCacheTests()
        {
        }

        private SqliteCache CreateDefault()
        {
            var logger = new TestLogger<SqliteCache>();
            var cacheDb = new SqliteCache(Configuration, logger);

            return cacheDb;
        }

        [TestMethod]
        public async Task BasicTests()
        {
            System.IO.File.Delete(Configuration.CachePath);

            using (var cache = CreateDefault())
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
            using (var cache = CreateDefault())
            {
                var bytes = await cache.GetAsync("hello");
                CollectionAssert.AreEqual(bytes, DefaultEncoding.GetBytes("hello"));
            }
        }

        [TestMethod]
        public void ExpiredIgnored()
        {
            using (var cache = CreateDefault())
            {
                cache.Set("hi there", DefaultEncoding.GetBytes("hello"),
                    new DistributedCacheEntryOptions()
                    .SetAbsoluteExpiration(DateTimeOffset.UtcNow.AddDays(-1)));

                Assert.IsNull(cache.Get("hi there"));
            }
        }

        [TestMethod]
        public void ExpiredRenewal()
        {
            using (var cache = CreateDefault())
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

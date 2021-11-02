using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeoSmart.Caching.Sqlite.Tests
{
    [TestClass]
    public class SqliteCacheTests
    {
        public Encoding DefaultEncoding = new UTF8Encoding(false);
        private readonly SqliteCacheOptions Configuration = new SqliteCacheOptions()
        {
            MemoryOnly = false,
            CachePath = $"test-{Guid.NewGuid()}.db",
        };

        [TestCleanup]
        public void DeleteTestDb()
        {
            System.IO.File.Delete(Configuration.CachePath);
        }

        private SqliteCache CreateDefault(bool persistent = true)
        {
            var logger = new TestLogger<SqliteCache>();
            var cacheDb = new SqliteCache(Configuration with { MemoryOnly = !persistent }, logger);

            return cacheDb;
        }

        [TestMethod]
        public async Task BasicTests()
        {
            using (var cache = CreateDefault(true))
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
            using (var cache = CreateDefault(true))
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

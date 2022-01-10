using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeoSmart.Caching.Sqlite.Tests
{
    [TestClass]
    public class BasicTests : IDisposable
    {
        public Encoding DefaultEncoding = new UTF8Encoding(false);
        private readonly SqliteCacheOptions Configuration = new SqliteCacheOptions()
        {
            MemoryOnly = false,
            CachePath = $"BasicTests-{Guid.NewGuid()}.db",
        };

        public void Dispose()
        {
            var logger = new TestLogger<SqliteCache>();
            logger.LogInformation("Delete db at path {DbPath}", Configuration.CachePath);
            System.IO.File.Delete(Configuration.CachePath);
        }

        private SqliteCache CreateDefault(bool persistent = true)
        {
            var logger = new TestLogger<SqliteCache>();
            logger.LogInformation("Creating a connection to db {DbPath}", Configuration.CachePath);
            var cacheDb = new SqliteCache(Configuration with { MemoryOnly = !persistent }, logger);

            return cacheDb;
        }

        [TestMethod]
        public async Task BasicSetGet()
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

        [TestMethod]
        public void ExpirationStoredInUtc()
        {
            var expiryUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            var expiryLocal = expiryUtc.ToOffset(TimeSpan.FromHours(5));

            using (var cache = CreateDefault())
            {
                cache.Set("key", DefaultEncoding.GetBytes("value"), new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = expiryLocal,
                });

                Assert.IsNull(cache.Get("key"));
            }
        }
    }
}

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
        [AssemblyInitialize]
        public static void SetSqliteProvider(TestContext _)
        {
            // SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());
            SQLitePCL.Batteries_V2.Init();
        }

        public static readonly Encoding DefaultEncoding = new UTF8Encoding(false);
        private readonly SqliteCacheOptions Configuration = new SqliteCacheOptions()
        {
            MemoryOnly = false,
            CachePath = $"BasicTests-{Guid.NewGuid()}.db",
        };

        public void Dispose()
        {
            var logger = new TestLogger<SqliteCache>();
            logger.LogInformation("Delete db at path {DbPath}", Configuration.CachePath);
            try
            {
                System.IO.File.Delete(Configuration.CachePath);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Unable to delete db file at {DbPath}", Configuration.CachePath);
            }
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

        [TestMethod]
        public void DoubleDispose()
        {
            using (var cache = CreateDefault(true))
            {
                cache.Dispose();
            }
        }

#if NETCOREAPP3_0_OR_GREATER
        [TestMethod]
        public async Task AsyncDispose()
        {
            await using (var cache = CreateDefault(true))
            {
                await cache.SetAsync("foo", DefaultEncoding.GetBytes("hello"));
                var bytes = await cache.GetAsync("foo");
                CollectionAssert.AreEqual(bytes, DefaultEncoding.GetBytes("hello"));
            }
        }
#endif
    }
}

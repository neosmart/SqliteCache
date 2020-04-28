using System;
using System.Diagnostics;
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
        public void TestTransactionUsageSpeed()
        {          
            Stopwatch sw = new Stopwatch();

            Random rnd = new Random();

            long noTransaction, withTransaction;

            using (var cache = CreateDefault())
            {
                sw.Start();

                for (int i=0; i < 1000; i++)
                { 
                    cache.SetString(rnd.Next().ToString(), rnd.Next().ToString());
                }

                sw.Stop();

                noTransaction = sw.ElapsedMilliseconds;
            }

            System.IO.File.Delete(Configuration.CachePath);

            using (var cache = CreateDefault())
            {
                sw.Restart();

                using (var session = new SqliteCacheSession(cache))
                { 
                    for (int i=0; i < 1000; i++)
                    { 
                        cache.SetString(rnd.Next().ToString(), rnd.Next().ToString());
                    }
                }

                sw.Stop();

                withTransaction = sw.ElapsedMilliseconds;
            }

            // at least 100 times faster
            Assert.IsTrue(noTransaction > 100*withTransaction);
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

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace NeoSmart.Caching.Sqlite.Tests
{
    [TestClass]
    public class DependencyInjection
    {
        public IServiceProvider CreateServices()
        {
            var builder = new ServiceCollection();
            builder.AddSqliteCache("test.db");

            return builder.BuildServiceProvider();
        }

        [TestMethod]
        public void TestInterfaceInjection()
        {
            var services = CreateServices();
            var cache = services.GetRequiredService<IDistributedCache>();
            Assert.IsInstanceOfType(cache, typeof(SqliteCache));
        }

        [TestMethod]
        public void TestTypeInjection()
        {
            var services = CreateServices();
            var cache = services.GetRequiredService<SqliteCache>();
            Assert.IsInstanceOfType(cache, typeof(SqliteCache));
        }

        /// <summary>
        /// Verify that `AddSqliteCache()` causes service lookups for both <c>IDistributedCache</c>
        /// and lookups for <c>SqliteCache</c> to return the same singleton instance and not two
        /// separate instances of <c>SqliteCache</c>.
        /// </summary>
        [TestMethod]
        public void TestInjectionSameness()
        {
            var services = CreateServices();
            Assert.AreSame(services.GetRequiredService<SqliteCache>(), services.GetRequiredService<IDistributedCache>());
        }
    }
}

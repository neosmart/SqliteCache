using System;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace NeoSmart.Caching.Sqlite.AspNetCore
{
    public static class SqliteCacheServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <c>SqliteCache</c> as a dependency-injected singleton, available
        /// both as <c>IDistributedCache</c> and <c>SqliteCache</c>.
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddSqliteCache(this IServiceCollection services,
            Action<SqliteCacheOptions> setupAction)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }
            else if (setupAction is null)
            {
                throw new ArgumentNullException(nameof(setupAction));
            }

            SQLitePCL.Batteries_V2.Init();
            services.AddOptions();
            services.AddSingleton<SqliteCache>();
            services.AddSingleton<IDistributedCache, SqliteCache>(services => services.GetRequiredService<SqliteCache>());
            services.Configure(setupAction);
            return services;
        }

        /// <summary>
        /// Registers <c>SqliteCache</c> as a dependency-injected singleton, available
        /// both as <c>IDistributedCache</c> and <c>SqliteCache</c>.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="path">The path where the SQLite database should be stored. It
        /// is created if it does not exist. (The path should be a file path, not a
        /// directory. Make sure the application has RW access at runtime.)</param>
        /// <returns></returns>
        public static IServiceCollection AddSqliteCache(this IServiceCollection services,
            string path)
        {
            return AddSqliteCache(services, options => options.CachePath = path);
        }
    }
}

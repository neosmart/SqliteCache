using System;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NeoSmart.Caching.Sqlite
{
    public static class SqliteCacheServiceCollectionExtensions
    {
        public static IServiceCollection AddSqliteCache(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddOptions();
            services.AddSingleton<IDistributedCache, SqliteCache>();
            return services;
        }

        public static IServiceCollection AddSqliteCache(this IServiceCollection services,
            Action<SqliteCacheOptions> setupAction)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (setupAction == null)
            {
                throw new ArgumentNullException(nameof(setupAction));
            }

            services.AddSqliteCache();
            services.Configure(setupAction);
            return services;
        }
    }
}

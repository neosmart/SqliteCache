using System;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NeoSmart.SqliteCache
{
    public static class SqliteCacheServiceCollectionExtensions
    {
        public static IServiceCollection AddSqliteCache(this IServiceCollection services,
            Action<SqliteCacheOptions> optionsConfig)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (optionsConfig == null)
            {
                throw new ArgumentNullException(nameof(optionsConfig));
            }

            services.AddOptions();
            services.Configure(optionsConfig);
            services.AddSingleton<IDistributedCache, SqliteCache>();

            return services;
        }
    }
}

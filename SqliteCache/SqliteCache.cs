using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using DbConnection = Microsoft.Data.Sqlite.SqliteConnection;
using DbCommand = Microsoft.Data.Sqlite.SqliteCommand;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace NeoSmart.Caching.Sqlite
{
    public sealed class SqliteCache : IDistributedCache, IDisposable
#if NETCOREAPP3_1_OR_GREATER
        , IAsyncDisposable
#endif
    {
        public const int SchemaVersion = 1;

        private readonly SqliteCacheOptions _config;
        private readonly ILogger _logger;
        private readonly Timer? _cleanupTimer;
        private readonly DbConnection _db;

        private DbCommandPool Commands { get; }

        static SqliteCache()
        {
            // SQLitePCL.Batteries.Init();
        }

        public SqliteCache(IOptions<SqliteCacheOptions> options, ILogger<SqliteCache>? logger = null)
            : this(options.Value, logger)
        {
        }

        public SqliteCache(SqliteCacheOptions options, ILogger<SqliteCache>? logger = null)
        {
            _config = options;
            _logger = logger ?? new NullLogger<SqliteCache>();

            _db = Connect(_config, _logger);
            Commands = new DbCommandPool(_db, _logger);

            // This has to be after the call to Connect()
            if (_config.CleanupInterval.HasValue)
            {
                _cleanupTimer = new Timer(_ =>
                {
                    _logger.LogTrace("Beginning background cache cleanup");
                    RemoveExpired();
                    _logger.LogTrace("Completed background cache cleanup");
                }, null, TimeSpan.Zero, _config.CleanupInterval.Value);
            }
        }

        public void Dispose()
        {
            _logger.LogTrace("Disposing SQLite cache database at {SqliteCacheDbPath}", _config.CachePath);
            _cleanupTimer?.Dispose();
            Commands.Dispose();

            _logger.LogTrace("Closing connection to SQLite database at {SqliteCacheDbPath}", _config.CachePath);
            _db.Close();
            _db.Dispose();
        }

#if NETCOREAPP3_0_OR_GREATER
        public async ValueTask DisposeAsync()
        {
            if (_cleanupTimer is not null)
            {
                await _cleanupTimer.DisposeAsync();
            }

            await Commands.DisposeAsync();

            _logger.LogTrace("Closing connection to SQLite database at {SqliteCacheDbPath}", _config.CachePath);
            await _db.CloseAsync();
            await _db.DisposeAsync();
        }
#endif

        #region Database Connection Initialization
        private static bool CheckExistingDb(DbConnection db, ILogger logger)
        {
            try
            {
                // Check for correct structure
                using (var cmd = new DbCommand(@"SELECT COUNT(*) from sqlite_master", db))
                {
                    var result = (long)cmd.ExecuteScalar()!;
                    // We are expecting two tables and one additional index
                    if (result != 3)
                    {
                        logger.LogWarning("Incorrect/incompatible existing cache db structure found!");
                        return false;
                    }
                }

                // Check for correct version
                using (var cmd = new DbCommand("SELECT value FROM meta WHERE key = 'version'", db))
                {
                    var result = (long)cmd.ExecuteScalar()!;
                    if (result != SchemaVersion)
                    {
                        logger.LogWarning("Existing cache db has unsupported schema version {SchemaVersion}",
                            result);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while checking compatibility of existing cache db!");
                return false;
            }

            return true;
        }

        private static DbConnection Connect(SqliteCacheOptions config, ILogger logger)
        {
            var connectionString = config.ConnectionString;
            logger.LogTrace("Opening connection to SQLite database: " +
                "{ConnectionString}", connectionString);

            DbConnection? db = null;

            // First try to open an existing database
            if (!config.MemoryOnly && System.IO.File.Exists(config.CachePath))
            {
                logger.LogTrace("Found existing database at {CachePath}", config.CachePath);

                db = new DbConnection(config.ConnectionString);
                db.Open();

                if (!CheckExistingDb(db, logger))
                {
                    logger.LogTrace("Closing connection to SQLite database at {SqliteCacheDbPath}", config.CachePath);
                    db.Close();
                    db.Dispose();
                    db = null;

                    logger.LogInformation("Deleting existing incompatible cache db file {CachePath}", config.CachePath);
                    System.IO.File.Delete(config.CachePath);
                }
            }

            if (db is null)
            {
                db = new DbConnection(config.ConnectionString);
                db.Open();
                Initialize(config, db, logger);
            }

            // Explicitly set default journal mode and fsync behavior
            using (var cmd = new DbCommand("PRAGMA journal_mode = WAL;", db))
            {
                cmd.ExecuteNonQuery();
            }
            using (var cmd = new DbCommand("PRAGMA synchronous = NORMAL;", db))
            {
                cmd.ExecuteNonQuery();
            }

            return db;
        }

        private static void Initialize(SqliteCacheOptions config, DbConnection db, ILogger logger)
        {
            logger.LogInformation("Initializing db cache: {ConnectionString}",
                config.ConnectionString);

            using (var transaction = db.BeginTransaction())
            {
                using (var cmd = new DbCommand(Resources.TableInitCommand, db))
                {
                    cmd.Transaction = transaction;
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = new DbCommand(
                    $"INSERT INTO meta (key, value) " +
                    $"VALUES " +
                    $"('version', {SchemaVersion}), " +
                    $"('created', {DateTimeOffset.UtcNow.Ticks})", db))
                {
                    cmd.Transaction = transaction;
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }
        }

        // Some day, Microsoft will deign it useful to add async service initializers and we can
        // bring this code back to the light of day.
#if false
        private async Task<bool> CheckExistingDbAsync(DbConnection db, CancellationToken cancel)
        {
            try
            {
                // Check for correct structure
                using (var cmd = new DbCommand(@"SELECT COUNT(*) from sqlite_master", db))
                {
                    var result = (long)await cmd.ExecuteScalarAsync(cancel);
                    // We are expecting two tables and one additional index
                    if (result != 3)
                    {
                        _logger.LogWarning("Incorrect/incompatible existing cache db structure found!");
                        return false;
                    }
                }

                // Check for correct version
                using (var cmd = new DbCommand("SELECT value FROM meta WHERE key = 'version'", db))
                {
                    var result = (long)await cmd.ExecuteScalarAsync(cancel);
                    if (result != SchemaVersion)
                    {
                        _logger.LogWarning("Existing cache db has unsupported schema version {SchemaVersion}",
                            result);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while checking compatibilty of existing cache db!");
                return false;
            }

            return true;
        }
        public async ValueTask ConnectAsync(CancellationToken cancel)
        {
            if (_db == null)
            {
                var connectionString = _config.ConnectionString;
                _logger.LogTrace("Opening connection to SQLite database: " +
                    "{ConnectionString}", connectionString);

                // First try to open an existing database
                if (!_config.MemoryOnly && System.IO.File.Exists(_config.CachePath))
                {
                    _logger.LogTrace("Found existing database at {CachePath}", _config.CachePath);

                    var db = new SqliteConnection(_config.ConnectionString);
                    await db.OpenAsync();
                    if (await CheckExistingDbAsync(db, cancel))
                    {
                        // Everything checks out, we can use this as our cache db
                        _db = db;
                    }
                    else
                    {
                        db?.Dispose();
                        db?.Close();

                        _logger.LogInformation("Deleting existing incompatible cache db file {CachePath}", _config.CachePath);
                        System.IO.File.Delete(_config.CachePath);
                    }
                }

                if (_db == null)
                {
                    _db = new DbConnection(_config.ConnectionString);
                    await _db.OpenAsync();
                    await InitializeAsync(cancel);
                }

                Commands = new DbCommandPool(_db, _logger);
            }
        }

        private async Task InitializeAsync(CancellationToken cancel)
        {
            _logger.LogInformation("Initializing db cache: {ConnectionString}",
                _config.ConnectionString);

            using (var transaction = _db.BeginTransaction())
            {
                using (var cmd = new DbCommand(Resources.TableInitCommand, _db))
                {
                    cmd.Transaction = transaction;
                    await cmd.ExecuteNonQueryAsync(cancel);
                }
                using (var cmd = new DbCommand(
                    $"INSERT INTO meta (key, value) " +
                    $"VALUES " +
                    $"('version', {SchemaVersion}), " +
                    $"('created', {DateTimeOffset.UtcNow.Ticks})" , _db))
                {
                    cmd.Transaction = transaction;
                    await cmd.ExecuteNonQueryAsync(cancel);
                }
                transaction.Commit();
            }
        }
#endif
        #endregion

        public byte[] Get(string key)
        {
            return (byte[])Commands.Use(Operation.Get, cmd =>
            {
                cmd.Parameters.AddWithValue("@key", key);
                cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.Ticks);
                return cmd.ExecuteScalar();
            })!;
        }

        public async Task<byte[]> GetAsync(string key, CancellationToken cancel = default)
        {
            return (byte[])(await Commands.UseAsync(Operation.Get, cmd =>
            {
                cmd.Parameters.AddWithValue("@key", key);
                cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.Ticks);
                return cmd.ExecuteScalarAsync(cancel);
            }))!;
        }

        public void Refresh(string key)
        {
            Commands.Use(Operation.Refresh, cmd =>
            {
                cmd.Parameters.AddWithValue("@key", key);
                cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.Ticks);
                return cmd.ExecuteScalar();
            });
        }

        public Task RefreshAsync(string key, CancellationToken cancel = default)
        {
            return Commands.UseAsync(Operation.Refresh, cmd =>
            {
                cmd.Parameters.AddWithValue("@key", key);
                cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.Ticks);
                return cmd.ExecuteScalarAsync(cancel);
            });
        }

        public void Remove(string key)
        {
            Commands.Use(Operation.Remove, cmd =>
            {
                cmd.Parameters.AddWithValue("@key", key);
                cmd.ExecuteNonQuery();
            });
        }

        public Task RemoveAsync(string key, CancellationToken cancel = default)
        {
            return Commands.UseAsync(Operation.Remove, cmd =>
            {
                cmd.Parameters.AddWithValue("@key", key);
                return cmd.ExecuteNonQueryAsync(cancel);
            });
        }

        private void CreateForSet(DbCommand cmd, string key, byte[] value, DistributedCacheEntryOptions options)
        {
            cmd.Parameters.AddWithValue("@key", key);
            cmd.Parameters.AddWithValue("@value", value);

            AddExpirationParameters(cmd, options);
        }

        private void CreateBulkInsert(DbCommand cmd, IEnumerable<KeyValuePair<string, byte[]>> keyValues, DistributedCacheEntryOptions options)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(DbCommands.Commands[(int)Operation.BulkInsert]);
            int i = 0;
            foreach (var pair in keyValues)
            {
                sb.Append($"(@key{i}, @value{i}, @expiry, @renewal),");
                cmd.Parameters.AddWithValue($"@key{i}", pair.Key);
                cmd.Parameters.AddWithValue($"@value{i}", pair.Value);
                i++;
            }
            sb.Remove(sb.Length - 1, 1);
            sb.Append(";");

            AddExpirationParameters(cmd, options);

            cmd.CommandText = sb.ToString();
        }

        public void Clear()
        {
            Commands.Use(conn =>
            {
                using var cmd = new DbCommand("DELETE FROM cache WHERE 1=1;", conn);
                cmd.ExecuteNonQuery();
                return true;
            });
        }

        public Task ClearAsync(CancellationToken cancel = default)
        {
            return Commands.UseAsync(async conn =>
            {
                using var cmd = new DbCommand("DELETE FROM cache WHERE 1=1;", conn);
                await cmd.ExecuteNonQueryAsync(cancel);
                return true;
            });
        }

        private void AddExpirationParameters(DbCommand cmd, DistributedCacheEntryOptions options)
        {
            DateTimeOffset? expiry = null;
            TimeSpan? renewal = null;

            if (options.AbsoluteExpiration.HasValue)
            {
                expiry = options.AbsoluteExpiration.Value.ToUniversalTime();
            }
            else if (options.AbsoluteExpirationRelativeToNow.HasValue)
            {
                expiry = DateTimeOffset.UtcNow
                    .Add(options.AbsoluteExpirationRelativeToNow.Value);
            }

            if (options.SlidingExpiration.HasValue)
            {
                renewal = options.SlidingExpiration.Value;
                expiry = (expiry ?? DateTimeOffset.UtcNow) + renewal;
            }

            cmd.Parameters.AddWithValue("@expiry", expiry?.Ticks ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@renewal", renewal?.Ticks ?? (object)DBNull.Value);
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            Commands.Use(Operation.Insert, cmd =>
            {
                CreateForSet(cmd, key, value, options);
                cmd.ExecuteNonQuery();
            });
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
            CancellationToken cancel = default)
        {
            return Commands.UseAsync(Operation.Insert, cmd =>
            {
                CreateForSet(cmd, key, value, options);
                return cmd.ExecuteNonQueryAsync(cancel);
            });
        }

        public void SetBulk(IEnumerable<KeyValuePair<string, byte[]>> keyValues, DistributedCacheEntryOptions options)
        {
            if (keyValues is null || !keyValues.Any())
            {
                return;
            }

            Commands.Use(Operation.BulkInsert, cmd =>
            {
                CreateBulkInsert(cmd, keyValues, options);
                return cmd.ExecuteNonQuery();
            });
        }

        public Task SetBulkAsync(IEnumerable<KeyValuePair<string, byte[]>> keyValues, DistributedCacheEntryOptions options,
            CancellationToken cancel = default)
        {
            if (keyValues is null || !keyValues.Any())
            {
                return Task.CompletedTask;
            }

            return Commands.UseAsync(Operation.BulkInsert, cmd =>
            {
                CreateBulkInsert(cmd, keyValues, options);
                return cmd.ExecuteNonQueryAsync(cancel);
            });
        }

        public void RemoveExpired()
        {
            var removed = (long)Commands.Use(Operation.RemoveExpired, cmd =>
            {
                cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.Ticks);
                return cmd.ExecuteScalar();
            })!;

            if (removed > 0)
            {
                _logger.LogTrace("Evicted {DeletedCacheEntryCount} expired entries from cache", removed);
            }
        }

        public async Task RemoveExpiredAsync(CancellationToken cancel = default)
        {
            var removed = (long)(await Commands.UseAsync(Operation.RemoveExpired, cmd =>
            {
                cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.Ticks);
                return cmd.ExecuteScalarAsync(cancel);
            }))!;

            if (removed > 0)
            {
                _logger.LogTrace("Evicted {DeletedCacheEntryCount} expired entries from cache", removed);
            }
        }
    }
}

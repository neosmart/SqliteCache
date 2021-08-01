using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using DbConnection = Microsoft.Data.Sqlite.SqliteConnection;
using DbCommand = Microsoft.Data.Sqlite.SqliteCommand;
using System.Diagnostics;

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
        private DbConnection _db;

        private DbCommandPool Commands { get; set; }

        static SqliteCache()
        {
            SQLitePCL.Batteries.Init();
        }

        public SqliteCache(IOptions<SqliteCacheOptions> options, ILogger<SqliteCache>? logger = null)
            : this(options.Value, logger)
        {
        }

        public SqliteCache(SqliteCacheOptions options, ILogger<SqliteCache>? logger = null)
        {
            _config = options;
            _logger = logger ?? new NullLogger<SqliteCache>();

            // Silence warnings about variables not initialized in constructor because they ARE
            // initialized in the call to `Connect()`.
            _db = null!;
            Commands = null!;
            Connect();

            // Directly checking _db/Commands will cause Roslyn to think they may be null
            var x = _db;
            Debug.Assert(x != null);
            var y = Commands;
            Debug.Assert(y != null);

            // This has to be after the call to Connect()
            if (_config.CleanupInterval.HasValue)
            {
                _cleanupTimer = new Timer(_ =>
                {
                    _logger.LogTrace("Beginning background cache cleanup");
                    RemoveExpired();
                }, null, TimeSpan.Zero, _config.CleanupInterval.Value);
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            Commands?.Dispose();
            _db?.Close();
            _db?.Dispose();
        }

#if NETCOREAPP3_0_OR_GREATER
        public async ValueTask DisposeAsync()
        {
            if (_cleanupTimer is not null)
            {
                await _cleanupTimer.DisposeAsync();
            }

            if (Commands is not null)
            {
                await Commands.DisposeAsync();
            }

            if (_db is not null)
            {
                await _db.CloseAsync();
                await _db.DisposeAsync();
            }
        }
#endif

        #region Database Connection Initialization
        private bool CheckExistingDb(DbConnection db)
        {
            try
            {
                // Check for correct structure
                using (var cmd = new DbCommand(@"SELECT COUNT(*) from sqlite_master", db))
                {
                    var result = (long)cmd.ExecuteScalar();
                    // We are expecting two tables and one additional index
                    if (result != 3)
                    {
                        _logger.LogWarning("Incorrect/incompatible existing cache db structure found!");
                        return false;
                    }
                }

                // Check for correct version
                using (var cmd = new DbCommand(@"SELECT value FROM meta WHERE key = ""version""", db))
                {
                    var result = (long)cmd.ExecuteScalar();
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

        private void Connect()
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

                    var db = new DbConnection(_config.ConnectionString);
                    db.Open();
                    if (CheckExistingDb(db))
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
                    _db.Open();
                    Initialize();
                }

                Commands = new DbCommandPool(_db, _logger);

                // Explicitly set default journal mode and fsync behavior
                using (var cmd = new DbCommand("PRAGMA journal_mode = WAL;", _db))
                {
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = new DbCommand("PRAGMA synchronous = NORMAL;", _db))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void Initialize()
        {
            _logger.LogInformation("Initializing db cache: {ConnectionString}",
                _config.ConnectionString);

            using (var transaction = _db.BeginTransaction())
            {
                using (var cmd = new DbCommand(Resources.TableInitCommand, _db))
                {
                    cmd.Transaction = transaction;
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = new DbCommand(
                    $"INSERT INTO meta (key, value) " +
                    $"VALUES " +
                    $@"(""version"", {SchemaVersion}), " +
                    $@"(""created"", {DateTimeOffset.UtcNow.Ticks})", _db))
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
                using (var cmd = new DbCommand(@"SELECT value FROM meta WHERE key = ""version""", db))
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
                    $@"(""version"", {SchemaVersion}), " +
                    $@"(""created"", {DateTimeOffset.UtcNow.Ticks})" , _db))
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
            return (byte[]) Commands.Use(Operation.Get, cmd =>
            {
                cmd.Parameters.AddWithValue("@key", key);
                cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.Ticks);
                return cmd.ExecuteScalar();
            })!;
        }

        public async Task<byte[]> GetAsync(string key, CancellationToken cancel = default)
        {
            return (byte[]) (await Commands.UseAsync(Operation.Get, cmd =>
            {
                cmd.Parameters.AddWithValue("@key", key);
                cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.Ticks);
                return cmd.ExecuteScalarAsync(cancel)!;
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
            })!;
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

            DateTimeOffset? expiry = null;
            TimeSpan? renewal = null;

            if (options.AbsoluteExpiration.HasValue)
            {
                expiry = options.AbsoluteExpiration.Value;
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

            cmd.Parameters.AddWithValue("@expiry", expiry?.Ticks ?? (object) DBNull.Value);
            cmd.Parameters.AddWithValue("@renewal", renewal?.Ticks ?? (object) DBNull.Value);
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

        public void RemoveExpired()
        {
            var removed = (long) Commands.Use(Operation.RemoveExpired, cmd =>
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
            var removed = (long) (await Commands.UseAsync(Operation.RemoveExpired, cmd =>
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

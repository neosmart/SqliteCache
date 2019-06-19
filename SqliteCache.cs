using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

using DbConnectionStringBuilder = Microsoft.Data.Sqlite.SqliteConnectionStringBuilder;
using DbConnection = Microsoft.Data.Sqlite.SqliteConnection;
using DbCommand = Microsoft.Data.Sqlite.SqliteCommand;
using DbDataReader = Microsoft.Data.Sqlite.SqliteDataReader;

namespace NeoSmart.SqliteCache
{
    public class SqliteCache : IDistributedCache, IDisposable
    {
        public const int SchemaVersion = 1;

        private readonly Configuration _config;
        private readonly ILogger _logger;
        private DbCommandPool _cachedDbCommands;
        private DbCommandPool Commands => _cachedDbCommands;

        private DbConnection _db;

        public SqliteCache(Configuration configuration, ILogger<SqliteCache> logger)
        {
            _config = configuration;
            _logger = logger;
        }

        #region Database Initialization
        private string ConnectionString
        {
            get
            {
                var sb = new DbConnectionStringBuilder();
                sb.DataSource = _config.MemoryOnly
                    ? ":memory:" : _config.CachePath;
                sb.Mode = _config.MemoryOnly
                    ? SqliteOpenMode.Memory : SqliteOpenMode.ReadWriteCreate;

                return sb.ConnectionString;
            }
        }

        public void Dispose()
        {
            _cachedDbCommands?.Dispose();
            _db?.Close();
            _db?.Dispose();
        }

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
                var connectionString = ConnectionString;
                _logger.LogDebug("Opening connection to SQLite database: " +
                    "{ConnectionString}", connectionString);

                // First try to open an existing database
                if (!_config.MemoryOnly && System.IO.File.Exists(_config.CachePath))
                {
                    _logger.LogTrace("Found existing database at {CachePath}", _config.CachePath);

                    var db = new SqliteConnection(ConnectionString);
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

                        _logger.LogDebug("Deleting existing incompatible cache db file {CachePath}", _config.CachePath);
                        System.IO.File.Delete(_config.CachePath);
                    }
                }

                if (_db == null)
                {
                    _db = new DbConnection(ConnectionString);
                    await _db.OpenAsync();
                    await InitializeAsync(cancel);
                }

                _cachedDbCommands = new DbCommandPool(_db, _logger);
            }
        }

        private async Task InitializeAsync(CancellationToken cancel)
        {
            _logger.LogInformation("Initializing db cache");

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
        #endregion

        public byte[] Get(string key)
        {
            return Commands.Use(Operation.Get, cmd =>
            {
                cmd.Parameters.AddWithValue("@key", key);
                cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.Ticks);
                return (byte[])cmd.ExecuteScalar();
            });
        }

        public async Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            return (byte[]) await Commands.UseAsync(Operation.Get, cmd =>
            {
                cmd.Parameters.AddWithValue("@key", key);
                cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.Ticks);
                return cmd.ExecuteScalarAsync();
            });
        }

        public void Refresh(string key)
        {
            throw new NotImplementedException();
        }

        public Task RefreshAsync(string key, CancellationToken cancel = default)
        {
            throw new NotImplementedException();
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

            long? expiry = null;
            long? renewal = null;

            if (options.AbsoluteExpiration.HasValue)
            {
                expiry = options.AbsoluteExpiration.Value.Ticks;
            }
            else if (options.AbsoluteExpirationRelativeToNow.HasValue)
            {
                expiry = DateTimeOffset.UtcNow
                    .Add(options.AbsoluteExpirationRelativeToNow.Value)
                    .Ticks;
            }
            else if (options.SlidingExpiration.HasValue)
            {
                renewal = options.SlidingExpiration.Value.Ticks;
                expiry = DateTimeOffset.UtcNow.Ticks + renewal;
            }

            cmd.Parameters.AddWithValue("@expiry", (object) expiry ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@renewal", (object) renewal ?? DBNull.Value);
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
    }
}

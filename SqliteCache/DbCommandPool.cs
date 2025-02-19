using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace NeoSmart.Caching.Sqlite
{
    class DbCommandPool : IDisposable
#if NETCOREAPP3_1_OR_GREATER
        , IAsyncDisposable
#endif
    {
        /// <summary>
        /// Number of connections to open to the database at startup. Ramps up as concurrency increases.
        /// </summary>
        private const int InitialConcurrency = 4;
        private readonly ILogger _logger;
        private readonly ConcurrentBag<SqliteCommand>[] _commands = new ConcurrentBag<SqliteCommand>[DbCommands.Count];
        private readonly ConcurrentBag<SqliteConnection> _connections = new ConcurrentBag<SqliteConnection>();
        private readonly string _connectionString;

        public DbCommandPool(SqliteConnection db, ILogger logger)
        {
            _connectionString = db.ConnectionString;
            _logger = logger;

            _logger.LogTrace("Initializing db command pool");
            for (int i = 0; i < _commands.Length; ++i)
            {
                _commands[i] = new ConcurrentBag<SqliteCommand>();
            }

            _logger.LogTrace("Creating {InitialConnections} initial connections in the pool", InitialConcurrency);
            for (int i = 0; i < InitialConcurrency; ++i)
            {
                var connection = new SqliteConnection(_connectionString);
                _logger.LogTrace("Opening connection to {SqliteCacheDbPath}", _connectionString);
                connection.Open();
                _connections.Add(connection);
            }
        }

        public void Use(Operation type, Action<SqliteCommand> handler)
        {
            Use<bool>(type, (cmd) =>
            {
                handler(cmd);
                return true;
            });
        }

        public R Use<R>(Func<SqliteConnection, R> handler)
        {
            if (!_connections.TryTake(out var db))
            {
                _logger.LogTrace("Adding a new connection to the connection pool");
                db = new SqliteConnection(_connectionString);
                _logger.LogTrace("Opening connection to {SqliteCacheDbPath}", _connectionString);
                db.Open();
            }

            try
            {
                return handler(db);
            }
            finally
            {
                _connections.Add(db);
            }
        }

        public R Use<R>(Operation type, Func<SqliteCommand, R> handler)
        {
            return Use((conn) =>
            {
                var pool = _commands[(int)type];
                if (!pool.TryTake(out var command))
                {
                    _logger.LogTrace("Adding a new {DbCommand} command to the command pool", type);
                    command = new SqliteCommand(DbCommands.Commands[(int)type], conn);
                }

                try
                {
                    command.Connection = conn;
                    return handler(command);
                }
                finally
                {
                    command.Connection = null;
                    command.Parameters.Clear();
                    pool.Add(command);
                }
            });
        }

        public async Task<R> UseAsync<R>(Func<SqliteConnection, Task<R>> handler)
        {
            if (!_connections.TryTake(out var db))
            {
                _logger.LogTrace("Adding a new connection to the connection pool");
                db = new SqliteConnection(_connectionString);
                _logger.LogTrace("Opening connection to {SqliteCacheDbPath}", _connectionString);
                await db.OpenAsync().ConfigureAwait(false);
            }

            try
            {
                return await handler(db).ConfigureAwait(false);
            }
            finally
            {
                _connections.Add(db);
            }
        }

        public Task<R> UseAsync<R>(Operation type, Func<SqliteCommand, Task<R>> handler)
        {
            return UseAsync(async (conn) =>
            {
                var pool = _commands[(int)type];
                if (!pool.TryTake(out var command))
                {
                    _logger.LogTrace("Adding a new {DbCommand} command to the command pool", type);
                    command = new SqliteCommand(DbCommands.Commands[(int)type], conn);
                }

                try
                {
                    command.Connection = conn;
                    return await handler(command).ConfigureAwait(false);
                }
                finally
                {
                    command.Connection = null;
                    command.Parameters.Clear();
                    pool.Add(command);
                }
            });
        }

        public void Dispose()
        {
            foreach (var pool in _commands)
            {
                while (pool.TryTake(out var cmd))
                {
                    cmd.Dispose();
                }
            }

            foreach (var conn in _connections)
            {
                _logger.LogTrace("Closing connection to {SqliteCacheDbPath}", _connectionString);
                conn.Close();
                conn.Dispose();
            }
        }

#if NETCOREAPP3_1_OR_GREATER
        public async ValueTask DisposeAsync()
        {
            foreach (var pool in _commands)
            {
                while (pool.TryTake(out var cmd))
                {
                    await cmd.DisposeAsync().ConfigureAwait(false);
                }
            }

            foreach (var conn in _connections)
            {
                await conn.CloseAsync().ConfigureAwait(false);
                await conn.DisposeAsync().ConfigureAwait(false);
            }
        }
#endif
    }
}

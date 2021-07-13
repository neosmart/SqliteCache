using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace NeoSmart.Caching.Sqlite
{
    class DbCommandPool : IDisposable
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

            _logger.LogTrace("Creating {InitialConnections} initial connections in the pool");
            for (int i = 0; i < InitialConcurrency; ++i)
            {
                var connection = new SqliteConnection(_connectionString);
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

        public R Use<R>(Operation type, Func<SqliteCommand, R> handler)
        {
            if (!_connections.TryTake(out var db))
            {
                _logger.LogTrace("Adding a new connection to the connection pool", type);
                db = new SqliteConnection(_connectionString);
            }

            var pool = _commands[(int)type];
            if (!pool.TryTake(out var command))
            {
                _logger.LogTrace("Adding a new {DbCommand} command to the command pool", type);
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
                command = new SqliteCommand(DbCommands.Commands[(int)type], db);
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
            }

            try
            {
                command.Connection = db;
                return handler(command);
            }
            finally
            {
                command.Parameters.Clear();
                pool.Add(command);
                _connections.Add(db);
            }
        }

        public async Task<R> UseAsync<R>(Operation type, Func<SqliteCommand, Task<R>> handler)
        {
            if (!_connections.TryTake(out var db))
            {
                _logger.LogTrace("Adding a new connection to the connection pool", type);
                db = new SqliteConnection(_connectionString);
            }

            var pool = _commands[(int)type];
            if (!pool.TryTake(out var command))
            {
                _logger.LogTrace("Adding a new {DbCommand} command to the command pool", type);
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
                command = new SqliteCommand(DbCommands.Commands[(int)type], db);
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
            }

            try
            {
                return await handler(command);
            }
            finally
            {
                command.Parameters.Clear();
                pool.Add(command);
                _connections.Add(db);
            }
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
                conn.Close();
                conn.Dispose();
            }
        }
    }
}

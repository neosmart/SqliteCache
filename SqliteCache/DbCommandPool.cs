using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace NeoSmart.Caching.Sqlite
{
    class DbCommandPool : IDisposable
    {
        private readonly ILogger _logger;
        private readonly ConcurrentBag<SqliteCommand>[] _pools = new ConcurrentBag<SqliteCommand>[DbCommands.Count];
        private readonly SqliteConnection _db;

        public DbCommandPool(SqliteConnection db, ILogger logger)
        {
            _db = db;
            _logger = logger;

            _logger.LogTrace("Initializing db command pool");
            for (int i = 0; i < _pools.Length; ++i)
            {
                _pools[i] = new ConcurrentBag<SqliteCommand>();
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
            var pool = _pools[(int)type];

            if (!pool.TryTake(out var command))
            {
                _logger.LogTrace("Adding a new {DbCommand} command to the command pool", type);
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
                command = new SqliteCommand(DbCommands.Commands[(int)type], _db);
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
            }

            try
            {
                return handler(command);
            }
            finally
            {
                command.Parameters.Clear();
                pool.Add(command);
            }
        }

        public async Task<R> UseAsync<R>(Operation type, Func<SqliteCommand, Task<R>> handler)
        {
            var pool = _pools[(int)type];

            if (!pool.TryTake(out var command))
            {
                _logger.LogTrace("Adding a new {DbCommand} command to the command pool", type);
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
                command = new SqliteCommand(DbCommands.Commands[(int)type], _db);
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
            }
        }

        public void Dispose()
        {
            foreach (var pool in _pools)
            {
                while (pool.TryTake(out var cmd))
                {
                    cmd.Dispose();
                }
            }
        }
    }
}

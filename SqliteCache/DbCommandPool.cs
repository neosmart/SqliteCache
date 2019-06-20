using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using LazyCommand = System.Lazy<Microsoft.Data.Sqlite.SqliteCommand>;

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

            _logger.LogDebug("Initializing db command pool");
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
                _logger.LogDebug("Adding a new {DbCommand} command to the command pool", type);
                command = new SqliteCommand(DbCommands.Commands[(int)type], _db);
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
                _logger.LogDebug("Adding a new {DbCommand} command to the command pool", type);
                command = new SqliteCommand(DbCommands.Commands[(int)type], _db);
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

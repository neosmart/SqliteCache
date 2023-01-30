using System;

namespace NeoSmart.Caching.Sqlite
{
    enum Operation
    {
        Insert,
        Remove,
        RemoveExpired,
        Get,
        Refresh,
        BulkInsert,
    }

    static class DbCommands
    {
        public readonly static string[] Commands;
        public readonly static int Count = Enum.GetValues(typeof(Operation)).Length;

        // We have two expiry fields, that can be considered a union of these
        // two cases: (AbsoluteExpiry) and (NextExpiry, Ttl)
        const string NotExpiredClause = " (expiry IS NULL OR expiry >= @now) ";

        static DbCommands()
        {
            Commands = new string[Count];

            Commands[(int)Operation.Insert] =
                "INSERT OR REPLACE INTO cache (key, value, expiry, renewal) " +
                "VALUES (@key, @value, @expiry, @renewal)";

            Commands[(int)Operation.Refresh] =
                $"UPDATE cache " +
                $"SET expiry = (@now + renewal) " +
                $"WHERE " +
                $"  key = @key " +
                $"  AND expiry >= @now " +
                $"  AND renewal IS NOT NULL;";

            Commands[(int)Operation.Get] =
                // Get an unexpired item from the cache
                $"SELECT value FROM cache " +
                $"  WHERE key = @key " +
                $"  AND {NotExpiredClause};" +
                // And update the expiry if it is unexpired and has a renewal
                Commands[(int)Operation.Refresh];

            Commands[(int)Operation.Remove] =
                "DELETE FROM cache " +
                "  WHERE key = @key";

            Commands[(int)Operation.RemoveExpired] =
                "DELETE FROM cache " +
                $"  WHERE NOT {NotExpiredClause};" +
                $"SELECT CHANGES();";

            Commands[(int)Operation.BulkInsert] =
                "INSERT OR REPLACE INTO cache (key, value, expiry, renewal) VALUES ";

#if DEBUG
            for (int i = 0; i < Count; ++i)
            {
                if (string.IsNullOrEmpty(Commands[i]))
                {
                    throw new Exception("Missing SQLite command for operation " + Enum.GetName(typeof(Operation), i));
                }
            }
#endif
        }
    }
}

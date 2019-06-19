using System;
using System.Collections.Generic;
using System.Text;

namespace NeoSmart.SqliteCache
{
    public enum Operation
    {
        Insert,
        Remove,
        RemoveExpired,
        Get
    }

    static class DbCommands
    {
        public readonly static string[] Commands;
        public readonly static int Count = Enum.GetValues(typeof(Operation)).Length;

        // We have two expiry fields, that can be considered a union of these
        // two cases: (AbsoluteExpiry) and (LastTouch, Ttl)
        const string NotExpiredClause =
            "  (expiry IS NULL " +
            "    OR (expiry2 IS NULL AND expiry >= @now) " +
            "    OR (expiry + expiry2 >= @now)" +
            "  )";

        static DbCommands()
        {
            Commands = new string[Count];

            Commands[(int) Operation.Insert] =
                "INSERT OR REPLACE INTO cache (key, value, expiry, expiry2) " +
                "VALUES (@key, @value, @expiry, @expiry2)";

            Commands[(int) Operation.Get] =
                "SELECT value FROM cache " +
                "  WHERE key = @key " +
                $"  AND {NotExpiredClause} " +
                $"LIMIT 1";

            Commands[(int) Operation.Remove] =
                "DELETE FROM cache " +
                "  WHERE key = @key " +
                "  LIMIT 1";

            Commands[(int)Operation.RemoveExpired] =
                "DELETE FROM cache " +
                $"  WHERE NOT {NotExpiredClause}";
        }
    }
}

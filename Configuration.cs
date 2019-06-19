using System;
using System.Collections.Generic;
using System.Text;

namespace NeoSmart.SqliteCache
{
    public class Configuration
    {
        /// <summary>
        /// Takes precedence over <see cref="CachePath"/>
        /// </summary>
        public bool MemoryOnly { get; set; }
        /// <summary>
        /// Only if <see cref="MemoryOnly" is <c>false</c> />
        /// </summary>
        public string CachePath { get; set; }
    }
}
